using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class EventBridgeServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonEventBridgeClient eventBridgeClient,
        string region)
    {
        var results = new List<TestResult>();
        var busName = TestRunner.MakeUniqueName("CSBus");
        var ruleName = TestRunner.MakeUniqueName("CSRule");
        var targetID = TestRunner.MakeUniqueName("CSTarget");

        try
        {
            results.Add(await runner.RunTestAsync("events", "CreateEventBus", async () =>
            {
                await eventBridgeClient.CreateEventBusAsync(new CreateEventBusRequest
                {
                    Name = busName
                });
            }));

            results.Add(await runner.RunTestAsync("events", "DescribeEventBus", async () =>
            {
                var resp = await eventBridgeClient.DescribeEventBusAsync(new DescribeEventBusRequest
                {
                    Name = busName
                });
                if (resp.Name == null)
                    throw new Exception("event bus name is nil");
            }));

            results.Add(await runner.RunTestAsync("events", "ListEventBuses", async () =>
            {
                var resp = await eventBridgeClient.ListEventBusesAsync(new ListEventBusesRequest());
                if (resp.EventBuses == null)
                    throw new Exception("event buses list is nil");
            }));

            results.Add(await runner.RunTestAsync("events", "PutRule", async () =>
            {
                await eventBridgeClient.PutRuleAsync(new PutRuleRequest
                {
                    Name = ruleName,
                    EventBusName = busName,
                    ScheduleExpression = "rate(5 minutes)",
                    State = RuleState.ENABLED
                });
            }));

            results.Add(await runner.RunTestAsync("events", "DescribeRule", async () =>
            {
                var resp = await eventBridgeClient.DescribeRuleAsync(new DescribeRuleRequest
                {
                    Name = ruleName,
                    EventBusName = busName
                });
                if (resp.Name == null)
                    throw new Exception("rule name is nil");
            }));

            results.Add(await runner.RunTestAsync("events", "ListRules", async () =>
            {
                var resp = await eventBridgeClient.ListRulesAsync(new ListRulesRequest
                {
                    EventBusName = busName
                });
                if (resp.Rules == null)
                    throw new Exception("rules list is nil");
            }));

            results.Add(await runner.RunTestAsync("events", "PutTargets", async () =>
            {
                await eventBridgeClient.PutTargetsAsync(new PutTargetsRequest
                {
                    Rule = ruleName,
                    EventBusName = busName,
                    Targets = new List<Target>
                    {
                        new Target
                        {
                            Id = targetID,
                            Arn = $"arn:aws:lambda:{region}:000000000000:function:TestFunction"
                        }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("events", "ListTargetsByRule", async () =>
            {
                var resp = await eventBridgeClient.ListTargetsByRuleAsync(new ListTargetsByRuleRequest
                {
                    Rule = ruleName,
                    EventBusName = busName
                });
                if (resp.Targets == null)
                    throw new Exception("targets list is nil");
            }));

            results.Add(await runner.RunTestAsync("events", "PutEvents", async () =>
            {
                var evt = System.Text.Json.JsonSerializer.Serialize(new
                {
                    source = "com.example.test",
                    detail_type = "TestEvent",
                    detail = new { message = "test" }
                });
                await eventBridgeClient.PutEventsAsync(new PutEventsRequest
                {
                    Entries = new List<PutEventsRequestEntry>
                    {
                        new PutEventsRequestEntry
                        {
                            Source = "com.example.test",
                            DetailType = "TestEvent",
                            Detail = evt,
                            EventBusName = busName
                        }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("events", "RemoveTargets", async () =>
            {
                await eventBridgeClient.RemoveTargetsAsync(new RemoveTargetsRequest
                {
                    Rule = ruleName,
                    EventBusName = busName,
                    Ids = new List<string> { targetID }
                });
            }));

            results.Add(await runner.RunTestAsync("events", "DisableRule", async () =>
            {
                await eventBridgeClient.DisableRuleAsync(new DisableRuleRequest
                {
                    Name = ruleName,
                    EventBusName = busName
                });
            }));

            results.Add(await runner.RunTestAsync("events", "EnableRule", async () =>
            {
                await eventBridgeClient.EnableRuleAsync(new EnableRuleRequest
                {
                    Name = ruleName,
                    EventBusName = busName
                });
            }));

            results.Add(await runner.RunTestAsync("events", "TagResource", async () =>
            {
                var ruleARN = $"arn:aws:events:{region}:000000000000:rule/{busName}/{ruleName}";
                await eventBridgeClient.TagResourceAsync(new TagResourceRequest
                {
                    ResourceARN = ruleARN,
                    Tags = new List<Tag> { new Tag { Key = "Environment", Value = "test" } }
                });
            }));

            results.Add(await runner.RunTestAsync("events", "ListTagsForResource", async () =>
            {
                var ruleARN = $"arn:aws:events:{region}:000000000000:rule/{busName}/{ruleName}";
                var resp = await eventBridgeClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceARN = ruleARN
                });
                if (resp.Tags == null)
                    throw new Exception("tags list is nil");
            }));

            results.Add(await runner.RunTestAsync("events", "UntagResource", async () =>
            {
                var ruleARN = $"arn:aws:events:{region}:000000000000:rule/{busName}/{ruleName}";
                await eventBridgeClient.UntagResourceAsync(new UntagResourceRequest
                {
                    ResourceARN = ruleARN,
                    TagKeys = new List<string> { "Environment" }
                });
            }));

            results.Add(await runner.RunTestAsync("events", "DeleteRule", async () =>
            {
                await eventBridgeClient.DeleteRuleAsync(new DeleteRuleRequest
                {
                    Name = ruleName,
                    EventBusName = busName
                });
            }));

            results.Add(await runner.RunTestAsync("events", "DeleteEventBus", async () =>
            {
                await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest
                {
                    Name = busName
                });
            }));
        }
        finally
        {
            try { await eventBridgeClient.DeleteRuleAsync(new DeleteRuleRequest { Name = ruleName, EventBusName = busName }); } catch { }
            try { await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest { Name = busName }); } catch { }
        }

        results.Add(await runner.RunTestAsync("events", "DescribeNonExistentRule", async () =>
        {
            try
            {
                await eventBridgeClient.DescribeRuleAsync(new DescribeRuleRequest
                {
                    Name = "NonExistentRule_xyz_12345"
                });
                throw new Exception("Expected error but got none");
            }
            catch (ResourceNotFoundException) { }
        }));

        results.Add(await runner.RunTestAsync("events", "DescribeEventBus_NonExistent", async () =>
        {
            try
            {
                await eventBridgeClient.DescribeEventBusAsync(new DescribeEventBusRequest
                {
                    Name = "nonexistent-bus-xyz-12345"
                });
                throw new Exception("Expected error for non-existent event bus");
            }
            catch (ResourceNotFoundException) { }
        }));

        results.Add(await runner.RunTestAsync("events", "DeleteEventBus_NonExistent", async () =>
        {
            try
            {
                await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest
                {
                    Name = "nonexistent-bus-xyz-12345"
                });
                throw new Exception("Expected error for non-existent event bus");
            }
            catch (AmazonEventBridgeException) { }
        }));

        results.Add(await runner.RunTestAsync("events", "DeleteRule_NonExistent", async () =>
        {
            try
            {
                await eventBridgeClient.DeleteRuleAsync(new DeleteRuleRequest
                {
                    Name = "nonexistent-rule-xyz-12345"
                });
                throw new Exception("Expected error for non-existent rule");
            }
            catch (ResourceNotFoundException) { }
        }));

        results.Add(await runner.RunTestAsync("events", "CreateEventBus_DuplicateName", async () =>
        {
            var dupBus = TestRunner.MakeUniqueName("DupBus");
            await eventBridgeClient.CreateEventBusAsync(new CreateEventBusRequest { Name = dupBus });
            try
            {
                try
                {
                    await eventBridgeClient.CreateEventBusAsync(new CreateEventBusRequest { Name = dupBus });
                    throw new Exception("Expected error for duplicate event bus name");
                }
                catch (ResourceAlreadyExistsException) { }
            }
            finally
            {
                try { await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest { Name = dupBus }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("events", "PutRule_DisableAndVerify", async () =>
        {
            var rdBus = TestRunner.MakeUniqueName("RdBus");
            var rdRule = TestRunner.MakeUniqueName("RdRule");
            await eventBridgeClient.CreateEventBusAsync(new CreateEventBusRequest { Name = rdBus });
            try
            {
                await eventBridgeClient.PutRuleAsync(new PutRuleRequest
                {
                    Name = rdRule,
                    EventBusName = rdBus,
                    Description = "test rule for disable"
                });
                await eventBridgeClient.DisableRuleAsync(new DisableRuleRequest { Name = rdRule, EventBusName = rdBus });
                var resp = await eventBridgeClient.DescribeRuleAsync(new DescribeRuleRequest
                {
                    Name = rdRule, EventBusName = rdBus
                });
                if (resp.State != RuleState.DISABLED)
                    throw new Exception($"expected state DISABLED, got {resp.State}");
                await eventBridgeClient.EnableRuleAsync(new EnableRuleRequest { Name = rdRule, EventBusName = rdBus });
                var resp2 = await eventBridgeClient.DescribeRuleAsync(new DescribeRuleRequest
                {
                    Name = rdRule, EventBusName = rdBus
                });
                if (resp2.State != RuleState.ENABLED)
                    throw new Exception($"expected state ENABLED, got {resp2.State}");
            }
            finally
            {
                try { await eventBridgeClient.DeleteRuleAsync(new DeleteRuleRequest { Name = rdRule, EventBusName = rdBus }); } catch { }
                try { await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest { Name = rdBus }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("events", "PutRule_WithEventPattern", async () =>
        {
            var epBus = TestRunner.MakeUniqueName("EpBus");
            var epRule = TestRunner.MakeUniqueName("EpRule");
            await eventBridgeClient.CreateEventBusAsync(new CreateEventBusRequest { Name = epBus });
            try
            {
                var pattern = System.Text.Json.JsonSerializer.Serialize(new
                {
                    source = new[] { "com.example.test" },
                    detail_type = new[] { "OrderCreated" }
                });
                await eventBridgeClient.PutRuleAsync(new PutRuleRequest
                {
                    Name = epRule,
                    EventBusName = epBus,
                    EventPattern = pattern
                });
                var resp = await eventBridgeClient.DescribeRuleAsync(new DescribeRuleRequest
                {
                    Name = epRule, EventBusName = epBus
                });
                if (string.IsNullOrEmpty(resp.EventPattern))
                    throw new Exception("event pattern is nil");
            }
            finally
            {
                try { await eventBridgeClient.DeleteRuleAsync(new DeleteRuleRequest { Name = epRule, EventBusName = epBus }); } catch { }
                try { await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest { Name = epBus }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("events", "PutEvents_DefaultBus", async () =>
        {
            var evt = System.Text.Json.JsonSerializer.Serialize(new
            {
                source = "com.test.default",
                detail_type = "DefaultBusEvent",
                detail = new { key = "value" }
            });
            var resp = await eventBridgeClient.PutEventsAsync(new PutEventsRequest
            {
                Entries = new List<PutEventsRequestEntry>
                {
                    new PutEventsRequestEntry
                    {
                        Source = "com.test.default",
                        DetailType = "DefaultBusEvent",
                        Detail = evt
                    }
                }
            });
            if (resp.FailedEntryCount != 0)
                throw new Exception($"expected 0 failed entries, got {resp.FailedEntryCount}");
        }));

        results.Add(await runner.RunTestAsync("events", "PutTargets_RemoveTargets_Verify", async () =>
        {
            var trBus = TestRunner.MakeUniqueName("TrBus");
            var trRule = TestRunner.MakeUniqueName("TrRule");
            var trTarget = TestRunner.MakeUniqueName("TrTarget");
            await eventBridgeClient.CreateEventBusAsync(new CreateEventBusRequest { Name = trBus });
            try
            {
                await eventBridgeClient.PutRuleAsync(new PutRuleRequest { Name = trRule, EventBusName = trBus });
                var targetARN = $"arn:aws:lambda:{region}:000000000000:function:TargetFunc";
                await eventBridgeClient.PutTargetsAsync(new PutTargetsRequest
                {
                    Rule = trRule, EventBusName = trBus,
                    Targets = new List<Target>
                    {
                        new Target { Id = trTarget, Arn = targetARN, Input = "{\"action\": \"test\"}" }
                    }
                });
                var listResp = await eventBridgeClient.ListTargetsByRuleAsync(new ListTargetsByRuleRequest
                {
                    Rule = trRule, EventBusName = trBus
                });
                if (listResp.Targets.Count != 1)
                    throw new Exception($"expected 1 target, got {listResp.Targets.Count}");
                if (listResp.Targets[0].Arn != targetARN)
                    throw new Exception($"target ARN mismatch, got {listResp.Targets[0].Arn}");
                await eventBridgeClient.RemoveTargetsAsync(new RemoveTargetsRequest
                {
                    Rule = trRule, EventBusName = trBus, Ids = new List<string> { trTarget }
                });
                var listResp2 = await eventBridgeClient.ListTargetsByRuleAsync(new ListTargetsByRuleRequest
                {
                    Rule = trRule, EventBusName = trBus
                });
                if (listResp2.Targets.Count != 0)
                    throw new Exception($"expected 0 targets after removal, got {listResp2.Targets.Count}");
            }
            finally
            {
                try { await eventBridgeClient.RemoveTargetsAsync(new RemoveTargetsRequest { Rule = trRule, EventBusName = trBus, Ids = new List<string> { trTarget } }); } catch { }
                try { await eventBridgeClient.DeleteRuleAsync(new DeleteRuleRequest { Name = trRule, EventBusName = trBus }); } catch { }
                try { await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest { Name = trBus }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("events", "DeleteRule_WithTargetsFails", async () =>
        {
            var dtBus = TestRunner.MakeUniqueName("DtBus");
            var dtRule = TestRunner.MakeUniqueName("DtRule");
            var dtTarget = TestRunner.MakeUniqueName("DtTarget");
            await eventBridgeClient.CreateEventBusAsync(new CreateEventBusRequest { Name = dtBus });
            try
            {
                await eventBridgeClient.PutRuleAsync(new PutRuleRequest { Name = dtRule, EventBusName = dtBus });
                await eventBridgeClient.PutTargetsAsync(new PutTargetsRequest
                {
                    Rule = dtRule, EventBusName = dtBus,
                    Targets = new List<Target>
                    {
                        new Target { Id = dtTarget, Arn = $"arn:aws:lambda:{region}:000000000000:function:F" }
                    }
                });
                try
                {
                    await eventBridgeClient.DeleteRuleAsync(new DeleteRuleRequest
                    {
                        Name = dtRule, EventBusName = dtBus
                    });
                    throw new Exception("Expected error when deleting rule with targets");
                }
                catch (AmazonEventBridgeException) { }
            }
            finally
            {
                try { await eventBridgeClient.RemoveTargetsAsync(new RemoveTargetsRequest { Rule = dtRule, EventBusName = dtBus, Ids = new List<string> { dtTarget } }); } catch { }
                try { await eventBridgeClient.DeleteRuleAsync(new DeleteRuleRequest { Name = dtRule, EventBusName = dtBus }); } catch { }
                try { await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest { Name = dtBus }); } catch { }
            }
        }));

        return results;
    }
}
