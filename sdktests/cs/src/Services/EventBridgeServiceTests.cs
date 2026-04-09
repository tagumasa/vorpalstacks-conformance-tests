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
            await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteRuleAsync(new DeleteRuleRequest { Name = ruleName, EventBusName = busName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest { Name = busName }); });
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
                await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest { Name = dupBus }); });
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
                await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteRuleAsync(new DeleteRuleRequest { Name = rdRule, EventBusName = rdBus }); });
                await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest { Name = rdBus }); });
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
                await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteRuleAsync(new DeleteRuleRequest { Name = epRule, EventBusName = epBus }); });
                await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest { Name = epBus }); });
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
                await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteRuleAsync(new DeleteRuleRequest { Name = trRule, EventBusName = trBus }); });
                await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest { Name = trBus }); });
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
                await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteRuleAsync(new DeleteRuleRequest { Name = dtRule, EventBusName = dtBus }); });
                await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest { Name = dtBus }); });
            }
        }));

        var updateBusName = TestRunner.MakeUniqueName("UpdateBus");
        try
        {
            await eventBridgeClient.CreateEventBusAsync(new CreateEventBusRequest { Name = updateBusName });

            results.Add(await runner.RunTestAsync("events", "UpdateEventBus", async () =>
            {
                await eventBridgeClient.UpdateEventBusAsync(new UpdateEventBusRequest
                {
                    Name = updateBusName,
                    Description = "Updated description"
                });
            }));

            results.Add(await runner.RunTestAsync("events", "UpdateEventBus_VerifyDescription", async () =>
            {
                var resp = await eventBridgeClient.DescribeEventBusAsync(new DescribeEventBusRequest { Name = updateBusName });
                if (resp.Description != "Updated description")
                    throw new Exception($"expected 'Updated description', got '{resp.Description}'");
            }));
        }
        finally
        {
            await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest { Name = updateBusName }); });
        }

        var archiveBusName = TestRunner.MakeUniqueName("ArchiveBus");
        var archiveName = TestRunner.MakeUniqueName("TestArchive");
        try
        {
            await eventBridgeClient.CreateEventBusAsync(new CreateEventBusRequest { Name = archiveBusName });

            results.Add(await runner.RunTestAsync("events", "CreateArchive", async () =>
            {
                await eventBridgeClient.CreateArchiveAsync(new CreateArchiveRequest
                {
                    ArchiveName = archiveName,
                    EventSourceArn = $"arn:aws:events:{region}:000000000000:event-bus/{archiveBusName}",
                    RetentionDays = 1
                });
            }));

            results.Add(await runner.RunTestAsync("events", "DescribeArchive", async () =>
            {
                var resp = await eventBridgeClient.DescribeArchiveAsync(new DescribeArchiveRequest
                {
                    ArchiveName = archiveName
                });
                if (resp.ArchiveArn == null)
                    throw new Exception("archive ARN is null");
            }));

            results.Add(await runner.RunTestAsync("events", "DescribeArchive_NonExistent", async () =>
            {
                try
                {
                    await eventBridgeClient.DescribeArchiveAsync(new DescribeArchiveRequest
                    {
                        ArchiveName = "nonexistent-archive-xyz-12345"
                    });
                    throw new Exception("expected error for non-existent archive");
                }
                catch (ResourceNotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("events", "DeleteArchive_NonExistent", async () =>
            {
                try
                {
                    await eventBridgeClient.DeleteArchiveAsync(new DeleteArchiveRequest
                    {
                        ArchiveName = "nonexistent-archive-xyz-12345"
                    });
                    throw new Exception("expected error for non-existent archive");
                }
                catch (ResourceNotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("events", "ListArchives", async () =>
            {
                var resp = await eventBridgeClient.ListArchivesAsync(new ListArchivesRequest());
                if (resp.Archives == null)
                    throw new Exception("archives list is null");
            }));

            results.Add(await runner.RunTestAsync("events", "UpdateArchive", async () =>
            {
                await eventBridgeClient.UpdateArchiveAsync(new UpdateArchiveRequest
                {
                    ArchiveName = archiveName,
                    RetentionDays = 2
                });
            }));
        }
        finally
        {
            await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteArchiveAsync(new DeleteArchiveRequest { ArchiveName = archiveName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest { Name = archiveBusName }); });
        }

        results.Add(await runner.RunTestAsync("events", "CreateConnection", async () =>
        {
            var connBus = TestRunner.MakeUniqueName("ConnBus");
            await eventBridgeClient.CreateEventBusAsync(new CreateEventBusRequest { Name = connBus });
            try
            {
                await eventBridgeClient.PutPermissionAsync(new PutPermissionRequest
                {
                    EventBusName = connBus,
                    Action = "events:PutEvents",
                    Principal = "000000000000",
                    StatementId = "TestConnStmt"
                });
                var resp = await eventBridgeClient.DescribeEventBusAsync(new DescribeEventBusRequest { Name = connBus });
                if (resp.Name == null)
                    throw new Exception("event bus name is null");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.RemovePermissionAsync(new RemovePermissionRequest { EventBusName = connBus, StatementId = "TestConnStmt" }); });
                await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest { Name = connBus }); });
            }
        }));

        results.Add(await runner.RunTestAsync("events", "DescribeConnection", async () =>
        {
            try
            {
                await eventBridgeClient.DescribeConnectionAsync(new DescribeConnectionRequest
                {
                    Name = "nonexistent-conn-xyz-12345"
                });
                throw new Exception("expected error for non-existent connection");
            }
            catch (ResourceNotFoundException) { }
        }));

        results.Add(await runner.RunTestAsync("events", "DescribeConnection_NonExistent", async () =>
        {
            try
            {
                await eventBridgeClient.DescribeConnectionAsync(new DescribeConnectionRequest
                {
                    Name = "nonexistent-conn-xyz-67890"
                });
                throw new Exception("expected error for non-existent connection");
            }
            catch (ResourceNotFoundException) { }
        }));

        results.Add(await runner.RunTestAsync("events", "DeleteConnection_NonExistent", async () =>
        {
            try
            {
                await eventBridgeClient.DeleteConnectionAsync(new DeleteConnectionRequest
                {
                    Name = "nonexistent-conn-xyz-12345"
                });
                throw new Exception("expected error for non-existent connection");
            }
            catch (ResourceNotFoundException) { }
        }));

        results.Add(await runner.RunTestAsync("events", "ListConnections", async () =>
        {
            var resp = await eventBridgeClient.ListConnectionsAsync(new ListConnectionsRequest());
            if (resp.Connections == null)
                throw new Exception("connections list is null");
        }));

        results.Add(await runner.RunTestAsync("events", "UpdateConnection", async () =>
        {
            try
            {
                await eventBridgeClient.UpdateConnectionAsync(new UpdateConnectionRequest
                {
                    Name = "nonexistent-conn-xyz-12345"
                });
                throw new Exception("expected error for non-existent connection update");
            }
            catch (ResourceNotFoundException) { }
        }));

        var destName = TestRunner.MakeUniqueName("TestDest");
        try
        {
            results.Add(await runner.RunTestAsync("events", "CreateApiDestination", async () =>
            {
                try
                {
                    await eventBridgeClient.CreateApiDestinationAsync(new CreateApiDestinationRequest
                    {
                        Name = destName,
                        ConnectionArn = $"arn:aws:events:{region}:000000000000:connection/TestConn",
                        HttpMethod = "POST",
                        InvocationEndpoint = "https://example.com/endpoint"
                    });
                }
                catch (AmazonEventBridgeException) { }
            }));

            results.Add(await runner.RunTestAsync("events", "DescribeApiDestination", async () =>
            {
                try
                {
                    await eventBridgeClient.DescribeApiDestinationAsync(new DescribeApiDestinationRequest
                    {
                        Name = destName
                    });
                }
                catch (ResourceNotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("events", "DescribeApiDestination_NonExistent", async () =>
            {
                try
                {
                    await eventBridgeClient.DescribeApiDestinationAsync(new DescribeApiDestinationRequest
                    {
                        Name = "nonexistent-dest-xyz-12345"
                    });
                    throw new Exception("expected error for non-existent api destination");
                }
                catch (ResourceNotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("events", "DeleteApiDestination_NonExistent", async () =>
            {
                try
                {
                    await eventBridgeClient.DeleteApiDestinationAsync(new DeleteApiDestinationRequest
                    {
                        Name = "nonexistent-dest-xyz-12345"
                    });
                    throw new Exception("expected error for non-existent api destination");
                }
                catch (ResourceNotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("events", "ListApiDestinations", async () =>
            {
                var resp = await eventBridgeClient.ListApiDestinationsAsync(new ListApiDestinationsRequest());
                if (resp.ApiDestinations == null)
                    throw new Exception("api destinations list is null");
            }));

            results.Add(await runner.RunTestAsync("events", "UpdateApiDestination", async () =>
            {
                try
                {
                    await eventBridgeClient.UpdateApiDestinationAsync(new UpdateApiDestinationRequest
                    {
                        Name = destName,
                        HttpMethod = "GET",
                        InvocationEndpoint = "https://example.com/updated"
                    });
                }
                catch (AmazonEventBridgeException) { }
            }));
        }
        finally
        {
            await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteApiDestinationAsync(new DeleteApiDestinationRequest { Name = destName }); });
        }

        results.Add(await runner.RunTestAsync("events", "TestEventPattern_Match", async () =>
        {
            var pattern = System.Text.Json.JsonSerializer.Serialize(new
            {
                source = new[] { "com.example.test" }
            });
            var evt = System.Text.Json.JsonSerializer.Serialize(new
            {
                source = "com.example.test",
                detail_type = "TestEvent",
                detail = new { message = "test" }
            });
            var resp = await eventBridgeClient.TestEventPatternAsync(new TestEventPatternRequest
            {
                EventPattern = pattern,
                Event = evt
            });
            if (resp.Result == null || !resp.Result.Value)
                throw new Exception("expected event pattern to match");
        }));

        results.Add(await runner.RunTestAsync("events", "TestEventPattern_NoMatch", async () =>
        {
            var pattern = System.Text.Json.JsonSerializer.Serialize(new
            {
                source = new[] { "com.different.source" }
            });
            var evt = System.Text.Json.JsonSerializer.Serialize(new
            {
                source = "com.example.test",
                detail_type = "TestEvent",
                detail = new { message = "test" }
            });
            var resp = await eventBridgeClient.TestEventPatternAsync(new TestEventPatternRequest
            {
                EventPattern = pattern,
                Event = evt
            });
            if (resp.Result != null && resp.Result.Value)
                throw new Exception("expected event pattern not to match");
        }));

        var replayBusName = TestRunner.MakeUniqueName("ReplayBus");
        var replayName = TestRunner.MakeUniqueName("TestReplay");
        try
        {
            await eventBridgeClient.CreateEventBusAsync(new CreateEventBusRequest { Name = replayBusName });

            results.Add(await runner.RunTestAsync("events", "StartReplay_DescribeReplay", async () =>
            {
                try
                {
                    await eventBridgeClient.StartReplayAsync(new StartReplayRequest
                    {
                        ReplayName = replayName,
                        EventSourceArn = $"arn:aws:events:{region}:000000000000:event-bus/{replayBusName}",
                        EventStartTime = DateTime.UtcNow.AddMinutes(-60),
                        EventEndTime = DateTime.UtcNow
                    });
                }
                catch (AmazonEventBridgeException) { }

                try
                {
                    var resp = await eventBridgeClient.DescribeReplayAsync(new DescribeReplayRequest
                    {
                        ReplayName = replayName
                    });
                    if (resp.ReplayArn == null)
                        throw new Exception("replay ARN is null");
                }
                catch (ResourceNotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("events", "CancelReplay_NonExistent", async () =>
            {
                try
                {
                    await eventBridgeClient.CancelReplayAsync(new CancelReplayRequest
                    {
                        ReplayName = "nonexistent-replay-xyz-12345"
                    });
                    throw new Exception("expected error for non-existent replay");
                }
                catch (ResourceNotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("events", "ListReplays", async () =>
            {
                var resp = await eventBridgeClient.ListReplaysAsync(new ListReplaysRequest());
                if (resp.Replays == null)
                    throw new Exception("replays list is null");
            }));
        }
        finally
        {
            await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.CancelReplayAsync(new CancelReplayRequest { ReplayName = replayName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest { Name = replayBusName }); });
        }

        var lrbtBusName = TestRunner.MakeUniqueName("LRBDBus");
        var lrbtRuleName = TestRunner.MakeUniqueName("LRBDRule");
        var lrbtTargetId = TestRunner.MakeUniqueName("LRBDTarget");
        try
        {
            await eventBridgeClient.CreateEventBusAsync(new CreateEventBusRequest { Name = lrbtBusName });
            await eventBridgeClient.PutRuleAsync(new PutRuleRequest { Name = lrbtRuleName, EventBusName = lrbtBusName });
            await eventBridgeClient.PutTargetsAsync(new PutTargetsRequest
            {
                Rule = lrbtRuleName,
                EventBusName = lrbtBusName,
                Targets = new List<Target>
                {
                    new Target
                    {
                        Id = lrbtTargetId,
                        Arn = $"arn:aws:lambda:{region}:000000000000:function:Func"
                    }
                }
            });

            results.Add(await runner.RunTestAsync("events", "ListRuleNamesByTarget", async () =>
            {
                var targetArn = $"arn:aws:lambda:{region}:000000000000:function:Func";
                var resp = await eventBridgeClient.ListRuleNamesByTargetAsync(new ListRuleNamesByTargetRequest
                {
                    TargetArn = targetArn,
                    EventBusName = lrbtBusName
                });
                if (resp.RuleNames == null)
                    throw new Exception("rule names list is null");
            }));
        }
        finally
        {
            try { await eventBridgeClient.RemoveTargetsAsync(new RemoveTargetsRequest { Rule = lrbtRuleName, EventBusName = lrbtBusName, Ids = new List<string> { lrbtTargetId } }); } catch { }
            await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteRuleAsync(new DeleteRuleRequest { Name = lrbtRuleName, EventBusName = lrbtBusName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await eventBridgeClient.DeleteEventBusAsync(new DeleteEventBusRequest { Name = lrbtBusName }); });
        }

        results.Add(await runner.RunTestAsync("events", "ListEventBuses_NamePrefix", async () =>
        {
            var prefix = TestRunner.MakeUniqueName("PrefBus").Substring(0, 10);
            var resp = await eventBridgeClient.ListEventBusesAsync(new ListEventBusesRequest
            {
                NamePrefix = prefix
            });
            if (resp.EventBuses == null)
                throw new Exception("event buses list is null");
        }));

        results.Add(await runner.RunTestAsync("events", "PutEvents_MultipleEntries", async () =>
        {
            var evt1 = System.Text.Json.JsonSerializer.Serialize(new { source = "com.test.multi", detail_type = "Event1", detail = new { id = 1 } });
            var evt2 = System.Text.Json.JsonSerializer.Serialize(new { source = "com.test.multi", detail_type = "Event2", detail = new { id = 2 } });
            var resp = await eventBridgeClient.PutEventsAsync(new PutEventsRequest
            {
                Entries = new List<PutEventsRequestEntry>
                {
                    new PutEventsRequestEntry { Source = "com.test.multi", DetailType = "Event1", Detail = evt1 },
                    new PutEventsRequestEntry { Source = "com.test.multi", DetailType = "Event2", Detail = evt2 }
                }
            });
            if (resp.FailedEntryCount != 0)
                throw new Exception($"expected 0 failed entries, got {resp.FailedEntryCount}");
        }));

        results.Add(await runner.RunTestAsync("events", "ListRules_Pagination", async () =>
        {
            var resp = await eventBridgeClient.ListRulesAsync(new ListRulesRequest { Limit = 5 });
            if (resp.Rules == null)
                throw new Exception("rules list is null");
            if (!string.IsNullOrEmpty(resp.NextToken))
            {
                var resp2 = await eventBridgeClient.ListRulesAsync(new ListRulesRequest
                {
                    Limit = 5,
                    NextToken = resp.NextToken
                });
                if (resp2.Rules == null)
                    throw new Exception("rules list page 2 is null");
            }
        }));

        return results;
    }
}
