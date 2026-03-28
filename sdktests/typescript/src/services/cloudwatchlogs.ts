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
import { ResourceAlreadyExistsException, ResourceNotFoundException } from '@aws-sdk/client-cloudwatch-logs';
import { TestRunner } from '../runner.js';
import { TestResult } from '../runner.js';

function makeUniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 999999)}`;
}

export async function runCloudWatchLogsTests(
  runner: TestRunner,
  logsClient: CloudWatchLogsClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const logGroupName = makeUniqueName('TestLogGroup');
  const logStreamName = makeUniqueName('TestLogStream');

  try {
    // CreateLogGroup
    results.push(
      await runner.runTest('logs', 'CreateLogGroup', async () => {
        await logsClient.send(
          new CreateLogGroupCommand({ logGroupName: logGroupName })
        );
      })
    );

    // DescribeLogGroups
    results.push(
      await runner.runTest('logs', 'DescribeLogGroups', async () => {
        const resp = await logsClient.send(new DescribeLogGroupsCommand({}));
        if (!resp.logGroups) throw new Error('logGroups is null');
      })
    );

    // DescribeLogStreams
    results.push(
      await runner.runTest('logs', 'DescribeLogStreams', async () => {
        const resp = await logsClient.send(
          new DescribeLogStreamsCommand({ logGroupName: logGroupName })
        );
        if (!resp.logStreams) throw new Error('logStreams is null');
      })
    );

    // CreateLogStream
    results.push(
      await runner.runTest('logs', 'CreateLogStream', async () => {
        await logsClient.send(
          new CreateLogStreamCommand({
            logGroupName: logGroupName,
            logStreamName: logStreamName,
          })
        );
      })
    );

    // PutLogEvents
    results.push(
      await runner.runTest('logs', 'PutLogEvents', async () => {
        await logsClient.send(
          new PutLogEventsCommand({
            logGroupName: logGroupName,
            logStreamName: logStreamName,
            logEvents: [
              {
                message: 'Test log message',
                timestamp: Date.now(),
              },
            ],
          })
        );
      })
    );

    // GetLogEvents
    results.push(
      await runner.runTest('logs', 'GetLogEvents', async () => {
        const resp = await logsClient.send(
          new GetLogEventsCommand({
            logGroupName: logGroupName,
            logStreamName: logStreamName,
          })
        );
        if (!resp.events) throw new Error('events is null');
      })
    );

    // FilterLogEvents
    results.push(
      await runner.runTest('logs', 'FilterLogEvents', async () => {
        const resp = await logsClient.send(
          new FilterLogEventsCommand({ logGroupName: logGroupName })
        );
        if (!resp.events) throw new Error('events is null');
      })
    );

    // PutRetentionPolicy
    results.push(
      await runner.runTest('logs', 'PutRetentionPolicy', async () => {
        await logsClient.send(
          new PutRetentionPolicyCommand({
            logGroupName: logGroupName,
            retentionInDays: 7,
          })
        );
      })
    );

    // DeleteLogStream
    results.push(
      await runner.runTest('logs', 'DeleteLogStream', async () => {
        await logsClient.send(
          new DeleteLogStreamCommand({
            logGroupName: logGroupName,
            logStreamName: logStreamName,
          })
        );
      })
    );

    // DeleteLogGroup
    results.push(
      await runner.runTest('logs', 'DeleteLogGroup', async () => {
        await logsClient.send(
          new DeleteLogGroupCommand({ logGroupName: logGroupName })
        );
      })
    );

  } finally {
    try {
      await logsClient.send(
        new DeleteLogGroupCommand({ logGroupName: logGroupName })
      );
    } catch { /* ignore */ }
  }

  // Error cases

  // CreateLogGroup_Duplicate
  const dupGroupName = makeUniqueName('DupLogGroup');
  results.push(
    await runner.runTest('logs', 'CreateLogGroup_Duplicate', async () => {
      try {
        await logsClient.send(
          new CreateLogGroupCommand({ logGroupName: dupGroupName })
        );
      } catch {
        // ignore first create error
      }

      try {
        await logsClient.send(
          new CreateLogGroupCommand({ logGroupName: dupGroupName })
        );
        throw new Error('Expected ResourceAlreadyExistsException but got none');
      } catch (err) {
        if (!(err instanceof ResourceAlreadyExistsException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceAlreadyExistsException, got ${name}`);
        }
      } finally {
        try {
          await logsClient.send(
            new DeleteLogGroupCommand({ logGroupName: dupGroupName })
          );
        } catch { /* ignore */ }
      }
    })
  );

  // DeleteLogGroup_NonExistent
  results.push(
    await runner.runTest('logs', 'DeleteLogGroup_NonExistent', async () => {
      try {
        await logsClient.send(
          new DeleteLogGroupCommand({ logGroupName: 'nonexistent-log-group-xyz' })
        );
        throw new Error('Expected error but got none');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  // PutLogEvents_GetLogEvents_Roundtrip
  const rtGroupName = makeUniqueName('RTLogGroup');
  const rtStreamName = makeUniqueName('RTLogStream');
  results.push(
    await runner.runTest('logs', 'PutLogEvents_GetLogEvents_Roundtrip', async () => {
      try {
        await logsClient.send(
          new CreateLogGroupCommand({ logGroupName: rtGroupName })
        );

        await logsClient.send(
          new CreateLogStreamCommand({
            logGroupName: rtGroupName,
            logStreamName: rtStreamName,
          })
        );

        const testMessage = 'roundtrip-log-message-verify-12345';
        const ts = Date.now();
        await logsClient.send(
          new PutLogEventsCommand({
            logGroupName: rtGroupName,
            logStreamName: rtStreamName,
            logEvents: [{ message: testMessage, timestamp: ts }],
          })
        );

        const resp = await logsClient.send(
          new GetLogEventsCommand({
            logGroupName: rtGroupName,
            logStreamName: rtStreamName,
          })
        );

        if (!resp.events || resp.events.length === 0) {
          throw new Error('no events returned');
        }
        if (resp.events[0].message !== testMessage) {
          throw new Error(`message mismatch: got ${resp.events[0].message}, want ${testMessage}`);
        }
      } finally {
        try {
          await logsClient.send(
            new DeleteLogGroupCommand({ logGroupName: rtGroupName })
          );
        } catch { /* ignore */ }
      }
    })
  );

  // DescribeLogGroups_ContainsCreated
  const dlgName = makeUniqueName('DLGGroup');
  results.push(
    await runner.runTest('logs', 'DescribeLogGroups_ContainsCreated', async () => {
      try {
        await logsClient.send(
          new CreateLogGroupCommand({ logGroupName: dlgName })
        );

        const resp = await logsClient.send(
          new DescribeLogGroupsCommand({ logGroupNamePrefix: dlgName })
        );

        if (resp.logGroups?.length !== 1) {
          throw new Error(`expected 1 log group, got ${resp.logGroups?.length}`);
        }
        if (resp.logGroups[0].logGroupName !== dlgName) {
          throw new Error('log group name mismatch');
        }
      } finally {
        try {
          await logsClient.send(
            new DeleteLogGroupCommand({ logGroupName: dlgName })
          );
        } catch { /* ignore */ }
      }
    })
  );

  return results;
}