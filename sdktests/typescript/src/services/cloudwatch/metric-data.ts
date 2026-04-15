import type { CloudWatchClient } from '@aws-sdk/client-cloudwatch';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName } from '../../helpers.js';
import {
  PutMetricDataCommand,
  ListMetricsCommand,
  GetMetricStatisticsCommand,
  GetMetricDataCommand,
  GetMetricWidgetImageCommand,
  ListDashboardsCommand,
} from '@aws-sdk/client-cloudwatch';

export async function runMetricDataTests(
  runner: TestRunner,
  client: CloudWatchClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const now = new Date();
  const namespace = makeUniqueName('TestNamespace');
  const metricName = makeUniqueName('TestMetric');

  results.push(await runner.runTest('cloudwatch', 'PutMetricData', async () => {
    await client.send(new PutMetricDataCommand({
      Namespace: namespace,
      MetricData: [{
        MetricName: metricName,
        Value: 42.0,
        Timestamp: now,
      }],
    }));
  }));

  results.push(await runner.runTest('cloudwatch', 'ListMetrics', async () => {
    const resp = await client.send(new ListMetricsCommand({ Namespace: namespace }));
    if (!resp.Metrics) throw new Error('expected metrics to be defined');
  }));

  results.push(await runner.runTest('cloudwatch', 'GetMetricStatistics', async () => {
    const resp = await client.send(new GetMetricStatisticsCommand({
      Namespace: namespace,
      MetricName: metricName,
      StartTime: new Date(now.getTime() - 3600000),
      EndTime: now,
      Period: 300,
      Statistics: ['Average'],
    }));
    if (!resp.Datapoints) throw new Error('expected datapoints to be defined');
  }));

  results.push(await runner.runTest('cloudwatch', 'ListDashboards', async () => {
    const resp = await client.send(new ListDashboardsCommand({}));
    if (resp.DashboardEntries?.length) {
      throw new Error(`expected no dashboards, got ${resp.DashboardEntries.length}`);
    }
  }));

  results.push(await runner.runTest('cloudwatch', 'PutMetricData_GetMetricStatistics_Roundtrip', async () => {
    const testNS = makeUniqueName('RoundtripNS');
    const testMetric = makeUniqueName('RoundtripMetric');

    await client.send(new PutMetricDataCommand({
      Namespace: testNS,
      MetricData: [
        { MetricName: testMetric, Value: 42.0, Unit: 'None', Timestamp: new Date(now.getTime() - 300000) },
        { MetricName: testMetric, Value: 58.0, Unit: 'None', Timestamp: new Date(now.getTime() - 120000) },
      ],
    }));

    const listResp = await client.send(new ListMetricsCommand({
      Namespace: testNS, MetricName: testMetric,
    }));
    if (!listResp.Metrics?.length) throw new Error('metric not found in ListMetrics');

    await client.send(new GetMetricStatisticsCommand({
      Namespace: testNS,
      MetricName: testMetric,
      StartTime: new Date(now.getTime() - 600000),
      EndTime: new Date(now.getTime() + 60000),
      Period: 60,
      Statistics: ['Sum'],
    }));
  }));

  results.push(await runner.runTest('cloudwatch', 'GetMetricData_Basic', async () => {
    const testNS = makeUniqueName('MetricDataNS');
    const testMetric = makeUniqueName('MetricDataMetric');

    await client.send(new PutMetricDataCommand({
      Namespace: testNS,
      MetricData: [
        { MetricName: testMetric, Value: 10.0, Timestamp: new Date(now.getTime() - 180000) },
        { MetricName: testMetric, Value: 20.0, Timestamp: new Date(now.getTime() - 60000) },
      ],
    }));

    const resp = await client.send(new GetMetricDataCommand({
      StartTime: new Date(now.getTime() - 600000),
      EndTime: new Date(now.getTime() + 60000),
      MetricDataQueries: [{
        Id: 'm1',
        MetricStat: {
          Metric: { Namespace: testNS, MetricName: testMetric },
          Period: 60,
          Stat: 'Sum',
        },
      }],
    }));
    if (!resp.MetricDataResults?.length) throw new Error('expected at least 1 MetricDataResult');
  }));

  results.push(await runner.runTest('cloudwatch', 'GetMetricWidgetImage_Basic', async () => {
    const resp = await client.send(new GetMetricWidgetImageCommand({
      MetricWidget: '{"metrics":[["AWS/EC2","CPUUtilization"]]}',
    }));
    if (!resp.MetricWidgetImage?.length) throw new Error('expected non-empty widget image');
  }));

  return results;
}
