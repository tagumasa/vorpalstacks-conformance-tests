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
        var roleName = TestRunner.MakeUniqueName("CSTSTestRole");

        var trustPolicy = @"{
            ""Version"": ""2012-10-17"",
            ""Statement"": [{
                ""Effect"": ""Allow"",
                ""Principal"": {""AWS"": ""arn:aws:iam::000000000000:root""},
                ""Action"": ""sts:AssumeRole""
            }]
        }";

        try
        {
            try
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = roleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            }
            catch { }

        results.Add(await runner.RunTestAsync("sts", "GetCallerIdentity", async () =>
        {
            var resp = await stsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest());
            if (resp.Arn == null)
                throw new Exception("Arn is null");
        }));

        results.Add(await runner.RunTestAsync("sts", "GetSessionToken", async () =>
        {
            var resp = await stsClient.GetSessionTokenAsync(new GetSessionTokenRequest());
            if (resp.Credentials == null)
                throw new Exception("Credentials is null");
        }));

        results.Add(await runner.RunTestAsync("sts", "AssumeRole", async () =>
        {
            var resp = await stsClient.AssumeRoleAsync(new AssumeRoleRequest
            {
                RoleArn = $"arn:aws:iam::000000000000:role/{roleName}",
                RoleSessionName = "test-session"
            });
            if (resp.Credentials == null)
                throw new Exception("Credentials is null");
        }));

        results.Add(await runner.RunTestAsync("sts", "AssumeRole_NonExistentRole", async () =>
        {
            try
            {
                await stsClient.AssumeRoleAsync(new AssumeRoleRequest
                {
                    RoleArn = "arn:aws:iam::000000000000:role/NonExistentRole_xyz_12345",
                    RoleSessionName = "test-session"
                });
                throw new Exception("Expected error but got none");
            }
            catch (AmazonSecurityTokenServiceException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("sts", "GetCallerIdentity_ContentVerify", async () =>
        {
            var resp = await stsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest());
            if (string.IsNullOrEmpty(resp.Account))
                throw new Exception("account is nil or empty");
            if (string.IsNullOrEmpty(resp.Arn))
                throw new Exception("ARN is nil or empty");
            if (string.IsNullOrEmpty(resp.UserId))
                throw new Exception("user ID is nil or empty");
        }));

        results.Add(await runner.RunTestAsync("sts", "GetSessionToken_ContentVerify", async () =>
        {
            var resp = await stsClient.GetSessionTokenAsync(new GetSessionTokenRequest
            {
                DurationSeconds = 3600
            });
            if (resp.Credentials == null)
                throw new Exception("credentials is nil");
            if (string.IsNullOrEmpty(resp.Credentials.AccessKeyId))
                throw new Exception("access key ID is nil or empty");
            if (string.IsNullOrEmpty(resp.Credentials.SecretAccessKey))
                throw new Exception("secret access key is nil or empty");
            if (string.IsNullOrEmpty(resp.Credentials.SessionToken))
                throw new Exception("session token is nil or empty");
            if (resp.Credentials.Expiration == null)
                throw new Exception("expiration is null");
        }));
        }
        finally
        {
            try
            {
                await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = roleName });
            }
            catch { }
        }

        return results;
    }
}
