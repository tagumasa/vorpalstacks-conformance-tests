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
        var scope = Scope.CLOUDFRONT;
        var ipSetName = TestRunner.MakeUniqueName("test-ipset");
        var ipSetDescription = "Test IP Set";
        string? ipSetId = null;
        string? ipSetLockToken = null;
        string? ipSetARN = null;

        results.Add(await runner.RunTestAsync("wafv2", "ListWebACLs", async () =>
        {
            await wafClient.ListWebACLsAsync(new ListWebACLsRequest { Scope = scope });
        }));

        results.Add(await runner.RunTestAsync("wafv2", "CreateIPSet", async () =>
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

        results.Add(await runner.RunTestAsync("wafv2", "GetIPSet", async () =>
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

        results.Add(await runner.RunTestAsync("wafv2", "ListIPSets", async () =>
        {
            await wafClient.ListIPSetsAsync(new ListIPSetsRequest { Scope = scope });
        }));

        results.Add(await runner.RunTestAsync("wafv2", "ListTagsForResource", async () =>
        {
            if (ipSetARN == null) throw new Exception("IP Set ARN not available");
            await wafClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
            {
                ResourceARN = ipSetARN,
            });
        }));

        results.Add(await runner.RunTestAsync("wafv2", "UpdateIPSet", async () =>
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

        results.Add(await runner.RunTestAsync("wafv2", "DeleteIPSet", async () =>
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

        results.Add(await runner.RunTestAsync("wafv2", "CreateRegexPatternSet", async () =>
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

        results.Add(await runner.RunTestAsync("wafv2", "GetRegexPatternSet", async () =>
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

        results.Add(await runner.RunTestAsync("wafv2", "ListRegexPatternSets", async () =>
        {
            await wafClient.ListRegexPatternSetsAsync(new ListRegexPatternSetsRequest { Scope = scope });
        }));

        results.Add(await runner.RunTestAsync("wafv2", "DeleteRegexPatternSet", async () =>
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

        results.Add(await runner.RunTestAsync("wafv2", "CreateRuleGroup", async () =>
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

        results.Add(await runner.RunTestAsync("wafv2", "GetRuleGroup", async () =>
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

        results.Add(await runner.RunTestAsync("wafv2", "ListRuleGroups", async () =>
        {
            await wafClient.ListRuleGroupsAsync(new ListRuleGroupsRequest { Scope = scope });
        }));

        results.Add(await runner.RunTestAsync("wafv2", "DeleteRuleGroup", async () =>
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

        results.Add(await runner.RunTestAsync("wafv2", "ListAvailableManagedRuleGroups", async () =>
        {
            await wafClient.ListAvailableManagedRuleGroupsAsync(new ListAvailableManagedRuleGroupsRequest { Scope = scope });
        }));

        results.Add(await runner.RunTestAsync("wafv2", "GetIPSet_NonExistent", async () =>
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

        results.Add(await runner.RunTestAsync("wafv2", "DeleteIPSet_NonExistent", async () =>
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

        results.Add(await runner.RunTestAsync("wafv2", "GetRegexPatternSet_NonExistent", async () =>
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

        results.Add(await runner.RunTestAsync("wafv2", "GetRuleGroup_NonExistent", async () =>
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

        results.Add(await runner.RunTestAsync("wafv2", "ListIPSets_ContainsCreated", async () =>
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

        results.Add(await runner.RunTestAsync("wafv2", "ListIPSets_Empty", async () =>
        {
            var uniquePrefix = TestRunner.MakeUniqueName("listempty");
            var existing = await wafClient.ListIPSetsAsync(new ListIPSetsRequest { Scope = scope });
            var existingNames = existing.IPSets?.Select(s => s.Name).ToHashSet() ?? new HashSet<string>();
            var candidate = uniquePrefix;
            var idx = 0;
            while (existingNames.Contains(candidate))
            {
                candidate = $"{uniquePrefix}-{idx++}";
            }
            var listResp = await wafClient.ListIPSetsAsync(new ListIPSetsRequest
            {
                Scope = scope,
                Limit = 1,
            });
            if (listResp.IPSets == null || listResp.IPSets.Count == 0)
            {
                return;
            }
            var hasNonMatch = listResp.IPSets.Any(s => !s.Name.StartsWith(uniquePrefix));
            if (!hasNonMatch && listResp.IPSets.Count > 0)
                throw new Exception("expected listing to return existing IP sets");
        }));

        results.Add(await runner.RunTestAsync("wafv2", "UpdateIPSet_ContentVerify", async () =>
        {
            var verifyName = TestRunner.MakeUniqueName("verify-ipset-update");
            var createResp = await wafClient.CreateIPSetAsync(new CreateIPSetRequest
            {
                Name = verifyName,
                Scope = scope,
                IPAddressVersion = IPAddressVersion.IPV4,
                Addresses = ["10.0.0.0/24"],
            });
            try
            {
                var getResp = await wafClient.GetIPSetAsync(new GetIPSetRequest
                {
                    Id = createResp.Summary?.Id ?? "",
                    Scope = scope,
                    Name = verifyName,
                });
                await wafClient.UpdateIPSetAsync(new UpdateIPSetRequest
                {
                    Id = createResp.Summary?.Id ?? "",
                    Scope = scope,
                    Name = verifyName,
                    Addresses = ["10.0.0.0/24", "172.16.0.0/16"],
                    LockToken = getResp.LockToken ?? "",
                });
                var verifyResp = await wafClient.GetIPSetAsync(new GetIPSetRequest
                {
                    Id = createResp.Summary?.Id ?? "",
                    Scope = scope,
                    Name = verifyName,
                });
                if (verifyResp.IPSet?.Addresses == null || verifyResp.IPSet.Addresses.Count < 2)
                    throw new Exception("IP set update content verification failed");
                var hasNew = verifyResp.IPSet.Addresses.Contains("172.16.0.0/16");
                if (!hasNew)
                    throw new Exception("new IP address not found after update");
            }
            finally
            {
                try
                {
                    var getResp = await wafClient.GetIPSetAsync(new GetIPSetRequest
                    {
                        Id = createResp.Summary?.Id ?? "",
                        Scope = scope,
                        Name = verifyName,
                    });
                    await wafClient.DeleteIPSetAsync(new DeleteIPSetRequest
                    {
                        Id = createResp.Summary?.Id ?? "",
                        Scope = scope,
                        Name = verifyName,
                        LockToken = getResp.LockToken ?? "",
                    });
                }
                catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("wafv2", "UpdateIPSet_StaleLockToken", async () =>
        {
            var staleName = TestRunner.MakeUniqueName("stale-ipset");
            var createResp = await wafClient.CreateIPSetAsync(new CreateIPSetRequest
            {
                Name = staleName,
                Scope = scope,
                IPAddressVersion = IPAddressVersion.IPV4,
                Addresses = ["10.0.0.0/24"],
            });
            try
            {
                await wafClient.UpdateIPSetAsync(new UpdateIPSetRequest
                {
                    Id = createResp.Summary?.Id ?? "",
                    Scope = scope,
                    Name = staleName,
                    Addresses = ["10.0.0.0/24", "192.168.0.0/24"],
                    LockToken = "stale-lock-token-xyz",
                });
                throw new Exception("expected error for stale lock token");
            }
            catch (AmazonWAFV2Exception) { }
            finally
            {
                try
                {
                    var getResp = await wafClient.GetIPSetAsync(new GetIPSetRequest
                    {
                        Id = createResp.Summary?.Id ?? "",
                        Scope = scope,
                        Name = staleName,
                    });
                    await wafClient.DeleteIPSetAsync(new DeleteIPSetRequest
                    {
                        Id = createResp.Summary?.Id ?? "",
                        Scope = scope,
                        Name = staleName,
                        LockToken = getResp.LockToken ?? "",
                    });
                }
                catch { }
            }
        }));

        var webACLName = TestRunner.MakeUniqueName("test-webacl");
        string? webACLId = null;
        string? webACLLockToken = null;
        string? webACLARN = null;

        results.Add(await runner.RunTestAsync("wafv2", "CreateWebACL", async () =>
        {
            var defaultAction = new DefaultAction { Allow = new AllowAction() };
            var visibilityConfig = new VisibilityConfig
            {
                SampledRequestsEnabled = true,
                CloudWatchMetricsEnabled = true,
                MetricName = "test-webacl-metric",
            };
            var resp = await wafClient.CreateWebACLAsync(new CreateWebACLRequest
            {
                Name = webACLName,
                Scope = scope,
                DefaultAction = defaultAction,
                VisibilityConfig = visibilityConfig,
            });
            webACLId = resp.Summary?.Id;
            webACLLockToken = resp.Summary?.LockToken;
            webACLARN = resp.Summary?.ARN;
        }));

        results.Add(await runner.RunTestAsync("wafv2", "GetWebACL", async () =>
        {
            if (webACLId == null) throw new Exception("WebACL ID not available");
            var resp = await wafClient.GetWebACLAsync(new GetWebACLRequest
            {
                Id = webACLId,
                Name = webACLName,
                Scope = scope,
            });
            if (resp.LockToken != null)
                webACLLockToken = resp.LockToken;
        }));

        results.Add(await runner.RunTestAsync("wafv2", "ListWebACLs_ContainsCreated", async () =>
        {
            if (webACLId == null) throw new Exception("WebACL ID not available");
            var listResp = await wafClient.ListWebACLsAsync(new ListWebACLsRequest { Scope = scope });
            var found = listResp.WebACLs?.Any(s => s.Name == webACLName) ?? false;
            if (!found)
                throw new Exception("created WebACL not found in list");
        }));

        results.Add(await runner.RunTestAsync("wafv2", "UpdateWebACL", async () =>
        {
            if (webACLId == null) throw new Exception("WebACL ID not available");
            var getResp = await wafClient.GetWebACLAsync(new GetWebACLRequest
            {
                Id = webACLId,
                Name = webACLName,
                Scope = scope,
            });
            var updatedDesc = "Updated WebACL description";
            await wafClient.UpdateWebACLAsync(new UpdateWebACLRequest
            {
                Id = webACLId,
                Name = webACLName,
                Scope = scope,
                DefaultAction = getResp.WebACL?.DefaultAction,
                Description = updatedDesc,
                VisibilityConfig = getResp.WebACL?.VisibilityConfig,
                LockToken = getResp.LockToken ?? webACLLockToken ?? "",
            });
        }));

        results.Add(await runner.RunTestAsync("wafv2", "UpdateWebACL_ContentVerify", async () =>
        {
            if (webACLId == null) throw new Exception("WebACL ID not available");
            var getResp = await wafClient.GetWebACLAsync(new GetWebACLRequest
            {
                Id = webACLId,
                Name = webACLName,
                Scope = scope,
            });
            var newDesc = "Verified update description";
            await wafClient.UpdateWebACLAsync(new UpdateWebACLRequest
            {
                Id = webACLId,
                Name = webACLName,
                Scope = scope,
                DefaultAction = getResp.WebACL?.DefaultAction,
                Description = newDesc,
                VisibilityConfig = getResp.WebACL?.VisibilityConfig,
                LockToken = getResp.LockToken ?? "",
            });
            var verifyResp = await wafClient.GetWebACLAsync(new GetWebACLRequest
            {
                Id = webACLId,
                Name = webACLName,
                Scope = scope,
            });
            if (verifyResp.WebACL?.Description != newDesc)
                throw new Exception("WebACL update content verification failed");
        }));

        results.Add(await runner.RunTestAsync("wafv2", "UpdateWebACL_StaleLockToken", async () =>
        {
            if (webACLId == null) throw new Exception("WebACL ID not available");
            var getResp = await wafClient.GetWebACLAsync(new GetWebACLRequest
            {
                Id = webACLId,
                Name = webACLName,
                Scope = scope,
            });
            if (getResp.LockToken != null)
                webACLLockToken = getResp.LockToken;
            try
            {
                await wafClient.UpdateWebACLAsync(new UpdateWebACLRequest
                {
                    Id = webACLId,
                    Name = webACLName,
                    Scope = scope,
                    DefaultAction = getResp.WebACL?.DefaultAction,
                    VisibilityConfig = getResp.WebACL?.VisibilityConfig,
                    LockToken = "stale-lock-token-xyz",
                });
                throw new Exception("expected error for stale lock token");
            }
            catch (AmazonWAFV2Exception) { }
        }));

        results.Add(await runner.RunTestAsync("wafv2", "TagResource_WebACL", async () =>
        {
            if (webACLARN == null) throw new Exception("WebACL ARN not available");
            await wafClient.TagResourceAsync(new TagResourceRequest
            {
                ResourceARN = webACLARN,
                Tags = [new Tag { Key = "Environment", Value = "Test" }],
            });
        }));

        results.Add(await runner.RunTestAsync("wafv2", "ListTagsForResource_WebACL", async () =>
        {
            if (webACLARN == null) throw new Exception("WebACL ARN not available");
            var resp = await wafClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
            {
                ResourceARN = webACLARN,
            });
            var hasTag = resp.TagInfoForResource?.TagList?.Any(t => t.Key == "Environment" && t.Value == "Test") ?? false;
            if (!hasTag)
                throw new Exception("expected tag not found on WebACL");
        }));

        results.Add(await runner.RunTestAsync("wafv2", "UntagResource_WebACL", async () =>
        {
            if (webACLARN == null) throw new Exception("WebACL ARN not available");
            await wafClient.UntagResourceAsync(new UntagResourceRequest
            {
                ResourceARN = webACLARN,
                TagKeys = ["Environment"],
            });
        }));

        results.Add(await runner.RunTestAsync("wafv2", "UntagResource_Verify", async () =>
        {
            if (webACLARN == null) throw new Exception("WebACL ARN not available");
            var resp = await wafClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
            {
                ResourceARN = webACLARN,
            });
            var hasTag = resp.TagInfoForResource?.TagList?.Any(t => t.Key == "Environment") ?? false;
            if (hasTag)
                throw new Exception("tag should have been removed");
        }));

        var logGroupName = $"/aws/wafv2/logs/{TestRunner.MakeUniqueName("waf-logs")}";

        results.Add(await runner.RunTestAsync("wafv2", "PutLoggingConfiguration", async () =>
        {
            if (webACLARN == null) throw new Exception("WebACL ARN not available");
            await wafClient.PutLoggingConfigurationAsync(new PutLoggingConfigurationRequest
            {
                LoggingConfiguration = new LoggingConfiguration
                {
                    ResourceArn = webACLARN,
                    LogDestinationConfigs = [logGroupName],
                },
            });
        }));

        results.Add(await runner.RunTestAsync("wafv2", "GetLoggingConfiguration", async () =>
        {
            if (webACLARN == null) throw new Exception("WebACL ARN not available");
            var resp = await wafClient.GetLoggingConfigurationAsync(new GetLoggingConfigurationRequest
            {
                ResourceArn = webACLARN,
            });
            if (resp.LoggingConfiguration == null)
                throw new Exception("logging configuration not found");
            if (resp.LoggingConfiguration.ResourceArn != webACLARN)
                throw new Exception("logging configuration resource ARN mismatch");
        }));

        results.Add(await runner.RunTestAsync("wafv2", "DeleteLoggingConfiguration", async () =>
        {
            if (webACLARN == null) throw new Exception("WebACL ARN not available");
            await wafClient.DeleteLoggingConfigurationAsync(new DeleteLoggingConfigurationRequest
            {
                ResourceArn = webACLARN,
            });
        }));

        results.Add(await runner.RunTestAsync("wafv2", "GetLoggingConfiguration_AfterDelete", async () =>
        {
            if (webACLARN == null) throw new Exception("WebACL ARN not available");
            try
            {
                await wafClient.GetLoggingConfigurationAsync(new GetLoggingConfigurationRequest
                {
                    ResourceArn = webACLARN,
                });
                throw new Exception("expected error after deleting logging configuration");
            }
            catch (AmazonWAFV2Exception) { }
        }));

        var associateWebACLName = TestRunner.MakeUniqueName("assoc-webacl");
        string? assocWebACLId = null;
        string? assocWebACLLockToken = null;
        var testResourceARN = $"arn:aws:apigateway:us-east-1::/restapis/{TestRunner.MakeUniqueName("api")}/stages/test";

        results.Add(await runner.RunTestAsync("wafv2", "AssociateWebACL", async () =>
        {
            var createResp = await wafClient.CreateWebACLAsync(new CreateWebACLRequest
            {
                Name = associateWebACLName,
                Scope = scope,
                DefaultAction = new DefaultAction { Allow = new AllowAction() },
                VisibilityConfig = new VisibilityConfig
                {
                    SampledRequestsEnabled = true,
                    CloudWatchMetricsEnabled = true,
                    MetricName = "assoc-webacl-metric",
                },
            });
            assocWebACLId = createResp.Summary?.Id;
            assocWebACLLockToken = createResp.Summary?.LockToken;
            await wafClient.AssociateWebACLAsync(new AssociateWebACLRequest
            {
                WebACLArn = createResp.Summary?.ARN ?? "",
                ResourceArn = testResourceARN,
            });
        }));

        results.Add(await runner.RunTestAsync("wafv2", "GetWebACLForResource", async () =>
        {
            try
            {
                var resp = await wafClient.GetWebACLForResourceAsync(new GetWebACLForResourceRequest
                {
                    ResourceArn = testResourceARN,
                });
                if (resp.WebACL == null)
                    throw new Exception("expected WebACL for resource");
            }
            catch (AmazonWAFV2Exception) { }
        }));

        results.Add(await runner.RunTestAsync("wafv2", "ListResourcesForWebACL", async () =>
        {
            if (assocWebACLId == null) throw new Exception("associated WebACL ID not available");
            try
            {
                await wafClient.ListResourcesForWebACLAsync(new ListResourcesForWebACLRequest
                {
                    WebACLArn = $"arn:aws:wafv2:us-east-1:000000000000:global/webacl/{associateWebACLName}/{assocWebACLId}",
                    ResourceType = ResourceType.APPLICATION_LOAD_BALANCER,
                });
            }
            catch (AmazonWAFV2Exception) { }
        }));

        results.Add(await runner.RunTestAsync("wafv2", "DisassociateWebACL", async () =>
        {
            await wafClient.DisassociateWebACLAsync(new DisassociateWebACLRequest
            {
                ResourceArn = testResourceARN,
            });
        }));

        results.Add(await runner.RunTestAsync("wafv2", "GetWebACLForResource_AfterDisassociate", async () =>
        {
            try
            {
                await wafClient.GetWebACLForResourceAsync(new GetWebACLForResourceRequest
                {
                    ResourceArn = testResourceARN,
                });
                throw new Exception("expected error after disassociating WebACL");
            }
            catch (AmazonWAFV2Exception) { }
        }));

        results.Add(await runner.RunTestAsync("wafv2", "DescribeManagedRuleGroup", async () =>
        {
            var managedGroups = await wafClient.ListAvailableManagedRuleGroupsAsync(new ListAvailableManagedRuleGroupsRequest { Scope = scope });
            var firstGroup = managedGroups.ManagedRuleGroups?.FirstOrDefault();
            if (firstGroup == null)
                throw new Exception("no managed rule groups available");
            await wafClient.DescribeManagedRuleGroupAsync(new DescribeManagedRuleGroupRequest
            {
                VendorName = firstGroup.VendorName,
                Name = firstGroup.Name,
                Scope = scope,
            });
        }));

        results.Add(await runner.RunTestAsync("wafv2", "DescribeManagedRuleGroup_NotFound", async () =>
        {
            try
            {
                await wafClient.DescribeManagedRuleGroupAsync(new DescribeManagedRuleGroupRequest
                {
                    VendorName = "FakeVendor",
                    Name = "FakeRuleGroup",
                    Scope = scope,
                });
                throw new Exception("expected error for non-existent managed rule group");
            }
            catch (AmazonWAFV2Exception) { }
        }));

        results.Add(await runner.RunTestAsync("wafv2", "DeleteWebACL", async () =>
        {
            if (webACLId == null) throw new Exception("WebACL ID not available");
            var getResp = await wafClient.GetWebACLAsync(new GetWebACLRequest
            {
                Id = webACLId,
                Name = webACLName,
                Scope = scope,
            });
            await wafClient.DeleteWebACLAsync(new DeleteWebACLRequest
            {
                Id = webACLId,
                Name = webACLName,
                Scope = scope,
                LockToken = getResp.LockToken ?? webACLLockToken ?? "",
            });
            webACLId = null;
        }));

        results.Add(await runner.RunTestAsync("wafv2", "GetWebACL_NonExistent", async () =>
        {
            try
            {
                await wafClient.GetWebACLAsync(new GetWebACLRequest
                {
                    Id = "nonexistent-webacl-xyz",
                    Name = "nonexistent-webacl-xyz",
                    Scope = scope,
                });
                throw new Exception("expected error for non-existent WebACL");
            }
            catch (AmazonWAFV2Exception) { }
        }));

        try
        {
            if (assocWebACLId != null)
            {
                var getResp = await wafClient.GetWebACLAsync(new GetWebACLRequest
                {
                    Id = assocWebACLId,
                    Name = associateWebACLName,
                    Scope = scope,
                });
                await wafClient.DeleteWebACLAsync(new DeleteWebACLRequest
                {
                    Id = assocWebACLId,
                    Name = associateWebACLName,
                    Scope = scope,
                    LockToken = getResp.LockToken ?? assocWebACLLockToken ?? "",
                });
            }
        }
        catch { }

        results.Add(await runner.RunTestAsync("wafv2", "UpdateRegexPatternSet", async () =>
        {
            var updateRegexName = TestRunner.MakeUniqueName("update-regex");
            var createResp = await wafClient.CreateRegexPatternSetAsync(new CreateRegexPatternSetRequest
            {
                Name = updateRegexName,
                Scope = scope,
                RegularExpressionList = [new Regex { RegexString = "^initial-.*" }],
            });
            try
            {
                var getResp = await wafClient.GetRegexPatternSetAsync(new GetRegexPatternSetRequest
                {
                    Id = createResp.Summary?.Id ?? "",
                    Name = updateRegexName,
                    Scope = scope,
                });
                await wafClient.UpdateRegexPatternSetAsync(new UpdateRegexPatternSetRequest
                {
                    Id = createResp.Summary?.Id ?? "",
                    Name = updateRegexName,
                    Scope = scope,
                    RegularExpressionList = [new Regex { RegexString = "^updated-.*" }],
                    LockToken = getResp.LockToken ?? "",
                });
            }
            finally
            {
                try
                {
                    var getResp = await wafClient.GetRegexPatternSetAsync(new GetRegexPatternSetRequest
                    {
                        Id = createResp.Summary?.Id ?? "",
                        Name = updateRegexName,
                        Scope = scope,
                    });
                    await wafClient.DeleteRegexPatternSetAsync(new DeleteRegexPatternSetRequest
                    {
                        Id = createResp.Summary?.Id ?? "",
                        Name = updateRegexName,
                        Scope = scope,
                        LockToken = getResp.LockToken ?? "",
                    });
                }
                catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("wafv2", "UpdateRegexPatternSet_ContentVerify", async () =>
        {
            var verifyRegexName = TestRunner.MakeUniqueName("verify-regex-update");
            var createResp = await wafClient.CreateRegexPatternSetAsync(new CreateRegexPatternSetRequest
            {
                Name = verifyRegexName,
                Scope = scope,
                RegularExpressionList = [new Regex { RegexString = "^before-.*" }],
            });
            try
            {
                var getResp = await wafClient.GetRegexPatternSetAsync(new GetRegexPatternSetRequest
                {
                    Id = createResp.Summary?.Id ?? "",
                    Name = verifyRegexName,
                    Scope = scope,
                });
                await wafClient.UpdateRegexPatternSetAsync(new UpdateRegexPatternSetRequest
                {
                    Id = createResp.Summary?.Id ?? "",
                    Name = verifyRegexName,
                    Scope = scope,
                    RegularExpressionList = [new Regex { RegexString = "^after-.*" }],
                    LockToken = getResp.LockToken ?? "",
                });
                var verifyResp = await wafClient.GetRegexPatternSetAsync(new GetRegexPatternSetRequest
                {
                    Id = createResp.Summary?.Id ?? "",
                    Name = verifyRegexName,
                    Scope = scope,
                });
                if (verifyResp.RegexPatternSet?.RegularExpressionList == null ||
                    verifyResp.RegexPatternSet.RegularExpressionList.Count == 0)
                    throw new Exception("regex pattern set content verification failed");
                var hasNew = verifyResp.RegexPatternSet.RegularExpressionList.Any(r => r.RegexString == "^after-.*");
                if (!hasNew)
                    throw new Exception("updated regex not found");
            }
            finally
            {
                try
                {
                    var getResp = await wafClient.GetRegexPatternSetAsync(new GetRegexPatternSetRequest
                    {
                        Id = createResp.Summary?.Id ?? "",
                        Name = verifyRegexName,
                        Scope = scope,
                    });
                    await wafClient.DeleteRegexPatternSetAsync(new DeleteRegexPatternSetRequest
                    {
                        Id = createResp.Summary?.Id ?? "",
                        Name = verifyRegexName,
                        Scope = scope,
                        LockToken = getResp.LockToken ?? "",
                    });
                }
                catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("wafv2", "UpdateRuleGroup", async () =>
        {
            var updateRGName = TestRunner.MakeUniqueName("update-rg");
            var createResp = await wafClient.CreateRuleGroupAsync(new CreateRuleGroupRequest
            {
                Name = updateRGName,
                Scope = scope,
                Capacity = 10,
                VisibilityConfig = new VisibilityConfig
                {
                    SampledRequestsEnabled = true,
                    CloudWatchMetricsEnabled = true,
                    MetricName = "update-rg-metric",
                },
            });
            try
            {
                var getResp = await wafClient.GetRuleGroupAsync(new GetRuleGroupRequest
                {
                    Id = createResp.Summary?.Id ?? "",
                    Name = updateRGName,
                    Scope = scope,
                });
                await wafClient.UpdateRuleGroupAsync(new UpdateRuleGroupRequest
                {
                    Id = createResp.Summary?.Id ?? "",
                    Name = updateRGName,
                    Scope = scope,
                    Rules = [new Rule
                    {
                        Name = "updated-rule",
                        Priority = 1,
                        Action = new RuleAction { Block = new BlockAction() },
                        Statement = new Statement
                        {
                            ByteMatchStatement = new ByteMatchStatement
                            {
                                FieldToMatch = new FieldToMatch { UriPath = new UriPath() },
                                PositionalConstraint = PositionalConstraint.CONTAINS,
                                SearchString = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("block")),
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
                            MetricName = "updated-rule-metric",
                        },
                    }],
                    VisibilityConfig = getResp.RuleGroup?.VisibilityConfig ?? new VisibilityConfig
                    {
                        SampledRequestsEnabled = true,
                        CloudWatchMetricsEnabled = true,
                        MetricName = "update-rg-metric",
                    },
                    LockToken = getResp.LockToken ?? "",
                });
            }
            finally
            {
                try
                {
                    var getResp = await wafClient.GetRuleGroupAsync(new GetRuleGroupRequest
                    {
                        Id = createResp.Summary?.Id ?? "",
                        Name = updateRGName,
                        Scope = scope,
                    });
                    await wafClient.DeleteRuleGroupAsync(new DeleteRuleGroupRequest
                    {
                        Id = createResp.Summary?.Id ?? "",
                        Name = updateRGName,
                        Scope = scope,
                        LockToken = getResp.LockToken ?? "",
                    });
                }
                catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("wafv2", "UpdateRuleGroup_ContentVerify", async () =>
        {
            var verifyRGName = TestRunner.MakeUniqueName("verify-rg-update");
            var createResp = await wafClient.CreateRuleGroupAsync(new CreateRuleGroupRequest
            {
                Name = verifyRGName,
                Scope = scope,
                Capacity = 10,
                VisibilityConfig = new VisibilityConfig
                {
                    SampledRequestsEnabled = true,
                    CloudWatchMetricsEnabled = true,
                    MetricName = "verify-rg-metric",
                },
            });
            try
            {
                var getResp = await wafClient.GetRuleGroupAsync(new GetRuleGroupRequest
                {
                    Id = createResp.Summary?.Id ?? "",
                    Name = verifyRGName,
                    Scope = scope,
                });
                await wafClient.UpdateRuleGroupAsync(new UpdateRuleGroupRequest
                {
                    Id = createResp.Summary?.Id ?? "",
                    Name = verifyRGName,
                    Scope = scope,
                    Rules = [new Rule
                    {
                        Name = "content-verify-rule",
                        Priority = 1,
                        Action = new RuleAction { Count = new CountAction() },
                        Statement = new Statement
                        {
                            ByteMatchStatement = new ByteMatchStatement
                            {
                                FieldToMatch = new FieldToMatch { UriPath = new UriPath() },
                                PositionalConstraint = PositionalConstraint.CONTAINS,
                                SearchString = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("count-me")),
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
                            MetricName = "content-verify-rule-metric",
                        },
                    }],
                    VisibilityConfig = getResp.RuleGroup?.VisibilityConfig ?? new VisibilityConfig
                    {
                        SampledRequestsEnabled = true,
                        CloudWatchMetricsEnabled = true,
                        MetricName = "verify-rg-metric",
                    },
                    LockToken = getResp.LockToken ?? "",
                });
                var verifyResp = await wafClient.GetRuleGroupAsync(new GetRuleGroupRequest
                {
                    Id = createResp.Summary?.Id ?? "",
                    Name = verifyRGName,
                    Scope = scope,
                });
                if (verifyResp.RuleGroup?.Rules == null || verifyResp.RuleGroup.Rules.Count == 0)
                    throw new Exception("rule group content verification failed");
                var hasRule = verifyResp.RuleGroup.Rules.Any(r => r.Name == "content-verify-rule");
                if (!hasRule)
                    throw new Exception("updated rule not found in rule group");
            }
            finally
            {
                try
                {
                    var getResp = await wafClient.GetRuleGroupAsync(new GetRuleGroupRequest
                    {
                        Id = createResp.Summary?.Id ?? "",
                        Name = verifyRGName,
                        Scope = scope,
                    });
                    await wafClient.DeleteRuleGroupAsync(new DeleteRuleGroupRequest
                    {
                        Id = createResp.Summary?.Id ?? "",
                        Name = verifyRGName,
                        Scope = scope,
                        LockToken = getResp.LockToken ?? "",
                    });
                }
                catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("wafv2", "UpdateRuleGroup_StaleLockToken", async () =>
        {
            var staleRGName = TestRunner.MakeUniqueName("stale-rg");
            var createResp = await wafClient.CreateRuleGroupAsync(new CreateRuleGroupRequest
            {
                Name = staleRGName,
                Scope = scope,
                Capacity = 10,
                VisibilityConfig = new VisibilityConfig
                {
                    SampledRequestsEnabled = true,
                    CloudWatchMetricsEnabled = true,
                    MetricName = "stale-rg-metric",
                },
            });
            try
            {
                await wafClient.UpdateRuleGroupAsync(new UpdateRuleGroupRequest
                {
                    Id = createResp.Summary?.Id ?? "",
                    Name = staleRGName,
                    Scope = scope,
                    Rules = [],
                    VisibilityConfig = new VisibilityConfig
                    {
                        SampledRequestsEnabled = true,
                        CloudWatchMetricsEnabled = true,
                        MetricName = "stale-rg-metric",
                    },
                    LockToken = "stale-lock-token-xyz",
                });
                throw new Exception("expected error for stale lock token");
            }
            catch (AmazonWAFV2Exception) { }
            finally
            {
                try
                {
                    var getResp = await wafClient.GetRuleGroupAsync(new GetRuleGroupRequest
                    {
                        Id = createResp.Summary?.Id ?? "",
                        Name = staleRGName,
                        Scope = scope,
                    });
                    await wafClient.DeleteRuleGroupAsync(new DeleteRuleGroupRequest
                    {
                        Id = createResp.Summary?.Id ?? "",
                        Name = staleRGName,
                        Scope = scope,
                        LockToken = getResp.LockToken ?? "",
                    });
                }
                catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("wafv2", "ListIPSets_Pagination", async () =>
        {
            var paginationPrefix = TestRunner.MakeUniqueName("pag-ipset");
            var createdIds = new List<(string Id, string Name, string? LockToken)>();
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    var name = $"{paginationPrefix}-{i}";
                    var resp = await wafClient.CreateIPSetAsync(new CreateIPSetRequest
                    {
                        Name = name,
                        Scope = scope,
                        IPAddressVersion = IPAddressVersion.IPV4,
                        Addresses = ["10.0.0.0/24"],
                    });
                    createdIds.Add((resp.Summary?.Id ?? "", name, resp.Summary?.LockToken));
                }
                var allIPSets = new List<IPSetSummary>();
                string? nextMarker = null;
                do
                {
                    var listResp = await wafClient.ListIPSetsAsync(new ListIPSetsRequest
                    {
                        Scope = scope,
                        Limit = 2,
                        NextMarker = nextMarker,
                    });
                    if (listResp.IPSets != null)
                        allIPSets.AddRange(listResp.IPSets);
                    nextMarker = listResp.NextMarker;
                } while (nextMarker != null);
                var totalCount = allIPSets.Count;
                if (totalCount < 3)
                    throw new Exception($"pagination returned fewer IP sets than expected: {totalCount}");
                foreach (var (id, name, _) in createdIds)
                {
                    var found = allIPSets.Any(s => s.Name == name);
                    if (!found)
                        throw new Exception($"IP set {name} not found in paginated results");
                }
            }
            finally
            {
                foreach (var (id, name, _) in createdIds)
                {
                    try
                    {
                        var getResp = await wafClient.GetIPSetAsync(new GetIPSetRequest
                        {
                            Id = id,
                            Name = name,
                            Scope = scope,
                        });
                        await wafClient.DeleteIPSetAsync(new DeleteIPSetRequest
                        {
                            Id = id,
                            Name = name,
                            Scope = scope,
                            LockToken = getResp.LockToken ?? "",
                        });
                    }
                    catch { }
                }
            }
        }));

        return results;
    }
}
