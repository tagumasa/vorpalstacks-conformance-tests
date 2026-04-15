import {
  CloudWatchLogsClient,
  CreateLogGroupCommand,
  DeleteLogGroupCommand,
  CreateLogStreamCommand,
  PutLogEventsCommand,
  GetLogEventsCommand,
  FilterLogEventsCommand,
  DescribeLogGroupsCommand,
  PutMetricFilterCommand,
  DeleteMetricFilterCommand,
} from '@aws-sdk/client-cloudwatch-logs';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runExtendedTests(
  runner: TestRunner,
  client: CloudWatchLogsClient,
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('logs', 'PutLogEvents_MultipleEvents', async () => {
    const meName = makeUniqueName('MEGroup');
    const meStream = makeUniqueName('MEStream');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: meName }));
      await client.send(new CreateLogStreamCommand({ logGroupName: meName, logStreamName: meStream }));
      const ts = Date.now();
      await client.send(new PutLogEventsCommand({
        logGroupName: meName,
        logStreamName: meStream,
        logEvents: [
          { message: 'event-1', timestamp: ts },
          { message: 'event-2', timestamp: ts + 1 },
          { message: 'event-3', timestamp: ts + 2 },
        ],
      }));
      const resp = await client.send(new GetLogEventsCommand({ logGroupName: meName, logStreamName: meStream }));
      if (resp.events?.length !== 3) throw new Error(`expected 3 events, got ${resp.events?.length}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: meName })));
    }
  }));

  results.push(await runner.runTest('logs', 'GetLogEvents_StartFromHead', async () => {
    const sfhName = makeUniqueName('SFHGroup');
    const sfhStream = makeUniqueName('SFHStream');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: sfhName }));
      await client.send(new CreateLogStreamCommand({ logGroupName: sfhName, logStreamName: sfhStream }));
      const ts = Date.now();
      await client.send(new PutLogEventsCommand({
        logGroupName: sfhName,
        logStreamName: sfhStream,
        logEvents: [
          { message: 'first-event', timestamp: ts },
          { message: 'second-event', timestamp: ts + 1 },
        ],
      }));
      const resp = await client.send(new GetLogEventsCommand({
        logGroupName: sfhName,
        logStreamName: sfhStream,
        startFromHead: true,
        limit: 1,
      }));
      if (!resp.events?.length) throw new Error('no events returned');
      if (resp.events[0].message !== 'first-event') throw new Error(`expected first-event when StartFromHead=true, got ${resp.events[0].message}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: sfhName })));
    }
  }));

  results.push(await runner.runTest('logs', 'FilterLogEvents_WithFilterPattern', async () => {
    const fepName = makeUniqueName('FEPGroup');
    const fepStream = makeUniqueName('FEPStream');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: fepName }));
      await client.send(new CreateLogStreamCommand({ logGroupName: fepName, logStreamName: fepStream }));
      const ts = Date.now();
      await client.send(new PutLogEventsCommand({
        logGroupName: fepName,
        logStreamName: fepStream,
        logEvents: [
          { message: 'ERROR disk full', timestamp: ts },
          { message: 'INFO started', timestamp: ts + 1 },
          { message: 'ERROR network timeout', timestamp: ts + 2 },
        ],
      }));
      const resp = await client.send(new FilterLogEventsCommand({
        logGroupName: fepName,
        filterPattern: 'ERROR',
      }));
      if (resp.events?.length !== 2) throw new Error(`expected 2 ERROR events, got ${resp.events?.length}`);
      for (const ev of resp.events) {
        if (!ev.message?.includes('ERROR')) throw new Error(`non-ERROR event in results: ${ev.message}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: fepName })));
    }
  }));

  results.push(await runner.runTest('logs', 'FilterLogEvents_WithLogStreamNames', async () => {
    const flsName = makeUniqueName('FLSGroup');
    const flsStream1 = makeUniqueName('FLSStream1');
    const flsStream2 = makeUniqueName('FLSStream2');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: flsName }));
      await client.send(new CreateLogStreamCommand({ logGroupName: flsName, logStreamName: flsStream1 }));
      await client.send(new CreateLogStreamCommand({ logGroupName: flsName, logStreamName: flsStream2 }));
      const ts = Date.now();
      await client.send(new PutLogEventsCommand({
        logGroupName: flsName, logStreamName: flsStream1,
        logEvents: [{ message: 'from-stream-1', timestamp: ts }],
      }));
      await client.send(new PutLogEventsCommand({
        logGroupName: flsName, logStreamName: flsStream2,
        logEvents: [{ message: 'from-stream-2', timestamp: ts + 1 }],
      }));
      const resp = await client.send(new FilterLogEventsCommand({
        logGroupName: flsName,
        logStreamNames: [flsStream1],
      }));
      if (resp.events?.length !== 1) throw new Error(`expected 1 event from stream1, got ${resp.events?.length}`);
      if (resp.events[0].logStreamName !== flsStream1) throw new Error(`logStreamName mismatch: got ${resp.events[0].logStreamName}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: flsName })));
    }
  }));

  results.push(await runner.runTest('logs', 'MetricFilterCount_Tracked', async () => {
    const mfcName = makeUniqueName('MFCGroup');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: mfcName }));
      const descResp = await client.send(new DescribeLogGroupsCommand({ logGroupNamePrefix: mfcName }));
      if (!descResp.logGroups?.length) throw new Error('log group not found');
      const mfc = descResp.logGroups[0].metricFilterCount;
      if (mfc !== undefined && mfc !== 0) throw new Error(`expected 0 filters, got ${mfc}`);

      await client.send(new PutMetricFilterCommand({
        logGroupName: mfcName, filterName: 'CountFilter1', filterPattern: 'ERROR',
        metricTransformations: [{ metricName: 'E', metricNamespace: 'NS', metricValue: '1' }],
      }));

      const descResp2 = await client.send(new DescribeLogGroupsCommand({ logGroupNamePrefix: mfcName }));
      if (descResp2.logGroups?.[0]?.metricFilterCount !== 1) throw new Error(`expected 1 filter, got ${descResp2.logGroups?.[0]?.metricFilterCount}`);

      await client.send(new DeleteMetricFilterCommand({ logGroupName: mfcName, filterName: 'CountFilter1' }));

      const descResp3 = await client.send(new DescribeLogGroupsCommand({ logGroupNamePrefix: mfcName }));
      if (descResp3.logGroups?.[0]?.metricFilterCount !== 0) throw new Error(`expected 0 filters after delete, got ${descResp3.logGroups?.[0]?.metricFilterCount}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: mfcName })));
    }
  }));
}
