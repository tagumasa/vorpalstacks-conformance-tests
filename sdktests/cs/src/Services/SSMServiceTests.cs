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
                try { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = pn }); } catch { }
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
                try { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = pn }); } catch { }
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
                try { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = rtName }); } catch { }
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
                try { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = validName }); } catch { }
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
                try { await ssmClient.DeleteParameterAsync(new DeleteParameterRequest { Name = dpName }); } catch { }
            }
        }));

        return results;
    }
}
