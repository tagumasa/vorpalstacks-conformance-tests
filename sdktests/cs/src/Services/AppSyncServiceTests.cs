using Amazon.AppSync;
using Amazon.AppSync.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static partial class AppSyncServiceTests
{
    private static AuthProvider MakeApiKeyAuthProvider() => new()
    {
        AuthType = AuthenticationType.API_KEY
    };

    private static AuthMode MakeApiKeyAuthMode() => new()
    {
        AuthType = AuthenticationType.API_KEY
    };

    private static EventConfig MakeMinEventConfig() => new()
    {
        AuthProviders = [MakeApiKeyAuthProvider()],
        ConnectionAuthModes = [MakeApiKeyAuthMode()],
        DefaultPublishAuthModes = [MakeApiKeyAuthMode()],
        DefaultSubscribeAuthModes = [MakeApiKeyAuthMode()]
    };

    public static async Task<List<TestResult>> RunTests(TestRunner runner, AmazonAppSyncClient client, string region)
    {
        var results = new List<TestResult>();
        results = await RunEventApiTests(runner, client, results, region);
        results = await RunGraphQLApiTests(runner, client, results, region);
        return results;
    }

    public static async Task<List<TestResult>> RunEventApiTests(
        TestRunner runner,
        AmazonAppSyncClient client,
        List<TestResult> results,
        string region)
    {
        var uid = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string? apiId = null;
        string? tagsApiId = null;
        string? ownerApiId = null;

        results.Add(await runner.RunTestAsync("appsync", "CreateApi", async () =>
        {
            var resp = await client.CreateApiAsync(new CreateApiRequest
            {
                Name = $"test-api-{uid}",
                EventConfig = MakeMinEventConfig()
            });
            if (resp.Api == null) throw new Exception("Api is null");
            if (string.IsNullOrEmpty(resp.Api.ApiId)) throw new Exception("ApiId is empty");
            apiId = resp.Api.ApiId;
        }));

        results.Add(await runner.RunTestAsync("appsync", "CreateApi_WithTags", async () =>
        {
            var resp = await client.CreateApiAsync(new CreateApiRequest
            {
                Name = $"test-api-tags-{uid}",
                EventConfig = MakeMinEventConfig(),
                Tags = new Dictionary<string, string>
                {
                    { "env", "test" },
                    { "team", "platform" }
                }
            });
            if (resp.Api == null) throw new Exception("invalid response");
            if (resp.Api.Tags == null || resp.Api.Tags.Count != 2)
                throw new Exception($"tags not persisted: {resp.Api.Tags?.Count ?? 0}");
            if (resp.Api.Tags["env"] != "test" || resp.Api.Tags["team"] != "platform")
                throw new Exception("tag values mismatch");
            tagsApiId = resp.Api.ApiId;
        }));

        results.Add(await runner.RunTestAsync("appsync", "CreateApi_WithOwnerContact", async () =>
        {
            var resp = await client.CreateApiAsync(new CreateApiRequest
            {
                Name = $"test-api-owner-{uid}",
                EventConfig = MakeMinEventConfig(),
                OwnerContact = "test@example.com"
            });
            if (resp.Api == null || resp.Api.OwnerContact != "test@example.com")
                throw new Exception($"ownerContact not set: {resp.Api?.OwnerContact}");
            ownerApiId = resp.Api.ApiId;
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetApi", async () =>
        {
            var resp = await client.GetApiAsync(new GetApiRequest { ApiId = apiId });
            if (resp.Api == null) throw new Exception("Api is null");
            if (resp.Api.ApiId != apiId) throw new Exception($"expected ApiId {apiId}, got {resp.Api.ApiId}");
            if (resp.Api.EventConfig == null) throw new Exception("EventConfig is null");
            if (resp.Api.EventConfig.AuthProviders == null || resp.Api.EventConfig.AuthProviders.Count != 1)
                throw new Exception($"expected 1 auth provider, got {resp.Api.EventConfig.AuthProviders?.Count ?? 0}");
            if (string.IsNullOrEmpty(resp.Api.ApiArn)) throw new Exception("ApiArn is null");
            if (!resp.Api.ApiArn.StartsWith("arn:aws:appsync:"))
                throw new Exception($"invalid ARN format: {resp.Api.ApiArn}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetApi_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.GetApiAsync(new GetApiRequest { ApiId = "does-not-exist" });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "ListApis", async () =>
        {
            var resp = await client.ListApisAsync(new ListApisRequest());
            if (resp.Apis == null || resp.Apis.Count < 3)
                throw new Exception($"expected at least 3 APIs, got {resp.Apis?.Count ?? 0}");
            var found = resp.Apis.Any(a => a.ApiId == apiId);
            if (!found) throw new Exception("created API not found in list");
        }));

        results.Add(await runner.RunTestAsync("appsync", "ListApis_WithPagination", async () =>
        {
            var resp = await client.ListApisAsync(new ListApisRequest { MaxResults = 1 });
            if (resp.Apis == null || resp.Apis.Count != 1)
                throw new Exception($"expected 1 API with maxResults=1, got {resp.Apis?.Count ?? 0}");
            if (string.IsNullOrEmpty(resp.NextToken))
                throw new Exception("expected NextToken when more results exist");
        }));

        results.Add(await runner.RunTestAsync("appsync", "UpdateApi", async () =>
        {
            var newName = $"updated-api-{uid}";
            var resp = await client.UpdateApiAsync(new UpdateApiRequest
            {
                ApiId = apiId,
                Name = newName,
                EventConfig = MakeMinEventConfig()
            });
            if (resp.Api == null || resp.Api.Name != newName)
                throw new Exception($"expected name {newName}, got {resp.Api?.Name}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "UpdateApi_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.UpdateApiAsync(new UpdateApiRequest
                {
                    ApiId = "does-not-exist",
                    Name = "noop",
                    EventConfig = MakeMinEventConfig()
                });
            });
        }));

        // Channel Namespace CRUD
        var nsName = $"testns-{uid}";
        string? nsArn = null;

        results.Add(await runner.RunTestAsync("appsync", "CreateChannelNamespace", async () =>
        {
            var resp = await client.CreateChannelNamespaceAsync(new CreateChannelNamespaceRequest
            {
                ApiId = apiId,
                Name = nsName,
                Tags = new Dictionary<string, string> { { "type", "test" } }
            });
            if (resp.ChannelNamespace == null) throw new Exception("ChannelNamespace is nil");
            if (resp.ChannelNamespace.Name != nsName)
                throw new Exception($"expected name {nsName}, got {resp.ChannelNamespace.Name}");
            nsArn = resp.ChannelNamespace.ChannelNamespaceArn;
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetChannelNamespace", async () =>
        {
            var resp = await client.GetChannelNamespaceAsync(new GetChannelNamespaceRequest
            {
                ApiId = apiId,
                Name = nsName
            });
            if (resp.ChannelNamespace == null || resp.ChannelNamespace.Name != nsName)
                throw new Exception($"expected namespace {nsName}, got {resp.ChannelNamespace?.Name}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "GetChannelNamespace_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.GetChannelNamespaceAsync(new GetChannelNamespaceRequest
                {
                    ApiId = apiId,
                    Name = "does-not-exist"
                });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "ListChannelNamespaces", async () =>
        {
            var resp = await client.ListChannelNamespacesAsync(new ListChannelNamespacesRequest { ApiId = apiId });
            if (resp.ChannelNamespaces == null || resp.ChannelNamespaces.Count == 0)
                throw new Exception("expected at least 1 namespace");
            var found = resp.ChannelNamespaces.Any(ns => ns.Name == nsName);
            if (!found) throw new Exception("created namespace not found in list");
        }));

        results.Add(await runner.RunTestAsync("appsync", "UpdateChannelNamespace", async () =>
        {
            var resp = await client.UpdateChannelNamespaceAsync(new UpdateChannelNamespaceRequest
            {
                ApiId = apiId,
                Name = nsName
            });
            if (resp.ChannelNamespace == null || resp.ChannelNamespace.Name != nsName)
                throw new Exception($"expected namespace {nsName}, got {resp.ChannelNamespace?.Name}");
        }));

        results.Add(await runner.RunTestAsync("appsync", "UpdateChannelNamespace_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<NotFoundException>(async () =>
            {
                await client.UpdateChannelNamespaceAsync(new UpdateChannelNamespaceRequest
                {
                    ApiId = apiId,
                    Name = "does-not-exist"
                });
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "TagChannelNamespace", async () =>
        {
            await client.TagResourceAsync(new TagResourceRequest
            {
                ResourceArn = nsArn,
                Tags = new Dictionary<string, string> { { "env", "production" } }
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "UntagChannelNamespace", async () =>
        {
            await client.UntagResourceAsync(new UntagResourceRequest
            {
                ResourceArn = nsArn,
                TagKeys = ["env"]
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteChannelNamespace", async () =>
        {
            await client.DeleteChannelNamespaceAsync(new DeleteChannelNamespaceRequest
            {
                ApiId = apiId,
                Name = nsName
            });
        }));

        results.Add(await runner.RunTestAsync("appsync", "DeleteApi", async () =>
        {
            await client.DeleteApiAsync(new DeleteApiRequest { ApiId = apiId });
        }));

        await TestHelpers.SafeCleanupAsync(async () =>
        {
            if (tagsApiId != null)
                await client.DeleteApiAsync(new DeleteApiRequest { ApiId = tagsApiId });
            if (ownerApiId != null)
                await client.DeleteApiAsync(new DeleteApiRequest { ApiId = ownerApiId });
        });

        return results;
    }
}
