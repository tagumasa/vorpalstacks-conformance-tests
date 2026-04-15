import type { CloudWatchClient } from '@aws-sdk/client-cloudwatch';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';
import {
  PutMetricAlarmCommand,
  DescribeAlarmsCommand,
  DeleteAlarmsCommand,
  DescribeAlarmsForMetricCommand,
} from '@aws-sdk/client-cloudwatch';

export async function runAlarmCrudTests(
  runner: TestRunner,
  client: CloudWatchClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('cloudwatch', 'PutMetricAlarm', async () => {
    const alarmName = makeUniqueName('TestAlarm');
    try {
      await client.send(new PutMetricAlarmCommand({
        AlarmName: alarmName,
        ComparisonOperator: 'GreaterThanThreshold',
        EvaluationPeriods: 1,
        MetricName: 'TestMetric',
        Namespace: makeUniqueName('AlarmNS'),
        Period: 300,
        Threshold: 50.0,
        Statistic: 'Average',
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteAlarmsCommand({ AlarmNames: [alarmName] })));
    }
  }));

  results.push(await runner.runTest('cloudwatch', 'DescribeAlarms', async () => {
    const resp = await client.send(new DescribeAlarmsCommand({}));
    for (const alarm of resp.MetricAlarms ?? []) {
      if (!alarm.AlarmName) throw new Error('expected every alarm to have a name');
    }
  }));

  results.push(await runner.runTest('cloudwatch', 'DescribeAlarms_NonExistent', async () => {
    const resp = await client.send(new DescribeAlarmsCommand({
      AlarmNames: [makeUniqueName('NonExistentAlarm')],
    }));
    if (resp.MetricAlarms?.length) {
      throw new Error(`expected no alarms, got ${resp.MetricAlarms.length}`);
    }
  }));

  results.push(await runner.runTest('cloudwatch', 'PutMetricAlarm_DeleteAlarm', async () => {
    const alarmName = makeUniqueName('DeleteAlarm');
    const testNS = makeUniqueName('AlarmNS');
    await client.send(new PutMetricAlarmCommand({
      AlarmName: alarmName,
      ComparisonOperator: 'GreaterThanThreshold',
      EvaluationPeriods: 1,
      MetricName: 'TestMetric',
      Namespace: testNS,
      Period: 300,
      Threshold: 50.0,
      Statistic: 'Average',
      AlarmDescription: 'Test alarm for deletion',
    }));

    const descResp = await client.send(new DescribeAlarmsCommand({ AlarmNames: [alarmName] }));
    if (descResp.MetricAlarms?.length !== 1) {
      throw new Error(`expected 1 alarm, got ${descResp.MetricAlarms?.length ?? 0}`);
    }
    if (descResp.MetricAlarms[0].AlarmDescription !== 'Test alarm for deletion') {
      throw new Error('alarm description mismatch');
    }

    await client.send(new DeleteAlarmsCommand({ AlarmNames: [alarmName] }));
  }));

  results.push(await runner.runTest('cloudwatch', 'DescribeAlarmsForMetric_Basic', async () => {
    const alarmName = makeUniqueName('DAMFAlarm');
    const testNS = makeUniqueName('DAMFNS');
    await client.send(new PutMetricAlarmCommand({
      AlarmName: alarmName,
      ComparisonOperator: 'GreaterThanThreshold',
      EvaluationPeriods: 1,
      MetricName: 'CPUUtilization',
      Namespace: testNS,
      Period: 300,
      Threshold: 80.0,
      Statistic: 'Average',
    }));
    try {
      const resp = await client.send(new DescribeAlarmsForMetricCommand({
        Namespace: testNS, MetricName: 'CPUUtilization',
      }));
      if (!resp.MetricAlarms?.length) throw new Error('expected at least 1 alarm for metric');
      if (!resp.MetricAlarms.some(a => a.AlarmName === alarmName)) {
        throw new Error(`alarm ${alarmName} not found in DescribeAlarmsForMetric result`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteAlarmsCommand({ AlarmNames: [alarmName] })));
    }
  }));

  results.push(await runner.runTest('cloudwatch', 'DescribeAlarms_AlarmNamePrefix', async () => {
    const prefix = makeUniqueName('PrefixAlarm');
    const testNS = makeUniqueName('PrefixNS');
    const alpha = `${prefix}-alpha`;
    const beta = `${prefix}-beta`;
    const other = 'OtherAlarm-no-match';
    const alarmNames = [alpha, beta, other];

    for (const name of alarmNames) {
      await client.send(new PutMetricAlarmCommand({
        AlarmName: name,
        ComparisonOperator: 'GreaterThanThreshold',
        EvaluationPeriods: 1,
        MetricName: 'TestMetric',
        Namespace: testNS,
        Period: 300,
        Threshold: 50.0,
        Statistic: 'Average',
      }));
    }
    try {
      const resp = await client.send(new DescribeAlarmsCommand({ AlarmNamePrefix: prefix }));
      if (resp.MetricAlarms?.length !== 2) {
        throw new Error(`expected 2 alarms with prefix, got ${resp.MetricAlarms?.length ?? 0}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteAlarmsCommand({ AlarmNames: alarmNames })));
    }
  }));

  return results;
}
