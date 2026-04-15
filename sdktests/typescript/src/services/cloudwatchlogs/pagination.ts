import {
  CloudWatchLogsClient,
  CreateLogGroupCommand,
  DeleteLogGroupCommand,
  DescribeLogGroupsCommand,
  CreateLogStreamCommand,
  DescribeLogStreamsCommand,
  DeleteLogStreamCommand,
} from '@aws-sdk/client-cloudwatch-logs';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, assertErrorContains, safeCleanup } from '../../helpers.js';

export async function runPaginationTests(
  runner: TestRunner,
  client: CloudWatchLogsClient,
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('logs', 'DescribeLogGroups_Pagination', async () => {
    const pgPrefix = makeUniqueName('PagGroup') + '-';
    const groupNames = [pgPrefix + 'alpha', pgPrefix + 'beta', pgPrefix + 'gamma'];
    try {
      for (const name of groupNames) {
        await client.send(new CreateLogGroupCommand({ logGroupName: name }));
      }
      const allGroups: string[] = [];
      let nextToken: string | undefined;
      do {
        const resp = await client.send(new DescribeLogGroupsCommand({
          logGroupNamePrefix: pgPrefix,
          limit: 2,
          nextToken,
        }));
        for (const g of resp.logGroups ?? []) {
          if (g.logGroupName) allGroups.push(g.logGroupName);
        }
        nextToken = resp.nextToken;
      } while (nextToken);
      if (allGroups.length !== 3) throw new Error(`expected 3 groups across pages, got ${allGroups.length}`);
    } finally {
      for (const name of groupNames) {
        await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: name })));
      }
    }
  }));

  results.push(await runner.runTest('logs', 'DescribeLogStreams_Pagination', async () => {
    const psName = makeUniqueName('PagStreamGroup');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: psName }));
      const streamNames = [psName + '-s1', psName + '-s2', psName + '-s3'];
      for (const sn of streamNames) {
        await client.send(new CreateLogStreamCommand({ logGroupName: psName, logStreamName: sn }));
      }
      const allStreams: string[] = [];
      let nextToken: string | undefined;
      do {
        const resp = await client.send(new DescribeLogStreamsCommand({
          logGroupName: psName,
          limit: 2,
          nextToken,
        }));
        for (const ls of resp.logStreams ?? []) {
          if (ls.logStreamName) allStreams.push(ls.logStreamName);
        }
        nextToken = resp.nextToken;
      } while (nextToken);
      if (allStreams.length !== 3) throw new Error(`expected 3 streams across pages, got ${allStreams.length}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: psName })));
    }
  }));

  results.push(await runner.runTest('logs', 'DescribeLogStreams_NamePrefix', async () => {
    const npName = makeUniqueName('NpStreamGroup');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: npName }));
      await client.send(new CreateLogStreamCommand({ logGroupName: npName, logStreamName: 'app-server-1' }));
      await client.send(new CreateLogStreamCommand({ logGroupName: npName, logStreamName: 'app-server-2' }));
      await client.send(new CreateLogStreamCommand({ logGroupName: npName, logStreamName: 'db-server-1' }));
      const resp = await client.send(new DescribeLogStreamsCommand({
        logGroupName: npName,
        logStreamNamePrefix: 'app-',
      }));
      if (resp.logStreams?.length !== 2) throw new Error(`expected 2 streams with prefix 'app-', got ${resp.logStreams?.length}`);
      for (const ls of resp.logStreams) {
        if (!ls.logStreamName?.startsWith('app-')) throw new Error(`unexpected stream: ${ls.logStreamName}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: npName })));
    }
  }));

  results.push(await runner.runTest('logs', 'DeleteLogStream_NonExistent', async () => {
    const dlsName = makeUniqueName('DlsGroup');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: dlsName }));
      let err: unknown;
      try {
        await client.send(new DeleteLogStreamCommand({
          logGroupName: dlsName,
          logStreamName: 'nonexistent-stream-xyz',
        }));
      } catch (e) {
        err = e;
      }
      assertErrorContains(err, 'ResourceNotFoundException');
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: dlsName })));
    }
  }));
}
