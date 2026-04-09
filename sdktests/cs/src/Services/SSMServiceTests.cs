using Amazon;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class SSMServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonSimpleSystemsManagementClient ssmClient,
        string region)
    {
        var results = new List<TestResult>();
        var paramName = TestRunner.MakeUniqueName("CSParam");

        try
        {
            results.Add(await runner.RunTestAsync("ssm", "PutParameter", async () =>
            {
                await ssmClient.PutParameterAsync(new PutParameterRequest
                {
                    Name = paramName,
                    Value = "test-value",
                    Type = ParameterType.String
                });
            }));

            results.Add(await runner.RunTestAsync("ssm", "GetParameters", async () =>
            {
                var resp = await ssmClient.GetParametersAsync(new GetParametersRequest
                {
                    Names = new List<string> { paramName }
                });
                if (resp.Parameters == null)
                    throw new Exception("Parameters is null");
            }));
        }
        finally
        {
            try
            {
                await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = paramName });
            }
            catch { }
        }

        results.Add(await runner.RunTestAsync("ssm", "MultiByteParameter", async () =>
        {
            var pairs = new (string Label, string Value)[]
            {
                ("ja", "日本語テストパラメータ"),
                ("zh", "简体中文测试参数"),
                ("tw", "繁體中文測試參數"),
            };
            foreach (var (label, value) in pairs)
            {
                var name = $"/test/multibyte-{label}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                await ssmClient.PutParameterAsync(new PutParameterRequest
                {
                    Name = name,
                    Value = value,
                    Type = ParameterType.String
                });
                var resp = await ssmClient.GetParameterAsync(new GetParameterRequest { Name = name });
                if (resp.Parameter.Value != value)
                    throw new Exception($"Mismatch for {label}: expected {value}, got {resp.Parameter.Value}");
                await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = name });
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "GetParameters_NotFound", async () =>
        {
            var resp = await ssmClient.GetParametersAsync(new GetParametersRequest
            {
                Names = new List<string> { "NonExistentParam_xyz_12345" }
            });
            if (resp.InvalidParameters == null || resp.InvalidParameters.Count == 0)
                throw new Exception("Expected InvalidParameters to be populated");
        }));

        results.Add(await runner.RunTestAsync("ssm", "GetParameter", async () =>
        {
            var pn = $"/test/gp-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            try
            {
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "test-val", Type = ParameterType.String });
                var resp = await ssmClient.GetParameterAsync(new GetParameterRequest { Name = pn });
                if (resp.Parameter == null)
                    throw new Exception("parameter is nil");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = pn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "GetParameter_NonExistent", async () =>
        {
            try
            {
                await ssmClient.GetParameterAsync(new GetParameterRequest { Name = "/nonexistent/param-xyz" });
                throw new Exception("expected error for non-existent parameter");
            }
            catch (AmazonSimpleSystemsManagementException) { }
        }));

        results.Add(await runner.RunTestAsync("ssm", "GetParametersByPath", async () =>
        {
            var pn = $"/test/gpbp-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            try
            {
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "test-val", Type = ParameterType.String });
                var resp = await ssmClient.GetParametersByPathAsync(new GetParametersByPathRequest { Path = "/test", Recursive = true });
                if (resp.Parameters == null)
                    throw new Exception("parameters list is nil");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = pn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "DeleteParameter_NonExistent", async () =>
        {
            try
            {
                await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = "/nonexistent/param-xyz" });
                throw new Exception("expected error for non-existent parameter");
            }
            catch (AmazonSimpleSystemsManagementException) { }
        }));

        results.Add(await runner.RunTestAsync("ssm", "PutParameter_GetParameter_Roundtrip", async () =>
        {
            var rtName = $"/rt/param-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            var rtValue = "roundtrip-value-12345";
            try
            {
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = rtName, Value = rtValue, Type = ParameterType.String });
                var resp = await ssmClient.GetParameterAsync(new GetParameterRequest { Name = rtName });
                if (resp.Parameter == null)
                    throw new Exception("parameter is nil");
                if (resp.Parameter.Value != rtValue)
                    throw new Exception($"value mismatch: got {resp.Parameter.Value}, want {rtValue}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = rtName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "GetParameters_InvalidNames", async () =>
        {
            var validName = $"/valid/param-test-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            try
            {
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = validName, Value = "valid", Type = ParameterType.String });
                var resp = await ssmClient.GetParametersAsync(new GetParametersRequest
                {
                    Names = new List<string> { validName, "/nonexistent/param-xyz" }
                });
                if (resp.Parameters.Count != 1)
                    throw new Exception($"expected 1 valid parameter, got {resp.Parameters.Count}");
                if (resp.InvalidParameters.Count != 1)
                    throw new Exception($"expected 1 invalid parameter, got {resp.InvalidParameters.Count}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = validName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "DescribeParameters_ContainsCreated", async () =>
        {
            var dpName = $"/dp/param-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            try
            {
                await ssmClient.PutParameterAsync(new PutParameterRequest
                {
                    Name = dpName,
                    Value = "desc-test",
                    Type = ParameterType.String,
                    Description = "Test description for search"
                });
                var resp = await ssmClient.DescribeParametersAsync(new DescribeParametersRequest
                {
                    Filters = new List<ParametersFilter>
                    {
                        new ParametersFilter { Key = ParametersFilterKey.Name, Values = new List<string> { dpName } }
                    }
                });
                if (resp.Parameters.Count != 1)
                    throw new Exception($"expected 1 parameter, got {resp.Parameters.Count}");
                if (resp.Parameters[0].Description != "Test description for search")
                    throw new Exception("description mismatch");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = dpName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "DescribeParameters", async () =>
        {
            var dp1 = $"/test/desc/p1-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            var dp2 = $"/test/desc/p2-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            try
            {
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = dp1, Value = "v1", Type = ParameterType.String, Description = "Test parameter 1" });
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = dp2, Value = "v2", Type = ParameterType.String, Description = "Test parameter 2" });
                var resp = await ssmClient.DescribeParametersAsync(new DescribeParametersRequest());
                if (resp.Parameters == null || resp.Parameters.Count < 2)
                    throw new Exception($"expected at least 2 parameters, got {resp.Parameters?.Count ?? 0}");
                var found = 0;
                foreach (var p in resp.Parameters)
                {
                    if (p.Name == dp1 || p.Name == dp2)
                        found++;
                }
                if (found < 2)
                    throw new Exception($"expected to find 2 parameters, found {found}");
            }
            finally
            {
                try { await ssmClient.DeleteParametersAsync(new DeleteParametersRequest { Names = new List<string> { dp1, dp2 } }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "DeleteParameter", async () =>
        {
            var pn = $"/test/del-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "to-delete", Type = ParameterType.String });
            await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = pn });
            try
            {
                await ssmClient.GetParameterAsync(new GetParameterRequest { Name = pn });
                throw new Exception("expected error when getting deleted parameter");
            }
            catch (AmazonSimpleSystemsManagementException) { }
        }));

        results.Add(await runner.RunTestAsync("ssm", "AddTagsToResource_ListTagsForResource_RemoveTagsFromResource", async () =>
        {
            var pn = $"/test/tags/param-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            try
            {
                var putResp = await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "tagged", Type = ParameterType.String });
                var resourceType = "Parameter";
                await ssmClient.AddTagsToResourceAsync(new AddTagsToResourceRequest
                {
                    ResourceType = resourceType,
                    ResourceId = pn,
                    Tags = new List<Tag>
                    {
                        new Tag { Key = "Environment", Value = "test" },
                        new Tag { Key = "Owner", Value = "sdk-tests" }
                    }
                });
                var listResp = await ssmClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceType = resourceType,
                    ResourceId = pn
                });
                if (listResp.TagList == null || listResp.TagList.Count != 2)
                    throw new Exception($"expected 2 tags, got {listResp.TagList?.Count ?? 0}");
                await ssmClient.RemoveTagsFromResourceAsync(new RemoveTagsFromResourceRequest
                {
                    ResourceType = resourceType,
                    ResourceId = pn,
                    TagKeys = new List<string> { "Environment" }
                });
                var listResp2 = await ssmClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceType = resourceType,
                    ResourceId = pn
                });
                if (listResp2.TagList == null || listResp2.TagList.Count != 1)
                    throw new Exception($"expected 1 tag after removal, got {listResp2.TagList?.Count ?? 0}");
                if (listResp2.TagList[0].Key != "Owner")
                    throw new Exception("expected remaining tag to be Owner");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = pn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "AddTagsToResource_Merge_ListTagsForResource_Empty", async () =>
        {
            var pn = $"/test/tags/merge-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            try
            {
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "merge", Type = ParameterType.String });
                var resourceType = "Parameter";
                var listBefore = await ssmClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceType = resourceType,
                    ResourceId = pn
                });
                if (listBefore.TagList != null && listBefore.TagList.Count > 0)
                    throw new Exception($"expected no tags initially, got {listBefore.TagList.Count}");
                await ssmClient.AddTagsToResourceAsync(new AddTagsToResourceRequest
                {
                    ResourceType = resourceType,
                    ResourceId = pn,
                    Tags = new List<Tag> { new Tag { Key = "Team", Value = "backend" } }
                });
                await ssmClient.AddTagsToResourceAsync(new AddTagsToResourceRequest
                {
                    ResourceType = resourceType,
                    ResourceId = pn,
                    Tags = new List<Tag> { new Tag { Key = "Env", Value = "prod" } }
                });
                var listAfter = await ssmClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceType = resourceType,
                    ResourceId = pn
                });
                if (listAfter.TagList == null || listAfter.TagList.Count != 2)
                    throw new Exception($"expected 2 tags after merge, got {listAfter.TagList?.Count ?? 0}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = pn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "AddTagsToResource_NonExistent", async () =>
        {
            try
            {
                await ssmClient.AddTagsToResourceAsync(new AddTagsToResourceRequest
                {
                    ResourceType = "Parameter",
                    ResourceId = "/nonexistent/param-tag-xyz",
                    Tags = new List<Tag> { new Tag { Key = "Env", Value = "test" } }
                });
                throw new Exception("expected error when tagging non-existent resource");
            }
            catch (AmazonSimpleSystemsManagementException) { }
        }));

        results.Add(await runner.RunTestAsync("ssm", "RemoveTagsFromResource_NonExistent", async () =>
        {
            try
            {
                await ssmClient.RemoveTagsFromResourceAsync(new RemoveTagsFromResourceRequest
                {
                    ResourceType = "Parameter",
                    ResourceId = "/nonexistent/param-tag-xyz",
                    TagKeys = new List<string> { "Env" }
                });
                throw new Exception("expected error when removing tags from non-existent resource");
            }
            catch (AmazonSimpleSystemsManagementException) { }
        }));

        results.Add(await runner.RunTestAsync("ssm", "PutParameter_Overwrite_IncrementsVersion", async () =>
        {
            var pn = $"/test/overwrite-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            try
            {
                var putResp1 = await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "v1", Type = ParameterType.String });
                if (putResp1.Version != 1)
                    throw new Exception($"expected version 1, got {putResp1.Version}");
                var putResp2 = await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "v2", Type = ParameterType.String, Overwrite = true });
                if (putResp2.Version != 2)
                    throw new Exception($"expected version 2 after overwrite, got {putResp2.Version}");
                var getResp = await ssmClient.GetParameterAsync(new GetParameterRequest { Name = pn });
                if (getResp.Parameter.Value != "v2")
                    throw new Exception($"expected value v2, got {getResp.Parameter.Value}");
                if (getResp.Parameter.Version != 2)
                    throw new Exception($"expected parameter version 2, got {getResp.Parameter.Version}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = pn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "PutParameter_Duplicate_NoOverwrite", async () =>
        {
            var pn = $"/test/dup-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            try
            {
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "original", Type = ParameterType.String });
                try
                {
                    await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "duplicate", Type = ParameterType.String });
                    throw new Exception("expected error when putting duplicate without Overwrite");
                }
                catch (AmazonSimpleSystemsManagementException) { }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = pn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "GetParameterHistory_TwoVersions", async () =>
        {
            var pn = $"/test/history-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            try
            {
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "v1", Type = ParameterType.String });
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "v2", Type = ParameterType.String, Overwrite = true });
                var histResp = await ssmClient.GetParameterHistoryAsync(new GetParameterHistoryRequest { Name = pn });
                if (histResp.Parameters == null || histResp.Parameters.Count != 2)
                    throw new Exception($"expected 2 history entries, got {histResp.Parameters?.Count ?? 0}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = pn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "GetParameterHistory_ContainsLabels", async () =>
        {
            var pn = $"/test/histlabel-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            try
            {
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "v1", Type = ParameterType.String });
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "v2", Type = ParameterType.String, Overwrite = true });
                await ssmClient.LabelParameterVersionAsync(new LabelParameterVersionRequest
                {
                    Name = pn,
                    ParameterVersion = 1,
                    Labels = new List<string> { "old-version" }
                });
                var histResp = await ssmClient.GetParameterHistoryAsync(new GetParameterHistoryRequest { Name = pn });
                if (histResp.Parameters == null)
                    throw new Exception("history is null");
                var v1Entry = histResp.Parameters.FirstOrDefault(p => p.Version == 1);
                if (v1Entry == null)
                    throw new Exception("version 1 not found in history");
                if (v1Entry.Labels == null || !v1Entry.Labels.Contains("old-version"))
                    throw new Exception("expected label 'old-version' on version 1");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = pn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "LabelParameterVersion_GetByLabel", async () =>
        {
            var pn = $"/test/labelget-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            try
            {
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "v1", Type = ParameterType.String });
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "v2", Type = ParameterType.String, Overwrite = true });
                await ssmClient.LabelParameterVersionAsync(new LabelParameterVersionRequest
                {
                    Name = pn,
                    ParameterVersion = 1,
                    Labels = new List<string> { "my-label" }
                });
                var histResp = await ssmClient.GetParameterHistoryAsync(new GetParameterHistoryRequest { Name = pn });
                if (histResp.Parameters == null)
                    throw new Exception("history is null");
                var labeled = histResp.Parameters.FirstOrDefault(p => p.Labels != null && p.Labels.Contains("my-label"));
                if (labeled == null)
                    throw new Exception("labeled version not found in history");
                if (labeled.Value != "v1")
                    throw new Exception($"expected value v1 for labeled version, got {labeled.Value}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = pn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "LabelParameterVersion_MovesLabel", async () =>
        {
            var pn = $"/test/movelabel-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            try
            {
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "v1", Type = ParameterType.String });
                await ssmClient.LabelParameterVersionAsync(new LabelParameterVersionRequest
                {
                    Name = pn,
                    ParameterVersion = 1,
                    Labels = new List<string> { "current" }
                });
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "v2", Type = ParameterType.String, Overwrite = true });
                await ssmClient.LabelParameterVersionAsync(new LabelParameterVersionRequest
                {
                    Name = pn,
                    ParameterVersion = 2,
                    Labels = new List<string> { "current" }
                });
                var histResp = await ssmClient.GetParameterHistoryAsync(new GetParameterHistoryRequest { Name = pn });
                if (histResp.Parameters == null)
                    throw new Exception("history is null");
                var labeled = histResp.Parameters.FirstOrDefault(p => p.Labels != null && p.Labels.Contains("current"));
                if (labeled == null)
                    throw new Exception("moved label not found in history");
                if (labeled.Value != "v2")
                    throw new Exception($"expected value v2 after label move, got {labeled.Value}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = pn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "DeleteParameters_Success", async () =>
        {
            var dp1 = $"/test/delbatch/p1-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            var dp2 = $"/test/delbatch/p2-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            await ssmClient.PutParameterAsync(new PutParameterRequest { Name = dp1, Value = "v1", Type = ParameterType.String });
            await ssmClient.PutParameterAsync(new PutParameterRequest { Name = dp2, Value = "v2", Type = ParameterType.String });
            var delResp = await ssmClient.DeleteParametersAsync(new DeleteParametersRequest
            {
                Names = new List<string> { dp1, dp2 }
            });
            if (delResp.DeletedParameters == null || delResp.DeletedParameters.Count != 2)
                throw new Exception($"expected 2 deleted parameters, got {delResp.DeletedParameters?.Count ?? 0}");
            if (delResp.InvalidParameters != null && delResp.InvalidParameters.Count > 0)
                throw new Exception($"expected no invalid parameters, got {delResp.InvalidParameters.Count}");
        }));

        results.Add(await runner.RunTestAsync("ssm", "DeleteParameters_MixedValidInvalid", async () =>
        {
            var dp1 = $"/test/delmix/p1-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            var dp2 = $"/test/delmix/p2-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            var nonExistent = $"/test/delmix/nonexistent-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            await ssmClient.PutParameterAsync(new PutParameterRequest { Name = dp1, Value = "v1", Type = ParameterType.String });
            await ssmClient.PutParameterAsync(new PutParameterRequest { Name = dp2, Value = "v2", Type = ParameterType.String });
            var delResp = await ssmClient.DeleteParametersAsync(new DeleteParametersRequest
            {
                Names = new List<string> { dp1, dp2, nonExistent }
            });
            if (delResp.DeletedParameters == null || delResp.DeletedParameters.Count != 2)
                throw new Exception($"expected 2 deleted parameters, got {delResp.DeletedParameters?.Count ?? 0}");
            if (delResp.InvalidParameters == null || delResp.InvalidParameters.Count != 1)
                throw new Exception($"expected 1 invalid parameter, got {delResp.InvalidParameters?.Count ?? 0}");
        }));

        results.Add(await runner.RunTestAsync("ssm", "GetParametersByPath_NonRecursive", async () =>
        {
            var nrBase = $"/test/nr-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            var p1 = $"{nrBase}/param1";
            var p2 = $"{nrBase}/nested/param2";
            try
            {
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = p1, Value = "v1", Type = ParameterType.String });
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = p2, Value = "v2", Type = ParameterType.String });
                var resp = await ssmClient.GetParametersByPathAsync(new GetParametersByPathRequest { Path = nrBase, Recursive = false });
                if (resp.Parameters == null || resp.Parameters.Count != 1)
                    throw new Exception($"expected 1 parameter non-recursive, got {resp.Parameters?.Count ?? 0}");
                if (resp.Parameters[0].Name != p1)
                    throw new Exception($"expected {p1}, got {resp.Parameters[0].Name}");
            }
            finally
            {
                try { await ssmClient.DeleteParametersAsync(new DeleteParametersRequest { Names = new List<string> { p1, p2 } }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "GetParameter_WithVersionSelector", async () =>
        {
            var pn = $"/test/ver-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            try
            {
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "v1", Type = ParameterType.String });
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "v2", Type = ParameterType.String, Overwrite = true });
                var histResp = await ssmClient.GetParameterHistoryAsync(new GetParameterHistoryRequest { Name = pn });
                if (histResp.Parameters == null || histResp.Parameters.Count != 2)
                    throw new Exception($"expected 2 history entries, got {histResp.Parameters?.Count ?? 0}");
                var v1Entry = histResp.Parameters.FirstOrDefault(p => p.Version == 1);
                var v2Entry = histResp.Parameters.FirstOrDefault(p => p.Version == 2);
                if (v1Entry == null || v1Entry.Value != "v1")
                    throw new Exception("version 1 not found or value mismatch");
                if (v2Entry == null || v2Entry.Value != "v2")
                    throw new Exception("version 2 not found or value mismatch");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = pn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "DescribeParameters_TypeFilter", async () =>
        {
            var pn = $"/test/typefilt-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            try
            {
                await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "secure-val", Type = ParameterType.SecureString });
                var resp = await ssmClient.DescribeParametersAsync(new DescribeParametersRequest
                {
                    Filters = new List<ParametersFilter>
                    {
                        new ParametersFilter { Key = ParametersFilterKey.Type, Values = new List<string> { "SecureString" } }
                    }
                });
                if (resp.Parameters == null || resp.Parameters.Count == 0)
                    throw new Exception("expected at least one SecureString parameter");
                var found = resp.Parameters.Any(p => p.Name == pn);
                if (!found)
                    throw new Exception("created SecureString parameter not found");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = pn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "PutParameter_ReturnsVersionAndTier", async () =>
        {
            var pn = $"/test/retvt-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            try
            {
                var resp = await ssmClient.PutParameterAsync(new PutParameterRequest { Name = pn, Value = "val", Type = ParameterType.String });
                if (resp.Version != 1)
                    throw new Exception($"expected version 1, got {resp.Version}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = pn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("ssm", "DescribeParameters_Pagination", async () =>
        {
            var pagBase = $"/test/pag-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            var names = new List<string>();
            try
            {
                for (int i = 0; i < 5; i++)
                {
                    var n = $"{pagBase}/p{i}";
                    names.Add(n);
                    await ssmClient.PutParameterAsync(new PutParameterRequest { Name = n, Value = $"v{i}", Type = ParameterType.String });
                }
                var allParams = new List<ParameterMetadata>();
                string? nextToken = null;
                do
                {
                    var resp = await ssmClient.DescribeParametersAsync(new DescribeParametersRequest
                    {
                        Filters = new List<ParametersFilter>
                        {
                            new ParametersFilter { Key = ParametersFilterKey.Name, Values = new List<string> { pagBase } }
                        },
                        NextToken = nextToken,
                        MaxResults = 2
                    });
                    if (resp.Parameters != null)
                        allParams.AddRange(resp.Parameters);
                    nextToken = resp.NextToken;
                } while (nextToken != null);
                if (allParams.Count != 5)
                    throw new Exception($"expected 5 parameters via pagination, got {allParams.Count}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await ssmClient.DeleteParametersAsync(new DeleteParametersRequest { Names = names }); });
            }
        }));

        return results;
    }
}
