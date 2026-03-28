import {
  KinesisClient,
  CreateStreamCommand,
  DescribeStreamCommand,
  ListStreamsCommand,
  DeleteStreamCommand,
  PutRecordCommand,
  PutRecordsCommand,
  GetRecordsCommand,
  GetShardIteratorCommand,
  ListShardsCommand,
  DescribeStreamSummaryCommand,
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
} from '@aws-sdk/client-kinesis';
import { ResourceNotFoundException } from '@aws-sdk/client-kinesis';
import { TestRunner } from '../runner.js';
import { TestResult } from '../runner.js';

function makeUniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 999999)}`;
}

export async function runKinesisTests(
  runner: TestRunner,
  kinesisClient: KinesisClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const streamName = makeUniqueName('TSStream');
  let streamCreated = false;
  let streamARN = '';
  let shardID = '';

  try {
    // CreateStream
    results.push(
      await runner.runTest('kinesis', 'CreateStream', async () => {
        const resp = await kinesisClient.send(
          new CreateStreamCommand({
            StreamName: streamName,
            ShardCount: 1,
          })
        );
        if (!resp) throw new Error('response is nil');
        streamCreated = true;
      })
    );

    // DescribeStreamSummary
    results.push(
      await runner.runTest('kinesis', 'DescribeStreamSummary', async () => {
        const resp = await kinesisClient.send(
          new DescribeStreamSummaryCommand({ StreamName: streamName })
        );
        if (!resp.StreamDescriptionSummary) {
          throw new Error('StreamDescriptionSummary is null');
        }
      })
    );

    // DescribeStream (wait for active)
    results.push(
      await runner.runTest('kinesis', 'DescribeStream', async () => {
        let attempts = 0;
        while (attempts < 30) {
          const resp = await kinesisClient.send(
            new DescribeStreamCommand({ StreamName: streamName })
          );
          if (resp.StreamDescription?.StreamARN) {
            streamARN = resp.StreamDescription.StreamARN;
          }
          if (resp.StreamDescription?.StreamStatus === 'ACTIVE') {
            if (resp.StreamDescription.Shards && resp.StreamDescription.Shards.length > 0) {
              shardID = resp.StreamDescription.Shards[0].ShardId || '';
            }
            return;
          }
          await new Promise((resolve) => setTimeout(resolve, 1000));
          attempts++;
        }
        throw new Error('Stream did not become active within timeout');
      })
    );

    // ListStreams
    results.push(
      await runner.runTest('kinesis', 'ListStreams', async () => {
        const resp = await kinesisClient.send(new ListStreamsCommand({}));
        if (!resp.StreamNames) throw new Error('StreamNames is null');
        const found = resp.StreamNames.includes(streamName);
        if (!found) throw new Error('Created stream not found in list');
      })
    );

    // ListShards
    results.push(
      await runner.runTest('kinesis', 'ListShards', async () => {
        const resp = await kinesisClient.send(
          new ListShardsCommand({ StreamName: streamName })
        );
        if (!resp.Shards) throw new Error('Shards is null');
      })
    );

    // PutRecord
    let sequenceNumber = '';
    results.push(
      await runner.runTest('kinesis', 'PutRecord', async () => {
        const resp = await kinesisClient.send(
          new PutRecordCommand({
            StreamName: streamName,
            Data: Buffer.from('test record data'),
            PartitionKey: 'partition-key-1',
          })
        );
        if (!resp.SequenceNumber) throw new Error('SequenceNumber is null');
        sequenceNumber = resp.SequenceNumber;
        if (!resp.ShardId) throw new Error('ShardId is null');
      })
    );

    // PutRecords (multiple)
    results.push(
      await runner.runTest('kinesis', 'PutRecords', async () => {
        const resp = await kinesisClient.send(
          new PutRecordsCommand({
            StreamName: streamName,
            Records: [
              { Data: Buffer.from('record 1'), PartitionKey: 'key-1' },
              { Data: Buffer.from('record 2'), PartitionKey: 'key-2' },
              { Data: Buffer.from('record 3'), PartitionKey: 'key-3' },
            ],
          })
        );
        if (!resp.FailedRecordCount && resp.FailedRecordCount !== 0) {
          throw new Error('FailedRecordCount is null');
        }
        if (!resp.Records) throw new Error('Records is null');
        for (let i = 0; i < resp.Records.length; i++) {
          if (!resp.Records[i].SequenceNumber) throw new Error(`record[${i}].SequenceNumber is nil`);
        }
      })
    );

    // GetShardIterator
    let shardIterator = '';
    results.push(
      await runner.runTest('kinesis', 'GetShardIterator', async () => {
        const resp = await kinesisClient.send(
          new GetShardIteratorCommand({
            StreamName: streamName,
            ShardId: shardID || 'shardId-000000000000',
            ShardIteratorType: 'TRIM_HORIZON',
          })
        );
        if (!resp.ShardIterator) throw new Error('ShardIterator is null');
        shardIterator = resp.ShardIterator;
      })
    );

    // GetRecords
    results.push(
      await runner.runTest('kinesis', 'GetRecords', async () => {
        const resp = await kinesisClient.send(
          new GetRecordsCommand({
            ShardIterator: shardIterator,
            Limit: 100,
          })
        );
        if (!resp.Records) throw new Error('Records is null');
      })
    );

    // EnableEnhancedMonitoring
    results.push(
      await runner.runTest('kinesis', 'EnableEnhancedMonitoring', async () => {
        const resp = await kinesisClient.send(
          new EnableEnhancedMonitoringCommand({
            StreamName: streamName,
            ShardLevelMetrics: ['IncomingBytes', 'OutgoingBytes'],
          })
        );
        if (!resp.CurrentShardLevelMetrics) throw new Error('CurrentShardLevelMetrics is nil');
      })
    );

    // DisableEnhancedMonitoring
    results.push(
      await runner.runTest('kinesis', 'DisableEnhancedMonitoring', async () => {
        const resp = await kinesisClient.send(
          new DisableEnhancedMonitoringCommand({
            StreamName: streamName,
            ShardLevelMetrics: [],
          })
        );
        if (!resp.CurrentShardLevelMetrics) throw new Error('CurrentShardLevelMetrics is nil');
      })
    );

    // AddTagsToStream
    results.push(
      await runner.runTest('kinesis', 'AddTagsToStream', async () => {
        await kinesisClient.send(
          new AddTagsToStreamCommand({
            StreamName: streamName,
            Tags: { Environment: 'test', Owner: 'test-user' },
          })
        );
      })
    );

    // ListTagsForStream
    results.push(
      await runner.runTest('kinesis', 'ListTagsForStream', async () => {
        const resp = await kinesisClient.send(
          new ListTagsForStreamCommand({ StreamName: streamName })
        );
        if (!resp.Tags) throw new Error('Tags is nil');
      })
    );

    // RemoveTagsFromStream
    results.push(
      await runner.runTest('kinesis', 'RemoveTagsFromStream', async () => {
        await kinesisClient.send(
          new RemoveTagsFromStreamCommand({
            StreamName: streamName,
            TagKeys: ['Environment'],
          })
        );
      })
    );

    // StartStreamEncryption
    results.push(
      await runner.runTest('kinesis', 'StartStreamEncryption', async () => {
        await kinesisClient.send(
          new StartStreamEncryptionCommand({
            StreamName: streamName,
            EncryptionType: 'KMS',
            KeyId: 'arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012',
          })
        );
      })
    );

    // StopStreamEncryption
    results.push(
      await runner.runTest('kinesis', 'StopStreamEncryption', async () => {
        await kinesisClient.send(
          new StopStreamEncryptionCommand({
            StreamName: streamName,
            EncryptionType: 'KMS',
            KeyId: 'arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012',
          })
        );
      })
    );

    // CreateStreamWithTags
    results.push(
      await runner.runTest('kinesis', 'CreateStreamWithTags', async () => {
        const tagsStream = makeUniqueName('TSTagsStream');
        try {
          await kinesisClient.send(
            new CreateStreamCommand({
              StreamName: tagsStream,
              ShardCount: 1,
              Tags: { Environment: 'test', Owner: 'test-user' },
            })
          );
        } finally {
          try { await kinesisClient.send(new DeleteStreamCommand({ StreamName: tagsStream })); } catch { /* ignore */ }
        }
      })
    );

    // IncreaseStreamRetentionPeriod
    results.push(
      await runner.runTest('kinesis', 'IncreaseStreamRetentionPeriod', async () => {
        await kinesisClient.send(
          new IncreaseStreamRetentionPeriodCommand({
            StreamName: streamName,
            RetentionPeriodHours: 48,
          })
        );
      })
    );

    // DecreaseStreamRetentionPeriod
    results.push(
      await runner.runTest('kinesis', 'DecreaseStreamRetentionPeriod', async () => {
        await kinesisClient.send(
          new DecreaseStreamRetentionPeriodCommand({
            StreamName: streamName,
            RetentionPeriodHours: 24,
          })
        );
      })
    );

    // RegisterStreamConsumer
    const consumerName = makeUniqueName('TSConsumer');
    results.push(
      await runner.runTest('kinesis', 'RegisterStreamConsumer', async () => {
        if (!streamARN) throw new Error('streamARN not available');
        const resp = await kinesisClient.send(
          new RegisterStreamConsumerCommand({
            StreamARN: streamARN,
            ConsumerName: consumerName,
          })
        );
        if (!resp.Consumer) throw new Error('Consumer is nil');
      })
    );

    // DescribeStreamConsumer
    results.push(
      await runner.runTest('kinesis', 'DescribeStreamConsumer', async () => {
        if (!streamARN) throw new Error('streamARN not available');
        const resp = await kinesisClient.send(
          new DescribeStreamConsumerCommand({
            StreamARN: streamARN,
            ConsumerName: consumerName,
          })
        );
        if (!resp.ConsumerDescription) throw new Error('ConsumerDescription is nil');
        if (!resp.ConsumerDescription.ConsumerARN) throw new Error('ConsumerDescription.ConsumerARN is nil');
      })
    );

    // ListStreamConsumers
    results.push(
      await runner.runTest('kinesis', 'ListStreamConsumers', async () => {
        if (!streamARN) throw new Error('streamARN not available');
        const resp = await kinesisClient.send(
          new ListStreamConsumersCommand({ StreamARN: streamARN })
        );
        if (!resp.Consumers) throw new Error('Consumers is nil');
      })
    );

    // DeregisterStreamConsumer
    results.push(
      await runner.runTest('kinesis', 'DeregisterStreamConsumer', async () => {
        if (!streamARN) throw new Error('streamARN not available');
        await kinesisClient.send(
          new DeregisterStreamConsumerCommand({
            StreamARN: streamARN,
            ConsumerName: consumerName,
          })
        );
      })
    );

    // UpdateStreamMode
    results.push(
      await runner.runTest('kinesis', 'UpdateStreamMode', async () => {
        const modeStream = makeUniqueName('TSModeStream');
        try {
          await kinesisClient.send(
            new CreateStreamCommand({
              StreamName: modeStream,
              ShardCount: 1,
            })
          );
          await new Promise((resolve) => setTimeout(resolve, 500));
          const descResp = await kinesisClient.send(
            new DescribeStreamCommand({ StreamName: modeStream })
          );
          if (descResp.StreamDescription?.StreamARN) {
            await kinesisClient.send(
              new UpdateStreamModeCommand({
                StreamARN: descResp.StreamDescription.StreamARN,
                StreamModeDetails: { StreamMode: 'ON_DEMAND' },
              })
            );
          }
        } finally {
          try { await kinesisClient.send(new DeleteStreamCommand({ StreamName: modeStream })); } catch { /* ignore */ }
        }
      })
    );

    // UpdateShardCount
    results.push(
      await runner.runTest('kinesis', 'UpdateShardCount', async () => {
        const resp = await kinesisClient.send(
          new UpdateShardCountCommand({
            StreamName: streamName,
            TargetShardCount: 2,
            ScalingType: 'UNIFORM_SCALING',
          })
        );
        if (!resp.CurrentShardCount) throw new Error('CurrentShardCount is nil');
      })
    );

    // MergeShards
    results.push(
      await runner.runTest('kinesis', 'MergeShards', async () => {
        const mergeStream = makeUniqueName('TSMergeStream');
        try {
          await kinesisClient.send(
            new CreateStreamCommand({
              StreamName: mergeStream,
              ShardCount: 2,
            })
          );
          await new Promise((resolve) => setTimeout(resolve, 500));
          const listResp = await kinesisClient.send(
            new ListShardsCommand({ StreamName: mergeStream })
          );
          const openShards = (listResp.Shards || []).filter(
            (s) => !s.SequenceNumberRange?.EndingSequenceNumber
          );
          if (openShards.length < 2) throw new Error(`need at least 2 open shards for merge, got ${openShards.length}`);
          await kinesisClient.send(
            new MergeShardsCommand({
              StreamName: mergeStream,
              ShardToMerge: openShards[0].ShardId,
              AdjacentShardToMerge: openShards[1].ShardId,
            })
          );
        } finally {
          try { await kinesisClient.send(new DeleteStreamCommand({ StreamName: mergeStream })); } catch { /* ignore */ }
        }
      })
    );

    // SplitShard
    results.push(
      await runner.runTest('kinesis', 'SplitShard', async () => {
        const listResp = await kinesisClient.send(
          new ListShardsCommand({ StreamName: streamName })
        );
        const openShard = (listResp.Shards || []).find(
          (s) => !s.SequenceNumberRange?.EndingSequenceNumber
        );
        if (!openShard) throw new Error('no open shard found for split');
        await kinesisClient.send(
          new SplitShardCommand({
            StreamName: streamName,
            ShardToSplit: openShard.ShardId,
            NewStartingHashKey: '9223372036854775808',
          })
        );
      })
    );

    // DescribeLimits
    results.push(
      await runner.runTest('kinesis', 'DescribeLimits', async () => {
        const resp = await kinesisClient.send(new DescribeLimitsCommand({}));
        if (resp.ShardLimit === undefined) throw new Error('ShardLimit is undefined');
      })
    );

    // ListShards_MultiShard
    results.push(
      await runner.runTest('kinesis', 'ListShards_MultiShard', async () => {
        const multiStream = makeUniqueName('TSMultiStream');
        try {
          await kinesisClient.send(
            new CreateStreamCommand({
              StreamName: multiStream,
              ShardCount: 3,
            })
          );
          await new Promise((resolve) => setTimeout(resolve, 1000));
          const resp = await kinesisClient.send(
            new ListShardsCommand({ StreamName: multiStream })
          );
          if ((resp.Shards || []).length !== 3) throw new Error(`expected 3 shards, got ${resp.Shards?.length}`);
        } finally {
          try { await kinesisClient.send(new DeleteStreamCommand({ StreamName: multiStream })); } catch { /* ignore */ }
        }
      })
    );

    // ListShardsWithExclusiveStart
    results.push(
      await runner.runTest('kinesis', 'ListShardsWithExclusiveStart', async () => {
        const listStream = makeUniqueName('TSListStream');
        try {
          await kinesisClient.send(
            new CreateStreamCommand({
              StreamName: listStream,
              ShardCount: 1,
            })
          );
          await new Promise((resolve) => setTimeout(resolve, 1000));
          const resp = await kinesisClient.send(
            new ListShardsCommand({ StreamName: listStream })
          );
          if (!resp.Shards) throw new Error('Shards is nil');
        } finally {
          try { await kinesisClient.send(new DeleteStreamCommand({ StreamName: listStream })); } catch { /* ignore */ }
        }
      })
    );

    // DeleteStream
    results.push(
      await runner.runTest('kinesis', 'DeleteStream', async () => {
        await kinesisClient.send(
          new DeleteStreamCommand({ StreamName: streamName })
        );
        streamCreated = false;
      })
    );

  } finally {
    try {
      if (streamCreated) {
        await kinesisClient.send(new DeleteStreamCommand({ StreamName: streamName }));
      }
    } catch { /* ignore */ }
  }

  // === ERROR / EDGE CASE TESTS ===

  // DescribeStream_NonExistent
  results.push(
    await runner.runTest('kinesis', 'DescribeStream_NonExistent', async () => {
      try {
        await kinesisClient.send(
          new DescribeStreamCommand({ StreamName: 'NonExistentStream_xyz_12345' })
        );
        throw new Error('expected error for non-existent stream');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException) && !(err instanceof Error && err.name === 'ResourceNotFoundException')) {
          throw err;
        }
      }
    })
  );

  // DeleteStream_NonExistent
  results.push(
    await runner.runTest('kinesis', 'DeleteStream_NonExistent', async () => {
      try {
        await kinesisClient.send(
          new DeleteStreamCommand({ StreamName: 'NonExistentStream_xyz_12345' })
        );
        throw new Error('expected error for non-existent stream');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException) && !(err instanceof Error && err.name === 'ResourceNotFoundException')) {
          throw err;
        }
      }
    })
  );

  // CreateStream_DuplicateName
  results.push(
    await runner.runTest('kinesis', 'CreateStream_DuplicateName', async () => {
      const dupStream = makeUniqueName('TSDupStream');
      try {
        await kinesisClient.send(
          new CreateStreamCommand({ StreamName: dupStream, ShardCount: 1 })
        );
        try {
          await kinesisClient.send(
            new CreateStreamCommand({ StreamName: dupStream, ShardCount: 1 })
          );
          throw new Error('expected error for duplicate stream name');
        } catch (err) {
          if (err instanceof Error && err.message.includes('expected error')) throw err;
        }
      } finally {
        try { await kinesisClient.send(new DeleteStreamCommand({ StreamName: dupStream })); } catch { /* ignore */ }
      }
    })
  );

  // PutRecord_NonExistent
  results.push(
    await runner.runTest('kinesis', 'PutRecord_NonExistent', async () => {
      try {
        await kinesisClient.send(
          new PutRecordCommand({
            StreamName: 'NonExistentStream_xyz_12345',
            Data: Buffer.from('test'),
            PartitionKey: 'key',
          })
        );
        throw new Error('Expected error but got none');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException) && !(err instanceof Error && err.name === 'ResourceNotFoundException')) {
          throw err;
        }
      }
    })
  );

  // PutRecord_GetRecords_Roundtrip
  results.push(
    await runner.runTest('kinesis', 'PutRecord_GetRecords_Roundtrip', async () => {
      const rtStream = makeUniqueName('TSRTStream');
      try {
        await kinesisClient.send(
          new CreateStreamCommand({ StreamName: rtStream, ShardCount: 1 })
        );
        await new Promise((resolve) => setTimeout(resolve, 1000));
        const testData = Buffer.from('roundtrip-kinesis-data-verify');
        const putResp = await kinesisClient.send(
          new PutRecordCommand({
            StreamName: rtStream,
            Data: testData,
            PartitionKey: 'partition-1',
          })
        );
        if (!putResp.SequenceNumber) throw new Error('sequence number is nil');

        const descResp = await kinesisClient.send(
          new DescribeStreamCommand({ StreamName: rtStream })
        );
        if (!descResp.StreamDescription?.Shards?.length) throw new Error('no shards');

        const iterResp = await kinesisClient.send(
          new GetShardIteratorCommand({
            StreamName: rtStream,
            ShardId: descResp.StreamDescription.Shards[0].ShardId,
            ShardIteratorType: 'TRIM_HORIZON',
          })
        );
        if (!iterResp.ShardIterator) throw new Error('shard iterator is nil');

        const getResp = await kinesisClient.send(
          new GetRecordsCommand({ ShardIterator: iterResp.ShardIterator })
        );
        if (!getResp.Records || getResp.Records.length === 0) throw new Error('no records returned');
        const record = getResp.Records[0];
        if (!record || !record.Data || !Buffer.from(record.Data).equals(testData)) {
          throw new Error(`data mismatch: got ${record.Data ? Buffer.from(record.Data).toString() : 'undefined'}, want ${testData.toString()}`);
        }
      } finally {
        try { await kinesisClient.send(new DeleteStreamCommand({ StreamName: rtStream })); } catch { /* ignore */ }
      }
    })
  );

  // ListShards_NonExistentStream
  results.push(
    await runner.runTest('kinesis', 'ListShards_NonExistentStream', async () => {
      try {
        await kinesisClient.send(
          new ListShardsCommand({ StreamName: 'NonExistentStream_xyz_12345' })
        );
        throw new Error('expected error for non-existent stream');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException) && !(err instanceof Error && err.name === 'ResourceNotFoundException')) {
          throw err;
        }
      }
    })
  );

  return results;
}
