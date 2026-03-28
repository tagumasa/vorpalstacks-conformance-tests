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
        var aliasName = "alias/" + TestRunner.MakeUniqueName("CSKMSAlias");
        var ciphertextBlob = (MemoryStream)null!;

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
                    throw new Exception("Aliases is nil");
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
                    throw new Exception("CiphertextBlob is nil");
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
                    throw new Exception("CiphertextBlob is nil or empty");
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
                    throw new Exception("CiphertextBlob is nil or empty");
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
                    throw new Exception("CiphertextBlob is nil");
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
                    throw new Exception("GrantToken is nil or empty");
                if (string.IsNullOrEmpty(resp.GrantId))
                    throw new Exception("GrantId is nil or empty");
            }));

            results.Add(await runner.RunTestAsync("kms", "ListGrants", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var resp = await kmsClient.ListGrantsAsync(new ListGrantsRequest { KeyId = keyId });
                if (resp.Grants == null)
                    throw new Exception("Grants list is nil");
            }));

            results.Add(await runner.RunTestAsync("kms", "ListRetirableGrants", async () =>
            {
                var resp = await kmsClient.ListRetirableGrantsAsync(new ListRetirableGrantsRequest
                {
                    RetiringPrincipal = "arn:aws:iam::000000000000:user/TestUser"
                });
                if (resp.Grants == null)
                    throw new Exception("Grants list is nil");
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
                    throw new Exception("PolicyNames list is nil");
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
                    throw new Exception("Tags list is nil");
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
                    throw new Exception("DeletionDate is nil");
            }));

            results.Add(await runner.RunTestAsync("kms", "CancelKeyDeletion", async () =>
            {
                if (string.IsNullOrEmpty(keyId))
                    throw new Exception("KeyId not set");
                var resp = await kmsClient.CancelKeyDeletionAsync(new CancelKeyDeletionRequest { KeyId = keyId });
                if (string.IsNullOrEmpty(resp.KeyId))
                    throw new Exception("KeyId in response is nil or empty");
            }));

            results.Add(await runner.RunTestAsync("kms", "DeleteAlias", async () =>
            {
                await kmsClient.DeleteAliasAsync(new DeleteAliasRequest { AliasName = aliasName });
            }));
        }
        finally
        {
            try { await kmsClient.DeleteAliasAsync(new DeleteAliasRequest { AliasName = aliasName }); } catch { }
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
                try { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = createResp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); } catch { }
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
                try { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = verifyResp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); } catch { }
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
                    try { await kmsClient.DeleteAliasAsync(new DeleteAliasRequest { AliasName = dupAlias }); } catch { }
                }
            }
            finally
            {
                try { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = dupKeyResp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); } catch { }
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
                    try { await kmsClient.EnableKeyAsync(new EnableKeyRequest { KeyId = disKeyResp.KeyMetadata.KeyId }); } catch { }
                }
            }
            finally
            {
                try { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = disKeyResp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); } catch { }
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
                try { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = invKeyResp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); } catch { }
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
                    try { await kmsClient.DeleteAliasAsync(new DeleteAliasRequest { AliasName = laAlias }); } catch { }
                }
            }
            finally
            {
                try { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = laKeyResp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); } catch { }
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
                try { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = gpKeyResp.KeyMetadata.KeyId, PendingWindowInDays = 7 }); } catch { }
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
                try { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = reKey1.KeyMetadata.KeyId, PendingWindowInDays = 7 }); } catch { }
                try { await kmsClient.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = reKey2.KeyMetadata.KeyId, PendingWindowInDays = 7 }); } catch { }
            }
        }));

        return results;
    }
}
