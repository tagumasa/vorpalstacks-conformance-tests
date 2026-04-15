import {
  CloudWatchLogsClient,
  CreateLogGroupCommand,
  DeleteLogGroupCommand,
  GetLogEventsCommand,
  CreateLogStreamCommand,
  PutLogEventsCommand,
  DescribeLogGroupsCommand,
} from '@aws-sdk/client-cloudwatch-logs';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, assertErrorContains, safeCleanup } from '../../helpers.js';

export async function runErrorAndRoundtripTests(
  runner: TestRunner,
  client: CloudWatchLogsClient,
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('logs', 'CreateLogGroup_Duplicate', async () => {
    const dupGroupName = makeUniqueName('DupLogGroup');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: dupGroupName }));
      try {
        await client.send(new CreateLogGroupCommand({ logGroupName: dupGroupName }));
        throw new Error('expected error for duplicate log group');
      } catch (e) {
        if (e instanceof Error && e.message.startsWith('expected error')) throw e;
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: dupGroupName })));
    }
  }));

  results.push(await runner.runTest('logs', 'DeleteLogGroup_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new DeleteLogGroupCommand({ logGroupName: 'nonexistent-log-group-xyz' }));
    } catch (e) {
      err = e;
    }
    assertErrorContains(err, 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest('logs', 'PutLogEvents_GetLogEvents_Roundtrip', async () => {
    const rtGroupName = makeUniqueName('RTLogGroup');
    const rtStreamName = makeUniqueName('RTLogStream');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: rtGroupName }));
      await client.send(new CreateLogStreamCommand({ logGroupName: rtGroupName, logStreamName: rtStreamName }));

      const testMessage = 'roundtrip-log-message-verify-12345';
      await client.send(new PutLogEventsCommand({
        logGroupName: rtGroupName,
        logStreamName: rtStreamName,
        logEvents: [{ message: testMessage, timestamp: Date.now() }],
      }));

      const resp = await client.send(new GetLogEventsCommand({ logGroupName: rtGroupName, logStreamName: rtStreamName }));
      if (!resp.events?.length) throw new Error('no events returned');
      if (resp.events[0].message !== testMessage) {
        throw new Error(`message mismatch: got ${resp.events[0].message}, want ${testMessage}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: rtGroupName })));
    }
  }));

  results.push(await runner.runTest('logs', 'DescribeLogGroups_ContainsCreated', async () => {
    const dlgName = makeUniqueName('DLGGroup');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: dlgName }));
      const resp = await client.send(new DescribeLogGroupsCommand({ logGroupNamePrefix: dlgName }));
      if (resp.logGroups?.length !== 1) throw new Error(`expected 1 log group, got ${resp.logGroups?.length}`);
      if (resp.logGroups[0].logGroupName !== dlgName) throw new Error('log group name mismatch');
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: dlgName })));
    }
  }));
}
