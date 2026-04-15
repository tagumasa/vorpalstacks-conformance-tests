import {
  EventBridgeClient,
  CreateEventBusCommand,
  DeleteEventBusCommand,
  CreateArchiveCommand,
  DeleteArchiveCommand,
  StartReplayCommand,
  DescribeReplayCommand,
  CancelReplayCommand,
  ListReplaysCommand,
} from '@aws-sdk/client-eventbridge';
import type { TestRunner, TestResult } from '../../runner.js';
import type { ServiceContext } from '../../runner.js';
import { makeUniqueName, safeCleanup, assertThrows } from '../../helpers.js';

export async function runReplayTests(
  runner: TestRunner,
  client: EventBridgeClient,
  ctx: ServiceContext,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const region = ctx.region;

  results.push(await runner.runTest('eventbridge', 'StartReplay_DescribeReplay', async () => {
    const srBus = makeUniqueName('SrBus');
    const srArchive = makeUniqueName('SrArchive');
    const srReplay = makeUniqueName('SrReplay');
    try {
      await client.send(new CreateEventBusCommand({ Name: srBus }));
      const busARN = `arn:aws:events:${region}:000000000000:event-bus/${srBus}`;
      const archiveARN = `arn:aws:events:${region}:000000000000:archive/${srArchive}`;
      await client.send(new CreateArchiveCommand({ ArchiveName: srArchive, EventSourceArn: busARN }));
      const startResp = await client.send(new StartReplayCommand({
        ReplayName: srReplay, EventSourceArn: archiveARN,
        Destination: { Arn: busARN },
        EventStartTime: new Date(Date.now() - 3600000), EventEndTime: new Date(),
      }));
      if (!startResp.ReplayArn) throw new Error('expected ReplayArn to be defined');
      const describeResp = await client.send(new DescribeReplayCommand({ ReplayName: srReplay }));
      if (describeResp.ReplayName !== srReplay) throw new Error(`replay name mismatch, got ${describeResp.ReplayName}`);
      if (describeResp.EventSourceArn !== archiveARN) throw new Error(`event source ARN mismatch, got ${describeResp.EventSourceArn}`);
    } finally {
      await safeCleanup(async () => { await client.send(new CancelReplayCommand({ ReplayName: srReplay })); });
      await safeCleanup(async () => { await client.send(new DeleteArchiveCommand({ ArchiveName: srArchive })); });
      await safeCleanup(async () => { await client.send(new DeleteEventBusCommand({ Name: srBus })); });
    }
  }));

  results.push(await runner.runTest('eventbridge', 'CancelReplay_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new CancelReplayCommand({ ReplayName: 'nonexistent-replay-xyz-12345' }));
    }, 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest('eventbridge', 'ListReplays', async () => {
    const resp = await client.send(new ListReplaysCommand({}));
    if (!resp.Replays) throw new Error('expected Replays to be defined');
  }));

  return results;
}
