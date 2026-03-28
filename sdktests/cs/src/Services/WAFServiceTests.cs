using Amazon.WAF;
using Amazon.WAF.Model;
using Amazon.Runtime;
using System.Linq;

namespace VorpalStacks.SDK.Tests.Services;

public static class WAFServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonWAFClient wafClient,
        string region)
    {
        var results = new List<TestResult>();
        var ipSetName = TestRunner.MakeUniqueName("test-ipset");
        var ruleName = TestRunner.MakeUniqueName("test-rule");
        var webACLName = TestRunner.MakeUniqueName("test-webacl");
        string? ipSetId = null;
        string? ruleId = null;
        string? webACLId = null;

        try
        {
            results.Add(await runner.RunTestAsync("waf", "GetChangeToken", async () =>
            {
                var resp = await wafClient.GetChangeTokenAsync(new GetChangeTokenRequest());
                if (string.IsNullOrEmpty(resp.ChangeToken))
                    throw new Exception("ChangeToken is null");
            }));

            results.Add(await runner.RunTestAsync("waf", "ListWebACLs", async () =>
            {
                var resp = await wafClient.ListWebACLsAsync(new ListWebACLsRequest { Limit = 10 });
                if (resp.WebACLs == null)
                    throw new Exception("WebACLs is null");
            }));

            results.Add(await runner.RunTestAsync("waf", "CreateIPSet", async () =>
            {
                var ctResp = await wafClient.GetChangeTokenAsync(new GetChangeTokenRequest());
                var resp = await wafClient.CreateIPSetAsync(new CreateIPSetRequest
                {
                    Name = ipSetName,
                    ChangeToken = ctResp.ChangeToken
                });
                ipSetId = resp.IPSet.IPSetId;
            }));

            results.Add(await runner.RunTestAsync("waf", "GetIPSet", async () =>
            {
                if (ipSetId == null) throw new Exception("IP Set ID not available");
                var resp = await wafClient.GetIPSetAsync(new GetIPSetRequest { IPSetId = ipSetId });
                if (resp.IPSet == null)
                    throw new Exception("IP set is nil");
            }));

            results.Add(await runner.RunTestAsync("waf", "ListIPSets", async () =>
            {
                var resp = await wafClient.ListIPSetsAsync(new ListIPSetsRequest { Limit = 10 });
                if (resp.IPSets == null)
                    throw new Exception("IP sets list is nil");
            }));

            results.Add(await runner.RunTestAsync("waf", "UpdateIPSet", async () =>
            {
                if (ipSetId == null) throw new Exception("IP Set ID not available");
                var ctResp = await wafClient.GetChangeTokenAsync(new GetChangeTokenRequest());
                await wafClient.UpdateIPSetAsync(new UpdateIPSetRequest
                {
                    IPSetId = ipSetId,
                    ChangeToken = ctResp.ChangeToken,
                    Updates = new List<IPSetUpdate>
                    {
                        new IPSetUpdate
                        {
                            Action = ChangeAction.INSERT,
                            IPSetDescriptor = new IPSetDescriptor
                            {
                                Type = "IPV4",
                                Value = "192.0.2.0/24"
                            }
                        }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("waf", "DeleteIPSet", async () =>
            {
                if (ipSetId == null) throw new Exception("IP Set ID not available");
                var ctResp = await wafClient.GetChangeTokenAsync(new GetChangeTokenRequest());
                await wafClient.DeleteIPSetAsync(new DeleteIPSetRequest
                {
                    IPSetId = ipSetId,
                    ChangeToken = ctResp.ChangeToken
                });
                ipSetId = null;
            }));

            results.Add(await runner.RunTestAsync("waf", "CreateRule", async () =>
            {
                var ctResp = await wafClient.GetChangeTokenAsync(new GetChangeTokenRequest());
                var resp = await wafClient.CreateRuleAsync(new CreateRuleRequest
                {
                    Name = ruleName,
                    MetricName = ruleName,
                    ChangeToken = ctResp.ChangeToken
                });
                ruleId = resp.Rule.RuleId;
            }));

            results.Add(await runner.RunTestAsync("waf", "GetRule", async () =>
            {
                if (ruleId == null) throw new Exception("Rule ID not available");
                var resp = await wafClient.GetRuleAsync(new GetRuleRequest { RuleId = ruleId });
                if (resp.Rule == null)
                    throw new Exception("Rule is nil");
            }));

            results.Add(await runner.RunTestAsync("waf", "ListRules", async () =>
            {
                var resp = await wafClient.ListRulesAsync(new ListRulesRequest { Limit = 10 });
                if (resp.Rules == null)
                    throw new Exception("Rules is null");
            }));

            results.Add(await runner.RunTestAsync("waf", "UpdateRule", async () =>
            {
                if (ruleId == null) throw new Exception("Rule ID not available");
                var ctResp = await wafClient.GetChangeTokenAsync(new GetChangeTokenRequest());
                await wafClient.UpdateRuleAsync(new UpdateRuleRequest
                {
                    RuleId = ruleId,
                    ChangeToken = ctResp.ChangeToken,
                    Updates = new List<RuleUpdate>
                    {
                        new RuleUpdate
                        {
                            Action = ChangeAction.INSERT,
                            Predicate = new Predicate
                            {
                                Negated = false,
                                Type = "IPMatch",
                                DataId = "test-data-id"
                            }
                        }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("waf", "DeleteRule", async () =>
            {
                if (ruleId == null) throw new Exception("Rule ID not available");
                var ctResp = await wafClient.GetChangeTokenAsync(new GetChangeTokenRequest());
                await wafClient.DeleteRuleAsync(new DeleteRuleRequest
                {
                    RuleId = ruleId,
                    ChangeToken = ctResp.ChangeToken
                });
                ruleId = null;
            }));

            results.Add(await runner.RunTestAsync("waf", "CreateWebACL", async () =>
            {
                var ctResp = await wafClient.GetChangeTokenAsync(new GetChangeTokenRequest());
                var resp = await wafClient.CreateWebACLAsync(new CreateWebACLRequest
                {
                    Name = webACLName,
                    MetricName = webACLName,
                    DefaultAction = new WafAction { Type = "ALLOW" },
                    ChangeToken = ctResp.ChangeToken
                });
                webACLId = resp.WebACL.WebACLId;
            }));

            results.Add(await runner.RunTestAsync("waf", "GetWebACL", async () =>
            {
                if (webACLId == null) throw new Exception("WebACL ID not available");
                var resp = await wafClient.GetWebACLAsync(new GetWebACLRequest { WebACLId = webACLId });
                if (resp.WebACL == null)
                    throw new Exception("WebACL is nil");
            }));

            results.Add(await runner.RunTestAsync("waf", "UpdateWebACL", async () =>
            {
                if (webACLId == null) throw new Exception("WebACL ID not available");
                var ctResp = await wafClient.GetChangeTokenAsync(new GetChangeTokenRequest());
                await wafClient.UpdateWebACLAsync(new UpdateWebACLRequest
                {
                    WebACLId = webACLId,
                    ChangeToken = ctResp.ChangeToken,
                    DefaultAction = new WafAction { Type = "BLOCK" },
                    Updates = new List<WebACLUpdate>
                    {
                        new WebACLUpdate
                        {
                            Action = ChangeAction.INSERT,
                            ActivatedRule = new ActivatedRule
                            {
                                Priority = 1,
                                RuleId = "test-rule",
                                Action = new WafAction { Type = "BLOCK" }
                            }
                        }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("waf", "DeleteWebACL", async () =>
            {
                if (webACLId == null) throw new Exception("WebACL ID not available");
                var ctResp = await wafClient.GetChangeTokenAsync(new GetChangeTokenRequest());
                await wafClient.DeleteWebACLAsync(new DeleteWebACLRequest
                {
                    WebACLId = webACLId,
                    ChangeToken = ctResp.ChangeToken
                });
                webACLId = null;
            }));

            results.Add(await runner.RunTestAsync("waf", "ListRuleGroups", async () =>
            {
                var resp = await wafClient.ListRuleGroupsAsync(new ListRuleGroupsRequest { Limit = 10 });
                if (resp.RuleGroups == null)
                    throw new Exception("RuleGroups is nil");
            }));

            results.Add(await runner.RunTestAsync("waf", "ListRateBasedRules", async () =>
            {
                var resp = await wafClient.ListRateBasedRulesAsync(new ListRateBasedRulesRequest { Limit = 10 });
                if (resp.Rules == null)
                    throw new Exception("Rules is null");
            }));

            results.Add(await runner.RunTestAsync("waf", "GetSampledRequests", async () =>
            {
                try
                {
                    await wafClient.GetSampledRequestsAsync(new GetSampledRequestsRequest
                    {
                        MaxItems = 10,
                        TimeWindow = new TimeWindow
                        {
                            StartTime = DateTime.UtcNow.AddHours(-1),
                            EndTime = DateTime.UtcNow
                        }
                    });
                }
                catch (AmazonWAFException) { }
            }));
        }
        finally
        {
            if (webACLId != null)
                try { var ct = await wafClient.GetChangeTokenAsync(new GetChangeTokenRequest()); await wafClient.DeleteWebACLAsync(new DeleteWebACLRequest { WebACLId = webACLId, ChangeToken = ct.ChangeToken }); } catch { }
            if (ruleId != null)
                try { var ct = await wafClient.GetChangeTokenAsync(new GetChangeTokenRequest()); await wafClient.DeleteRuleAsync(new DeleteRuleRequest { RuleId = ruleId, ChangeToken = ct.ChangeToken }); } catch { }
            if (ipSetId != null)
                try { var ct = await wafClient.GetChangeTokenAsync(new GetChangeTokenRequest()); await wafClient.DeleteIPSetAsync(new DeleteIPSetRequest { IPSetId = ipSetId, ChangeToken = ct.ChangeToken }); } catch { }
        }

        results.Add(await runner.RunTestAsync("waf", "GetIPSet_NonExistent", async () =>
        {
            try
            {
                await wafClient.GetIPSetAsync(new GetIPSetRequest { IPSetId = "nonexistent-ipset-id" });
                throw new Exception("expected error for non-existent IP Set");
            }
            catch (AmazonWAFException) { }
        }));

        results.Add(await runner.RunTestAsync("waf", "GetRule_NonExistent", async () =>
        {
            try
            {
                await wafClient.GetRuleAsync(new GetRuleRequest { RuleId = "nonexistent-rule-id" });
                throw new Exception("expected error for non-existent Rule");
            }
            catch (AmazonWAFException) { }
        }));

        results.Add(await runner.RunTestAsync("waf", "GetWebACL_NonExistent", async () =>
        {
            try
            {
                await wafClient.GetWebACLAsync(new GetWebACLRequest { WebACLId = "nonexistent-webacl-id" });
                throw new Exception("expected error for non-existent WebACL");
            }
            catch (AmazonWAFException) { }
        }));

        return results;
    }
}
