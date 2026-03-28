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
        var trustPolicy = @"{""Version"":""2012-10-17"",""Statement"":[{""Effect"":""Allow"",""Principal"":{""Service"":""lambda.amazonaws.com""},""Action"":""sts:AssumeRole""}]}";
        var s3Policy = @"{""Version"":""2012-10-17"",""Statement"":[{""Effect"":""Allow"",""Action"":""s3:*"",""Resource"":""*""}]}";
        var s3GetObjectPolicy = @"{""Version"":""2012-10-17"",""Statement"":[{""Effect"":""Allow"",""Action"":""s3:GetObject"",""Resource"":""*""}]}";
        var logsPolicy = @"{""Version"":""2012-10-17"",""Statement"":[{""Effect"":""Allow"",""Action"":""logs:*"",""Resource"":""*""}]}";

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

            results.Add(await runner.RunTestAsync("iam", "CreatePolicy", async () =>
            {
                var resp = await iamClient.CreatePolicyAsync(new CreatePolicyRequest
                {
                    PolicyName = policyName,
                    PolicyDocument = s3Policy
                });
                if (resp.Policy == null)
                    throw new Exception("Policy is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "ListPolicies", async () =>
            {
                var resp = await iamClient.ListPoliciesAsync(new ListPoliciesRequest());
                if (resp.Policies == null)
                    throw new Exception("Policies is null");
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

            results.Add(await runner.RunTestAsync("iam", "AddRoleToInstanceProfile", async () =>
            {
                await iamClient.AddRoleToInstanceProfileAsync(new AddRoleToInstanceProfileRequest
                {
                    InstanceProfileName = instanceProfileName,
                    RoleName = roleName
                });
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

            results.Add(await runner.RunTestAsync("iam", "GetAccountSummary", async () =>
            {
                var resp = await iamClient.GetAccountSummaryAsync(new GetAccountSummaryRequest());
                if (resp.SummaryMap == null)
                    throw new Exception("SummaryMap is null");
            }));

            results.Add(await runner.RunTestAsync("iam", "DeleteUserPolicy", async () =>
            {
                await iamClient.DeleteUserPolicyAsync(new DeleteUserPolicyRequest
                {
                    UserName = updatedUserName,
                    PolicyName = "TestUserPolicy"
                });
            }));

            results.Add(await runner.RunTestAsync("iam", "DeleteRolePolicy", async () =>
            {
                await iamClient.DeleteRolePolicyAsync(new DeleteRolePolicyRequest
                {
                    RoleName = roleName,
                    PolicyName = "TestRolePolicy"
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
        }
        finally
        {
            try { await iamClient.DeleteAccessKeyAsync(new DeleteAccessKeyRequest { AccessKeyId = accessKeyId, UserName = updatedUserName }); } catch { }
            try { await iamClient.DeleteLoginProfileAsync(new DeleteLoginProfileRequest { UserName = updatedUserName }); } catch { }
            try { await iamClient.DeleteUserPolicyAsync(new DeleteUserPolicyRequest { UserName = updatedUserName, PolicyName = "TestUserPolicy" }); } catch { }
            try { await iamClient.DeleteUserAsync(new DeleteUserRequest { UserName = updatedUserName }); } catch { }
            try { await iamClient.DeleteUserAsync(new DeleteUserRequest { UserName = userName }); } catch { }
            try { await iamClient.DeleteRolePolicyAsync(new DeleteRolePolicyRequest { RoleName = roleName, PolicyName = "TestRolePolicy" }); } catch { }
            try { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = roleName }); } catch { }
            try { await iamClient.DeleteGroupAsync(new DeleteGroupRequest { GroupName = groupName }); } catch { }
            try { await iamClient.RemoveRoleFromInstanceProfileAsync(new RemoveRoleFromInstanceProfileRequest { InstanceProfileName = instanceProfileName, RoleName = roleName }); } catch { }
            try { await iamClient.DeleteInstanceProfileAsync(new DeleteInstanceProfileRequest { InstanceProfileName = instanceProfileName }); } catch { }
            try { await iamClient.DeletePolicyAsync(new DeletePolicyRequest { PolicyArn = $"arn:aws:iam::000000000000:policy/{policyName}" }); } catch { }
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
            catch (NoSuchEntityException)
            {
            }
        }));

        return results;
    }
}
