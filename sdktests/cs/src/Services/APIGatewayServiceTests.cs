using Amazon;
using Amazon.APIGateway;
using Amazon.APIGateway.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class APIGatewayServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonAPIGatewayClient apigatewayClient,
        string region)
    {
        var results = new List<TestResult>();
        var apiName = TestRunner.MakeUniqueName("CSRestApi");
        string? restApiId = null;
        string? deploymentId = null;

        try
        {
            results.Add(await runner.RunTestAsync("apigateway", "CreateRestApi", async () =>
            {
                var resp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                {
                    Name = apiName,
                    Description = "Test REST API"
                });
                restApiId = resp.Id;
                if (string.IsNullOrEmpty(resp.Id))
                    throw new Exception("RestApi Id is null");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetRestApis", async () =>
            {
                var resp = await apigatewayClient.GetRestApisAsync(new GetRestApisRequest
                {
                    Limit = 100
                });
                if (resp.Items == null)
                    throw new Exception("Items is null");
                bool found = false;
                foreach (var item in resp.Items)
                {
                    if (item.Name == apiName)
                    {
                        restApiId = item.Id;
                        found = true;
                        break;
                    }
                }
                if (!found)
                    throw new Exception("API not found in GetRestApis");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetRestApi", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.GetRestApiAsync(new GetRestApiRequest
                {
                    RestApiId = restApiId
                });
                if (string.IsNullOrEmpty(resp.Name))
                    throw new Exception("API name is null");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "UpdateRestApi", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.UpdateRestApiAsync(new UpdateRestApiRequest
                {
                    RestApiId = restApiId,
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/description",
                            Value = "Updated API"
                        }
                    }
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "CreateResource", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.CreateResourceAsync(new CreateResourceRequest
                {
                    RestApiId = restApiId,
                    ParentId = restApiId,
                    PathPart = "test"
                });
                if (string.IsNullOrEmpty(resp.Id))
                    throw new Exception("resource ID is null");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetResources", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.GetResourcesAsync(new GetResourcesRequest
                {
                    RestApiId = restApiId
                });
                if (resp.Items == null)
                    throw new Exception("items list is null");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "CreateDeployment", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.CreateDeploymentAsync(new CreateDeploymentRequest
                {
                    RestApiId = restApiId
                });
                if (!string.IsNullOrEmpty(resp.Id))
                    deploymentId = resp.Id;
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetDeployments", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.GetDeploymentsAsync(new GetDeploymentsRequest
                {
                    RestApiId = restApiId
                });
                if (resp.Items == null)
                    throw new Exception("items list is null");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "CreateStage", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                if (string.IsNullOrEmpty(deploymentId))
                    throw new Exception("Deployment ID not available");
                var resp = await apigatewayClient.CreateStageAsync(new CreateStageRequest
                {
                    RestApiId = restApiId,
                    StageName = "test",
                    DeploymentId = deploymentId
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetStage", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.GetStageAsync(new GetStageRequest
                {
                    RestApiId = restApiId,
                    StageName = "test"
                });
                if (string.IsNullOrEmpty(resp.StageName))
                    throw new Exception("stage name is null");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetStages", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.GetStagesAsync(new GetStagesRequest
                {
                    RestApiId = restApiId
                });
                if (resp.Item == null)
                    throw new Exception("stage item is null");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "DeleteRestApi", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest
                {
                    RestApiId = restApiId
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));
        }
        finally
        {
            if (!string.IsNullOrEmpty(restApiId))
            {
                try
                {
                    await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = restApiId });
                }
                catch { }
            }
        }

        // === ERROR / EDGE CASE TESTS ===

        results.Add(await runner.RunTestAsync("apigateway", "GetRestApi_NonExistent", async () =>
        {
            try
            {
                await apigatewayClient.GetRestApiAsync(new GetRestApiRequest
                {
                    RestApiId = "nonexistent-api-id-xyz"
                });
                throw new Exception("Expected NotFoundException but got none");
            }
            catch (NotFoundException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("apigateway", "DeleteRestApi_NonExistent", async () =>
        {
            try
            {
                await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest
                {
                    RestApiId = "nonexistent-api-id-xyz"
                });
                throw new Exception("Expected NotFoundException but got none");
            }
            catch (NotFoundException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("apigateway", "GetStage_NonExistent", async () =>
        {
            var tmpApiName = TestRunner.MakeUniqueName("TmpAPI");
            string? tmpApiId = null;
            try
            {
                var createResp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                {
                    Name = tmpApiName
                });
                tmpApiId = createResp.Id;

                try
                {
                    await apigatewayClient.GetStageAsync(new GetStageRequest
                    {
                        RestApiId = tmpApiId,
                        StageName = "nonexistent_stage"
                    });
                    throw new Exception("Expected NotFoundException but got none");
                }
                catch (NotFoundException)
                {
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(tmpApiId))
                {
                    try
                    {
                        await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = tmpApiId });
                    }
                    catch { }
                }
            }
        }));

        results.Add(await runner.RunTestAsync("apigateway", "UpdateRestApi_VerifyUpdate", async () =>
        {
            var uaApiName = TestRunner.MakeUniqueName("UaAPI");
            string? uaApiId = null;
            try
            {
                var createResp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                {
                    Name = uaApiName,
                    Description = "original desc"
                });
                uaApiId = createResp.Id;

                var newDesc = "updated description v2";
                await apigatewayClient.UpdateRestApiAsync(new UpdateRestApiRequest
                {
                    RestApiId = uaApiId,
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/description",
                            Value = newDesc
                        }
                    }
                });

                var resp = await apigatewayClient.GetRestApiAsync(new GetRestApiRequest
                {
                    RestApiId = uaApiId
                });
                if (resp.Description != newDesc)
                    throw new Exception($"description not updated, got {resp.Description}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(uaApiId))
                {
                    try
                    {
                        await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = uaApiId });
                    }
                    catch { }
                }
            }
        }));

        results.Add(await runner.RunTestAsync("apigateway", "CreateResource_NestedPath", async () =>
        {
            var crApiName = TestRunner.MakeUniqueName("CrAPI");
            string? crApiId = null;
            try
            {
                var createResp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                {
                    Name = crApiName
                });
                crApiId = createResp.Id;

                var usersResp = await apigatewayClient.CreateResourceAsync(new CreateResourceRequest
                {
                    RestApiId = crApiId,
                    ParentId = crApiId,
                    PathPart = "users"
                });

                var userIdResp = await apigatewayClient.CreateResourceAsync(new CreateResourceRequest
                {
                    RestApiId = crApiId,
                    ParentId = usersResp.Id,
                    PathPart = "{userId}"
                });
                if (userIdResp.Path != "/users/{userId}")
                    throw new Exception($"nested path mismatch, got {userIdResp.Path}");

                var resResp = await apigatewayClient.GetResourcesAsync(new GetResourcesRequest
                {
                    RestApiId = crApiId
                });
                if (resResp.Items == null || resResp.Items.Count < 3)
                    throw new Exception($"expected at least 3 resources, got {resResp.Items?.Count ?? 0}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(crApiId))
                {
                    try
                    {
                        await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = crApiId });
                    }
                    catch { }
                }
            }
        }));

        results.Add(await runner.RunTestAsync("apigateway", "CreateStage_VerifyConfig", async () =>
        {
            var csApiName = TestRunner.MakeUniqueName("CsAPI");
            string? csApiId = null;
            try
            {
                var createResp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                {
                    Name = csApiName
                });
                csApiId = createResp.Id;

                var depResp = await apigatewayClient.CreateDeploymentAsync(new CreateDeploymentRequest
                {
                    RestApiId = csApiId
                });

                var stageDesc = "test stage description";
                await apigatewayClient.CreateStageAsync(new CreateStageRequest
                {
                    RestApiId = csApiId,
                    StageName = "v1",
                    DeploymentId = depResp.Id,
                    Description = stageDesc
                });

                var resp = await apigatewayClient.GetStageAsync(new GetStageRequest
                {
                    RestApiId = csApiId,
                    StageName = "v1"
                });
                if (resp.Description != stageDesc)
                    throw new Exception($"stage description mismatch, got {resp.Description}");
                if (resp.DeploymentId != depResp.Id)
                    throw new Exception($"deployment ID mismatch, got {resp.DeploymentId}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(csApiId))
                {
                    try
                    {
                        await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = csApiId });
                    }
                    catch { }
                }
            }
        }));

        results.Add(await runner.RunTestAsync("apigateway", "GetRestApis_ContainsCreated", async () =>
        {
            var gaApiName = TestRunner.MakeUniqueName("GaAPI");
            var gaDesc = "searchable description";
            string? gaApiId = null;
            try
            {
                var createResp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                {
                    Name = gaApiName,
                    Description = gaDesc
                });
                gaApiId = createResp.Id;

                var resp = await apigatewayClient.GetRestApisAsync(new GetRestApisRequest
                {
                    Limit = 500
                });
                bool found = false;
                foreach (var item in resp.Items)
                {
                    if (item.Name == gaApiName)
                    {
                        found = true;
                        if (item.Description != gaDesc)
                            throw new Exception($"description mismatch in list, got {item.Description}");
                        break;
                    }
                }
                if (!found)
                    throw new Exception($"created API {gaApiName} not found in GetRestApis");
            }
            finally
            {
                if (!string.IsNullOrEmpty(gaApiId))
                {
                    try
                    {
                        await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = gaApiId });
                    }
                    catch { }
                }
            }
        }));

        return results;
    }
}
