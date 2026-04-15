import type { CloudWatchClient } from '@aws-sdk/client-cloudwatch';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';
import {
  PutCompositeAlarmCommand,
  DescribeAlarmsCommand,
  DeleteAlarmsCommand,
} from '@aws-sdk/client-cloudwatch';

export async function runCompositeAlarmTests(
  runner: TestRunner,
  client: CloudWatchClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('cloudwatch', 'PutCompositeAlarm_Basic', async () => {
    const alarmName = makeUniqueName('CompositeAlarm');
    await client.send(new PutCompositeAlarmCommand({
      AlarmName: alarmName,
      AlarmRule: 'TRUE',
      AlarmDescription: 'Test composite alarm',
      ActionsEnabled: true,
      AlarmActions: ['arn:aws:sns:us-east-1:123456789012:my-topic'],
      OKActions: ['arn:aws:sns:us-east-1:123456789012:ok-topic'],
    }));
    try {
      const descResp = await client.send(new DescribeAlarmsCommand({ AlarmTypes: ['CompositeAlarm'] }));
      if (!descResp.CompositeAlarms?.length) throw new Error('expected at least 1 composite alarm');
      const target = descResp.CompositeAlarms.find(a => a.AlarmName === alarmName);
      if (!target) throw new Error(`composite alarm ${alarmName} not found`);
      if (target.AlarmRule !== 'TRUE') throw new Error(`expected AlarmRule=TRUE, got ${target.AlarmRule}`);
      if (target.AlarmDescription !== 'Test composite alarm') throw new Error('description mismatch');
    } finally {
      await safeCleanup(() => client.send(new DeleteAlarmsCommand({ AlarmNames: [alarmName] })));
    }
  }));

  results.push(await runner.runTest('cloudwatch', 'PutCompositeAlarm_DeleteAlarm', async () => {
    const alarmName = makeUniqueName('CompDelAlarm');
    await client.send(new PutCompositeAlarmCommand({
      AlarmName: alarmName,
      AlarmRule: 'FALSE',
    }));
    try {
      const descResp = await client.send(new DescribeAlarmsCommand({ AlarmTypes: ['CompositeAlarm'] }));
      if (!descResp.CompositeAlarms?.length) throw new Error('expected at least 1 composite alarm');

      await client.send(new DeleteAlarmsCommand({ AlarmNames: [alarmName] }));

      const descResp2 = await client.send(new DescribeAlarmsCommand({ AlarmTypes: ['CompositeAlarm'] }));
      if (descResp2.CompositeAlarms?.some(a => a.AlarmName === alarmName)) {
        throw new Error(`alarm ${alarmName} should have been deleted`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteAlarmsCommand({ AlarmNames: [alarmName] })));
    }
  }));

  return results;
}
