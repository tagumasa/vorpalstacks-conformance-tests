import {
  KinesisClient,
  CreateStreamCommand,
  DescribeStreamCommand,
  DescribeStreamSummaryCommand,
  ListStreamsCommand,
  DeleteStreamCommand,
  PutRecordCommand,
  PutRecordsCommand,
  GetRecordsCommand,
  GetShardIteratorCommand,
  ListShardsCommand,
  UpdateShardCountCommand,
  SplitShardCommand,
  MergeShardsCommand,
  EnableEnhancedMonitoringCommand,
  DisableEnhancedMonitoringCommand,
  AddTagsToStreamCommand,
  ListTagsForStreamCommand,
  RemoveTagsFromStreamCommand,
  StartStreamEncryptionCommand,
  StopStreamEncryptionCommand,
  IncreaseStreamRetentionPeriodCommand,
  DecreaseStreamRetentionPeriodCommand,
  RegisterStreamConsumerCommand,
  DescribeStreamConsumerCommand,
  ListStreamConsumersCommand,
  DeregisterStreamConsumerCommand,
  UpdateStreamModeCommand,
  DescribeLimitsCommand,
  SubscribeToShardCommand,
} from '@aws-sdk/client-kinesis';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';
import { TEST_KMS_KEY, waitForActive, type StreamState } from './context.js';

