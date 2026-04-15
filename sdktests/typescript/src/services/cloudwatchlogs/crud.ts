import {
  CloudWatchLogsClient,
  CreateLogGroupCommand,
  DescribeLogGroupsCommand,
  DescribeLogStreamsCommand,
  CreateLogStreamCommand,
  PutLogEventsCommand,
  GetLogEventsCommand,
  FilterLogEventsCommand,
  PutRetentionPolicyCommand,
  DeleteLogStreamCommand,
  DeleteLogGroupCommand,
} from '@aws-sdk/client-cloudwatch-logs';
import type { TestRunner, TestResult } from '../../runner.js';

export async function runCRUDTests(
  runner: TestRunner,
  client: CloudWatchLogsClient,
  logGroupName: string,
  logStreamName: string,
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('logs', 'CreateLogGroup', async () => {
    await client.send(new CreateLogGroupCommand({ logGroupName }));
  }));

  results.push(await runner.runTest('logs', 'DescribeLogGroups', async () => {
    const resp = await client.send(new DescribeLogGroupsCommand({}));
    if (!resp.logGroups?.length) throw new Error('expected logGroups to be non-empty');
  }));

  results.push(await runner.runTest('logs', 'DescribeLogStreams', async () => {
    const resp = await client.send(new DescribeLogStreamsCommand({ logGroupName }));
    if (!resp.logStreams) throw new Error('expected logStreams to be defined');
  }));

  results.push(await runner.runTest('logs', 'CreateLogStream', async () => {
    await client.send(new CreateLogStreamCommand({ logGroupName, logStreamName }));
  }));

  results.push(await runner.runTest('logs', 'PutLogEvents', async () => {
    await client.send(new PutLogEventsCommand({
      logGroupName,
      logStreamName,
      logEvents: [{ message: 'Test log message', timestamp: Date.now() }],
    }));
  }));

  results.push(await runner.runTest('logs', 'GetLogEvents', async () => {
    const resp = await client.send(new GetLogEventsCommand({ logGroupName, logStreamName }));
    if (!resp.events?.length) throw new Error('expected events to be non-empty');
  }));

  results.push(await runner.runTest('logs', 'FilterLogEvents', async () => {
    const resp = await client.send(new FilterLogEventsCommand({ logGroupName }));
    if (!resp.events?.length) throw new Error('expected events to be non-empty');
  }));

  results.push(await runner.runTest('logs', 'PutRetentionPolicy', async () => {
    await client.send(new PutRetentionPolicyCommand({ logGroupName, retentionInDays: 7 }));
  }));

  results.push(await runner.runTest('logs', 'DeleteLogStream', async () => {
    await client.send(new DeleteLogStreamCommand({ logGroupName, logStreamName }));
  }));

  results.push(await runner.runTest('logs', 'DeleteLogGroup', async () => {
    await client.send(new DeleteLogGroupCommand({ logGroupName }));
  }));
}
