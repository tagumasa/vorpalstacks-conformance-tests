using Amazon.CognitoIdentity;
using Amazon.CognitoIdentity.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static class CognitoIdentityServiceTests
{
    public static async Task<List<TestResult>> RunTests(TestRunner runner, AmazonCognitoIdentityClient client, string region)
    {
        var results = new List<TestResult>();
        var uid = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var poolName = $"test-idpool-{uid}";
        string? poolId = null;

        results.Add(await runner.RunTestAsync("cognito-identity", "CreateIdentityPool", async () =>
        {
            var resp = await client.CreateIdentityPoolAsync(new CreateIdentityPoolRequest
            {
                IdentityPoolName = poolName,
                AllowUnauthenticatedIdentities = true
            });
            if (string.IsNullOrEmpty(resp.IdentityPoolId)) throw new Exception("IdentityPoolId is null");
            poolId = resp.IdentityPoolId;
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "DescribeIdentityPool", async () =>
        {
            var resp = await client.DescribeIdentityPoolAsync(new DescribeIdentityPoolRequest { IdentityPoolId = poolId });
            if (resp.IdentityPoolName != poolName) throw new Exception($"expected {poolName}, got {resp.IdentityPoolName}");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "CreateIdentityPool_WithOptions", async () =>
        {
            var name = $"test-idpool-opts-{uid}";
            var resp = await client.CreateIdentityPoolAsync(new CreateIdentityPoolRequest
            {
                IdentityPoolName = name,
                AllowUnauthenticatedIdentities = false,
                AllowClassicFlow = true,
                DeveloperProviderName = "my-dev-provider",
                OpenIdConnectProviderARNs = ["arn:aws:iam::123456789012:oidc-provider/example.com"],
                SamlProviderARNs = ["arn:aws:iam::123456789012:saml-provider/example.com"],
                CognitoIdentityProviders =
                [
                    new CognitoIdentityProviderInfo
                    {
                        ProviderName = "cognito-idp.us-east-1.amazonaws.com/us-east-1_xxxxx",
                        ClientId = "abc123",
                        ServerSideTokenCheck = true
                    }
                ],
                SupportedLoginProviders = new Dictionary<string, string>
                {
                    { "graph.facebook.com", "1234567890" }
                }
            });
            if (resp.IdentityPoolName != name) throw new Exception($"expected {name}, got {resp.IdentityPoolName}");
            if (resp.AllowUnauthenticatedIdentities == true) throw new Exception("expected AllowUnauthenticatedIdentities false");
            if (resp.AllowClassicFlow != true) throw new Exception("expected AllowClassicFlow true");
            if (resp.DeveloperProviderName != "my-dev-provider") throw new Exception("expected DeveloperProviderName");
            if (resp.OpenIdConnectProviderARNs == null || resp.OpenIdConnectProviderARNs.Count != 1)
                throw new Exception("expected 1 OpenIdConnectProviderARN");
            if (resp.SamlProviderARNs == null || resp.SamlProviderARNs.Count != 1)
                throw new Exception("expected 1 SamlProviderARN");
            if (resp.CognitoIdentityProviders == null || resp.CognitoIdentityProviders.Count != 1)
                throw new Exception("expected 1 CognitoIdentityProvider");
            if (resp.SupportedLoginProviders == null || resp.SupportedLoginProviders.Count != 1)
                throw new Exception("expected 1 SupportedLoginProvider");
            await TestHelpers.SafeCleanupAsync(async () =>
            {
                await client.DeleteIdentityPoolAsync(new DeleteIdentityPoolRequest { IdentityPoolId = resp.IdentityPoolId });
            });
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "ListIdentityPools", async () =>
        {
            var resp = await client.ListIdentityPoolsAsync(new ListIdentityPoolsRequest { MaxResults = 10 });
            if (resp.IdentityPools == null || resp.IdentityPools.Count < 1)
                throw new Exception($"expected at least 1 pool, got {resp.IdentityPools?.Count ?? 0}");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "UpdateIdentityPool", async () =>
        {
            var newName = poolName + "-updated";
            await client.UpdateIdentityPoolAsync(new UpdateIdentityPoolRequest
            {
                IdentityPoolId = poolId,
                IdentityPoolName = newName,
                AllowUnauthenticatedIdentities = false
            });
            var resp = await client.DescribeIdentityPoolAsync(new DescribeIdentityPoolRequest { IdentityPoolId = poolId });
            if (resp.IdentityPoolName != newName) throw new Exception($"expected {newName}, got {resp.IdentityPoolName}");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "GetId", async () =>
        {
            var resp = await client.GetIdAsync(new GetIdRequest { IdentityPoolId = poolId });
            if (string.IsNullOrEmpty(resp.IdentityId)) throw new Exception("IdentityId is null or empty");
        }));

        string? identityId = null;
        results.Add(await runner.RunTestAsync("cognito-identity", "GetId_WithLogins", async () =>
        {
            var resp = await client.GetIdAsync(new GetIdRequest
            {
                IdentityPoolId = poolId,
                Logins = new Dictionary<string, string> { { "graph.facebook.com", "test-token" } }
            });
            if (string.IsNullOrEmpty(resp.IdentityId)) throw new Exception("IdentityId is null or empty");
            identityId = resp.IdentityId;
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "DescribeIdentity", async () =>
        {
            var resp = await client.DescribeIdentityAsync(new DescribeIdentityRequest { IdentityId = identityId });
            if (resp.IdentityId != identityId) throw new Exception($"expected {identityId}, got {resp.IdentityId}");
            var found = resp.Logins != null && resp.Logins.Any(l => l == "graph.facebook.com");
            if (!found) throw new Exception("expected graph.facebook.com in Logins");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "GetCredentialsForIdentity", async () =>
        {
            var resp = await client.GetCredentialsForIdentityAsync(new GetCredentialsForIdentityRequest
            {
                IdentityId = identityId
            });
            if (resp.IdentityId != identityId) throw new Exception($"expected {identityId}, got {resp.IdentityId}");
            if (resp.Credentials == null) throw new Exception("Credentials is nil");
            if (string.IsNullOrEmpty(resp.Credentials.AccessKeyId)) throw new Exception("AccessKeyId is null or empty");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "SetIdentityPoolRoles", async () =>
        {
            await client.SetIdentityPoolRolesAsync(new SetIdentityPoolRolesRequest
            {
                IdentityPoolId = poolId,
                Roles = new Dictionary<string, string>
                {
                    { "authenticated", "arn:aws:iam::123456789012:role/auth-role" },
                    { "unauthenticated", "arn:aws:iam::123456789012:role/unauth-role" }
                }
            });
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "GetIdentityPoolRoles", async () =>
        {
            var resp = await client.GetIdentityPoolRolesAsync(new GetIdentityPoolRolesRequest { IdentityPoolId = poolId });
            if (resp.Roles == null) throw new Exception("Roles is nil");
            if (resp.Roles["authenticated"] != "arn:aws:iam::123456789012:role/auth-role")
                throw new Exception("unexpected authenticated role");
            if (resp.Roles["unauthenticated"] != "arn:aws:iam::123456789012:role/unauth-role")
                throw new Exception("unexpected unauthenticated role");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "SetIdentityPoolRoles_WithMappings", async () =>
        {
            await client.SetIdentityPoolRolesAsync(new SetIdentityPoolRolesRequest
            {
                IdentityPoolId = poolId,
                Roles = new Dictionary<string, string>
                {
                    { "authenticated", "arn:aws:iam::123456789012:role/auth-role" }
                },
                RoleMappings = new Dictionary<string, RoleMapping>
                {
                    {
                        "graph.facebook.com", new RoleMapping
                        {
                            Type = RoleMappingType.Token,
                            AmbiguousRoleResolution = AmbiguousRoleResolutionType.AuthenticatedRole
                        }
                    }
                }
            });
            var resp = await client.GetIdentityPoolRolesAsync(new GetIdentityPoolRolesRequest { IdentityPoolId = poolId });
            if (resp.RoleMappings == null) throw new Exception("RoleMappings is nil");
            if (!resp.RoleMappings.TryGetValue("graph.facebook.com", out var m) || m.Type != RoleMappingType.Token)
                throw new Exception("expected Token type");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "TagResource", async () =>
        {
            await client.DescribeIdentityPoolAsync(new DescribeIdentityPoolRequest { IdentityPoolId = poolId });
            await client.TagResourceAsync(new TagResourceRequest
            {
                ResourceArn = $"arn:aws:cognito-identity:{region}:000000000000:identitypool/{poolId}",
                Tags = new Dictionary<string, string>
                {
                    { "Environment", "test" },
                    { "Team", "sdk-tests" }
                }
            });
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "ListTagsForResource", async () =>
        {
            var resp = await client.ListTagsForResourceAsync(new ListTagsForResourceRequest
            {
                ResourceArn = $"arn:aws:cognito-identity:{region}:000000000000:identitypool/{poolId}"
            });
            if (resp.Tags == null) throw new Exception("Tags is nil");
            if (resp.Tags["Environment"] != "test") throw new Exception("expected Environment=test");
            if (resp.Tags["Team"] != "sdk-tests") throw new Exception("expected Team=sdk-tests");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "UntagResource", async () =>
        {
            var arn = $"arn:aws:cognito-identity:{region}:000000000000:identitypool/{poolId}";
            await client.UntagResourceAsync(new UntagResourceRequest
            {
                ResourceArn = arn,
                TagKeys = ["Team"]
            });
            var resp = await client.ListTagsForResourceAsync(new ListTagsForResourceRequest { ResourceArn = arn });
            if (resp.Tags != null && resp.Tags.ContainsKey("Team"))
                throw new Exception("Team tag should have been removed");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "ListIdentities", async () =>
        {
            var resp = await client.ListIdentitiesAsync(new ListIdentitiesRequest
            {
                IdentityPoolId = poolId,
                MaxResults = 10
            });
            if (resp.IdentityPoolId != poolId) throw new Exception($"expected pool ID {poolId}");
            if (resp.Identities == null || resp.Identities.Count < 1)
                throw new Exception($"expected at least 1 identity, got {resp.Identities?.Count ?? 0}");
        }));

        string? secondIdentityId = null;
        results.Add(await runner.RunTestAsync("cognito-identity", "GetId_SecondIdentity", async () =>
        {
            var resp = await client.GetIdAsync(new GetIdRequest
            {
                IdentityPoolId = poolId,
                Logins = new Dictionary<string, string> { { "accounts.google.com", "google-token" } }
            });
            secondIdentityId = resp.IdentityId;
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "DeleteIdentities", async () =>
        {
            var resp = await client.DeleteIdentitiesAsync(new DeleteIdentitiesRequest
            {
                IdentityIdsToDelete = [secondIdentityId]
            });
            if (resp.UnprocessedIdentityIds != null && resp.UnprocessedIdentityIds.Count > 0)
                throw new Exception("unexpected unprocessed identities");
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await client.DescribeIdentityAsync(new DescribeIdentityRequest { IdentityId = secondIdentityId });
            });
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "GetOpenIdToken", async () =>
        {
            var resp = await client.GetOpenIdTokenAsync(new GetOpenIdTokenRequest { IdentityId = identityId });
            if (resp.IdentityId != identityId) throw new Exception($"expected {identityId}, got {resp.IdentityId}");
            if (string.IsNullOrEmpty(resp.Token)) throw new Exception("Token is nil or empty");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "GetOpenIdToken_WithLogins", async () =>
        {
            var resp = await client.GetOpenIdTokenAsync(new GetOpenIdTokenRequest
            {
                IdentityId = identityId,
                Logins = new Dictionary<string, string> { { "graph.facebook.com", "new-token" } }
            });
            if (string.IsNullOrEmpty(resp.Token)) throw new Exception("Token is nil or empty");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "SetPrincipalTagAttributeMap", async () =>
        {
            await client.SetPrincipalTagAttributeMapAsync(new SetPrincipalTagAttributeMapRequest
            {
                IdentityPoolId = poolId,
                IdentityProviderName = "graph.facebook.com",
                PrincipalTags = new Dictionary<string, string>
                {
                    { "email", "email" },
                    { "username", "sub" }
                },
                UseDefaults = false
            });
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "GetPrincipalTagAttributeMap", async () =>
        {
            var resp = await client.GetPrincipalTagAttributeMapAsync(new GetPrincipalTagAttributeMapRequest
            {
                IdentityPoolId = poolId,
                IdentityProviderName = "graph.facebook.com"
            });
            if (resp.IdentityPoolId != poolId) throw new Exception("expected pool ID");
            if (resp.PrincipalTags == null || resp.PrincipalTags["email"] != "email")
                throw new Exception("expected email->email mapping");
            if (resp.PrincipalTags["username"] != "sub")
                throw new Exception("expected username->sub mapping");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "GetPrincipalTagAttributeMap_Defaults", async () =>
        {
            var resp = await client.GetPrincipalTagAttributeMapAsync(new GetPrincipalTagAttributeMapRequest
            {
                IdentityPoolId = poolId,
                IdentityProviderName = "accounts.google.com"
            });
            if (resp.UseDefaults != true) throw new Exception("expected UseDefaults=true for non-existent mapping");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "GetOpenIdTokenForDeveloperIdentity", async () =>
        {
            var resp = await client.GetOpenIdTokenForDeveloperIdentityAsync(new GetOpenIdTokenForDeveloperIdentityRequest
            {
                IdentityPoolId = poolId,
                Logins = new Dictionary<string, string> { { "my-dev-provider", "dev-user-1" } }
            });
            if (string.IsNullOrEmpty(resp.IdentityId)) throw new Exception("IdentityId is nil or empty");
            if (string.IsNullOrEmpty(resp.Token)) throw new Exception("Token is nil or empty");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "LookupDeveloperIdentity", async () =>
        {
            var resp = await client.LookupDeveloperIdentityAsync(new LookupDeveloperIdentityRequest
            {
                IdentityPoolId = poolId,
                DeveloperUserIdentifier = "dev-user-1"
            });
            if (resp.DeveloperUserIdentifierList == null || resp.DeveloperUserIdentifierList.Count < 1)
                throw new Exception("expected at least 1 developer user identifier");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "GetOpenIdTokenForDeveloperIdentity_Reuse", async () =>
        {
            var resp = await client.GetOpenIdTokenForDeveloperIdentityAsync(new GetOpenIdTokenForDeveloperIdentityRequest
            {
                IdentityPoolId = poolId,
                Logins = new Dictionary<string, string> { { "my-dev-provider", "dev-user-1" } }
            });
            if (string.IsNullOrEmpty(resp.IdentityId)) throw new Exception("IdentityId is nil or empty");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "MergeDeveloperIdentities", async () =>
        {
            await client.GetOpenIdTokenForDeveloperIdentityAsync(new GetOpenIdTokenForDeveloperIdentityRequest
            {
                IdentityPoolId = poolId,
                Logins = new Dictionary<string, string> { { "my-dev-provider", "dev-user-2" } }
            });
            var resp = await client.MergeDeveloperIdentitiesAsync(new MergeDeveloperIdentitiesRequest
            {
                SourceUserIdentifier = "dev-user-1",
                DestinationUserIdentifier = "dev-user-2",
                DeveloperProviderName = "my-dev-provider",
                IdentityPoolId = poolId
            });
            if (string.IsNullOrEmpty(resp.IdentityId)) throw new Exception("IdentityId is nil or empty after merge");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "UnlinkDeveloperIdentity", async () =>
        {
            await client.UnlinkDeveloperIdentityAsync(new UnlinkDeveloperIdentityRequest
            {
                IdentityPoolId = poolId,
                IdentityId = identityId,
                DeveloperProviderName = "my-dev-provider",
                DeveloperUserIdentifier = "dev-user-1"
            });
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "UnlinkIdentity", async () =>
        {
            await client.UnlinkIdentityAsync(new UnlinkIdentityRequest
            {
                IdentityId = identityId,
                Logins = new Dictionary<string, string> { { "graph.facebook.com", "token" } },
                LoginsToRemove = ["graph.facebook.com"]
            });
            var resp = await client.DescribeIdentityAsync(new DescribeIdentityRequest { IdentityId = identityId });
            if (resp.Logins != null && resp.Logins.Any(l => l == "graph.facebook.com"))
                throw new Exception("graph.facebook.com should have been unlinked");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "DeleteIdentityPool", async () =>
        {
            await client.DeleteIdentityPoolAsync(new DeleteIdentityPoolRequest { IdentityPoolId = poolId });
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await client.DescribeIdentityPoolAsync(new DescribeIdentityPoolRequest { IdentityPoolId = poolId });
            });
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "DescribeIdentityPool_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await client.DescribeIdentityPoolAsync(new DescribeIdentityPoolRequest
                {
                    IdentityPoolId = "us-east-1:00000000-0000-0000-0000-000000000000"
                });
            });
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "DeleteIdentityPool_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await client.DeleteIdentityPoolAsync(new DeleteIdentityPoolRequest
                {
                    IdentityPoolId = "us-east-1:00000000-0000-0000-0000-000000000000"
                });
            });
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "DescribeIdentity_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await client.DescribeIdentityAsync(new DescribeIdentityRequest
                {
                    IdentityId = "00000000-0000-0000-0000-000000000000"
                });
            });
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "GetId_NonExistentPool", async () =>
        {
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await client.GetIdAsync(new GetIdRequest
                {
                    IdentityPoolId = "us-east-1:00000000-0000-0000-0000-000000000000"
                });
            });
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "CreateIdentityPool_WithTags", async () =>
        {
            var name = $"test-idpool-tags-{uid}";
            var resp = await client.CreateIdentityPoolAsync(new CreateIdentityPoolRequest
            {
                IdentityPoolName = name,
                AllowUnauthenticatedIdentities = true,
                IdentityPoolTags = new Dictionary<string, string>
                {
                    { "Env", "production" },
                    { "Cost", "high" }
                }
            });
            if (resp.IdentityPoolTags == null) throw new Exception("IdentityPoolTags is nil");
            if (resp.IdentityPoolTags["Env"] != "production") throw new Exception("expected Env=production");
            await TestHelpers.SafeCleanupAsync(async () =>
            {
                await client.DeleteIdentityPoolAsync(new DeleteIdentityPoolRequest { IdentityPoolId = resp.IdentityPoolId });
            });
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "SetIdentityPoolRoles_RuleMappings", async () =>
        {
            var name = $"test-idpool-rules-{uid}";
            var createResp = await client.CreateIdentityPoolAsync(new CreateIdentityPoolRequest
            {
                IdentityPoolName = name,
                AllowUnauthenticatedIdentities = true
            });
            var pid = createResp.IdentityPoolId;

            await client.SetIdentityPoolRolesAsync(new SetIdentityPoolRolesRequest
            {
                IdentityPoolId = pid,
                Roles = new Dictionary<string, string>
                {
                    { "authenticated", "arn:aws:iam::123456789012:role/auth" }
                },
                RoleMappings = new Dictionary<string, RoleMapping>
                {
                    {
                        "graph.facebook.com", new RoleMapping
                        {
                            Type = RoleMappingType.Rules,
                            AmbiguousRoleResolution = AmbiguousRoleResolutionType.Deny,
                            RulesConfiguration = new RulesConfigurationType
                            {
                                Rules =
                                [
                                    new MappingRule
                                    {
                                        Claim = "isAdmin",
                                        MatchType = MappingRuleMatchType.Equals,
                                        Value = "true",
                                        RoleARN = "arn:aws:iam::123456789012:role/admin"
                                    }
                                ]
                            }
                        }
                    }
                }
            });

            var resp = await client.GetIdentityPoolRolesAsync(new GetIdentityPoolRolesRequest { IdentityPoolId = pid });
            if (resp.RoleMappings == null) throw new Exception("RoleMappings is nil");
            if (!resp.RoleMappings.TryGetValue("graph.facebook.com", out var m) || m.Type != RoleMappingType.Rules)
                throw new Exception("expected Rules type");
            if (m.RulesConfiguration == null || m.RulesConfiguration.Rules == null || m.RulesConfiguration.Rules.Count != 1)
                throw new Exception("expected 1 rule");
            if (m.RulesConfiguration.Rules[0].Claim != "isAdmin") throw new Exception("expected claim isAdmin");
            await TestHelpers.SafeCleanupAsync(async () =>
            {
                await client.DeleteIdentityPoolAsync(new DeleteIdentityPoolRequest { IdentityPoolId = pid });
            });
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "GetCredentialsForIdentity_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await client.GetCredentialsForIdentityAsync(new GetCredentialsForIdentityRequest
                {
                    IdentityId = "00000000-0000-0000-0000-000000000000"
                });
            });
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "GetOpenIdToken_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await client.GetOpenIdTokenAsync(new GetOpenIdTokenRequest
                {
                    IdentityId = "00000000-0000-0000-0000-000000000000"
                });
            });
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "DeleteIdentities_NonExistent", async () =>
        {
            var resp = await client.DeleteIdentitiesAsync(new DeleteIdentitiesRequest
            {
                IdentityIdsToDelete = ["00000000-0000-0000-0000-000000000000"]
            });
            if (resp.UnprocessedIdentityIds == null || resp.UnprocessedIdentityIds.Count != 1)
                throw new Exception("expected 1 unprocessed identity");
        }));

        results.Add(await runner.RunTestAsync("cognito-identity", "GetIdentityPoolRoles_NonExistent", async () =>
        {
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await client.GetIdentityPoolRolesAsync(new GetIdentityPoolRolesRequest
                {
                    IdentityPoolId = "us-east-1:00000000-0000-0000-0000-000000000000"
                });
            });
        }));

        // Pagination test
        results.Add(await runner.RunTestAsync("cognito-identity", "ListIdentityPools_Pagination", async () =>
        {
            var pgTs = $"{uid}";
            var pgPools = new List<string>();
            for (var i = 0; i < 5; i++)
            {
                var name = $"PagIdPool-{pgTs}-{i}";
                try
                {
                    var resp = await client.CreateIdentityPoolAsync(new CreateIdentityPoolRequest
                    {
                        IdentityPoolName = name,
                        AllowUnauthenticatedIdentities = true
                    });
                    pgPools.Add(resp.IdentityPoolId);
                }
                catch
                {
                    foreach (var pid in pgPools)
                    {
                        try { await client.DeleteIdentityPoolAsync(new DeleteIdentityPoolRequest { IdentityPoolId = pid }); } catch { }
                    }
                    throw;
                }
            }

            var allPools = new List<string>();
            string? nextToken = null;
            try
            {
                while (true)
                {
                    var resp = await client.ListIdentityPoolsAsync(new ListIdentityPoolsRequest
                    {
                        MaxResults = 2,
                        NextToken = nextToken
                    });
                    if (resp.IdentityPools != null)
                    {
                        foreach (var p in resp.IdentityPools)
                        {
                            if (p.IdentityPoolName != null && p.IdentityPoolName.Contains($"PagIdPool-{pgTs}"))
                                allPools.Add(p.IdentityPoolName);
                        }
                    }
                    if (!string.IsNullOrEmpty(resp.NextToken))
                        nextToken = resp.NextToken;
                    else
                        break;
                }
            }
            finally
            {
                foreach (var pid in pgPools)
                {
                    try { await client.DeleteIdentityPoolAsync(new DeleteIdentityPoolRequest { IdentityPoolId = pid }); } catch { }
                }
            }

            if (allPools.Count != 5)
                throw new Exception($"expected 5 paginated identity pools, got {allPools.Count}");
        }));

        return results;
    }
}
