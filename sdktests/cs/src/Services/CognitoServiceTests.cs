using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static class CognitoServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonCognitoIdentityProviderClient cognitoClient,
        string region)
    {
        var results = new List<TestResult>();
        var userPoolId = "";
        var poolName = TestRunner.MakeUniqueName("CSPool");
        var clientName = TestRunner.MakeUniqueName("CSClient");
        var clientId = "";
        var domainName = TestRunner.MakeUniqueName("csdomain");
        var groupName = TestRunner.MakeUniqueName("CSGroup");
        var userName = TestRunner.MakeUniqueName("CSUser");
        var resourceServerId = TestRunner.MakeUniqueName("csresource");

        try
        {
            // Test 1: CreateUserPool
            results.Add(await runner.RunTestAsync("cognito", "CreateUserPool", async () =>
            {
                var resp = await cognitoClient.CreateUserPoolAsync(new CreateUserPoolRequest
                {
                    PoolName = poolName,
                    Policies = new UserPoolPolicyType
                    {
                        PasswordPolicy = new PasswordPolicyType
                        {
                            MinimumLength = 8,
                            RequireUppercase = true,
                            RequireLowercase = true,
                            RequireNumbers = true,
                            RequireSymbols = false,
                        },
                    },
                });
                if (resp.UserPool == null)
                    throw new Exception("UserPool is null");
                userPoolId = resp.UserPool.Id;
            }));

            if (!string.IsNullOrEmpty(userPoolId))
            {
                // Test 2: DescribeUserPool
                results.Add(await runner.RunTestAsync("cognito", "DescribeUserPool", async () =>
                {
                    var resp = await cognitoClient.DescribeUserPoolAsync(new DescribeUserPoolRequest
                    {
                        UserPoolId = userPoolId
                    });
                    if (resp.UserPool == null)
                        throw new Exception("UserPool is null");
                }));

                // Test 3: CreateUserPoolClient
                results.Add(await runner.RunTestAsync("cognito", "CreateUserPoolClient", async () =>
                {
                    var resp = await cognitoClient.CreateUserPoolClientAsync(new CreateUserPoolClientRequest
                    {
                        UserPoolId = userPoolId,
                        ClientName = clientName,
                    });
                    if (resp.UserPoolClient == null || string.IsNullOrEmpty(resp.UserPoolClient.ClientId))
                        throw new Exception("UserPoolClient is null or ClientId is empty");
                    clientId = resp.UserPoolClient.ClientId;
                }));

                // Test 4: DescribeUserPoolClient
                if (!string.IsNullOrEmpty(clientId))
                {
                    results.Add(await runner.RunTestAsync("cognito", "DescribeUserPoolClient", async () =>
                    {
                        var resp = await cognitoClient.DescribeUserPoolClientAsync(new DescribeUserPoolClientRequest
                        {
                            ClientId = clientId,
                            UserPoolId = userPoolId,
                        });
                        if (resp.UserPoolClient == null)
                            throw new Exception("UserPoolClient is null");
                    }));

                    // Test 5: UpdateUserPoolClient
                    results.Add(await runner.RunTestAsync("cognito", "UpdateUserPoolClient", async () =>
                    {
                        var resp = await cognitoClient.UpdateUserPoolClientAsync(new UpdateUserPoolClientRequest
                        {
                            ClientId = clientId,
                            UserPoolId = userPoolId,
                            ClientName = "updated-client",
                        });
                        if (resp.UserPoolClient == null)
                            throw new Exception("UserPoolClient is null");
                    }));
                }

                // Test 6: CreateUserPoolDomain
                results.Add(await runner.RunTestAsync("cognito", "CreateUserPoolDomain", async () =>
                {
                    await cognitoClient.CreateUserPoolDomainAsync(new CreateUserPoolDomainRequest
                    {
                        Domain = domainName,
                        UserPoolId = userPoolId,
                    });
                }));

                // Test 7: DescribeUserPoolDomain
                results.Add(await runner.RunTestAsync("cognito", "DescribeUserPoolDomain", async () =>
                {
                    var resp = await cognitoClient.DescribeUserPoolDomainAsync(new DescribeUserPoolDomainRequest
                    {
                        Domain = domainName,
                    });
                    if (resp.DomainDescription == null)
                        throw new Exception("DomainDescription is null");
                }));

                // Test 8: ListUserPoolClients
                results.Add(await runner.RunTestAsync("cognito", "ListUserPoolClients", async () =>
                {
                    var resp = await cognitoClient.ListUserPoolClientsAsync(new ListUserPoolClientsRequest
                    {
                        UserPoolId = userPoolId,
                        MaxResults = 10,
                    });
                    if (resp.UserPoolClients == null)
                        throw new Exception("UserPoolClients is null");
                    if (resp.UserPoolClients.Count < 1)
                        throw new Exception("Expected at least 1 client");
                }));

                // Test 9: ListUserPools
                results.Add(await runner.RunTestAsync("cognito", "ListUserPools", async () =>
                {
                    var resp = await cognitoClient.ListUserPoolsAsync(new ListUserPoolsRequest
                    {
                        MaxResults = 10,
                    });
                    if (resp.UserPools == null)
                        throw new Exception("UserPools is null");
                    if (resp.UserPools.Count < 1)
                        throw new Exception("Expected at least 1 pool");
                }));

                // Test 10: CreateGroup
                results.Add(await runner.RunTestAsync("cognito", "CreateGroup", async () =>
                {
                    var resp = await cognitoClient.CreateGroupAsync(new CreateGroupRequest
                    {
                        GroupName = groupName,
                        UserPoolId = userPoolId,
                    });
                    if (resp.Group == null)
                        throw new Exception("Group is null");
                }));

                // Test 11: ListGroups
                results.Add(await runner.RunTestAsync("cognito", "ListGroups", async () =>
                {
                    var resp = await cognitoClient.ListGroupsAsync(new ListGroupsRequest
                    {
                        UserPoolId = userPoolId,
                    });
                    if (resp.Groups == null)
                        throw new Exception("Groups is null");
                    if (resp.Groups.Count < 1)
                        throw new Exception("Expected at least 1 group");
                }));

                // Test 12: AdminCreateUser
                results.Add(await runner.RunTestAsync("cognito", "AdminCreateUser", async () =>
                {
                    var resp = await cognitoClient.AdminCreateUserAsync(new AdminCreateUserRequest
                    {
                        UserPoolId = userPoolId,
                        Username = userName,
                        TemporaryPassword = "TempPass123!",
                        MessageAction = MessageActionType.SUPPRESS,
                    });
                    if (resp.User == null)
                        throw new Exception("User is null");
                    if (resp.User.UserStatus != UserStatusType.FORCE_CHANGE_PASSWORD)
                        throw new Exception($"Expected FORCE_CHANGE_PASSWORD, got {resp.User.UserStatus}");
                }));

                // Test 13: AdminGetUser
                results.Add(await runner.RunTestAsync("cognito", "AdminGetUser", async () =>
                {
                    var resp = await cognitoClient.AdminGetUserAsync(new AdminGetUserRequest
                    {
                        UserPoolId = userPoolId,
                        Username = userName,
                    });
                    if (resp.Username != userName)
                        throw new Exception("Username mismatch");
                }));

                // Test 14: ListUsers
                results.Add(await runner.RunTestAsync("cognito", "ListUsers", async () =>
                {
                    var resp = await cognitoClient.ListUsersAsync(new ListUsersRequest
                    {
                        UserPoolId = userPoolId,
                    });
                    if (resp.Users == null)
                        throw new Exception("Users is null");
                    if (resp.Users.Count < 1)
                        throw new Exception("Expected at least 1 user");
                }));

                // Test 15: CreateResourceServer
                results.Add(await runner.RunTestAsync("cognito", "CreateResourceServer", async () =>
                {
                    var resp = await cognitoClient.CreateResourceServerAsync(new CreateResourceServerRequest
                    {
                        UserPoolId = userPoolId,
                        Identifier = resourceServerId,
                        Name = "Test Resource Server",
                    });
                    if (resp.ResourceServer == null)
                        throw new Exception("ResourceServer is null");
                }));

                // Test 16: ListResourceServers
                results.Add(await runner.RunTestAsync("cognito", "ListResourceServers", async () =>
                {
                    var resp = await cognitoClient.ListResourceServersAsync(new ListResourceServersRequest
                    {
                        UserPoolId = userPoolId,
                    });
                    if (resp.ResourceServers == null)
                        throw new Exception("ResourceServers is null");
                    if (resp.ResourceServers.Count < 1)
                        throw new Exception("Expected at least 1 resource server");
                }));

                // Test 17: UpdateUserPool
                results.Add(await runner.RunTestAsync("cognito", "UpdateUserPool", async () =>
                {
                    await cognitoClient.UpdateUserPoolAsync(new UpdateUserPoolRequest
                    {
                        UserPoolId = userPoolId,
                        Policies = new UserPoolPolicyType
                        {
                            PasswordPolicy = new PasswordPolicyType
                            {
                                MinimumLength = 10,
                                RequireUppercase = true,
                                RequireLowercase = true,
                                RequireNumbers = true,
                                RequireSymbols = true,
                            },
                        },
                    });
                }));

                // Test 18: CreateIdentityProvider
                results.Add(await runner.RunTestAsync("cognito", "CreateIdentityProvider", async () =>
                {
                    var resp = await cognitoClient.CreateIdentityProviderAsync(new CreateIdentityProviderRequest
                    {
                        UserPoolId = userPoolId,
                        ProviderName = "TestProvider",
                        ProviderType = "Facebook",
                        ProviderDetails = new Dictionary<string, string>
                        {
                            { "client_id", "test-client-id" },
                            { "client_secret", "test-client-secret" },
                            { "authorize_scopes", "public_profile,email" },
                        },
                    });
                    if (resp.IdentityProvider == null)
                        throw new Exception("IdentityProvider is null");
                }));

                // Test 19: ListIdentityProviders
                results.Add(await runner.RunTestAsync("cognito", "ListIdentityProviders", async () =>
                {
                    var resp = await cognitoClient.ListIdentityProvidersAsync(new ListIdentityProvidersRequest
                    {
                        UserPoolId = userPoolId,
                    });
                    if (resp.Providers == null)
                        throw new Exception("Providers is null");
                    if (resp.Providers.Count < 1)
                        throw new Exception("Expected at least 1 identity provider");
                }));

                // Test 20: SetUserPoolMfaConfig
                results.Add(await runner.RunTestAsync("cognito", "SetUserPoolMfaConfig", async () =>
                {
                    await cognitoClient.SetUserPoolMfaConfigAsync(new SetUserPoolMfaConfigRequest
                    {
                        UserPoolId = userPoolId,
                        SmsMfaConfiguration = new SmsMfaConfigType
                        {
                            SmsConfiguration = new SmsConfigurationType
                            {
                                SnsCallerArn = "arn:aws:sns:us-east-1:123456789012:sms-topic",
                                ExternalId = "external-id",
                            },
                        },
                    });
                }));

                // Test 21: GetUserPoolMfaConfig
                results.Add(await runner.RunTestAsync("cognito", "GetUserPoolMfaConfig", async () =>
                {
                    var resp = await cognitoClient.GetUserPoolMfaConfigAsync(new GetUserPoolMfaConfigRequest
                    {
                        UserPoolId = userPoolId,
                    });
                    if (resp == null)
                        throw new Exception("Response is null");
                }));

                // Test 22: AdminDisableUser
                results.Add(await runner.RunTestAsync("cognito", "AdminDisableUser", async () =>
                {
                    await cognitoClient.AdminDisableUserAsync(new AdminDisableUserRequest
                    {
                        UserPoolId = userPoolId,
                        Username = userName,
                    });
                }));

                // Test 23: AdminEnableUser
                results.Add(await runner.RunTestAsync("cognito", "AdminEnableUser", async () =>
                {
                    await cognitoClient.AdminEnableUserAsync(new AdminEnableUserRequest
                    {
                        UserPoolId = userPoolId,
                        Username = userName,
                    });
                }));

                // Test 24: AdminDeleteUser
                results.Add(await runner.RunTestAsync("cognito", "AdminDeleteUser", async () =>
                {
                    await cognitoClient.AdminDeleteUserAsync(new AdminDeleteUserRequest
                    {
                        UserPoolId = userPoolId,
                        Username = userName,
                    });
                }));

                // Test 25: DeleteUserPoolDomain
                results.Add(await runner.RunTestAsync("cognito", "DeleteUserPoolDomain", async () =>
                {
                    await cognitoClient.DeleteUserPoolDomainAsync(new DeleteUserPoolDomainRequest
                    {
                        Domain = domainName,
                        UserPoolId = userPoolId,
                    });
                }));

                // Test 26: DeleteUserPoolClient
                if (!string.IsNullOrEmpty(clientId))
                {
                    results.Add(await runner.RunTestAsync("cognito", "DeleteUserPoolClient", async () =>
                    {
                        await cognitoClient.DeleteUserPoolClientAsync(new DeleteUserPoolClientRequest
                        {
                            ClientId = clientId,
                            UserPoolId = userPoolId,
                        });
                    }));
                }

                // Test 27: DeleteGroup
                results.Add(await runner.RunTestAsync("cognito", "DeleteGroup", async () =>
                {
                    await cognitoClient.DeleteGroupAsync(new DeleteGroupRequest
                    {
                        GroupName = groupName,
                        UserPoolId = userPoolId,
                    });
                }));

                // Test 28: GetCSVHeader
                results.Add(await runner.RunTestAsync("cognito", "GetCSVHeader", async () =>
                {
                    var resp = await cognitoClient.GetCSVHeaderAsync(new GetCSVHeaderRequest
                    {
                        UserPoolId = userPoolId,
                    });
                    if (resp.CSVHeader == null || resp.CSVHeader.Count == 0)
                        throw new Exception("CSVHeader is null or empty");
                }));

                // Test 29: DescribeRiskConfiguration
                results.Add(await runner.RunTestAsync("cognito", "DescribeRiskConfiguration", async () =>
                {
                    var resp = await cognitoClient.DescribeRiskConfigurationAsync(new DescribeRiskConfigurationRequest
                    {
                        UserPoolId = userPoolId,
                    });
                    if (resp == null)
                        throw new Exception("Response is null");
                }));

                // Test 30: DeleteUserPool
                results.Add(await runner.RunTestAsync("cognito", "DeleteUserPool", async () =>
                {
                    await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest
                    {
                        UserPoolId = userPoolId,
                    });
                }));

                userPoolId = "";

                // Test 31: TagResource
                results.Add(await runner.RunTestAsync("cognito", "TagResource", async () =>
                {
                    var tagPoolName = TestRunner.MakeUniqueName("CSPoolTag");
                    var createResp = await cognitoClient.CreateUserPoolAsync(new CreateUserPoolRequest
                    {
                        PoolName = tagPoolName,
                    });
                    try
                    {
                        await cognitoClient.TagResourceAsync(new TagResourceRequest
                        {
                            ResourceArn = createResp.UserPool.Arn,
                            Tags = new Dictionary<string, string>
                            {
                                { "Environment", "test" },
                                { "Owner", "test-user" },
                            },
                        });
                    }
                    finally
                    {
                        try { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); } catch { }
                    }
                }));

                // Test 32: ListTagsForResource
                results.Add(await runner.RunTestAsync("cognito", "ListTagsForResource", async () =>
                {
                    var tagPoolName = TestRunner.MakeUniqueName("CSPoolListTags");
                    var createResp = await cognitoClient.CreateUserPoolAsync(new CreateUserPoolRequest
                    {
                        PoolName = tagPoolName,
                    });
                    try
                    {
                        await cognitoClient.TagResourceAsync(new TagResourceRequest
                        {
                            ResourceArn = createResp.UserPool.Arn,
                            Tags = new Dictionary<string, string>
                            {
                                { "Test", "value" },
                            },
                        });
                        var listResp = await cognitoClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                        {
                            ResourceArn = createResp.UserPool.Arn,
                        });
                        if (listResp.Tags == null || !listResp.Tags.ContainsKey("Test") || listResp.Tags["Test"] != "value")
                            throw new Exception("Tag not found or value mismatch");
                    }
                    finally
                    {
                        try { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); } catch { }
                    }
                }));

                // Test 33: UntagResource
                results.Add(await runner.RunTestAsync("cognito", "UntagResource", async () =>
                {
                    var tagPoolName = TestRunner.MakeUniqueName("CSPoolUntag");
                    var createResp = await cognitoClient.CreateUserPoolAsync(new CreateUserPoolRequest
                    {
                        PoolName = tagPoolName,
                    });
                    try
                    {
                        await cognitoClient.TagResourceAsync(new TagResourceRequest
                        {
                            ResourceArn = createResp.UserPool.Arn,
                            Tags = new Dictionary<string, string>
                            {
                                { "Test", "value" },
                            },
                        });
                        await cognitoClient.UntagResourceAsync(new UntagResourceRequest
                        {
                            ResourceArn = createResp.UserPool.Arn,
                            TagKeys = new List<string> { "Test" },
                        });
                        var listResp = await cognitoClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                        {
                            ResourceArn = createResp.UserPool.Arn,
                        });
                        if (listResp.Tags != null && listResp.Tags.ContainsKey("Test"))
                            throw new Exception("Tag should have been removed");
                    }
                    finally
                    {
                        try { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); } catch { }
                    }
                }));

                // Test 34: GlobalSignOut
                results.Add(await runner.RunTestAsync("cognito", "GlobalSignOut", async () =>
                {
                    try
                    {
                        await cognitoClient.GlobalSignOutAsync(new GlobalSignOutRequest
                        {
                            AccessToken = "dummy-token",
                        });
                        throw new Exception("expected NotAuthorizedException");
                    }
                    catch (NotAuthorizedException)
                    {
                    }
                }));
            }

            // Test 35: DescribeUserPool_NonExistent
            results.Add(await runner.RunTestAsync("cognito", "DescribeUserPool_NonExistent", async () =>
            {
                try
                {
                    await cognitoClient.DescribeUserPoolAsync(new DescribeUserPoolRequest
                    {
                        UserPoolId = "us-east-1_nonexistentpool",
                    });
                    throw new Exception("expected ResourceNotFoundException");
                }
                catch (ResourceNotFoundException)
                {
                }
            }));

            // Test 36: DeleteUserPool_NonExistent
            results.Add(await runner.RunTestAsync("cognito", "DeleteUserPool_NonExistent", async () =>
            {
                try
                {
                    await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest
                    {
                        UserPoolId = "us-east-1_nonexistentpool",
                    });
                    throw new Exception("expected ResourceNotFoundException");
                }
                catch (ResourceNotFoundException)
                {
                }
            }));

            // Test 37: AdminGetUser_NonExistent
            results.Add(await runner.RunTestAsync("cognito", "AdminGetUser_NonExistent", async () =>
            {
                var errPoolName = TestRunner.MakeUniqueName("CSErrPool");
                var createResp = await cognitoClient.CreateUserPoolAsync(new CreateUserPoolRequest
                {
                    PoolName = errPoolName,
                });
                try
                {
                    try
                    {
                        await cognitoClient.AdminGetUserAsync(new AdminGetUserRequest
                        {
                            UserPoolId = createResp.UserPool.Id,
                            Username = "nonexistent-user-xyz",
                        });
                        throw new Exception("expected error for non-existent user");
                    }
                    catch (UserNotFoundException)
                    {
                    }
                }
                finally
                {
                    try { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); } catch { }
                }
            }));

            // Test 38: CreateUserPool_DuplicateName
            results.Add(await runner.RunTestAsync("cognito", "CreateUserPool_DuplicateName", async () =>
            {
                var dupPoolName = TestRunner.MakeUniqueName("CSDupPool");
                var resp1 = await cognitoClient.CreateUserPoolAsync(new CreateUserPoolRequest
                {
                    PoolName = dupPoolName,
                });
                try
                {
                    var resp2 = await cognitoClient.CreateUserPoolAsync(new CreateUserPoolRequest
                    {
                        PoolName = dupPoolName,
                    });
                    try
                    {
                        if (resp2.UserPool.Id == resp1.UserPool.Id)
                            throw new Exception("duplicate pool should have different ID");
                    }
                    finally
                    {
                        try { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = resp2.UserPool.Id }); } catch { }
                    }
                }
                finally
                {
                    try { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = resp1.UserPool.Id }); } catch { }
                }
            }));

            // Test 39: AdminCreateUser_VerifyAttributes
            results.Add(await runner.RunTestAsync("cognito", "AdminCreateUser_VerifyAttributes", async () =>
            {
                var attrPoolName = TestRunner.MakeUniqueName("CSAttrPool");
                var createResp = await cognitoClient.CreateUserPoolAsync(new CreateUserPoolRequest
                {
                    PoolName = attrPoolName,
                });
                try
                {
                    var attrUser = TestRunner.MakeUniqueName("CSAttrUser");
                    var createUserResp = await cognitoClient.AdminCreateUserAsync(new AdminCreateUserRequest
                    {
                        UserPoolId = createResp.UserPool.Id,
                        Username = attrUser,
                        TemporaryPassword = "TempPass123!",
                        MessageAction = MessageActionType.SUPPRESS,
                        UserAttributes = new List<AttributeType>
                        {
                            new AttributeType { Name = "email", Value = "test@example.com" },
                            new AttributeType { Name = "name", Value = "Test User" },
                        },
                    });
                    if (createUserResp.User == null)
                        throw new Exception("User is null");
                    if (createUserResp.User.Username != attrUser)
                        throw new Exception("Username mismatch");
                    if (createUserResp.User.Enabled != true)
                        throw new Exception("User should be enabled");
                }
                finally
                {
                    try { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); } catch { }
                }
            }));

            // Test 40: ListUsers_ContainsCreated
            results.Add(await runner.RunTestAsync("cognito", "ListUsers_ContainsCreated", async () =>
            {
                var listPoolName = TestRunner.MakeUniqueName("CSListPool");
                var createResp = await cognitoClient.CreateUserPoolAsync(new CreateUserPoolRequest
                {
                    PoolName = listPoolName,
                });
                try
                {
                    var listUser = TestRunner.MakeUniqueName("CSListUser");
                    await cognitoClient.AdminCreateUserAsync(new AdminCreateUserRequest
                    {
                        UserPoolId = createResp.UserPool.Id,
                        Username = listUser,
                        TemporaryPassword = "TempPass123!",
                        MessageAction = MessageActionType.SUPPRESS,
                    });
                    var listResp = await cognitoClient.ListUsersAsync(new ListUsersRequest
                    {
                        UserPoolId = createResp.UserPool.Id,
                    });
                    var found = listResp.Users.Any(u => u.Username == listUser);
                    if (!found)
                        throw new Exception("created user not found in ListUsers");
                }
                finally
                {
                    try { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); } catch { }
                }
            }));

            // Test 41: ListGroups_ContainsCreated
            results.Add(await runner.RunTestAsync("cognito", "ListGroups_ContainsCreated", async () =>
            {
                var grpPoolName = TestRunner.MakeUniqueName("CSGrpPool");
                var createResp = await cognitoClient.CreateUserPoolAsync(new CreateUserPoolRequest
                {
                    PoolName = grpPoolName,
                });
                try
                {
                    var testGroup = TestRunner.MakeUniqueName("CSTestGrp");
                    await cognitoClient.CreateGroupAsync(new CreateGroupRequest
                    {
                        GroupName = testGroup,
                        UserPoolId = createResp.UserPool.Id,
                        Description = "Test group description",
                    });
                    var listResp = await cognitoClient.ListGroupsAsync(new ListGroupsRequest
                    {
                        UserPoolId = createResp.UserPool.Id,
                    });
                    var group = listResp.Groups.FirstOrDefault(g => g.GroupName == testGroup);
                    if (group == null)
                        throw new Exception("created group not found in ListGroups");
                    if (group.Description != "Test group description")
                        throw new Exception("group description mismatch");
                }
                finally
                {
                    try { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); } catch { }
                }
            }));
        }
        finally
        {
            if (!string.IsNullOrEmpty(userPoolId))
            {
                try { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = userPoolId }); } catch { }
            }
        }

        return results;
    }
}
