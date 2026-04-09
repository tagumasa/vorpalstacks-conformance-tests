using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class CloudWatchLogsServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonCloudWatchLogsClient cloudWatchLogsClient,
        string region)
    {
        var results = new List<TestResult>();
        var logGroupName = TestRunner.MakeUniqueName("CSLogGroup");
        var logStreamName = TestRunner.MakeUniqueName("CSLogStream");

        try
        {
            results.Add(await runner.RunTestAsync("logs", "CreateLogGroup", async () =>
            {
                var resp = await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest
                {
                    LogGroupName = logGroupName
                });
            }));

            results.Add(await runner.RunTestAsync("logs", "DescribeLogGroups", async () =>
            {
                var resp = await cloudWatchLogsClient.DescribeLogGroupsAsync(new DescribeLogGroupsRequest());
                if (resp.LogGroups == null)
                    throw new Exception("log groups list is nil");
            }));

            results.Add(await runner.RunTestAsync("logs", "DescribeLogStreams", async () =>
            {
                var resp = await cloudWatchLogsClient.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
                {
                    LogGroupName = logGroupName
                });
                if (resp.LogStreams == null)
                    throw new Exception("log streams list is nil");
            }));

            results.Add(await runner.RunTestAsync("logs", "CreateLogStream", async () =>
            {
                await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = logGroupName,
                    LogStreamName = logStreamName
                });
            }));

            results.Add(await runner.RunTestAsync("logs", "PutLogEvents", async () =>
            {
                await cloudWatchLogsClient.PutLogEventsAsync(new PutLogEventsRequest
                {
                    LogGroupName = logGroupName,
                    LogStreamName = logStreamName,
                    LogEvents = new List<InputLogEvent>
                    {
                        new InputLogEvent
                        {
                            Message = "Test log message",
                            Timestamp = DateTime.UtcNow
                        }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("logs", "GetLogEvents", async () =>
            {
                var resp = await cloudWatchLogsClient.GetLogEventsAsync(new GetLogEventsRequest
                {
                    LogGroupName = logGroupName,
                    LogStreamName = logStreamName
                });
                if (resp.Events == null)
                    throw new Exception("events list is nil");
            }));

            results.Add(await runner.RunTestAsync("logs", "FilterLogEvents", async () =>
            {
                var resp = await cloudWatchLogsClient.FilterLogEventsAsync(new FilterLogEventsRequest
                {
                    LogGroupName = logGroupName
                });
                if (resp.Events == null)
                    throw new Exception("events list is nil");
            }));

            results.Add(await runner.RunTestAsync("logs", "PutRetentionPolicy", async () =>
            {
                await cloudWatchLogsClient.PutRetentionPolicyAsync(new PutRetentionPolicyRequest
                {
                    LogGroupName = logGroupName,
                    RetentionInDays = 7
                });
            }));

            results.Add(await runner.RunTestAsync("logs", "DeleteLogStream", async () =>
            {
                await cloudWatchLogsClient.DeleteLogStreamAsync(new DeleteLogStreamRequest
                {
                    LogGroupName = logGroupName,
                    LogStreamName = logStreamName
                });
            }));

            results.Add(await runner.RunTestAsync("logs", "DeleteLogGroup", async () =>
            {
                await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest
                {
                    LogGroupName = logGroupName
                });
            }));
        }
        finally
        {
            try
            {
                await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest
                {
                    LogGroupName = logGroupName
                });
            }
            catch { }
        }

        results.Add(await runner.RunTestAsync("logs", "CreateLogGroup_Duplicate", async () =>
        {
            var dupGroupName = TestRunner.MakeUniqueName("DupLogGroup");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest
            {
                LogGroupName = dupGroupName
            });
            try
            {
                try
                {
                    await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest
                    {
                        LogGroupName = dupGroupName
                    });
                    throw new Exception("expected error for duplicate log group");
                }
                catch (ResourceAlreadyExistsException) { }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = dupGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "DeleteLogGroup_NonExistent", async () =>
        {
            try
            {
                await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest
                {
                    LogGroupName = "nonexistent-log-group-xyz"
                });
                throw new Exception("expected error for non-existent log group");
            }
            catch (ResourceNotFoundException) { }
        }));

        results.Add(await runner.RunTestAsync("logs", "PutLogEvents_GetLogEvents_Roundtrip", async () =>
        {
            var rtGroupName = TestRunner.MakeUniqueName("RTLogGroup");
            var rtStreamName = TestRunner.MakeUniqueName("RTLogStream");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = rtGroupName });
            try
            {
                await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = rtGroupName,
                    LogStreamName = rtStreamName
                });
                var testMessage = "roundtrip-log-message-verify-12345";
                await cloudWatchLogsClient.PutLogEventsAsync(new PutLogEventsRequest
                {
                    LogGroupName = rtGroupName,
                    LogStreamName = rtStreamName,
                    LogEvents = new List<InputLogEvent>
                    {
                        new InputLogEvent { Message = testMessage, Timestamp = DateTime.UtcNow }
                    }
                });
                var resp = await cloudWatchLogsClient.GetLogEventsAsync(new GetLogEventsRequest
                {
                    LogGroupName = rtGroupName,
                    LogStreamName = rtStreamName
                });
                if (resp.Events.Count == 0)
                    throw new Exception("no events returned");
                if (resp.Events[0].Message != testMessage)
                    throw new Exception($"message mismatch: got {resp.Events[0].Message}, want {testMessage}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = rtGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "DescribeLogGroups_ContainsCreated", async () =>
        {
            var dlgName = TestRunner.MakeUniqueName("DLGGroup");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = dlgName });
            try
            {
                var resp = await cloudWatchLogsClient.DescribeLogGroupsAsync(new DescribeLogGroupsRequest
                {
                    LogGroupNamePrefix = dlgName
                });
                if (resp.LogGroups.Count != 1)
                    throw new Exception($"expected 1 log group, got {resp.LogGroups.Count}");
                if (resp.LogGroups[0].LogGroupName != dlgName)
                    throw new Exception("log group name mismatch");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = dlgName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "TagResource_Basic", async () =>
        {
            var tagGroupName = TestRunner.MakeUniqueName("TagLogGroup");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = tagGroupName });
            try
            {
                var arn = $"arn:aws:logs:{region}:000000000000:log-group:{tagGroupName}";
                await cloudWatchLogsClient.TagResourceAsync(new TagResourceRequest
                {
                    ResourceArn = arn,
                    Tags = new Dictionary<string, string>
                    {
                        { "Environment", "Test" },
                        { "Team", "DevOps" }
                    }
                });
                var resp = await cloudWatchLogsClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceArn = arn
                });
                if (resp.Tags == null || resp.Tags.Count < 2)
                    throw new Exception("expected tags to be present");
                if (resp.Tags["Environment"] != "Test")
                    throw new Exception("Environment tag mismatch");
                if (resp.Tags["Team"] != "DevOps")
                    throw new Exception("Team tag mismatch");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = tagGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "ListTagsForResource_Basic", async () =>
        {
            var ltrGroupName = TestRunner.MakeUniqueName("LTRLogGroup");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest
            {
                LogGroupName = ltrGroupName,
                Tags = new Dictionary<string, string>
                {
                    { "Key1", "Value1" }
                }
            });
            try
            {
                var arn = $"arn:aws:logs:{region}:000000000000:log-group:{ltrGroupName}";
                var resp = await cloudWatchLogsClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceArn = arn
                });
                if (resp.Tags == null)
                    throw new Exception("tags is nil");
                if (!resp.Tags.ContainsKey("Key1") || resp.Tags["Key1"] != "Value1")
                    throw new Exception("expected tag Key1=Value1");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = ltrGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "UntagResource_Basic", async () =>
        {
            var utGroupName = TestRunner.MakeUniqueName("UTLogGroup");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest
            {
                LogGroupName = utGroupName,
                Tags = new Dictionary<string, string>
                {
                    { "Keep", "yes" },
                    { "Remove", "yes" }
                }
            });
            try
            {
                var arn = $"arn:aws:logs:{region}:000000000000:log-group:{utGroupName}";
                await cloudWatchLogsClient.UntagResourceAsync(new UntagResourceRequest
                {
                    ResourceArn = arn,
                    TagKeys = new List<string> { "Remove" }
                });
                var resp = await cloudWatchLogsClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceArn = arn
                });
                if (resp.Tags.ContainsKey("Remove"))
                    throw new Exception("expected Remove tag to be removed");
                if (!resp.Tags.ContainsKey("Keep"))
                    throw new Exception("expected Keep tag to still exist");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = utGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "TagLogGroup_Basic", async () =>
        {
            var tlgGroupName = TestRunner.MakeUniqueName("TLGLogGroup");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest
            {
                LogGroupName = tlgGroupName,
                Tags = new Dictionary<string, string>
                {
                    { "CreatedBy", "conformance-test" },
                    { "Version", "1.0" }
                }
            });
            try
            {
                var arn = $"arn:aws:logs:{region}:000000000000:log-group:{tlgGroupName}";
                var resp = await cloudWatchLogsClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceArn = arn
                });
                if (resp.Tags == null)
                    throw new Exception("tags is nil");
                if (resp.Tags["CreatedBy"] != "conformance-test")
                    throw new Exception("CreatedBy tag mismatch");
                if (resp.Tags["Version"] != "1.0")
                    throw new Exception("Version tag mismatch");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = tlgGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "CreateLogGroup_WithTags", async () =>
        {
            var cwtGroupName = TestRunner.MakeUniqueName("CWTLogGroup");
            var tags = new Dictionary<string, string>
            {
                { "Project", "VorpalStacks" },
                { "Owner", "SDKTests" }
            };
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest
            {
                LogGroupName = cwtGroupName,
                Tags = tags
            });
            try
            {
                var arn = $"arn:aws:logs:{region}:000000000000:log-group:{cwtGroupName}";
                var resp = await cloudWatchLogsClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceArn = arn
                });
                if (resp.Tags == null || resp.Tags.Count != 2)
                    throw new Exception($"expected 2 tags, got {resp.Tags?.Count ?? 0}");
                if (resp.Tags["Project"] != "VorpalStacks")
                    throw new Exception("Project tag mismatch");
                if (resp.Tags["Owner"] != "SDKTests")
                    throw new Exception("Owner tag mismatch");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = cwtGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "DeleteRetentionPolicy_Basic", async () =>
        {
            var drpGroupName = TestRunner.MakeUniqueName("DRPLogGroup");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = drpGroupName });
            try
            {
                await cloudWatchLogsClient.PutRetentionPolicyAsync(new PutRetentionPolicyRequest
                {
                    LogGroupName = drpGroupName,
                    RetentionInDays = 14
                });
                await cloudWatchLogsClient.DeleteRetentionPolicyAsync(new DeleteRetentionPolicyRequest
                {
                    LogGroupName = drpGroupName
                });
                var resp = await cloudWatchLogsClient.DescribeLogGroupsAsync(new DescribeLogGroupsRequest
                {
                    LogGroupNamePrefix = drpGroupName
                });
                if (resp.LogGroups.Count == 0)
                    throw new Exception("log group not found");
                if (resp.LogGroups[0].RetentionInDays != 0)
                    throw new Exception($"expected retention 0 after delete, got {resp.LogGroups[0].RetentionInDays}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = drpGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "PutMetricFilter_Basic", async () =>
        {
            var mfGroupName = TestRunner.MakeUniqueName("MFLogGroup");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = mfGroupName });
            try
            {
                await cloudWatchLogsClient.PutMetricFilterAsync(new PutMetricFilterRequest
                {
                    LogGroupName = mfGroupName,
                    FilterName = "TestMetricFilter",
                    FilterPattern = "[ip, user, timestamp, request, status_code = *, *, *, *, *]",
                    MetricTransformations = new List<MetricTransformation>
                    {
                        new MetricTransformation
                        {
                            MetricName = "RequestCount",
                            MetricNamespace = "LogMetrics",
                            MetricValue = "1"
                        }
                    }
                });
                var resp = await cloudWatchLogsClient.DescribeMetricFiltersAsync(new DescribeMetricFiltersRequest
                {
                    LogGroupName = mfGroupName
                });
                if (resp.MetricFilters == null || resp.MetricFilters.Count == 0)
                    throw new Exception("expected metric filter to exist");
                if (resp.MetricFilters[0].FilterName != "TestMetricFilter")
                    throw new Exception("filter name mismatch");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = mfGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "DescribeMetricFilters_Basic", async () =>
        {
            var dmfGroupName = TestRunner.MakeUniqueName("DMFLogGroup");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = dmfGroupName });
            try
            {
                await cloudWatchLogsClient.PutMetricFilterAsync(new PutMetricFilterRequest
                {
                    LogGroupName = dmfGroupName,
                    FilterName = "DMFFilter",
                    FilterPattern = "[ip, user, timestamp, request, status_code = *, *, *, *, *]",
                    MetricTransformations = new List<MetricTransformation>
                    {
                        new MetricTransformation
                        {
                            MetricName = "DMFMetric",
                            MetricNamespace = "DMFNamespace",
                            MetricValue = "1"
                        }
                    }
                });
                var resp = await cloudWatchLogsClient.DescribeMetricFiltersAsync(new DescribeMetricFiltersRequest
                {
                    LogGroupName = dmfGroupName
                });
                if (resp.MetricFilters == null || resp.MetricFilters.Count == 0)
                    throw new Exception("expected at least one metric filter");
                if (resp.MetricFilters[0].FilterName != "DMFFilter")
                    throw new Exception("filter name mismatch");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = dmfGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "DescribeMetricFilters_FilterNamePrefix", async () =>
        {
            var dmfGroupName2 = TestRunner.MakeUniqueName("DMF2LogGroup");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = dmfGroupName2 });
            try
            {
                await cloudWatchLogsClient.PutMetricFilterAsync(new PutMetricFilterRequest
                {
                    LogGroupName = dmfGroupName2,
                    FilterName = "PrefixFilterA",
                    FilterPattern = "[...]",
                    MetricTransformations = new List<MetricTransformation>
                    {
                        new MetricTransformation
                        {
                            MetricName = "MetricA",
                            MetricNamespace = "NS",
                            MetricValue = "1"
                        }
                    }
                });
                await cloudWatchLogsClient.PutMetricFilterAsync(new PutMetricFilterRequest
                {
                    LogGroupName = dmfGroupName2,
                    FilterName = "PrefixFilterB",
                    FilterPattern = "[...]",
                    MetricTransformations = new List<MetricTransformation>
                    {
                        new MetricTransformation
                        {
                            MetricName = "MetricB",
                            MetricNamespace = "NS",
                            MetricValue = "1"
                        }
                    }
                });
                var resp = await cloudWatchLogsClient.DescribeMetricFiltersAsync(new DescribeMetricFiltersRequest
                {
                    LogGroupName = dmfGroupName2,
                    FilterNamePrefix = "PrefixFilter"
                });
                if (resp.MetricFilters == null || resp.MetricFilters.Count != 2)
                    throw new Exception($"expected 2 filters with prefix, got {resp.MetricFilters?.Count ?? 0}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = dmfGroupName2 }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "DeleteMetricFilter_Basic", async () =>
        {
            var delmfGroupName = TestRunner.MakeUniqueName("DelMFLogGroup");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = delmfGroupName });
            try
            {
                await cloudWatchLogsClient.PutMetricFilterAsync(new PutMetricFilterRequest
                {
                    LogGroupName = delmfGroupName,
                    FilterName = "DeleteMeFilter",
                    FilterPattern = "[...]",
                    MetricTransformations = new List<MetricTransformation>
                    {
                        new MetricTransformation
                        {
                            MetricName = "DelMetric",
                            MetricNamespace = "DelNS",
                            MetricValue = "1"
                        }
                    }
                });
                await cloudWatchLogsClient.DeleteMetricFilterAsync(new DeleteMetricFilterRequest
                {
                    LogGroupName = delmfGroupName,
                    FilterName = "DeleteMeFilter"
                });
                var resp = await cloudWatchLogsClient.DescribeMetricFiltersAsync(new DescribeMetricFiltersRequest
                {
                    LogGroupName = delmfGroupName
                });
                if (resp.MetricFilters != null && resp.MetricFilters.Count > 0)
                    throw new Exception("expected no metric filters after delete");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = delmfGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "TestMetricFilter_Basic", async () =>
        {
            var tmfGroupName = TestRunner.MakeUniqueName("TMFLogGroup");
            var tmfStreamName = TestRunner.MakeUniqueName("TMFStream");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = tmfGroupName });
            try
            {
                await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = tmfGroupName,
                    LogStreamName = tmfStreamName
                });
                await cloudWatchLogsClient.PutMetricFilterAsync(new PutMetricFilterRequest
                {
                    LogGroupName = tmfGroupName,
                    FilterName = "TMFFilter",
                    FilterPattern = "[count, msg, status = *, *, *]",
                    MetricTransformations = new List<MetricTransformation>
                    {
                        new MetricTransformation
                        {
                            MetricName = "EventCount",
                            MetricNamespace = "TMFNamespace",
                            MetricValue = "$count"
                        }
                    }
                });
                var baseTs = DateTime.UtcNow.AddSeconds(-5);
                await cloudWatchLogsClient.PutLogEventsAsync(new PutLogEventsRequest
                {
                    LogGroupName = tmfGroupName,
                    LogStreamName = tmfStreamName,
                    LogEvents = new List<InputLogEvent>
                    {
                        new InputLogEvent { Message = "10 hello world 200", Timestamp = baseTs.AddSeconds(1) },
                        new InputLogEvent { Message = "20 goodbye world 404", Timestamp = baseTs.AddSeconds(2) },
                        new InputLogEvent { Message = "30 another entry 500", Timestamp = baseTs.AddSeconds(3) }
                    }
                });
                await Task.Delay(2000);
                var resp = await cloudWatchLogsClient.FilterLogEventsAsync(new FilterLogEventsRequest
                {
                    LogGroupName = tmfGroupName,
                    FilterPattern = "[count, msg, status = *, *, *]"
                });
                if (resp.Events == null || resp.Events.Count == 0)
                    throw new Exception("expected filtered events from metric filter pattern");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = tmfGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "PutSubscriptionFilter_Basic", async () =>
        {
            var sfGroupName = TestRunner.MakeUniqueName("SFLogGroup");
            var sfStreamName = TestRunner.MakeUniqueName("SFStream");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = sfGroupName });
            try
            {
                await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = sfGroupName,
                    LogStreamName = sfStreamName
                });
                await cloudWatchLogsClient.PutSubscriptionFilterAsync(new PutSubscriptionFilterRequest
                {
                    LogGroupName = sfGroupName,
                    FilterName = "TestSubFilter",
                    FilterPattern = "[...]",
                    DestinationArn = $"arn:aws:lambda:{region}:000000000000:function:dummy"
                });
                var resp = await cloudWatchLogsClient.DescribeSubscriptionFiltersAsync(new DescribeSubscriptionFiltersRequest
                {
                    LogGroupName = sfGroupName
                });
                if (resp.SubscriptionFilters == null || resp.SubscriptionFilters.Count == 0)
                    throw new Exception("expected subscription filter to exist");
                if (resp.SubscriptionFilters[0].FilterName != "TestSubFilter")
                    throw new Exception("subscription filter name mismatch");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = sfGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "DescribeSubscriptionFilters_Basic", async () =>
        {
            var dsfGroupName = TestRunner.MakeUniqueName("DSFLogGroup");
            var dsfStreamName = TestRunner.MakeUniqueName("DSFStream");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = dsfGroupName });
            try
            {
                await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = dsfGroupName,
                    LogStreamName = dsfStreamName
                });
                await cloudWatchLogsClient.PutSubscriptionFilterAsync(new PutSubscriptionFilterRequest
                {
                    LogGroupName = dsfGroupName,
                    FilterName = "DSFSubFilter",
                    FilterPattern = "[...]",
                    DestinationArn = $"arn:aws:lambda:{region}:000000000000:function:dummy"
                });
                var resp = await cloudWatchLogsClient.DescribeSubscriptionFiltersAsync(new DescribeSubscriptionFiltersRequest
                {
                    LogGroupName = dsfGroupName
                });
                if (resp.SubscriptionFilters == null || resp.SubscriptionFilters.Count == 0)
                    throw new Exception("expected at least one subscription filter");
                if (resp.SubscriptionFilters[0].FilterName != "DSFSubFilter")
                    throw new Exception("filter name mismatch");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = dsfGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "DeleteSubscriptionFilter_Basic", async () =>
        {
            var delsfGroupName = TestRunner.MakeUniqueName("DelSFLogGroup");
            var delsfStreamName = TestRunner.MakeUniqueName("DelSFStream");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = delsfGroupName });
            try
            {
                await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = delsfGroupName,
                    LogStreamName = delsfStreamName
                });
                await cloudWatchLogsClient.PutSubscriptionFilterAsync(new PutSubscriptionFilterRequest
                {
                    LogGroupName = delsfGroupName,
                    FilterName = "DelSubFilter",
                    FilterPattern = "[...]",
                    DestinationArn = $"arn:aws:lambda:{region}:000000000000:function:dummy"
                });
                await cloudWatchLogsClient.DeleteSubscriptionFilterAsync(new DeleteSubscriptionFilterRequest
                {
                    LogGroupName = delsfGroupName,
                    FilterName = "DelSubFilter"
                });
                var resp = await cloudWatchLogsClient.DescribeSubscriptionFiltersAsync(new DescribeSubscriptionFiltersRequest
                {
                    LogGroupName = delsfGroupName
                });
                if (resp.SubscriptionFilters != null && resp.SubscriptionFilters.Count > 0)
                    throw new Exception("expected no subscription filters after delete");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = delsfGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "PutDestination_Basic", async () =>
        {
            var destName = TestRunner.MakeUniqueName("TestDest");
            await cloudWatchLogsClient.PutDestinationAsync(new PutDestinationRequest
            {
                DestinationName = destName,
                TargetArn = $"arn:aws:kinesis:{region}:000000000000:stream/dummy",
                RoleArn = $"arn:aws:iam::000000000000:role/dummy"
            });
            try
            {
                var resp = await cloudWatchLogsClient.DescribeDestinationsAsync(new DescribeDestinationsRequest
                {
                    DestinationNamePrefix = destName
                });
                if (resp.Destinations == null || resp.Destinations.Count == 0)
                    throw new Exception("expected destination to exist");
                if (resp.Destinations[0].DestinationName != destName)
                    throw new Exception("destination name mismatch");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteDestinationAsync(new DeleteDestinationRequest { DestinationName = destName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "DescribeDestinations_Basic", async () =>
        {
            var ddName = TestRunner.MakeUniqueName("DDEst");
            await cloudWatchLogsClient.PutDestinationAsync(new PutDestinationRequest
            {
                DestinationName = ddName,
                TargetArn = $"arn:aws:kinesis:{region}:000000000000:stream/dummy",
                RoleArn = $"arn:aws:iam::000000000000:role/dummy"
            });
            try
            {
                var resp = await cloudWatchLogsClient.DescribeDestinationsAsync(new DescribeDestinationsRequest());
                if (resp.Destinations == null)
                    throw new Exception("destinations list is nil");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteDestinationAsync(new DeleteDestinationRequest { DestinationName = ddName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "PutDestinationPolicy_Basic", async () =>
        {
            var pdpName = TestRunner.MakeUniqueName("PDPEst");
            await cloudWatchLogsClient.PutDestinationAsync(new PutDestinationRequest
            {
                DestinationName = pdpName,
                TargetArn = $"arn:aws:kinesis:{region}:000000000000:stream/dummy",
                RoleArn = $"arn:aws:iam::000000000000:role/dummy"
            });
            try
            {
                var policy = "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":{\"AWS\":\"000000000000\"},\"Action\":\"logs:PutSubscriptionFilter\",\"Resource\":\"*\"}]}";
                await cloudWatchLogsClient.PutDestinationPolicyAsync(new PutDestinationPolicyRequest
                {
                    DestinationName = pdpName,
                    AccessPolicy = policy
                });
                var resp = await cloudWatchLogsClient.DescribeDestinationsAsync(new DescribeDestinationsRequest
                {
                    DestinationNamePrefix = pdpName
                });
                if (resp.Destinations == null || resp.Destinations.Count == 0)
                    throw new Exception("destination not found after policy update");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteDestinationAsync(new DeleteDestinationRequest { DestinationName = pdpName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "PutDestination_UpdateInPlace", async () =>
        {
            var uipName = TestRunner.MakeUniqueName("UIPEst");
            await cloudWatchLogsClient.PutDestinationAsync(new PutDestinationRequest
            {
                DestinationName = uipName,
                TargetArn = $"arn:aws:kinesis:{region}:000000000000:stream/dummy",
                RoleArn = $"arn:aws:iam::000000000000:role/dummy"
            });
            try
            {
                var newRoleArn = $"arn:aws:iam::000000000000:role/updated";
                await cloudWatchLogsClient.PutDestinationAsync(new PutDestinationRequest
                {
                    DestinationName = uipName,
                    TargetArn = $"arn:aws:kinesis:{region}:000000000000:stream/updated",
                    RoleArn = newRoleArn
                });
                var resp = await cloudWatchLogsClient.DescribeDestinationsAsync(new DescribeDestinationsRequest
                {
                    DestinationNamePrefix = uipName
                });
                if (resp.Destinations == null || resp.Destinations.Count == 0)
                    throw new Exception("destination not found after update");
                if (resp.Destinations[0].RoleArn != newRoleArn)
                    throw new Exception($"role ARN mismatch after update");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteDestinationAsync(new DeleteDestinationRequest { DestinationName = uipName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "DeleteDestination_Basic", async () =>
        {
            var dd2Name = TestRunner.MakeUniqueName("DelDest");
            await cloudWatchLogsClient.PutDestinationAsync(new PutDestinationRequest
            {
                DestinationName = dd2Name,
                TargetArn = $"arn:aws:kinesis:{region}:000000000000:stream/dummy",
                RoleArn = $"arn:aws:iam::000000000000:role/dummy"
            });
            await cloudWatchLogsClient.DeleteDestinationAsync(new DeleteDestinationRequest
            {
                DestinationName = dd2Name
            });
            var resp = await cloudWatchLogsClient.DescribeDestinationsAsync(new DescribeDestinationsRequest
            {
                DestinationNamePrefix = dd2Name
            });
            if (resp.Destinations != null && resp.Destinations.Count > 0)
                throw new Exception("expected destination to be deleted");
        }));

        results.Add(await runner.RunTestAsync("logs", "DescribeLogGroups_Pagination", async () =>
        {
            var pgGroupName = TestRunner.MakeUniqueName("PGLogGroup");
            for (int i = 0; i < 3; i++)
            {
                await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest
                {
                    LogGroupName = $"{pgGroupName}-{i}"
                });
            }
            try
            {
                var allGroups = new List<LogGroup>();
                string? nextToken = null;
                do
                {
                    var resp = await cloudWatchLogsClient.DescribeLogGroupsAsync(new DescribeLogGroupsRequest
                    {
                        Limit = 2,
                        NextToken = nextToken
                    });
                    if (resp.LogGroups != null)
                        allGroups.AddRange(resp.LogGroups);
                    nextToken = resp.NextToken;
                } while (nextToken != null);

                bool found = false;
                foreach (var g in allGroups)
                {
                    if (g.LogGroupName != null && g.LogGroupName.StartsWith(pgGroupName))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    throw new Exception("pagination did not return created log groups");
            }
            finally
            {
                for (int i = 0; i < 3; i++)
                {
                    try { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = $"{pgGroupName}-{i}" }); } catch { }
                }
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "DescribeLogStreams_Pagination", async () =>
        {
            var psGroupName = TestRunner.MakeUniqueName("PSLogGroup");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = psGroupName });
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                    {
                        LogGroupName = psGroupName,
                        LogStreamName = $"stream-{i}"
                    });
                }
                var allStreams = new List<LogStream>();
                string? nextToken = null;
                do
                {
                    var resp = await cloudWatchLogsClient.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
                    {
                        LogGroupName = psGroupName,
                        Limit = 2,
                        NextToken = nextToken
                    });
                    if (resp.LogStreams != null)
                        allStreams.AddRange(resp.LogStreams);
                    nextToken = resp.NextToken;
                } while (nextToken != null);

                if (allStreams.Count < 3)
                    throw new Exception($"pagination returned {allStreams.Count} streams, expected at least 3");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = psGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "DescribeLogStreams_NamePrefix", async () =>
        {
            var npGroupName = TestRunner.MakeUniqueName("NPLogGroup");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = npGroupName });
            try
            {
                await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = npGroupName,
                    LogStreamName = "prefix-alpha"
                });
                await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = npGroupName,
                    LogStreamName = "prefix-beta"
                });
                await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = npGroupName,
                    LogStreamName = "other-stream"
                });
                var resp = await cloudWatchLogsClient.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
                {
                    LogGroupName = npGroupName,
                    LogStreamNamePrefix = "prefix-"
                });
                if (resp.LogStreams == null || resp.LogStreams.Count != 2)
                    throw new Exception($"expected 2 streams with prefix, got {resp.LogStreams?.Count ?? 0}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = npGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "DeleteLogStream_NonExistent", async () =>
        {
            var dlsGroupName = TestRunner.MakeUniqueName("DLSLogGroup");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = dlsGroupName });
            try
            {
                try
                {
                    await cloudWatchLogsClient.DeleteLogStreamAsync(new DeleteLogStreamRequest
                    {
                        LogGroupName = dlsGroupName,
                        LogStreamName = "nonexistent-stream-xyz"
                    });
                    throw new Exception("expected error for non-existent log stream");
                }
                catch (ResourceNotFoundException) { }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = dlsGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "PutLogEvents_MultipleEvents", async () =>
        {
            var meGroupName = TestRunner.MakeUniqueName("MELogGroup");
            var meStreamName = TestRunner.MakeUniqueName("MEStream");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = meGroupName });
            try
            {
                await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = meGroupName,
                    LogStreamName = meStreamName
                });
                var baseTs = DateTime.UtcNow;
                await cloudWatchLogsClient.PutLogEventsAsync(new PutLogEventsRequest
                {
                    LogGroupName = meGroupName,
                    LogStreamName = meStreamName,
                    LogEvents = new List<InputLogEvent>
                    {
                        new InputLogEvent { Message = "event-one", Timestamp = baseTs.AddMilliseconds(0) },
                        new InputLogEvent { Message = "event-two", Timestamp = baseTs.AddMilliseconds(1) },
                        new InputLogEvent { Message = "event-three", Timestamp = baseTs.AddMilliseconds(2) }
                    }
                });
                var resp = await cloudWatchLogsClient.GetLogEventsAsync(new GetLogEventsRequest
                {
                    LogGroupName = meGroupName,
                    LogStreamName = meStreamName
                });
                if (resp.Events == null || resp.Events.Count < 3)
                    throw new Exception($"expected at least 3 events, got {resp.Events?.Count ?? 0}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = meGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "GetLogEvents_StartFromHead", async () =>
        {
            var sfhGroupName = TestRunner.MakeUniqueName("SFHLogGroup");
            var sfhStreamName = TestRunner.MakeUniqueName("SFHStream");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = sfhGroupName });
            try
            {
                await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = sfhGroupName,
                    LogStreamName = sfhStreamName
                });
                var baseTs = DateTime.UtcNow;
                await cloudWatchLogsClient.PutLogEventsAsync(new PutLogEventsRequest
                {
                    LogGroupName = sfhGroupName,
                    LogStreamName = sfhStreamName,
                    LogEvents = new List<InputLogEvent>
                    {
                        new InputLogEvent { Message = "first-event", Timestamp = baseTs.AddMilliseconds(0) },
                        new InputLogEvent { Message = "second-event", Timestamp = baseTs.AddMilliseconds(1) }
                    }
                });
                var resp = await cloudWatchLogsClient.GetLogEventsAsync(new GetLogEventsRequest
                {
                    LogGroupName = sfhGroupName,
                    LogStreamName = sfhStreamName,
                    StartFromHead = true
                });
                if (resp.Events == null || resp.Events.Count == 0)
                    throw new Exception("expected events from head");
                if (resp.Events[0].Message != "first-event")
                    throw new Exception($"expected first-event, got {resp.Events[0].Message}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = sfhGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "FilterLogEvents_WithFilterPattern", async () =>
        {
            var fpGroupName = TestRunner.MakeUniqueName("FPLogGroup");
            var fpStreamName = TestRunner.MakeUniqueName("FPStream");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = fpGroupName });
            try
            {
                await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = fpGroupName,
                    LogStreamName = fpStreamName
                });
                var baseTs = DateTime.UtcNow;
                await cloudWatchLogsClient.PutLogEventsAsync(new PutLogEventsRequest
                {
                    LogGroupName = fpGroupName,
                    LogStreamName = fpStreamName,
                    LogEvents = new List<InputLogEvent>
                    {
                        new InputLogEvent { Message = "ERROR something went wrong", Timestamp = baseTs.AddMilliseconds(0) },
                        new InputLogEvent { Message = "INFO everything is fine", Timestamp = baseTs.AddMilliseconds(1) },
                        new InputLogEvent { Message = "ERROR another error occurred", Timestamp = baseTs.AddMilliseconds(2) }
                    }
                });
                var resp = await cloudWatchLogsClient.FilterLogEventsAsync(new FilterLogEventsRequest
                {
                    LogGroupName = fpGroupName,
                    FilterPattern = "ERROR"
                });
                if (resp.Events == null || resp.Events.Count == 0)
                    throw new Exception("expected filtered events matching ERROR");
                foreach (var e in resp.Events)
                {
                    if (!e.Message.Contains("ERROR"))
                        throw new Exception($"filtered event does not contain ERROR: {e.Message}");
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = fpGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "FilterLogEvents_WithLogStreamNames", async () =>
        {
            var lsnGroupName = TestRunner.MakeUniqueName("LSNLogGroup");
            var lsnStreamA = TestRunner.MakeUniqueName("LSNStreamA");
            var lsnStreamB = TestRunner.MakeUniqueName("LSNStreamB");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = lsnGroupName });
            try
            {
                await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = lsnGroupName,
                    LogStreamName = lsnStreamA
                });
                await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = lsnGroupName,
                    LogStreamName = lsnStreamB
                });
                var baseTs = DateTime.UtcNow;
                await cloudWatchLogsClient.PutLogEventsAsync(new PutLogEventsRequest
                {
                    LogGroupName = lsnGroupName,
                    LogStreamName = lsnStreamA,
                    LogEvents = new List<InputLogEvent>
                    {
                        new InputLogEvent { Message = "event-in-a", Timestamp = baseTs.AddMilliseconds(0) }
                    }
                });
                await cloudWatchLogsClient.PutLogEventsAsync(new PutLogEventsRequest
                {
                    LogGroupName = lsnGroupName,
                    LogStreamName = lsnStreamB,
                    LogEvents = new List<InputLogEvent>
                    {
                        new InputLogEvent { Message = "event-in-b", Timestamp = baseTs.AddMilliseconds(1) }
                    }
                });
                var resp = await cloudWatchLogsClient.FilterLogEventsAsync(new FilterLogEventsRequest
                {
                    LogGroupName = lsnGroupName,
                    LogStreamNames = new List<string> { lsnStreamA }
                });
                if (resp.Events == null || resp.Events.Count == 0)
                    throw new Exception("expected events from specified log stream");
                foreach (var e in resp.Events)
                {
                    if (e.LogStreamName != lsnStreamA)
                        throw new Exception($"expected event from stream {lsnStreamA}, got {e.LogStreamName}");
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = lsnGroupName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "MetricFilterCount_Tracked", async () =>
        {
            var mfcGroupName = TestRunner.MakeUniqueName("MFCLogGroup");
            var mfcStreamName = TestRunner.MakeUniqueName("MFCStream");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = mfcGroupName });
            try
            {
                await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = mfcGroupName,
                    LogStreamName = mfcStreamName
                });
                await cloudWatchLogsClient.PutMetricFilterAsync(new PutMetricFilterRequest
                {
                    LogGroupName = mfcGroupName,
                    FilterName = "ErrorCountFilter",
                    FilterPattern = "[level, ...] = \"ERROR\", ...",
                    MetricTransformations = new List<MetricTransformation>
                    {
                        new MetricTransformation
                        {
                            MetricName = "ErrorCount",
                            MetricNamespace = "MFCNamespace",
                            MetricValue = "1"
                        }
                    }
                });
                var baseTs = DateTime.UtcNow;
                await cloudWatchLogsClient.PutLogEventsAsync(new PutLogEventsRequest
                {
                    LogGroupName = mfcGroupName,
                    LogStreamName = mfcStreamName,
                    LogEvents = new List<InputLogEvent>
                    {
                        new InputLogEvent { Message = "ERROR disk full", Timestamp = baseTs.AddMilliseconds(0) },
                        new InputLogEvent { Message = "INFO normal operation", Timestamp = baseTs.AddMilliseconds(1) },
                        new InputLogEvent { Message = "ERROR network timeout", Timestamp = baseTs.AddMilliseconds(2) },
                        new InputLogEvent { Message = "ERROR out of memory", Timestamp = baseTs.AddMilliseconds(3) }
                    }
                });
                await Task.Delay(2000);
                var resp = await cloudWatchLogsClient.DescribeMetricFiltersAsync(new DescribeMetricFiltersRequest
                {
                    LogGroupName = mfcGroupName
                });
                if (resp.MetricFilters == null || resp.MetricFilters.Count == 0)
                    throw new Exception("expected metric filter to exist");
                if (resp.MetricFilters[0].FilterName != "ErrorCountFilter")
                    throw new Exception("filter name mismatch");
                if (resp.MetricFilters[0].MetricTransformations.Count == 0)
                    throw new Exception("expected metric transformation");
                if (resp.MetricFilters[0].MetricTransformations[0].MetricName != "ErrorCount")
                    throw new Exception("metric name mismatch");
                var filterResp = await cloudWatchLogsClient.FilterLogEventsAsync(new FilterLogEventsRequest
                {
                    LogGroupName = mfcGroupName,
                    FilterPattern = "[level, ...] = \"ERROR\", ..."
                });
                if (filterResp.Events == null || filterResp.Events.Count < 3)
                    throw new Exception($"expected at least 3 ERROR events, got {filterResp.Events?.Count ?? 0}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = mfcGroupName }); });
            }
        }));

        return results;
    }
}
