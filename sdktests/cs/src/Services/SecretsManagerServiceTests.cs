using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.Runtime;

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
                try { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); } catch { }
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
                try { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); } catch { }
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
                try { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); } catch { }
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
                try { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = sn, ForceDeleteWithoutRecovery = true }); } catch { }
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
                try { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = dupName, ForceDeleteWithoutRecovery = true }); } catch { }
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
                try { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = secName, ForceDeleteWithoutRecovery = true }); } catch { }
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
                try { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = secName, ForceDeleteWithoutRecovery = true }); } catch { }
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
                try { await secretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest { SecretId = secName, ForceDeleteWithoutRecovery = true }); } catch { }
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

        return results;
    }
}
