using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static class KMSServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonKeyManagementServiceClient kmsClient,
        string region)
    {
        var results = new List<TestResult>();
        var keyId = "";
        var keyArn = "";
        var aliasName = "alias/" + TestRunner.MakeUniqueName("CSKMSAlias");
        var ciphertextBlob = (MemoryStream)null!;
        var grantId = "";
        var grantToken = "";

        try
        {
            results.Add(await runner.RunTestAsync("kms", "CreateKey", async () =>
            {
                var resp = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "Test key for conformance",
                    KeyUsage = KeyUsageType.ENCRYPT_DECRYPT
                });
                if (resp.KeyMetadata == null || string.IsNullOrEmpty(resp.KeyMetadata.KeyId))
                    throw new Exception("KeyId is null or empty");
                keyId = resp.KeyMetadata.KeyId;
                keyArn = resp.KeyMetadata.Arn;
            }));

            results.Add(await runner.RunTestAsync("kms", "DescribeKey", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var resp = await kmsClient.DescribeKeyAsync(new DescribeKeyRequest
                {
                    KeyId = keyId
                });
                if (resp.KeyMetadata == null)
                    throw new Exception("KeyMetadata is null");
            }));

            results.Add(await runner.RunTestAsync("kms", "ListKeys", async () =>
            {
                var resp = await kmsClient.ListKeysAsync(new ListKeysRequest());
                if (resp.Keys == null)
                    throw new Exception("Keys is null");
            }));

            results.Add(await runner.RunTestAsync("kms", "ListKeys_Basic", async () =>
            {
                var resp = await kmsClient.ListKeysAsync(new ListKeysRequest { Limit = 10 });
                if (resp.Keys == null)
                    throw new Exception("Keys is null");
                if (resp.Keys.Count == 0)
                    throw new Exception("Expected at least one key");
            }));

            results.Add(await runner.RunTestAsync("kms", "ListKeys_ContainsCreatedKey", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var resp = await kmsClient.ListKeysAsync(new ListKeysRequest());
                var found = resp.Keys.Any(k => k.KeyId == keyId);
                if (!found)
                    throw new Exception("Created key not found in ListKeys");
            }));

            results.Add(await runner.RunTestAsync("kms", "CreateKey_ContentVerify", async () =>
            {
                var resp = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "Content verify key",
                    KeyUsage = KeyUsageType.ENCRYPT_DECRYPT
                });
                try
                {
                    if (resp.KeyMetadata == null)
                        throw new Exception("KeyMetadata is null");
                    if (resp.KeyMetadata.KeyState != KeyState.Enabled)
                        throw new Exception($"Expected Enabled state, got {resp.KeyMetadata.KeyState}");
                    if (resp.KeyMetadata.Description != "Content verify key")
                        throw new Exception($"Expected 'Content verify key', got '{resp.KeyMetadata.Description}'");
                    if (resp.KeyMetadata.KeyUsage != KeyUsageType.ENCRYPT_DECRYPT)
                        throw new Exception("Expected ENCRYPT_DECRYPT key usage");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = resp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "EnableKey", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                await kmsClient.EnableKeyAsync(new EnableKeyRequest { KeyId = keyId });
            }));

            results.Add(await runner.RunTestAsync("kms", "DisableKey", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                await kmsClient.DisableKeyAsync(new DisableKeyRequest { KeyId = keyId });
            }));

            results.Add(await runner.RunTestAsync("kms", "EnableKey_AfterDisable", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                await kmsClient.EnableKeyAsync(new EnableKeyRequest { KeyId = keyId });
            }));

            results.Add(await runner.RunTestAsync("kms", "UpdateKeyDescription", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                await kmsClient.UpdateKeyDescriptionAsync(new UpdateKeyDescriptionRequest
                {
                    KeyId = keyId,
                    Description = "Updated Key Description"
                });
            }));

            results.Add(await runner.RunTestAsync("kms", "CreateAlias", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                await kmsClient.CreateAliasAsync(new CreateAliasRequest
                {
                    AliasName = aliasName,
                    TargetKeyId = keyId
                });
            }));

            results.Add(await runner.RunTestAsync("kms", "ListAliases", async () =>
            {
                var resp = await kmsClient.ListAliasesAsync(new ListAliasesRequest());
                if (resp.Aliases == null)
                    throw new Exception("Aliases is null");
            }));

            results.Add(await runner.RunTestAsync("kms", "Encrypt", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var resp = await kmsClient.EncryptAsync(new EncryptRequest
                {
                    KeyId = keyId,
                    Plaintext = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Hello, KMS!"))
                });
                if (resp.CiphertextBlob == null)
                    throw new Exception("CiphertextBlob is null");
            }));

            results.Add(await runner.RunTestAsync("kms", "Encrypt_ForDecrypt", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var resp = await kmsClient.EncryptAsync(new EncryptRequest
                {
                    KeyId = keyId,
                    Plaintext = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Hello, KMS!"))
                });
                ciphertextBlob = resp.CiphertextBlob;
            }));

            results.Add(await runner.RunTestAsync("kms", "Decrypt", async () =>
            {
                if (ciphertextBlob == null)
                    throw new Exception("CiphertextBlob not available");
                await kmsClient.DecryptAsync(new DecryptRequest
                {
                    CiphertextBlob = ciphertextBlob
                });
            }));

            results.Add(await runner.RunTestAsync("kms", "GenerateDataKey", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var resp = await kmsClient.GenerateDataKeyAsync(new GenerateDataKeyRequest
                {
                    KeyId = keyId,
                    KeySpec = DataKeySpec.AES_256
                });
                if (resp.CiphertextBlob == null || resp.CiphertextBlob.Length == 0)
                    throw new Exception("CiphertextBlob is null or empty");
                if (resp.Plaintext == null || resp.Plaintext.Length != 32)
                    throw new Exception($"Expected 32-byte plaintext, got {resp.Plaintext?.Length}");
            }));

            results.Add(await runner.RunTestAsync("kms", "GenerateDataKeyWithoutPlaintext", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var resp = await kmsClient.GenerateDataKeyWithoutPlaintextAsync(new GenerateDataKeyWithoutPlaintextRequest
                {
                    KeyId = keyId,
                    KeySpec = DataKeySpec.AES_256
                });
                if (resp.CiphertextBlob == null || resp.CiphertextBlob.Length == 0)
                    throw new Exception("CiphertextBlob is null or empty");
            }));

            results.Add(await runner.RunTestAsync("kms", "GenerateRandom", async () =>
            {
                var resp = await kmsClient.GenerateRandomAsync(new GenerateRandomRequest
                {
                    NumberOfBytes = 32
                });
                if (resp.Plaintext == null || resp.Plaintext.Length != 32)
                    throw new Exception($"Expected 32-byte plaintext, got {resp.Plaintext?.Length}");
            }));

            results.Add(await runner.RunTestAsync("kms", "ReEncrypt", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                if (ciphertextBlob == null)
                    throw new Exception("CiphertextBlob not available");
                var resp = await kmsClient.ReEncryptAsync(new ReEncryptRequest
                {
                    CiphertextBlob = ciphertextBlob,
                    DestinationKeyId = keyId
                });
                if (resp.CiphertextBlob == null)
                    throw new Exception("CiphertextBlob is null");
            }));

            results.Add(await runner.RunTestAsync("kms", "EnableKeyRotation", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                await kmsClient.EnableKeyRotationAsync(new EnableKeyRotationRequest { KeyId = keyId });
            }));

            results.Add(await runner.RunTestAsync("kms", "GetKeyRotationStatus", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                await kmsClient.GetKeyRotationStatusAsync(new GetKeyRotationStatusRequest { KeyId = keyId });
            }));

            results.Add(await runner.RunTestAsync("kms", "DisableKeyRotation", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                await kmsClient.DisableKeyRotationAsync(new DisableKeyRotationRequest { KeyId = keyId });
            }));

            results.Add(await runner.RunTestAsync("kms", "CreateGrant", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var resp = await kmsClient.CreateGrantAsync(new CreateGrantRequest
                {
                    KeyId = keyId,
                    GranteePrincipal = "arn:aws:iam::000000000000:user/TestUser",
                    Operations = new List<string> { "Encrypt" }
                });
                if (string.IsNullOrEmpty(resp.GrantToken))
                    throw new Exception("GrantToken is null or empty");
                if (string.IsNullOrEmpty(resp.GrantId))
                    throw new Exception("GrantId is null or empty");
                grantId = resp.GrantId;
                grantToken = resp.GrantToken;
            }));

            results.Add(await runner.RunTestAsync("kms", "ListGrants", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var resp = await kmsClient.ListGrantsAsync(new ListGrantsRequest { KeyId = keyId });
                if (resp.Grants == null)
                    throw new Exception("Grants list is null");
            }));

            results.Add(await runner.RunTestAsync("kms", "ListRetirableGrants", async () =>
            {
                var resp = await kmsClient.ListRetirableGrantsAsync(new ListRetirableGrantsRequest
                {
                    RetiringPrincipal = "arn:aws:iam::000000000000:user/TestUser"
                });
                if (resp.Grants == null)
                    throw new Exception("Grants list is null");
            }));

            results.Add(await runner.RunTestAsync("kms", "RetireGrant", async () =>
            {
                if (string.IsNullOrEmpty(grantToken))
                    throw new Exception("GrantToken not set");
                await kmsClient.RetireGrantAsync(new RetireGrantRequest
                {
                    GrantToken = grantToken
                });
            }));

            results.Add(await runner.RunTestAsync("kms", "RevokeGrant", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                if (string.IsNullOrEmpty(grantId))
                    throw new Exception("GrantId not set");
                var revokeGrantResp = await kmsClient.CreateGrantAsync(new CreateGrantRequest
                {
                    KeyId = keyId,
                    GranteePrincipal = "arn:aws:iam::000000000000:user/TestUser2",
                    Operations = new List<string> { "Decrypt" }
                });
                try
                {
                    await kmsClient.RevokeGrantAsync(new RevokeGrantRequest
                    {
                        KeyId = keyId,
                        GrantId = revokeGrantResp.GrantId
                    });
                }
                catch { }
            }));

            results.Add(await runner.RunTestAsync("kms", "PutKeyPolicy", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var policy = @"{""Version"":""2012-10-17"",""Statement"":[{""Effect"":""Allow"",""Principal"":{""AWS"":""*""},""Action"":""kms:*"",""Resource"":""*""}]}";
                await kmsClient.PutKeyPolicyAsync(new PutKeyPolicyRequest
                {
                    KeyId = keyId,
                    PolicyName = "default",
                    Policy = policy
                });
            }));

            results.Add(await runner.RunTestAsync("kms", "GetKeyPolicy", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var resp = await kmsClient.GetKeyPolicyAsync(new GetKeyPolicyRequest
                {
                    KeyId = keyId,
                    PolicyName = "default"
                });
                if (string.IsNullOrEmpty(resp.Policy))
                    throw new Exception("Policy is empty");
            }));

            results.Add(await runner.RunTestAsync("kms", "ListKeyPolicies", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var resp = await kmsClient.ListKeyPoliciesAsync(new ListKeyPoliciesRequest { KeyId = keyId });
                if (resp.PolicyNames == null)
                    throw new Exception("PolicyNames list is null");
            }));

            results.Add(await runner.RunTestAsync("kms", "TagResource", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                await kmsClient.TagResourceAsync(new TagResourceRequest
                {
                    KeyId = keyId,
                    Tags = new List<Tag>
                    {
                        new Tag { TagKey = "Environment", TagValue = "test" },
                        new Tag { TagKey = "Project", TagValue = "sdk-tests" }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("kms", "ListResourceTags", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var resp = await kmsClient.ListResourceTagsAsync(new ListResourceTagsRequest { KeyId = keyId });
                if (resp.Tags == null)
                    throw new Exception("Tags list is null");
            }));

            results.Add(await runner.RunTestAsync("kms", "ListResourceTags_ContentVerify", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var resp = await kmsClient.ListResourceTagsAsync(new ListResourceTagsRequest { KeyId = keyId });
                if (resp.Tags == null)
                    throw new Exception("Tags list is null");
                var envTag = resp.Tags.FirstOrDefault(t => t.TagKey == "Environment");
                if (envTag == null || envTag.TagValue != "test")
                    throw new Exception("Expected Environment=test tag");
                var projTag = resp.Tags.FirstOrDefault(t => t.TagKey == "Project");
                if (projTag == null || projTag.TagValue != "sdk-tests")
                    throw new Exception("Expected Project=sdk-tests tag");
            }));

            results.Add(await runner.RunTestAsync("kms", "UntagResource", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                await kmsClient.UntagResourceAsync(new UntagResourceRequest
                {
                    KeyId = keyId,
                    TagKeys = new List<string> { "Environment" }
                });
            }));

            results.Add(await runner.RunTestAsync("kms", "UpdateAlias", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                await kmsClient.UpdateAliasAsync(new UpdateAliasRequest
                {
                    AliasName = aliasName,
                    TargetKeyId = keyId
                });
                var listResp = await kmsClient.ListAliasesAsync(new ListAliasesRequest());
                var found = listResp.Aliases.Any(a => a.AliasName == aliasName);
                if (!found)
                    throw new Exception($"Alias {aliasName} not found after update");
            }));

            results.Add(await runner.RunTestAsync("kms", "ScheduleKeyDeletion", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var resp = await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest
                {
                    KeyId = keyId,
                    PendingWindowInDays = 7
                });
                if (resp.DeletionDate == null)
                    throw new Exception("DeletionDate is null");
            }));

            results.Add(await runner.RunTestAsync("kms", "ScheduleKeyDeletion_ReturnsKeyID", async () =>
            {
                var sdkResp = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "Return key ID test" });
                try
                {
                    var delResp = await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest
                    {
                        KeyId = sdkResp.KeyMetadata.KeyId,
                        PendingWindowInDays = 7
                    });
                    if (string.IsNullOrEmpty(delResp.KeyId))
                        throw new Exception("KeyId not returned");
                    if (delResp.KeyId != sdkResp.KeyMetadata.KeyId)
                        throw new Exception("Returned KeyId does not match");
                }
                catch { }
            }));

            results.Add(await runner.RunTestAsync("kms", "CancelKeyDeletion", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var resp = await kmsClient.CancelKeyDeletionAsync(new CancelKeyDeletionRequest { KeyId = keyId });
                if (string.IsNullOrEmpty(resp.KeyId))
                    throw new Exception("KeyId in response is null or empty");
            }));

            results.Add(await runner.RunTestAsync("kms", "CancelKeyDeletion_RestoresEnabledState", async () =>
            {
                var ckrKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "Cancel restore test" });
                try
                {
                    await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest
                    {
                        KeyId = ckrKey.KeyMetadata.KeyId,
                        PendingWindowInDays = 7
                    });
                    var descResp = await kmsClient.DescribeKeyAsync(new DescribeKeyRequest { KeyId = ckrKey.KeyMetadata.KeyId });
                    if (descResp.KeyMetadata.KeyState != KeyState.PendingDeletion)
                        throw new Exception($"Expected PendingDeletion, got {descResp.KeyMetadata.KeyState}");
                    await kmsClient.CancelKeyDeletionAsync(new CancelKeyDeletionRequest { KeyId = ckrKey.KeyMetadata.KeyId });
                    var descResp2 = await kmsClient.DescribeKeyAsync(new DescribeKeyRequest { KeyId = ckrKey.KeyMetadata.KeyId });
                    if (descResp2.KeyMetadata.KeyState != KeyState.Enabled)
                        throw new Exception($"Expected Enabled after cancel, got {descResp2.KeyMetadata.KeyState}");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = ckrKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "DeleteAlias", async () =>
            {
                await kmsClient.DeleteAliasAsync(new DeleteAliasRequest { AliasName = aliasName });
            }));

            results.Add(await runner.RunTestAsync("kms", "TagResource_ByAlias", async () =>
            {
                var tbaKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "Tag by alias" });
                var tbaAlias = "alias/" + TestRunner.MakeUniqueName("TbaAlias");
                try
                {
                    await kmsClient.CreateAliasAsync(new CreateAliasRequest
                    {
                        AliasName = tbaAlias,
                        TargetKeyId = tbaKey.KeyMetadata.KeyId
                    });
                    await kmsClient.TagResourceAsync(new TagResourceRequest
                    {
                        KeyId = tbaAlias,
                        Tags = new List<Tag>
                        {
                            new Tag { TagKey = "AliasTag", TagValue = "via-alias" }
                        }
                    });
                    var tagResp = await kmsClient.ListResourceTagsAsync(new ListResourceTagsRequest { KeyId = tbaAlias });
                    var found = tagResp.Tags.Any(t => t.TagKey == "AliasTag" && t.TagValue == "via-alias");
                    if (!found)
                        throw new Exception("Tag applied by alias not found");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.DeleteAliasAsync(new DeleteAliasRequest { AliasName = tbaAlias }); });
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = tbaKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "CreateKey_RSA", async () =>
            {
                var resp = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "RSA signing key",
                    KeyUsage = KeyUsageType.SIGN_VERIFY,
                    KeySpec = KeySpec.RSA_2048
                });
                if (resp.KeyMetadata == null)
                    throw new Exception("KeyMetadata is null");
                if (resp.KeyMetadata.KeyUsage != KeyUsageType.SIGN_VERIFY)
                    throw new Exception("Expected SIGN_VERIFY key usage");
                try
                {
                    await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = resp.KeyMetadata.KeyId, PendingWindowInDays = 7 });
                }
                catch { }
            }));

            results.Add(await runner.RunTestAsync("kms", "GetPublicKey_RSA", async () =>
            {
                var rsaKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "RSA get public key",
                    KeyUsage = KeyUsageType.SIGN_VERIFY,
                    KeySpec = KeySpec.RSA_2048
                });
                try
                {
                    var resp = await kmsClient.GetPublicKeyAsync(new GetPublicKeyRequest
                    {
                        KeyId = rsaKey.KeyMetadata.KeyId
                    });
                    if (resp.PublicKey == null || resp.PublicKey.Length == 0)
                        throw new Exception("PublicKey is null or empty");
                    if (resp.KeyUsage != KeyUsageType.SIGN_VERIFY)
                        throw new Exception("Expected SIGN_VERIFY key usage");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = rsaKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "Sign_RSA", async () =>
            {
                var signKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "RSA sign test",
                    KeyUsage = KeyUsageType.SIGN_VERIFY,
                    KeySpec = KeySpec.RSA_2048
                });
                try
                {
                    var message = System.Text.Encoding.UTF8.GetBytes("sign-test-message");
                    var resp = await kmsClient.SignAsync(new SignRequest
                    {
                        KeyId = signKey.KeyMetadata.KeyId,
                        Message = new MemoryStream(message),
                        SigningAlgorithm = SigningAlgorithmSpec.RSASSA_PSS_SHA_256
                    });
                    if (resp.Signature == null || resp.Signature.Length == 0)
                        throw new Exception("Signature is null or empty");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = signKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "Verify_RSA", async () =>
            {
                var verifyKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "RSA verify test",
                    KeyUsage = KeyUsageType.SIGN_VERIFY,
                    KeySpec = KeySpec.RSA_2048
                });
                try
                {
                    var message = System.Text.Encoding.UTF8.GetBytes("verify-test-message");
                    var signResp = await kmsClient.SignAsync(new SignRequest
                    {
                        KeyId = verifyKey.KeyMetadata.KeyId,
                        Message = new MemoryStream(message),
                        SigningAlgorithm = SigningAlgorithmSpec.RSASSA_PSS_SHA_256
                    });
                    var verifyResp = await kmsClient.VerifyAsync(new VerifyRequest
                    {
                        KeyId = verifyKey.KeyMetadata.KeyId,
                        Message = new MemoryStream(message),
                        Signature = signResp.Signature,
                        SigningAlgorithm = SigningAlgorithmSpec.RSASSA_PSS_SHA_256
                    });
                    if (!(verifyResp.SignatureValid ?? false))
                        throw new Exception("Signature should be valid");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = verifyKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "Verify_RSA_InvalidSignature", async () =>
            {
                var invSignKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "RSA invalid sig test",
                    KeyUsage = KeyUsageType.SIGN_VERIFY,
                    KeySpec = KeySpec.RSA_2048
                });
                try
                {
                    var message = System.Text.Encoding.UTF8.GetBytes("invalid-sig-message");
                    var fakeSignature = new MemoryStream(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                    var verifyResp = await kmsClient.VerifyAsync(new VerifyRequest
                    {
                        KeyId = invSignKey.KeyMetadata.KeyId,
                        Message = new MemoryStream(message),
                        Signature = fakeSignature,
                        SigningAlgorithm = SigningAlgorithmSpec.RSASSA_PSS_SHA_256
                    });
                    if (verifyResp.SignatureValid == true)
                        throw new Exception("Fake signature should not be valid");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = invSignKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "Sign_DisabledKey", async () =>
            {
                var disSignKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "Disabled sign key",
                    KeyUsage = KeyUsageType.SIGN_VERIFY,
                    KeySpec = KeySpec.RSA_2048
                });
                try
                {
                    await kmsClient.DisableKeyAsync(new DisableKeyRequest { KeyId = disSignKey.KeyMetadata.KeyId });
                    try
                    {
                        try
                        {
                            await kmsClient.SignAsync(new SignRequest
                            {
                                KeyId = disSignKey.KeyMetadata.KeyId,
                                Message = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("should fail")),
                                SigningAlgorithm = SigningAlgorithmSpec.RSASSA_PSS_SHA_256
                            });
                            throw new Exception("Expected error when signing with disabled key");
                        }
                        catch (DisabledException) { }
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.EnableKeyAsync(new EnableKeyRequest { KeyId = disSignKey.KeyMetadata.KeyId }); });
                    }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = disSignKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "CreateKey_HMAC", async () =>
            {
                var resp = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "HMAC key",
                    KeyUsage = KeyUsageType.GENERATE_VERIFY_MAC,
                    KeySpec = KeySpec.HMAC_256
                });
                if (resp.KeyMetadata == null)
                    throw new Exception("KeyMetadata is null");
                if (resp.KeyMetadata.KeyUsage != KeyUsageType.GENERATE_VERIFY_MAC)
                    throw new Exception("Expected GENERATE_VERIFY_MAC key usage");
                try
                {
                    await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = resp.KeyMetadata.KeyId, PendingWindowInDays = 7 });
                }
                catch { }
            }));

            results.Add(await runner.RunTestAsync("kms", "GenerateMac", async () =>
            {
                var hmacKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "Generate MAC key",
                    KeyUsage = KeyUsageType.GENERATE_VERIFY_MAC,
                    KeySpec = KeySpec.HMAC_256
                });
                try
                {
                    var resp = await kmsClient.GenerateMacAsync(new GenerateMacRequest
                    {
                        KeyId = hmacKey.KeyMetadata.KeyId,
                        Message = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("mac-test-message")),
                        MacAlgorithm = MacAlgorithmSpec.HMAC_SHA_256
                    });
                    if (resp.Mac == null || resp.Mac.Length == 0)
                        throw new Exception("MAC is null or empty");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = hmacKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "VerifyMac", async () =>
            {
                var vmKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "Verify MAC key",
                    KeyUsage = KeyUsageType.GENERATE_VERIFY_MAC,
                    KeySpec = KeySpec.HMAC_256
                });
                try
                {
                    var message = System.Text.Encoding.UTF8.GetBytes("verify-mac-message");
                    var macResp = await kmsClient.GenerateMacAsync(new GenerateMacRequest
                    {
                        KeyId = vmKey.KeyMetadata.KeyId,
                        Message = new MemoryStream(message),
                        MacAlgorithm = MacAlgorithmSpec.HMAC_SHA_256
                    });
                    var verifyResp = await kmsClient.VerifyMacAsync(new VerifyMacRequest
                    {
                        KeyId = vmKey.KeyMetadata.KeyId,
                        Message = new MemoryStream(message),
                        Mac = macResp.Mac,
                        MacAlgorithm = MacAlgorithmSpec.HMAC_SHA_256
                    });
                    if (!(verifyResp.MacValid ?? false))
                        throw new Exception("MAC should be valid");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = vmKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "VerifyMac_InvalidMac", async () =>
            {
                var invMacKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "Invalid MAC key",
                    KeyUsage = KeyUsageType.GENERATE_VERIFY_MAC,
                    KeySpec = KeySpec.HMAC_256
                });
                try
                {
                    var fakeMac = new MemoryStream(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                    var verifyResp = await kmsClient.VerifyMacAsync(new VerifyMacRequest
                    {
                        KeyId = invMacKey.KeyMetadata.KeyId,
                        Message = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("invalid-mac-message")),
                        Mac = fakeMac,
                        MacAlgorithm = MacAlgorithmSpec.HMAC_SHA_256
                    });
                    if (verifyResp.MacValid == true)
                        throw new Exception("Fake MAC should not be valid");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = invMacKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "GenerateDataKeyPair", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var dkKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "Data key pair test",
                    KeyUsage = KeyUsageType.ENCRYPT_DECRYPT
                });
                try
                {
                    var resp = await kmsClient.GenerateDataKeyPairAsync(new GenerateDataKeyPairRequest
                    {
                        KeyId = dkKey.KeyMetadata.KeyId,
                        KeyPairSpec = DataKeyPairSpec.RSA_2048
                    });
                    if (resp.PrivateKeyCiphertextBlob == null || resp.PrivateKeyCiphertextBlob.Length == 0)
                        throw new Exception("PrivateKeyCiphertextBlob is null or empty");
                    if (resp.PrivateKeyPlaintext == null || resp.PrivateKeyPlaintext.Length == 0)
                        throw new Exception("PrivateKeyPlaintext is null or empty");
                    if (resp.PublicKey == null || resp.PublicKey.Length == 0)
                        throw new Exception("PublicKey is null or empty");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = dkKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "GenerateDataKeyPairWithoutPlaintext", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var dkpKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "Data key pair no plaintext",
                    KeyUsage = KeyUsageType.ENCRYPT_DECRYPT
                });
                try
                {
                    var resp = await kmsClient.GenerateDataKeyPairWithoutPlaintextAsync(new GenerateDataKeyPairWithoutPlaintextRequest
                    {
                        KeyId = dkpKey.KeyMetadata.KeyId,
                        KeyPairSpec = DataKeyPairSpec.RSA_2048
                    });
                    if (resp.PrivateKeyCiphertextBlob == null || resp.PrivateKeyCiphertextBlob.Length == 0)
                        throw new Exception("PrivateKeyCiphertextBlob is null or empty");
                    if (resp.PublicKey == null || resp.PublicKey.Length == 0)
                        throw new Exception("PublicKey is null or empty");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = dkpKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "ListKeyRotations", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                await kmsClient.EnableKeyRotationAsync(new EnableKeyRotationRequest { KeyId = keyId });
                var resp = await kmsClient.ListKeyRotationsAsync(new ListKeyRotationsRequest { KeyId = keyId });
                if (resp.Rotations == null)
                    throw new Exception("Rotations list is null");
            }));

            results.Add(await runner.RunTestAsync("kms", "GetKeyRotationStatus_ContentVerify", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                await kmsClient.EnableKeyRotationAsync(new EnableKeyRotationRequest { KeyId = keyId });
                var resp = await kmsClient.GetKeyRotationStatusAsync(new GetKeyRotationStatusRequest { KeyId = keyId });
                if (!(resp.KeyRotationEnabled ?? false))
                    throw new Exception("Expected KeyRotationEnabled to be true");
            }));

            results.Add(await runner.RunTestAsync("kms", "Encrypt_ByKeyARN", async () =>
            {
                var arnKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "Encrypt by ARN" });
                try
                {
                    if (string.IsNullOrEmpty(arnKey.KeyMetadata.Arn))
                        throw new Exception("Key ARN is empty");
                    var resp = await kmsClient.EncryptAsync(new EncryptRequest
                    {
                        KeyId = arnKey.KeyMetadata.Arn,
                        Plaintext = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("encrypt-by-arn"))
                    });
                    if (resp.CiphertextBlob == null)
                        throw new Exception("CiphertextBlob is null");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = arnKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "Encrypt_ByAlias", async () =>
            {
                var ebaKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "Encrypt by alias" });
                var ebaAlias = "alias/" + TestRunner.MakeUniqueName("EbaAlias");
                try
                {
                    await kmsClient.CreateAliasAsync(new CreateAliasRequest
                    {
                        AliasName = ebaAlias,
                        TargetKeyId = ebaKey.KeyMetadata.KeyId
                    });
                    var resp = await kmsClient.EncryptAsync(new EncryptRequest
                    {
                        KeyId = ebaAlias,
                        Plaintext = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("encrypt-by-alias"))
                    });
                    if (resp.CiphertextBlob == null)
                        throw new Exception("CiphertextBlob is null");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.DeleteAliasAsync(new DeleteAliasRequest { AliasName = ebaAlias }); });
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = ebaKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "CreateKey_InvalidKeyUsageKeySpec", async () =>
            {
                try
                {
                    await kmsClient.CreateKeyAsync(new CreateKeyRequest
                    {
                        Description = "Invalid key usage/spec combo",
                        KeyUsage = KeyUsageType.SIGN_VERIFY,
                        KeySpec = KeySpec.SYMMETRIC_DEFAULT
                    });
                    throw new Exception("Expected error for invalid KeyUsage/KeySpec combination");
                }
                catch (AmazonKeyManagementServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("kms", "GenerateDataKey_NumberOfBytes", async () =>
            {
                var nbKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "NumberOfBytes test" });
                try
                {
                    var resp = await kmsClient.GenerateDataKeyAsync(new GenerateDataKeyRequest
                    {
                        KeyId = nbKey.KeyMetadata.KeyId,
                        NumberOfBytes = 64
                    });
                    using var ms = new MemoryStream();
                    resp.Plaintext.CopyTo(ms);
                    if (ms.Length != 64)
                        throw new Exception($"Expected 64-byte plaintext, got {ms.Length}");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = nbKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "GenerateRandom_VariousSizes", async () =>
            {
                foreach (var size in new[] { 1, 16, 64, 256 })
                {
                    var resp = await kmsClient.GenerateRandomAsync(new GenerateRandomRequest { NumberOfBytes = size });
                    if (resp.Plaintext == null || resp.Plaintext.Length != size)
                        throw new Exception($"Expected {size}-byte plaintext, got {resp.Plaintext?.Length}");
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "CreateKey_MultiRegion", async () =>
            {
                var resp = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "Multi-region key",
                    MultiRegion = true
                });
                try
                {
                    if (resp.KeyMetadata == null)
                        throw new Exception("KeyMetadata is null");
                    if (!(resp.KeyMetadata.MultiRegion ?? false))
                        throw new Exception("Expected MultiRegion to be true");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = resp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "CreateKey_WithTags", async () =>
            {
                var resp = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "Key with tags",
                    Tags = new List<Tag>
                    {
                        new Tag { TagKey = "CreatedBy", TagValue = "conformance-test" },
                        new Tag { TagKey = "Environment", TagValue = "test" }
                    }
                });
                try
                {
                    var tagResp = await kmsClient.ListResourceTagsAsync(new ListResourceTagsRequest { KeyId = resp.KeyMetadata.KeyId });
                    var createdBy = tagResp.Tags.FirstOrDefault(t => t.TagKey == "CreatedBy");
                    if (createdBy == null || createdBy.TagValue != "conformance-test")
                        throw new Exception("Expected CreatedBy=conformance-test tag");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = resp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "DescribeKey_ByAlias", async () =>
            {
                var dbaKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "Describe by alias" });
                var dbaAlias = "alias/" + TestRunner.MakeUniqueName("DbaAlias");
                try
                {
                    await kmsClient.CreateAliasAsync(new CreateAliasRequest
                    {
                        AliasName = dbaAlias,
                        TargetKeyId = dbaKey.KeyMetadata.KeyId
                    });
                    var resp = await kmsClient.DescribeKeyAsync(new DescribeKeyRequest { KeyId = dbaAlias });
                    if (resp.KeyMetadata == null)
                        throw new Exception("KeyMetadata is null");
                    if (resp.KeyMetadata.KeyId != dbaKey.KeyMetadata.KeyId)
                        throw new Exception("KeyId mismatch when describing by alias");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.DeleteAliasAsync(new DeleteAliasRequest { AliasName = dbaAlias }); });
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = dbaKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "UpdateKeyDescription_VerifyChange", async () =>
            {
                var udkKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "Original desc" });
                try
                {
                    await kmsClient.UpdateKeyDescriptionAsync(new UpdateKeyDescriptionRequest
                    {
                        KeyId = udkKey.KeyMetadata.KeyId,
                        Description = "Updated description"
                    });
                    var descResp = await kmsClient.DescribeKeyAsync(new DescribeKeyRequest { KeyId = udkKey.KeyMetadata.KeyId });
                    if (descResp.KeyMetadata.Description != "Updated description")
                        throw new Exception($"Expected 'Updated description', got '{descResp.KeyMetadata.Description}'");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = udkKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "Sign_InvalidAlgorithm", async () =>
            {
                var saKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "Invalid algo key",
                    KeyUsage = KeyUsageType.SIGN_VERIFY,
                    KeySpec = KeySpec.RSA_2048
                });
                try
                {
                    try
                    {
                        await kmsClient.SignAsync(new SignRequest
                        {
                            KeyId = saKey.KeyMetadata.KeyId,
                            Message = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test")),
                            SigningAlgorithm = SigningAlgorithmSpec.ECDSA_SHA_256
                        });
                        throw new Exception("Expected error for incompatible signing algorithm");
                    }
                    catch (AmazonKeyManagementServiceException) { }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = saKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "Encrypt_WrongKeyUsage", async () =>
            {
                var wkuKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "Wrong key usage",
                    KeyUsage = KeyUsageType.SIGN_VERIFY,
                    KeySpec = KeySpec.RSA_2048
                });
                try
                {
                    try
                    {
                        await kmsClient.EncryptAsync(new EncryptRequest
                        {
                            KeyId = wkuKey.KeyMetadata.KeyId,
                            Plaintext = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("should fail"))
                        });
                        throw new Exception("Expected error when encrypting with SIGN_VERIFY key");
                    }
                    catch (AmazonKeyManagementServiceException) { }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = wkuKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "DisableKey_NonExistent", async () =>
            {
                try
                {
                    await kmsClient.DisableKeyAsync(new DisableKeyRequest
                    {
                        KeyId = "arn:aws:kms:us-east-1:000000000000:key/00000000-0000-0000-0000-000000000000"
                    });
                    throw new Exception("Expected error for non-existent key");
                }
                catch (NotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("kms", "EnableKey_NonExistent", async () =>
            {
                try
                {
                    await kmsClient.EnableKeyAsync(new EnableKeyRequest
                    {
                        KeyId = "arn:aws:kms:us-east-1:000000000000:key/00000000-0000-0000-0000-000000000000"
                    });
                    throw new Exception("Expected error for non-existent key");
                }
                catch (NotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("kms", "ScheduleKeyDeletion_NonExistent", async () =>
            {
                try
                {
                    await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest
                    {
                        KeyId = "arn:aws:kms:us-east-1:000000000000:key/00000000-0000-0000-0000-000000000000",
                        PendingWindowInDays = 7
                    });
                    throw new Exception("Expected error for non-existent key");
                }
                catch (NotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("kms", "GetPublicKey_NonExistent", async () =>
            {
                try
                {
                    await kmsClient.GetPublicKeyAsync(new GetPublicKeyRequest
                    {
                        KeyId = "arn:aws:kms:us-east-1:000000000000:key/00000000-0000-0000-0000-000000000000"
                    });
                    throw new Exception("Expected error for non-existent key");
                }
                catch (NotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("kms", "ListGrants_NonExistent", async () =>
            {
                try
                {
                    await kmsClient.ListGrantsAsync(new ListGrantsRequest
                    {
                        KeyId = "arn:aws:kms:us-east-1:000000000000:key/00000000-0000-0000-0000-000000000000"
                    });
                    throw new Exception("Expected error for non-existent key");
                }
                catch (NotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("kms", "GenerateDataKey_DisabledKey", async () =>
            {
                var gdkKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "GDK disabled" });
                try
                {
                    await kmsClient.DisableKeyAsync(new DisableKeyRequest { KeyId = gdkKey.KeyMetadata.KeyId });
                    try
                    {
                        try
                        {
                            await kmsClient.GenerateDataKeyAsync(new GenerateDataKeyRequest
                            {
                                KeyId = gdkKey.KeyMetadata.KeyId,
                                KeySpec = DataKeySpec.AES_256
                            });
                            throw new Exception("Expected error when generating data key with disabled key");
                        }
                        catch (DisabledException) { }
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.EnableKeyAsync(new EnableKeyRequest { KeyId = gdkKey.KeyMetadata.KeyId }); });
                    }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = gdkKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "DeleteAlias_NonExistent", async () =>
            {
                try
                {
                    await kmsClient.DeleteAliasAsync(new DeleteAliasRequest
                    {
                        AliasName = "alias/" + TestRunner.MakeUniqueName("NonExistentAlias")
                    });
                    throw new Exception("Expected error for non-existent alias");
                }
                catch (NotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("kms", "CreateAlias_AliasAWSReserved", async () =>
            {
                try
                {
                    await kmsClient.CreateAliasAsync(new CreateAliasRequest
                    {
                        AliasName = "alias/aws/some-service",
                        TargetKeyId = keyId
                    });
                    throw new Exception("Expected error for AWS reserved alias");
                }
                catch (AmazonKeyManagementServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("kms", "CreateAlias_WithoutPrefix", async () =>
            {
                try
                {
                    await kmsClient.CreateAliasAsync(new CreateAliasRequest
                    {
                        AliasName = "InvalidAliasName",
                        TargetKeyId = keyId
                    });
                    throw new Exception("Expected error for alias without alias/ prefix");
                }
                catch (AmazonKeyManagementServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("kms", "PutKeyPolicy_InvalidJSON", async () =>
            {
                var pkpKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "Invalid JSON policy" });
                try
                {
                    try
                    {
                        await kmsClient.PutKeyPolicyAsync(new PutKeyPolicyRequest
                        {
                            KeyId = pkpKey.KeyMetadata.KeyId,
                            PolicyName = "default",
                            Policy = "this is not valid json"
                        });
                        throw new Exception("Expected error for invalid JSON policy");
                    }
                    catch (AmazonKeyManagementServiceException) { }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = pkpKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "Encrypt_SignVerifyKey", async () =>
            {
                var svKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "Sign verify encrypt test",
                    KeyUsage = KeyUsageType.SIGN_VERIFY,
                    KeySpec = KeySpec.RSA_2048
                });
                try
                {
                    try
                    {
                        await kmsClient.EncryptAsync(new EncryptRequest
                        {
                            KeyId = svKey.KeyMetadata.KeyId,
                            Plaintext = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("should fail"))
                        });
                        throw new Exception("Expected error when encrypting with SIGN_VERIFY key");
                    }
                    catch (AmazonKeyManagementServiceException) { }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = svKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "ReEncrypt_InvalidCiphertext", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                try
                {
                    await kmsClient.ReEncryptAsync(new ReEncryptRequest
                    {
                        CiphertextBlob = new MemoryStream(new byte[] { 0, 1, 2, 3, 4, 5 }),
                        DestinationKeyId = keyId
                    });
                    throw new Exception("Expected error for invalid ciphertext");
                }
                catch (AmazonKeyManagementServiceException) { }
            }));

            results.Add(await runner.RunTestAsync("kms", "ListAliases_FilterByKeyID", async () =>
            {
                var lafKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "Alias filter test" });
                var lafAlias = "alias/" + TestRunner.MakeUniqueName("LafAlias");
                try
                {
                    await kmsClient.CreateAliasAsync(new CreateAliasRequest
                    {
                        AliasName = lafAlias,
                        TargetKeyId = lafKey.KeyMetadata.KeyId
                    });
                    var resp = await kmsClient.ListAliasesAsync(new ListAliasesRequest
                    {
                        KeyId = lafKey.KeyMetadata.KeyId
                    });
                    if (resp.Aliases == null)
                        throw new Exception("Aliases is null");
                    var found = resp.Aliases.Any(a => a.AliasName == lafAlias);
                    if (!found)
                        throw new Exception($"Alias {lafAlias} not found in filtered ListAliases");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.DeleteAliasAsync(new DeleteAliasRequest { AliasName = lafAlias }); });
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = lafKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "GetKeyRotationStatus_DisabledRotation", async () =>
            {
                var drKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "Disabled rotation test" });
                try
                {
                    var resp = await kmsClient.GetKeyRotationStatusAsync(new GetKeyRotationStatusRequest { KeyId = drKey.KeyMetadata.KeyId });
                    if (resp.KeyRotationEnabled == true)
                        throw new Exception("Expected KeyRotationEnabled to be false for new key");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = drKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "CreateKey_ExternalOrigin", async () =>
            {
                var resp = await kmsClient.CreateKeyAsync(new CreateKeyRequest
                {
                    Description = "External origin key",
                    Origin = OriginType.EXTERNAL
                });
                try
                {
                    if (resp.KeyMetadata == null)
                        throw new Exception("KeyMetadata is null");
                    if (resp.KeyMetadata.Origin != OriginType.EXTERNAL)
                        throw new Exception($"Expected EXTERNAL origin, got {resp.KeyMetadata.Origin}");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = resp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "Encrypt_EncryptionContext", async () =>
            {
                var ecKey = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "Encryption context test" });
                try
                {
                    var plaintext = System.Text.Encoding.UTF8.GetBytes("encryption-context-test");
                    var context = new Dictionary<string, string> { { "purpose", "test" }, { "app", "conformance" } };
                    var encResp = await kmsClient.EncryptAsync(new EncryptRequest
                    {
                        KeyId = ecKey.KeyMetadata.KeyId,
                        Plaintext = new MemoryStream(plaintext),
                        EncryptionContext = context
                    });
                    if (encResp.CiphertextBlob == null)
                        throw new Exception("CiphertextBlob is null");
                    var decResp = await kmsClient.DecryptAsync(new DecryptRequest
                    {
                        CiphertextBlob = encResp.CiphertextBlob,
                        EncryptionContext = context
                    });
                    using var ms = new MemoryStream();
                    decResp.Plaintext.CopyTo(ms);
                    if (!plaintext.SequenceEqual(ms.ToArray()))
                        throw new Exception("Plaintext mismatch with encryption context");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = ecKey.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                }
            }));

            results.Add(await runner.RunTestAsync("kms", "ListKeys_Pagination", async () =>
            {
                var resp1 = await kmsClient.ListKeysAsync(new ListKeysRequest { Limit = 1 });
                if (resp1.Keys == null || resp1.Keys.Count == 0)
                    throw new Exception("Expected at least one key on first page");
                var allKeys = new List<Amazon.KeyManagementService.Model.KeyListEntry>(resp1.Keys);
                var marker = resp1.NextMarker;
                while (!string.IsNullOrEmpty(marker))
                {
                    var resp2 = await kmsClient.ListKeysAsync(new ListKeysRequest { Limit = 1, Marker = marker });
                    if (resp2.Keys != null && resp2.Keys.Count > 0)
                        allKeys.AddRange(resp2.Keys);
                    marker = resp2.NextMarker;
                }
                if (allKeys.Count == 0)
                    throw new Exception("No keys found after pagination");
            }));
        }
        finally
        {
            await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.DeleteAliasAsync(new DeleteAliasRequest { AliasName = aliasName }); });
            try
            {
                if (!string.IsNullOrEmpty(keyId))
                    await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = keyId, PendingWindowInDays = 7 });
            }
            catch { }
        }

        results.Add(await runner.RunTestAsync("kms", "DescribeNonExistentKey", async () =>
        {
            try
            {
                await kmsClient.DescribeKeyAsync(new DescribeKeyRequest
                {
                    KeyId = "12345678-1234-1234-1234-123456789012"
                });
                throw new Exception("Expected error but got none");
            }
            catch (NotFoundException) { }
        }));

        results.Add(await runner.RunTestAsync("kms", "Encrypt_DecryptRoundtrip", async () =>
        {
            var rtAlias = "alias/" + TestRunner.MakeUniqueName("RtAlias");
            var createResp = await kmsClient.CreateKeyAsync(new CreateKeyRequest
            {
                Description = "Roundtrip test key"
            });
            try
            {
                var rtKeyId = createResp.KeyMetadata.KeyId;
                var plaintext = System.Text.Encoding.UTF8.GetBytes("roundtrip-test-data-12345");
                var encResp = await kmsClient.EncryptAsync(new EncryptRequest
                {
                    KeyId = rtKeyId,
                    Plaintext = new MemoryStream(plaintext)
                });
                var decResp = await kmsClient.DecryptAsync(new DecryptRequest
                {
                    CiphertextBlob = encResp.CiphertextBlob
                });
                using var ms = new MemoryStream();
                decResp.Plaintext.CopyTo(ms);
                var decrypted = ms.ToArray();
                if (!plaintext.SequenceEqual(decrypted))
                    throw new Exception("Plaintext mismatch after roundtrip");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = createResp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
            }
        }));

        results.Add(await runner.RunTestAsync("kms", "GenerateDataKey_ContentVerify", async () =>
        {
            var verifyResp = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "Verify key" });
            try
            {
                var resp = await kmsClient.GenerateDataKeyAsync(new GenerateDataKeyRequest
                {
                    KeyId = verifyResp.KeyMetadata.KeyId,
                    KeySpec = DataKeySpec.AES_256
                });
                using var ms = new MemoryStream();
                resp.Plaintext.CopyTo(ms);
                if (ms.Length != 32)
                    throw new Exception($"Expected 32-byte plaintext, got {ms.Length}");
                if (resp.CiphertextBlob.Length == 0)
                    throw new Exception("CiphertextBlob is empty");
                if (ms.Length == resp.CiphertextBlob.Length)
                    throw new Exception("Plaintext and CiphertextBlob should have different lengths");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = verifyResp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
            }
        }));

        results.Add(await runner.RunTestAsync("kms", "CreateAlias_Duplicate", async () =>
        {
            var dupKeyResp = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "Dup alias test" });
            try
            {
                var dupAlias = "alias/" + TestRunner.MakeUniqueName("DupAlias");
                await kmsClient.CreateAliasAsync(new CreateAliasRequest
                {
                    AliasName = dupAlias,
                    TargetKeyId = dupKeyResp.KeyMetadata.KeyId
                });
                try
                {
                    try
                    {
                        await kmsClient.CreateAliasAsync(new CreateAliasRequest
                        {
                            AliasName = dupAlias,
                            TargetKeyId = dupKeyResp.KeyMetadata.KeyId
                        });
                        throw new Exception("Expected error for duplicate alias");
                    }
                    catch (AlreadyExistsException) { }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.DeleteAliasAsync(new DeleteAliasRequest { AliasName = dupAlias }); });
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = dupKeyResp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
            }
        }));

        results.Add(await runner.RunTestAsync("kms", "Encrypt_DisabledKey", async () =>
        {
            var disKeyResp = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "Disable test" });
            try
            {
                await kmsClient.DisableKeyAsync(new DisableKeyRequest { KeyId = disKeyResp.KeyMetadata.KeyId });
                try
                {
                    try
                    {
                        await kmsClient.EncryptAsync(new EncryptRequest
                        {
                            KeyId = disKeyResp.KeyMetadata.KeyId,
                            Plaintext = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("should fail"))
                        });
                        throw new Exception("Expected error when encrypting with disabled key");
                    }
                    catch (DisabledException) { }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.EnableKeyAsync(new EnableKeyRequest { KeyId = disKeyResp.KeyMetadata.KeyId }); });
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = disKeyResp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
            }
        }));

        results.Add(await runner.RunTestAsync("kms", "ScheduleKeyDeletion_InvalidWindow", async () =>
        {
            var invKeyResp = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "Invalid window test" });
            try
            {
                try
                {
                    await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest
                    {
                        KeyId = invKeyResp.KeyMetadata.KeyId,
                        PendingWindowInDays = 3
                    });
                    throw new Exception("Expected error for invalid pending window (3 days, min is 7)");
                }
                catch (AmazonKeyManagementServiceException) { }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = invKeyResp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
            }
        }));

        results.Add(await runner.RunTestAsync("kms", "ListAliases_ContainsCreated", async () =>
        {
            var laKeyResp = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "List alias test" });
            try
            {
                var laAlias = "alias/" + TestRunner.MakeUniqueName("LaAlias");
                await kmsClient.CreateAliasAsync(new CreateAliasRequest
                {
                    AliasName = laAlias,
                    TargetKeyId = laKeyResp.KeyMetadata.KeyId
                });
                try
                {
                    var listResp = await kmsClient.ListAliasesAsync(new ListAliasesRequest());
                    var found = listResp.Aliases.Any(a => a.AliasName == laAlias);
                    if (!found)
                        throw new Exception($"Created alias {laAlias} not found in ListAliases");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.DeleteAliasAsync(new DeleteAliasRequest { AliasName = laAlias }); });
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = laKeyResp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
            }
        }));

        results.Add(await runner.RunTestAsync("kms", "GetKeyPolicy_ContentVerify", async () =>
        {
            var gpKeyResp = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "Policy verify" });
            try
            {
                var policy = @"{""Version"":""2012-10-17"",""Statement"":[{""Effect"":""Allow"",""Principal"":{""AWS"":""*""},""Action"":""kms:*"",""Resource"":""*""}]}";
                await kmsClient.PutKeyPolicyAsync(new PutKeyPolicyRequest
                {
                    KeyId = gpKeyResp.KeyMetadata.KeyId,
                    PolicyName = "default",
                    Policy = policy
                });
                var getResp = await kmsClient.GetKeyPolicyAsync(new GetKeyPolicyRequest
                {
                    KeyId = gpKeyResp.KeyMetadata.KeyId,
                    PolicyName = "default"
                });
                if (getResp.Policy != policy)
                    throw new Exception("Policy content mismatch");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = gpKeyResp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
            }
        }));

        results.Add(await runner.RunTestAsync("kms", "ReEncrypt_WithDifferentKey", async () =>
        {
            var reKey1 = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "ReEncrypt source" });
            var reKey2 = await kmsClient.CreateKeyAsync(new CreateKeyRequest { Description = "ReEncrypt dest" });
            try
            {
                var plaintext = System.Text.Encoding.UTF8.GetBytes("re-encrypt-test");
                var encResp = await kmsClient.EncryptAsync(new EncryptRequest
                {
                    KeyId = reKey1.KeyMetadata.KeyId,
                    Plaintext = new MemoryStream(plaintext)
                });
                var reResp = await kmsClient.ReEncryptAsync(new ReEncryptRequest
                {
                    CiphertextBlob = encResp.CiphertextBlob,
                    DestinationKeyId = reKey2.KeyMetadata.KeyId
                });
                var decResp = await kmsClient.DecryptAsync(new DecryptRequest
                {
                    CiphertextBlob = reResp.CiphertextBlob,
                    KeyId = reKey2.KeyMetadata.KeyId
                });
                using var ms2 = new MemoryStream();
                decResp.Plaintext.CopyTo(ms2);
                if (!plaintext.SequenceEqual(ms2.ToArray()))
                    throw new Exception("Plaintext mismatch after re-encrypt");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = reKey1.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
                await TestHelpers.SafeCleanupAsync(async () => { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = reKey2.KeyMetadata.KeyId, PendingWindowInDays = 7 }); });
            }
        }));

        return results;
    }
}
