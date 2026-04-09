using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;

namespace VorpalStacks.SDK.Tests;

public static class IamHelpers
{
    public const string LambdaTrustPolicy = @"{
        ""Version"": ""2012-10-17"",
        ""Statement"": [{
            ""Effect"": ""Allow"",
            ""Principal"": {""Service"": ""lambda.amazonaws.com""},
            ""Action"": ""sts:AssumeRole""
        }]
    }";

    public const string StatesTrustPolicy = @"{
        ""Version"": ""2012-10-17"",
        ""Statement"": [{
            ""Effect"": ""Allow"",
            ""Principal"": {""Service"": ""states.amazonaws.com""},
            ""Action"": ""sts:AssumeRole""
        }]
    }";

    public const string SchedulerTrustPolicy = @"{
        ""Version"": ""2012-10-17"",
        ""Statement"": [{
            ""Effect"": ""Allow"",
            ""Principal"": {""Service"": ""scheduler.amazonaws.com""},
            ""Action"": ""sts:AssumeRole""
        }]
    }";

    public const string EventsTrustPolicy = @"{
        ""Version"": ""2012-10-17"",
        ""Statement"": [{
            ""Effect"": ""Allow"",
            ""Principal"": {""Service"": ""events.amazonaws.com""},
            ""Action"": ""sts:AssumeRole""
        }]
    }";

    public static string MakeTrustPolicy(string service) => $@"{{
        ""Version"": ""2012-10-17"",
        ""Statement"": [{{
            ""Effect"": ""Allow"",
            ""Principal"": {{""Service"": ""{service}.amazonaws.com""}},
            ""Action"": ""sts:AssumeRole""
        }}]
    }}";

    public static async Task<(string roleName, string roleArn)> CreateTestRoleAsync(
        AmazonIdentityManagementServiceClient iamClient,
        string prefix,
        string trustPolicy)
    {
        var roleName = TestRunner.MakeUniqueName(prefix);
        await iamClient.CreateRoleAsync(new CreateRoleRequest
        {
            RoleName = roleName,
            AssumeRolePolicyDocument = trustPolicy
        });
        var roleArn = $"arn:aws:iam::000000000000:role/{roleName}";
        return (roleName, roleArn);
    }

    public static async Task DeleteTestRoleAsync(
        AmazonIdentityManagementServiceClient iamClient,
        string roleName)
    {
        await TestHelpers.SafeCleanupAsync(async () =>
        {
            try
            {
                var attachedPolicies = await iamClient.ListAttachedRolePoliciesAsync(
                    new ListAttachedRolePoliciesRequest { RoleName = roleName });
                foreach (var policy in attachedPolicies.AttachedPolicies)
                    await iamClient.DetachRolePolicyAsync(
                        new DetachRolePolicyRequest { RoleName = roleName, PolicyArn = policy.PolicyArn });
            }
            catch { }

            try
            {
                var inlinePolicies = await iamClient.ListRolePoliciesAsync(
                    new ListRolePoliciesRequest { RoleName = roleName });
                foreach (var policyName in inlinePolicies.PolicyNames)
                    await iamClient.DeleteRolePolicyAsync(
                        new DeleteRolePolicyRequest { RoleName = roleName, PolicyName = policyName });
            }
            catch { }

            await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = roleName });
        });
    }
}
