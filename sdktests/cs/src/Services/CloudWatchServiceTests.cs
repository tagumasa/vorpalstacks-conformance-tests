using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class CloudWatchServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonCloudWatchClient cloudwatchClient,
        string region)
    {
        var results = new List<TestResult>();
        var namespaceName = TestRunner.MakeUniqueName("CSNamespace");

        results.Add(await runner.RunTestAsync("cloudwatch", "ListMetrics", async () =>
        {
            var resp = await cloudwatchClient.ListMetricsAsync(new ListMetricsRequest
            {
                Namespace = "AWS/EC2",
                MetricName = "CPUUtilization"
            });
            if (resp.Metrics == null)
                throw new Exception("Metrics is null");
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "GetMetricStatistics", async () =>
        {
            var resp = await cloudwatchClient.GetMetricStatisticsAsync(new GetMetricStatisticsRequest
            {
                Namespace = "AWS/EC2",
                MetricName = "CPUUtilization",
                StartTime = DateTime.UtcNow.AddHours(-1),
                EndTime = DateTime.UtcNow,
                Period = 300,
                Statistics = new List<string> { "Average" }
            });
            if (resp.Datapoints == null)
                throw new Exception("Datapoints is null");
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "PutMetricData", async () =>
        {
            await cloudwatchClient.PutMetricDataAsync(new PutMetricDataRequest
            {
                Namespace = namespaceName,
                MetricData = new List<MetricDatum>
                {
                    new MetricDatum
                    {
                        MetricName = "TestMetric",
                        Value = 100,
                        Unit = StandardUnit.None
                    }
                }
            });
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "GetMetricStatistics_InvalidParameterValue", async () =>
        {
            var resp = await cloudwatchClient.GetMetricStatisticsAsync(new GetMetricStatisticsRequest
            {
                Namespace = "Invalid.Namespace!@#$",
                MetricName = "TestMetric",
                StartTime = DateTime.UtcNow.AddHours(-1),
                EndTime = DateTime.UtcNow,
                Period = 300,
                Statistics = new List<string> { "Average" }
            });
            if (resp.Datapoints != null && resp.Datapoints.Count > 0)
                throw new Exception("Expected empty datapoints for invalid namespace");
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "PutMetricAlarm", async () =>
        {
            var alarmName = TestRunner.MakeUniqueName("TestAlarm");
            await cloudwatchClient.PutMetricAlarmAsync(new PutMetricAlarmRequest
            {
                AlarmName = alarmName,
                ComparisonOperator = ComparisonOperator.GreaterThanThreshold,
                EvaluationPeriods = 1,
                MetricName = "TestMetric",
                Namespace = namespaceName,
                Period = 300,
                Threshold = 50.0,
                Statistic = Statistic.Average
            });
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "DescribeAlarms", async () =>
        {
            var resp = await cloudwatchClient.DescribeAlarmsAsync(new DescribeAlarmsRequest());
            if (resp.MetricAlarms == null)
                throw new Exception("metric alarms list is nil");
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "ListDashboards", async () =>
        {
            var resp = await cloudwatchClient.ListDashboardsAsync(new ListDashboardsRequest());
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "PutMetricData_GetMetricStatistics_Roundtrip", async () =>
        {
            var testNS = TestRunner.MakeUniqueName("RoundtripNS");
            var testMetric = TestRunner.MakeUniqueName("RoundtripMetric");
            var now = DateTime.UtcNow;

            await cloudwatchClient.PutMetricDataAsync(new PutMetricDataRequest
            {
                Namespace = testNS,
                MetricData = new List<MetricDatum>
                {
                    new MetricDatum
                    {
                        MetricName = testMetric,
                        Value = 42.0,
                        Unit = StandardUnit.None,
                        Timestamp = now.AddMinutes(-5)
                    },
                    new MetricDatum
                    {
                        MetricName = testMetric,
                        Value = 58.0,
                        Unit = StandardUnit.None,
                        Timestamp = now.AddMinutes(-2)
                    }
                }
            });

            var listResp = await cloudwatchClient.ListMetricsAsync(new ListMetricsRequest
            {
                Namespace = testNS,
                MetricName = testMetric
            });
            if (listResp.Metrics.Count == 0)
                throw new Exception("metric not found in ListMetrics");

            var statsResp = await cloudwatchClient.GetMetricStatisticsAsync(new GetMetricStatisticsRequest
            {
                Namespace = testNS,
                MetricName = testMetric,
                StartTime = now.AddMinutes(-10),
                EndTime = now.AddMinutes(1),
                Period = 60,
                Statistics = new List<string> { "Sum" }
            });
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "DescribeAlarms_NonExistent", async () =>
        {
            var alarmName = TestRunner.MakeUniqueName("NonExistentAlarm");
            var resp = await cloudwatchClient.DescribeAlarmsAsync(new DescribeAlarmsRequest
            {
                AlarmNames = new List<string> { alarmName }
            });
            if (resp.MetricAlarms.Count != 0)
                throw new Exception($"expected no alarms, got {resp.MetricAlarms.Count}");
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "PutMetricAlarm_DeleteAlarm", async () =>
        {
            var alarmName = TestRunner.MakeUniqueName("DeleteAlarm");
            var testNS = TestRunner.MakeUniqueName("AlarmNS");
            await cloudwatchClient.PutMetricAlarmAsync(new PutMetricAlarmRequest
            {
                AlarmName = alarmName,
                ComparisonOperator = ComparisonOperator.GreaterThanThreshold,
                EvaluationPeriods = 1,
                MetricName = "TestMetric",
                Namespace = testNS,
                Period = 300,
                Threshold = 50.0,
                Statistic = Statistic.Average,
                AlarmDescription = "Test alarm for deletion"
            });

            var descResp = await cloudwatchClient.DescribeAlarmsAsync(new DescribeAlarmsRequest
            {
                AlarmNames = new List<string> { alarmName }
            });
            if (descResp.MetricAlarms.Count != 1)
                throw new Exception($"expected 1 alarm, got {descResp.MetricAlarms.Count}");
            if (descResp.MetricAlarms[0].AlarmDescription != "Test alarm for deletion")
                throw new Exception("alarm description mismatch");

            await cloudwatchClient.DeleteAlarmsAsync(new DeleteAlarmsRequest
            {
                AlarmNames = new List<string> { alarmName }
            });
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "GetMetricData_Basic", async () =>
        {
            var testNS = TestRunner.MakeUniqueName("MetricDataNS");
            var testMetric = TestRunner.MakeUniqueName("MetricDataMetric");
            var now = DateTime.UtcNow;

            await cloudwatchClient.PutMetricDataAsync(new PutMetricDataRequest
            {
                Namespace = testNS,
                MetricData = new List<MetricDatum>
                {
                    new MetricDatum
                    {
                        MetricName = testMetric,
                        Value = 10.0,
                        Timestamp = now.AddMinutes(-3)
                    },
                    new MetricDatum
                    {
                        MetricName = testMetric,
                        Value = 20.0,
                        Timestamp = now.AddMinutes(-1)
                    }
                }
            });

            var resp = await cloudwatchClient.GetMetricDataAsync(new GetMetricDataRequest
            {
                StartTime = now.AddMinutes(-10),
                EndTime = now.AddMinutes(1),
                MetricDataQueries = new List<MetricDataQuery>
                {
                    new MetricDataQuery
                    {
                        Id = "m1",
                        MetricStat = new MetricStat
                        {
                            Metric = new Amazon.CloudWatch.Model.Metric
                            {
                                Namespace = testNS,
                                MetricName = testMetric
                            },
                            Period = 60,
                            Stat = "Sum"
                        }
                    }
                }
            });
            if (resp.MetricDataResults == null)
                throw new Exception("MetricDataResults is null");
            if (resp.MetricDataResults.Count == 0)
                throw new Exception("expected at least 1 MetricDataResult");
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "DescribeAlarmsForMetric_Basic", async () =>
        {
            var testNS = TestRunner.MakeUniqueName("DAMFNS");
            var alarmName = TestRunner.MakeUniqueName("DAMFAlarm");
            await cloudwatchClient.PutMetricAlarmAsync(new PutMetricAlarmRequest
            {
                AlarmName = alarmName,
                ComparisonOperator = ComparisonOperator.GreaterThanThreshold,
                EvaluationPeriods = 1,
                MetricName = "CPUUtilization",
                Namespace = testNS,
                Period = 300,
                Threshold = 80.0,
                Statistic = Statistic.Average
            });

            try
            {
                var resp = await cloudwatchClient.DescribeAlarmsForMetricAsync(new DescribeAlarmsForMetricRequest
                {
                    Namespace = testNS,
                    MetricName = "CPUUtilization"
                });
                if (resp.MetricAlarms.Count == 0)
                    throw new Exception("expected at least 1 alarm for metric");
                var found = resp.MetricAlarms.Any(a => a.AlarmName == alarmName);
                if (!found)
                    throw new Exception($"alarm {alarmName} not found in DescribeAlarmsForMetric result");
            }
            finally
            {
                await cloudwatchClient.DeleteAlarmsAsync(new DeleteAlarmsRequest
                {
                    AlarmNames = new List<string> { alarmName }
                });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "SetAlarmState_Basic", async () =>
        {
            var alarmName = TestRunner.MakeUniqueName("SetStateAlarm");
            var testNS = TestRunner.MakeUniqueName("SetStateNS");
            await cloudwatchClient.PutMetricAlarmAsync(new PutMetricAlarmRequest
            {
                AlarmName = alarmName,
                ComparisonOperator = ComparisonOperator.GreaterThanThreshold,
                EvaluationPeriods = 1,
                MetricName = "TestMetric",
                Namespace = testNS,
                Period = 300,
                Threshold = 50.0,
                Statistic = Statistic.Average
            });

            try
            {
                await cloudwatchClient.SetAlarmStateAsync(new SetAlarmStateRequest
                {
                    AlarmName = alarmName,
                    StateValue = "ALARM",
                    StateReason = "Test state change"
                });

                var descResp = await cloudwatchClient.DescribeAlarmsAsync(new DescribeAlarmsRequest
                {
                    AlarmNames = new List<string> { alarmName }
                });
                if (descResp.MetricAlarms.Count != 1)
                    throw new Exception($"expected 1 alarm, got {descResp.MetricAlarms.Count}");
                if (descResp.MetricAlarms[0].StateValue != "ALARM")
                    throw new Exception($"expected ALARM state, got {descResp.MetricAlarms[0].StateValue}");
            }
            finally
            {
                await cloudwatchClient.DeleteAlarmsAsync(new DeleteAlarmsRequest
                {
                    AlarmNames = new List<string> { alarmName }
                });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "DescribeAlarms_AlarmNamePrefix", async () =>
        {
            var prefix = TestRunner.MakeUniqueName("PrefixAlarm");
            var testNS = TestRunner.MakeUniqueName("PrefixNS");

            await cloudwatchClient.PutMetricAlarmAsync(new PutMetricAlarmRequest
            {
                AlarmName = prefix + "-alpha",
                ComparisonOperator = ComparisonOperator.GreaterThanThreshold,
                EvaluationPeriods = 1,
                MetricName = "TestMetric",
                Namespace = testNS,
                Period = 300,
                Threshold = 50.0,
                Statistic = Statistic.Average
            });

            await cloudwatchClient.PutMetricAlarmAsync(new PutMetricAlarmRequest
            {
                AlarmName = prefix + "-beta",
                ComparisonOperator = ComparisonOperator.GreaterThanThreshold,
                EvaluationPeriods = 1,
                MetricName = "TestMetric",
                Namespace = testNS,
                Period = 300,
                Threshold = 50.0,
                Statistic = Statistic.Average
            });

            await cloudwatchClient.PutMetricAlarmAsync(new PutMetricAlarmRequest
            {
                AlarmName = "OtherAlarm-no-match",
                ComparisonOperator = ComparisonOperator.GreaterThanThreshold,
                EvaluationPeriods = 1,
                MetricName = "TestMetric",
                Namespace = testNS,
                Period = 300,
                Threshold = 50.0,
                Statistic = Statistic.Average
            });

            try
            {
                var resp = await cloudwatchClient.DescribeAlarmsAsync(new DescribeAlarmsRequest
                {
                    AlarmNamePrefix = prefix
                });
                if (resp.MetricAlarms.Count != 2)
                    throw new Exception($"expected 2 alarms with prefix {prefix}, got {resp.MetricAlarms.Count}");
            }
            finally
            {
                await cloudwatchClient.DeleteAlarmsAsync(new DeleteAlarmsRequest
                {
                    AlarmNames = new List<string> { prefix + "-alpha", prefix + "-beta", "OtherAlarm-no-match" }
                });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "TagResource_Basic", async () =>
        {
            var alarmName = TestRunner.MakeUniqueName("TagAlarm");
            var testNS = TestRunner.MakeUniqueName("TagNS");
            await cloudwatchClient.PutMetricAlarmAsync(new PutMetricAlarmRequest
            {
                AlarmName = alarmName,
                ComparisonOperator = ComparisonOperator.GreaterThanThreshold,
                EvaluationPeriods = 1,
                MetricName = "TestMetric",
                Namespace = testNS,
                Period = 300,
                Threshold = 50.0,
                Statistic = Statistic.Average
            });

            try
            {
                var descResp = await cloudwatchClient.DescribeAlarmsAsync(new DescribeAlarmsRequest
                {
                    AlarmNames = new List<string> { alarmName }
                });
                if (descResp.MetricAlarms.Count != 1 || string.IsNullOrEmpty(descResp.MetricAlarms[0].AlarmArn))
                    throw new Exception("failed to get alarm ARN");
                var alarmARN = descResp.MetricAlarms[0].AlarmArn;

                await cloudwatchClient.TagResourceAsync(new TagResourceRequest
                {
                    ResourceARN = alarmARN,
                    Tags = new List<Amazon.CloudWatch.Model.Tag>
                    {
                        new Amazon.CloudWatch.Model.Tag { Key = "Environment", Value = "test" },
                        new Amazon.CloudWatch.Model.Tag { Key = "Team", Value = "platform" }
                    }
                });

                var tagResp = await cloudwatchClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceARN = alarmARN
                });
                if (tagResp.Tags.Count != 2)
                    throw new Exception($"expected 2 tags, got {tagResp.Tags.Count}");
            }
            finally
            {
                await cloudwatchClient.DeleteAlarmsAsync(new DeleteAlarmsRequest
                {
                    AlarmNames = new List<string> { alarmName }
                });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "UntagResource_Basic", async () =>
        {
            var alarmName = TestRunner.MakeUniqueName("UntagAlarm");
            var testNS = TestRunner.MakeUniqueName("UntagNS");
            await cloudwatchClient.PutMetricAlarmAsync(new PutMetricAlarmRequest
            {
                AlarmName = alarmName,
                ComparisonOperator = ComparisonOperator.GreaterThanThreshold,
                EvaluationPeriods = 1,
                MetricName = "TestMetric",
                Namespace = testNS,
                Period = 300,
                Threshold = 50.0,
                Statistic = Statistic.Average
            });

            try
            {
                var descResp = await cloudwatchClient.DescribeAlarmsAsync(new DescribeAlarmsRequest
                {
                    AlarmNames = new List<string> { alarmName }
                });
                if (descResp.MetricAlarms.Count != 1 || string.IsNullOrEmpty(descResp.MetricAlarms[0].AlarmArn))
                    throw new Exception("failed to get alarm ARN");
                var alarmARN = descResp.MetricAlarms[0].AlarmArn;

                await cloudwatchClient.TagResourceAsync(new TagResourceRequest
                {
                    ResourceARN = alarmARN,
                    Tags = new List<Amazon.CloudWatch.Model.Tag>
                    {
                        new Amazon.CloudWatch.Model.Tag { Key = "Keep", Value = "yes" },
                        new Amazon.CloudWatch.Model.Tag { Key = "Remove", Value = "yes" }
                    }
                });

                await cloudwatchClient.UntagResourceAsync(new UntagResourceRequest
                {
                    ResourceARN = alarmARN,
                    TagKeys = new List<string> { "Remove" }
                });

                var tagResp = await cloudwatchClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceARN = alarmARN
                });
                if (tagResp.Tags.Count != 1)
                    throw new Exception($"expected 1 tag after untag, got {tagResp.Tags.Count}");
                if (tagResp.Tags[0].Key != "Keep")
                    throw new Exception($"expected Keep tag, got {tagResp.Tags[0].Key}");
            }
            finally
            {
                await cloudwatchClient.DeleteAlarmsAsync(new DeleteAlarmsRequest
                {
                    AlarmNames = new List<string> { alarmName }
                });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "GetMetricWidgetImage_Basic", async () =>
        {
            var widget = "{\"metrics\":[[\"AWS/EC2\",\"CPUUtilization\"]]}";
            var resp = await cloudwatchClient.GetMetricWidgetImageAsync(new GetMetricWidgetImageRequest
            {
                MetricWidget = widget
            });
            if (resp.MetricWidgetImage == null || resp.MetricWidgetImage.Length == 0)
                throw new Exception("expected non-empty image");
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "EnableAlarmActions_Basic", async () =>
        {
            var alarmName = TestRunner.MakeUniqueName("EnableAlarm");
            var testNS = TestRunner.MakeUniqueName("EnableNS");
            await cloudwatchClient.PutMetricAlarmAsync(new PutMetricAlarmRequest
            {
                AlarmName = alarmName,
                ComparisonOperator = ComparisonOperator.GreaterThanThreshold,
                EvaluationPeriods = 1,
                MetricName = "TestMetric",
                Namespace = testNS,
                Period = 300,
                Threshold = 50.0,
                Statistic = Statistic.Average
            });

            try
            {
                await cloudwatchClient.DisableAlarmActionsAsync(new DisableAlarmActionsRequest
                {
                    AlarmNames = new List<string> { alarmName }
                });

                var descResp = await cloudwatchClient.DescribeAlarmsAsync(new DescribeAlarmsRequest
                {
                    AlarmNames = new List<string> { alarmName }
                });
                if (descResp.MetricAlarms.Count != 1)
                    throw new Exception($"expected 1 alarm, got {descResp.MetricAlarms.Count}");
                if (descResp.MetricAlarms[0].ActionsEnabled == true)
                    throw new Exception("expected ActionsEnabled=false after disable");

                await cloudwatchClient.EnableAlarmActionsAsync(new EnableAlarmActionsRequest
                {
                    AlarmNames = new List<string> { alarmName }
                });

                var descResp2 = await cloudwatchClient.DescribeAlarmsAsync(new DescribeAlarmsRequest
                {
                    AlarmNames = new List<string> { alarmName }
                });
                if (descResp2.MetricAlarms[0].ActionsEnabled != true)
                    throw new Exception("expected ActionsEnabled=true after enable");
            }
            finally
            {
                await cloudwatchClient.DeleteAlarmsAsync(new DeleteAlarmsRequest
                {
                    AlarmNames = new List<string> { alarmName }
                });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "DescribeAlarmHistory_Basic", async () =>
        {
            var alarmName = TestRunner.MakeUniqueName("HistoryAlarm");
            var testNS = TestRunner.MakeUniqueName("HistoryNS");
            await cloudwatchClient.PutMetricAlarmAsync(new PutMetricAlarmRequest
            {
                AlarmName = alarmName,
                ComparisonOperator = ComparisonOperator.GreaterThanThreshold,
                EvaluationPeriods = 1,
                MetricName = "TestMetric",
                Namespace = testNS,
                Period = 300,
                Threshold = 50.0,
                Statistic = Statistic.Average
            });

            try
            {
                await cloudwatchClient.SetAlarmStateAsync(new SetAlarmStateRequest
                {
                    AlarmName = alarmName,
                    StateValue = "ALARM",
                    StateReason = "Manual alarm state change"
                });

                var histResp = await cloudwatchClient.DescribeAlarmHistoryAsync(new DescribeAlarmHistoryRequest
                {
                    AlarmName = alarmName
                });
                if (histResp.AlarmHistoryItems.Count == 0)
                    throw new Exception("expected alarm history items, got 0");
                var hasStateUpdate = histResp.AlarmHistoryItems.Any(
                    item => item.HistoryItemType == "StateUpdate");
                if (!hasStateUpdate)
                    throw new Exception("expected StateUpdate history item");
            }
            finally
            {
                await cloudwatchClient.DeleteAlarmsAsync(new DeleteAlarmsRequest
                {
                    AlarmNames = new List<string> { alarmName }
                });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "DescribeAlarmHistory_FilterByType", async () =>
        {
            var alarmName = TestRunner.MakeUniqueName("HistFilterAlarm");
            var testNS = TestRunner.MakeUniqueName("HistFilterNS");
            await cloudwatchClient.PutMetricAlarmAsync(new PutMetricAlarmRequest
            {
                AlarmName = alarmName,
                ComparisonOperator = ComparisonOperator.GreaterThanThreshold,
                EvaluationPeriods = 1,
                MetricName = "TestMetric",
                Namespace = testNS,
                Period = 300,
                Threshold = 50.0,
                Statistic = Statistic.Average
            });

            try
            {
                await cloudwatchClient.SetAlarmStateAsync(new SetAlarmStateRequest
                {
                    AlarmName = alarmName,
                    StateValue = "OK",
                    StateReason = "Recovered"
                });

                var histResp = await cloudwatchClient.DescribeAlarmHistoryAsync(new DescribeAlarmHistoryRequest
                {
                    AlarmName = alarmName,
                    HistoryItemType = "StateUpdate"
                });
                foreach (var item in histResp.AlarmHistoryItems)
                {
                    if (item.HistoryItemType != "StateUpdate")
                        throw new Exception($"expected only StateUpdate items, got {item.HistoryItemType}");
                }
                if (histResp.AlarmHistoryItems.Count == 0)
                    throw new Exception("expected at least 1 StateUpdate item");
            }
            finally
            {
                await cloudwatchClient.DeleteAlarmsAsync(new DeleteAlarmsRequest
                {
                    AlarmNames = new List<string> { alarmName }
                });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "PutCompositeAlarm_Basic", async () =>
        {
            var alarmName = TestRunner.MakeUniqueName("CompositeAlarm");
            await cloudwatchClient.PutCompositeAlarmAsync(new PutCompositeAlarmRequest
            {
                AlarmName = alarmName,
                AlarmRule = "TRUE",
                AlarmDescription = "Test composite alarm",
                ActionsEnabled = true,
                AlarmActions = new List<string> { "arn:aws:sns:us-east-1:123456789012:my-topic" },
                OKActions = new List<string> { "arn:aws:sns:us-east-1:123456789012:ok-topic" }
            });

            try
            {
                var descResp = await cloudwatchClient.DescribeAlarmsAsync(new DescribeAlarmsRequest
                {
                    AlarmTypes = new List<string> { "CompositeAlarm" }
                });
                if (descResp.CompositeAlarms.Count == 0)
                    throw new Exception("expected at least 1 composite alarm, got 0");
                var found = false;
                foreach (var a in descResp.CompositeAlarms)
                {
                    if (a.AlarmName == alarmName)
                    {
                        found = true;
                        if (a.AlarmRule != "TRUE")
                            throw new Exception($"expected AlarmRule=TRUE, got {a.AlarmRule}");
                        if (a.AlarmDescription != "Test composite alarm")
                            throw new Exception("description mismatch");
                        break;
                    }
                }
                if (!found)
                    throw new Exception($"composite alarm {alarmName} not found");
            }
            finally
            {
                await cloudwatchClient.DeleteAlarmsAsync(new DeleteAlarmsRequest
                {
                    AlarmNames = new List<string> { alarmName }
                });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudwatch", "PutCompositeAlarm_DeleteAlarm", async () =>
        {
            var alarmName = TestRunner.MakeUniqueName("CompDelAlarm");
            await cloudwatchClient.PutCompositeAlarmAsync(new PutCompositeAlarmRequest
            {
                AlarmName = alarmName,
                AlarmRule = "FALSE"
            });

            var descResp = await cloudwatchClient.DescribeAlarmsAsync(new DescribeAlarmsRequest
            {
                AlarmTypes = new List<string> { "CompositeAlarm" }
            });
            if (descResp.CompositeAlarms.Count == 0)
                throw new Exception("expected at least 1 composite alarm, got 0");

            await cloudwatchClient.DeleteAlarmsAsync(new DeleteAlarmsRequest
            {
                AlarmNames = new List<string> { alarmName }
            });

            var descResp2 = await cloudwatchClient.DescribeAlarmsAsync(new DescribeAlarmsRequest
            {
                AlarmTypes = new List<string> { "CompositeAlarm" }
            });
            foreach (var a in descResp2.CompositeAlarms)
            {
                if (a.AlarmName == alarmName)
                    throw new Exception($"alarm {alarmName} should have been deleted");
            }
        }));

        return results;
    }
}
