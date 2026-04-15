import {
  EventBridgeClient,
  CreateEventBusCommand,
  DeleteEventBusCommand,
  CreateArchiveCommand,
  DeleteArchiveCommand,
  DescribeArchiveCommand,
  ListArchivesCommand,
  UpdateArchiveCommand,
} from '@aws-sdk/client-eventbridge';
import type { TestRunner, TestResult } from '../../runner.js';
import type { ServiceContext } from '../../runner.js';
import { makeUniqueName, safeCleanup, assertThrows } from '../../helpers.js';

export async function runArchiveTests(
  runner: TestRunner,
  client: EventBridgeClient,
  ctx: ServiceContext,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const region = ctx.region;

  results.push(await runner.runTest('eventbridge', 'CreateArchive', async () => {
    const caBus = makeUniqueName('CaBus');
    const caArchive = makeUniqueName('CaArchive');
    try {
      await client.send(new CreateEventBusCommand({ Name: caBus }));
      const busARN = `arn:aws:events:${region}:000000000000:event-bus/${caBus}`;
      const resp = await client.send(new CreateArchiveCommand({
        ArchiveName: caArchive, EventSourceArn: busARN, Description: 'test archive',
      }));
      if (!resp.ArchiveArn) throw new Error('expected ArchiveArn to be defined');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteArchiveCommand({ ArchiveName: caArchive })); });
      await safeCleanup(async () => { await client.send(new DeleteEventBusCommand({ Name: caBus })); });
    }
  }));

  results.push(await runner.runTest('eventbridge', 'DescribeArchive', async () => {
    const daBus = makeUniqueName('DaBus');
    const daArchive = makeUniqueName('DaArchive');
    try {
      await client.send(new CreateEventBusCommand({ Name: daBus }));
      const busARN = `arn:aws:events:${region}:000000000000:event-bus/${daBus}`;
      await client.send(new CreateArchiveCommand({
        ArchiveName: daArchive, EventSourceArn: busARN, Description: 'test archive for describe',
      }));
      const resp = await client.send(new DescribeArchiveCommand({ ArchiveName: daArchive }));
      if (resp.ArchiveName !== daArchive) throw new Error(`archive name mismatch, got ${resp.ArchiveName}`);
      if (resp.EventSourceArn !== busARN) throw new Error(`event source ARN mismatch, got ${resp.EventSourceArn}`);
      if (resp.Description !== 'test archive for describe') throw new Error(`description mismatch, got ${resp.Description}`);
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteArchiveCommand({ ArchiveName: daArchive })); });
      await safeCleanup(async () => { await client.send(new DeleteEventBusCommand({ Name: daBus })); });
    }
  }));

  results.push(await runner.runTest('eventbridge', 'DescribeArchive_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new DescribeArchiveCommand({ ArchiveName: 'nonexistent-archive-xyz-12345' }));
    }, 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest('eventbridge', 'DeleteArchive_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new DeleteArchiveCommand({ ArchiveName: 'nonexistent-archive-xyz-12345' }));
    }, 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest('eventbridge', 'ListArchives', async () => {
    const resp = await client.send(new ListArchivesCommand({}));
    if (!resp.Archives) throw new Error('expected Archives to be defined');
  }));

  results.push(await runner.runTest('eventbridge', 'UpdateArchive', async () => {
    const uaBus = makeUniqueName('UaBus');
    const uaArchive = makeUniqueName('UaArchive');
    try {
      await client.send(new CreateEventBusCommand({ Name: uaBus }));
      const busARN = `arn:aws:events:${region}:000000000000:event-bus/${uaBus}`;
      await client.send(new CreateArchiveCommand({
        ArchiveName: uaArchive, EventSourceArn: busARN, Description: 'original description',
      }));
      const resp = await client.send(new UpdateArchiveCommand({ ArchiveName: uaArchive, Description: 'updated description' }));
      if (!resp.ArchiveArn) throw new Error('expected ArchiveArn to be defined');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteArchiveCommand({ ArchiveName: uaArchive })); });
      await safeCleanup(async () => { await client.send(new DeleteEventBusCommand({ Name: uaBus })); });
    }
  }));

  return results;
}
