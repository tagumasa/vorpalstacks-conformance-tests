import type { CloudWatchClient } from '@aws-sdk/client-cloudwatch';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';
import {
  PutMetricAlarmCommand,
  DescribeAlarmsCommand,
  DeleteAlarmsCommand,
  TagResourceCommand,
  UntagResourceCommand,
  ListTagsForResourceCommand,
  EnableAlarmActionsCommand,
  DisableAlarmActionsCommand,
} from '@aws-sdk/client-cloudwatch';

export async function runTagAndActionTests(
  runner: TestRunner,
  client: CloudWatchClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('cloudwatch', 'TagResource_Basic', async () => {
    const alarmName = makeUniqueName('TagAlarm');
    const testNS = makeUniqueName('TagNS');
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
      const descResp = await client.send(new DescribeAlarmsCommand({ AlarmNames: [alarmName] }));
      const alarmARN = descResp.MetricAlarms?.[0]?.AlarmArn;
      if (!alarmARN) throw new Error('failed to get alarm ARN');

      await client.send(new TagResourceCommand({
        ResourceARN: alarmARN,
        Tags: [{ Key: 'Environment', Value: 'test' }, { Key: 'Team', Value: 'platform' }],
      }));

      const tagResp = await client.send(new ListTagsForResourceCommand({ ResourceARN: alarmARN }));
      if (tagResp.Tags?.length !== 2) {
        throw new Error(`expected 2 tags, got ${tagResp.Tags?.length ?? 0}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteAlarmsCommand({ AlarmNames: [alarmName] })));
    }
  }));

  results.push(await runner.runTest('cloudwatch', 'UntagResource_Basic', async () => {
    const alarmName = makeUniqueName('UntagAlarm');
    const testNS = makeUniqueName('UntagNS');
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
      const descResp = await client.send(new DescribeAlarmsCommand({ AlarmNames: [alarmName] }));
      const alarmARN = descResp.MetricAlarms?.[0]?.AlarmArn;
      if (!alarmARN) throw new Error('failed to get alarm ARN');

      await client.send(new TagResourceCommand({
        ResourceARN: alarmARN,
        Tags: [{ Key: 'Keep', Value: 'yes' }, { Key: 'Remove', Value: 'yes' }],
      }));
      await client.send(new UntagResourceCommand({
        ResourceARN: alarmARN, TagKeys: ['Remove'],
      }));

      const tagResp = await client.send(new ListTagsForResourceCommand({ ResourceARN: alarmARN }));
      if (tagResp.Tags?.length !== 1) {
        throw new Error(`expected 1 tag after untag, got ${tagResp.Tags?.length ?? 0}`);
      }
      if (tagResp.Tags[0].Key !== 'Keep') {
        throw new Error(`expected Keep tag, got ${tagResp.Tags[0].Key}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteAlarmsCommand({ AlarmNames: [alarmName] })));
    }
  }));

  results.push(await runner.runTest('cloudwatch', 'EnableAlarmActions_Basic', async () => {
    const alarmName = makeUniqueName('EnableAlarm');
    const testNS = makeUniqueName('EnableNS');
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
      await client.send(new DisableAlarmActionsCommand({ AlarmNames: [alarmName] }));
      const descResp = await client.send(new DescribeAlarmsCommand({ AlarmNames: [alarmName] }));
      if (descResp.MetricAlarms?.length !== 1) {
        throw new Error(`expected 1 alarm, got ${descResp.MetricAlarms?.length ?? 0}`);
      }
      if (descResp.MetricAlarms[0].ActionsEnabled === true) {
        throw new Error('expected ActionsEnabled=false after disable');
      }

      await client.send(new EnableAlarmActionsCommand({ AlarmNames: [alarmName] }));
      const descResp2 = await client.send(new DescribeAlarmsCommand({ AlarmNames: [alarmName] }));
      if (descResp2.MetricAlarms?.[0]?.ActionsEnabled !== true) {
        throw new Error('expected ActionsEnabled=true after enable');
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteAlarmsCommand({ AlarmNames: [alarmName] })));
    }
  }));

  return results;
}
