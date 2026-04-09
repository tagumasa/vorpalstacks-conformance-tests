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
                results.Add(await runner.RunTestAsync("cognito", "DescribeUserPool", async () =>
                {
                    var resp = await cognitoClient.DescribeUserPoolAsync(new DescribeUserPoolRequest
                    {
                        UserPoolId = userPoolId
                    });
                    if (resp.UserPool == null)
                        throw new Exception("UserPool is null");
                }));

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

                results.Add(await runner.RunTestAsync("cognito", "CreateUserPoolDomain", async () =>
                {
                    await cognitoClient.CreateUserPoolDomainAsync(new CreateUserPoolDomainRequest
                    {
                        Domain = domainName,
                        UserPoolId = userPoolId,
                    });
                }));

                results.Add(await runner.RunTestAsync("cognito", "DescribeUserPoolDomain", async () =>
                {
                    var resp = await cognitoClient.DescribeUserPoolDomainAsync(new DescribeUserPoolDomainRequest
                    {
                        Domain = domainName,
                    });
                    if (resp.DomainDescription == null)
                        throw new Exception("DomainDescription is null");
                }));

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

                results.Add(await runner.RunTestAsync("cognito", "GetUserPoolMfaConfig", async () =>
                {
                    var resp = await cognitoClient.GetUserPoolMfaConfigAsync(new GetUserPoolMfaConfigRequest
                    {
                        UserPoolId = userPoolId,
                    });
                    if (resp == null)
                        throw new Exception("Response is null");
                }));

                results.Add(await runner.RunTestAsync("cognito", "AdminDisableUser", async () =>
                {
                    await cognitoClient.AdminDisableUserAsync(new AdminDisableUserRequest
                    {
                        UserPoolId = userPoolId,
                        Username = userName,
                    });
                }));

                results.Add(await runner.RunTestAsync("cognito", "AdminEnableUser", async () =>
                {
                    await cognitoClient.AdminEnableUserAsync(new AdminEnableUserRequest
                    {
                        UserPoolId = userPoolId,
                        Username = userName,
                    });
                }));

                results.Add(await runner.RunTestAsync("cognito", "AdminDeleteUser", async () =>
                {
                    await cognitoClient.AdminDeleteUserAsync(new AdminDeleteUserRequest
                    {
                        UserPoolId = userPoolId,
                        Username = userName,
                    });
                }));

                results.Add(await runner.RunTestAsync("cognito", "DeleteUserPoolDomain", async () =>
                {
                    await cognitoClient.DeleteUserPoolDomainAsync(new DeleteUserPoolDomainRequest
                    {
                        Domain = domainName,
                        UserPoolId = userPoolId,
                    });
                }));

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

                results.Add(await runner.RunTestAsync("cognito", "DeleteGroup", async () =>
                {
                    await cognitoClient.DeleteGroupAsync(new DeleteGroupRequest
                    {
                        GroupName = groupName,
                        UserPoolId = userPoolId,
                    });
                }));

                results.Add(await runner.RunTestAsync("cognito", "GetCSVHeader", async () =>
                {
                    var resp = await cognitoClient.GetCSVHeaderAsync(new GetCSVHeaderRequest
                    {
                        UserPoolId = userPoolId,
                    });
                    if (resp.CSVHeader == null || resp.CSVHeader.Count == 0)
                        throw new Exception("CSVHeader is null or empty");
                }));

                results.Add(await runner.RunTestAsync("cognito", "DescribeRiskConfiguration", async () =>
                {
                    var resp = await cognitoClient.DescribeRiskConfigurationAsync(new DescribeRiskConfigurationRequest
                    {
                        UserPoolId = userPoolId,
                    });
                    if (resp == null)
                        throw new Exception("Response is null");
                }));

                results.Add(await runner.RunTestAsync("cognito", "DescribeIdentityProvider", async () =>
                {
                    var resp = await cognitoClient.DescribeIdentityProviderAsync(new DescribeIdentityProviderRequest
                    {
                        UserPoolId = userPoolId,
                        ProviderName = "TestProvider",
                    });
                    if (resp.IdentityProvider == null)
                        throw new Exception("IdentityProvider is null");
                    if (resp.IdentityProvider.ProviderName != "TestProvider")
                        throw new Exception($"ProviderName mismatch: got {resp.IdentityProvider.ProviderName}");
                    if (resp.IdentityProvider.ProviderType != "Facebook")
                        throw new Exception($"ProviderType mismatch: got {resp.IdentityProvider.ProviderType}");
                }));

                results.Add(await runner.RunTestAsync("cognito", "UpdateIdentityProvider", async () =>
                {
                    await cognitoClient.UpdateIdentityProviderAsync(new UpdateIdentityProviderRequest
                    {
                        UserPoolId = userPoolId,
                        ProviderName = "TestProvider",
                        ProviderDetails = new Dictionary<string, string>
                        {
                            { "updated_key", "updated_value" },
                        },
                    });
                    var descResp = await cognitoClient.DescribeIdentityProviderAsync(new DescribeIdentityProviderRequest
                    {
                        UserPoolId = userPoolId,
                        ProviderName = "TestProvider",
                    });
                    if (descResp.IdentityProvider.ProviderDetails == null)
                        throw new Exception("ProviderDetails is null after update");
                    if (descResp.IdentityProvider.ProviderDetails.TryGetValue("updated_key", out var val) && val != "updated_value")
                        throw new Exception("ProviderDetails not updated");
                }));

                results.Add(await runner.RunTestAsync("cognito", "DeleteIdentityProvider", async () =>
                {
                    var delProvider = TestRunner.MakeUniqueName("CSDelProvider");
                    await cognitoClient.CreateIdentityProviderAsync(new CreateIdentityProviderRequest
                    {
                        UserPoolId = userPoolId,
                        ProviderName = delProvider,
                        ProviderType = "Google",
                        ProviderDetails = new Dictionary<string, string>
                        {
                            { "client_id", "test" },
                        },
                    });
                    await cognitoClient.DeleteIdentityProviderAsync(new DeleteIdentityProviderRequest
                    {
                        UserPoolId = userPoolId,
                        ProviderName = delProvider,
                    });
                    try
                    {
                        await cognitoClient.DescribeIdentityProviderAsync(new DescribeIdentityProviderRequest
                        {
                            UserPoolId = userPoolId,
                            ProviderName = delProvider,
                        });
                        throw new Exception("expected ResourceNotFoundException after delete");
                    }
                    catch (ResourceNotFoundException)
                    {
                    }
                }));

                results.Add(await runner.RunTestAsync("cognito", "DescribeResourceServer", async () =>
                {
                    var resp = await cognitoClient.DescribeResourceServerAsync(new DescribeResourceServerRequest
                    {
                        UserPoolId = userPoolId,
                        Identifier = resourceServerId,
                    });
                    if (resp.ResourceServer == null)
                        throw new Exception("ResourceServer is null");
                    if (resp.ResourceServer.Identifier != resourceServerId)
                        throw new Exception($"Identifier mismatch: got {resp.ResourceServer.Identifier}");
                    if (resp.ResourceServer.Name != "Test Resource Server")
                        throw new Exception($"Name mismatch: got {resp.ResourceServer.Name}");
                }));

                results.Add(await runner.RunTestAsync("cognito", "UpdateResourceServer", async () =>
                {
                    var resp = await cognitoClient.UpdateResourceServerAsync(new UpdateResourceServerRequest
                    {
                        UserPoolId = userPoolId,
                        Identifier = resourceServerId,
                        Name = "Updated Resource Server",
                        Scopes = new List<ResourceServerScopeType>
                        {
                            new ResourceServerScopeType { ScopeName = "read", ScopeDescription = "Read access" },
                        },
                    });
                    if (resp.ResourceServer == null)
                        throw new Exception("ResourceServer is null");
                    if (resp.ResourceServer.Name != "Updated Resource Server")
                        throw new Exception($"Name not updated: got {resp.ResourceServer.Name}");
                }));

                results.Add(await runner.RunTestAsync("cognito", "DeleteResourceServer", async () =>
                {
                    var delRs = TestRunner.MakeUniqueName("CSDelRS");
                    await cognitoClient.CreateResourceServerAsync(new CreateResourceServerRequest
                    {
                        UserPoolId = userPoolId,
                        Identifier = delRs,
                        Name = "Deletable RS",
                    });
                    await cognitoClient.DeleteResourceServerAsync(new DeleteResourceServerRequest
                    {
                        UserPoolId = userPoolId,
                        Identifier = delRs,
                    });
                    try
                    {
                        await cognitoClient.DescribeResourceServerAsync(new DescribeResourceServerRequest
                        {
                            UserPoolId = userPoolId,
                            Identifier = delRs,
                        });
                        throw new Exception("expected ResourceNotFoundException after delete");
                    }
                    catch (ResourceNotFoundException)
                    {
                    }
                }));

                results.Add(await runner.RunTestAsync("cognito", "UpdateUserPoolDomain", async () =>
                {
                    var udDomain = TestRunner.MakeUniqueName("CSUDDomain");
                    await cognitoClient.CreateUserPoolDomainAsync(new CreateUserPoolDomainRequest
                    {
                        Domain = udDomain,
                        UserPoolId = userPoolId,
                    });
                    try
                    {
                        var resp = await cognitoClient.UpdateUserPoolDomainAsync(new UpdateUserPoolDomainRequest
                        {
                            Domain = udDomain,
                            UserPoolId = userPoolId,
                        });
                        if (string.IsNullOrEmpty(resp.CloudFrontDomain))
                            throw new Exception("CloudFrontDomain is null or empty");
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolDomainAsync(new DeleteUserPoolDomainRequest { Domain = udDomain, UserPoolId = userPoolId }); });
                    }
                }));

                results.Add(await runner.RunTestAsync("cognito", "GetGroup", async () =>
                {
                    var getGroupName = TestRunner.MakeUniqueName("CSGetGroup");
                    await cognitoClient.CreateGroupAsync(new CreateGroupRequest
                    {
                        GroupName = getGroupName,
                        UserPoolId = userPoolId,
                    });
                    try
                    {
                        var resp = await cognitoClient.GetGroupAsync(new GetGroupRequest
                        {
                            GroupName = getGroupName,
                            UserPoolId = userPoolId,
                        });
                        if (resp.Group == null)
                            throw new Exception("Group is null");
                        if (resp.Group.GroupName != getGroupName)
                            throw new Exception($"GroupName mismatch: got {resp.Group.GroupName}");
                        if (resp.Group.UserPoolId != userPoolId)
                            throw new Exception($"UserPoolId mismatch: got {resp.Group.UserPoolId}");
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteGroupAsync(new DeleteGroupRequest { GroupName = getGroupName, UserPoolId = userPoolId }); });
                    }
                }));

                results.Add(await runner.RunTestAsync("cognito", "UpdateGroup", async () =>
                {
                    var ugGroupName = TestRunner.MakeUniqueName("CSUGGroup");
                    await cognitoClient.CreateGroupAsync(new CreateGroupRequest
                    {
                        GroupName = ugGroupName,
                        UserPoolId = userPoolId,
                        Description = "Original description",
                    });
                    try
                    {
                        await cognitoClient.UpdateGroupAsync(new UpdateGroupRequest
                        {
                            GroupName = ugGroupName,
                            UserPoolId = userPoolId,
                            Description = "Updated description",
                            Precedence = 10,
                        });
                        var resp = await cognitoClient.GetGroupAsync(new GetGroupRequest
                        {
                            GroupName = ugGroupName,
                            UserPoolId = userPoolId,
                        });
                        if (resp.Group.Description != "Updated description")
                            throw new Exception($"Description not updated: got {resp.Group.Description}");
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteGroupAsync(new DeleteGroupRequest { GroupName = ugGroupName, UserPoolId = userPoolId }); });
                    }
                }));

                results.Add(await runner.RunTestAsync("cognito", "AdminUpdateUserAttributes", async () =>
                {
                    var attrUser2 = TestRunner.MakeUniqueName("CSAttrUser2");
                    await cognitoClient.AdminCreateUserAsync(new AdminCreateUserRequest
                    {
                        UserPoolId = userPoolId,
                        Username = attrUser2,
                        TemporaryPassword = "TempPass123!",
                        MessageAction = MessageActionType.SUPPRESS,
                    });
                    try
                    {
                        await cognitoClient.AdminUpdateUserAttributesAsync(new AdminUpdateUserAttributesRequest
                        {
                            UserPoolId = userPoolId,
                            Username = attrUser2,
                            UserAttributes = new List<AttributeType>
                            {
                                new AttributeType { Name = "email", Value = "updated@example.com" },
                                new AttributeType { Name = "phone_number", Value = "+441234567890" },
                            },
                        });
                        var getResp = await cognitoClient.AdminGetUserAsync(new AdminGetUserRequest
                        {
                            UserPoolId = userPoolId,
                            Username = attrUser2,
                        });
                        var found = getResp.UserAttributes.Any(a => a.Name == "email" && a.Value == "updated@example.com");
                        if (!found)
                            throw new Exception("updated email attribute not found");
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.AdminDeleteUserAsync(new AdminDeleteUserRequest { UserPoolId = userPoolId, Username = attrUser2 }); });
                    }
                }));

                results.Add(await runner.RunTestAsync("cognito", "AdminDeleteUserAttributes", async () =>
                {
                    var daUser = TestRunner.MakeUniqueName("CSDAUser");
                    await cognitoClient.AdminCreateUserAsync(new AdminCreateUserRequest
                    {
                        UserPoolId = userPoolId,
                        Username = daUser,
                        TemporaryPassword = "TempPass123!",
                        MessageAction = MessageActionType.SUPPRESS,
                        UserAttributes = new List<AttributeType>
                        {
                            new AttributeType { Name = "email", Value = "da@example.com" },
                            new AttributeType { Name = "name", Value = "DA User" },
                        },
                    });
                    try
                    {
                        await cognitoClient.AdminDeleteUserAttributesAsync(new AdminDeleteUserAttributesRequest
                        {
                            UserPoolId = userPoolId,
                            Username = daUser,
                            UserAttributeNames = new List<string> { "name" },
                        });
                        var getResp = await cognitoClient.AdminGetUserAsync(new AdminGetUserRequest
                        {
                            UserPoolId = userPoolId,
                            Username = daUser,
                        });
                        var hasName = getResp.UserAttributes.Any(a => a.Name == "name");
                        if (hasName)
                            throw new Exception("attribute 'name' should have been deleted");
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.AdminDeleteUserAsync(new AdminDeleteUserRequest { UserPoolId = userPoolId, Username = daUser }); });
                    }
                }));

                results.Add(await runner.RunTestAsync("cognito", "AdminResetUserPassword", async () =>
                {
                    var rpUser = TestRunner.MakeUniqueName("CSRPUser");
                    await cognitoClient.AdminCreateUserAsync(new AdminCreateUserRequest
                    {
                        UserPoolId = userPoolId,
                        Username = rpUser,
                        TemporaryPassword = "TempPass123!",
                        MessageAction = MessageActionType.SUPPRESS,
                    });
                    try
                    {
                        await cognitoClient.AdminSetUserPasswordAsync(new AdminSetUserPasswordRequest
                        {
                            UserPoolId = userPoolId,
                            Username = rpUser,
                            Password = "PermPass123!",
                            Permanent = true,
                        });
                        await cognitoClient.AdminResetUserPasswordAsync(new AdminResetUserPasswordRequest
                        {
                            UserPoolId = userPoolId,
                            Username = rpUser,
                        });
                        var getResp = await cognitoClient.AdminGetUserAsync(new AdminGetUserRequest
                        {
                            UserPoolId = userPoolId,
                            Username = rpUser,
                        });
                        if (getResp.UserStatus != UserStatusType.FORCE_CHANGE_PASSWORD && getResp.UserStatus != UserStatusType.RESET_REQUIRED)
                            throw new Exception($"expected FORCE_CHANGE_PASSWORD or RESET_REQUIRED after reset, got {getResp.UserStatus}");
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.AdminDeleteUserAsync(new AdminDeleteUserRequest { UserPoolId = userPoolId, Username = rpUser }); });
                    }
                }));

                results.Add(await runner.RunTestAsync("cognito", "AdminSetUserPassword", async () =>
                {
                    var spUser = TestRunner.MakeUniqueName("CSSPUser");
                    await cognitoClient.AdminCreateUserAsync(new AdminCreateUserRequest
                    {
                        UserPoolId = userPoolId,
                        Username = spUser,
                        TemporaryPassword = "TempPass123!",
                        MessageAction = MessageActionType.SUPPRESS,
                    });
                    try
                    {
                        await cognitoClient.AdminSetUserPasswordAsync(new AdminSetUserPasswordRequest
                        {
                            UserPoolId = userPoolId,
                            Username = spUser,
                            Password = "NewPermPass123!",
                            Permanent = true,
                        });
                        var getResp = await cognitoClient.AdminGetUserAsync(new AdminGetUserRequest
                        {
                            UserPoolId = userPoolId,
                            Username = spUser,
                        });
                        if (getResp.UserStatus != UserStatusType.CONFIRMED)
                            throw new Exception($"expected CONFIRMED after permanent password, got {getResp.UserStatus}");
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.AdminDeleteUserAsync(new AdminDeleteUserRequest { UserPoolId = userPoolId, Username = spUser }); });
                    }
                }));

                results.Add(await runner.RunTestAsync("cognito", "SignUp", async () =>
                {
                    var signUpClientName = TestRunner.MakeUniqueName("CSSignUpClient");
                    var signUpClientResp = await cognitoClient.CreateUserPoolClientAsync(new CreateUserPoolClientRequest
                    {
                        UserPoolId = userPoolId,
                        ClientName = signUpClientName,
                    });
                    var signUpClientId = signUpClientResp.UserPoolClient.ClientId;
                    try
                    {
                        var signUpUser = TestRunner.MakeUniqueName("CSSignUpUser");
                        var resp = await cognitoClient.SignUpAsync(new SignUpRequest
                        {
                            ClientId = signUpClientId,
                            Username = signUpUser,
                            Password = "SignUpPass123!",
                            UserAttributes = new List<AttributeType>
                            {
                                new AttributeType { Name = "email", Value = "signup@example.com" },
                            },
                        });
                        if (string.IsNullOrEmpty(resp.UserSub))
                            throw new Exception("UserSub is null or empty");
                        if (resp.UserConfirmed == true)
                            throw new Exception("expected UserConfirmed=false after SignUp");
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolClientAsync(new DeleteUserPoolClientRequest { ClientId = signUpClientId, UserPoolId = userPoolId }); });
                    }
                }));

                results.Add(await runner.RunTestAsync("cognito", "ConfirmSignUp", async () =>
                {
                    var confirmClientName = TestRunner.MakeUniqueName("CSConfirmClient");
                    var confirmClientResp = await cognitoClient.CreateUserPoolClientAsync(new CreateUserPoolClientRequest
                    {
                        UserPoolId = userPoolId,
                        ClientName = confirmClientName,
                    });
                    var confirmClientId = confirmClientResp.UserPoolClient.ClientId;
                    try
                    {
                        var confirmUser = TestRunner.MakeUniqueName("CSConfirmUser");
                        await cognitoClient.SignUpAsync(new SignUpRequest
                        {
                            ClientId = confirmClientId,
                            Username = confirmUser,
                            Password = "ConfirmPass123!",
                        });
                        await cognitoClient.ConfirmSignUpAsync(new ConfirmSignUpRequest
                        {
                            ClientId = confirmClientId,
                            Username = confirmUser,
                            ConfirmationCode = "123456",
                        });
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolClientAsync(new DeleteUserPoolClientRequest { ClientId = confirmClientId, UserPoolId = userPoolId }); });
                    }
                }));

                results.Add(await runner.RunTestAsync("cognito", "AdminInitiateAuth", async () =>
                {
                    var authClientName = TestRunner.MakeUniqueName("CSAuthClient");
                    var authClientResp = await cognitoClient.CreateUserPoolClientAsync(new CreateUserPoolClientRequest
                    {
                        UserPoolId = userPoolId,
                        ClientName = authClientName,
                    });
                    var authClientId = authClientResp.UserPoolClient.ClientId;
                    try
                    {
                        var authUser = TestRunner.MakeUniqueName("CSAuthUser");
                        await cognitoClient.AdminCreateUserAsync(new AdminCreateUserRequest
                        {
                            UserPoolId = userPoolId,
                            Username = authUser,
                            TemporaryPassword = "TempPass123!",
                            MessageAction = MessageActionType.SUPPRESS,
                        });
                        try
                        {
                            await cognitoClient.AdminSetUserPasswordAsync(new AdminSetUserPasswordRequest
                            {
                                UserPoolId = userPoolId,
                                Username = authUser,
                                Password = "AuthPass123!",
                                Permanent = true,
                            });
                            var authResp = await cognitoClient.AdminInitiateAuthAsync(new AdminInitiateAuthRequest
                            {
                                UserPoolId = userPoolId,
                                ClientId = authClientId,
                                AuthFlow = AuthFlowType.ADMIN_NO_SRP_AUTH,
                                AuthParameters = new Dictionary<string, string>
                                {
                                    { "USERNAME", authUser },
                                    { "PASSWORD", "AuthPass123!" },
                                },
                            });
                            if (authResp.AuthenticationResult == null)
                                throw new Exception("AuthenticationResult is null");
                            if (string.IsNullOrEmpty(authResp.AuthenticationResult.AccessToken))
                                throw new Exception("AccessToken is null or empty");
                            if (string.IsNullOrEmpty(authResp.AuthenticationResult.IdToken))
                                throw new Exception("IdToken is null or empty");
                        }
                        finally
                        {
                            await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.AdminDeleteUserAsync(new AdminDeleteUserRequest { UserPoolId = userPoolId, Username = authUser }); });
                        }
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolClientAsync(new DeleteUserPoolClientRequest { ClientId = authClientId, UserPoolId = userPoolId }); });
                    }
                }));

                results.Add(await runner.RunTestAsync("cognito", "AdminAddUserToGroup", async () =>
                {
                    var ugUser = TestRunner.MakeUniqueName("CSUGUser");
                    var ugGroup = TestRunner.MakeUniqueName("CSUGGroup");
                    await cognitoClient.AdminCreateUserAsync(new AdminCreateUserRequest
                    {
                        UserPoolId = userPoolId,
                        Username = ugUser,
                        TemporaryPassword = "TempPass123!",
                        MessageAction = MessageActionType.SUPPRESS,
                    });
                    try
                    {
                        await cognitoClient.CreateGroupAsync(new CreateGroupRequest
                        {
                            GroupName = ugGroup,
                            UserPoolId = userPoolId,
                        });
                        try
                        {
                            await cognitoClient.AdminAddUserToGroupAsync(new AdminAddUserToGroupRequest
                            {
                                UserPoolId = userPoolId,
                                GroupName = ugGroup,
                                Username = ugUser,
                            });
                        }
                        finally
                        {
                            await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteGroupAsync(new DeleteGroupRequest { GroupName = ugGroup, UserPoolId = userPoolId }); });
                        }
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.AdminDeleteUserAsync(new AdminDeleteUserRequest { UserPoolId = userPoolId, Username = ugUser }); });
                    }
                }));

                results.Add(await runner.RunTestAsync("cognito", "ListUsersInGroup", async () =>
                {
                    var ug2User = TestRunner.MakeUniqueName("CSUG2User");
                    var ug2Group = TestRunner.MakeUniqueName("CSUG2Group");
                    await cognitoClient.AdminCreateUserAsync(new AdminCreateUserRequest
                    {
                        UserPoolId = userPoolId,
                        Username = ug2User,
                        TemporaryPassword = "TempPass123!",
                        MessageAction = MessageActionType.SUPPRESS,
                    });
                    try
                    {
                        await cognitoClient.CreateGroupAsync(new CreateGroupRequest
                        {
                            GroupName = ug2Group,
                            UserPoolId = userPoolId,
                        });
                        try
                        {
                            await cognitoClient.AdminAddUserToGroupAsync(new AdminAddUserToGroupRequest
                            {
                                UserPoolId = userPoolId,
                                GroupName = ug2Group,
                                Username = ug2User,
                            });
                            var listResp = await cognitoClient.ListUsersInGroupAsync(new ListUsersInGroupRequest
                            {
                                UserPoolId = userPoolId,
                                GroupName = ug2Group,
                            });
                            var found = listResp.Users.Any(u => u.Username == ug2User);
                            if (!found)
                                throw new Exception("user not found in ListUsersInGroup");
                            await cognitoClient.AdminRemoveUserFromGroupAsync(new AdminRemoveUserFromGroupRequest
                            {
                                UserPoolId = userPoolId,
                                GroupName = ug2Group,
                                Username = ug2User,
                            });
                            var listResp2 = await cognitoClient.ListUsersInGroupAsync(new ListUsersInGroupRequest
                            {
                                UserPoolId = userPoolId,
                                GroupName = ug2Group,
                            });
                            var stillFound = listResp2.Users.Any(u => u.Username == ug2User);
                            if (stillFound)
                                throw new Exception("user still in group after AdminRemoveUserFromGroup");
                        }
                        finally
                        {
                            await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteGroupAsync(new DeleteGroupRequest { GroupName = ug2Group, UserPoolId = userPoolId }); });
                        }
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.AdminDeleteUserAsync(new AdminDeleteUserRequest { UserPoolId = userPoolId, Username = ug2User }); });
                    }
                }));

                results.Add(await runner.RunTestAsync("cognito", "AdminListGroupsForUser", async () =>
                {
                    var lgUser = TestRunner.MakeUniqueName("CSLGUser");
                    var lgGroup1 = TestRunner.MakeUniqueName("CSLGGroup1");
                    var lgGroup2 = TestRunner.MakeUniqueName("CSLGGroup2");
                    await cognitoClient.AdminCreateUserAsync(new AdminCreateUserRequest
                    {
                        UserPoolId = userPoolId,
                        Username = lgUser,
                        TemporaryPassword = "TempPass123!",
                        MessageAction = MessageActionType.SUPPRESS,
                    });
                    try
                    {
                        await cognitoClient.CreateGroupAsync(new CreateGroupRequest
                        {
                            GroupName = lgGroup1,
                            UserPoolId = userPoolId,
                        });
                        try
                        {
                            await cognitoClient.CreateGroupAsync(new CreateGroupRequest
                            {
                                GroupName = lgGroup2,
                                UserPoolId = userPoolId,
                            });
                            try
                            {
                                await cognitoClient.AdminAddUserToGroupAsync(new AdminAddUserToGroupRequest
                                {
                                    UserPoolId = userPoolId,
                                    GroupName = lgGroup1,
                                    Username = lgUser,
                                });
                                await cognitoClient.AdminAddUserToGroupAsync(new AdminAddUserToGroupRequest
                                {
                                    UserPoolId = userPoolId,
                                    GroupName = lgGroup2,
                                    Username = lgUser,
                                });
                                var resp = await cognitoClient.AdminListGroupsForUserAsync(new AdminListGroupsForUserRequest
                                {
                                    UserPoolId = userPoolId,
                                    Username = lgUser,
                                });
                                if (resp.Groups.Count < 2)
                                    throw new Exception($"expected at least 2 groups, got {resp.Groups.Count}");
                            }
                            finally
                            {
                                await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteGroupAsync(new DeleteGroupRequest { GroupName = lgGroup2, UserPoolId = userPoolId }); });
                            }
                        }
                        finally
                        {
                            await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteGroupAsync(new DeleteGroupRequest { GroupName = lgGroup1, UserPoolId = userPoolId }); });
                        }
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.AdminDeleteUserAsync(new AdminDeleteUserRequest { UserPoolId = userPoolId, Username = lgUser }); });
                    }
                }));

                results.Add(await runner.RunTestAsync("cognito", "AdminUserGlobalSignOut", async () =>
                {
                    var gsoUser = TestRunner.MakeUniqueName("CSGSOUser");
                    await cognitoClient.AdminCreateUserAsync(new AdminCreateUserRequest
                    {
                        UserPoolId = userPoolId,
                        Username = gsoUser,
                        TemporaryPassword = "TempPass123!",
                        MessageAction = MessageActionType.SUPPRESS,
                    });
                    try
                    {
                        await cognitoClient.AdminSetUserPasswordAsync(new AdminSetUserPasswordRequest
                        {
                            UserPoolId = userPoolId,
                            Username = gsoUser,
                            Password = "GSOPass123!",
                            Permanent = true,
                        });
                        await cognitoClient.AdminUserGlobalSignOutAsync(new AdminUserGlobalSignOutRequest
                        {
                            UserPoolId = userPoolId,
                            Username = gsoUser,
                        });
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.AdminDeleteUserAsync(new AdminDeleteUserRequest { UserPoolId = userPoolId, Username = gsoUser }); });
                    }
                }));

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

                results.Add(await runner.RunTestAsync("cognito", "DeleteUserPool", async () =>
                {
                    await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest
                    {
                        UserPoolId = userPoolId,
                    });
                }));

                userPoolId = "";

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
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); });
                    }
                }));

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
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); });
                    }
                }));

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
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); });
                    }
                }));
            }

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
                    await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); });
                }
            }));

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
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = resp2.UserPool.Id }); });
                    }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = resp1.UserPool.Id }); });
                }
            }));

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
                    await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); });
                }
            }));

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
                    await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); });
                }
            }));

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
                    await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); });
                }
            }));

            results.Add(await runner.RunTestAsync("cognito", "GetGroup_NonExistent", async () =>
            {
                var nePoolName = TestRunner.MakeUniqueName("CSNEPool");
                var createResp = await cognitoClient.CreateUserPoolAsync(new CreateUserPoolRequest
                {
                    PoolName = nePoolName,
                });
                try
                {
                    try
                    {
                        await cognitoClient.GetGroupAsync(new GetGroupRequest
                        {
                            GroupName = "nonexistent-group-xyz",
                            UserPoolId = createResp.UserPool.Id,
                        });
                        throw new Exception("expected error for non-existent group");
                    }
                    catch (ResourceNotFoundException)
                    {
                    }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); });
                }
            }));

            results.Add(await runner.RunTestAsync("cognito", "DescribeIdentityProvider_NonExistent", async () =>
            {
                var nePoolName = TestRunner.MakeUniqueName("CSDIPPool");
                var createResp = await cognitoClient.CreateUserPoolAsync(new CreateUserPoolRequest
                {
                    PoolName = nePoolName,
                });
                try
                {
                    try
                    {
                        await cognitoClient.DescribeIdentityProviderAsync(new DescribeIdentityProviderRequest
                        {
                            UserPoolId = createResp.UserPool.Id,
                            ProviderName = "nonexistent-idp-xyz",
                        });
                        throw new Exception("expected error for non-existent identity provider");
                    }
                    catch (ResourceNotFoundException)
                    {
                    }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); });
                }
            }));

            results.Add(await runner.RunTestAsync("cognito", "DescribeResourceServer_NonExistent", async () =>
            {
                var nePoolName = TestRunner.MakeUniqueName("CSDRSPool");
                var createResp = await cognitoClient.CreateUserPoolAsync(new CreateUserPoolRequest
                {
                    PoolName = nePoolName,
                });
                try
                {
                    try
                    {
                        await cognitoClient.DescribeResourceServerAsync(new DescribeResourceServerRequest
                        {
                            UserPoolId = createResp.UserPool.Id,
                            Identifier = "nonexistent-rs-xyz",
                        });
                        throw new Exception("expected error for non-existent resource server");
                    }
                    catch (ResourceNotFoundException)
                    {
                    }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); });
                }
            }));

            results.Add(await runner.RunTestAsync("cognito", "DeleteIdentityProvider_NonExistent", async () =>
            {
                var nePoolName = TestRunner.MakeUniqueName("CSDLIPPool");
                var createResp = await cognitoClient.CreateUserPoolAsync(new CreateUserPoolRequest
                {
                    PoolName = nePoolName,
                });
                try
                {
                    try
                    {
                        await cognitoClient.DeleteIdentityProviderAsync(new DeleteIdentityProviderRequest
                        {
                            UserPoolId = createResp.UserPool.Id,
                            ProviderName = "nonexistent-idp-xyz",
                        });
                        throw new Exception("expected error for non-existent identity provider");
                    }
                    catch (ResourceNotFoundException)
                    {
                    }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); });
                }
            }));

            results.Add(await runner.RunTestAsync("cognito", "DeleteResourceServer_NonExistent", async () =>
            {
                var nePoolName = TestRunner.MakeUniqueName("CSDLRSPool");
                var createResp = await cognitoClient.CreateUserPoolAsync(new CreateUserPoolRequest
                {
                    PoolName = nePoolName,
                });
                try
                {
                    try
                    {
                        await cognitoClient.DeleteResourceServerAsync(new DeleteResourceServerRequest
                        {
                            UserPoolId = createResp.UserPool.Id,
                            Identifier = "nonexistent-rs-xyz",
                        });
                        throw new Exception("expected error for non-existent resource server");
                    }
                    catch (ResourceNotFoundException)
                    {
                    }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = createResp.UserPool.Id }); });
                }
            }));

            results.Add(await runner.RunTestAsync("cognito", "ListUserPools_Pagination", async () =>
            {
                var pgTs = DateTime.UtcNow.Ticks.ToString();
                var pgPoolIds = new List<string>();
                for (var i = 0; i < 5; i++)
                {
                    var name = $"CSPagPool-{pgTs}-{i}";
                    try
                    {
                        var createResp = await cognitoClient.CreateUserPoolAsync(new CreateUserPoolRequest
                        {
                            PoolName = name,
                        });
                        if (createResp.UserPool == null)
                            throw new Exception($"UserPool is null for {name}");
                        pgPoolIds.Add(createResp.UserPool.Id);
                    }
                    catch (Exception)
                    {
                        foreach (var pid in pgPoolIds)
                        {
                            await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = pid }); });
                        }
                        throw;
                    }
                }

                try
                {
                    var pageCount = 0;
                    string? nextToken = null;
                    while (true)
                    {
                        var resp = await cognitoClient.ListUserPoolsAsync(new ListUserPoolsRequest
                        {
                            MaxResults = 2,
                            NextToken = nextToken,
                        });
                        pageCount++;
                        if (!string.IsNullOrEmpty(resp.NextToken))
                        {
                            nextToken = resp.NextToken;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (pageCount < 2)
                        throw new Exception($"expected at least 2 pages with MaxResults=2, got {pageCount}");
                }
                finally
                {
                    foreach (var pid in pgPoolIds)
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = pid }); });
                    }
                }
            }));
        }
        finally
        {
            if (!string.IsNullOrEmpty(userPoolId))
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = userPoolId }); });
            }
        }

        return results;
    }
}
