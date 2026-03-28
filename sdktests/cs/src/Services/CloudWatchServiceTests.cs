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

        return results;
    }
}
