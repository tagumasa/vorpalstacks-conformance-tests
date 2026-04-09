using System.Text;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static class STSServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonSecurityTokenServiceClient stsClient,
        AmazonIdentityManagementServiceClient iamClient,
        string region)
    {
        var results = new List<TestResult>();
        var suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1000000;
        var roleName = $"CSTestRole-{suffix}";
        var samlRoleName = $"CSTSAMLRole-{suffix}";
        var webIdRoleName = $"CSTWebIdRole-{suffix}";

        var trustPolicy = @"{
            ""Version"": ""2012-10-17"",
            ""Statement"": [{
                ""Effect"": ""Allow"",
                ""Principal"": {""AWS"": ""arn:aws:iam::000000000000:root""},
                ""Action"": ""sts:AssumeRole""
            }]
        }";

        var samlTrustPolicy = @"{
            ""Version"": ""2012-10-17"",
            ""Statement"": [{
                ""Effect"": ""Allow"",
                ""Principal"": {""Federated"": ""arn:aws:iam::000000000000:saml-provider/TestProvider""},
                ""Action"": ""sts:AssumeRoleWithSAML""
            }]
        }";

        var webIdTrustPolicy = @"{
            ""Version"": ""2012-10-17"",
            ""Statement"": [{
                ""Effect"": ""Allow"",
                ""Principal"": {""Federated"": ""arn:aws:iam::000000000000:oidc-provider/example.com""},
                ""Action"": ""sts:AssumeRoleWithWebIdentity""
            }]
        }";

        try
        {
            foreach (var rn in new[] { roleName, samlRoleName, webIdRoleName })
            {
                try { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = rn, AssumeRolePolicyDocument = rn == roleName ? trustPolicy : rn == samlRoleName ? samlTrustPolicy : webIdTrustPolicy }); } catch { }
            }

            results.Add(await runner.RunTestAsync("sts", "GetCallerIdentity", async () =>
            {
                var resp = await stsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest());
                if (resp.UserId == null) throw new Exception("UserId is null");
            }));

            results.Add(await runner.RunTestAsync("sts", "GetSessionToken", async () =>
            {
                var resp = await stsClient.GetSessionTokenAsync(new GetSessionTokenRequest());
                if (resp.Credentials == null) throw new Exception("credentials is null");
            }));

            results.Add(await runner.RunTestAsync("sts", "AssumeRole", async () =>
            {
                var resp = await stsClient.AssumeRoleAsync(new AssumeRoleRequest
                {
                    RoleArn = $"arn:aws:iam::000000000000:role/{roleName}",
                    RoleSessionName = "TestSession"
                });
                if (resp.Credentials == null) throw new Exception("credentials is null");
            }));

            results.Add(await runner.RunTestAsync("sts", "AssumeRole_NonExistentRole", async () =>
            {
                try
                {
                    await stsClient.AssumeRoleAsync(new AssumeRoleRequest
                    {
                        RoleArn = "arn:aws:iam::000000000000:role/NonExistentRole",
                        RoleSessionName = "TestSession"
                    });
                    throw new Exception("expected error for non-existent role");
                }
                catch (AmazonSecurityTokenServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("sts", "GetCallerIdentity_ContentVerify", async () =>
            {
                var resp = await stsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest());
                if (string.IsNullOrEmpty(resp.Account)) throw new Exception("account is nil or empty");
                if (string.IsNullOrEmpty(resp.Arn)) throw new Exception("ARN is nil or empty");
                if (string.IsNullOrEmpty(resp.UserId)) throw new Exception("user ID is nil or empty");
            }));

            results.Add(await runner.RunTestAsync("sts", "GetSessionToken_ContentVerify", async () =>
            {
                var resp = await stsClient.GetSessionTokenAsync(new GetSessionTokenRequest { DurationSeconds = 3600 });
                if (resp.Credentials == null) throw new Exception("credentials is nil");
                if (string.IsNullOrEmpty(resp.Credentials.AccessKeyId)) throw new Exception("access key ID is nil or empty");
                if (string.IsNullOrEmpty(resp.Credentials.SecretAccessKey)) throw new Exception("secret access key is nil or empty");
                if (string.IsNullOrEmpty(resp.Credentials.SessionToken)) throw new Exception("session token is nil or empty");
                if (resp.Credentials.Expiration == null) throw new Exception("expiration is null");
            }));

            results.Add(await runner.RunTestAsync("sts", "AssumeRole_ContentVerify", async () =>
            {
                var resp = await stsClient.AssumeRoleAsync(new AssumeRoleRequest
                {
                    RoleArn = $"arn:aws:iam::000000000000:role/{roleName}",
                    RoleSessionName = "VerifySession"
                });
                if (resp.Credentials == null) throw new Exception("credentials is nil");
                if (string.IsNullOrEmpty(resp.Credentials.AccessKeyId)) throw new Exception("access key ID is nil or empty");
                if (string.IsNullOrEmpty(resp.Credentials.SecretAccessKey)) throw new Exception("secret access key is nil or empty");
                if (string.IsNullOrEmpty(resp.Credentials.SessionToken)) throw new Exception("session token is nil or empty");
                if (resp.Credentials.Expiration == null) throw new Exception("expiration is null");
                if (resp.AssumedRoleUser == null) throw new Exception("assumed role user is nil");
                if (string.IsNullOrEmpty(resp.AssumedRoleUser.AssumedRoleId)) throw new Exception("assumed role ID is nil or empty");
                if (string.IsNullOrEmpty(resp.AssumedRoleUser.Arn)) throw new Exception("assumed role user ARN is nil or empty");
            }));

            results.Add(await runner.RunTestAsync("sts", "AssumeRole_WithSourceIdentity", async () =>
            {
                var resp = await stsClient.AssumeRoleAsync(new AssumeRoleRequest
                {
                    RoleArn = $"arn:aws:iam::000000000000:role/{roleName}",
                    RoleSessionName = "SourceIdSession",
                    SourceIdentity = "AdminUser"
                });
                if (resp.SourceIdentity != "AdminUser") throw new Exception($"SourceIdentity mismatch, got: {resp.SourceIdentity}");
            }));

            results.Add(await runner.RunTestAsync("sts", "AssumeRole_WithPolicy", async () =>
            {
                var inlinePolicy = @"{""Version"":""2012-10-17"",""Statement"":[{""Effect"":""Allow"",""Action"":""s3:GetObject"",""Resource"":""*""}]}";
                var resp = await stsClient.AssumeRoleAsync(new AssumeRoleRequest
                {
                    RoleArn = $"arn:aws:iam::000000000000:role/{roleName}",
                    RoleSessionName = "PolicySession",
                    Policy = inlinePolicy
                });
                if (resp.PackedPolicySize <= 0) throw new Exception($"PackedPolicySize should be > 0, got: {resp.PackedPolicySize}");
            }));

            results.Add(await runner.RunTestAsync("sts", "AssumeRole_InvalidDuration", async () =>
            {
                try
                {
                    await stsClient.AssumeRoleAsync(new AssumeRoleRequest
                    {
                        RoleArn = $"arn:aws:iam::000000000000:role/{roleName}",
                        RoleSessionName = "DurationSession",
                        DurationSeconds = 100
                    });
                    throw new Exception("expected error for duration < 900");
                }
                catch (AmazonSecurityTokenServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("sts", "AssumeRole_EmptySessionName", async () =>
            {
                try
                {
                    await stsClient.AssumeRoleAsync(new AssumeRoleRequest
                    {
                        RoleArn = $"arn:aws:iam::000000000000:role/{roleName}",
                        RoleSessionName = ""
                    });
                    throw new Exception("expected error for empty session name");
                }
                catch (AmazonSecurityTokenServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("sts", "AssumeRoleWithSAML_Basic", async () =>
            {
                var resp = await stsClient.AssumeRoleWithSAMLAsync(new AssumeRoleWithSAMLRequest
                {
                    RoleArn = $"arn:aws:iam::000000000000:role/{samlRoleName}",
                    PrincipalArn = "arn:aws:iam::000000000000:saml-provider/TestProvider",
                    SAMLAssertion = "VGhpcyBpcyBhIGR1bW15IFNBTUwgYXNzZXJ0aW9u"
                });
                if (resp.Credentials == null) throw new Exception("credentials is nil");
            }));

            results.Add(await runner.RunTestAsync("sts", "AssumeRoleWithSAML_ContentVerify", async () =>
            {
                var resp = await stsClient.AssumeRoleWithSAMLAsync(new AssumeRoleWithSAMLRequest
                {
                    RoleArn = $"arn:aws:iam::000000000000:role/{samlRoleName}",
                    PrincipalArn = "arn:aws:iam::000000000000:saml-provider/TestProvider",
                    SAMLAssertion = "VGhpcyBpcyBhIGR1bW15IFNBTUwgYXNzZXJ0aW9u"
                });
                if (resp.Credentials == null || string.IsNullOrEmpty(resp.Credentials.AccessKeyId)) throw new Exception("credentials or access key ID is nil");
                if (resp.Credentials.Expiration == null) throw new Exception("expiration is null");
                if (resp.AssumedRoleUser == null || string.IsNullOrEmpty(resp.AssumedRoleUser.AssumedRoleId)) throw new Exception("assumed role user is nil");
                if (string.IsNullOrEmpty(resp.Subject)) throw new Exception("subject is nil or empty");
                if (string.IsNullOrEmpty(resp.SubjectType)) throw new Exception("subject type is nil or empty");
                if (string.IsNullOrEmpty(resp.Issuer)) throw new Exception("issuer is nil or empty");
            }));

            results.Add(await runner.RunTestAsync("sts", "AssumeRoleWithSAML_WithPolicy", async () =>
            {
                var inlinePolicy = @"{""Version"":""2012-10-17"",""Statement"":[{""Effect"":""Allow"",""Action"":""*"",""Resource"":""*""}]}";
                var resp = await stsClient.AssumeRoleWithSAMLAsync(new AssumeRoleWithSAMLRequest
                {
                    RoleArn = $"arn:aws:iam::000000000000:role/{samlRoleName}",
                    PrincipalArn = "arn:aws:iam::000000000000:saml-provider/TestProvider",
                    SAMLAssertion = "VGhpcyBpcyBhIGR1bW15IFNBTUwgYXNzZXJ0aW9u",
                    Policy = inlinePolicy
                });
                if (resp.PackedPolicySize <= 0) throw new Exception($"PackedPolicySize should be > 0, got: {resp.PackedPolicySize}");
            }));

            results.Add(await runner.RunTestAsync("sts", "AssumeRoleWithSAML_InvalidAssertion", async () =>
            {
                try
                {
                    await stsClient.AssumeRoleWithSAMLAsync(new AssumeRoleWithSAMLRequest
                    {
                        RoleArn = $"arn:aws:iam::000000000000:role/{samlRoleName}",
                        PrincipalArn = "arn:aws:iam::000000000000:saml-provider/TestProvider",
                        SAMLAssertion = ""
                    });
                    throw new Exception("expected error for empty SAML assertion");
                }
                catch (AmazonSecurityTokenServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("sts", "AssumeRoleWithSAML_NonExistentRole", async () =>
            {
                try
                {
                    await stsClient.AssumeRoleWithSAMLAsync(new AssumeRoleWithSAMLRequest
                    {
                        RoleArn = "arn:aws:iam::000000000000:role/NonExistentSAMLRole",
                        PrincipalArn = "arn:aws:iam::000000000000:saml-provider/TestProvider",
                        SAMLAssertion = "VGhpcyBpcyBhIGR1bW15IFNBTUwgYXNzZXJ0aW9u"
                    });
                    throw new Exception("expected error for non-existent role");
                }
                catch (AmazonSecurityTokenServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("sts", "AssumeRoleWithWebIdentity_Basic", async () =>
            {
                var resp = await stsClient.AssumeRoleWithWebIdentityAsync(new AssumeRoleWithWebIdentityRequest
                {
                    RoleArn = $"arn:aws:iam::000000000000:role/{webIdRoleName}",
                    RoleSessionName = "WebIdSession",
                    WebIdentityToken = "dummy-web-identity-token",
                    ProviderId = "example.com"
                });
                if (resp.Credentials == null) throw new Exception("credentials is nil");
            }));

            results.Add(await runner.RunTestAsync("sts", "AssumeRoleWithWebIdentity_ContentVerify", async () =>
            {
                var resp = await stsClient.AssumeRoleWithWebIdentityAsync(new AssumeRoleWithWebIdentityRequest
                {
                    RoleArn = $"arn:aws:iam::000000000000:role/{webIdRoleName}",
                    RoleSessionName = "WebIdVerifySession",
                    WebIdentityToken = "dummy-web-identity-token",
                    ProviderId = "example.com"
                });
                if (resp.Credentials == null || string.IsNullOrEmpty(resp.Credentials.AccessKeyId)) throw new Exception("credentials or access key ID is nil");
                if (resp.Credentials.Expiration == null) throw new Exception("expiration is null");
                if (resp.AssumedRoleUser == null || string.IsNullOrEmpty(resp.AssumedRoleUser.AssumedRoleId)) throw new Exception("assumed role user is nil");
                if (string.IsNullOrEmpty(resp.SubjectFromWebIdentityToken)) throw new Exception("subject from web identity token is nil or empty");
                if (string.IsNullOrEmpty(resp.Audience)) throw new Exception("audience is nil or empty");
            }));

            results.Add(await runner.RunTestAsync("sts", "AssumeRoleWithWebIdentity_WithPolicy", async () =>
            {
                var inlinePolicy = @"{""Version"":""2012-10-17"",""Statement"":[{""Effect"":""Allow"",""Action"":""dynamodb:Query"",""Resource"":""*""}]}";
                var resp = await stsClient.AssumeRoleWithWebIdentityAsync(new AssumeRoleWithWebIdentityRequest
                {
                    RoleArn = $"arn:aws:iam::000000000000:role/{webIdRoleName}",
                    RoleSessionName = "WebIdPolicySession",
                    WebIdentityToken = "dummy-web-identity-token",
                    ProviderId = "example.com",
                    Policy = inlinePolicy
                });
                if (resp.PackedPolicySize <= 0) throw new Exception($"PackedPolicySize should be > 0, got: {resp.PackedPolicySize}");
            }));

            results.Add(await runner.RunTestAsync("sts", "AssumeRoleWithWebIdentity_EmptyToken", async () =>
            {
                try
                {
                    await stsClient.AssumeRoleWithWebIdentityAsync(new AssumeRoleWithWebIdentityRequest
                    {
                        RoleArn = $"arn:aws:iam::000000000000:role/{webIdRoleName}",
                        RoleSessionName = "WebIdSession",
                        WebIdentityToken = "",
                        ProviderId = "example.com"
                    });
                    throw new Exception("expected error for empty web identity token");
                }
                catch (AmazonSecurityTokenServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("sts", "AssumeRoleWithWebIdentity_NonExistentRole", async () =>
            {
                try
                {
                    await stsClient.AssumeRoleWithWebIdentityAsync(new AssumeRoleWithWebIdentityRequest
                    {
                        RoleArn = "arn:aws:iam::000000000000:role/NonExistentWebIdRole",
                        RoleSessionName = "WebIdSession",
                        WebIdentityToken = "dummy-token",
                        ProviderId = "example.com"
                    });
                    throw new Exception("expected error for non-existent role");
                }
                catch (AmazonSecurityTokenServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("sts", "DecodeAuthorizationMessage_Basic", async () =>
            {
                var originalMsg = @"{""ErrorCode"":""AccessDenied"",""Message"":""Not authorized""}";
                var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(originalMsg));
                var resp = await stsClient.DecodeAuthorizationMessageAsync(new DecodeAuthorizationMessageRequest { EncodedMessage = encoded });
                if (resp.DecodedMessage != originalMsg) throw new Exception($"decoded message mismatch, got: {resp.DecodedMessage}");
            }));

            results.Add(await runner.RunTestAsync("sts", "DecodeAuthorizationMessage_PlainText", async () =>
            {
                var originalMsg = "Plain text error message";
                var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(originalMsg));
                var resp = await stsClient.DecodeAuthorizationMessageAsync(new DecodeAuthorizationMessageRequest { EncodedMessage = encoded });
                if (resp.DecodedMessage != originalMsg) throw new Exception($"decoded message mismatch, got: {resp.DecodedMessage}");
            }));

            results.Add(await runner.RunTestAsync("sts", "DecodeAuthorizationMessage_InvalidBase64", async () =>
            {
                try
                {
                    await stsClient.DecodeAuthorizationMessageAsync(new DecodeAuthorizationMessageRequest { EncodedMessage = "not-valid-base64!!!" });
                    throw new Exception("expected error for invalid base64");
                }
                catch (AmazonSecurityTokenServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("sts", "DecodeAuthorizationMessage_Empty", async () =>
            {
                try
                {
                    await stsClient.DecodeAuthorizationMessageAsync(new DecodeAuthorizationMessageRequest { EncodedMessage = "" });
                    throw new Exception("expected error for empty encoded message");
                }
                catch (AmazonSecurityTokenServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("sts", "GetAccessKeyInfo_AKIAPrefix", async () =>
            {
                var resp = await stsClient.GetAccessKeyInfoAsync(new GetAccessKeyInfoRequest { AccessKeyId = "AKIAIOSFODNN7EXAMPLE" });
                if (string.IsNullOrEmpty(resp.Account)) throw new Exception("account is nil or empty");
            }));

            results.Add(await runner.RunTestAsync("sts", "GetAccessKeyInfo_ASIAPrefix", async () =>
            {
                var resp = await stsClient.GetAccessKeyInfoAsync(new GetAccessKeyInfoRequest { AccessKeyId = "ASIAIOSFODNN7EXAMPLE" });
                if (string.IsNullOrEmpty(resp.Account)) throw new Exception("account is nil or empty");
            }));

            results.Add(await runner.RunTestAsync("sts", "GetAccessKeyInfo_UnknownPrefix", async () =>
            {
                var resp = await stsClient.GetAccessKeyInfoAsync(new GetAccessKeyInfoRequest { AccessKeyId = "UNKNOWN1234567890" });
                if (string.IsNullOrEmpty(resp.Account)) throw new Exception("account is nil or empty for unknown prefix");
            }));

            results.Add(await runner.RunTestAsync("sts", "GetAccessKeyInfo_Invalid", async () =>
            {
                try
                {
                    await stsClient.GetAccessKeyInfoAsync(new GetAccessKeyInfoRequest { AccessKeyId = "" });
                    throw new Exception("expected error for empty access key");
                }
                catch (AmazonSecurityTokenServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("sts", "GetFederationToken_Basic", async () =>
            {
                var resp = await stsClient.GetFederationTokenAsync(new GetFederationTokenRequest { Name = "TestFederatedUser" });
                if (resp.Credentials == null) throw new Exception("credentials is nil");
                if (resp.FederatedUser == null) throw new Exception("federated user is nil");
            }));

            results.Add(await runner.RunTestAsync("sts", "GetFederationToken_ContentVerify", async () =>
            {
                var resp = await stsClient.GetFederationTokenAsync(new GetFederationTokenRequest { Name = "FederatedVerify" });
                if (resp.Credentials == null || string.IsNullOrEmpty(resp.Credentials.AccessKeyId)) throw new Exception("credentials or access key ID is nil");
                if (string.IsNullOrEmpty(resp.Credentials.SecretAccessKey)) throw new Exception("secret access key is nil or empty");
                if (string.IsNullOrEmpty(resp.Credentials.SessionToken)) throw new Exception("session token is nil or empty");
                if (resp.Credentials.Expiration == null) throw new Exception("expiration is null");
                if (resp.FederatedUser == null) throw new Exception("federated user is nil");
                if (string.IsNullOrEmpty(resp.FederatedUser.FederatedUserId)) throw new Exception("federated user ID is nil or empty");
                if (string.IsNullOrEmpty(resp.FederatedUser.Arn)) throw new Exception("federated user ARN is nil or empty");
            }));

            results.Add(await runner.RunTestAsync("sts", "GetFederationToken_WithPolicy", async () =>
            {
                var inlinePolicy = @"{""Version"":""2012-10-17"",""Statement"":[{""Effect"":""Allow"",""Action"":""s3:*"",""Resource"":""*""}]}";
                var resp = await stsClient.GetFederationTokenAsync(new GetFederationTokenRequest { Name = "FederatedPolicy", Policy = inlinePolicy });
                if (resp.PackedPolicySize <= 0) throw new Exception($"PackedPolicySize should be > 0, got: {resp.PackedPolicySize}");
            }));

            results.Add(await runner.RunTestAsync("sts", "GetFederationToken_InvalidName", async () =>
            {
                try
                {
                    await stsClient.GetFederationTokenAsync(new GetFederationTokenRequest { Name = "" });
                    throw new Exception("expected error for empty name");
                }
                catch (AmazonSecurityTokenServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("sts", "GetFederationToken_InvalidPolicy", async () =>
            {
                try
                {
                    await stsClient.GetFederationTokenAsync(new GetFederationTokenRequest { Name = "FederatedBadPolicy", Policy = "not-valid-json" });
                    throw new Exception("expected error for malformed policy");
                }
                catch (AmazonSecurityTokenServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("sts", "GetFederationToken_InvalidDuration", async () =>
            {
                try
                {
                    await stsClient.GetFederationTokenAsync(new GetFederationTokenRequest { Name = "FederatedBadDuration", DurationSeconds = 100 });
                    throw new Exception("expected error for duration < 900");
                }
                catch (AmazonSecurityTokenServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("sts", "GetDelegatedAccessToken_Basic", async () =>
            {
                var resp = await stsClient.GetDelegatedAccessTokenAsync(new GetDelegatedAccessTokenRequest { TradeInToken = "dummy-trade-in-token" });
                if (resp.Credentials == null) throw new Exception("credentials is nil");
            }));

            results.Add(await runner.RunTestAsync("sts", "GetDelegatedAccessToken_ContentVerify", async () =>
            {
                var resp = await stsClient.GetDelegatedAccessTokenAsync(new GetDelegatedAccessTokenRequest { TradeInToken = "dummy-trade-in-token-verify" });
                if (resp.Credentials == null || string.IsNullOrEmpty(resp.Credentials.AccessKeyId)) throw new Exception("credentials or access key ID is nil");
                if (resp.Credentials.Expiration == null) throw new Exception("expiration is null");
                if (string.IsNullOrEmpty(resp.AssumedPrincipal)) throw new Exception("assumed principal is nil or empty");
                if (resp.PackedPolicySize == null) throw new Exception("packed policy size is nil");
            }));

            results.Add(await runner.RunTestAsync("sts", "GetDelegatedAccessToken_EmptyToken", async () =>
            {
                try
                {
                    await stsClient.GetDelegatedAccessTokenAsync(new GetDelegatedAccessTokenRequest { TradeInToken = "" });
                    throw new Exception("expected error for empty trade-in token");
                }
                catch (AmazonSecurityTokenServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("sts", "GetSessionToken_ExtendedDuration", async () =>
            {
                var resp = await stsClient.GetSessionTokenAsync(new GetSessionTokenRequest { DurationSeconds = 86400 });
                if (resp.Credentials == null) throw new Exception("credentials is nil");
            }));
        }
        finally
        {
            foreach (var rn in new[] { roleName, samlRoleName, webIdRoleName })
            {
                try { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = rn }); } catch { }
            }
        }

        return results;
    }
}
