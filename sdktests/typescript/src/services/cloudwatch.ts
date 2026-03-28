import {
  CloudWatchClient,
  PutMetricDataCommand,
  ListMetricsCommand,
  GetMetricStatisticsCommand,
  PutMetricAlarmCommand,
  DescribeAlarmsCommand,
  ListDashboardsCommand,
  DeleteAlarmsCommand,
} from "@aws-sdk/client-cloudwatch";
import { TestRunner, TestResult } from "../runner";

export async function runCloudWatchTests(
  runner: TestRunner,
  client: CloudWatchClient,
  region: string
): Promise<TestResult[]> {
  const namespace = `TestNamespace-${Date.now()}`;
  const metricName = `TestMetric-${Date.now()}`;

  const results: TestResult[] = [];

  results.push(await runner.runTest("cloudwatch", "PutMetricData", async () => {
    const command = new PutMetricDataCommand({
      Namespace: namespace,
      MetricData: [
        {
          MetricName: metricName,
          Value: 42.0,
          Timestamp: new Date(),
        },
      ],
    });
    await client.send(command);
  }));

  results.push(await runner.runTest("cloudwatch", "ListMetrics", async () => {
    const command = new ListMetricsCommand({
      Namespace: namespace,
    });
    const response = await client.send(command);
    if (!response.Metrics) {
      throw new Error("metrics list is nil");
    }
  }));

  results.push(await runner.runTest("cloudwatch", "GetMetricStatistics", async () => {
    const now = new Date();
    const command = new GetMetricStatisticsCommand({
      Namespace: namespace,
      MetricName: metricName,
      StartTime: new Date(now.getTime() - 1 * 60 * 60 * 1000),
      EndTime: now,
      Period: 300,
      Statistics: ["Average"],
    });
    await client.send(command);
  }));

  results.push(await runner.runTest("cloudwatch", "PutMetricAlarm", async () => {
    const alarmName = `TestAlarm-${Date.now()}`;
    const command = new PutMetricAlarmCommand({
      AlarmName: alarmName,
      ComparisonOperator: "GreaterThanThreshold",
      EvaluationPeriods: 1,
      MetricName: metricName,
      Namespace: namespace,
      Period: 300,
      Threshold: 50.0,
      Statistic: "Average",
    });
    await client.send(command);
  }));

  results.push(await runner.runTest("cloudwatch", "DescribeAlarms", async () => {
    const command = new DescribeAlarmsCommand({});
    const response = await client.send(command);
    if (!response.MetricAlarms) {
      throw new Error("metric alarms list is nil");
    }
  }));

  results.push(await runner.runTest("cloudwatch", "ListDashboards", async () => {
    const command = new ListDashboardsCommand({});
    await client.send(command);
  }));

  results.push(await runner.runTest("cloudwatch", "PutMetricData_GetMetricStatistics_Roundtrip", async () => {
    const testNS = `RoundtripNS-${Date.now()}`;
    const testMetric = `RoundtripMetric-${Date.now()}`;
    const now = new Date();

    await client.send(new PutMetricDataCommand({
      Namespace: testNS,
      MetricData: [
        {
          MetricName: testMetric,
          Value: 42.0,
          Unit: "None",
          Timestamp: new Date(now.getTime() - 5 * 60 * 1000),
        },
        {
          MetricName: testMetric,
          Value: 58.0,
          Unit: "None",
          Timestamp: new Date(now.getTime() - 2 * 60 * 1000),
        },
      ],
    }));

    const listResp = await client.send(new ListMetricsCommand({
      Namespace: testNS,
      MetricName: testMetric,
    }));
    if (!listResp.Metrics || listResp.Metrics.length === 0) {
      throw new Error("metric not found in ListMetrics");
    }

    await client.send(new GetMetricStatisticsCommand({
      Namespace: testNS,
      MetricName: testMetric,
      StartTime: new Date(now.getTime() - 10 * 60 * 1000),
      EndTime: new Date(now.getTime() + 1 * 60 * 1000),
      Period: 60,
      Statistics: ["Sum"],
    }));
  }));

  results.push(await runner.runTest("cloudwatch", "DescribeAlarms_NonExistent", async () => {
    const alarmName = `NonExistentAlarm-${Date.now()}`;
    const command = new DescribeAlarmsCommand({
      AlarmNames: [alarmName],
    });
    const response = await client.send(command);
    if (response.MetricAlarms && response.MetricAlarms.length !== 0) {
      throw new Error(`expected no alarms, got ${response.MetricAlarms.length}`);
    }
  }));

  results.push(await runner.runTest("cloudwatch", "PutMetricAlarm_DeleteAlarm", async () => {
    const alarmName = `DeleteAlarm-${Date.now()}`;
    const testNS = `AlarmNS-${Date.now()}`;

    await client.send(new PutMetricAlarmCommand({
      AlarmName: alarmName,
      ComparisonOperator: "GreaterThanThreshold",
      EvaluationPeriods: 1,
      MetricName: "TestMetric",
      Namespace: testNS,
      Period: 300,
      Threshold: 50.0,
      Statistic: "Average",
      AlarmDescription: "Test alarm for deletion",
    }));

    const descResp = await client.send(new DescribeAlarmsCommand({
      AlarmNames: [alarmName],
    }));
    if (!descResp.MetricAlarms || descResp.MetricAlarms.length !== 1) {
      throw new Error(`expected 1 alarm, got ${descResp.MetricAlarms?.length}`);
    }
    if (descResp.MetricAlarms[0].AlarmDescription !== "Test alarm for deletion") {
      throw new Error("alarm description mismatch");
    }

    await client.send(new DeleteAlarmsCommand({
      AlarmNames: [alarmName],
    }));
  }));

  return results;
}
