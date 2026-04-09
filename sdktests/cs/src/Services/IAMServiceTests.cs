using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class IAMServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonIdentityManagementServiceClient iamClient,
        string region)
    {
        var results = new List<TestResult>();
        var userName = TestRunner.MakeUniqueName("CSUser");
        var groupName = TestRunner.MakeUniqueName("CSGroup");
        var roleName = TestRunner.MakeUniqueName("CSRole");
        var policyName = TestRunner.MakeUniqueName("CSPolicy");
        var instanceProfileName = TestRunner.MakeUniqueName("CSInstanceProfile");
        var accessKeyId = "";
        var updatedUserName = "";
        var policyArn = "";
        var policyVersionId = "";
        var trustPolicy = @"{""Version"":""2012-10-17"",""Statement"":[{""Effect"":""Allow"",""Principal"":{""Service"":""lambda.amazonaws.com""},""Action"":""sts:AssumeRole""}]}";
        var updatedTrustPolicy = @"{""Version"":""2012-10-17"",""Statement"":[{""Effect"":""Allow"",""Principal"":{""Service"":""ec2.amazonaws.com""},""Action"":""sts:AssumeRole""}]}";
        var s3Policy = @"{""Version"":""2012-10-17"",""Statement"":[{""Effect"":""Allow"",""Action"":""s3:*"",""Resource"":""*""}]}";
        var s3GetObjectPolicy = @"{""Version"":""2012-10-17"",""Statement"":[{""Effect"":""Allow"",""Action"":""s3:GetObject"",""Resource"":""*""}]}";
        var logsPolicy = @"{""Version"":""2012-10-17"",""Statement"":[{""Effect"":""Allow"",""Action"":""logs:*"",""Resource"":""*""}]}";
        var policyVersion2Doc = @"{""Version"":""2012-10-17"",""Statement"":[{""Effect"":""Deny"",""Action"":""s3:DeleteBucket"",""Resource"":""*""}]}";
        var samlMetadata = @"<EntityDescriptor xmlns=""urn:oasis:names:tc:SAML:2.0:metadata""><SPSSODescriptor protocolSupportEnumeration=""urn:oasis:names:tc:SAML:2.0:protocol""><NameIDFormat>urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress</NameIDFormat></SPSSODescriptor></EntityDescriptor>";

        try
        {
            results.Add(await runner.RunTestAsync("iam", "CreateUser", async () =>
            {
                var resp = await iamClient.CreateUserAsync(new CreateUserRequest { UserName = userName });
                if (resp.User == null)
                    throw new Exception("User is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "GetUser", async () =>
            {
                var resp = await iamClient.GetUserAsync(new GetUserRequest { UserName = userName });
                if (resp.User == null)
                    throw new Exception("User is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "ListUsers", async () =>
            {
                var resp = await iamClient.ListUsersAsync(new ListUsersRequest());
                if (resp.Users == null)
                    throw new Exception("Users is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "ListUsers_Pagination", async () =>
            {
                var allUsers = new List<User>();
                var request = new ListUsersRequest { MaxItems = 10 };
                var resp = await iamClient.ListUsersAsync(request);
                allUsers.AddRange(resp.Users);
                while (resp.IsTruncated == true)
                {
                    request.Marker = resp.Marker;
                    resp = await iamClient.ListUsersAsync(request);
                    allUsers.AddRange(resp.Users);
                }
                if (allUsers.Count == 0)
                    throw new Exception("No users found");
            }));

            results.Add(await runner.RunTestAsync("iam", "CreateAccessKey", async () =>
            {
                var resp = await iamClient.CreateAccessKeyAsync(new CreateAccessKeyRequest { UserName = userName });
                if (resp.AccessKey == null || string.IsNullOrEmpty(resp.AccessKey.AccessKeyId))
                    throw new Exception("AccessKeyId is null or empty");
                accessKeyId = resp.AccessKey.AccessKeyId;
            }));

            results.Add(await runner.RunTestAsync("iam", "ListAccessKeys", async () =>
            {
                var resp = await iamClient.ListAccessKeysAsync(new ListAccessKeysRequest { UserName = userName });
                if (resp.AccessKeyMetadata == null)
                    throw new Exception("AccessKeyMetadata is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "UpdateAccessKey", async () =>
            {
                await iamClient.UpdateAccessKeyAsync(new UpdateAccessKeyRequest
                {
                    AccessKeyId = accessKeyId,
                    UserName = userName,
                    Status = StatusType.Inactive
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "GetAccessKeyLastUsed", async () =>
            {
                var resp = await iamClient.GetAccessKeyLastUsedAsync(new GetAccessKeyLastUsedRequest { AccessKeyId = accessKeyId });
                if (resp.AccessKeyLastUsed == null)
                    throw new Exception("AccessKeyLastUsed is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "CreateLoginProfile", async () =>
            {
                await iamClient.CreateLoginProfileAsync(new CreateLoginProfileRequest
                {
                    UserName = userName,
                    Password = "TempPassword123!"
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "GetLoginProfile", async () =>
            {
                var resp = await iamClient.GetLoginProfileAsync(new GetLoginProfileRequest { UserName = userName });
                if (resp.LoginProfile == null)
                    throw new Exception("LoginProfile is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "UpdateLoginProfile", async () =>
            {
                await iamClient.UpdateLoginProfileAsync(new UpdateLoginProfileRequest
                {
                    UserName = userName,
                    Password = "NewPassword456!",
                    PasswordResetRequired = true
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "UpdateUser", async () =>
            {
                updatedUserName = userName + "Updated";
                await iamClient.UpdateUserAsync(new UpdateUserRequest
                {
                    UserName = userName,
                    NewUserName = updatedUserName
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "TagUser", async () =>
            {
                await iamClient.TagUserAsync(new TagUserRequest
                {
                    UserName = updatedUserName,
                    Tags = new List<Tag> { new Tag { Key = "Environment", Value = "test" } }
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "ListUserTags", async () =>
            {
                var resp = await iamClient.ListUserTagsAsync(new ListUserTagsRequest { UserName = updatedUserName });
                if (resp.Tags == null)
                    throw new Exception("Tags is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "UntagUser", async () =>
            {
                await iamClient.UntagUserAsync(new UntagUserRequest
                {
                    UserName = updatedUserName,
                    TagKeys = new List<string> { "Environment" }
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "CreateGroup", async () =>
            {
                var resp = await iamClient.CreateGroupAsync(new CreateGroupRequest { GroupName = groupName });
                if (resp.Group == null)
                    throw new Exception("Group is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "GetGroup", async () =>
            {
                var resp = await iamClient.GetGroupAsync(new GetGroupRequest { GroupName = groupName });
                if (resp.Group == null)
                    throw new Exception("Group is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "ListGroups", async () =>
            {
                var resp = await iamClient.ListGroupsAsync(new ListGroupsRequest());
                if (resp.Groups == null)
                    throw new Exception("Groups is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "UpdateGroup", async () =>
            {
                var updatedGroupName = groupName + "Updated";
                await iamClient.UpdateGroupAsync(new UpdateGroupRequest
                {
                    GroupName = groupName,
                    NewGroupName = updatedGroupName
                });
                await iamClient.UpdateGroupAsync(new UpdateGroupRequest
                {
                    GroupName = updatedGroupName,
                    NewGroupName = groupName
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "AddUserToGroup", async () =>
            {
                await iamClient.AddUserToGroupAsync(new AddUserToGroupRequest
                {
                    GroupName = groupName,
                    UserName = updatedUserName
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "RemoveUserFromGroup", async () =>
            {
                await iamClient.RemoveUserFromGroupAsync(new RemoveUserFromGroupRequest
                {
                    GroupName = groupName,
                    UserName = updatedUserName
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "PutGroupPolicy", async () =>
            {
                await iamClient.PutGroupPolicyAsync(new PutGroupPolicyRequest
                {
                    GroupName = groupName,
                    PolicyName = "TestGroupPolicy",
                    PolicyDocument = s3GetObjectPolicy
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "GetGroupPolicy", async () =>
            {
                var resp = await iamClient.GetGroupPolicyAsync(new GetGroupPolicyRequest
                {
                    GroupName = groupName,
                    PolicyName = "TestGroupPolicy"
                });
                if (resp.PolicyDocument == null)
                    throw new Exception("PolicyDocument is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "ListGroupPolicies", async () =>
            {
                var resp = await iamClient.ListGroupPoliciesAsync(new ListGroupPoliciesRequest { GroupName = groupName });
                if (resp.PolicyNames == null)
                    throw new Exception("PolicyNames is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "DeleteGroupPolicy", async () =>
            {
                await iamClient.DeleteGroupPolicyAsync(new DeleteGroupPolicyRequest
                {
                    GroupName = groupName,
                    PolicyName = "TestGroupPolicy"
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "CreateRole", async () =>
            {
                var resp = await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = roleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
                if (resp.Role == null)
                    throw new Exception("Role is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "CreateRole_InvalidName", async () =>
            {
                try
                {
                    await iamClient.CreateRoleAsync(new CreateRoleRequest
                    {
                        RoleName = "invalid:role-name",
                        AssumeRolePolicyDocument = trustPolicy
                    });
                    throw new Exception("Expected error but got none");
                }
                catch (Exception ex) when (ex is not Exception { Message: "Expected error but got none" }) { }
            }));

            results.Add(await runner.RunTestAsync("iam", "GetRole", async () =>
            {
                var resp = await iamClient.GetRoleAsync(new GetRoleRequest { RoleName = roleName });
                if (resp.Role == null)
                    throw new Exception("Role is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "ListRoles", async () =>
            {
                var resp = await iamClient.ListRolesAsync(new ListRolesRequest());
                if (resp.Roles == null)
                    throw new Exception("Roles is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "ListRoles_Pagination", async () =>
            {
                var allRoles = new List<Role>();
                var request = new ListRolesRequest { MaxItems = 10 };
                var resp = await iamClient.ListRolesAsync(request);
                allRoles.AddRange(resp.Roles);
                while (resp.IsTruncated == true)
                {
                    request.Marker = resp.Marker;
                    resp = await iamClient.ListRolesAsync(request);
                    allRoles.AddRange(resp.Roles);
                }
                if (allRoles.Count == 0)
                    throw new Exception("No roles found");
            }));

            results.Add(await runner.RunTestAsync("iam", "UpdateRole", async () =>
            {
                await iamClient.UpdateRoleAsync(new UpdateRoleRequest
                {
                    RoleName = roleName,
                    Description = "Updated via UpdateRole",
                    MaxSessionDuration = 3600
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "UpdateAssumeRolePolicy", async () =>
            {
                await iamClient.UpdateAssumeRolePolicyAsync(new UpdateAssumeRolePolicyRequest
                {
                    RoleName = roleName,
                    PolicyDocument = updatedTrustPolicy
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "UpdateRoleDescription", async () =>
            {
                await iamClient.UpdateRoleDescriptionAsync(new UpdateRoleDescriptionRequest
                {
                    RoleName = roleName,
                    Description = "Updated role description"
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "TagRole", async () =>
            {
                await iamClient.TagRoleAsync(new TagRoleRequest
                {
                    RoleName = roleName,
                    Tags = new List<Tag> { new Tag { Key = "Environment", Value = "test" } }
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "ListRoleTags", async () =>
            {
                var resp = await iamClient.ListRoleTagsAsync(new ListRoleTagsRequest { RoleName = roleName });
                if (resp.Tags == null)
                    throw new Exception("Tags is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "UntagRole", async () =>
            {
                await iamClient.UntagRoleAsync(new UntagRoleRequest
                {
                    RoleName = roleName,
                    TagKeys = new List<string> { "Environment" }
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "PutRolePolicy", async () =>
            {
                await iamClient.PutRolePolicyAsync(new PutRolePolicyRequest
                {
                    RoleName = roleName,
                    PolicyName = "TestRolePolicy",
                    PolicyDocument = logsPolicy
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "GetRolePolicy", async () =>
            {
                var resp = await iamClient.GetRolePolicyAsync(new GetRolePolicyRequest
                {
                    RoleName = roleName,
                    PolicyName = "TestRolePolicy"
                });
                if (resp.PolicyDocument == null)
                    throw new Exception("PolicyDocument is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "ListRolePolicies", async () =>
            {
                var resp = await iamClient.ListRolePoliciesAsync(new ListRolePoliciesRequest
                {
                    RoleName = roleName
                });
                if (resp.PolicyNames == null)
                    throw new Exception("PolicyNames is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "DeleteRolePolicy", async () =>
            {
                await iamClient.DeleteRolePolicyAsync(new DeleteRolePolicyRequest
                {
                    RoleName = roleName,
                    PolicyName = "TestRolePolicy"
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "CreatePolicy", async () =>
            {
                var resp = await iamClient.CreatePolicyAsync(new CreatePolicyRequest
                {
                    PolicyName = policyName,
                    PolicyDocument = s3Policy
                });
                if (resp.Policy == null)
                    throw new Exception("Policy is null");
                policyArn = resp.Policy.Arn;
            }));

            results.Add(await runner.RunTestAsync("iam", "GetPolicy", async () =>
            {
                var resp = await iamClient.GetPolicyAsync(new GetPolicyRequest { PolicyArn = policyArn });
                if (resp.Policy == null)
                    throw new Exception("Policy is null");
                if (resp.Policy.Arn == null)
                    throw new Exception("Policy Arn is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "ListPolicies", async () =>
            {
                var resp = await iamClient.ListPoliciesAsync(new ListPoliciesRequest());
                if (resp.Policies == null)
                    throw new Exception("Policies is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "ListPolicies_Pagination", async () =>
            {
                var allPolicies = new List<ManagedPolicy>();
                var request = new ListPoliciesRequest { MaxItems = 10, Scope = PolicyScopeType.Local };
                var resp = await iamClient.ListPoliciesAsync(request);
                allPolicies.AddRange(resp.Policies);
                while (resp.IsTruncated == true)
                {
                    request.Marker = resp.Marker;
                    resp = await iamClient.ListPoliciesAsync(request);
                    allPolicies.AddRange(resp.Policies);
                }
            }));

            results.Add(await runner.RunTestAsync("iam", "TagPolicy", async () =>
            {
                await iamClient.TagPolicyAsync(new TagPolicyRequest
                {
                    PolicyArn = policyArn,
                    Tags = new List<Tag> { new Tag { Key = "Environment", Value = "test" } }
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "ListPolicyTags", async () =>
            {
                var resp = await iamClient.ListPolicyTagsAsync(new ListPolicyTagsRequest { PolicyArn = policyArn });
                if (resp.Tags == null)
                    throw new Exception("Tags is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "UntagPolicy", async () =>
            {
                await iamClient.UntagPolicyAsync(new UntagPolicyRequest
                {
                    PolicyArn = policyArn,
                    TagKeys = new List<string> { "Environment" }
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "CreatePolicyVersion", async () =>
            {
                var resp = await iamClient.CreatePolicyVersionAsync(new CreatePolicyVersionRequest
                {
                    PolicyArn = policyArn,
                    PolicyDocument = policyVersion2Doc,
                    SetAsDefault = false
                });
                if (resp.PolicyVersion == null)
                    throw new Exception("PolicyVersion is null");
                policyVersionId = resp.PolicyVersion.VersionId;
            }));

            results.Add(await runner.RunTestAsync("iam", "ListPolicyVersions", async () =>
            {
                var resp = await iamClient.ListPolicyVersionsAsync(new ListPolicyVersionsRequest { PolicyArn = policyArn });
                if (resp.Versions == null)
                    throw new Exception("Versions is null");
                if (resp.Versions.Count < 2)
                    throw new Exception("Expected at least 2 policy versions");
            }));

            results.Add(await runner.RunTestAsync("iam", "GetPolicyVersion", async () =>
            {
                var resp = await iamClient.GetPolicyVersionAsync(new GetPolicyVersionRequest
                {
                    PolicyArn = policyArn,
                    VersionId = policyVersionId
                });
                if (resp.PolicyVersion == null)
                    throw new Exception("PolicyVersion is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "SetDefaultPolicyVersion", async () =>
            {
                await iamClient.SetDefaultPolicyVersionAsync(new SetDefaultPolicyVersionRequest
                {
                    PolicyArn = policyArn,
                    VersionId = policyVersionId
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "DeletePolicyVersion", async () =>
            {
                await iamClient.DeletePolicyVersionAsync(new DeletePolicyVersionRequest
                {
                    PolicyArn = policyArn,
                    VersionId = "v1"
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "AttachUserPolicy", async () =>
            {
                await iamClient.AttachUserPolicyAsync(new AttachUserPolicyRequest
                {
                    UserName = updatedUserName,
                    PolicyArn = policyArn
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "ListAttachedUserPolicies", async () =>
            {
                var resp = await iamClient.ListAttachedUserPoliciesAsync(new ListAttachedUserPoliciesRequest { UserName = updatedUserName });
                if (resp.AttachedPolicies == null)
                    throw new Exception("AttachedPolicies is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "DetachUserPolicy", async () =>
            {
                await iamClient.DetachUserPolicyAsync(new DetachUserPolicyRequest
                {
                    UserName = updatedUserName,
                    PolicyArn = policyArn
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "AttachGroupPolicy", async () =>
            {
                await iamClient.AttachGroupPolicyAsync(new AttachGroupPolicyRequest
                {
                    GroupName = groupName,
                    PolicyArn = policyArn
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "ListAttachedGroupPolicies", async () =>
            {
                var resp = await iamClient.ListAttachedGroupPoliciesAsync(new ListAttachedGroupPoliciesRequest { GroupName = groupName });
                if (resp.AttachedPolicies == null)
                    throw new Exception("AttachedPolicies is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "DetachGroupPolicy", async () =>
            {
                await iamClient.DetachGroupPolicyAsync(new DetachGroupPolicyRequest
                {
                    GroupName = groupName,
                    PolicyArn = policyArn
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "AttachRolePolicy_FullCycle", async () =>
            {
                await iamClient.AttachRolePolicyAsync(new AttachRolePolicyRequest
                {
                    RoleName = roleName,
                    PolicyArn = policyArn
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "ListAttachedRolePolicies", async () =>
            {
                var resp = await iamClient.ListAttachedRolePoliciesAsync(new ListAttachedRolePoliciesRequest { RoleName = roleName });
                if (resp.AttachedPolicies == null)
                    throw new Exception("AttachedPolicies is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "ListEntitiesForPolicy_Role", async () =>
            {
                var resp = await iamClient.ListEntitiesForPolicyAsync(new ListEntitiesForPolicyRequest
                {
                    PolicyArn = policyArn,
                    EntityFilter = EntityType.Role
                });
                if (resp.PolicyRoles == null)
                    throw new Exception("PolicyRoles is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "DetachRolePolicy", async () =>
            {
                await iamClient.DetachRolePolicyAsync(new DetachRolePolicyRequest
                {
                    RoleName = roleName,
                    PolicyArn = policyArn
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "PutUserPermissionsBoundary", async () =>
            {
                await iamClient.PutUserPermissionsBoundaryAsync(new PutUserPermissionsBoundaryRequest
                {
                    UserName = updatedUserName,
                    PermissionsBoundary = policyArn
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "GetUser_PermissionsBoundary", async () =>
            {
                var resp = await iamClient.GetUserAsync(new GetUserRequest { UserName = updatedUserName });
                if (resp.User.PermissionsBoundary == null)
                    throw new Exception("PermissionsBoundary is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "DeleteUserPermissionsBoundary", async () =>
            {
                await iamClient.DeleteUserPermissionsBoundaryAsync(new DeleteUserPermissionsBoundaryRequest
                {
                    UserName = updatedUserName
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "PutRolePermissionsBoundary", async () =>
            {
                await iamClient.PutRolePermissionsBoundaryAsync(new PutRolePermissionsBoundaryRequest
                {
                    RoleName = roleName,
                    PermissionsBoundary = policyArn
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "GetRole_PermissionsBoundary", async () =>
            {
                var resp = await iamClient.GetRoleAsync(new GetRoleRequest { RoleName = roleName });
                if (resp.Role.PermissionsBoundary == null)
                    throw new Exception("PermissionsBoundary is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "DeleteRolePermissionsBoundary", async () =>
            {
                await iamClient.DeleteRolePermissionsBoundaryAsync(new DeleteRolePermissionsBoundaryRequest
                {
                    RoleName = roleName
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "CreateInstanceProfile", async () =>
            {
                var resp = await iamClient.CreateInstanceProfileAsync(new CreateInstanceProfileRequest
                {
                    InstanceProfileName = instanceProfileName
                });
                if (resp.InstanceProfile == null)
                    throw new Exception("InstanceProfile is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "GetInstanceProfile", async () =>
            {
                var resp = await iamClient.GetInstanceProfileAsync(new GetInstanceProfileRequest
                {
                    InstanceProfileName = instanceProfileName
                });
                if (resp.InstanceProfile == null)
                    throw new Exception("InstanceProfile is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "ListInstanceProfiles", async () =>
            {
                var resp = await iamClient.ListInstanceProfilesAsync(new ListInstanceProfilesRequest());
                if (resp.InstanceProfiles == null)
                    throw new Exception("InstanceProfiles is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "TagInstanceProfile", async () =>
            {
                await iamClient.TagInstanceProfileAsync(new TagInstanceProfileRequest
                {
                    InstanceProfileName = instanceProfileName,
                    Tags = new List<Tag> { new Tag { Key = "Environment", Value = "test" } }
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "ListInstanceProfileTags", async () =>
            {
                var resp = await iamClient.ListInstanceProfileTagsAsync(new ListInstanceProfileTagsRequest { InstanceProfileName = instanceProfileName });
                if (resp.Tags == null)
                    throw new Exception("Tags is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "UntagInstanceProfile", async () =>
            {
                await iamClient.UntagInstanceProfileAsync(new UntagInstanceProfileRequest
                {
                    InstanceProfileName = instanceProfileName,
                    TagKeys = new List<string> { "Environment" }
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "AddRoleToInstanceProfile", async () =>
            {
                await iamClient.AddRoleToInstanceProfileAsync(new AddRoleToInstanceProfileRequest
                {
                    InstanceProfileName = instanceProfileName,
                    RoleName = roleName
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "ListInstanceProfilesForRole", async () =>
            {
                var resp = await iamClient.ListInstanceProfilesForRoleAsync(new ListInstanceProfilesForRoleRequest { RoleName = roleName });
                if (resp.InstanceProfiles == null)
                    throw new Exception("InstanceProfiles is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "RemoveRoleFromInstanceProfile", async () =>
            {
                await iamClient.RemoveRoleFromInstanceProfileAsync(new RemoveRoleFromInstanceProfileRequest
                {
                    InstanceProfileName = instanceProfileName,
                    RoleName = roleName
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "DeleteInstanceProfile", async () =>
            {
                await iamClient.DeleteInstanceProfileAsync(new DeleteInstanceProfileRequest
                {
                    InstanceProfileName = instanceProfileName
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "PutUserPolicy", async () =>
            {
                await iamClient.PutUserPolicyAsync(new PutUserPolicyRequest
                {
                    UserName = updatedUserName,
                    PolicyName = "TestUserPolicy",
                    PolicyDocument = s3GetObjectPolicy
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "GetUserPolicy", async () =>
            {
                var resp = await iamClient.GetUserPolicyAsync(new GetUserPolicyRequest
                {
                    UserName = updatedUserName,
                    PolicyName = "TestUserPolicy"
                });
                if (resp.PolicyDocument == null)
                    throw new Exception("PolicyDocument is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "ListUserPolicies", async () =>
            {
                var resp = await iamClient.ListUserPoliciesAsync(new ListUserPoliciesRequest
                {
                    UserName = updatedUserName
                });
                if (resp.PolicyNames == null)
                    throw new Exception("PolicyNames is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "GetAccountSummary", async () =>
            {
                var resp = await iamClient.GetAccountSummaryAsync(new GetAccountSummaryRequest());
                if (resp.SummaryMap == null)
                    throw new Exception("SummaryMap is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "GetAccountAuthorizationDetails", async () =>
            {
                var resp = await iamClient.GetAccountAuthorizationDetailsAsync(new GetAccountAuthorizationDetailsRequest());
                if (resp.UserDetailList == null)
                    throw new Exception("UserDetailList is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "DeleteUserPolicy", async () =>
            {
                await iamClient.DeleteUserPolicyAsync(new DeleteUserPolicyRequest
                {
                    UserName = updatedUserName,
                    PolicyName = "TestUserPolicy"
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "DeleteLoginProfile", async () =>
            {
                await iamClient.DeleteLoginProfileAsync(new DeleteLoginProfileRequest
                {
                    UserName = updatedUserName
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "DeleteAccessKey", async () =>
            {
                if (string.IsNullOrEmpty(accessKeyId))
                    throw new Exception("AccessKeyId not set");
                await iamClient.DeleteAccessKeyAsync(new DeleteAccessKeyRequest
                {
                    AccessKeyId = accessKeyId,
                    UserName = updatedUserName
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "DeleteUser", async () =>
            {
                await iamClient.DeleteUserAsync(new DeleteUserRequest { UserName = updatedUserName });
            }));

            results.Add(await runner.RunTestAsync("iam", "DeleteGroup", async () =>
            {
                await iamClient.DeleteGroupAsync(new DeleteGroupRequest { GroupName = groupName });
            }));

            results.Add(await runner.RunTestAsync("iam", "DeleteRole", async () =>
            {
                await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = roleName });
            }));

            results.Add(await runner.RunTestAsync("iam", "DeletePolicy", async () =>
            {
                await iamClient.DeletePolicyAsync(new DeletePolicyRequest { PolicyArn = policyArn });
            }));
        }
        finally
        {
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteUserPermissionsBoundaryAsync(new DeleteUserPermissionsBoundaryRequest { UserName = updatedUserName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DetachUserPolicyAsync(new DetachUserPolicyRequest { UserName = updatedUserName, PolicyArn = policyArn }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteAccessKeyAsync(new DeleteAccessKeyRequest { AccessKeyId = accessKeyId, UserName = updatedUserName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteLoginProfileAsync(new DeleteLoginProfileRequest { UserName = updatedUserName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteUserPolicyAsync(new DeleteUserPolicyRequest { UserName = updatedUserName, PolicyName = "TestUserPolicy" }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteUserAsync(new DeleteUserRequest { UserName = updatedUserName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteUserAsync(new DeleteUserRequest { UserName = userName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DetachGroupPolicyAsync(new DetachGroupPolicyRequest { GroupName = groupName, PolicyArn = policyArn }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteGroupPolicyAsync(new DeleteGroupPolicyRequest { GroupName = groupName, PolicyName = "TestGroupPolicy" }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteGroupAsync(new DeleteGroupRequest { GroupName = groupName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRolePermissionsBoundaryAsync(new DeleteRolePermissionsBoundaryRequest { RoleName = roleName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DetachRolePolicyAsync(new DetachRolePolicyRequest { RoleName = roleName, PolicyArn = policyArn }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRolePolicyAsync(new DeleteRolePolicyRequest { RoleName = roleName, PolicyName = "TestRolePolicy" }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = roleName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.RemoveRoleFromInstanceProfileAsync(new RemoveRoleFromInstanceProfileRequest { InstanceProfileName = instanceProfileName, RoleName = roleName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteInstanceProfileAsync(new DeleteInstanceProfileRequest { InstanceProfileName = instanceProfileName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeletePolicyAsync(new DeletePolicyRequest { PolicyArn = policyArn }); });
        }

        results.Add(await runner.RunTestAsync("iam", "DeleteNonExistentAccessKey", async () =>
        {
            try
            {
                await iamClient.DeleteAccessKeyAsync(new DeleteAccessKeyRequest
                {
                    AccessKeyId = "AKIAIOSFODNN7EXAMPLE"
                });
                throw new Exception("Expected error but got none");
            }
            catch (NoSuchEntityException) { }
        }));

        results.Add(await runner.RunTestAsync("iam", "Error_DeleteNonExistentUser", async () =>
        {
            try
            {
                await iamClient.DeleteUserAsync(new DeleteUserRequest
                {
                    UserName = "NonExistentUser_xyz_12345"
                });
                throw new Exception("Expected error but got none");
            }
            catch (NoSuchEntityException) { }
        }));

        results.Add(await runner.RunTestAsync("iam", "Error_GetNonExistentRole", async () =>
        {
            try
            {
                await iamClient.GetRoleAsync(new GetRoleRequest
                {
                    RoleName = "NonExistentRole_xyz_12345"
                });
                throw new Exception("Expected error but got none");
            }
            catch (NoSuchEntityException) { }
        }));

        results.Add(await runner.RunTestAsync("iam", "Error_AttachPolicyToNonExistentUser", async () =>
        {
            var tempPolicyName = TestRunner.MakeUniqueName("CSTempPolicy");
            string tempPolicyArn = "";
            try
            {
                var createResp = await iamClient.CreatePolicyAsync(new CreatePolicyRequest
                {
                    PolicyName = tempPolicyName,
                    PolicyDocument = s3Policy
                });
                tempPolicyArn = createResp.Policy.Arn;
                try
                {
                    await iamClient.AttachUserPolicyAsync(new AttachUserPolicyRequest
                    {
                        UserName = "NonExistentUser_xyz_12345",
                        PolicyArn = tempPolicyArn
                    });
                    throw new Exception("Expected error but got none");
                }
                catch (NoSuchEntityException) { }
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempPolicyArn))
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeletePolicyAsync(new DeletePolicyRequest { PolicyArn = tempPolicyArn }); });
                }
            }
        }));

        results.Add(await runner.RunTestAsync("iam", "Error_DeleteDefaultPolicyVersion", async () =>
        {
            var tempPolicyName = TestRunner.MakeUniqueName("CSTempPolicy2");
            string tempPolicyArn = "";
            try
            {
                var createResp = await iamClient.CreatePolicyAsync(new CreatePolicyRequest
                {
                    PolicyName = tempPolicyName,
                    PolicyDocument = s3Policy
                });
                tempPolicyArn = createResp.Policy.Arn;
                try
                {
                    await iamClient.DeletePolicyVersionAsync(new DeletePolicyVersionRequest
                    {
                        PolicyArn = tempPolicyArn,
                        VersionId = "v1"
                    });
                    throw new Exception("Expected error but got none");
                }
                catch (DeleteConflictException) { }
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempPolicyArn))
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeletePolicyAsync(new DeletePolicyRequest { PolicyArn = tempPolicyArn }); });
                }
            }
        }));

        results.Add(await runner.RunTestAsync("iam", "Error_CreateDuplicateUser", async () =>
        {
            var tempUserName = TestRunner.MakeUniqueName("CSDupUser");
            try
            {
                await iamClient.CreateUserAsync(new CreateUserRequest { UserName = tempUserName });
                try
                {
                    await iamClient.CreateUserAsync(new CreateUserRequest { UserName = tempUserName });
                    throw new Exception("Expected error but got none");
                }
                catch (EntityAlreadyExistsException) { }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteUserAsync(new DeleteUserRequest { UserName = tempUserName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("iam", "Error_CreateDuplicatePolicy", async () =>
        {
            var tempPolicyName = TestRunner.MakeUniqueName("CSDupPolicy");
            try
            {
                await iamClient.CreatePolicyAsync(new CreatePolicyRequest
                {
                    PolicyName = tempPolicyName,
                    PolicyDocument = s3Policy
                });
                try
                {
                    await iamClient.CreatePolicyAsync(new CreatePolicyRequest
                    {
                        PolicyName = tempPolicyName,
                        PolicyDocument = s3Policy
                    });
                    throw new Exception("Expected error but got none");
                }
                catch (EntityAlreadyExistsException) { }
            }
            finally
            {
                try { await iamClient.DeletePolicyAsync(new DeletePolicyRequest { PolicyArn = $"arn:aws:iam::000000000000:policy/{tempPolicyName}" }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("iam", "CreateAccountAlias", async () =>
        {
            var aliasName = TestRunner.MakeUniqueName("CSAlias").ToLowerInvariant().Replace("_", "").Replace(" ", "");
            await iamClient.CreateAccountAliasAsync(new CreateAccountAliasRequest { AccountAlias = aliasName });
            try { await iamClient.DeleteAccountAliasAsync(new DeleteAccountAliasRequest()); } catch { }
        }));

        results.Add(await runner.RunTestAsync("iam", "ListAccountAliases", async () =>
        {
            var aliasName = TestRunner.MakeUniqueName("CSAlias").ToLowerInvariant().Replace("_", "").Replace(" ", "");
            try
            {
                await iamClient.CreateAccountAliasAsync(new CreateAccountAliasRequest { AccountAlias = aliasName });
                var resp = await iamClient.ListAccountAliasesAsync(new ListAccountAliasesRequest());
                if (resp.AccountAliases == null)
                    throw new Exception("AccountAliases is null");
            }
            finally
            {
                try { await iamClient.DeleteAccountAliasAsync(new DeleteAccountAliasRequest()); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("iam", "DeleteAccountAlias", async () =>
        {
            var aliasName = TestRunner.MakeUniqueName("CSAlias").ToLowerInvariant().Replace("_", "").Replace(" ", "");
            await iamClient.CreateAccountAliasAsync(new CreateAccountAliasRequest { AccountAlias = aliasName });
            await iamClient.DeleteAccountAliasAsync(new DeleteAccountAliasRequest());
        }));

        results.Add(await runner.RunTestAsync("iam", "UpdateAccountPasswordPolicy", async () =>
        {
            await iamClient.UpdateAccountPasswordPolicyAsync(new UpdateAccountPasswordPolicyRequest
            {
                MinimumPasswordLength = 12,
                RequireSymbols = true,
                RequireNumbers = true,
                RequireUppercaseCharacters = true,
                RequireLowercaseCharacters = true,
                AllowUsersToChangePassword = true
            });
            try { await iamClient.DeleteAccountPasswordPolicyAsync(new DeleteAccountPasswordPolicyRequest()); } catch { }
        }));

        results.Add(await runner.RunTestAsync("iam", "GetAccountPasswordPolicy", async () =>
        {
            try
            {
                await iamClient.UpdateAccountPasswordPolicyAsync(new UpdateAccountPasswordPolicyRequest
                {
                    MinimumPasswordLength = 12,
                    RequireSymbols = true,
                    RequireNumbers = true,
                    RequireUppercaseCharacters = true,
                    RequireLowercaseCharacters = true,
                    AllowUsersToChangePassword = true
                });
                var resp = await iamClient.GetAccountPasswordPolicyAsync(new GetAccountPasswordPolicyRequest());
                if (resp.PasswordPolicy == null)
                    throw new Exception("PasswordPolicy is null");
            }
            finally
            {
                try { await iamClient.DeleteAccountPasswordPolicyAsync(new DeleteAccountPasswordPolicyRequest()); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("iam", "DeleteAccountPasswordPolicy", async () =>
        {
            await iamClient.UpdateAccountPasswordPolicyAsync(new UpdateAccountPasswordPolicyRequest
            {
                MinimumPasswordLength = 8,
                AllowUsersToChangePassword = true
            });
            await iamClient.DeleteAccountPasswordPolicyAsync(new DeleteAccountPasswordPolicyRequest());
        }));

        results.Add(await runner.RunTestAsync("iam", "CreateServiceLinkedRole", async () =>
        {
            var slrName = TestRunner.MakeUniqueName("CSSLR");
            try
            {
                var resp = await iamClient.CreateServiceLinkedRoleAsync(new CreateServiceLinkedRoleRequest
                {
                    AWSServiceName = "autoscaling.amazonaws.com",
                    Description = "Test service linked role",
                    CustomSuffix = slrName
                });
                if (resp.Role == null)
                    throw new Exception("Role is null");
            }
            finally
            {
                try { await iamClient.DeleteServiceLinkedRoleAsync(new DeleteServiceLinkedRoleRequest { RoleName = $"AWSServiceRoleForautoscaling.amazonaws.com_{slrName}" }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("iam", "DeleteServiceLinkedRole", async () =>
        {
            var slrName = TestRunner.MakeUniqueName("CSSLR2");
            string? actualRoleName = null;
            try
            {
                var createResp = await iamClient.CreateServiceLinkedRoleAsync(new CreateServiceLinkedRoleRequest
                {
                    AWSServiceName = "autoscaling.amazonaws.com",
                    Description = "Test service linked role for deletion",
                    CustomSuffix = slrName
                });
                actualRoleName = createResp.Role?.RoleName;
                if (string.IsNullOrEmpty(actualRoleName))
                    throw new Exception("Created role name is null");
                var resp = await iamClient.DeleteServiceLinkedRoleAsync(new DeleteServiceLinkedRoleRequest { RoleName = actualRoleName });
                if (resp.DeletionTaskId == null)
                    throw new Exception("DeletionTaskId is null");
            }
            finally
            {
                if (!string.IsNullOrEmpty(actualRoleName))
                    await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteServiceLinkedRoleAsync(new DeleteServiceLinkedRoleRequest { RoleName = actualRoleName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("iam", "GetServiceLinkedRoleDeletionStatus", async () =>
        {
            var slrName = TestRunner.MakeUniqueName("CSSLR3");
            string? actualRoleName = null;
            string? deletionTaskId = null;
            try
            {
                var createResp = await iamClient.CreateServiceLinkedRoleAsync(new CreateServiceLinkedRoleRequest
                {
                    AWSServiceName = "autoscaling.amazonaws.com",
                    Description = "Test service linked role for status",
                    CustomSuffix = slrName
                });
                actualRoleName = createResp.Role?.RoleName;
                if (string.IsNullOrEmpty(actualRoleName))
                    throw new Exception("Created role name is null");
                var deleteResp = await iamClient.DeleteServiceLinkedRoleAsync(new DeleteServiceLinkedRoleRequest { RoleName = actualRoleName });
                deletionTaskId = deleteResp.DeletionTaskId;
                if (string.IsNullOrEmpty(deletionTaskId))
                    throw new Exception("DeletionTaskId is null");
                var resp = await iamClient.GetServiceLinkedRoleDeletionStatusAsync(new GetServiceLinkedRoleDeletionStatusRequest { DeletionTaskId = deletionTaskId });
                if (resp.Status == null)
                    throw new Exception("Status is null");
            }
            finally
            {
                if (!string.IsNullOrEmpty(actualRoleName))
                    await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteServiceLinkedRoleAsync(new DeleteServiceLinkedRoleRequest { RoleName = actualRoleName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("iam", "CreateSAMLProvider", async () =>
        {
            var providerName = TestRunner.MakeUniqueName("CSSAML");
            try
            {
                var resp = await iamClient.CreateSAMLProviderAsync(new CreateSAMLProviderRequest
                {
                    Name = providerName,
                    SAMLMetadataDocument = samlMetadata
                });
                if (resp.SAMLProviderArn == null)
                    throw new Exception("SAMLProviderArn is null");
            }
            finally
            {
                try { await iamClient.DeleteSAMLProviderAsync(new DeleteSAMLProviderRequest { SAMLProviderArn = $"arn:aws:iam::000000000000:saml-provider/{providerName}" }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("iam", "GetSAMLProvider", async () =>
        {
            var providerName = TestRunner.MakeUniqueName("CSSAML");
            string providerArn = "";
            try
            {
                var createResp = await iamClient.CreateSAMLProviderAsync(new CreateSAMLProviderRequest
                {
                    Name = providerName,
                    SAMLMetadataDocument = samlMetadata
                });
                providerArn = createResp.SAMLProviderArn;
                var resp = await iamClient.GetSAMLProviderAsync(new GetSAMLProviderRequest { SAMLProviderArn = providerArn });
                if (resp.SAMLMetadataDocument == null)
                    throw new Exception("SAMLMetadataDocument is null");
            }
            finally
            {
                if (!string.IsNullOrEmpty(providerArn))
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteSAMLProviderAsync(new DeleteSAMLProviderRequest { SAMLProviderArn = providerArn }); });
                }
            }
        }));

        results.Add(await runner.RunTestAsync("iam", "ListSAMLProviders", async () =>
        {
            var resp = await iamClient.ListSAMLProvidersAsync(new ListSAMLProvidersRequest());
            if (resp.SAMLProviderList != null)
            {
                foreach (var p in resp.SAMLProviderList)
                {
                    if (string.IsNullOrEmpty(p.Arn))
                        throw new Exception("SAMLProvider ARN is null");
                }
            }
        }));

        results.Add(await runner.RunTestAsync("iam", "UpdateSAMLProvider", async () =>
        {
            var providerName = TestRunner.MakeUniqueName("CSSAML");
            string providerArn = "";
            try
            {
                var createResp = await iamClient.CreateSAMLProviderAsync(new CreateSAMLProviderRequest
                {
                    Name = providerName,
                    SAMLMetadataDocument = samlMetadata
                });
                providerArn = createResp.SAMLProviderArn;
                await iamClient.UpdateSAMLProviderAsync(new UpdateSAMLProviderRequest
                {
                    SAMLProviderArn = providerArn,
                    SAMLMetadataDocument = samlMetadata
                });
            }
            finally
            {
                if (!string.IsNullOrEmpty(providerArn))
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteSAMLProviderAsync(new DeleteSAMLProviderRequest { SAMLProviderArn = providerArn }); });
                }
            }
        }));

        results.Add(await runner.RunTestAsync("iam", "TagSAMLProvider", async () =>
        {
            var providerName = TestRunner.MakeUniqueName("CSSAML");
            string providerArn = "";
            try
            {
                var createResp = await iamClient.CreateSAMLProviderAsync(new CreateSAMLProviderRequest
                {
                    Name = providerName,
                    SAMLMetadataDocument = samlMetadata
                });
                providerArn = createResp.SAMLProviderArn;
                await iamClient.TagSAMLProviderAsync(new TagSAMLProviderRequest
                {
                    SAMLProviderArn = providerArn,
                    Tags = new List<Tag> { new Tag { Key = "Environment", Value = "test" } }
                });
            }
            finally
            {
                if (!string.IsNullOrEmpty(providerArn))
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteSAMLProviderAsync(new DeleteSAMLProviderRequest { SAMLProviderArn = providerArn }); });
                }
            }
        }));

        results.Add(await runner.RunTestAsync("iam", "ListSAMLProviderTags", async () =>
        {
            var providerName = TestRunner.MakeUniqueName("CSSAML");
            string providerArn = "";
            try
            {
                var createResp = await iamClient.CreateSAMLProviderAsync(new CreateSAMLProviderRequest
                {
                    Name = providerName,
                    SAMLMetadataDocument = samlMetadata
                });
                providerArn = createResp.SAMLProviderArn;
                await iamClient.TagSAMLProviderAsync(new TagSAMLProviderRequest
                {
                    SAMLProviderArn = providerArn,
                    Tags = new List<Tag> { new Tag { Key = "Environment", Value = "test" } }
                });
                var resp = await iamClient.ListSAMLProviderTagsAsync(new ListSAMLProviderTagsRequest { SAMLProviderArn = providerArn });
                if (resp.Tags == null)
                    throw new Exception("Tags is null");
            }
            finally
            {
                if (!string.IsNullOrEmpty(providerArn))
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteSAMLProviderAsync(new DeleteSAMLProviderRequest { SAMLProviderArn = providerArn }); });
                }
            }
        }));

        results.Add(await runner.RunTestAsync("iam", "UntagSAMLProvider", async () =>
        {
            var providerName = TestRunner.MakeUniqueName("CSSAML");
            string providerArn = "";
            try
            {
                var createResp = await iamClient.CreateSAMLProviderAsync(new CreateSAMLProviderRequest
                {
                    Name = providerName,
                    SAMLMetadataDocument = samlMetadata
                });
                providerArn = createResp.SAMLProviderArn;
                await iamClient.TagSAMLProviderAsync(new TagSAMLProviderRequest
                {
                    SAMLProviderArn = providerArn,
                    Tags = new List<Tag> { new Tag { Key = "Environment", Value = "test" } }
                });
                await iamClient.UntagSAMLProviderAsync(new UntagSAMLProviderRequest
                {
                    SAMLProviderArn = providerArn,
                    TagKeys = new List<string> { "Environment" }
                });
            }
            finally
            {
                if (!string.IsNullOrEmpty(providerArn))
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteSAMLProviderAsync(new DeleteSAMLProviderRequest { SAMLProviderArn = providerArn }); });
                }
            }
        }));

        results.Add(await runner.RunTestAsync("iam", "DeleteSAMLProvider", async () =>
        {
            var providerName = TestRunner.MakeUniqueName("CSSAML");
            var createResp = await iamClient.CreateSAMLProviderAsync(new CreateSAMLProviderRequest
            {
                Name = providerName,
                SAMLMetadataDocument = samlMetadata
            });
            await iamClient.DeleteSAMLProviderAsync(new DeleteSAMLProviderRequest { SAMLProviderArn = createResp.SAMLProviderArn });
        }));

        results.Add(await runner.RunTestAsync("iam", "CreateVirtualMFADevice", async () =>
        {
            var deviceName = TestRunner.MakeUniqueName("CSMFA");
            try
            {
                var resp = await iamClient.CreateVirtualMFADeviceAsync(new CreateVirtualMFADeviceRequest
                {
                    VirtualMFADeviceName = deviceName
                });
                if (resp.VirtualMFADevice == null)
                    throw new Exception("VirtualMFADevice is null");
            }
            finally
            {
                try { await iamClient.DeleteVirtualMFADeviceAsync(new DeleteVirtualMFADeviceRequest { SerialNumber = $"arn:aws:iam::000000000000:mfa/{deviceName}" }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("iam", "ListVirtualMFADevices", async () =>
        {
            var resp = await iamClient.ListVirtualMFADevicesAsync(new ListVirtualMFADevicesRequest());
            if (resp.VirtualMFADevices == null)
                throw new Exception("VirtualMFADevices is null");
        }));

        results.Add(await runner.RunTestAsync("iam", "DeleteVirtualMFADevice", async () =>
        {
            var deviceName = TestRunner.MakeUniqueName("CSMFA");
            var createResp = await iamClient.CreateVirtualMFADeviceAsync(new CreateVirtualMFADeviceRequest
            {
                VirtualMFADeviceName = deviceName
            });
            await iamClient.DeleteVirtualMFADeviceAsync(new DeleteVirtualMFADeviceRequest
            {
                SerialNumber = createResp.VirtualMFADevice.SerialNumber
            });
        }));

        return results;
    }
}
