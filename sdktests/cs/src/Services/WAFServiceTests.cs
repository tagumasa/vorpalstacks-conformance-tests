using Amazon.WAFV2;
using Amazon.WAFV2.Model;
using Amazon.Runtime;
using System.Text.Json;

namespace VorpalStacks.SDK.Tests.Services;

public static class WAFServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonWAFV2Client wafClient,
        string region)
    {
        var results = new List<TestResult>();
        var scope = Scope.REGIONAL;
        var ipSetName = TestRunner.MakeUniqueName("test-ipset");
        var ipSetDescription = "Test IP Set";
        string? ipSetId = null;
        string? ipSetLockToken = null;
        string? ipSetARN = null;

        results.Add(await runner.RunTestAsync("waf", "ListWebACLs", async () =>
        {
            await wafClient.ListWebACLsAsync(new ListWebACLsRequest { Scope = scope });
        }));

        results.Add(await runner.RunTestAsync("waf", "CreateIPSet", async () =>
        {
            var resp = await wafClient.CreateIPSetAsync(new CreateIPSetRequest
            {
                Name = ipSetName,
                Description = ipSetDescription,
                Scope = scope,
                IPAddressVersion = IPAddressVersion.IPV4,
                Addresses = ["10.0.0.0/24"],
            });
            ipSetId = resp.Summary?.Id;
            ipSetLockToken = resp.Summary?.LockToken;
            ipSetARN = resp.Summary?.ARN;
        }));

        results.Add(await runner.RunTestAsync("waf", "GetIPSet", async () =>
        {
            if (ipSetId == null) throw new Exception("IP Set ID not available");
            var resp = await wafClient.GetIPSetAsync(new GetIPSetRequest
            {
                Id = ipSetId,
                Scope = scope,
                Name = ipSetName,
            });
            if (resp.LockToken != null)
                ipSetLockToken = resp.LockToken;
        }));

        results.Add(await runner.RunTestAsync("waf", "ListIPSets", async () =>
        {
            await wafClient.ListIPSetsAsync(new ListIPSetsRequest { Scope = scope });
        }));

        results.Add(await runner.RunTestAsync("waf", "ListTagsForResource", async () =>
        {
            if (ipSetARN == null) throw new Exception("IP Set ARN not available");
            await wafClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
            {
                ResourceARN = ipSetARN,
            });
        }));

        results.Add(await runner.RunTestAsync("waf", "UpdateIPSet", async () =>
        {
            if (ipSetId == null) throw new Exception("IP Set ID not available");
            var getResp = await wafClient.GetIPSetAsync(new GetIPSetRequest
            {
                Id = ipSetId,
                Scope = scope,
                Name = ipSetName,
            });
            var currentLockToken = getResp.LockToken ?? ipSetLockToken;
            await wafClient.UpdateIPSetAsync(new UpdateIPSetRequest
            {
                Id = ipSetId,
                Scope = scope,
                Name = ipSetName,
                Addresses = ["10.0.0.0/24", "192.168.0.0/24"],
                LockToken = currentLockToken,
            });
        }));

        results.Add(await runner.RunTestAsync("waf", "DeleteIPSet", async () =>
        {
            if (ipSetId == null) throw new Exception("IP Set ID not available");
            var getResp = await wafClient.GetIPSetAsync(new GetIPSetRequest
            {
                Id = ipSetId,
                Scope = scope,
                Name = ipSetName,
            });
            await wafClient.DeleteIPSetAsync(new DeleteIPSetRequest
            {
                Id = ipSetId,
                Scope = scope,
                Name = ipSetName,
                LockToken = getResp.LockToken ?? ipSetLockToken,
            });
            ipSetId = null;
        }));

        var regexPatternSetName = TestRunner.MakeUniqueName("test-regex");
        string? regexPatternSetId = null;
        string? regexPatternSetLockToken = null;

        results.Add(await runner.RunTestAsync("waf", "CreateRegexPatternSet", async () =>
        {
            var resp = await wafClient.CreateRegexPatternSetAsync(new CreateRegexPatternSetRequest
            {
                Name = regexPatternSetName,
                Description = "Test Regex Pattern Set",
                Scope = scope,
                RegularExpressionList = [new Regex
                {
                    RegexString = "^test-.*",
                }],
            });
            regexPatternSetId = resp.Summary?.Id;
        }));

        results.Add(await runner.RunTestAsync("waf", "GetRegexPatternSet", async () =>
        {
            if (regexPatternSetId == null) throw new Exception("Regex Pattern Set ID not available");
            var resp = await wafClient.GetRegexPatternSetAsync(new GetRegexPatternSetRequest
            {
                Name = regexPatternSetName,
                Scope = scope,
                Id = regexPatternSetId,
            });
            regexPatternSetLockToken = resp.LockToken;
        }));

        results.Add(await runner.RunTestAsync("waf", "ListRegexPatternSets", async () =>
        {
            await wafClient.ListRegexPatternSetsAsync(new ListRegexPatternSetsRequest { Scope = scope });
        }));

        results.Add(await runner.RunTestAsync("waf", "DeleteRegexPatternSet", async () =>
        {
            if (regexPatternSetId == null) throw new Exception("Regex Pattern Set ID not available");
            var getResp = await wafClient.GetRegexPatternSetAsync(new GetRegexPatternSetRequest
            {
                Name = regexPatternSetName,
                Scope = scope,
                Id = regexPatternSetId,
            });
            await wafClient.DeleteRegexPatternSetAsync(new DeleteRegexPatternSetRequest
            {
                Name = regexPatternSetName,
                Scope = scope,
                Id = regexPatternSetId,
                LockToken = getResp.LockToken ?? regexPatternSetLockToken,
            });
        }));

        var ruleGroupName = TestRunner.MakeUniqueName("test-rulegroup");
        string? ruleGroupId = null;
        string? ruleGroupLockToken = null;

        results.Add(await runner.RunTestAsync("waf", "CreateRuleGroup", async () =>
        {
            var visibilityConfig = new VisibilityConfig
            {
                SampledRequestsEnabled = true,
                CloudWatchMetricsEnabled = true,
                MetricName = "test-rulegroup-metric",
            };
            var resp = await wafClient.CreateRuleGroupAsync(new CreateRuleGroupRequest
            {
                Name = ruleGroupName,
                Description = "Test Rule Group",
                Scope = scope,
                Capacity = 10,
                Rules = [new Rule
                {
                    Name = "test-rule",
                    Priority = 1,
                    Action = new RuleAction { Allow = new AllowAction() },
                    Statement = new Statement
                    {
                        ByteMatchStatement = new ByteMatchStatement
                        {
                            FieldToMatch = new FieldToMatch { UriPath = new UriPath() },
                            PositionalConstraint = PositionalConstraint.STARTS_WITH,
                            SearchString = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test")),
                            TextTransformations = [new TextTransformation
                            {
                                Priority = 0,
                                Type = TextTransformationType.NONE,
                            }],
                        },
                    },
                    VisibilityConfig = new VisibilityConfig
                    {
                        SampledRequestsEnabled = true,
                        CloudWatchMetricsEnabled = true,
                        MetricName = "test-rule-metric",
                    },
                }],
                VisibilityConfig = visibilityConfig,
            });
            ruleGroupId = resp.Summary?.Id;
        }));

        results.Add(await runner.RunTestAsync("waf", "GetRuleGroup", async () =>
        {
            if (ruleGroupId == null) throw new Exception("Rule Group ID not available");
            var resp = await wafClient.GetRuleGroupAsync(new GetRuleGroupRequest
            {
                Name = ruleGroupName,
                Scope = scope,
                Id = ruleGroupId,
            });
            ruleGroupLockToken = resp.LockToken;
        }));

        results.Add(await runner.RunTestAsync("waf", "ListRuleGroups", async () =>
        {
            await wafClient.ListRuleGroupsAsync(new ListRuleGroupsRequest { Scope = scope });
        }));

        results.Add(await runner.RunTestAsync("waf", "DeleteRuleGroup", async () =>
        {
            if (ruleGroupId == null) throw new Exception("Rule Group ID not available");
            var getResp = await wafClient.GetRuleGroupAsync(new GetRuleGroupRequest
            {
                Name = ruleGroupName,
                Scope = scope,
                Id = ruleGroupId,
            });
            await wafClient.DeleteRuleGroupAsync(new DeleteRuleGroupRequest
            {
                Name = ruleGroupName,
                Scope = scope,
                Id = ruleGroupId,
                LockToken = getResp.LockToken ?? ruleGroupLockToken,
            });
        }));

        results.Add(await runner.RunTestAsync("waf", "ListAvailableManagedRuleGroups", async () =>
        {
            await wafClient.ListAvailableManagedRuleGroupsAsync(new ListAvailableManagedRuleGroupsRequest { Scope = scope });
        }));

        results.Add(await runner.RunTestAsync("waf", "GetIPSet_NonExistent", async () =>
        {
            try
            {
                await wafClient.GetIPSetAsync(new GetIPSetRequest
                {
                    Id = "nonexistent-ipset-xyz",
                    Scope = scope,
                    Name = "nonexistent-ipset-xyz",
                });
                throw new Exception("expected error for non-existent IP set");
            }
            catch (AmazonWAFV2Exception) { }
        }));

        results.Add(await runner.RunTestAsync("waf", "DeleteIPSet_NonExistent", async () =>
        {
            try
            {
                await wafClient.DeleteIPSetAsync(new DeleteIPSetRequest
                {
                    Id = "nonexistent-ipset-xyz",
                    Scope = scope,
                    Name = "nonexistent-ipset-xyz",
                    LockToken = "fake-lock-token",
                });
                throw new Exception("expected error for non-existent IP set");
            }
            catch (AmazonWAFV2Exception) { }
        }));

        results.Add(await runner.RunTestAsync("waf", "GetRegexPatternSet_NonExistent", async () =>
        {
            try
            {
                await wafClient.GetRegexPatternSetAsync(new GetRegexPatternSetRequest
                {
                    Name = "nonexistent-regex-xyz",
                    Scope = scope,
                    Id = "nonexistent-regex-xyz",
                });
                throw new Exception("expected error for non-existent regex pattern set");
            }
            catch (AmazonWAFV2Exception) { }
        }));

        results.Add(await runner.RunTestAsync("waf", "GetRuleGroup_NonExistent", async () =>
        {
            try
            {
                await wafClient.GetRuleGroupAsync(new GetRuleGroupRequest
                {
                    Name = "nonexistent-rulegroup-xyz",
                    Scope = scope,
                    Id = "nonexistent-rulegroup-xyz",
                });
                throw new Exception("expected error for non-existent rule group");
            }
            catch (AmazonWAFV2Exception) { }
        }));

        results.Add(await runner.RunTestAsync("waf", "ListIPSets_ContainsCreated", async () =>
        {
            var listName = TestRunner.MakeUniqueName("verify-ipset");
            var createResp = await wafClient.CreateIPSetAsync(new CreateIPSetRequest
            {
                Name = listName,
                Description = "Verify IP Set",
                Scope = scope,
                IPAddressVersion = IPAddressVersion.IPV4,
                Addresses = ["10.0.0.0/24"],
            });
            var listResp = await wafClient.ListIPSetsAsync(new ListIPSetsRequest { Scope = scope });
            var found = listResp.IPSets?.Any(s => s.Name == listName) ?? false;
            if (!found)
                throw new Exception("created IP set not found in list");
            try
            {
                await wafClient.DeleteIPSetAsync(new DeleteIPSetRequest
                {
                    Id = createResp.Summary?.Id ?? "",
                    Scope = scope,
                    Name = listName,
                    LockToken = createResp.Summary?.LockToken ?? "",
                });
            }
            catch { }
        }));

        return results;
    }
}
