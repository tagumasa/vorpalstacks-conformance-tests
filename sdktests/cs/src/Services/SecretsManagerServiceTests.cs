using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.Runtime;
using System.IO;

namespace VorpalStacks.SDK.Tests.Services;

public static class SecretsManagerServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonSecretsManagerClient secretsManagerClient,
        string region)
    {
        var results = new List<TestResult>();
        var secretName = TestRunner.MakeUniqueName("CSSecret");

        try
        {
            results.Add(await runner.RunTestAsync("secretsmanager", "CreateSecret", async () =>
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest
                {
                    Name = secretName,
                    SecretString = "test-secret-value"
                });
            }));

            results.Add(await runner.RunTestAsync("secretsmanager", "ListSecrets", async () =>
            {
                var resp = await secretsManagerClient.ListSecretsAsync(new ListSecretsRequest());
                if (resp.SecretList == null)
                    throw new Exception("SecretList is null");
            }));

            results.Add(await runner.RunTestAsync("secretsmanager", "DescribeSecret", async () =>
            {
                var resp = await secretsManagerClient.DescribeSecretAsync(new DescribeSecretRequest
                {
                    SecretId = secretName
                });
                if (resp.Name == null)
                    throw new Exception("Name is null");
            }));

            results.Add(await runner.RunTestAsync("secretsmanager", "DeleteSecret", async () =>
            {
                await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest
                {
                    SecretId = secretName,
                    ForceDeleteWithoutRecovery = true
                });
            }));
        }
        finally
        {
            try
            {
                await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest
                {
                    SecretId = secretName,
                    ForceDeleteWithoutRecovery = true
                });
            }
            catch { }
        }

        results.Add(await runner.RunTestAsync("secretsmanager", "GetSecretValue", async () =>
        {
            var sn = TestRunner.MakeUniqueName("GVSecret");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = sn, SecretString = "my-secret-value" });
                var resp = await secretsManagerClient.GetSecretValueAsync(new GetSecretValueRequest { SecretId = sn });
                if (string.IsNullOrEmpty(resp.SecretString) && resp.SecretBinary == null)
                    throw new Exception("secret value is nil");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "UpdateSecret", async () =>
        {
            var sn = TestRunner.MakeUniqueName("UpdateSecret");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = sn, SecretString = "original" });
                var resp = await secretsManagerClient.UpdateSecretAsync(new UpdateSecretRequest { SecretId = sn, SecretString = "updated-secret-value" });
                if (string.IsNullOrEmpty(resp.ARN))
                    throw new Exception("secret ARN is nil");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "TagResource", async () =>
        {
            var sn = TestRunner.MakeUniqueName("TagSecret");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = sn, SecretString = "tag-test" });
                await secretsManagerClient.TagResourceAsync(new TagResourceRequest
                {
                    SecretId = sn,
                    Tags = new List<Tag>
                    {
                        new Tag { Key = "Environment", Value = "test" },
                        new Tag { Key = "Project", Value = "sdk-tests" }
                    }
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "ListSecretVersionIds", async () =>
        {
            var sn = TestRunner.MakeUniqueName("VersionSecret");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = sn, SecretString = "v1" });
                var resp = await secretsManagerClient.ListSecretVersionIdsAsync(new ListSecretVersionIdsRequest { SecretId = sn });
                if (resp.Versions == null)
                    throw new Exception("versions list is nil");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "GetSecretValue_NonExistent", async () =>
        {
            try
            {
                await secretsManagerClient.GetSecretValueAsync(new GetSecretValueRequest
                {
                    SecretId = "arn:aws:secretsmanager:us-east-1:000000000000:secret:nonexistent-secret-xyz"
                });
                throw new Exception("expected error for non-existent secret");
            }
            catch (ResourceNotFoundException) { }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "CreateSecret_Duplicate", async () =>
        {
            var dupName = TestRunner.MakeUniqueName("DupSecret");
            await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = dupName, SecretString = "initial-value" });
            try
            {
                try
                {
                    await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = dupName, SecretString = "duplicate-value" });
                    throw new Exception("expected error for duplicate secret name");
                }
                catch (ResourceExistsException) { }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = dupName, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "GetSecretValue_ContentVerify", async () =>
        {
            var secName = TestRunner.MakeUniqueName("VerifySecret");
            var secValue = "my-verified-secret-123";
            await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = secName, SecretString = secValue });
            try
            {
                var resp = await secretsManagerClient.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secName });
                if (resp.SecretString != secValue)
                    throw new Exception($"secret value mismatch: got {resp.SecretString}, want {secValue}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = secName, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "UpdateSecret_ContentVerify", async () =>
        {
            var secName = TestRunner.MakeUniqueName("UpdateVerify");
            await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = secName, SecretString = "original-value" });
            try
            {
                var updatedValue = "updated-secret-value-456";
                await secretsManagerClient.UpdateSecretAsync(new UpdateSecretRequest { SecretId = secName, SecretString = updatedValue });
                var resp = await secretsManagerClient.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secName });
                if (resp.SecretString != updatedValue)
                    throw new Exception($"secret value not updated: got {resp.SecretString}, want {updatedValue}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = secName, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "ListSecrets_ContainsCreated", async () =>
        {
            var secName = TestRunner.MakeUniqueName("ListVerify");
            await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = secName, SecretString = "list-test" });
            try
            {
                var resp = await secretsManagerClient.ListSecretsAsync(new ListSecretsRequest());
                bool found = false;
                foreach (var s in resp.SecretList)
                {
                    if (s.Name == secName)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    throw new Exception("created secret not found in ListSecrets");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = secName, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "DeleteSecret_NonExistent", async () =>
        {
            try
            {
                await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest
                {
                    SecretId = "arn:aws:secretsmanager:us-east-1:000000000000:secret:nonexistent-xyz",
                    ForceDeleteWithoutRecovery = true
                });
                throw new Exception("expected error for non-existent secret");
            }
            catch (ResourceNotFoundException) { }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "MultiByteSecret", async () =>
        {
            var pairs = new (string Label, string Value)[]
            {
                ("ja", "日本語テストシークレット"),
                ("zh", "简体中文测试机密"),
                ("tw", "繁體中文測試機密"),
            };
            foreach (var (label, value) in pairs)
            {
                var name = TestRunner.MakeUniqueName($"MultiByte-{label}");
                try
                {
                    await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest
                    {
                        Name = name,
                        SecretString = value
                    });
                    var resp = await secretsManagerClient.GetSecretValueAsync(new GetSecretValueRequest
                    {
                        SecretId = name
                    });
                    if (resp.SecretString != value)
                        throw new Exception($"Mismatch for {label}: expected {value}, got {resp.SecretString}");
                }
                finally
                {
                    try
                    {
                        await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest
                        {
                            SecretId = name,
                            ForceDeleteWithoutRecovery = true
                        });
                    }
                    catch { }
                }
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "DescribeSecret_NonExistent", async () =>
        {
            try
            {
                await secretsManagerClient.DescribeSecretAsync(new DescribeSecretRequest
                {
                    SecretId = "NonExistentSecret_xyz_12345"
                });
                throw new Exception("Expected error but got none");
            }
            catch (ResourceNotFoundException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "PutSecretValue_Basic", async () =>
        {
            var sn = TestRunner.MakeUniqueName("PutValue");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = sn, SecretString = "initial" });
                var resp = await secretsManagerClient.PutSecretValueAsync(new PutSecretValueRequest
                {
                    SecretId = sn,
                    SecretString = "new-value"
                });
                if (string.IsNullOrEmpty(resp.VersionId))
                    throw new Exception("version ID is nil");
                var getResp = await secretsManagerClient.GetSecretValueAsync(new GetSecretValueRequest { SecretId = sn });
                if (getResp.SecretString != "new-value")
                    throw new Exception($"value mismatch: got {getResp.SecretString}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "PutSecretValue_ContentVerify", async () =>
        {
            var sn = TestRunner.MakeUniqueName("PutVerify");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = sn, SecretString = "v1" });
                await secretsManagerClient.PutSecretValueAsync(new PutSecretValueRequest { SecretId = sn, SecretString = "v2" });
                await secretsManagerClient.PutSecretValueAsync(new PutSecretValueRequest { SecretId = sn, SecretString = "v3" });
                var verResp = await secretsManagerClient.ListSecretVersionIdsAsync(new ListSecretVersionIdsRequest { SecretId = sn });
                if (verResp.Versions.Count != 3)
                    throw new Exception($"expected 3 versions, got {verResp.Versions.Count}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "RestoreSecret_Basic", async () =>
        {
            var sn = TestRunner.MakeUniqueName("RestoreSec");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = sn, SecretString = "restore-test" });
                await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn });
                var listResp = await secretsManagerClient.ListSecretsAsync(new ListSecretsRequest());
                foreach (var s in listResp.SecretList)
                {
                    if (s.Name == sn)
                        throw new Exception("soft-deleted secret should not appear in ListSecrets");
                }
                await secretsManagerClient.RestoreSecretAsync(new RestoreSecretRequest { SecretId = sn });
                var getResp = await secretsManagerClient.GetSecretValueAsync(new GetSecretValueRequest { SecretId = sn });
                if (getResp.SecretString != "restore-test")
                    throw new Exception("value mismatch after restore");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "RestoreSecret_NonExistent", async () =>
        {
            try
            {
                await secretsManagerClient.RestoreSecretAsync(new RestoreSecretRequest
                {
                    SecretId = "nonexistent-restore-xyz"
                });
                throw new Exception("expected error for non-existent secret");
            }
            catch (ResourceNotFoundException) { }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "GetRandomPassword_Basic", async () =>
        {
            var resp = await secretsManagerClient.GetRandomPasswordAsync(new GetRandomPasswordRequest());
            if (resp.RandomPassword == null || resp.RandomPassword.Length != 32)
                throw new Exception($"expected default password length 32, got {resp.RandomPassword?.Length ?? 0}");
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "GetRandomPassword_CustomLength", async () =>
        {
            var resp = await secretsManagerClient.GetRandomPasswordAsync(new GetRandomPasswordRequest
            {
                PasswordLength = 16
            });
            if (resp.RandomPassword == null || resp.RandomPassword.Length != 16)
                throw new Exception($"expected password length 16, got {resp.RandomPassword?.Length ?? 0}");
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "GetRandomPassword_ExcludeCharacters", async () =>
        {
            var resp = await secretsManagerClient.GetRandomPasswordAsync(new GetRandomPasswordRequest
            {
                PasswordLength = 50,
                ExcludeCharacters = "abcdefABCDEF0123456789",
                ExcludePunctuation = true,
                IncludeSpace = false
            });
            foreach (var c in resp.RandomPassword)
            {
                if (c >= 'a' && c <= 'f')
                    throw new Exception($"found excluded lowercase char: {c}");
                if (c >= 'A' && c <= 'F')
                    throw new Exception($"found excluded uppercase char: {c}");
                if (c >= '0' && c <= '5')
                    throw new Exception($"found excluded digit: {c}");
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "GetRandomPassword_RequireEachIncludedType", async () =>
        {
            var resp = await secretsManagerClient.GetRandomPasswordAsync(new GetRandomPasswordRequest
            {
                PasswordLength = 20,
                RequireEachIncludedType = true
            });
            bool hasLower = false, hasUpper = false, hasDigit = false, hasPunct = false;
            foreach (var c in resp.RandomPassword)
            {
                if (c >= 'a' && c <= 'z') hasLower = true;
                if (c >= 'A' && c <= 'Z') hasUpper = true;
                if (c >= '0' && c <= '9') hasDigit = true;
                if ((c >= '!' && c <= '/') || (c >= ':' && c <= '@') || (c >= '[' && c <= '`') || (c >= '{' && c <= '~'))
                    hasPunct = true;
            }
            if (!hasLower || !hasUpper || !hasDigit || !hasPunct)
                throw new Exception($"missing required types: lower={hasLower} upper={hasUpper} digit={hasDigit} punct={hasPunct}");
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "UpdateSecretVersionStage_Basic", async () =>
        {
            var sn = TestRunner.MakeUniqueName("VersionStage");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = sn, SecretString = "v1" });
                var putResp = await secretsManagerClient.PutSecretValueAsync(new PutSecretValueRequest { SecretId = sn, SecretString = "v2" });
                if (string.IsNullOrEmpty(putResp.VersionId))
                    throw new Exception("version ID is nil from PutSecretValue");
                var v2VersionId = putResp.VersionId;

                var descResp = await secretsManagerClient.DescribeSecretAsync(new DescribeSecretRequest { SecretId = sn });
                if (descResp.VersionIdsToStages == null)
                    throw new Exception("VersionIdsToStages is nil");
                if (!descResp.VersionIdsToStages.ContainsKey(v2VersionId))
                    throw new Exception("v2 not in VersionIdsToStages");
                bool hasCurrent = false;
                foreach (var s in descResp.VersionIdsToStages[v2VersionId])
                {
                    if (s == "AWSCURRENT") hasCurrent = true;
                }
                if (!hasCurrent)
                    throw new Exception("v2 should have AWSCURRENT stage");

                await secretsManagerClient.UpdateSecretVersionStageAsync(new UpdateSecretVersionStageRequest
                {
                    SecretId = sn,
                    VersionStage = "AWSCURRENT",
                    MoveToVersionId = v2VersionId
                });

                var descResp2 = await secretsManagerClient.DescribeSecretAsync(new DescribeSecretRequest { SecretId = sn });
                if (descResp2.VersionIdsToStages == null)
                    throw new Exception("VersionIdsToStages is nil after update");
                if (!descResp2.VersionIdsToStages.ContainsKey(v2VersionId))
                    throw new Exception("v2 not in VersionIdsToStages after update");
                hasCurrent = false;
                foreach (var s in descResp2.VersionIdsToStages[v2VersionId])
                {
                    if (s == "AWSCURRENT") hasCurrent = true;
                }
                if (!hasCurrent)
                    throw new Exception("v2 should still have AWSCURRENT");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "UntagResource_Basic", async () =>
        {
            var sn = TestRunner.MakeUniqueName("UntagTest");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest
                {
                    Name = sn,
                    Tags = new List<Tag>
                    {
                        new Tag { Key = "env", Value = "test" },
                        new Tag { Key = "team", Value = "dev" }
                    }
                });
                await secretsManagerClient.UntagResourceAsync(new UntagResourceRequest
                {
                    SecretId = sn,
                    TagKeys = new List<string> { "env" }
                });
                var descResp = await secretsManagerClient.DescribeSecretAsync(new DescribeSecretRequest { SecretId = sn });
                foreach (var t in descResp.Tags)
                {
                    if (t.Key == "env")
                        throw new Exception("env tag should have been removed");
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "DescribeSecret_Tags", async () =>
        {
            var sn = TestRunner.MakeUniqueName("ListTags");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest
                {
                    Name = sn,
                    Tags = new List<Tag>
                    {
                        new Tag { Key = "key1", Value = "val1" },
                        new Tag { Key = "key2", Value = "val2" }
                    }
                });
                var resp = await secretsManagerClient.DescribeSecretAsync(new DescribeSecretRequest { SecretId = sn });
                if (resp.Tags.Count != 2)
                    throw new Exception($"expected 2 tags, got {resp.Tags.Count}");
                var tagMap = new Dictionary<string, string>();
                foreach (var t in resp.Tags)
                {
                    tagMap[t.Key] = t.Value;
                }
                if (tagMap["key1"] != "val1" || tagMap["key2"] != "val2")
                    throw new Exception("tag content mismatch");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "CreateSecret_WithTags", async () =>
        {
            var sn = TestRunner.MakeUniqueName("TagCreate");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest
                {
                    Name = sn,
                    SecretString = "tagged",
                    Tags = new List<Tag>
                    {
                        new Tag { Key = "Owner", Value = "test-suite" }
                    }
                });
                var resp = await secretsManagerClient.DescribeSecretAsync(new DescribeSecretRequest { SecretId = sn });
                bool found = false;
                foreach (var t in resp.Tags)
                {
                    if (t.Key == "Owner" && t.Value == "test-suite")
                        found = true;
                }
                if (!found)
                    throw new Exception("Owner tag not found in DescribeSecret");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "PutResourcePolicy_Basic", async () =>
        {
            var sn = TestRunner.MakeUniqueName("PolicyTest");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = sn, SecretString = "policy-test" });
                var policy = "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":\"*\",\"Action\":\"secretsmanager:GetSecretValue\",\"Resource\":\"*\"}]}";
                await secretsManagerClient.PutResourcePolicyAsync(new PutResourcePolicyRequest
                {
                    SecretId = sn,
                    ResourcePolicy = policy
                });
                var getResp = await secretsManagerClient.GetResourcePolicyAsync(new GetResourcePolicyRequest { SecretId = sn });
                if (getResp.ResourcePolicy != policy)
                    throw new Exception("policy mismatch");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "DeleteResourcePolicy_Basic", async () =>
        {
            var sn = TestRunner.MakeUniqueName("DelPolicy");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = sn, SecretString = "del-policy" });
                var policy = "{\"Version\":\"2012-10-17\",\"Statement\":[]}";
                await secretsManagerClient.PutResourcePolicyAsync(new PutResourcePolicyRequest { SecretId = sn, ResourcePolicy = policy });
                await secretsManagerClient.DeleteResourcePolicyAsync(new DeleteResourcePolicyRequest { SecretId = sn });
                var getResp = await secretsManagerClient.GetResourcePolicyAsync(new GetResourcePolicyRequest { SecretId = sn });
                if (getResp.ResourcePolicy != null)
                    throw new Exception("policy should be nil after deletion");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "ValidateResourcePolicy_Valid", async () =>
        {
            var policy = "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":\"*\",\"Action\":\"*\",\"Resource\":\"*\"}]}";
            var resp = await secretsManagerClient.ValidateResourcePolicyAsync(new ValidateResourcePolicyRequest { ResourcePolicy = policy });
            if (resp.PolicyValidationPassed != true)
                throw new Exception("expected policy validation to pass");
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "ValidateResourcePolicy_Invalid", async () =>
        {
            var resp = await secretsManagerClient.ValidateResourcePolicyAsync(new ValidateResourcePolicyRequest { ResourcePolicy = "not valid json {" });
            if (resp.PolicyValidationPassed == true)
                throw new Exception("expected policy validation to fail for invalid JSON");
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "GetResourcePolicy_NonExistent", async () =>
        {
            try
            {
                await secretsManagerClient.GetResourcePolicyAsync(new GetResourcePolicyRequest
                {
                    SecretId = "nonexistent-policy-secret"
                });
                throw new Exception("expected error for non-existent secret");
            }
            catch (ResourceNotFoundException) { }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "PutResourcePolicy_NonExistent", async () =>
        {
            try
            {
                await secretsManagerClient.PutResourcePolicyAsync(new PutResourcePolicyRequest
                {
                    SecretId = "nonexistent-policy-secret",
                    ResourcePolicy = "{\"Version\":\"2012-10-17\"}"
                });
                throw new Exception("expected error for non-existent secret");
            }
            catch (ResourceNotFoundException) { }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "BatchGetSecretValue_Basic", async () =>
        {
            var sec1 = TestRunner.MakeUniqueName("Batch1");
            var sec2 = TestRunner.MakeUniqueName("Batch2");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = sec1, SecretString = "batch-value-" + sec1 });
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = sec2, SecretString = "batch-value-" + sec2 });
                var resp = await secretsManagerClient.BatchGetSecretValueAsync(new BatchGetSecretValueRequest
                {
                    SecretIdList = new List<string> { sec1, sec2 }
                });
                if (resp.SecretValues.Count != 2)
                    throw new Exception($"expected 2 secret values, got {resp.SecretValues.Count}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sec1, ForceDeleteWithoutRecovery = true }); });
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sec2, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "BatchGetSecretValue_NonExistent", async () =>
        {
            var sn = TestRunner.MakeUniqueName("BatchNE");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = sn, SecretString = "exists" });
                var resp = await secretsManagerClient.BatchGetSecretValueAsync(new BatchGetSecretValueRequest
                {
                    SecretIdList = new List<string> { sn, "nonexistent-batch-secret" }
                });
                if (resp.SecretValues.Count != 1)
                    throw new Exception($"expected 1 secret value, got {resp.SecretValues.Count}");
                if (resp.Errors.Count != 1)
                    throw new Exception($"expected 1 error, got {resp.Errors.Count}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "RotateSecret_Basic", async () =>
        {
            var sn = TestRunner.MakeUniqueName("RotateTest");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = sn, SecretString = "rotate-me" });
                var resp = await secretsManagerClient.RotateSecretAsync(new RotateSecretRequest { SecretId = sn });
                if (string.IsNullOrEmpty(resp.VersionId))
                    throw new Exception("version ID is nil after rotation");
                var descResp = await secretsManagerClient.DescribeSecretAsync(new DescribeSecretRequest { SecretId = sn });
                if (!descResp.LastRotatedDate.HasValue)
                    throw new Exception("LastRotatedDate should be set after rotation");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "CancelRotateSecret_Basic", async () =>
        {
            var sn = TestRunner.MakeUniqueName("CancelRot");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = sn, SecretString = "cancel-rotate" });
                await secretsManagerClient.RotateSecretAsync(new RotateSecretRequest { SecretId = sn });
                await secretsManagerClient.CancelRotateSecretAsync(new CancelRotateSecretRequest { SecretId = sn });
                var descResp = await secretsManagerClient.DescribeSecretAsync(new DescribeSecretRequest { SecretId = sn });
                if (descResp.RotationEnabled == true)
                    throw new Exception("rotation should be disabled after cancel");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "CreateSecret_WithDescription", async () =>
        {
            var sn = TestRunner.MakeUniqueName("DescTest");
            var desc = "My test secret description";
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest
                {
                    Name = sn,
                    SecretString = "desc-value",
                    Description = desc
                });
                var resp = await secretsManagerClient.DescribeSecretAsync(new DescribeSecretRequest { SecretId = sn });
                if (resp.Description != desc)
                    throw new Exception($"description mismatch: got {resp.Description}, want {desc}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "ListSecrets_Filters", async () =>
        {
            var prefix = TestRunner.MakeUniqueName("FilterTest");
            var alpha = prefix + "-alpha";
            var beta = prefix + "-beta";
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = alpha, SecretString = "a" });
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = beta, SecretString = "b" });
                var resp = await secretsManagerClient.ListSecretsAsync(new ListSecretsRequest
                {
                    Filters = new List<Filter>
                    {
                        new Filter { Key = FilterNameStringType.Name, Values = new List<string> { alpha } }
                    }
                });
                if (resp.SecretList.Count != 1)
                    throw new Exception($"expected 1 secret, got {resp.SecretList.Count}");
                if (resp.SecretList[0].Name != alpha)
                    throw new Exception("wrong secret returned");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = alpha, ForceDeleteWithoutRecovery = true }); });
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = beta, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "DescribeSecret_ContentVerify", async () =>
        {
            var sn = TestRunner.MakeUniqueName("DescVerify");
            try
            {
                var createResp = await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest
                {
                    Name = sn,
                    SecretString = "desc-content",
                    Description = "test description"
                });
                var resp = await secretsManagerClient.DescribeSecretAsync(new DescribeSecretRequest { SecretId = sn });
                if (resp.Name != sn)
                    throw new Exception("name mismatch");
                if (resp.ARN != createResp.ARN)
                    throw new Exception("ARN mismatch");
                if (resp.Description != "test description")
                    throw new Exception("description mismatch");
                if (!resp.CreatedDate.HasValue)
                    throw new Exception("CreatedDate is nil");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "CreateSecret_Binary", async () =>
        {
            var sn = TestRunner.MakeUniqueName("BinaryTest");
            var binaryData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest
                {
                    Name = sn,
                    SecretBinary = new MemoryStream(binaryData)
                });
                var resp = await secretsManagerClient.GetSecretValueAsync(new GetSecretValueRequest { SecretId = sn });
                if (resp.SecretBinary == null)
                    throw new Exception("SecretBinary is nil");
                var resultBytes = resp.SecretBinary.ToArray();
                if (resultBytes.Length != binaryData.Length)
                    throw new Exception("binary data length mismatch");
                for (int i = 0; i < binaryData.Length; i++)
                {
                    if (resultBytes[i] != binaryData[i])
                        throw new Exception("binary data mismatch");
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "UpdateSecret_ClearDescription", async () =>
        {
            var sn = TestRunner.MakeUniqueName("ClearDesc");
            try
            {
                await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest
                {
                    Name = sn,
                    SecretString = "clear-desc",
                    Description = "initial description"
                });
                await secretsManagerClient.UpdateSecretAsync(new UpdateSecretRequest
                {
                    SecretId = sn,
                    Description = ""
                });
                var resp = await secretsManagerClient.DescribeSecretAsync(new DescribeSecretRequest { SecretId = sn });
                if (!string.IsNullOrEmpty(resp.Description))
                    throw new Exception($"description should be cleared, got {resp.Description}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
            }
        }));

        results.Add(await runner.RunTestAsync("secretsmanager", "ListSecrets_Pagination", async () =>
        {
            var pgTs = TestRunner.MakeUniqueName("PagSecret");
            var pgSecrets = new List<string>();
            try
            {
                for (int i = 0; i < 5; i++)
                {
                    var name = pgTs + "-" + i;
                    await secretsManagerClient.CreateSecretAsync(new CreateSecretRequest { Name = name, SecretString = "pagval" });
                    pgSecrets.Add(name);
                }
                var allSecrets = new List<string>();
                string nextToken = null;
                do
                {
                    var req = new ListSecretsRequest { MaxResults = 2 };
                    if (nextToken != null) req.NextToken = nextToken;
                    var resp = await secretsManagerClient.ListSecretsAsync(req);
                    foreach (var s in resp.SecretList)
                    {
                        if (s.Name != null && s.Name.Contains(pgTs))
                            allSecrets.Add(s.Name);
                    }
                    nextToken = resp.NextToken;
                } while (!string.IsNullOrEmpty(nextToken));

                if (allSecrets.Count != 5)
                    throw new Exception($"expected 5 paginated secrets, got {allSecrets.Count}");
            }
            finally
            {
                foreach (var sn in pgSecrets)
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); });
                }
            }
        }));

        return results;
    }
}
