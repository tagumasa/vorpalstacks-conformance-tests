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
        string? resourceId = null;
        string? deploymentId = null;
        string? validatorId = null;
        string? authorizerId = null;
        string apiKeyValue = "";
        string? apiKeyId = null;
        string? usagePlanId = null;
        string? domainName = null;

        try
        {
            results.Add(await runner.RunTestAsync("apigateway", "CreateRestApi", async () =>
            {
                var resp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                {
                    Name = apiName,
                    Description = "Test API"
                });
                restApiId = resp.Id;
                if (string.IsNullOrEmpty(resp.Id))
                    throw new Exception("RestApi Id is null");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetRestApis", async () =>
            {
                var resp = await apigatewayClient.GetRestApisAsync(new GetRestApisRequest
                {
                    Limit = 500
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
                if (string.IsNullOrEmpty(resp.Name) || resp.Name != apiName)
                    throw new Exception($"name mismatch, got {resp.Name}");
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
                resourceId = resp.Id;
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetResource", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                var resp = await apigatewayClient.GetResourceAsync(new GetResourceRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId
                });
                if (string.IsNullOrEmpty(resp.Id) || resp.Id != resourceId)
                    throw new Exception($"resource ID mismatch, got {resp.Id}");
                if (string.IsNullOrEmpty(resp.Path) || resp.Path != "/test")
                    throw new Exception($"path mismatch, got {resp.Path}");
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
                if (resp.Items.Count < 2)
                    throw new Exception($"expected at least 2 resources, got {resp.Items.Count}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "UpdateResource", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                var resp = await apigatewayClient.UpdateResourceAsync(new UpdateResourceRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId,
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/pathPart",
                            Value = "items"
                        }
                    }
                });
                if (string.IsNullOrEmpty(resp.Path) || resp.Path != "/items")
                    throw new Exception($"path not updated, got {resp.Path}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "PutMethod", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                var resp = await apigatewayClient.PutMethodAsync(new PutMethodRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId,
                    HttpMethod = "GET",
                    AuthorizationType = "NONE",
                    ApiKeyRequired = false
                });
                if (resp.HttpMethod != "GET")
                    throw new Exception($"httpMethod mismatch, got {resp.HttpMethod}");
                if (resp.AuthorizationType != "NONE")
                    throw new Exception($"authorizationType mismatch, got {resp.AuthorizationType}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetMethod", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                var resp = await apigatewayClient.GetMethodAsync(new GetMethodRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId,
                    HttpMethod = "GET"
                });
                if (resp.HttpMethod != "GET")
                    throw new Exception($"httpMethod mismatch, got {resp.HttpMethod}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "UpdateMethod", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                var resp = await apigatewayClient.UpdateMethodAsync(new UpdateMethodRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId,
                    HttpMethod = "GET",
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/authorizationType",
                            Value = "AWS_IAM"
                        }
                    }
                });
                if (resp.AuthorizationType != "AWS_IAM")
                    throw new Exception($"authorizationType not updated, got {resp.AuthorizationType}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "PutIntegration", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                var resp = await apigatewayClient.PutIntegrationAsync(new PutIntegrationRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId,
                    HttpMethod = "GET",
                    Type = IntegrationType.MOCK,
                    RequestTemplates = new Dictionary<string, string>
                    {
                        { "application/json", "{\"statusCode\": 200}" }
                    }
                });
                if (resp.Type != IntegrationType.MOCK)
                    throw new Exception($"type mismatch, got {resp.Type}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetIntegration", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                var resp = await apigatewayClient.GetIntegrationAsync(new GetIntegrationRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId,
                    HttpMethod = "GET"
                });
                if (resp.Type != IntegrationType.MOCK)
                    throw new Exception($"type mismatch, got {resp.Type}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "UpdateIntegration", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                var resp = await apigatewayClient.UpdateIntegrationAsync(new UpdateIntegrationRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId,
                    HttpMethod = "GET",
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/timeoutInMillis",
                            Value = "5000"
                        }
                    }
                });
                if (resp.TimeoutInMillis != 5000)
                    throw new Exception($"timeoutInMillis not updated, got {resp.TimeoutInMillis}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "PutIntegrationResponse", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                var resp = await apigatewayClient.PutIntegrationResponseAsync(new PutIntegrationResponseRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId,
                    HttpMethod = "GET",
                    StatusCode = "200",
                    ResponseTemplates = new Dictionary<string, string>
                    {
                        { "application/json", "{\"message\": \"ok\"}" }
                    },
                    SelectionPattern = @"2\d{2}"
                });
                if (resp.StatusCode != "200")
                    throw new Exception($"statusCode mismatch, got {resp.StatusCode}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetIntegrationResponse", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                var resp = await apigatewayClient.GetIntegrationResponseAsync(new GetIntegrationResponseRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId,
                    HttpMethod = "GET",
                    StatusCode = "200"
                });
                if (resp.StatusCode != "200")
                    throw new Exception($"statusCode mismatch, got {resp.StatusCode}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "UpdateIntegrationResponse", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                var resp = await apigatewayClient.UpdateIntegrationResponseAsync(new UpdateIntegrationResponseRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId,
                    HttpMethod = "GET",
                    StatusCode = "200",
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/selectionPattern",
                            Value = "ok"
                        }
                    }
                });
                if (resp.SelectionPattern != "ok")
                    throw new Exception($"selectionPattern not updated, got {resp.SelectionPattern}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "PutMethodResponse", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                var resp = await apigatewayClient.PutMethodResponseAsync(new PutMethodResponseRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId,
                    HttpMethod = "GET",
                    StatusCode = "200",
                    ResponseModels = new Dictionary<string, string>
                    {
                        { "application/json", "Empty" }
                    }
                });
                if (resp.StatusCode != "200")
                    throw new Exception($"statusCode mismatch, got {resp.StatusCode}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetMethodResponse", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                var resp = await apigatewayClient.GetMethodResponseAsync(new GetMethodResponseRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId,
                    HttpMethod = "GET",
                    StatusCode = "200"
                });
                if (resp.StatusCode != "200")
                    throw new Exception($"statusCode mismatch, got {resp.StatusCode}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "DeleteMethodResponse", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                await apigatewayClient.DeleteMethodResponseAsync(new DeleteMethodResponseRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId,
                    HttpMethod = "GET",
                    StatusCode = "200"
                });
            }));

            results.Add(await runner.RunTestAsync("apigateway", "DeleteIntegrationResponse", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                await apigatewayClient.DeleteIntegrationResponseAsync(new DeleteIntegrationResponseRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId,
                    HttpMethod = "GET",
                    StatusCode = "200"
                });
            }));

            results.Add(await runner.RunTestAsync("apigateway", "DeleteIntegration", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                await apigatewayClient.DeleteIntegrationAsync(new DeleteIntegrationRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId,
                    HttpMethod = "GET"
                });
            }));

            results.Add(await runner.RunTestAsync("apigateway", "DeleteMethod", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                await apigatewayClient.DeleteMethodAsync(new DeleteMethodRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId,
                    HttpMethod = "GET"
                });
            }));

            results.Add(await runner.RunTestAsync("apigateway", "DeleteResource", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(resourceId))
                    throw new Exception("API ID or resource ID not available");
                await apigatewayClient.DeleteResourceAsync(new DeleteResourceRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resourceId
                });
            }));

            results.Add(await runner.RunTestAsync("apigateway", "CreateDeployment", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.CreateDeploymentAsync(new CreateDeploymentRequest
                {
                    RestApiId = restApiId,
                    Description = "test deployment"
                });
                if (string.IsNullOrEmpty(resp.Id))
                    throw new Exception("deployment ID is nil");
                deploymentId = resp.Id;
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetDeployment", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(deploymentId))
                    throw new Exception("API ID or deployment ID not available");
                var resp = await apigatewayClient.GetDeploymentAsync(new GetDeploymentRequest
                {
                    RestApiId = restApiId,
                    DeploymentId = deploymentId
                });
                if (string.IsNullOrEmpty(resp.Id) || resp.Id != deploymentId)
                    throw new Exception($"deployment ID mismatch, got {resp.Id}");
                if (resp.CreatedDate == null)
                    throw new Exception("createdDate is null");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "UpdateDeployment", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(deploymentId))
                    throw new Exception("API ID or deployment ID not available");
                var resp = await apigatewayClient.UpdateDeploymentAsync(new UpdateDeploymentRequest
                {
                    RestApiId = restApiId,
                    DeploymentId = deploymentId,
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/description",
                            Value = "updated deployment"
                        }
                    }
                });
                if (resp.Description != "updated deployment")
                    throw new Exception($"description not updated, got {resp.Description}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetDeployments", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.GetDeploymentsAsync(new GetDeploymentsRequest
                {
                    RestApiId = restApiId
                });
                if (resp.Items == null || resp.Items.Count == 0)
                    throw new Exception("expected at least 1 deployment");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "CreateStage", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(deploymentId))
                    throw new Exception("API ID or deployment ID not available");
                var resp = await apigatewayClient.CreateStageAsync(new CreateStageRequest
                {
                    RestApiId = restApiId,
                    StageName = "test",
                    DeploymentId = deploymentId,
                    Description = "test stage"
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
                if (resp.StageName != "test")
                    throw new Exception($"stage name mismatch, got {resp.StageName}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetStages", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.GetStagesAsync(new GetStagesRequest
                {
                    RestApiId = restApiId
                });
                if (resp.Item == null || resp.Item.Count == 0)
                    throw new Exception("expected at least 1 stage");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "UpdateStage", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.UpdateStageAsync(new UpdateStageRequest
                {
                    RestApiId = restApiId,
                    StageName = "test",
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/description",
                            Value = "updated stage"
                        }
                    }
                });
                if (resp.Description != "updated stage")
                    throw new Exception($"description not updated, got {resp.Description}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "DeleteStage", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                await apigatewayClient.DeleteStageAsync(new DeleteStageRequest
                {
                    RestApiId = restApiId,
                    StageName = "test"
                });
            }));

            results.Add(await runner.RunTestAsync("apigateway", "DeleteDeployment", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(deploymentId))
                    throw new Exception("API ID or deployment ID not available");
                await apigatewayClient.DeleteDeploymentAsync(new DeleteDeploymentRequest
                {
                    RestApiId = restApiId,
                    DeploymentId = deploymentId
                });
            }));

            results.Add(await runner.RunTestAsync("apigateway", "CreateRequestValidator", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.CreateRequestValidatorAsync(new CreateRequestValidatorRequest
                {
                    RestApiId = restApiId,
                    Name = "test-validator",
                    ValidateRequestBody = true,
                    ValidateRequestParameters = true
                });
                if (string.IsNullOrEmpty(resp.Id))
                    throw new Exception("validator ID is nil");
                if (resp.ValidateRequestBody != true)
                    throw new Exception("validateRequestBody should be true");
                if (resp.ValidateRequestParameters != true)
                    throw new Exception("validateRequestParameters should be true");
                validatorId = resp.Id;
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetRequestValidator", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(validatorId))
                    throw new Exception("API ID or validator ID not available");
                var resp = await apigatewayClient.GetRequestValidatorAsync(new GetRequestValidatorRequest
                {
                    RestApiId = restApiId,
                    RequestValidatorId = validatorId
                });
                if (resp.Name != "test-validator")
                    throw new Exception($"name mismatch, got {resp.Name}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "UpdateRequestValidator", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(validatorId))
                    throw new Exception("API ID or validator ID not available");
                var resp = await apigatewayClient.UpdateRequestValidatorAsync(new UpdateRequestValidatorRequest
                {
                    RestApiId = restApiId,
                    RequestValidatorId = validatorId,
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/name",
                            Value = "updated-validator"
                        }
                    }
                });
                if (resp.Name != "updated-validator")
                    throw new Exception($"name not updated, got {resp.Name}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetRequestValidators", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.GetRequestValidatorsAsync(new GetRequestValidatorsRequest
                {
                    RestApiId = restApiId,
                    Limit = 100
                });
                if (resp.Items == null || resp.Items.Count == 0)
                    throw new Exception("expected at least 1 validator");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "DeleteRequestValidator", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(validatorId))
                    throw new Exception("API ID or validator ID not available");
                await apigatewayClient.DeleteRequestValidatorAsync(new DeleteRequestValidatorRequest
                {
                    RestApiId = restApiId,
                    RequestValidatorId = validatorId
                });
            }));

            results.Add(await runner.RunTestAsync("apigateway", "CreateModel", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.CreateModelAsync(new CreateModelRequest
                {
                    RestApiId = restApiId,
                    Name = "UserModel",
                    ContentType = "application/json",
                    Description = "User model",
                    Schema = "{\"type\":\"object\"}"
                });
                if (string.IsNullOrEmpty(resp.Id))
                    throw new Exception("model ID is nil");
                if (resp.Name != "UserModel")
                    throw new Exception($"name mismatch, got {resp.Name}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetModel", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.GetModelAsync(new GetModelRequest
                {
                    RestApiId = restApiId,
                    ModelName = "UserModel"
                });
                if (resp.Name != "UserModel")
                    throw new Exception($"name mismatch, got {resp.Name}");
                if (resp.Schema != "{\"type\":\"object\"}")
                    throw new Exception($"schema mismatch, got {resp.Schema}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "UpdateModel", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.UpdateModelAsync(new UpdateModelRequest
                {
                    RestApiId = restApiId,
                    ModelName = "UserModel",
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/description",
                            Value = "updated model"
                        }
                    }
                });
                if (resp.Description != "updated model")
                    throw new Exception($"description not updated, got {resp.Description}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetModels", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.GetModelsAsync(new GetModelsRequest
                {
                    RestApiId = restApiId,
                    Limit = 100
                });
                if (resp.Items == null || resp.Items.Count == 0)
                    throw new Exception("expected at least 1 model");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "DeleteModel", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                await apigatewayClient.DeleteModelAsync(new DeleteModelRequest
                {
                    RestApiId = restApiId,
                    ModelName = "UserModel"
                });
            }));

            results.Add(await runner.RunTestAsync("apigateway", "CreateAuthorizer", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.CreateAuthorizerAsync(new CreateAuthorizerRequest
                {
                    RestApiId = restApiId,
                    Name = "test-authorizer",
                    Type = AuthorizerType.TOKEN,
                    AuthorizerUri = "https://example.com/auth",
                    IdentitySource = "method.request.header.Authorization",
                    AuthorizerResultTtlInSeconds = 300
                });
                if (string.IsNullOrEmpty(resp.Id))
                    throw new Exception("authorizer ID is nil");
                if (resp.Type != AuthorizerType.TOKEN)
                    throw new Exception($"type mismatch, got {resp.Type}");
                authorizerId = resp.Id;
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetAuthorizer", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(authorizerId))
                    throw new Exception("API ID or authorizer ID not available");
                var resp = await apigatewayClient.GetAuthorizerAsync(new GetAuthorizerRequest
                {
                    RestApiId = restApiId,
                    AuthorizerId = authorizerId
                });
                if (resp.Name != "test-authorizer")
                    throw new Exception($"name mismatch, got {resp.Name}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "UpdateAuthorizer", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(authorizerId))
                    throw new Exception("API ID or authorizer ID not available");
                var resp = await apigatewayClient.UpdateAuthorizerAsync(new UpdateAuthorizerRequest
                {
                    RestApiId = restApiId,
                    AuthorizerId = authorizerId,
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/name",
                            Value = "updated-authorizer"
                        }
                    }
                });
                if (resp.Name != "updated-authorizer")
                    throw new Exception($"name not updated, got {resp.Name}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetAuthorizers", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resp = await apigatewayClient.GetAuthorizersAsync(new GetAuthorizersRequest
                {
                    RestApiId = restApiId,
                    Limit = 100
                });
                if (resp.Items == null || resp.Items.Count == 0)
                    throw new Exception("expected at least 1 authorizer");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "TestInvokeAuthorizer", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(authorizerId))
                    throw new Exception("API ID or authorizer ID not available");
                var resp = await apigatewayClient.TestInvokeAuthorizerAsync(new TestInvokeAuthorizerRequest
                {
                    RestApiId = restApiId,
                    AuthorizerId = authorizerId,
                    Headers = new Dictionary<string, string>
                    {
                        { "Authorization", "Bearer test-token" }
                    }
                });
                if (resp.ClientStatus != 200)
                    throw new Exception($"expected clientStatus 200, got {resp.ClientStatus}");
                if (resp.Policy == null)
                    throw new Exception("policy is nil");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "DeleteAuthorizer", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(authorizerId))
                    throw new Exception("API ID or authorizer ID not available");
                await apigatewayClient.DeleteAuthorizerAsync(new DeleteAuthorizerRequest
                {
                    RestApiId = restApiId,
                    AuthorizerId = authorizerId
                });
            }));

            results.Add(await runner.RunTestAsync("apigateway", "TestInvokeMethod", async () =>
            {
                if (string.IsNullOrEmpty(restApiId))
                    throw new Exception("API ID not available");
                var resResp = await apigatewayClient.CreateResourceAsync(new CreateResourceRequest
                {
                    RestApiId = restApiId,
                    ParentId = restApiId,
                    PathPart = "mock"
                });
                await apigatewayClient.PutMethodAsync(new PutMethodRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resResp.Id,
                    HttpMethod = "POST",
                    AuthorizationType = "NONE"
                });
                await apigatewayClient.PutIntegrationAsync(new PutIntegrationRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resResp.Id,
                    HttpMethod = "POST",
                    Type = IntegrationType.MOCK,
                    RequestTemplates = new Dictionary<string, string>
                    {
                        { "application/json", "{\"statusCode\": 200}" }
                    }
                });
                var resp = await apigatewayClient.TestInvokeMethodAsync(new TestInvokeMethodRequest
                {
                    RestApiId = restApiId,
                    ResourceId = resResp.Id,
                    HttpMethod = "POST",
                    Body = "{\"test\": \"data\"}"
                });
                if (resp.Status != 200)
                    throw new Exception($"expected status 200, got {resp.Status}");
                if (resp.Log == null)
                    throw new Exception("log is nil");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "CreateApiKey", async () =>
            {
                var resp = await apigatewayClient.CreateApiKeyAsync(new CreateApiKeyRequest
                {
                    Name = "test-api-key",
                    Description = "Test API key",
                    Enabled = true,
                    Tags = new Dictionary<string, string>
                    {
                        { "env", "test" }
                    }
                });
                if (string.IsNullOrEmpty(resp.Id))
                    throw new Exception("api key ID is nil");
                if (string.IsNullOrEmpty(resp.Value))
                    throw new Exception("api key value is nil");
                if (resp.Enabled != true)
                    throw new Exception("expected enabled=true");
                apiKeyValue = resp.Value;
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetApiKeys", async () =>
            {
                var resp = await apigatewayClient.GetApiKeysAsync(new GetApiKeysRequest
                {
                    Limit = 100
                });
                if (resp.Items == null || resp.Items.Count == 0)
                    throw new Exception("expected at least 1 api key");
                foreach (var item in resp.Items)
                {
                    if (item.Name == "test-api-key")
                    {
                        apiKeyId = item.Id;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(apiKeyId))
                    throw new Exception("test-api-key not found");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetApiKey", async () =>
            {
                if (string.IsNullOrEmpty(apiKeyId))
                    throw new Exception("api key ID not available");
                var resp = await apigatewayClient.GetApiKeyAsync(new GetApiKeyRequest
                {
                    ApiKey = apiKeyId,
                    IncludeValue = true
                });
                if (resp.Name != "test-api-key")
                    throw new Exception($"name mismatch, got {resp.Name}");
                if (resp.Value != apiKeyValue)
                    throw new Exception($"value mismatch, got {resp.Value}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "UpdateApiKey", async () =>
            {
                if (string.IsNullOrEmpty(apiKeyId))
                    throw new Exception("api key ID not available");
                var resp = await apigatewayClient.UpdateApiKeyAsync(new UpdateApiKeyRequest
                {
                    ApiKey = apiKeyId,
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/name",
                            Value = "updated-api-key"
                        }
                    }
                });
                if (resp.Name != "updated-api-key")
                    throw new Exception($"name not updated, got {resp.Name}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "DeleteApiKey", async () =>
            {
                if (string.IsNullOrEmpty(apiKeyId))
                    throw new Exception("api key ID not available");
                await apigatewayClient.DeleteApiKeyAsync(new DeleteApiKeyRequest
                {
                    ApiKey = apiKeyId
                });
            }));

            results.Add(await runner.RunTestAsync("apigateway", "CreateUsagePlan", async () =>
            {
                var resp = await apigatewayClient.CreateUsagePlanAsync(new CreateUsagePlanRequest
                {
                    Name = "test-usage-plan",
                    Description = "Test usage plan",
                    Throttle = new ThrottleSettings
                    {
                        BurstLimit = 10,
                        RateLimit = 5.0
                    },
                    Quota = new QuotaSettings
                    {
                        Limit = 1000,
                        Period = QuotaPeriodType.MONTH
                    },
                    Tags = new Dictionary<string, string>
                    {
                        { "team", "backend" }
                    }
                });
                if (string.IsNullOrEmpty(resp.Id))
                    throw new Exception("usage plan ID is nil");
                if (resp.Throttle == null || resp.Throttle.BurstLimit != 10)
                    throw new Exception("throttle burstLimit mismatch");
                if (resp.Quota == null || resp.Quota.Period != QuotaPeriodType.MONTH)
                    throw new Exception("quota period mismatch");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetUsagePlans", async () =>
            {
                var resp = await apigatewayClient.GetUsagePlansAsync(new GetUsagePlansRequest
                {
                    Limit = 100
                });
                if (resp.Items == null || resp.Items.Count == 0)
                    throw new Exception("expected at least 1 usage plan");
                foreach (var item in resp.Items)
                {
                    if (item.Name == "test-usage-plan")
                    {
                        usagePlanId = item.Id;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(usagePlanId))
                    throw new Exception("test-usage-plan not found");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetUsagePlan", async () =>
            {
                if (string.IsNullOrEmpty(usagePlanId))
                    throw new Exception("usage plan ID not available");
                var resp = await apigatewayClient.GetUsagePlanAsync(new GetUsagePlanRequest
                {
                    UsagePlanId = usagePlanId
                });
                if (resp.Name != "test-usage-plan")
                    throw new Exception($"name mismatch, got {resp.Name}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "UpdateUsagePlan", async () =>
            {
                if (string.IsNullOrEmpty(usagePlanId))
                    throw new Exception("usage plan ID not available");
                var resp = await apigatewayClient.UpdateUsagePlanAsync(new UpdateUsagePlanRequest
                {
                    UsagePlanId = usagePlanId,
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/name",
                            Value = "updated-usage-plan"
                        }
                    }
                });
                if (resp.Name != "updated-usage-plan")
                    throw new Exception($"name not updated, got {resp.Name}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "DeleteUsagePlan", async () =>
            {
                if (string.IsNullOrEmpty(usagePlanId))
                    throw new Exception("usage plan ID not available");
                await apigatewayClient.DeleteUsagePlanAsync(new DeleteUsagePlanRequest
                {
                    UsagePlanId = usagePlanId
                });
            }));

            results.Add(await runner.RunTestAsync("apigateway", "CreateUsagePlanKey_Lifecycle", async () =>
            {
                var keyResp = await apigatewayClient.CreateApiKeyAsync(new CreateApiKeyRequest
                {
                    Name = "upk-test-key",
                    Enabled = true
                });
                string? keyId = keyResp.Id;
                try
                {
                    var upResp = await apigatewayClient.CreateUsagePlanAsync(new CreateUsagePlanRequest
                    {
                        Name = "upk-test-plan"
                    });
                    string upId = upResp.Id;
                    try
                    {
                        var upkResp = await apigatewayClient.CreateUsagePlanKeyAsync(new CreateUsagePlanKeyRequest
                        {
                            UsagePlanId = upId,
                            KeyId = keyId,
                            KeyType = "API_KEY"
                        });
                        if (string.IsNullOrEmpty(upkResp.Id))
                            throw new Exception("usage plan key ID is nil");

                        var getResp = await apigatewayClient.GetUsagePlanKeyAsync(new GetUsagePlanKeyRequest
                        {
                            UsagePlanId = upId,
                            KeyId = keyId
                        });
                        if (getResp.Type != "API_KEY")
                            throw new Exception($"type mismatch, got {getResp.Type}");

                        var keysResp = await apigatewayClient.GetUsagePlanKeysAsync(new GetUsagePlanKeysRequest
                        {
                            UsagePlanId = upId,
                            Limit = 100
                        });
                        if (keysResp.Items == null || keysResp.Items.Count == 0)
                            throw new Exception("expected at least 1 usage plan key");

                        await apigatewayClient.DeleteUsagePlanKeyAsync(new DeleteUsagePlanKeyRequest
                        {
                            UsagePlanId = upId,
                            KeyId = keyId
                        });
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteUsagePlanAsync(new DeleteUsagePlanRequest { UsagePlanId = upId }); });
                    }
                }
                finally
                {
                    if (!string.IsNullOrEmpty(keyId))
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteApiKeyAsync(new DeleteApiKeyRequest { ApiKey = keyId }); });
                    }
                }
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetUsage", async () =>
            {
                var upName = TestRunner.MakeUniqueName("usage-plan");
                var upResp = await apigatewayClient.CreateUsagePlanAsync(new CreateUsagePlanRequest
                {
                    Name = upName
                });
                string upId = upResp.Id;
                try
                {
                    var now = DateTime.UtcNow;
                    var startDate = now.AddMonths(-1).ToString("yyyy-MM-dd");
                    var endDate = now.ToString("yyyy-MM-dd");
                    var resp = await apigatewayClient.GetUsageAsync(new GetUsageRequest
                    {
                        UsagePlanId = upId,
                        StartDate = startDate,
                        EndDate = endDate
                    });
                    if (string.IsNullOrEmpty(resp.UsagePlanId) || resp.UsagePlanId != upId)
                        throw new Exception("usagePlanId mismatch");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteUsagePlanAsync(new DeleteUsagePlanRequest { UsagePlanId = upId }); });
                }
            }));

            results.Add(await runner.RunTestAsync("apigateway", "CreateDomainName", async () =>
            {
                var dn = TestRunner.MakeUniqueName("test") + ".example.com";
                var resp = await apigatewayClient.CreateDomainNameAsync(new CreateDomainNameRequest
                {
                    DomainName = dn,
                    CertificateName = "test-cert",
                    Tags = new Dictionary<string, string>
                    {
                        { "domain", "test" }
                    }
                });
                if (resp.Name != dn)
                    throw new Exception($"domain name mismatch, got {resp.Name}");
                if (string.IsNullOrEmpty(resp.DomainNameId))
                    throw new Exception("domain name ID is nil");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetDomainNames", async () =>
            {
                var resp = await apigatewayClient.GetDomainNamesAsync(new GetDomainNamesRequest
                {
                    Limit = 100
                });
                if (resp.Items == null || resp.Items.Count == 0)
                    throw new Exception("expected at least 1 domain name");
                domainName = resp.Items[0].Name;
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetDomainName", async () =>
            {
                if (string.IsNullOrEmpty(domainName))
                    throw new Exception("domain name not available");
                var resp = await apigatewayClient.GetDomainNameAsync(new GetDomainNameRequest
                {
                    DomainName = domainName
                });
                if (resp.Name != domainName)
                    throw new Exception($"domain name mismatch, got {resp.Name}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "UpdateDomainName", async () =>
            {
                if (string.IsNullOrEmpty(domainName))
                    throw new Exception("domain name not available");
                var resp = await apigatewayClient.UpdateDomainNameAsync(new UpdateDomainNameRequest
                {
                    DomainName = domainName,
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/certificateName",
                            Value = "updated-cert"
                        }
                    }
                });
                if (resp.CertificateName != "updated-cert")
                    throw new Exception($"certificateName not updated, got {resp.CertificateName}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "CreateBasePathMapping", async () =>
            {
                if (string.IsNullOrEmpty(restApiId) || string.IsNullOrEmpty(domainName))
                    throw new Exception("API ID or domain name not available");
                var resp = await apigatewayClient.CreateBasePathMappingAsync(new CreateBasePathMappingRequest
                {
                    DomainName = domainName,
                    RestApiId = restApiId,
                    BasePath = "v1",
                    Stage = "prod"
                });
                if (resp.BasePath != "v1")
                    throw new Exception($"basePath mismatch, got {resp.BasePath}");
                if (resp.RestApiId != restApiId)
                    throw new Exception($"restApiId mismatch, got {resp.RestApiId}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetBasePathMappings", async () =>
            {
                if (string.IsNullOrEmpty(domainName))
                    throw new Exception("domain name not available");
                var resp = await apigatewayClient.GetBasePathMappingsAsync(new GetBasePathMappingsRequest
                {
                    DomainName = domainName,
                    Limit = 100
                });
                if (resp.Items == null || resp.Items.Count == 0)
                    throw new Exception("expected at least 1 base path mapping");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "GetBasePathMapping", async () =>
            {
                if (string.IsNullOrEmpty(domainName))
                    throw new Exception("domain name not available");
                var resp = await apigatewayClient.GetBasePathMappingAsync(new GetBasePathMappingRequest
                {
                    DomainName = domainName,
                    BasePath = "v1"
                });
                if (resp.BasePath != "v1")
                    throw new Exception($"basePath mismatch, got {resp.BasePath}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "UpdateBasePathMapping", async () =>
            {
                if (string.IsNullOrEmpty(domainName))
                    throw new Exception("domain name not available");
                var resp = await apigatewayClient.UpdateBasePathMappingAsync(new UpdateBasePathMappingRequest
                {
                    DomainName = domainName,
                    BasePath = "v1",
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/stage",
                            Value = "staging"
                        }
                    }
                });
                if (resp.Stage != "staging")
                    throw new Exception($"stage not updated, got {resp.Stage}");
            }));

            results.Add(await runner.RunTestAsync("apigateway", "DeleteBasePathMapping", async () =>
            {
                if (string.IsNullOrEmpty(domainName))
                    throw new Exception("domain name not available");
                await apigatewayClient.DeleteBasePathMappingAsync(new DeleteBasePathMappingRequest
                {
                    DomainName = domainName,
                    BasePath = "v1"
                });
            }));

            results.Add(await runner.RunTestAsync("apigateway", "DeleteDomainName", async () =>
            {
                if (string.IsNullOrEmpty(domainName))
                    throw new Exception("domain name not available");
                await apigatewayClient.DeleteDomainNameAsync(new DeleteDomainNameRequest
                {
                    DomainName = domainName
                });
                domainName = null;
            }));

            results.Add(await runner.RunTestAsync("apigateway", "TagResource_UntagResource_ListTags", async () =>
            {
                var tagApiName = TestRunner.MakeUniqueName("TagAPI");
                string? tagApiId = null;
                try
                {
                    var createResp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                    {
                        Name = tagApiName
                    });
                    tagApiId = createResp.Id;

                    var arn = $"arn:aws:apigateway:{region}::/restapis/{tagApiId}";

                    await apigatewayClient.TagResourceAsync(new TagResourceRequest
                    {
                        ResourceArn = arn,
                        Tags = new Dictionary<string, string>
                        {
                            { "key1", "value1" },
                            { "key2", "value2" }
                        }
                    });

                    var tagResp = await apigatewayClient.GetTagsAsync(new GetTagsRequest
                    {
                        ResourceArn = arn
                    });
                    if (tagResp.Tags == null || tagResp.Tags["key1"] != "value1")
                        throw new Exception($"tags mismatch, got {tagResp.Tags}");

                    await apigatewayClient.UntagResourceAsync(new UntagResourceRequest
                    {
                        ResourceArn = arn,
                        TagKeys = new List<string> { "key2" }
                    });

                    var tagResp2 = await apigatewayClient.GetTagsAsync(new GetTagsRequest
                    {
                        ResourceArn = arn
                    });
                    if (tagResp2.Tags != null && tagResp2.Tags.ContainsKey("key2"))
                        throw new Exception("key2 should have been removed");
                    if (tagResp2.Tags == null || tagResp2.Tags["key1"] != "value1")
                        throw new Exception("key1 should still exist");
                }
                finally
                {
                    if (!string.IsNullOrEmpty(tagApiId))
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = tagApiId }); });
                    }
                }
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
            if (!string.IsNullOrEmpty(domainName))
            {
                try
                {
                    await apigatewayClient.DeleteDomainNameAsync(new DeleteDomainNameRequest { DomainName = domainName });
                }
                catch { }
            }
        }

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
                    await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = tmpApiId }); });
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
                    await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = uaApiId }); });
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
                    await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = crApiId }); });
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
                    await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = csApiId }); });
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
                    await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = gaApiId }); });
                }
            }
        }));

        results.Add(await runner.RunTestAsync("apigateway", "PutMethod_AuthorizationTypes", async () =>
        {
            var pmApiName = TestRunner.MakeUniqueName("PmAPI");
            string? pmApiId = null;
            try
            {
                var createResp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                {
                    Name = pmApiName
                });
                pmApiId = createResp.Id;

                var resResp = await apigatewayClient.CreateResourceAsync(new CreateResourceRequest
                {
                    RestApiId = pmApiId,
                    ParentId = pmApiId,
                    PathPart = "secure"
                });

                foreach (var authType in new[] { "NONE", "AWS_IAM", "CUSTOM" })
                {
                    await apigatewayClient.PutMethodAsync(new PutMethodRequest
                    {
                        RestApiId = pmApiId,
                        ResourceId = resResp.Id,
                        HttpMethod = "GET",
                        AuthorizationType = authType
                    });
                    var getResp = await apigatewayClient.GetMethodAsync(new GetMethodRequest
                    {
                        RestApiId = pmApiId,
                        ResourceId = resResp.Id,
                        HttpMethod = "GET"
                    });
                    if (getResp.AuthorizationType != authType)
                        throw new Exception($"auth type mismatch for {authType}, got {getResp.AuthorizationType}");
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(pmApiId))
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = pmApiId }); });
                }
            }
        }));

        results.Add(await runner.RunTestAsync("apigateway", "PutIntegration_Types", async () =>
        {
            var itApiName = TestRunner.MakeUniqueName("ItAPI");
            string? itApiId = null;
            try
            {
                var createResp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                {
                    Name = itApiName
                });
                itApiId = createResp.Id;

                var resResp = await apigatewayClient.CreateResourceAsync(new CreateResourceRequest
                {
                    RestApiId = itApiId,
                    ParentId = itApiId,
                    PathPart = "inttest"
                });

                await apigatewayClient.PutMethodAsync(new PutMethodRequest
                {
                    RestApiId = itApiId,
                    ResourceId = resResp.Id,
                    HttpMethod = "POST",
                    AuthorizationType = "NONE"
                });

                foreach (var intType in new[] { IntegrationType.MOCK, IntegrationType.HTTP, IntegrationType.HTTP_PROXY, IntegrationType.AWS_PROXY })
                {
                    await apigatewayClient.PutIntegrationAsync(new PutIntegrationRequest
                    {
                        RestApiId = itApiId,
                        ResourceId = resResp.Id,
                        HttpMethod = "POST",
                        Type = intType
                    });
                    var getResp = await apigatewayClient.GetIntegrationAsync(new GetIntegrationRequest
                    {
                        RestApiId = itApiId,
                        ResourceId = resResp.Id,
                        HttpMethod = "POST"
                    });
                    if (getResp.Type != intType)
                        throw new Exception($"type mismatch, expected {intType} got {getResp.Type}");
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(itApiId))
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = itApiId }); });
                }
            }
        }));

        results.Add(await runner.RunTestAsync("apigateway", "MethodWithIntegration_FullLifecycle", async () =>
        {
            var lcApiName = TestRunner.MakeUniqueName("LcAPI");
            string? lcApiId = null;
            try
            {
                var createResp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                {
                    Name = lcApiName
                });
                lcApiId = createResp.Id;

                var resResp = await apigatewayClient.CreateResourceAsync(new CreateResourceRequest
                {
                    RestApiId = lcApiId,
                    ParentId = lcApiId,
                    PathPart = "lifecycle"
                });

                await apigatewayClient.PutMethodAsync(new PutMethodRequest
                {
                    RestApiId = lcApiId,
                    ResourceId = resResp.Id,
                    HttpMethod = "GET",
                    AuthorizationType = "NONE",
                    OperationName = "GetLifecycle"
                });

                await apigatewayClient.PutIntegrationAsync(new PutIntegrationRequest
                {
                    RestApiId = lcApiId,
                    ResourceId = resResp.Id,
                    HttpMethod = "GET",
                    Type = IntegrationType.MOCK,
                    IntegrationHttpMethod = "POST",
                    Uri = "https://httpbin.org/post",
                    RequestParameters = new Dictionary<string, string>
                    {
                        { "integration.request.header.X-Custom", "'static'" }
                    },
                    RequestTemplates = new Dictionary<string, string>
                    {
                        { "application/json", "{\"statusCode\":200}" }
                    },
                    PassthroughBehavior = "WHEN_NO_MATCH",
                    TimeoutInMillis = 3000,
                    CacheNamespace = "lifecycle",
                    CacheKeyParameters = new List<string> { "header.Authorization" }
                });

                var getIntResp = await apigatewayClient.GetIntegrationAsync(new GetIntegrationRequest
                {
                    RestApiId = lcApiId,
                    ResourceId = resResp.Id,
                    HttpMethod = "GET"
                });
                if (getIntResp.Uri != "https://httpbin.org/post")
                    throw new Exception($"uri mismatch, got {getIntResp.Uri}");
                if (getIntResp.TimeoutInMillis != 3000)
                    throw new Exception($"timeoutInMillis mismatch, got {getIntResp.TimeoutInMillis}");

                await apigatewayClient.PutIntegrationResponseAsync(new PutIntegrationResponseRequest
                {
                    RestApiId = lcApiId,
                    ResourceId = resResp.Id,
                    HttpMethod = "GET",
                    StatusCode = "200",
                    ResponseParameters = new Dictionary<string, string>
                    {
                        { "method.response.header.Content-Type", "integration.response.header.Content-Type" }
                    },
                    ResponseTemplates = new Dictionary<string, string>
                    {
                        { "application/json", "$input.json('$')" }
                    },
                    SelectionPattern = @"2\d{2}"
                });

                await apigatewayClient.PutMethodResponseAsync(new PutMethodResponseRequest
                {
                    RestApiId = lcApiId,
                    ResourceId = resResp.Id,
                    HttpMethod = "GET",
                    StatusCode = "200",
                    ResponseParameters = new Dictionary<string, bool>
                    {
                        { "method.response.header.Content-Type", true }
                    },
                    ResponseModels = new Dictionary<string, string>
                    {
                        { "application/json", "Empty" }
                    }
                });

                await apigatewayClient.DeleteMethodResponseAsync(new DeleteMethodResponseRequest
                {
                    RestApiId = lcApiId,
                    ResourceId = resResp.Id,
                    HttpMethod = "GET",
                    StatusCode = "200"
                });

                await apigatewayClient.DeleteIntegrationResponseAsync(new DeleteIntegrationResponseRequest
                {
                    RestApiId = lcApiId,
                    ResourceId = resResp.Id,
                    HttpMethod = "GET",
                    StatusCode = "200"
                });

                await apigatewayClient.DeleteIntegrationAsync(new DeleteIntegrationRequest
                {
                    RestApiId = lcApiId,
                    ResourceId = resResp.Id,
                    HttpMethod = "GET"
                });

                await apigatewayClient.DeleteMethodAsync(new DeleteMethodRequest
                {
                    RestApiId = lcApiId,
                    ResourceId = resResp.Id,
                    HttpMethod = "GET"
                });

                await apigatewayClient.DeleteResourceAsync(new DeleteResourceRequest
                {
                    RestApiId = lcApiId,
                    ResourceId = resResp.Id
                });
            }
            finally
            {
                if (!string.IsNullOrEmpty(lcApiId))
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = lcApiId }); });
                }
            }
        }));

        results.Add(await runner.RunTestAsync("apigateway", "Deployment_Stage_FullLifecycle", async () =>
        {
            var dsApiName = TestRunner.MakeUniqueName("DsAPI");
            string? dsApiId = null;
            try
            {
                var createResp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                {
                    Name = dsApiName
                });
                dsApiId = createResp.Id;

                var depResp = await apigatewayClient.CreateDeploymentAsync(new CreateDeploymentRequest
                {
                    RestApiId = dsApiId,
                    Description = "v1 deployment"
                });

                var getDepResp = await apigatewayClient.GetDeploymentAsync(new GetDeploymentRequest
                {
                    RestApiId = dsApiId,
                    DeploymentId = depResp.Id
                });
                if (getDepResp.Description != "v1 deployment")
                    throw new Exception($"deployment description mismatch, got {getDepResp.Description}");

                await apigatewayClient.UpdateDeploymentAsync(new UpdateDeploymentRequest
                {
                    RestApiId = dsApiId,
                    DeploymentId = depResp.Id,
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/description",
                            Value = "v1 updated"
                        }
                    }
                });

                await apigatewayClient.CreateStageAsync(new CreateStageRequest
                {
                    RestApiId = dsApiId,
                    StageName = "production",
                    DeploymentId = depResp.Id,
                    Description = "production stage"
                });

                await apigatewayClient.UpdateStageAsync(new UpdateStageRequest
                {
                    RestApiId = dsApiId,
                    StageName = "production",
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/description",
                            Value = "production updated"
                        },
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/variables/env",
                            Value = "prod"
                        }
                    }
                });

                var stageResp = await apigatewayClient.GetStageAsync(new GetStageRequest
                {
                    RestApiId = dsApiId,
                    StageName = "production"
                });
                if (stageResp.Variables == null || stageResp.Variables["env"] != "prod")
                    throw new Exception($"stage variables not set, got {stageResp.Variables}");

                await apigatewayClient.DeleteStageAsync(new DeleteStageRequest
                {
                    RestApiId = dsApiId,
                    StageName = "production"
                });

                await apigatewayClient.DeleteDeploymentAsync(new DeleteDeploymentRequest
                {
                    RestApiId = dsApiId,
                    DeploymentId = depResp.Id
                });
            }
            finally
            {
                if (!string.IsNullOrEmpty(dsApiId))
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = dsApiId }); });
                }
            }
        }));

        results.Add(await runner.RunTestAsync("apigateway", "RequestValidator_FullLifecycle", async () =>
        {
            var rvApiName = TestRunner.MakeUniqueName("RvAPI");
            string? rvApiId = null;
            try
            {
                var createResp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                {
                    Name = rvApiName
                });
                rvApiId = createResp.Id;

                await apigatewayClient.CreateRequestValidatorAsync(new CreateRequestValidatorRequest
                {
                    RestApiId = rvApiId,
                    Name = "body-only",
                    ValidateRequestBody = true,
                    ValidateRequestParameters = false
                });

                await apigatewayClient.CreateRequestValidatorAsync(new CreateRequestValidatorRequest
                {
                    RestApiId = rvApiId,
                    Name = "params-only",
                    ValidateRequestBody = false,
                    ValidateRequestParameters = true
                });

                var rvListResp = await apigatewayClient.GetRequestValidatorsAsync(new GetRequestValidatorsRequest
                {
                    RestApiId = rvApiId,
                    Limit = 100
                });
                if (rvListResp.Items == null || rvListResp.Items.Count < 2)
                    throw new Exception($"expected at least 2 validators, got {rvListResp.Items?.Count ?? 0}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(rvApiId))
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = rvApiId }); });
                }
            }
        }));

        results.Add(await runner.RunTestAsync("apigateway", "Model_FullLifecycle", async () =>
        {
            var mlApiName = TestRunner.MakeUniqueName("MlAPI");
            string? mlApiId = null;
            try
            {
                var createResp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                {
                    Name = mlApiName
                });
                mlApiId = createResp.Id;

                await apigatewayClient.CreateModelAsync(new CreateModelRequest
                {
                    RestApiId = mlApiId,
                    Name = "ErrorModel",
                    ContentType = "application/json",
                    Description = "Error response",
                    Schema = "{\"type\":\"object\",\"properties\":{\"message\":{\"type\":\"string\"}}}"
                });

                var getResp = await apigatewayClient.GetModelAsync(new GetModelRequest
                {
                    RestApiId = mlApiId,
                    ModelName = "ErrorModel"
                });
                if (getResp.ContentType != "application/json")
                    throw new Exception($"contentType mismatch, got {getResp.ContentType}");

                await apigatewayClient.UpdateModelAsync(new UpdateModelRequest
                {
                    RestApiId = mlApiId,
                    ModelName = "ErrorModel",
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/schema",
                            Value = "{\"type\":\"object\"}"
                        }
                    }
                });

                var modelsResp = await apigatewayClient.GetModelsAsync(new GetModelsRequest
                {
                    RestApiId = mlApiId,
                    Limit = 100
                });
                if (modelsResp.Items == null || modelsResp.Items.Count == 0)
                    throw new Exception("expected at least 1 model");

                await apigatewayClient.DeleteModelAsync(new DeleteModelRequest
                {
                    RestApiId = mlApiId,
                    ModelName = "ErrorModel"
                });
            }
            finally
            {
                if (!string.IsNullOrEmpty(mlApiId))
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = mlApiId }); });
                }
            }
        }));

        results.Add(await runner.RunTestAsync("apigateway", "Authorizer_FullLifecycle", async () =>
        {
            var auApiName = TestRunner.MakeUniqueName("AuAPI");
            string? auApiId = null;
            try
            {
                var createResp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                {
                    Name = auApiName
                });
                auApiId = createResp.Id;

                var authResp = await apigatewayClient.CreateAuthorizerAsync(new CreateAuthorizerRequest
                {
                    RestApiId = auApiId,
                    Name = "lambda-auth",
                    Type = AuthorizerType.TOKEN,
                    AuthorizerUri = "https://example.com/lambda",
                    IdentitySource = "method.request.header.Auth",
                    AuthorizerCredentials = "arn:aws:iam::123456789012:role/lambda-auth-role",
                    IdentityValidationExpression = "Bearer .*",
                    AuthorizerResultTtlInSeconds = 600
                });
                if (authResp.AuthorizerResultTtlInSeconds != 600)
                    throw new Exception($"ttl mismatch, got {authResp.AuthorizerResultTtlInSeconds}");

                await apigatewayClient.UpdateAuthorizerAsync(new UpdateAuthorizerRequest
                {
                    RestApiId = auApiId,
                    AuthorizerId = authResp.Id,
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/authorizerResultTtlInSeconds",
                            Value = "1200"
                        }
                    }
                });

                var getAuthResp = await apigatewayClient.GetAuthorizerAsync(new GetAuthorizerRequest
                {
                    RestApiId = auApiId,
                    AuthorizerId = authResp.Id
                });
                if (getAuthResp.AuthorizerResultTtlInSeconds != 1200)
                    throw new Exception($"ttl not updated, got {getAuthResp.AuthorizerResultTtlInSeconds}");

                var authListResp = await apigatewayClient.GetAuthorizersAsync(new GetAuthorizersRequest
                {
                    RestApiId = auApiId,
                    Limit = 100
                });
                if (authListResp.Items == null || authListResp.Items.Count == 0)
                    throw new Exception("expected at least 1 authorizer");

                await apigatewayClient.DeleteAuthorizerAsync(new DeleteAuthorizerRequest
                {
                    RestApiId = auApiId,
                    AuthorizerId = authResp.Id
                });
            }
            finally
            {
                if (!string.IsNullOrEmpty(auApiId))
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = auApiId }); });
                }
            }
        }));

        results.Add(await runner.RunTestAsync("apigateway", "DomainName_BasePathMapping_FullLifecycle", async () =>
        {
            var dbApiName = TestRunner.MakeUniqueName("DbAPI");
            string? dbApiId = null;
            var dbDomain = TestRunner.MakeUniqueName("lc") + ".example.com";
            try
            {
                var createResp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                {
                    Name = dbApiName
                });
                dbApiId = createResp.Id;

                var dnResp = await apigatewayClient.CreateDomainNameAsync(new CreateDomainNameRequest
                {
                    DomainName = dbDomain,
                    CertificateName = "lc-cert"
                });

                await apigatewayClient.CreateBasePathMappingAsync(new CreateBasePathMappingRequest
                {
                    DomainName = dbDomain,
                    RestApiId = dbApiId,
                    BasePath = "(none)",
                    Stage = "prod"
                });

                await apigatewayClient.GetBasePathMappingsAsync(new GetBasePathMappingsRequest
                {
                    DomainName = dbDomain
                });

                await apigatewayClient.DeleteBasePathMappingAsync(new DeleteBasePathMappingRequest
                {
                    DomainName = dbDomain,
                    BasePath = "(none)"
                });

                await apigatewayClient.DeleteDomainNameAsync(new DeleteDomainNameRequest
                {
                    DomainName = dbDomain
                });
            }
            finally
            {
                if (!string.IsNullOrEmpty(dbApiId))
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = dbApiId }); });
                }
            }
        }));

        results.Add(await runner.RunTestAsync("apigateway", "UsagePlan_WithApiStages", async () =>
        {
            var usApiName = TestRunner.MakeUniqueName("UsAPI");
            string? usApiId = null;
            try
            {
                var createResp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                {
                    Name = usApiName
                });
                usApiId = createResp.Id;

                var depResp = await apigatewayClient.CreateDeploymentAsync(new CreateDeploymentRequest
                {
                    RestApiId = usApiId
                });

                await apigatewayClient.CreateStageAsync(new CreateStageRequest
                {
                    RestApiId = usApiId,
                    StageName = "api-stage",
                    DeploymentId = depResp.Id
                });

                var upName = TestRunner.MakeUniqueName("us-plan");
                var upResp = await apigatewayClient.CreateUsagePlanAsync(new CreateUsagePlanRequest
                {
                    Name = upName,
                    ApiStages = new List<ApiStage>
                    {
                        new ApiStage
                        {
                            ApiId = usApiId,
                            Stage = "api-stage"
                        }
                    }
                });

                var getResp = await apigatewayClient.GetUsagePlanAsync(new GetUsagePlanRequest
                {
                    UsagePlanId = upResp.Id
                });
                if (getResp.ApiStages == null || getResp.ApiStages.Count == 0)
                    throw new Exception("expected apiStages to be set");

                await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteUsagePlanAsync(new DeleteUsagePlanRequest { UsagePlanId = upResp.Id }); });
            }
            finally
            {
                if (!string.IsNullOrEmpty(usApiId))
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = usApiId }); });
                }
            }
        }));

        results.Add(await runner.RunTestAsync("apigateway", "GetRestApis_Pagination", async () =>
        {
            var pgTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var pgApiIds = new List<string>();
            try
            {
                for (int i = 0; i < 5; i++)
                {
                    var name = $"PagAPI-{pgTs}-{i}";
                    var resp = await apigatewayClient.CreateRestApiAsync(new CreateRestApiRequest
                    {
                        Name = name,
                        Description = "pagination test"
                    });
                    pgApiIds.Add(resp.Id);
                }

                var allApis = new List<string>();
                string? position = null;
                while (true)
                {
                    var req = new GetRestApisRequest
                    {
                        Limit = 2
                    };
                    if (!string.IsNullOrEmpty(position))
                        req.Position = position;
                    var resp = await apigatewayClient.GetRestApisAsync(req);
                    foreach (var item in resp.Items)
                    {
                        if (item.Name != null && item.Name.StartsWith($"PagAPI-{pgTs}-"))
                            allApis.Add(item.Name);
                    }
                    if (!string.IsNullOrEmpty(resp.Position))
                        position = resp.Position;
                    else
                        break;
                }

                if (allApis.Count != 5)
                    throw new Exception($"expected 5 paginated rest apis, got {allApis.Count}");
            }
            finally
            {
                foreach (var id in pgApiIds)
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await apigatewayClient.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = id }); });
                }
            }
        }));

        return results;
    }
}
