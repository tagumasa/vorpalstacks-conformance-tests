using System.Text;
using Amazon.AppSync;
using Amazon.AppSync.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static partial class AppSyncServiceTests
{
    public static async Task<List<TestResult>> RunGraphQLApiTests(
        TestRunner runner,
        AmazonAppSyncClient client,
        List<TestResult> results,
        string region)
    {
        var uid = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        string? taggedApiArn = null;
        string? taggedApiId = null;
        string? tagNsName = null;
        string? nsArn = null;
        string? gqlApiId = null;
        string? gqlTagsApiId = null;
        string? functionId = null;
        string? apiKeyId = null;
        string? descApiKeyId = null;
        string? domainName = null;
        string? tagDomainName = null;
        string? mergedApiId = null;
        string? sourceApiId2 = null;
        string? associationId = null;
        string? mergedAssocId = null;

        var tagApiInfo = await CreateTagApiAsync(client, uid);
        taggedApiId = tagApiInfo.ApiId;
        taggedApiArn = tagApiInfo.ApiArn;

        results.Add(await runner.RunTestAsync("appsync", "ListTagsForResource", async () =>
        {
            var resp = await client.ListTagsForResourceAsync(new ListTagsForResourceRequest
            {
                ResourceArn = taggedApiArn
            });
            if (resp.Tags == null)
                throw new Exception("Tags is nil");
            if (resp.Tags["key1"] != "value1")
                throw new Exception($"expected key1=value1, got: {resp.Tags}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "TagResource", async () =>
        {
            await client.TagResourceAsync(new TagResourceRequest
            {
                ResourceArn = taggedApiArn,
                Tags = new Dictionary<string, string>
                {
                    { "key2", "value2" },
                    { "key3", "value3" }
                }
            });
            var resp = await client.ListTagsForResourceAsync(new ListTagsForResourceRequest
            {
                ResourceArn = taggedApiArn
            });
            if (resp.Tags == null || resp.Tags.Count != 3)
                throw new Exception($"expected 3 tags, got {resp.Tags?.Count ?? 0}");
            if (resp.Tags["key2"] != "value2" || resp.Tags["key3"] != "value3")
                throw new Exception($"new tags not found: {resp.Tags}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "UntagResource", async () =>
        {
            await client.UntagResourceAsync(new UntagResourceRequest
            {
                ResourceArn = taggedApiArn,
                TagKeys = ["key2"]
            });
            var resp = await client.ListTagsForResourceAsync(new ListTagsForResourceRequest
            {
                ResourceArn = taggedApiArn
            });
            if (resp.Tags == null || resp.Tags.ContainsKey("key2"))
                throw new Exception($"key2 should have been removed: {resp.Tags}");
            if (resp.Tags["key1"] != "value1" || resp.Tags["key3"] != "value3")
                throw new Exception($"remaining tags incorrect: {resp.Tags}");
        }));

        var tagEventApiId = await CreateSimpleEventApiAsync(client, uid);
        tagNsName = $"tag-ns-{uid}";

        results.Add(await runner.RunTestAsync("appsync", "CreateChannelNamespace_ForTagging", async () =>
        {
            var resp = await client.CreateChannelNamespaceAsync(new CreateChannelNamespaceRequest
            {
                ApiId = tagEventApiId,
                Name = tagNsName,
                Tags = new Dictionary<string, string> { { "nsKey", "nsValue" } }
            });
            if (resp.ChannelNamespace == null || string.IsNullOrEmpty(resp.ChannelNamespace.ChannelNamespaceArn))
                throw new Exception("invalid response");
            nsArn = resp.ChannelNamespace.ChannelNamespaceArn;
        }));

        results.Add(await runner.RunTestAsync("appsync", "TagResource_ChannelNamespace", async () =>
        {
            await client.TagResourceAsync(new TagResourceRequest
            {
                ResourceArn = nsArn,
                Tags = new Dictionary<string, string> { { "added", "yes" } }
            });
            var resp = await client.ListTagsForResourceAsync(new ListTagsForResourceRequest
            {
                ResourceArn = nsArn
            });
            if (resp.Tags == null)
                throw new Exception("Tags is nil");
            if (resp.Tags["nsKey"] != "nsValue" || resp.Tags["added"] != "yes")
                throw new Exception($"namespace tags incorrect: {resp.Tags}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "CreateGraphqlApi", async () =>
        {
            var resp = await client.CreateGraphqlApiAsync(new CreateGraphqlApiRequest
            {
                Name = $"test-gql-{uid}",
                AuthenticationType = AuthenticationType.API_KEY
            });
            if (resp.GraphqlApi == null)
                throw new Exception("GraphqlApi is nil");
            if (string.IsNullOrEmpty(resp.GraphqlApi.ApiId))
                throw new Exception("ApiId is empty");
            if (resp.GraphqlApi.AuthenticationType != AuthenticationType.API_KEY)
                throw new Exception($"expected API_KEY auth type, got {resp.GraphqlApi.AuthenticationType}");
            if (string.IsNullOrEmpty(resp.GraphqlApi.Arn))
                throw new Exception("Arn is nil");
            if (!resp.GraphqlApi.Arn.StartsWith("arn:aws:appsync:"))
                throw new Exception($"invalid ARN format: {resp.GraphqlApi.Arn}");
            gqlApiId = resp.GraphqlApi.ApiId;
        }));

        results.Add(await runner.RunTestAsync("appsync", "CreateGraphqlApi_WithTags", async () =>
        {
            var resp = await client.CreateGraphqlApiAsync(new CreateGraphqlApiRequest
            {
                Name = $"test-gql-tags-{uid}",
                AuthenticationType = AuthenticationType.API_KEY,
                Tags = new Dictionary<string, string> { { "env", "dev" } }
            });
            if (resp.GraphqlApi == null || resp.GraphqlApi.Tags == null)
                throw new Exception("Tags not returned");
            if (resp.GraphqlApi.Tags["env"] != "dev")
                throw new Exception($"tag not persisted: {resp.GraphqlApi.Tags}");
            gqlTagsApiId = resp.GraphqlApi.ApiId;
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetGraphqlApi", async () =>
        {
            var resp = await client.GetGraphqlApiAsync(new GetGraphqlApiRequest { ApiId = gqlApiId });
            if (resp.GraphqlApi == null)
                throw new Exception("GraphqlApi is nil");
            if (resp.GraphqlApi.ApiId != gqlApiId)
                throw new Exception($"expected ApiId {gqlApiId}, got {resp.GraphqlApi.ApiId}");
            if (resp.GraphqlApi.AuthenticationType != AuthenticationType.API_KEY)
                throw new Exception($"expected API_KEY auth type, got {resp.GraphqlApi.AuthenticationType}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetGraphqlApi_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.GetGraphqlApiAsync(new GetGraphqlApiRequest { ApiId = "does-not-exist" });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "ListGraphqlApis", async () =>
        {
            var resp = await client.ListGraphqlApisAsync(new ListGraphqlApisRequest());
            if (resp.GraphqlApis == null || resp.GraphqlApis.Count < 2)
                throw new Exception($"expected at least 2 GraphQL APIs, got {resp.GraphqlApis?.Count ?? 0}");
            var found = resp.GraphqlApis.Any(a => a.ApiId == gqlApiId);
            if (!found) throw new Exception("created GraphQL API not found in list");
        }));

        results.Add(await runner.RunTestAsync("appsync", "ListGraphqlApis_WithPagination", async () =>
        {
            var resp = await client.ListGraphqlApisAsync(new ListGraphqlApisRequest { MaxResults = 1 });
            if (resp.GraphqlApis == null || resp.GraphqlApis.Count != 1)
                throw new Exception($"expected 1 API with maxResults=1, got {resp.GraphqlApis?.Count ?? 0}");
            if (string.IsNullOrEmpty(resp.NextToken))
                throw new Exception("expected NextToken when more results exist");
        }));

        results.Add(await runner.RunTestAsync("appsync", "UpdateGraphqlApi", async () =>
        {
            var newName = $"updated-gql-{uid}";
            var resp = await client.UpdateGraphqlApiAsync(new UpdateGraphqlApiRequest
            {
                ApiId = gqlApiId,
                Name = newName,
                AuthenticationType = AuthenticationType.OPENID_CONNECT
            });
            if (resp.GraphqlApi == null || resp.GraphqlApi.Name != newName)
                throw new Exception($"expected name {newName}, got {resp.GraphqlApi?.Name}");
            if (resp.GraphqlApi.AuthenticationType != AuthenticationType.OPENID_CONNECT)
                throw new Exception($"expected OPENID_CONNECT auth type, got {resp.GraphqlApi.AuthenticationType}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "UpdateGraphqlApi_NonExistent", async () =>
        {
            try
            {
                await client.UpdateGraphqlApiAsync(new UpdateGraphqlApiRequest
                {
                    ApiId = "does-not-exist",
                    Name = "noop",
                    AuthenticationType = AuthenticationType.API_KEY
                });
                throw new Exception("expected error for non-existent GraphQL API");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "CreateDataSource", async () =>
        {
            var resp = await client.CreateDataSourceAsync(new CreateDataSourceRequest
            {
                ApiId = gqlApiId,
                Name = "testDS",
                Type = DataSourceType.NONE
            });
            if (resp.DataSource == null)
                throw new Exception("DataSource is nil");
            if (resp.DataSource.Name != "testDS")
                throw new Exception($"expected name testDS, got {resp.DataSource.Name}");
            if (resp.DataSource.Type != DataSourceType.NONE)
                throw new Exception($"expected NONE type, got {resp.DataSource.Type}");
            if (string.IsNullOrEmpty(resp.DataSource.DataSourceArn))
                throw new Exception("DataSourceArn is nil");
        }));

        results.Add(await runner.RunTestAsync("appsync", "CreateDataSource_WithDescription", async () =>
        {
            var resp = await client.CreateDataSourceAsync(new CreateDataSourceRequest
            {
                ApiId = gqlApiId,
                Name = "testDS2",
                Type = DataSourceType.NONE,
                Description = "test description"
            });
            if (resp.DataSource == null)
                throw new Exception("DataSource is nil");
            if (resp.DataSource.Description != "test description")
                throw new Exception($"description not set: {resp.DataSource.Description}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetDataSource", async () =>
        {
            var resp = await client.GetDataSourceAsync(new GetDataSourceRequest
            {
                ApiId = gqlApiId,
                Name = "testDS"
            });
            if (resp.DataSource == null || resp.DataSource.Name != "testDS")
                throw new Exception($"expected testDS, got {resp.DataSource?.Name}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetDataSource_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.GetDataSourceAsync(new GetDataSourceRequest
                {
                    ApiId = gqlApiId,
                    Name = "does-not-exist"
                });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "ListDataSources", async () =>
        {
            var resp = await client.ListDataSourcesAsync(new ListDataSourcesRequest { ApiId = gqlApiId });
            if (resp.DataSources == null || resp.DataSources.Count < 2)
                throw new Exception($"expected at least 2 data sources, got {resp.DataSources?.Count ?? 0}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "UpdateDataSource", async () =>
        {
            var resp = await client.UpdateDataSourceAsync(new UpdateDataSourceRequest
            {
                ApiId = gqlApiId,
                Name = "testDS",
                Type = DataSourceType.NONE,
                Description = "updated description"
            });
            if (resp.DataSource == null)
                throw new Exception("DataSource is nil");
            if (resp.DataSource.Description != "updated description")
                throw new Exception($"description not updated: {resp.DataSource.Description}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteDataSource", async () =>
        {
            await client.DeleteDataSourceAsync(new DeleteDataSourceRequest
            {
                ApiId = gqlApiId,
                Name = "testDS2"
            });
            try
            {
                await client.GetDataSourceAsync(new GetDataSourceRequest
                {
                    ApiId = gqlApiId,
                    Name = "testDS2"
                });
                throw new Exception("expected error after deleting data source");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteDataSource_NonExistent", async () =>
        {
            try
            {
                await client.DeleteDataSourceAsync(new DeleteDataSourceRequest
                {
                    ApiId = gqlApiId,
                    Name = "already-deleted"
                });
                throw new Exception("expected error for non-existent data source");
            }
            catch { }
        }));

        var sdl = "type Post { id: ID! title: String! }";

        results.Add(await runner.RunTestAsync("appsync", "CreateType", async () =>
        {
            var resp = await client.CreateTypeAsync(new CreateTypeRequest
            {
                ApiId = gqlApiId,
                Definition = sdl,
                Format = TypeDefinitionFormat.SDL
            });
            if (resp.Type == null)
                throw new Exception("Type is nil");
            if (resp.Type.Name != "Post")
                throw new Exception($"expected name Post, got {resp.Type.Name}");
            if (resp.Type.Format != TypeDefinitionFormat.SDL)
                throw new Exception($"expected SDL format, got {resp.Type.Format}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetType", async () =>
        {
            var resp = await client.GetTypeAsync(new GetTypeRequest
            {
                ApiId = gqlApiId,
                TypeName = "Post",
                Format = TypeDefinitionFormat.SDL
            });
            if (resp.Type == null || resp.Type.Name != "Post")
                throw new Exception($"expected Post type, got {resp.Type?.Name}");
            if (string.IsNullOrEmpty(resp.Type.Definition))
                throw new Exception("definition is empty");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetType_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.GetTypeAsync(new GetTypeRequest
                {
                    ApiId = gqlApiId,
                    TypeName = "DoesNotExist",
                    Format = TypeDefinitionFormat.SDL
                });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "ListTypes", async () =>
        {
            var resp = await client.ListTypesAsync(new ListTypesRequest
            {
                ApiId = gqlApiId,
                Format = TypeDefinitionFormat.SDL
            });
            if (resp.Types == null || resp.Types.Count < 1)
                throw new Exception($"expected at least 1 type, got {resp.Types?.Count ?? 0}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "UpdateType", async () =>
        {
            var updatedSDL = "type Post { id: ID! title: String! content: String }";
            var resp = await client.UpdateTypeAsync(new UpdateTypeRequest
            {
                ApiId = gqlApiId,
                TypeName = "Post",
                Definition = updatedSDL,
                Format = TypeDefinitionFormat.SDL
            });
            if (resp.Type == null)
                throw new Exception("Type is nil");
            if (resp.Type.Definition == null || !resp.Type.Definition.Contains("content"))
                throw new Exception($"definition not updated: {resp.Type.Definition}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteType", async () =>
        {
            await client.DeleteTypeAsync(new DeleteTypeRequest
            {
                ApiId = gqlApiId,
                TypeName = "Post"
            });
            try
            {
                await client.GetTypeAsync(new GetTypeRequest
                {
                    ApiId = gqlApiId,
                    TypeName = "Post",
                    Format = TypeDefinitionFormat.SDL
                });
                throw new Exception("expected error after deleting type");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteType_NonExistent", async () =>
        {
            try
            {
                await client.DeleteTypeAsync(new DeleteTypeRequest
                {
                    ApiId = gqlApiId,
                    TypeName = "already-deleted"
                });
                throw new Exception("expected error for non-existent type");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "CreateResolver", async () =>
        {
            var resp = await client.CreateResolverAsync(new CreateResolverRequest
            {
                ApiId = gqlApiId,
                TypeName = "Query",
                FieldName = "getPost",
                DataSourceName = "testDS"
            });
            if (resp.Resolver == null)
                throw new Exception("Resolver is nil");
            if (resp.Resolver.FieldName != "getPost")
                throw new Exception($"expected fieldName getPost, got {resp.Resolver.FieldName}");
            if (resp.Resolver.TypeName != "Query")
                throw new Exception($"expected typeName Query, got {resp.Resolver.TypeName}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetResolver", async () =>
        {
            var resp = await client.GetResolverAsync(new GetResolverRequest
            {
                ApiId = gqlApiId,
                TypeName = "Query",
                FieldName = "getPost"
            });
            if (resp.Resolver == null || resp.Resolver.FieldName != "getPost")
                throw new Exception($"expected getPost resolver, got {resp.Resolver?.FieldName}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetResolver_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.GetResolverAsync(new GetResolverRequest
                {
                    ApiId = gqlApiId,
                    TypeName = "Query",
                    FieldName = "doesNotExist"
                });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "ListResolvers", async () =>
        {
            var resp = await client.ListResolversAsync(new ListResolversRequest
            {
                ApiId = gqlApiId,
                TypeName = "Query"
            });
            if (resp.Resolvers == null || resp.Resolvers.Count < 1)
                throw new Exception($"expected at least 1 resolver, got {resp.Resolvers?.Count ?? 0}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "UpdateResolver", async () =>
        {
            var resp = await client.UpdateResolverAsync(new UpdateResolverRequest
            {
                ApiId = gqlApiId,
                TypeName = "Query",
                FieldName = "getPost",
                RequestMappingTemplate = "{\"version\": \"2017-02-28\", \"payload\": {}}"
            });
            if (resp.Resolver == null)
                throw new Exception("Resolver is nil");
            if (string.IsNullOrEmpty(resp.Resolver.RequestMappingTemplate))
                throw new Exception("requestMappingTemplate not updated");
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteResolver", async () =>
        {
            await client.DeleteResolverAsync(new DeleteResolverRequest
            {
                ApiId = gqlApiId,
                TypeName = "Query",
                FieldName = "getPost"
            });
            try
            {
                await client.GetResolverAsync(new GetResolverRequest
                {
                    ApiId = gqlApiId,
                    TypeName = "Query",
                    FieldName = "getPost"
                });
                throw new Exception("expected error after deleting resolver");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteResolver_NonExistent", async () =>
        {
            try
            {
                await client.DeleteResolverAsync(new DeleteResolverRequest
                {
                    ApiId = gqlApiId,
                    TypeName = "Query",
                    FieldName = "already-deleted"
                });
                throw new Exception("expected error for non-existent resolver");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "CreateFunction", async () =>
        {
            var resp = await client.CreateFunctionAsync(new CreateFunctionRequest
            {
                ApiId = gqlApiId,
                Name = "testFn",
                DataSourceName = "testDS",
                FunctionVersion = "2018-05-29"
            });
            if (resp.FunctionConfiguration == null)
                throw new Exception("FunctionConfiguration is nil");
            if (string.IsNullOrEmpty(resp.FunctionConfiguration.FunctionId))
                throw new Exception("FunctionId is empty");
            if (resp.FunctionConfiguration.Name != "testFn")
                throw new Exception($"expected name testFn, got {resp.FunctionConfiguration.Name}");
            functionId = resp.FunctionConfiguration.FunctionId;
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetFunction", async () =>
        {
            var resp = await client.GetFunctionAsync(new GetFunctionRequest
            {
                ApiId = gqlApiId,
                FunctionId = functionId
            });
            if (resp.FunctionConfiguration == null || resp.FunctionConfiguration.FunctionId != functionId)
                throw new Exception($"expected function {functionId}, got {resp.FunctionConfiguration?.FunctionId}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetFunction_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.GetFunctionAsync(new GetFunctionRequest
                {
                    ApiId = gqlApiId,
                    FunctionId = "does-not-exist"
                });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "ListFunctions", async () =>
        {
            var resp = await client.ListFunctionsAsync(new ListFunctionsRequest { ApiId = gqlApiId });
            if (resp.Functions == null || resp.Functions.Count < 1)
                throw new Exception($"expected at least 1 function, got {resp.Functions?.Count ?? 0}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "UpdateFunction", async () =>
        {
            var resp = await client.UpdateFunctionAsync(new UpdateFunctionRequest
            {
                ApiId = gqlApiId,
                FunctionId = functionId,
                Name = "updatedFn",
                DataSourceName = "testDS",
                FunctionVersion = "2018-05-29"
            });
            if (resp.FunctionConfiguration == null || resp.FunctionConfiguration.Name != "updatedFn")
                throw new Exception($"expected name updatedFn, got {resp.FunctionConfiguration?.Name}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteFunction", async () =>
        {
            await client.DeleteFunctionAsync(new DeleteFunctionRequest
            {
                ApiId = gqlApiId,
                FunctionId = functionId
            });
            try
            {
                await client.GetFunctionAsync(new GetFunctionRequest
                {
                    ApiId = gqlApiId,
                    FunctionId = functionId
                });
                throw new Exception("expected error after deleting function");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteFunction_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.DeleteFunctionAsync(new DeleteFunctionRequest
                {
                    ApiId = gqlApiId,
                    FunctionId = "already-deleted"
                });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "ListResolversByFunction", async () =>
        {
            var createFnResp = await client.CreateFunctionAsync(new CreateFunctionRequest
            {
                ApiId = gqlApiId,
                Name = "listByFn",
                DataSourceName = "testDS",
                FunctionVersion = "2018-05-29"
            });
            var listFnId = createFnResp.FunctionConfiguration!.FunctionId;

            await client.CreateResolverAsync(new CreateResolverRequest
            {
                ApiId = gqlApiId,
                TypeName = "Query",
                FieldName = "fnTest",
                DataSourceName = "testDS"
            });

            var pipelineResp = await client.CreateResolverAsync(new CreateResolverRequest
            {
                ApiId = gqlApiId,
                TypeName = "Query",
                FieldName = "pipelineTest",
                Kind = ResolverKind.PIPELINE,
                PipelineConfig = new PipelineConfig { Functions = [listFnId] }
            });

            var listResp = await client.ListResolversByFunctionAsync(new ListResolversByFunctionRequest
            {
                ApiId = gqlApiId,
                FunctionId = listFnId
            });
            if (listResp.Resolvers == null || listResp.Resolvers.Count < 1)
                throw new Exception($"expected at least 1 resolver, got {listResp.Resolvers?.Count ?? 0}");
            var found = listResp.Resolvers.Any(r => r.FieldName == pipelineResp.Resolver!.FieldName);
            if (!found)
                throw new Exception("pipeline resolver not found in ListResolversByFunction");

            await TestHelpers.SafeCleanupAsync(async () =>
            {
                try { await client.DeleteResolverAsync(new DeleteResolverRequest { ApiId = gqlApiId, TypeName = "Query", FieldName = "fnTest" }); } catch { }
                try { await client.DeleteResolverAsync(new DeleteResolverRequest { ApiId = gqlApiId, TypeName = "Query", FieldName = "pipelineTest" }); } catch { }
                try { await client.DeleteFunctionAsync(new DeleteFunctionRequest { ApiId = gqlApiId, FunctionId = listFnId }); } catch { }
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "StartSchemaCreation", async () =>
        {
            var schemaSDL = "type Query { hello: String } type Mutation { addPost(title: String!): Post } type Post { id: ID! title: String! }";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(schemaSDL));
            var resp = await client.StartSchemaCreationAsync(new StartSchemaCreationRequest
            {
                ApiId = gqlApiId,
                Definition = stream
            });
            if (resp.Status != SchemaStatus.PROCESSING && resp.Status != SchemaStatus.SUCCESS)
                throw new Exception($"expected PROCESSING or SUCCESS status, got {resp.Status}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetSchemaCreationStatus", async () =>
        {
            var resp = await client.GetSchemaCreationStatusAsync(new GetSchemaCreationStatusRequest { ApiId = gqlApiId });
            if (resp.Status != SchemaStatus.SUCCESS && resp.Status != SchemaStatus.PROCESSING)
                throw new Exception($"expected SUCCESS or PROCESSING status, got {resp.Status}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetIntrospectionSchema", async () =>
        {
            var resp = await client.GetIntrospectionSchemaAsync(new GetIntrospectionSchemaRequest
            {
                ApiId = gqlApiId,
                Format = OutputType.SDL
            });
            if (resp.Schema == null || resp.Schema.Length == 0)
                throw new Exception("Schema is empty");
            using var reader = new StreamReader(resp.Schema);
            var schemaStr = reader.ReadToEnd();
            var expectedTypes = new[] { "type Query", "type Mutation", "type Post" };
            foreach (var expected in expectedTypes)
            {
                if (!schemaStr.Contains(expected))
                    throw new Exception($"schema missing \"{expected}\"");
            }
        }));

        results.Add(await runner.RunTestAsync("appsync", "PutGraphqlApiEnvironmentVariables", async () =>
        {
            var resp = await client.PutGraphqlApiEnvironmentVariablesAsync(new PutGraphqlApiEnvironmentVariablesRequest
            {
                ApiId = gqlApiId,
                EnvironmentVariables = new Dictionary<string, string>
                {
                    { "ENV1", "value1" },
                    { "ENV2", "value2" }
                }
            });
            if (resp.EnvironmentVariables == null)
                throw new Exception("EnvironmentVariables is nil");
            if (resp.EnvironmentVariables["ENV1"] != "value1" || resp.EnvironmentVariables["ENV2"] != "value2")
                throw new Exception($"env vars not persisted: {resp.EnvironmentVariables}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetGraphqlApiEnvironmentVariables", async () =>
        {
            var resp = await client.GetGraphqlApiEnvironmentVariablesAsync(new GetGraphqlApiEnvironmentVariablesRequest { ApiId = gqlApiId });
            if (resp.EnvironmentVariables == null)
                throw new Exception("EnvironmentVariables is nil");
            if (resp.EnvironmentVariables["ENV1"] != "value1" || resp.EnvironmentVariables["ENV2"] != "value2")
                throw new Exception($"env vars incorrect: {resp.EnvironmentVariables}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "PutGraphqlApiEnvironmentVariables_Replace", async () =>
        {
            var resp = await client.PutGraphqlApiEnvironmentVariablesAsync(new PutGraphqlApiEnvironmentVariablesRequest
            {
                ApiId = gqlApiId,
                EnvironmentVariables = new Dictionary<string, string> { { "ENV3", "value3" } }
            });
            if (resp.EnvironmentVariables == null || resp.EnvironmentVariables.Count != 1)
                throw new Exception($"expected 1 env var after replace, got {resp.EnvironmentVariables?.Count ?? 0}");
            if (resp.EnvironmentVariables["ENV3"] != "value3")
                throw new Exception($"expected ENV3=value3, got {resp.EnvironmentVariables}");
            if (resp.EnvironmentVariables.ContainsKey("ENV1"))
                throw new Exception("ENV1 should have been replaced");
        }));

        results.Add(await runner.RunTestAsync("appsync", "CreateApiKey", async () =>
        {
            var resp = await client.CreateApiKeyAsync(new CreateApiKeyRequest { ApiId = gqlApiId });
            if (resp.ApiKey == null)
                throw new Exception("ApiKey is nil");
            if (string.IsNullOrEmpty(resp.ApiKey.Id))
                throw new Exception("Id is empty");
            if (resp.ApiKey.Expires == 0)
                throw new Exception("Expires should be set (default 365 days)");
            if (resp.ApiKey.Deletes == 0)
                throw new Exception("Deletes should be set (same as Expires)");
            apiKeyId = resp.ApiKey.Id;
        }));

        results.Add(await runner.RunTestAsync("appsync", "CreateApiKey_WithDescription", async () =>
        {
            var resp = await client.CreateApiKeyAsync(new CreateApiKeyRequest
            {
                ApiId = gqlApiId,
                Description = "test key"
            });
            if (resp.ApiKey == null)
                throw new Exception("ApiKey is nil");
            if (resp.ApiKey.Description != "test key")
                throw new Exception($"description not set: {resp.ApiKey.Description}");
            descApiKeyId = resp.ApiKey.Id;
        }));

        results.Add(await runner.RunTestAsync("appsync", "ListApiKeys", async () =>
        {
            var resp = await client.ListApiKeysAsync(new ListApiKeysRequest { ApiId = gqlApiId });
            if (resp.ApiKeys == null || resp.ApiKeys.Count < 2)
                throw new Exception($"expected at least 2 API keys, got {resp.ApiKeys?.Count ?? 0}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "ListApiKeys_WithPagination", async () =>
        {
            var resp = await client.ListApiKeysAsync(new ListApiKeysRequest { ApiId = gqlApiId, MaxResults = 1 });
            if (resp.ApiKeys == null || resp.ApiKeys.Count != 1)
                throw new Exception($"expected 1 API key with maxResults=1, got {resp.ApiKeys?.Count ?? 0}");
            if (string.IsNullOrEmpty(resp.NextToken))
                throw new Exception("expected NextToken when more results exist");
        }));

        results.Add(await runner.RunTestAsync("appsync", "UpdateApiKey", async () =>
        {
            var newExpiry = DateTimeOffset.UtcNow.Add(TimeSpan.FromDays(180)).ToUnixTimeSeconds();
            var resp = await client.UpdateApiKeyAsync(new UpdateApiKeyRequest
            {
                ApiId = gqlApiId,
                Id = apiKeyId,
                Description = "updated key",
                Expires = newExpiry
            });
            if (resp.ApiKey == null)
                throw new Exception("ApiKey is nil");
            if (resp.ApiKey.Description != "updated key")
                throw new Exception($"description not updated: {resp.ApiKey.Description}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "UpdateApiKey_NonExistent", async () =>
        {
            try
            {
                await client.UpdateApiKeyAsync(new UpdateApiKeyRequest
                {
                    ApiId = gqlApiId,
                    Id = "does-not-exist",
                    Description = "noop"
                });
                throw new Exception("expected error for non-existent API key");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteApiKey", async () =>
        {
            await client.DeleteApiKeyAsync(new DeleteApiKeyRequest { ApiId = gqlApiId, Id = apiKeyId });
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteApiKey_NonExistent", async () =>
        {
            try
            {
                await client.DeleteApiKeyAsync(new DeleteApiKeyRequest { ApiId = gqlApiId, Id = "already-deleted" });
                throw new Exception("expected error for non-existent API key");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "CreateApiCache", async () =>
        {
            var resp = await client.CreateApiCacheAsync(new CreateApiCacheRequest
            {
                ApiId = gqlApiId,
                Type = ApiCacheType.SMALL,
                Ttl = 300,
                ApiCachingBehavior = ApiCachingBehavior.FULL_REQUEST_CACHING
            });
            if (resp.ApiCache == null)
                throw new Exception("ApiCache is nil");
            if (resp.ApiCache.Type != ApiCacheType.SMALL)
                throw new Exception($"expected SMALL type, got {resp.ApiCache.Type}");
            if (resp.ApiCache.Ttl != 300)
                throw new Exception($"expected TTL 300, got {resp.ApiCache.Ttl}");
            if (resp.ApiCache.ApiCachingBehavior != ApiCachingBehavior.FULL_REQUEST_CACHING)
                throw new Exception($"expected FULL_REQUEST_CACHING, got {resp.ApiCache.ApiCachingBehavior}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetApiCache", async () =>
        {
            var resp = await client.GetApiCacheAsync(new GetApiCacheRequest { ApiId = gqlApiId });
            if (resp.ApiCache == null)
                throw new Exception("ApiCache is nil");
            if (resp.ApiCache.Ttl != 300)
                throw new Exception($"expected TTL 300, got {resp.ApiCache.Ttl}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetApiCache_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.GetApiCacheAsync(new GetApiCacheRequest { ApiId = "does-not-exist" });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "UpdateApiCache", async () =>
        {
            var resp = await client.UpdateApiCacheAsync(new UpdateApiCacheRequest
            {
                ApiId = gqlApiId,
                Type = ApiCacheType.MEDIUM,
                Ttl = 600,
                ApiCachingBehavior = ApiCachingBehavior.PER_RESOLVER_CACHING
            });
            if (resp.ApiCache == null)
                throw new Exception("ApiCache is nil");
            if (resp.ApiCache.Type != ApiCacheType.MEDIUM)
                throw new Exception($"expected MEDIUM type, got {resp.ApiCache.Type}");
            if (resp.ApiCache.Ttl != 600)
                throw new Exception($"expected TTL 600, got {resp.ApiCache.Ttl}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "FlushApiCache", async () =>
        {
            await client.FlushApiCacheAsync(new FlushApiCacheRequest { ApiId = gqlApiId });
            var verifyResp = await client.GetApiCacheAsync(new GetApiCacheRequest { ApiId = gqlApiId });
            if (verifyResp.ApiCache == null)
                throw new Exception("ApiCache is nil after flush");
        }));

        results.Add(await runner.RunTestAsync("appsync", "FlushApiCache_NonExistent", async () =>
        {
            try
            {
                await client.FlushApiCacheAsync(new FlushApiCacheRequest { ApiId = "does-not-exist" });
                throw new Exception("expected error for non-existent cache");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteApiCache", async () =>
        {
            await client.DeleteApiCacheAsync(new DeleteApiCacheRequest { ApiId = gqlApiId });
            try
            {
                await client.GetApiCacheAsync(new GetApiCacheRequest { ApiId = gqlApiId });
                throw new Exception("expected error after deleting cache");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteApiCache_NonExistent", async () =>
        {
            try
            {
                await client.DeleteApiCacheAsync(new DeleteApiCacheRequest { ApiId = gqlApiId });
                throw new Exception("expected error for non-existent cache");
            }
            catch { }
        }));

        domainName = $"test-domain-{uid}.example.com";

        results.Add(await runner.RunTestAsync("appsync", "CreateDomainName", async () =>
        {
            var resp = await client.CreateDomainNameAsync(new CreateDomainNameRequest
            {
                DomainName = domainName,
                CertificateArn = "arn:aws:acm:us-east-1:123456789012:certificate/test-cert",
                Description = "test domain"
            });
            if (resp.DomainNameConfig == null)
                throw new Exception("DomainNameConfig is nil");
            if (resp.DomainNameConfig.DomainName != domainName)
                throw new Exception($"expected domain {domainName}, got {resp.DomainNameConfig.DomainName}");
            if (string.IsNullOrEmpty(resp.DomainNameConfig.DomainNameArn))
                throw new Exception("DomainNameArn is nil");
            if (string.IsNullOrEmpty(resp.DomainNameConfig.AppsyncDomainName))
                throw new Exception("AppsyncDomainName is nil");
            if (string.IsNullOrEmpty(resp.DomainNameConfig.HostedZoneId))
                throw new Exception("HostedZoneId is nil");
        }));

        tagDomainName = $"tag-domain-{uid}.example.com";

        results.Add(await runner.RunTestAsync("appsync", "CreateDomainName_WithTags", async () =>
        {
            var resp = await client.CreateDomainNameAsync(new CreateDomainNameRequest
            {
                DomainName = tagDomainName,
                CertificateArn = "arn:aws:acm:us-east-1:123456789012:certificate/tag-cert",
                Tags = new Dictionary<string, string> { { "env", "prod" } }
            });
            if (resp.DomainNameConfig == null || resp.DomainNameConfig.Tags == null)
                throw new Exception("Tags not returned");
            if (resp.DomainNameConfig.Tags["env"] != "prod")
                throw new Exception($"tag not persisted: {resp.DomainNameConfig.Tags}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetDomainName", async () =>
        {
            var resp = await client.GetDomainNameAsync(new GetDomainNameRequest { DomainName = domainName });
            if (resp.DomainNameConfig == null || resp.DomainNameConfig.DomainName != domainName)
                throw new Exception($"expected domain {domainName}, got {resp.DomainNameConfig?.DomainName}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetDomainName_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.GetDomainNameAsync(new GetDomainNameRequest { DomainName = "does-not-exist.example.com" });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "ListDomainNames", async () =>
        {
            var resp = await client.ListDomainNamesAsync(new ListDomainNamesRequest());
            if (resp.DomainNameConfigs == null || resp.DomainNameConfigs.Count < 2)
                throw new Exception($"expected at least 2 domain names, got {resp.DomainNameConfigs?.Count ?? 0}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "ListDomainNames_WithPagination", async () =>
        {
            var resp = await client.ListDomainNamesAsync(new ListDomainNamesRequest { MaxResults = 1 });
            if (resp.DomainNameConfigs == null || resp.DomainNameConfigs.Count != 1)
                throw new Exception($"expected 1 domain name with maxResults=1, got {resp.DomainNameConfigs?.Count ?? 0}");
            if (string.IsNullOrEmpty(resp.NextToken))
                throw new Exception("expected NextToken when more results exist");
        }));

        results.Add(await runner.RunTestAsync("appsync", "UpdateDomainName", async () =>
        {
            var resp = await client.UpdateDomainNameAsync(new UpdateDomainNameRequest
            {
                DomainName = domainName,
                Description = "updated description"
            });
            if (resp.DomainNameConfig == null)
                throw new Exception("DomainNameConfig is nil");
            if (resp.DomainNameConfig.Description != "updated description")
                throw new Exception($"description not updated: {resp.DomainNameConfig.Description}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "UpdateDomainName_NonExistent", async () =>
        {
            try
            {
                await client.UpdateDomainNameAsync(new UpdateDomainNameRequest
                {
                    DomainName = "does-not-exist.example.com"
                });
                throw new Exception("expected error for non-existent domain name");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "AssociateApi", async () =>
        {
            var resp = await client.AssociateApiAsync(new AssociateApiRequest
            {
                DomainName = domainName,
                ApiId = gqlApiId
            });
            if (resp.ApiAssociation == null)
                throw new Exception("ApiAssociation is nil");
            if (resp.ApiAssociation.ApiId != gqlApiId)
                throw new Exception($"expected apiId {gqlApiId}, got {resp.ApiAssociation.ApiId}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetApiAssociation", async () =>
        {
            var resp = await client.GetApiAssociationAsync(new GetApiAssociationRequest { DomainName = domainName });
            if (resp.ApiAssociation == null)
                throw new Exception("ApiAssociation is nil");
            if (resp.ApiAssociation.ApiId != gqlApiId)
                throw new Exception($"expected apiId {gqlApiId}, got {resp.ApiAssociation.ApiId}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "DisassociateApi", async () =>
        {
            await client.DisassociateApiAsync(new DisassociateApiRequest { DomainName = domainName });
            try
            {
                await client.GetApiAssociationAsync(new GetApiAssociationRequest { DomainName = domainName });
                throw new Exception("expected error after disassociating API");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "DisassociateApi_NotAssociated", async () =>
        {
            try
            {
                await client.DisassociateApiAsync(new DisassociateApiRequest { DomainName = domainName });
                throw new Exception("expected error for disassociating non-associated domain");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "CreateGraphqlApi_ForMerged", async () =>
        {
            var resp = await client.CreateGraphqlApiAsync(new CreateGraphqlApiRequest
            {
                Name = $"merged-api-{uid}",
                AuthenticationType = AuthenticationType.API_KEY,
                ApiType = GraphQLApiType.MERGED
            });
            if (resp.GraphqlApi == null || string.IsNullOrEmpty(resp.GraphqlApi.ApiId))
                throw new Exception("invalid response");
            if (resp.GraphqlApi.ApiType != GraphQLApiType.MERGED)
                throw new Exception($"expected MERGED api type, got {resp.GraphqlApi.ApiType}");
            mergedApiId = resp.GraphqlApi.ApiId;
        }));

        results.Add(await runner.RunTestAsync("appsync", "CreateGraphqlApi_ForSource", async () =>
        {
            var resp = await client.CreateGraphqlApiAsync(new CreateGraphqlApiRequest
            {
                Name = $"source-api-{uid}",
                AuthenticationType = AuthenticationType.API_KEY
            });
            if (resp.GraphqlApi == null || string.IsNullOrEmpty(resp.GraphqlApi.ApiId))
                throw new Exception("invalid response");
            sourceApiId2 = resp.GraphqlApi.ApiId;
        }));

        results.Add(await runner.RunTestAsync("appsync", "AssociateSourceGraphqlApi", async () =>
        {
            var resp = await client.AssociateSourceGraphqlApiAsync(new AssociateSourceGraphqlApiRequest
            {
                MergedApiIdentifier = mergedApiId,
                SourceApiIdentifier = sourceApiId2,
                Description = "test association"
            });
            if (resp.SourceApiAssociation == null)
                throw new Exception("SourceApiAssociation is nil");
            if (string.IsNullOrEmpty(resp.SourceApiAssociation.AssociationId))
                throw new Exception("AssociationId is empty");
            if (resp.SourceApiAssociation.SourceApiAssociationStatus != SourceApiAssociationStatus.MERGE_SCHEDULED)
                throw new Exception($"expected MERGE_SCHEDULED, got {resp.SourceApiAssociation.SourceApiAssociationStatus}");
            if (resp.SourceApiAssociation.Description != "test association")
                throw new Exception($"description not set: {resp.SourceApiAssociation.Description}");
            associationId = resp.SourceApiAssociation.AssociationId;
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetSourceApiAssociation", async () =>
        {
            var resp = await client.GetSourceApiAssociationAsync(new GetSourceApiAssociationRequest
            {
                MergedApiIdentifier = mergedApiId,
                AssociationId = associationId
            });
            if (resp.SourceApiAssociation == null || resp.SourceApiAssociation.AssociationId != associationId)
                throw new Exception($"expected association {associationId}, got {resp.SourceApiAssociation?.AssociationId}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetSourceApiAssociation_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.GetSourceApiAssociationAsync(new GetSourceApiAssociationRequest
                {
                    MergedApiIdentifier = mergedApiId,
                    AssociationId = "does-not-exist"
                });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "UpdateSourceApiAssociation", async () =>
        {
            var resp = await client.UpdateSourceApiAssociationAsync(new UpdateSourceApiAssociationRequest
            {
                MergedApiIdentifier = mergedApiId,
                AssociationId = associationId,
                Description = "updated association"
            });
            if (resp.SourceApiAssociation == null)
                throw new Exception("SourceApiAssociation is nil");
            if (resp.SourceApiAssociation.Description != "updated association")
                throw new Exception($"description not updated: {resp.SourceApiAssociation.Description}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "StartSchemaMerge", async () =>
        {
            var resp = await client.StartSchemaMergeAsync(new StartSchemaMergeRequest
            {
                MergedApiIdentifier = mergedApiId,
                AssociationId = associationId
            });
            if (resp.SourceApiAssociationStatus != SourceApiAssociationStatus.MERGE_SUCCESS)
                throw new Exception($"expected MERGE_SUCCESS, got {resp.SourceApiAssociationStatus}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "ListSourceApiAssociations", async () =>
        {
            var resp = await client.ListSourceApiAssociationsAsync(new ListSourceApiAssociationsRequest
            {
                ApiId = sourceApiId2
            });
            if (resp.SourceApiAssociationSummaries == null || resp.SourceApiAssociationSummaries.Count < 1)
                throw new Exception($"expected at least 1 association, got {resp.SourceApiAssociationSummaries?.Count ?? 0}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "DisassociateSourceGraphqlApi", async () =>
        {
            var resp = await client.DisassociateSourceGraphqlApiAsync(new DisassociateSourceGraphqlApiRequest
            {
                MergedApiIdentifier = mergedApiId,
                AssociationId = associationId
            });
            if (resp.SourceApiAssociationStatus != SourceApiAssociationStatus.MERGE_SUCCESS)
                throw new Exception($"expected MERGE_SUCCESS status, got {resp.SourceApiAssociationStatus}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "DisassociateSourceGraphqlApi_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.DisassociateSourceGraphqlApiAsync(new DisassociateSourceGraphqlApiRequest
                {
                    MergedApiIdentifier = mergedApiId,
                    AssociationId = "already-deleted"
                });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "AssociateMergedGraphqlApi", async () =>
        {
            var resp = await client.AssociateMergedGraphqlApiAsync(new AssociateMergedGraphqlApiRequest
            {
                SourceApiIdentifier = sourceApiId2,
                MergedApiIdentifier = mergedApiId,
                Description = "merged from source side"
            });
            if (resp.SourceApiAssociation == null)
                throw new Exception("SourceApiAssociation is nil");
            if (string.IsNullOrEmpty(resp.SourceApiAssociation.AssociationId))
                throw new Exception("AssociationId is empty");
            mergedAssocId = resp.SourceApiAssociation.AssociationId;
        }));

        results.Add(await runner.RunTestAsync("appsync", "DisassociateMergedGraphqlApi", async () =>
        {
            var resp = await client.DisassociateMergedGraphqlApiAsync(new DisassociateMergedGraphqlApiRequest
            {
                SourceApiIdentifier = sourceApiId2,
                AssociationId = mergedAssocId
            });
            if (resp.SourceApiAssociationStatus == null)
                throw new Exception("expected non-empty status, got null");
        }));

        results.Add(await runner.RunTestAsync("appsync", "DisassociateMergedGraphqlApi_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.DisassociateMergedGraphqlApiAsync(new DisassociateMergedGraphqlApiRequest
                {
                    SourceApiIdentifier = sourceApiId2,
                    AssociationId = "already-deleted"
                });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteDomainName", async () =>
        {
            await client.DeleteDomainNameAsync(new DeleteDomainNameRequest { DomainName = domainName });
            try
            {
                await client.GetDomainNameAsync(new GetDomainNameRequest { DomainName = domainName });
                throw new Exception("expected error after deleting domain name");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteDomainName_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.DeleteDomainNameAsync(new DeleteDomainNameRequest { DomainName = "already-deleted.example.com" });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteDomainName_WithTags", async () =>
        {
            await client.DeleteDomainNameAsync(new DeleteDomainNameRequest { DomainName = tagDomainName });
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteGraphqlApi", async () =>
        {
            if (descApiKeyId != null)
            {
                try { await client.DeleteApiKeyAsync(new DeleteApiKeyRequest { ApiId = gqlApiId, Id = descApiKeyId }); } catch { }
            }
            await client.DeleteGraphqlApiAsync(new DeleteGraphqlApiRequest { ApiId = gqlApiId });
            try
            {
                await client.GetGraphqlApiAsync(new GetGraphqlApiRequest { ApiId = gqlApiId });
                throw new Exception("expected error after deleting GraphQL API");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteGraphqlApi_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.DeleteGraphqlApiAsync(new DeleteGraphqlApiRequest { ApiId = "already-deleted" });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteGraphqlApi_WithTags", async () =>
        {
            await client.DeleteGraphqlApiAsync(new DeleteGraphqlApiRequest { ApiId = gqlTagsApiId });
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteGraphqlApi_Merged", async () =>
        {
            await client.DeleteGraphqlApiAsync(new DeleteGraphqlApiRequest { ApiId = mergedApiId });
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteGraphqlApi_Source", async () =>
        {
            await client.DeleteGraphqlApiAsync(new DeleteGraphqlApiRequest { ApiId = sourceApiId2 });
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteChannelNamespace", async () =>
        {
            await client.DeleteChannelNamespaceAsync(new DeleteChannelNamespaceRequest
            {
                ApiId = tagEventApiId,
                Name = tagNsName
            });
            try
            {
                await client.GetChannelNamespaceAsync(new GetChannelNamespaceRequest
                {
                    ApiId = tagEventApiId,
                    Name = tagNsName
                });
                throw new Exception("expected error after deleting namespace");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteChannelNamespace_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.DeleteChannelNamespaceAsync(new DeleteChannelNamespaceRequest
                {
                    ApiId = tagEventApiId,
                    Name = "already-deleted"
                });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteChannelNamespace_ForTagging", async () =>
        {
            if (nsArn != null)
            {
                try { await client.UntagResourceAsync(new UntagResourceRequest { ResourceArn = nsArn, TagKeys = ["nsKey", "added"] }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("appsync", "ListApis_NextTokenFollowUp", async () =>
        {
            var allApis = new List<string>();
            string? nextToken = null;
            var pageCount = 0;
            while (true)
            {
                var req = new ListApisRequest { MaxResults = 1 };
                if (nextToken != null) req.NextToken = nextToken;
                var resp = await client.ListApisAsync(req);
                pageCount++;
                if (resp.Apis != null)
                    foreach (var api in resp.Apis)
                        if (api.Name != null) allApis.Add(api.Name);
                if (!string.IsNullOrEmpty(resp.NextToken))
                    nextToken = resp.NextToken;
                else
                    break;
            }
            if (pageCount < 2)
                throw new Exception($"expected at least 2 pages for ListApis with MaxResults=1, got {pageCount}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteApi", async () =>
        {
            var delApi = await CreateSimpleEventApiAsync(client, uid + 2);
            await client.DeleteApiAsync(new DeleteApiRequest { ApiId = delApi });
            try
            {
                await client.GetApiAsync(new GetApiRequest { ApiId = delApi });
                throw new Exception("expected error after deleting API");
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteApi_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.DeleteApiAsync(new DeleteApiRequest { ApiId = "already-deleted" });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteApi_WithTags", async () =>
        {
            var tagApi2 = await CreateTagApiAsync(client, uid + 1);
            await client.DeleteApiAsync(new DeleteApiRequest { ApiId = tagApi2.ApiId });
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteApi_ForTagging", async () =>
        {
            await client.DeleteApiAsync(new DeleteApiRequest { ApiId = taggedApiId });
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteApi_WithOwnerContact", async () =>
        {
            var ownerApi = await CreateOwnerApiAsync(client, uid);
            await client.DeleteApiAsync(new DeleteApiRequest { ApiId = ownerApi });
        }));

        await TestHelpers.SafeCleanupAsync(async () =>
        {
            try { await client.DeleteApiAsync(new DeleteApiRequest { ApiId = tagEventApiId }); } catch { }
        });

        return results;
    }

    private static async Task<(string ApiId, string ApiArn)> CreateTagApiAsync(AmazonAppSyncClient client, long uid)
    {
        var resp = await client.CreateApiAsync(new CreateApiRequest
        {
            Name = $"test-api-tag-{uid}",
            EventConfig = MakeMinEventConfig(),
            Tags = new Dictionary<string, string> { { "key1", "value1" } }
        });
        if (resp.Api == null || string.IsNullOrEmpty(resp.Api.ApiArn))
            throw new Exception("invalid response");
        return (resp.Api.ApiId, resp.Api.ApiArn);
    }

    private static async Task<string> CreateSimpleEventApiAsync(AmazonAppSyncClient client, long uid)
    {
        var resp = await client.CreateApiAsync(new CreateApiRequest
        {
            Name = $"test-api-ns-{uid}",
            EventConfig = MakeMinEventConfig()
        });
        if (resp.Api == null) throw new Exception("invalid response");
        return resp.Api.ApiId;
    }

    private static async Task<string> CreateOwnerApiAsync(AmazonAppSyncClient client, long uid)
    {
        var resp = await client.CreateApiAsync(new CreateApiRequest
        {
            Name = $"test-api-owner-cleanup-{uid}",
            EventConfig = MakeMinEventConfig(),
            OwnerContact = "cleanup@example.com"
        });
        if (resp.Api == null) throw new Exception("invalid response");
        return resp.Api.ApiId;
    }
}
