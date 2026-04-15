import type { CloudWatchClient } from '@aws-sdk/client-cloudwatch';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';
import {
  PutMetricAlarmCommand,
  DescribeAlarmsCommand,
  DeleteAlarmsCommand,
  SetAlarmStateCommand,
  DescribeAlarmHistoryCommand,
} from '@aws-sdk/client-cloudwatch';

export async function runAlarmStateTests(
  runner: TestRunner,
  client: CloudWatchClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('cloudwatch', 'SetAlarmState_Basic', async () => {
    const alarmName = makeUniqueName('SetStateAlarm');
    const testNS = makeUniqueName('SetStateNS');
    await client.send(new PutMetricAlarmCommand({
      AlarmName: alarmName,
      ComparisonOperator: 'GreaterThanThreshold',
      EvaluationPeriods: 1,
      MetricName: 'TestMetric',
      Namespace: testNS,
      Period: 300,
      Threshold: 50.0,
      Statistic: 'Average',
    }));
    try {
      await client.send(new SetAlarmStateCommand({
        AlarmName: alarmName,
        StateValue: 'ALARM',
        StateReason: 'Test state change',
      }));
      const descResp = await client.send(new DescribeAlarmsCommand({ AlarmNames: [alarmName] }));
      if (descResp.MetricAlarms?.length !== 1) {
        throw new Error(`expected 1 alarm, got ${descResp.MetricAlarms?.length ?? 0}`);
      }
      if (descResp.MetricAlarms[0].StateValue !== 'ALARM') {
        throw new Error(`expected ALARM state, got ${descResp.MetricAlarms[0].StateValue}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteAlarmsCommand({ AlarmNames: [alarmName] })));
    }
  }));

  results.push(await runner.runTest('cloudwatch', 'DescribeAlarmHistory_Basic', async () => {
    const alarmName = makeUniqueName('HistoryAlarm');
    const testNS = makeUniqueName('HistoryNS');
    await client.send(new PutMetricAlarmCommand({
      AlarmName: alarmName,
      ComparisonOperator: 'GreaterThanThreshold',
      EvaluationPeriods: 1,
      MetricName: 'TestMetric',
      Namespace: testNS,
      Period: 300,
      Threshold: 50.0,
      Statistic: 'Average',
    }));
    try {
      await client.send(new SetAlarmStateCommand({
        AlarmName: alarmName,
        StateValue: 'ALARM',
        StateReason: 'Manual alarm state change',
      }));
      const histResp = await client.send(new DescribeAlarmHistoryCommand({ AlarmName: alarmName }));
      if (!histResp.AlarmHistoryItems?.length) throw new Error('expected alarm history items');
      if (!histResp.AlarmHistoryItems.some(item => item.HistoryItemType === 'StateUpdate')) {
        throw new Error('expected StateUpdate history item');
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteAlarmsCommand({ AlarmNames: [alarmName] })));
    }
  }));

  results.push(await runner.runTest('cloudwatch', 'DescribeAlarmHistory_FilterByType', async () => {
    const alarmName = makeUniqueName('HistFilterAlarm');
    const testNS = makeUniqueName('HistFilterNS');
    await client.send(new PutMetricAlarmCommand({
      AlarmName: alarmName,
      ComparisonOperator: 'GreaterThanThreshold',
      EvaluationPeriods: 1,
      MetricName: 'TestMetric',
      Namespace: testNS,
      Period: 300,
      Threshold: 50.0,
      Statistic: 'Average',
    }));
    try {
      await client.send(new SetAlarmStateCommand({
        AlarmName: alarmName,
        StateValue: 'OK',
        StateReason: 'Recovered',
      }));
      const histResp = await client.send(new DescribeAlarmHistoryCommand({
        AlarmName: alarmName,
        HistoryItemType: 'StateUpdate',
      }));
      if (!histResp.AlarmHistoryItems?.length) {
        throw new Error('expected at least 1 StateUpdate item');
      }
      for (const item of histResp.AlarmHistoryItems) {
        if (item.HistoryItemType !== 'StateUpdate') {
          throw new Error(`expected only StateUpdate items, got ${item.HistoryItemType}`);
        }
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteAlarmsCommand({ AlarmNames: [alarmName] })));
    }
  }));

  return results;
}