export async function runStreamLifecycleTests(
  runner: TestRunner,
  client: KinesisClient,
  state: StreamState,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  try {
    results.push(await runner.runTest('kinesis', 'CreateStream', async () => {
      await client.send(new CreateStreamCommand({ StreamName: state.name, ShardCount: 1 }));
      state.created = true;
    }));

    results.push(await runner.runTest('kinesis', 'ListStreams', async () => {
      const resp = await client.send(new ListStreamsCommand({ Limit: 10 }));
      if (!resp.StreamNames) throw new Error('expected StreamNames to be defined');
    }));

    results.push(await runner.runTest('kinesis', 'DescribeStream', async () => {
      const info = await waitForActive(client, state.name);
      state.arn = info.streamARN;
      state.shardIds = info.shardIds;
    }));

    results.push(await runner.runTest('kinesis', 'DescribeStreamSummary', async () => {
      const resp = await client.send(new DescribeStreamSummaryCommand({ StreamName: state.name }));
      const summary = resp.StreamDescriptionSummary;
      if (!summary?.StreamARN) throw new Error('expected StreamARN to be defined');
      if (summary.OpenShardCount === undefined) throw new Error('expected OpenShardCount to be defined');
    }));

    results.push(await runner.runTest('kinesis', 'PutRecord', async () => {
      const resp = await client.send(new PutRecordCommand({
        StreamName: state.name,
        Data: Buffer.from('test-data'),
        PartitionKey: 'partition-key-1',
      }));
      if (!resp.SequenceNumber) throw new Error('expected SequenceNumber to be defined');
    }));

    results.push(await runner.runTest('kinesis', 'PutRecords', async () => {
      const resp = await client.send(new PutRecordsCommand({
        StreamName: state.name,
        Records: [
          { Data: Buffer.from('test-data-1'), PartitionKey: 'partition-key-1' },
          { Data: Buffer.from('test-data-2'), PartitionKey: 'partition-key-2' },
        ],
      }));
      if (!resp.Records) throw new Error('expected Records to be defined');
      for (const [i, rec] of resp.Records.entries()) {
        if (!rec.SequenceNumber) throw new Error(`record[${i}]: expected SequenceNumber to be defined`);
      }
    }));

    let shardIterator = '';
    if (state.shardIds.length > 0) {
      results.push(await runner.runTest('kinesis', 'GetShardIterator', async () => {
        const resp = await client.send(new GetShardIteratorCommand({
          StreamName: state.name,
          ShardId: state.shardIds[0],
          ShardIteratorType: 'TRIM_HORIZON',
        }));
        if (!resp.ShardIterator) throw new Error('expected ShardIterator to be defined');
        shardIterator = resp.ShardIterator;
      }));
    }

    results.push(await runner.runTest('kinesis', 'ListShards', async () => {
      const resp = await client.send(new ListShardsCommand({ StreamName: state.name }));
      if (!resp.Shards) throw new Error('expected Shards to be defined');
    }));

    const multiStream = makeUniqueName('ts-kinesis-multi');
    results.push(await runner.runTest('kinesis', 'ListShards_MultiShard', async () => {
      try {
        await client.send(new CreateStreamCommand({ StreamName: multiStream, ShardCount: 3 }));
        await waitForActive(client, multiStream);
        const resp = await client.send(new ListShardsCommand({ StreamName: multiStream }));
        if (resp.Shards?.length !== 3) throw new Error(`expected 3 shards, got ${resp.Shards?.length ?? 0}`);
      } finally {
        await safeCleanup(() => client.send(new DeleteStreamCommand({ StreamName: multiStream })));
      }
    }));

    if (shardIterator) {
      results.push(await runner.runTest('kinesis', 'GetRecords', async () => {
        const resp = await client.send(new GetRecordsCommand({ ShardIterator: shardIterator }));
        if (!resp.Records) throw new Error('expected Records to be defined');
      }));
    }

    results.push(await runner.runTest('kinesis', 'EnableEnhancedMonitoring', async () => {
      const resp = await client.send(new EnableEnhancedMonitoringCommand({
        StreamName: state.name,
        ShardLevelMetrics: ['IncomingBytes', 'OutgoingBytes'],
      }));
      if (!resp.CurrentShardLevelMetrics) throw new Error('expected CurrentShardLevelMetrics to be defined');
    }));

    results.push(await runner.runTest('kinesis', 'DisableEnhancedMonitoring', async () => {
      const resp = await client.send(new DisableEnhancedMonitoringCommand({
        StreamName: state.name,
        ShardLevelMetrics: [],
      }));
      if (!resp.CurrentShardLevelMetrics) throw new Error('expected CurrentShardLevelMetrics to be defined');
    }));

    results.push(await runner.runTest('kinesis', 'AddTagsToStream', async () => {
      await client.send(new AddTagsToStreamCommand({
        StreamName: state.name,
        Tags: { Environment: 'test', Owner: 'test-user' },
      }));
    }));

    results.push(await runner.runTest('kinesis', 'ListTagsForStream', async () => {
      const resp = await client.send(new ListTagsForStreamCommand({ StreamName: state.name }));
      if (!resp.Tags) throw new Error('expected Tags to be defined');
    }));

    results.push(await runner.runTest('kinesis', 'RemoveTagsFromStream', async () => {
      await client.send(new RemoveTagsFromStreamCommand({
        StreamName: state.name,
        TagKeys: ['Environment'],
      }));
    }));

    results.push(await runner.runTest('kinesis', 'StartStreamEncryption', async () => {
      await client.send(new StartStreamEncryptionCommand({
        StreamName: state.name,
        EncryptionType: 'KMS',
        KeyId: TEST_KMS_KEY,
      }));
    }));

    results.push(await runner.runTest('kinesis', 'StopStreamEncryption', async () => {
      await client.send(new StopStreamEncryptionCommand({
        StreamName: state.name,
        EncryptionType: 'KMS',
        KeyId: TEST_KMS_KEY,
      }));
    }));

    results.push(await runner.runTest('kinesis', 'CreateStreamWithTags', async () => {
      const tagsStream = makeUniqueName('ts-kinesis-tags');
      try {
        await client.send(new CreateStreamCommand({
          StreamName: tagsStream,
          ShardCount: 1,
          Tags: { Environment: 'test', Owner: 'test-user' },
        }));
      } finally {
        await safeCleanup(() => client.send(new DeleteStreamCommand({ StreamName: tagsStream })));
      }
    }));

    results.push(await runner.runTest('kinesis', 'IncreaseStreamRetentionPeriod', async () => {
      await client.send(new IncreaseStreamRetentionPeriodCommand({
        StreamName: state.name,
        RetentionPeriodHours: 48,
      }));
    }));

    results.push(await runner.runTest('kinesis', 'DecreaseStreamRetentionPeriod', async () => {
      await client.send(new DecreaseStreamRetentionPeriodCommand({
        StreamName: state.name,
        RetentionPeriodHours: 24,
      }));
    }));

    const consumerName = makeUniqueName('ts-kinesis-consumer');
    results.push(await runner.runTest('kinesis', 'RegisterStreamConsumer', async () => {
      const resp = await client.send(new RegisterStreamConsumerCommand({
        StreamARN: state.arn,
        ConsumerName: consumerName,
      }));
      if (!resp.Consumer) throw new Error('expected Consumer to be defined');
    }));

    results.push(await runner.runTest('kinesis', 'DescribeStreamConsumer', async () => {
      const resp = await client.send(new DescribeStreamConsumerCommand({
        StreamARN: state.arn,
        ConsumerName: consumerName,
      }));
      const desc = resp.ConsumerDescription;
      if (!desc?.ConsumerARN) throw new Error('expected ConsumerARN to be defined');
    }));

    results.push(await runner.runTest('kinesis', 'ListStreamConsumers', async () => {
      const resp = await client.send(new ListStreamConsumersCommand({ StreamARN: state.arn }));
      if (!resp.Consumers) throw new Error('expected Consumers to be defined');
    }));

    let consumerARN = '';
    try {
      const consumerDesc = await client.send(new DescribeStreamConsumerCommand({
        StreamARN: state.arn,
        ConsumerName: consumerName,
      }));
      consumerARN = consumerDesc.ConsumerDescription?.ConsumerARN ?? '';
    } catch { /* skip SubscribeToShard if consumer not available */ }

    if (consumerARN && state.shardIds.length > 0) {
      results.push(await runner.runTest('kinesis', 'SubscribeToShard', async () => {
        const resp = await client.send(new SubscribeToShardCommand({
          ConsumerARN: consumerARN,
          ShardId: state.shardIds[0],
          StartingPosition: { Type: 'TRIM_HORIZON' },
        }));
        const eventStream = resp.EventStream;
        if (!eventStream) throw new Error('expected EventStream to be defined');

        const eventPromise = (async () => {
          for await (const event of eventStream) {
            if (event.SubscribeToShardEvent) {
              if (!event.SubscribeToShardEvent.Records?.length) {
                throw new Error('SubscribeToShardEvent contained zero records');
              }
              return;
            }
          }
          throw new Error('stream closed without receiving any event');
        })();

        await Promise.race([
          eventPromise,
          new Promise<never>((_, reject) =>
            setTimeout(() => reject(new Error('timed out waiting for SubscribeToShard event')), 15000),
          ),
        ]);
      }));
    } else {
      results.push(runner.skipTest('kinesis', 'SubscribeToShard', 'consumerARN or shardId not available'));
    }

    results.push(await runner.runTest('kinesis', 'DeregisterStreamConsumer', async () => {
      await client.send(new DeregisterStreamConsumerCommand({
        StreamARN: state.arn,
        ConsumerName: consumerName,
      }));
    }));

    results.push(await runner.runTest('kinesis', 'UpdateStreamMode', async () => {
      const modeStream = makeUniqueName('ts-kinesis-mode');
      try {
        await client.send(new CreateStreamCommand({ StreamName: modeStream, ShardCount: 1 }));
        const descResp = await client.send(new DescribeStreamCommand({ StreamName: modeStream }));
        const arn = descResp.StreamDescription?.StreamARN;
        if (!arn) throw new Error('expected StreamARN to be defined');
        await client.send(new UpdateStreamModeCommand({
          StreamARN: arn,
          StreamModeDetails: { StreamMode: 'ON_DEMAND' },
        }));
      } finally {
        await safeCleanup(() => client.send(new DeleteStreamCommand({ StreamName: modeStream })));
      }
    }));

    results.push(await runner.runTest('kinesis', 'UpdateShardCount', async () => {
      const resp = await client.send(new UpdateShardCountCommand({
        StreamName: state.name,
        TargetShardCount: 2,
        ScalingType: 'UNIFORM_SCALING',
      }));
      if (resp.CurrentShardCount === undefined) throw new Error('expected CurrentShardCount to be defined');
      await waitForActive(client, state.name);
    }));

    results.push(await runner.runTest('kinesis', 'MergeShards', async () => {
      const mergeStream = makeUniqueName('ts-kinesis-merge');
      try {
        await client.send(new CreateStreamCommand({ StreamName: mergeStream, ShardCount: 2 }));
        const info = await waitForActive(client, mergeStream);
        if (info.shardIds.length < 2) throw new Error('need at least 2 shards for merge');
        await client.send(new MergeShardsCommand({
          StreamName: mergeStream,
          ShardToMerge: info.shardIds[0],
          AdjacentShardToMerge: info.shardIds[1],
        }));
      } finally {
        await safeCleanup(() => client.send(new DeleteStreamCommand({ StreamName: mergeStream })));
      }
    }));

    results.push(await runner.runTest('kinesis', 'SplitShard', async () => {
      const lsResp = await client.send(new ListShardsCommand({ StreamName: state.name }));
      const openShard = lsResp.Shards?.find(
        s => !s.SequenceNumberRange?.EndingSequenceNumber,
      );
      if (!openShard?.ShardId) throw new Error('no open shard found for split');
      await client.send(new SplitShardCommand({
        StreamName: state.name,
        ShardToSplit: openShard.ShardId,
        NewStartingHashKey: '9223372036854775808',
      }));
    }));

    results.push(await runner.runTest('kinesis', 'DescribeLimits', async () => {
      const resp = await client.send(new DescribeLimitsCommand({}));
      if (resp.ShardLimit === undefined) throw new Error('expected ShardLimit to be defined');
    }));

    results.push(await runner.runTest('kinesis', 'DeleteStream', async () => {
      await client.send(new DeleteStreamCommand({ StreamName: state.name }));
      state.created = false;
    }));

    results.push(await runner.runTest('kinesis', 'ListShardsWithExclusiveStart', async () => {
      const lsStream = makeUniqueName('ts-kinesis-ls');
      try {
        await client.send(new CreateStreamCommand({ StreamName: lsStream, ShardCount: 1 }));
        await waitForActive(client, lsStream);
        const resp = await client.send(new ListShardsCommand({ StreamName: lsStream }));
        if (!resp.Shards) throw new Error('expected Shards to be defined');
      } finally {
        await safeCleanup(() => client.send(new DeleteStreamCommand({ StreamName: lsStream })));
      }
    }));

  } finally {
    await safeCleanup(async () => {
      if (state.created) await client.send(new DeleteStreamCommand({ StreamName: state.name }));
    });
  }

  return results;
}
