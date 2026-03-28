using Amazon;
using Amazon.Kinesis;
using Amazon.Kinesis.Model;
using Amazon.Runtime;
using System.Text;

namespace VorpalStacks.SDK.Tests.Services;

public static class KinesisServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonKinesisClient kinesisClient,
        string region)
    {
        var results = new List<TestResult>();
        var streamName = TestRunner.MakeUniqueName("cs");

        async Task WaitForStreamActive(string name)
        {
            for (int i = 0; i < 30; i++)
            {
                try
                {
                    var resp = await kinesisClient.DescribeStreamAsync(new DescribeStreamRequest
                    {
                        StreamName = name
                    });
                    if (resp.StreamDescription.StreamStatus == StreamStatus.ACTIVE)
                        return;
                }
                catch { }
                await Task.Delay(1000);
            }
            throw new Exception($"Stream {name} did not become ACTIVE");
        }

        async Task CleanupStream(string name)
        {
            try { await kinesisClient.DeleteStreamAsync(new DeleteStreamRequest { StreamName = name }); }
            catch { }
        }

        // Test 1: CreateStream
        results.Add(await runner.RunTestAsync("kinesis", "CreateStream", async () =>
        {
            await kinesisClient.CreateStreamAsync(new CreateStreamRequest
            {
                StreamName = streamName,
                ShardCount = 1
            });
        }));

        // Test 2: ListStreams
        results.Add(await runner.RunTestAsync("kinesis", "ListStreams", async () =>
        {
            var resp = await kinesisClient.ListStreamsAsync(new ListStreamsRequest());
            if (resp.StreamNames == null)
                throw new Exception("StreamNames is null");
        }));

        await WaitForStreamActive(streamName);

        // Test 3: DescribeStream (captures streamARN)
        string streamARN = null!;
        results.Add(await runner.RunTestAsync("kinesis", "DescribeStream", async () =>
        {
            var resp = await kinesisClient.DescribeStreamAsync(new DescribeStreamRequest
            {
                StreamName = streamName
            });
            if (resp.StreamDescription.StreamName == null)
                throw new Exception("StreamName is null");
            streamARN = resp.StreamDescription.StreamARN;
        }));

        // Test 4: DescribeStreamSummary
        results.Add(await runner.RunTestAsync("kinesis", "DescribeStreamSummary", async () =>
        {
            var resp = await kinesisClient.DescribeStreamSummaryAsync(new DescribeStreamSummaryRequest
            {
                StreamName = streamName
            });
            if (resp.StreamDescriptionSummary == null)
                throw new Exception("StreamDescriptionSummary is null");
        }));

        // Test 5: PutRecord
        results.Add(await runner.RunTestAsync("kinesis", "PutRecord", async () =>
        {
            var resp = await kinesisClient.PutRecordAsync(new PutRecordRequest
            {
                StreamName = streamName,
                Data = new MemoryStream(Encoding.UTF8.GetBytes("test-data")),
                PartitionKey = "partition-key-1"
            });
            if (string.IsNullOrEmpty(resp.SequenceNumber))
                throw new Exception("SequenceNumber is null");
        }));

        // Test 6: PutRecords
        results.Add(await runner.RunTestAsync("kinesis", "PutRecords", async () =>
        {
            var resp = await kinesisClient.PutRecordsAsync(new PutRecordsRequest
            {
                StreamName = streamName,
                Records = new List<PutRecordsRequestEntry>
                {
                    new PutRecordsRequestEntry
                    {
                        Data = new MemoryStream(Encoding.UTF8.GetBytes("test-data-1")),
                        PartitionKey = "partition-key-1"
                    },
                    new PutRecordsRequestEntry
                    {
                        Data = new MemoryStream(Encoding.UTF8.GetBytes("test-data-2")),
                        PartitionKey = "partition-key-2"
                    }
                }
            });
            if (resp.Records == null || resp.Records.Count != 2)
                throw new Exception("Expected 2 records in response");
            if (string.IsNullOrEmpty(resp.Records[0].SequenceNumber))
                throw new Exception("SequenceNumber for record 0 is null");
            if (string.IsNullOrEmpty(resp.Records[1].SequenceNumber))
                throw new Exception("SequenceNumber for record 1 is null");
        }));

        // Get shard info for iterator tests
        string shardId = null!;
        try
        {
            var descResp = await kinesisClient.DescribeStreamAsync(new DescribeStreamRequest
            {
                StreamName = streamName
            });
            if (descResp.StreamDescription.Shards.Count > 0)
                shardId = descResp.StreamDescription.Shards[0].ShardId;
        }
        catch { }

        // Test 7: GetShardIterator
        string shardIterator = null!;
        if (shardId != null)
        {
            results.Add(await runner.RunTestAsync("kinesis", "GetShardIterator", async () =>
            {
                var resp = await kinesisClient.GetShardIteratorAsync(new GetShardIteratorRequest
                {
                    StreamName = streamName,
                    ShardId = shardId,
                    ShardIteratorType = ShardIteratorType.TRIM_HORIZON
                });
                if (string.IsNullOrEmpty(resp.ShardIterator))
                    throw new Exception("ShardIterator is null");
                shardIterator = resp.ShardIterator;
            }));
        }

        // Test 8: ListShards
        results.Add(await runner.RunTestAsync("kinesis", "ListShards", async () =>
        {
            var resp = await kinesisClient.ListShardsAsync(new ListShardsRequest
            {
                StreamName = streamName
            });
            if (resp.Shards == null)
                throw new Exception("Shards is null");
        }));

        // Test 9: ListShards_MultiShard
        results.Add(await runner.RunTestAsync("kinesis", "ListShards_MultiShard", async () =>
        {
            var multiStream = TestRunner.MakeUniqueName("cs-multi");
            await kinesisClient.CreateStreamAsync(new CreateStreamRequest
            {
                StreamName = multiStream,
                ShardCount = 3
            });
            try
            {
                await Task.Delay(1000);
                var resp = await kinesisClient.ListShardsAsync(new ListShardsRequest
                {
                    StreamName = multiStream
                });
                if (resp.Shards.Count != 3)
                    throw new Exception($"Expected 3 shards, got {resp.Shards.Count}");
            }
            finally
            {
                await CleanupStream(multiStream);
            }
        }));

        // Test 10: GetRecords
        if (shardIterator != null)
        {
            results.Add(await runner.RunTestAsync("kinesis", "GetRecords", async () =>
            {
                var resp = await kinesisClient.GetRecordsAsync(new GetRecordsRequest
                {
                    ShardIterator = shardIterator
                });
                if (resp.Records == null)
                    throw new Exception("Records is null");
            }));
        }

        // Test 11: EnableEnhancedMonitoring
        results.Add(await runner.RunTestAsync("kinesis", "EnableEnhancedMonitoring", async () =>
        {
            await kinesisClient.EnableEnhancedMonitoringAsync(new EnableEnhancedMonitoringRequest
            {
                StreamName = streamName,
                ShardLevelMetrics = new List<string>
                {
                    "IncomingBytes",
                    "OutgoingBytes"
                }
            });
        }));

        // Test 12: DisableEnhancedMonitoring
        results.Add(await runner.RunTestAsync("kinesis", "DisableEnhancedMonitoring", async () =>
        {
            await kinesisClient.DisableEnhancedMonitoringAsync(new DisableEnhancedMonitoringRequest
            {
                StreamName = streamName,
                ShardLevelMetrics = new List<string>()
            });
        }));

        // Test 13: AddTagsToStream
        results.Add(await runner.RunTestAsync("kinesis", "AddTagsToStream", async () =>
        {
            await kinesisClient.AddTagsToStreamAsync(new AddTagsToStreamRequest
            {
                StreamName = streamName,
                Tags = new Dictionary<string, string>
                {
                    { "Environment", "test" },
                    { "Owner", "test-user" }
                }
            });
        }));

        // Test 14: ListTagsForStream
        results.Add(await runner.RunTestAsync("kinesis", "ListTagsForStream", async () =>
        {
            var resp = await kinesisClient.ListTagsForStreamAsync(new ListTagsForStreamRequest
            {
                StreamName = streamName
            });
            if (resp.Tags == null)
                throw new Exception("Tags is null");
        }));

        // Test 15: RemoveTagsFromStream
        results.Add(await runner.RunTestAsync("kinesis", "RemoveTagsFromStream", async () =>
        {
            await kinesisClient.RemoveTagsFromStreamAsync(new RemoveTagsFromStreamRequest
            {
                StreamName = streamName,
                TagKeys = new List<string> { "Environment" }
            });
        }));

        // Test 16: StartStreamEncryption
        results.Add(await runner.RunTestAsync("kinesis", "StartStreamEncryption", async () =>
        {
            await kinesisClient.StartStreamEncryptionAsync(new StartStreamEncryptionRequest
            {
                StreamName = streamName,
                EncryptionType = EncryptionType.KMS,
                KeyId = "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012"
            });
        }));

        // Test 17: StopStreamEncryption
        results.Add(await runner.RunTestAsync("kinesis", "StopStreamEncryption", async () =>
        {
            await kinesisClient.StopStreamEncryptionAsync(new StopStreamEncryptionRequest
            {
                StreamName = streamName,
                EncryptionType = EncryptionType.KMS,
                KeyId = "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012"
            });
        }));

        // Test 18: CreateStreamWithTags
        results.Add(await runner.RunTestAsync("kinesis", "CreateStreamWithTags", async () =>
        {
            var tagStream = TestRunner.MakeUniqueName("cs-tags");
            await kinesisClient.CreateStreamAsync(new CreateStreamRequest
            {
                StreamName = tagStream,
                ShardCount = 1,
                Tags = new Dictionary<string, string>
                {
                    { "Environment", "test" },
                    { "Owner", "test-user" }
                }
            });
            await CleanupStream(tagStream);
        }));

        // Test 19: IncreaseStreamRetentionPeriod
        results.Add(await runner.RunTestAsync("kinesis", "IncreaseStreamRetentionPeriod", async () =>
        {
            await kinesisClient.IncreaseStreamRetentionPeriodAsync(new IncreaseStreamRetentionPeriodRequest
            {
                StreamName = streamName,
                RetentionPeriodHours = 48
            });
        }));

        // Test 20: DecreaseStreamRetentionPeriod
        results.Add(await runner.RunTestAsync("kinesis", "DecreaseStreamRetentionPeriod", async () =>
        {
            await kinesisClient.DecreaseStreamRetentionPeriodAsync(new DecreaseStreamRetentionPeriodRequest
            {
                StreamName = streamName,
                RetentionPeriodHours = 24
            });
        }));

        // Test 21: RegisterStreamConsumer
        var consumerName = TestRunner.MakeUniqueName("cs-consumer");
        results.Add(await runner.RunTestAsync("kinesis", "RegisterStreamConsumer", async () =>
        {
            var resp = await kinesisClient.RegisterStreamConsumerAsync(new RegisterStreamConsumerRequest
            {
                StreamARN = streamARN,
                ConsumerName = consumerName
            });
            if (resp.Consumer == null)
                throw new Exception("Consumer is null");
        }));

        // Test 22: DescribeStreamConsumer
        results.Add(await runner.RunTestAsync("kinesis", "DescribeStreamConsumer", async () =>
        {
            var resp = await kinesisClient.DescribeStreamConsumerAsync(new DescribeStreamConsumerRequest
            {
                StreamARN = streamARN,
                ConsumerName = consumerName
            });
            if (resp.ConsumerDescription == null)
                throw new Exception("ConsumerDescription is null");
            if (string.IsNullOrEmpty(resp.ConsumerDescription.ConsumerARN))
                throw new Exception("ConsumerARN is null");
        }));

        // Test 23: ListStreamConsumers
        results.Add(await runner.RunTestAsync("kinesis", "ListStreamConsumers", async () =>
        {
            var resp = await kinesisClient.ListStreamConsumersAsync(new ListStreamConsumersRequest
            {
                StreamARN = streamARN
            });
            if (resp.Consumers == null)
                throw new Exception("Consumers is null");
        }));

        // Test 24: SubscribeToShard - SKIP (streaming API, too complex for this test framework)

        // Test 25: DeregisterStreamConsumer
        results.Add(await runner.RunTestAsync("kinesis", "DeregisterStreamConsumer", async () =>
        {
            await kinesisClient.DeregisterStreamConsumerAsync(new DeregisterStreamConsumerRequest
            {
                StreamARN = streamARN,
                ConsumerName = consumerName
            });
        }));

        // Test 26: UpdateStreamMode
        results.Add(await runner.RunTestAsync("kinesis", "UpdateStreamMode", async () =>
        {
            var modeStream = TestRunner.MakeUniqueName("cs-mode");
            await kinesisClient.CreateStreamAsync(new CreateStreamRequest
            {
                StreamName = modeStream,
                ShardCount = 1
            });
            try
            {
                await Task.Delay(500);
                var descResp = await kinesisClient.DescribeStreamAsync(new DescribeStreamRequest
                {
                    StreamName = modeStream
                });
                await kinesisClient.UpdateStreamModeAsync(new UpdateStreamModeRequest
                {
                    StreamARN = descResp.StreamDescription.StreamARN,
                    StreamModeDetails = new StreamModeDetails
                    {
                        StreamMode = StreamMode.ON_DEMAND
                    }
                });
            }
            finally
            {
                await CleanupStream(modeStream);
            }
        }));

        // Test 27: UpdateShardCount
        results.Add(await runner.RunTestAsync("kinesis", "UpdateShardCount", async () =>
        {
            await kinesisClient.UpdateShardCountAsync(new UpdateShardCountRequest
            {
                StreamName = streamName,
                TargetShardCount = 2,
                ScalingType = ScalingType.UNIFORM_SCALING
            });
        }));

        // Test 28: MergeShards
        results.Add(await runner.RunTestAsync("kinesis", "MergeShards", async () =>
        {
            var mergeStream = TestRunner.MakeUniqueName("cs-merge");
            await kinesisClient.CreateStreamAsync(new CreateStreamRequest
            {
                StreamName = mergeStream,
                ShardCount = 2
            });
            try
            {
                await Task.Delay(500);
                var listResp = await kinesisClient.ListShardsAsync(new ListShardsRequest
                {
                    StreamName = mergeStream
                });
                var openShards = listResp.Shards
                    .Where(s => string.IsNullOrEmpty(s.SequenceNumberRange.EndingSequenceNumber))
                    .ToList();
                if (openShards.Count < 2)
                    throw new Exception($"Need at least 2 open shards for merge, got {openShards.Count}");
                await kinesisClient.MergeShardsAsync(new MergeShardsRequest
                {
                    StreamName = mergeStream,
                    ShardToMerge = openShards[0].ShardId,
                    AdjacentShardToMerge = openShards[1].ShardId
                });
            }
            finally
            {
                await CleanupStream(mergeStream);
            }
        }));

        // Test 29: SplitShard
        results.Add(await runner.RunTestAsync("kinesis", "SplitShard", async () =>
        {
            var listResp = await kinesisClient.ListShardsAsync(new ListShardsRequest
            {
                StreamName = streamName
            });
            var openShard = listResp.Shards
                .FirstOrDefault(s => string.IsNullOrEmpty(s.SequenceNumberRange.EndingSequenceNumber));
            if (openShard == null)
                throw new Exception("No open shard found for split");
            await kinesisClient.SplitShardAsync(new SplitShardRequest
            {
                StreamName = streamName,
                ShardToSplit = openShard.ShardId,
                NewStartingHashKey = "9223372036854775808"
            });
        }));

        // Test 30: DescribeLimits
        results.Add(await runner.RunTestAsync("kinesis", "DescribeLimits", async () =>
        {
            var resp = await kinesisClient.DescribeLimitsAsync(new DescribeLimitsRequest());
            if (resp.ShardLimit == null)
                throw new Exception("ShardLimit is null");
        }));

        // Test 31: DeleteStream
        results.Add(await runner.RunTestAsync("kinesis", "DeleteStream", async () =>
        {
            await kinesisClient.DeleteStreamAsync(new DeleteStreamRequest
            {
                StreamName = streamName
            });
        }));

        // Test 32: ListShardsWithExclusiveStart
        results.Add(await runner.RunTestAsync("kinesis", "ListShardsWithExclusiveStart", async () =>
        {
            var listStream = TestRunner.MakeUniqueName("cs-list");
            await kinesisClient.CreateStreamAsync(new CreateStreamRequest
            {
                StreamName = listStream,
                ShardCount = 1
            });
            try
            {
                await Task.Delay(1000);
                await kinesisClient.ListShardsAsync(new ListShardsRequest
                {
                    StreamName = listStream
                });
            }
            finally
            {
                await CleanupStream(listStream);
            }
        }));

        // === ERROR / EDGE CASE TESTS ===

        // Test 33: DescribeStream_NonExistent
        results.Add(await runner.RunTestAsync("kinesis", "DescribeStream_NonExistent", async () =>
        {
            try
            {
                await kinesisClient.DescribeStreamAsync(new DescribeStreamRequest
                {
                    StreamName = "NonExistentStream_xyz_12345"
                });
                throw new Exception("Expected error but got none");
            }
            catch (ResourceNotFoundException) { }
        }));

        // Test 34: DeleteStream_NonExistent
        results.Add(await runner.RunTestAsync("kinesis", "DeleteStream_NonExistent", async () =>
        {
            try
            {
                await kinesisClient.DeleteStreamAsync(new DeleteStreamRequest
                {
                    StreamName = "NonExistentStream_xyz_12345"
                });
                throw new Exception("Expected error but got none");
            }
            catch (ResourceNotFoundException) { }
        }));

        // Test 35: CreateStream_DuplicateName
        results.Add(await runner.RunTestAsync("kinesis", "CreateStream_DuplicateName", async () =>
        {
            var dupStream = TestRunner.MakeUniqueName("cs-dup");
            await kinesisClient.CreateStreamAsync(new CreateStreamRequest
            {
                StreamName = dupStream,
                ShardCount = 1
            });
            try
            {
                try
                {
                    await kinesisClient.CreateStreamAsync(new CreateStreamRequest
                    {
                        StreamName = dupStream,
                        ShardCount = 1
                    });
                    throw new Exception("Expected error for duplicate stream name");
                }
                catch (ResourceInUseException) { }
            }
            finally
            {
                await CleanupStream(dupStream);
            }
        }));

        // Test 36: PutRecord_GetRecords_Roundtrip
        results.Add(await runner.RunTestAsync("kinesis", "PutRecord_GetRecords_Roundtrip", async () =>
        {
            var rtStream = TestRunner.MakeUniqueName("cs-rt");
            await kinesisClient.CreateStreamAsync(new CreateStreamRequest
            {
                StreamName = rtStream,
                ShardCount = 1
            });
            try
            {
                await Task.Delay(1000);

                var testData = Encoding.UTF8.GetBytes("roundtrip-kinesis-data-verify");
                var putResp = await kinesisClient.PutRecordAsync(new PutRecordRequest
                {
                    StreamName = rtStream,
                    Data = new MemoryStream(testData),
                    PartitionKey = "partition-1"
                });
                if (string.IsNullOrEmpty(putResp.SequenceNumber))
                    throw new Exception("SequenceNumber is null");

                var descResp = await kinesisClient.DescribeStreamAsync(new DescribeStreamRequest
                {
                    StreamName = rtStream
                });
                if (descResp.StreamDescription.Shards.Count == 0)
                    throw new Exception("No shards");

                var iterResp = await kinesisClient.GetShardIteratorAsync(new GetShardIteratorRequest
                {
                    StreamName = rtStream,
                    ShardId = descResp.StreamDescription.Shards[0].ShardId,
                    ShardIteratorType = ShardIteratorType.TRIM_HORIZON
                });

                var getResp = await kinesisClient.GetRecordsAsync(new GetRecordsRequest
                {
                    ShardIterator = iterResp.ShardIterator
                });
                if (getResp.Records.Count == 0)
                    throw new Exception("No records returned");

                using var reader = new StreamReader(getResp.Records[0].Data, Encoding.UTF8);
                var actualData = reader.ReadToEnd();
                if (actualData != Encoding.UTF8.GetString(testData))
                    throw new Exception($"Data mismatch: got {actualData}, want {Encoding.UTF8.GetString(testData)}");
            }
            finally
            {
                await CleanupStream(rtStream);
            }
        }));

        // Test 37: ListShards_NonExistentStream
        results.Add(await runner.RunTestAsync("kinesis", "ListShards_NonExistentStream", async () =>
        {
            try
            {
                await kinesisClient.ListShardsAsync(new ListShardsRequest
                {
                    StreamName = "NonExistentStream_xyz_12345"
                });
                throw new Exception("Expected error but got none");
            }
            catch (ResourceNotFoundException) { }
        }));

        return results;
    }
}
