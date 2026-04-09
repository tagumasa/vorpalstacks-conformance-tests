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
        var trustPolicy = IamHelpers.LambdaTrustPolicy;

        var roleArn = $"arn:aws:iam::000000000000:role/{roleName}";
        var functionARN = $"arn:aws:lambda:{region}:000000000000:function:{functionName}";

        try
        {
            await TestHelpers.SafeCleanupAsync(async () =>
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = roleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            });

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
            await TestHelpers.SafeCleanupAsync(async () => { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = functionName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = roleName }); });
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
                await TestHelpers.SafeCleanupAsync(async () => { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = dupName }); });
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = dupRoleName }); });
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
                await TestHelpers.SafeCleanupAsync(async () => { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = invFunc }); });
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = invRoleName }); });
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
                await TestHelpers.SafeCleanupAsync(async () => { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = gfcFunc }); });
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = gfcRoleName }); });
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
                await TestHelpers.SafeCleanupAsync(async () => { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = pvFunc }); });
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = pvRoleName }); });
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
                await TestHelpers.SafeCleanupAsync(async () => { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = lfFunc }); });
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = lfRoleName }); });
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
                await TestHelpers.SafeCleanupAsync(async () => { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = caFunc }); });
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = caRoleName }); });
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
                await TestHelpers.SafeCleanupAsync(async () => { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = ucFunc }); });
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = ucRoleName }); });
            }
        }));

        // === GROUP A: LAYER OPERATIONS ===

        var layerName = TestRunner.MakeUniqueName("CSLayer");

        results.Add(await runner.RunTestAsync("lambda", "PublishLayerVersion", async () =>
        {
            var resp = await lambdaClient.PublishLayerVersionAsync(new PublishLayerVersionRequest
            {
                LayerName = layerName,
                Content = new LayerVersionContentInput
                {
                    ZipFile = new MemoryStream(functionCode.ToArray())
                },
                Description = "Test layer version",
                CompatibleRuntimes = new List<string> { "nodejs20.x" }
            });
            if (string.IsNullOrEmpty(resp.LayerArn))
                throw new Exception("LayerArn is null");
            if (resp.Version != 1)
                throw new Exception($"expected version 1, got {resp.Version}");
            if (resp.Content == null || string.IsNullOrEmpty(resp.Content.CodeSha256))
                throw new Exception("CodeSha256 is nil");
        }));

        results.Add(await runner.RunTestAsync("lambda", "GetLayerVersion", async () =>
        {
            var resp = await lambdaClient.GetLayerVersionAsync(new GetLayerVersionRequest
            {
                LayerName = layerName,
                VersionNumber = 1
            });
            if (resp.Content == null || string.IsNullOrEmpty(resp.Content.CodeSha256))
                throw new Exception("CodeSha256 is nil");
            if (resp.Version != 1)
                throw new Exception($"expected version 1, got {resp.Version}");
        }));

        results.Add(await runner.RunTestAsync("lambda", "ListLayers", async () =>
        {
            var resp = await lambdaClient.ListLayersAsync(new ListLayersRequest());
            if (resp.Layers == null)
                throw new Exception("layers list is nil");
            var found = resp.Layers.Any(l => l.LayerName == layerName);
            if (!found)
                throw new Exception($"layer {layerName} not found in ListLayers");
        }));

        results.Add(await runner.RunTestAsync("lambda", "ListLayerVersions", async () =>
        {
            var resp = await lambdaClient.ListLayerVersionsAsync(new ListLayerVersionsRequest
            {
                LayerName = layerName
            });
            if (resp.LayerVersions == null)
                throw new Exception("layer versions list is nil");
            if (resp.LayerVersions.Count == 0)
                throw new Exception("expected at least 1 layer version");
        }));

        results.Add(await runner.RunTestAsync("lambda", "DeleteLayerVersion", async () =>
        {
            await lambdaClient.DeleteLayerVersionAsync(new DeleteLayerVersionRequest
            {
                LayerName = layerName,
                VersionNumber = 1
            });
        }));

        // === GROUP B: EVENT SOURCE MAPPING ===

        var esmFuncName = TestRunner.MakeUniqueName("EsmFunc");
        var esmRoleName = TestRunner.MakeUniqueName("EsmRole");
        var esmRole = $"arn:aws:iam::000000000000:role/{esmRoleName}";
        var esmEventSourceArn = "arn:aws:sqs:us-east-1:000000000000:test-queue";
        string? esmUUID = null;

        try
        {
            try
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = esmRoleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            }
            catch { }
            try
            {
                await lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                {
                    FunctionName = esmFuncName,
                    Runtime = Runtime.Nodejs20X,
                    Role = esmRole,
                    Handler = "index.handler",
                    Code = new FunctionCode { ZipFile = new MemoryStream(functionCode.ToArray()) }
                });

                results.Add(await runner.RunTestAsync("lambda", "CreateEventSourceMapping", async () =>
                {
                    var resp = await lambdaClient.CreateEventSourceMappingAsync(new CreateEventSourceMappingRequest
                    {
                        FunctionName = esmFuncName,
                        EventSourceArn = esmEventSourceArn,
                        BatchSize = 10,
                        Enabled = true
                    });
                    if (string.IsNullOrEmpty(resp.UUID))
                        throw new Exception("UUID is nil or empty");
                    esmUUID = resp.UUID;
                }));

                results.Add(await runner.RunTestAsync("lambda", "GetEventSourceMapping", async () =>
                {
                    if (esmUUID == null)
                        throw new Exception("no UUID from CreateEventSourceMapping");
                    var resp = await lambdaClient.GetEventSourceMappingAsync(new GetEventSourceMappingRequest
                    {
                        UUID = esmUUID
                    });
                    if (string.IsNullOrEmpty(resp.FunctionArn))
                        throw new Exception("FunctionArn is nil");
                    if (resp.EventSourceArn != esmEventSourceArn)
                        throw new Exception($"EventSourceArn mismatch, got {resp.EventSourceArn}");
                }));

                results.Add(await runner.RunTestAsync("lambda", "UpdateEventSourceMapping", async () =>
                {
                    if (esmUUID == null)
                        throw new Exception("no UUID from CreateEventSourceMapping");
                    var resp = await lambdaClient.UpdateEventSourceMappingAsync(new UpdateEventSourceMappingRequest
                    {
                        UUID = esmUUID,
                        BatchSize = 50,
                        Enabled = false
                    });
                    if (resp.BatchSize != 50)
                        throw new Exception($"BatchSize not updated, got {resp.BatchSize}");
                }));

                results.Add(await runner.RunTestAsync("lambda", "ListEventSourceMappings", async () =>
                {
                    var resp = await lambdaClient.ListEventSourceMappingsAsync(new ListEventSourceMappingsRequest
                    {
                        FunctionName = esmFuncName
                    });
                    if (resp.EventSourceMappings == null)
                        throw new Exception("event source mappings list is nil");
                    if (resp.EventSourceMappings.Count == 0)
                        throw new Exception("expected at least 1 event source mapping");
                }));

                results.Add(await runner.RunTestAsync("lambda", "DeleteEventSourceMapping", async () =>
                {
                    if (esmUUID == null)
                        throw new Exception("no UUID from CreateEventSourceMapping");
                    await lambdaClient.DeleteEventSourceMappingAsync(new DeleteEventSourceMappingRequest
                    {
                        UUID = esmUUID
                    });
                }));
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = esmFuncName }); });
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = esmRoleName }); });
            }
        }
        catch { }

        results.Add(await runner.RunTestAsync("lambda", "GetEventSourceMapping_NonExistent", async () =>
        {
            try
            {
                await lambdaClient.GetEventSourceMappingAsync(new GetEventSourceMappingRequest
                {
                    UUID = "00000000-0000-0000-0000-000000000000"
                });
                throw new Exception("Expected error for non-existent event source mapping");
            }
            catch (AmazonLambdaException) { }
        }));

        // === GROUP C: PROVISIONED CONCURRENCY ===

        var pcFuncName = TestRunner.MakeUniqueName("PcFunc");
        var pcRoleName = TestRunner.MakeUniqueName("PcRole");
        var pcRole = $"arn:aws:iam::000000000000:role/{pcRoleName}";
        string? pcVersion = null;

        try
        {
            try
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = pcRoleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            }
            catch { }
            try
            {
                await lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                {
                    FunctionName = pcFuncName,
                    Runtime = Runtime.Nodejs20X,
                    Role = pcRole,
                    Handler = "index.handler",
                    Code = new FunctionCode { ZipFile = new MemoryStream(functionCode.ToArray()) }
                });

                var publishResp = await lambdaClient.PublishVersionAsync(new PublishVersionRequest
                {
                    FunctionName = pcFuncName
                });
                pcVersion = publishResp.Version;

                results.Add(await runner.RunTestAsync("lambda", "PutProvisionedConcurrencyConfig", async () =>
                {
                    var resp = await lambdaClient.PutProvisionedConcurrencyConfigAsync(new PutProvisionedConcurrencyConfigRequest
                    {
                        FunctionName = pcFuncName,
                        Qualifier = pcVersion,
                        ProvisionedConcurrentExecutions = 5
                    });
                    if (resp.AllocatedProvisionedConcurrentExecutions == null)
                        throw new Exception("AllocatedProvisionedConcurrentExecutions is nil");
                }));

                results.Add(await runner.RunTestAsync("lambda", "GetProvisionedConcurrencyConfig", async () =>
                {
                    var resp = await lambdaClient.GetProvisionedConcurrencyConfigAsync(new GetProvisionedConcurrencyConfigRequest
                    {
                        FunctionName = pcFuncName,
                        Qualifier = pcVersion
                    });
                    if (string.IsNullOrEmpty(resp.Status))
                        throw new Exception("Status is empty");
                }));

                results.Add(await runner.RunTestAsync("lambda", "ListProvisionedConcurrencyConfigs", async () =>
                {
                    var resp = await lambdaClient.ListProvisionedConcurrencyConfigsAsync(new ListProvisionedConcurrencyConfigsRequest
                    {
                        FunctionName = pcFuncName
                    });
                    if (resp.ProvisionedConcurrencyConfigs == null)
                        throw new Exception("configs list is nil");
                    if (resp.ProvisionedConcurrencyConfigs.Count == 0)
                        throw new Exception("expected at least 1 config");
                }));

                results.Add(await runner.RunTestAsync("lambda", "DeleteProvisionedConcurrencyConfig", async () =>
                {
                    await lambdaClient.DeleteProvisionedConcurrencyConfigAsync(new DeleteProvisionedConcurrencyConfigRequest
                    {
                        FunctionName = pcFuncName,
                        Qualifier = pcVersion
                    });
                }));

                results.Add(await runner.RunTestAsync("lambda", "GetProvisionedConcurrencyConfig_NonExistent", async () =>
                {
                    try
                    {
                        await lambdaClient.GetProvisionedConcurrencyConfigAsync(new GetProvisionedConcurrencyConfigRequest
                        {
                            FunctionName = pcFuncName,
                            Qualifier = pcVersion
                        });
                        throw new Exception("Expected error for deleted provisioned concurrency config");
                    }
                    catch (AmazonLambdaException) { }
                }));
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = pcFuncName }); });
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = pcRoleName }); });
            }
        }
        catch { }

        // === GROUP D: FUNCTION EVENT INVOKE CONFIG ===

        var eicFuncName = TestRunner.MakeUniqueName("EicFunc");
        var eicRoleName = TestRunner.MakeUniqueName("EicRole");
        var eicRole = $"arn:aws:iam::000000000000:role/{eicRoleName}";

        try
        {
            try
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = eicRoleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            }
            catch { }
            try
            {
                await lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                {
                    FunctionName = eicFuncName,
                    Runtime = Runtime.Nodejs20X,
                    Role = eicRole,
                    Handler = "index.handler",
                    Code = new FunctionCode { ZipFile = new MemoryStream(functionCode.ToArray()) }
                });

                results.Add(await runner.RunTestAsync("lambda", "PutFunctionEventInvokeConfig", async () =>
                {
                    var resp = await lambdaClient.PutFunctionEventInvokeConfigAsync(new PutFunctionEventInvokeConfigRequest
                    {
                        FunctionName = eicFuncName,
                        MaximumEventAgeInSeconds = 3600,
                        MaximumRetryAttempts = 2
                    });
                    if (resp.LastModified == null)
                        throw new Exception("LastModified is nil");
                }));

                results.Add(await runner.RunTestAsync("lambda", "GetFunctionEventInvokeConfig", async () =>
                {
                    var resp = await lambdaClient.GetFunctionEventInvokeConfigAsync(new GetFunctionEventInvokeConfigRequest
                    {
                        FunctionName = eicFuncName
                    });
                    if (resp.MaximumEventAgeInSeconds != 3600)
                        throw new Exception($"MaximumEventAgeInSeconds mismatch, got {resp.MaximumEventAgeInSeconds}");
                    if (resp.MaximumRetryAttempts != 2)
                        throw new Exception($"MaximumRetryAttempts mismatch, got {resp.MaximumRetryAttempts}");
                }));

                results.Add(await runner.RunTestAsync("lambda", "ListFunctionEventInvokeConfigs", async () =>
                {
                    var resp = await lambdaClient.ListFunctionEventInvokeConfigsAsync(new ListFunctionEventInvokeConfigsRequest
                    {
                        FunctionName = eicFuncName
                    });
                    if (resp.FunctionEventInvokeConfigs == null)
                        throw new Exception("configs list is nil");
                    if (resp.FunctionEventInvokeConfigs.Count == 0)
                        throw new Exception("expected at least 1 config");
                }));

                results.Add(await runner.RunTestAsync("lambda", "DeleteFunctionEventInvokeConfig", async () =>
                {
                    await lambdaClient.DeleteFunctionEventInvokeConfigAsync(new DeleteFunctionEventInvokeConfigRequest
                    {
                        FunctionName = eicFuncName
                    });
                }));
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = eicFuncName }); });
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = eicRoleName }); });
            }
        }
        catch { }

        // === GROUP E: FUNCTION URL CONFIG ===

        var furlFuncName = TestRunner.MakeUniqueName("FurlFunc");
        var furlRoleName = TestRunner.MakeUniqueName("FurlRole");
        var furlRole = $"arn:aws:iam::000000000000:role/{furlRoleName}";

        try
        {
            try
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = furlRoleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            }
            catch { }
            try
            {
                await lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                {
                    FunctionName = furlFuncName,
                    Runtime = Runtime.Nodejs20X,
                    Role = furlRole,
                    Handler = "index.handler",
                    Code = new FunctionCode { ZipFile = new MemoryStream(functionCode.ToArray()) }
                });

                results.Add(await runner.RunTestAsync("lambda", "CreateFunctionUrlConfig", async () =>
                {
                    var resp = await lambdaClient.CreateFunctionUrlConfigAsync(new CreateFunctionUrlConfigRequest
                    {
                        FunctionName = furlFuncName,
                        AuthType = FunctionUrlAuthType.NONE
                    });
                    if (string.IsNullOrEmpty(resp.FunctionUrl))
                        throw new Exception("FunctionUrl is nil or empty");
                    if (resp.AuthType != FunctionUrlAuthType.NONE)
                        throw new Exception($"AuthType mismatch, got {resp.AuthType}");
                }));

                results.Add(await runner.RunTestAsync("lambda", "GetFunctionUrlConfig", async () =>
                {
                    var resp = await lambdaClient.GetFunctionUrlConfigAsync(new GetFunctionUrlConfigRequest
                    {
                        FunctionName = furlFuncName
                    });
                    if (string.IsNullOrEmpty(resp.FunctionUrl))
                        throw new Exception("FunctionUrl is nil or empty");
                }));

                results.Add(await runner.RunTestAsync("lambda", "UpdateFunctionUrlConfig", async () =>
                {
                    var resp = await lambdaClient.UpdateFunctionUrlConfigAsync(new UpdateFunctionUrlConfigRequest
                    {
                        FunctionName = furlFuncName,
                        AuthType = FunctionUrlAuthType.AWS_IAM
                    });
                    if (resp.AuthType != FunctionUrlAuthType.AWS_IAM)
                        throw new Exception($"AuthType not updated, got {resp.AuthType}");
                }));

                results.Add(await runner.RunTestAsync("lambda", "ListFunctionUrlConfigs", async () =>
                {
                    var resp = await lambdaClient.ListFunctionUrlConfigsAsync(new ListFunctionUrlConfigsRequest
                    {
                        FunctionName = furlFuncName
                    });
                    if (resp.FunctionUrlConfigs == null)
                        throw new Exception("url configs list is nil");
                    if (resp.FunctionUrlConfigs.Count == 0)
                        throw new Exception("expected at least 1 url config");
                }));

                results.Add(await runner.RunTestAsync("lambda", "DeleteFunctionUrlConfig", async () =>
                {
                    await lambdaClient.DeleteFunctionUrlConfigAsync(new DeleteFunctionUrlConfigRequest
                    {
                        FunctionName = furlFuncName
                    });
                }));
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = furlFuncName }); });
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = furlRoleName }); });
            }
        }
        catch { }

        // === GROUP F: INVOKE ASYNC & RESPONSE STREAM ===

        var iaFuncName = TestRunner.MakeUniqueName("IaFunc");
        var iaRoleName = TestRunner.MakeUniqueName("IaRole");
        var iaRole = $"arn:aws:iam::000000000000:role/{iaRoleName}";
        var iaCode = System.Text.Encoding.UTF8.GetBytes("exports.handler = async () => { return { statusCode: 200 }; };");

        try
        {
            try
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = iaRoleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            }
            catch { }
            try
            {
                await lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                {
                    FunctionName = iaFuncName,
                    Runtime = Runtime.Nodejs20X,
                    Role = iaRole,
                    Handler = "index.handler",
                    Code = new FunctionCode { ZipFile = new MemoryStream(iaCode) }
                });

                results.Add(await runner.RunTestAsync("lambda", "InvokeAsync", async () =>
                {
                    var resp = await lambdaClient.InvokeAsync(new InvokeRequest
                    {
                        FunctionName = iaFuncName,
                        InvocationType = InvocationType.Event,
                        PayloadStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"test\": true}"))
                    });
                    if (resp.StatusCode != 202)
                        throw new Exception($"expected status 202, got {resp.StatusCode}");
                }));

                results.Add(await runner.RunTestAsync("lambda", "InvokeWithResponseStream", async () =>
                {
                    var resp = await lambdaClient.InvokeWithResponseStreamAsync(new InvokeWithResponseStreamRequest
                    {
                        FunctionName = iaFuncName
                    });
                    if (resp.StatusCode != 200)
                        throw new Exception($"expected status 200, got {resp.StatusCode}");
                    if (string.IsNullOrEmpty(resp.ResponseStreamContentType))
                        throw new Exception("ResponseStreamContentType is nil");
                }));
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = iaFuncName }); });
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = iaRoleName }); });
            }
        }
        catch { }

        // === GROUP G: ERROR CASES ===

        results.Add(await runner.RunTestAsync("lambda", "CreateFunction_InvalidRuntime", async () =>
        {
            var invRtFuncName = TestRunner.MakeUniqueName("InvRtFunc");
            var invRtRoleName = TestRunner.MakeUniqueName("InvRtRole");
            var invRtRole = $"arn:aws:iam::000000000000:role/{invRtRoleName}";
            try
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = invRtRoleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            }
            catch { }
            try
            {
                try
                {
                    await lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                    {
                        FunctionName = invRtFuncName,
                        Runtime = Runtime.FindValue("invalid_runtime_99"),
                        Role = invRtRole,
                        Handler = "index.handler",
                        Code = new FunctionCode { ZipFile = new MemoryStream(functionCode.ToArray()) }
                    });
                    throw new Exception("Expected error for invalid runtime");
                }
                catch (InvalidParameterValueException) { }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = invRtFuncName }); });
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = invRtRoleName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("lambda", "GetAlias_NonExistent", async () =>
        {
            try
            {
                await lambdaClient.GetAliasAsync(new GetAliasRequest
                {
                    FunctionName = functionName,
                    Name = "nonexistent-alias-xyz"
                });
                throw new Exception("Expected error for non-existent alias");
            }
            catch (AmazonLambdaException) { }
        }));

        results.Add(await runner.RunTestAsync("lambda", "GetLayerVersion_NonExistent", async () =>
        {
            try
            {
                await lambdaClient.GetLayerVersionAsync(new GetLayerVersionRequest
                {
                    LayerName = "nonexistent-layer-xyz",
                    VersionNumber = 999
                });
                throw new Exception("Expected error for non-existent layer version");
            }
            catch (AmazonLambdaException) { }
        }));

        results.Add(await runner.RunTestAsync("lambda", "GetFunctionUrlConfig_NoConfig", async () =>
        {
            var nofcFuncName = TestRunner.MakeUniqueName("NofcFunc");
            var nofcRoleName = TestRunner.MakeUniqueName("NofcRole");
            var nofcRole = $"arn:aws:iam::000000000000:role/{nofcRoleName}";
            try
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = nofcRoleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            }
            catch { }
            try
            {
                await lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                {
                    FunctionName = nofcFuncName,
                    Runtime = Runtime.Nodejs20X,
                    Role = nofcRole,
                    Handler = "index.handler",
                    Code = new FunctionCode { ZipFile = new MemoryStream(functionCode.ToArray()) }
                });
                try
                {
                    await lambdaClient.GetFunctionUrlConfigAsync(new GetFunctionUrlConfigRequest
                    {
                        FunctionName = nofcFuncName
                    });
                    throw new Exception("Expected error when no URL config set");
                }
                catch (AmazonLambdaException) { }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = nofcFuncName }); });
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = nofcRoleName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("lambda", "PutFunctionEventInvokeConfig_NonExistent", async () =>
        {
            try
            {
                await lambdaClient.PutFunctionEventInvokeConfigAsync(new PutFunctionEventInvokeConfigRequest
                {
                    FunctionName = "nonexistent-func-xyz-123",
                    MaximumEventAgeInSeconds = 3600
                });
                throw new Exception("Expected error for non-existent function");
            }
            catch (AmazonLambdaException) { }
        }));

        // === PAGINATION TESTS ===

        results.Add(await runner.RunTestAsync("lambda", "ListFunctions_Pagination", async () =>
        {
            var pgTs = DateTime.UtcNow.Ticks.ToString();
            var pgFuncs = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                var name = $"PagFunc-{pgTs}-{i}";
                await lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                {
                    FunctionName = name,
                    Runtime = Runtime.Nodejs20X,
                    Role = roleArn,
                    Handler = "index.handler",
                    Code = new FunctionCode { ZipFile = new MemoryStream(functionCode.ToArray()) }
                });
                pgFuncs.Add(name);
            }

            try
            {
                var allFuncs = new List<string>();
                string? marker = null;
                while (true)
                {
                    var req = new ListFunctionsRequest { MaxItems = 2 };
                    if (marker != null)
                        req.Marker = marker;
                    var resp = await lambdaClient.ListFunctionsAsync(req);
                    foreach (var f in resp.Functions)
                    {
                        if (f.FunctionName != null && f.FunctionName.StartsWith($"PagFunc-{pgTs}"))
                            allFuncs.Add(f.FunctionName);
                    }
                    if (!string.IsNullOrEmpty(resp.NextMarker))
                        marker = resp.NextMarker;
                    else
                        break;
                }

                if (allFuncs.Count != 5)
                    throw new Exception($"expected 5 paginated functions, got {allFuncs.Count}");
            }
            finally
            {
                foreach (var name in pgFuncs)
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = name }); });
                }
            }
        }));

        return results;
    }
}
