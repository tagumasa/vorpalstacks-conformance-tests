using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class LambdaServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonLambdaClient lambdaClient,
        AmazonIdentityManagementServiceClient iamClient,
        string region)
    {
        var results = new List<TestResult>();
        var functionName = TestRunner.MakeUniqueName("CSFunc");
        var roleName = TestRunner.MakeUniqueName("CSRole");
        var functionCode = new MemoryStream(new byte[]
        {
            0x50, 0x4b, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00, 0x08, 0x00
        });
        var trustPolicy = @"{
            ""Version"": ""2012-10-17"",
            ""Statement"": [
                {
                    ""Effect"": ""Allow"",
                    ""Principal"": { ""Service"": ""lambda.amazonaws.com"" },
                    ""Action"": ""sts:AssumeRole""
                }
            ]
        }";

        var roleArn = $"arn:aws:iam::000000000000:role/{roleName}";
        var functionARN = $"arn:aws:lambda:{region}:000000000000:function:{functionName}";

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

            results.Add(await runner.RunTestAsync("lambda", "CreateFunction", async () =>
            {
                await lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                {
                    FunctionName = functionName,
                    Runtime = Runtime.Nodejs20X,
                    Role = roleArn,
                    Handler = "index.handler",
                    Code = new FunctionCode { ZipFile = new MemoryStream(functionCode.ToArray()) }
                });
            }));

            results.Add(await runner.RunTestAsync("lambda", "GetFunction", async () =>
            {
                var resp = await lambdaClient.GetFunctionAsync(new GetFunctionRequest
                {
                    FunctionName = functionName
                });
                if (resp.Configuration == null)
                    throw new Exception("Configuration is null");
            }));

            results.Add(await runner.RunTestAsync("lambda", "GetFunctionConfiguration", async () =>
            {
                var resp = await lambdaClient.GetFunctionConfigurationAsync(new GetFunctionConfigurationRequest
                {
                    FunctionName = functionName
                });
                if (resp.FunctionName == null)
                    throw new Exception("FunctionName is null");
            }));

            results.Add(await runner.RunTestAsync("lambda", "ListFunctions", async () =>
            {
                var resp = await lambdaClient.ListFunctionsAsync(new ListFunctionsRequest());
                if (resp.Functions == null)
                    throw new Exception("Functions is null");
            }));

            results.Add(await runner.RunTestAsync("lambda", "UpdateFunctionCode", async () =>
            {
                await lambdaClient.UpdateFunctionCodeAsync(new UpdateFunctionCodeRequest
                {
                    FunctionName = functionName,
                    ZipFile = new MemoryStream(new byte[] { 0x50, 0x4b, 0x03, 0x04 })
                });
            }));

            results.Add(await runner.RunTestAsync("lambda", "UpdateFunctionConfiguration", async () =>
            {
                await lambdaClient.UpdateFunctionConfigurationAsync(new UpdateFunctionConfigurationRequest
                {
                    FunctionName = functionName,
                    Description = "Updated function"
                });
            }));

            results.Add(await runner.RunTestAsync("lambda", "PublishVersion", async () =>
            {
                await lambdaClient.PublishVersionAsync(new PublishVersionRequest
                {
                    FunctionName = functionName
                });
            }));

            results.Add(await runner.RunTestAsync("lambda", "ListVersionsByFunction", async () =>
            {
                var resp = await lambdaClient.ListVersionsByFunctionAsync(new ListVersionsByFunctionRequest
                {
                    FunctionName = functionName
                });
                if (resp.Versions == null)
                    throw new Exception("Versions is null");
            }));

            results.Add(await runner.RunTestAsync("lambda", "Invoke", async () =>
            {
                var resp = await lambdaClient.InvokeAsync(new InvokeRequest
                {
                    FunctionName = functionName
                });
                if (resp.StatusCode == 0)
                    throw new Exception("StatusCode is zero");
            }));

            results.Add(await runner.RunTestAsync("lambda", "CreateAlias", async () =>
            {
                var resp = await lambdaClient.CreateAliasAsync(new CreateAliasRequest
                {
                    FunctionName = functionName,
                    Name = "live",
                    FunctionVersion = "$LATEST"
                });
                if (resp.AliasArn == null)
                    throw new Exception("AliasArn is null");
            }));

            results.Add(await runner.RunTestAsync("lambda", "GetAlias", async () =>
            {
                var resp = await lambdaClient.GetAliasAsync(new GetAliasRequest
                {
                    FunctionName = functionName,
                    Name = "live"
                });
                if (resp.Name == null)
                    throw new Exception("alias name is nil");
            }));

            results.Add(await runner.RunTestAsync("lambda", "UpdateAlias", async () =>
            {
                await lambdaClient.UpdateAliasAsync(new UpdateAliasRequest
                {
                    FunctionName = functionName,
                    Name = "live",
                    Description = "Production alias"
                });
            }));

            results.Add(await runner.RunTestAsync("lambda", "ListAliases", async () =>
            {
                var resp = await lambdaClient.ListAliasesAsync(new ListAliasesRequest
                {
                    FunctionName = functionName
                });
                if (resp.Aliases == null)
                    throw new Exception("aliases list is nil");
            }));

            results.Add(await runner.RunTestAsync("lambda", "PutFunctionConcurrency", async () =>
            {
                await lambdaClient.PutFunctionConcurrencyAsync(new PutFunctionConcurrencyRequest
                {
                    FunctionName = functionName,
                    ReservedConcurrentExecutions = 10
                });
            }));

            results.Add(await runner.RunTestAsync("lambda", "GetFunctionConcurrency", async () =>
            {
                var resp = await lambdaClient.GetFunctionConcurrencyAsync(new GetFunctionConcurrencyRequest
                {
                    FunctionName = functionName
                });
                if (resp.ReservedConcurrentExecutions == null)
                    throw new Exception("concurrency is nil");
            }));

            results.Add(await runner.RunTestAsync("lambda", "DeleteFunctionConcurrency", async () =>
            {
                await lambdaClient.DeleteFunctionConcurrencyAsync(new DeleteFunctionConcurrencyRequest
                {
                    FunctionName = functionName
                });
            }));

            results.Add(await runner.RunTestAsync("lambda", "AddPermission", async () =>
            {
                await lambdaClient.AddPermissionAsync(new AddPermissionRequest
                {
                    FunctionName = functionName,
                    StatementId = "test-perm-1",
                    Action = "lambda:InvokeFunction",
                    Principal = "apigateway.amazonaws.com"
                });
            }));

            results.Add(await runner.RunTestAsync("lambda", "GetPolicy", async () =>
            {
                var resp = await lambdaClient.GetPolicyAsync(new Amazon.Lambda.Model.GetPolicyRequest
                {
                    FunctionName = functionName
                });
                if (string.IsNullOrEmpty(resp.Policy))
                    throw new Exception("policy is empty");
            }));

            results.Add(await runner.RunTestAsync("lambda", "RemovePermission", async () =>
            {
                await lambdaClient.AddPermissionAsync(new AddPermissionRequest
                {
                    FunctionName = functionName,
                    StatementId = "test-perm-2",
                    Action = "lambda:InvokeFunction",
                    Principal = "apigateway.amazonaws.com"
                });
                await lambdaClient.RemovePermissionAsync(new RemovePermissionRequest
                {
                    FunctionName = functionName,
                    StatementId = "test-perm-2"
                });
            }));

            results.Add(await runner.RunTestAsync("lambda", "TagResource", async () =>
            {
                await lambdaClient.TagResourceAsync(new TagResourceRequest
                {
                    Resource = functionARN,
                    Tags = new Dictionary<string, string>
                    {
                        { "Environment", "test" },
                        { "Project", "sdk-tests" }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("lambda", "ListTags", async () =>
            {
                var resp = await lambdaClient.ListTagsAsync(new ListTagsRequest
                {
                    Resource = functionARN
                });
                if (resp.Tags == null)
                    throw new Exception("tags is nil");
            }));

            results.Add(await runner.RunTestAsync("lambda", "UntagResource", async () =>
            {
                await lambdaClient.UntagResourceAsync(new UntagResourceRequest
                {
                    Resource = functionARN,
                    TagKeys = new List<string> { "Environment" }
                });
            }));

            results.Add(await runner.RunTestAsync("lambda", "GetAccountSettings", async () =>
            {
                var resp = await lambdaClient.GetAccountSettingsAsync(new GetAccountSettingsRequest());
                if (resp.AccountLimit == null)
                    throw new Exception("account limit is nil");
            }));

            results.Add(await runner.RunTestAsync("lambda", "DeleteAlias", async () =>
            {
                await lambdaClient.DeleteAliasAsync(new DeleteAliasRequest
                {
                    FunctionName = functionName,
                    Name = "live"
                });
            }));

            results.Add(await runner.RunTestAsync("lambda", "DeleteFunction", async () =>
            {
                await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest
                {
                    FunctionName = functionName
                });
            }));
        }
        finally
        {
            try { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = functionName }); } catch { }
            try { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = roleName }); } catch { }
        }

        results.Add(await runner.RunTestAsync("lambda", "GetFunction_NonExistent", async () =>
        {
            try
            {
                await lambdaClient.GetFunctionAsync(new GetFunctionRequest
                {
                    FunctionName = "NoSuchFunction_xyz_12345"
                });
                throw new Exception("Expected error but got none");
            }
            catch (AmazonLambdaException) { }
        }));

        results.Add(await runner.RunTestAsync("lambda", "Invoke_NonExistent", async () =>
        {
            try
            {
                await lambdaClient.InvokeAsync(new InvokeRequest
                {
                    FunctionName = "NoSuchFunction_xyz_12345"
                });
                throw new Exception("Expected error but got none");
            }
            catch (AmazonLambdaException) { }
        }));

        results.Add(await runner.RunTestAsync("lambda", "UpdateFunctionCode_NonExistent", async () =>
        {
            try
            {
                await lambdaClient.UpdateFunctionCodeAsync(new UpdateFunctionCodeRequest
                {
                    FunctionName = "NoSuchFunction_xyz_12345",
                    ZipFile = new MemoryStream(new byte[] { 0x50, 0x4b })
                });
                throw new Exception("Expected error but got none");
            }
            catch (AmazonLambdaException) { }
        }));

        results.Add(await runner.RunTestAsync("lambda", "DeleteFunction_NonExistent", async () =>
        {
            try
            {
                await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest
                {
                    FunctionName = "NoSuchFunction_xyz_12345"
                });
                throw new Exception("Expected error but got none");
            }
            catch (AmazonLambdaException) { }
        }));

        results.Add(await runner.RunTestAsync("lambda", "CreateFunction_DuplicateName", async () =>
        {
            var dupName = TestRunner.MakeUniqueName("DupFunc");
            var dupRoleName = TestRunner.MakeUniqueName("DupRole");
            var dupRole = $"arn:aws:iam::000000000000:role/{dupRoleName}";
            try
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = dupRoleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            }
            catch { }
            try
            {
                await lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                {
                    FunctionName = dupName,
                    Runtime = Runtime.Nodejs20X,
                    Role = dupRole,
                    Handler = "index.handler",
                    Code = new FunctionCode { ZipFile = new MemoryStream(functionCode.ToArray()) }
                });
                try
                {
                    await lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                    {
                        FunctionName = dupName,
                        Runtime = Runtime.Nodejs20X,
                        Role = dupRole,
                        Handler = "index.handler",
                        Code = new FunctionCode { ZipFile = new MemoryStream(functionCode.ToArray()) }
                    });
                    throw new Exception("Expected error for duplicate function name");
                }
                catch (ResourceConflictException) { }
            }
            finally
            {
                try { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = dupName }); } catch { }
                try { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = dupRoleName }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("lambda", "Invoke_VerifyResponsePayload", async () =>
        {
            var invFunc = TestRunner.MakeUniqueName("InvFunc");
            var invRoleName = TestRunner.MakeUniqueName("InvRole");
            var invRole = $"arn:aws:iam::000000000000:role/{invRoleName}";
            var invCode = System.Text.Encoding.UTF8.GetBytes("exports.handler = async (event) => { return { statusCode: 200, body: JSON.stringify({result: 'ok'}) }; };");
            try
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = invRoleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            }
            catch { }
            try
            {
                await lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                {
                    FunctionName = invFunc,
                    Runtime = Runtime.Nodejs20X,
                    Role = invRole,
                    Handler = "index.handler",
                    Code = new FunctionCode { ZipFile = new MemoryStream(invCode) }
                });
                var resp = await lambdaClient.InvokeAsync(new InvokeRequest
                {
                    FunctionName = invFunc
                });
                if (resp.StatusCode != 200)
                    throw new Exception($"expected status 200, got {resp.StatusCode}");
                if (resp.Payload == null || resp.Payload.Length == 0)
                    throw new Exception("expected non-empty payload");
            }
            finally
            {
                try { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = invFunc }); } catch { }
                try { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = invRoleName }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("lambda", "GetFunction_ContainsCodeConfig", async () =>
        {
            var gfcFunc = TestRunner.MakeUniqueName("GfcFunc");
            var gfcRoleName = TestRunner.MakeUniqueName("GfcRole");
            var gfcRole = $"arn:aws:iam::000000000000:role/{gfcRoleName}";
            var gfcDesc = "Test description for verification";
            try
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = gfcRoleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            }
            catch { }
            try
            {
                await lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                {
                    FunctionName = gfcFunc,
                    Runtime = Runtime.Nodejs20X,
                    Role = gfcRole,
                    Handler = "index.handler",
                    Code = new FunctionCode { ZipFile = new MemoryStream(functionCode.ToArray()) },
                    Description = gfcDesc,
                    Timeout = 15,
                    MemorySize = 256
                });
                var resp = await lambdaClient.GetFunctionAsync(new GetFunctionRequest
                {
                    FunctionName = gfcFunc
                });
                if (resp.Configuration == null)
                    throw new Exception("configuration is nil");
                if (resp.Configuration.Description != gfcDesc)
                    throw new Exception($"description mismatch, got {resp.Configuration.Description}");
                if (resp.Configuration.Timeout != 15)
                    throw new Exception($"timeout mismatch, got {resp.Configuration.Timeout}");
                if (resp.Configuration.MemorySize != 256)
                    throw new Exception($"memory size mismatch, got {resp.Configuration.MemorySize}");
                if (resp.Code == null || string.IsNullOrEmpty(resp.Code.Location))
                    throw new Exception("code location should not be nil");
            }
            finally
            {
                try { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = gfcFunc }); } catch { }
                try { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = gfcRoleName }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("lambda", "PublishVersion_VerifyVersion", async () =>
        {
            var pvFunc = TestRunner.MakeUniqueName("PvFunc");
            var pvRoleName = TestRunner.MakeUniqueName("PvRole");
            var pvRole = $"arn:aws:iam::000000000000:role/{pvRoleName}";
            try
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = pvRoleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            }
            catch { }
            try
            {
                await lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                {
                    FunctionName = pvFunc,
                    Runtime = Runtime.Nodejs20X,
                    Role = pvRole,
                    Handler = "index.handler",
                    Code = new FunctionCode { ZipFile = new MemoryStream(functionCode.ToArray()) }
                });
                var resp = await lambdaClient.PublishVersionAsync(new PublishVersionRequest
                {
                    FunctionName = pvFunc
                });
                if (resp.Version == "$LATEST")
                    throw new Exception($"published version should not be $LATEST, got {resp.Version}");
                if (resp.Version != "1")
                    throw new Exception($"first published version should be 1, got {resp.Version}");
            }
            finally
            {
                try { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = pvFunc }); } catch { }
                try { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = pvRoleName }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("lambda", "ListFunctions_ReturnsCreated", async () =>
        {
            var lfFunc = TestRunner.MakeUniqueName("LfFunc");
            var lfRoleName = TestRunner.MakeUniqueName("LfRole");
            var lfRole = $"arn:aws:iam::000000000000:role/{lfRoleName}";
            try
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = lfRoleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            }
            catch { }
            try
            {
                await lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                {
                    FunctionName = lfFunc,
                    Runtime = Runtime.Nodejs20X,
                    Role = lfRole,
                    Handler = "index.handler",
                    Code = new FunctionCode { ZipFile = new MemoryStream(functionCode.ToArray()) }
                });
                var resp = await lambdaClient.ListFunctionsAsync(new ListFunctionsRequest());
                var found = resp.Functions.Any(f => f.FunctionName == lfFunc);
                if (!found)
                    throw new Exception($"created function {lfFunc} not found in ListFunctions");
            }
            finally
            {
                try { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = lfFunc }); } catch { }
                try { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = lfRoleName }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("lambda", "CreateAlias_DuplicateName", async () =>
        {
            var caFunc = TestRunner.MakeUniqueName("CaFunc");
            var caRoleName = TestRunner.MakeUniqueName("CaRole");
            var caRole = $"arn:aws:iam::000000000000:role/{caRoleName}";
            try
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = caRoleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            }
            catch { }
            try
            {
                await lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                {
                    FunctionName = caFunc,
                    Runtime = Runtime.Nodejs20X,
                    Role = caRole,
                    Handler = "index.handler",
                    Code = new FunctionCode { ZipFile = new MemoryStream(functionCode.ToArray()) }
                });
                await lambdaClient.CreateAliasAsync(new CreateAliasRequest
                {
                    FunctionName = caFunc,
                    Name = "prod",
                    FunctionVersion = "$LATEST"
                });
                try
                {
                    await lambdaClient.CreateAliasAsync(new CreateAliasRequest
                    {
                        FunctionName = caFunc,
                        Name = "prod",
                        FunctionVersion = "$LATEST"
                    });
                    throw new Exception("Expected error for duplicate alias name");
                }
                catch (ResourceConflictException) { }
            }
            finally
            {
                try { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = caFunc }); } catch { }
                try { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = caRoleName }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("lambda", "UpdateFunctionConfiguration_VerifyUpdate", async () =>
        {
            var ucFunc = TestRunner.MakeUniqueName("UcFunc");
            var ucRoleName = TestRunner.MakeUniqueName("UcRole");
            var ucRole = $"arn:aws:iam::000000000000:role/{ucRoleName}";
            try
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = ucRoleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            }
            catch { }
            try
            {
                await lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                {
                    FunctionName = ucFunc,
                    Runtime = Runtime.Nodejs20X,
                    Role = ucRole,
                    Handler = "index.handler",
                    Code = new FunctionCode { ZipFile = new MemoryStream(functionCode.ToArray()) },
                    Description = "original"
                });
                var newDesc = "updated description";
                await lambdaClient.UpdateFunctionConfigurationAsync(new UpdateFunctionConfigurationRequest
                {
                    FunctionName = ucFunc,
                    Description = newDesc,
                    Timeout = 30,
                    MemorySize = 512
                });
                var resp = await lambdaClient.GetFunctionConfigurationAsync(new GetFunctionConfigurationRequest
                {
                    FunctionName = ucFunc
                });
                if (resp.Description != newDesc)
                    throw new Exception($"description not updated, got {resp.Description}");
                if (resp.Timeout != 30)
                    throw new Exception($"timeout not updated, got {resp.Timeout}");
                if (resp.MemorySize != 512)
                    throw new Exception($"memory size not updated, got {resp.MemorySize}");
            }
            finally
            {
                try { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = ucFunc }); } catch { }
                try { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = ucRoleName }); } catch { }
            }
        }));

        return results;
    }
}
