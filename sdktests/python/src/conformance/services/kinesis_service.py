import time
import uuid
from botocore.exceptions import ClientError
from ..runner import TestRunner, TestResult


def _make_unique_name(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"


async def run_kinesis_tests(
    runner: TestRunner,
    endpoint: str,
    region: str,
) -> list[TestResult]:
    results: list[TestResult] = []
    import boto3

    session = boto3.Session(
        aws_access_key_id="test",
        aws_secret_access_key="test",
    )
    kinesis_client = session.client(
        "kinesis", endpoint_url=endpoint, region_name=region
    )

    stream_name = _make_unique_name("PyStream")
    stream_created = False
    stream_arn = ""

    try:

        def _create_stream():
            nonlocal stream_created
            kinesis_client.create_stream(StreamName=stream_name, ShardCount=1)
            stream_created = True

        results.append(await runner.run_test("kinesis", "CreateStream", _create_stream))

        def _describe_stream_summary():
            resp = kinesis_client.describe_stream_summary(StreamName=stream_name)
            assert resp.get("StreamDescriptionSummary"), (
                "StreamDescriptionSummary is null"
            )

        results.append(
            await runner.run_test(
                "kinesis", "DescribeStreamSummary", _describe_stream_summary
            )
        )

        def _describe_stream():
            attempts = 0
            while attempts < 30:
                resp = kinesis_client.describe_stream(StreamName=stream_name)
                if resp["StreamDescription"]["StreamStatus"] == "ACTIVE":
                    return
                time.sleep(1)
                attempts += 1
            raise AssertionError("Stream did not become active within timeout")

        results.append(
            await runner.run_test("kinesis", "DescribeStream", _describe_stream)
        )

        def _list_streams():
            resp = kinesis_client.list_streams()
            assert resp.get("StreamNames") is not None
            assert stream_name in resp["StreamNames"]

        results.append(await runner.run_test("kinesis", "ListStreams", _list_streams))

        stream_arn = f"arn:aws:kinesis:{region}:000000000000:stream/{stream_name}"

        def _list_shards():
            resp = kinesis_client.list_shards(StreamName=stream_name)
            assert resp.get("Shards") is not None

        results.append(await runner.run_test("kinesis", "ListShards", _list_shards))

        def _list_shards_multi_shard():
            multi_name = _make_unique_name("PyStreamMulti")
            kinesis_client.create_stream(StreamName=multi_name, ShardCount=3)
            try:
                time.sleep(1)
                resp = kinesis_client.list_shards(StreamName=multi_name)
                assert len(resp.get("Shards", [])) == 3, (
                    f"expected 3 shards, got {len(resp.get('Shards', []))}"
                )
            finally:
                try:
                    kinesis_client.delete_stream(StreamName=multi_name)
                except Exception:
                    pass

        results.append(
            await runner.run_test(
                "kinesis", "ListShards_MultiShard", _list_shards_multi_shard
            )
        )

        def _put_record():
            resp = kinesis_client.put_record(
                StreamName=stream_name,
                Data=b"test record data",
                PartitionKey="partition-key-1",
            )
            assert resp.get("SequenceNumber"), "SequenceNumber is null"
            assert resp.get("ShardId"), "ShardId is null"

        results.append(await runner.run_test("kinesis", "PutRecord", _put_record))

        def _put_records():
            resp = kinesis_client.put_records(
                StreamName=stream_name,
                Records=[
                    {"Data": b"record 1", "PartitionKey": "key-1"},
                    {"Data": b"record 2", "PartitionKey": "key-2"},
                    {"Data": b"record 3", "PartitionKey": "key-3"},
                ],
            )
            assert resp.get("FailedRecordCount") is not None
            assert resp.get("Records") is not None
            for i, rec in enumerate(resp["Records"]):
                assert rec.get("SequenceNumber"), f"record[{i}].SequenceNumber is nil"

        results.append(await runner.run_test("kinesis", "PutRecords", _put_records))

        def _get_shard_iterator():
            nonlocal shard_iterator
            resp = kinesis_client.get_shard_iterator(
                StreamName=stream_name,
                ShardId="shardId-000000000000",
                ShardIteratorType="LATEST",
            )
            assert resp.get("ShardIterator"), "ShardIterator is null"
            shard_iterator = resp["ShardIterator"]

        shard_iterator = ""

        results.append(
            await runner.run_test("kinesis", "GetShardIterator", _get_shard_iterator)
        )

        def _get_records():
            if not shard_iterator:
                raise AssertionError("ShardIterator is empty")
            resp = kinesis_client.get_records(ShardIterator=shard_iterator, Limit=100)
            assert "Records" in resp or "NextShardIterator" in resp

        results.append(await runner.run_test("kinesis", "GetRecords", _get_records))

        def _enable_enhanced_monitoring():
            resp = kinesis_client.enable_enhanced_monitoring(
                StreamName=stream_name,
                ShardLevelMetrics=["IncomingBytes", "OutgoingBytes"],
            )
            assert resp.get("CurrentShardLevelMetrics") is not None

        results.append(
            await runner.run_test(
                "kinesis", "EnableEnhancedMonitoring", _enable_enhanced_monitoring
            )
        )

        def _disable_enhanced_monitoring():
            resp = kinesis_client.disable_enhanced_monitoring(
                StreamName=stream_name,
                ShardLevelMetrics=["IncomingBytes", "OutgoingBytes"],
            )
            assert resp.get("CurrentShardLevelMetrics") is not None

        results.append(
            await runner.run_test(
                "kinesis", "DisableEnhancedMonitoring", _disable_enhanced_monitoring
            )
        )

        def _add_tags_to_stream():
            kinesis_client.add_tags_to_stream(
                StreamName=stream_name,
                Tags={"Environment": "test", "Owner": "test-user"},
            )

        results.append(
            await runner.run_test("kinesis", "AddTagsToStream", _add_tags_to_stream)
        )

        def _list_tags_for_stream():
            resp = kinesis_client.list_tags_for_stream(StreamName=stream_name)
            assert resp.get("Tags") is not None

        results.append(
            await runner.run_test("kinesis", "ListTagsForStream", _list_tags_for_stream)
        )

        def _remove_tags_from_stream():
            kinesis_client.remove_tags_from_stream(
                StreamName=stream_name, TagKeys=["Environment"]
            )

        results.append(
            await runner.run_test(
                "kinesis", "RemoveTagsFromStream", _remove_tags_from_stream
            )
        )

        kms_key = "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012"

        def _start_stream_encryption():
            kinesis_client.start_stream_encryption(
                StreamName=stream_name,
                EncryptionType="KMS",
                KeyId=kms_key,
            )

        results.append(
            await runner.run_test(
                "kinesis", "StartStreamEncryption", _start_stream_encryption
            )
        )

        def _stop_stream_encryption():
            kinesis_client.stop_stream_encryption(
                StreamName=stream_name,
                EncryptionType="KMS",
                KeyId=kms_key,
            )

        results.append(
            await runner.run_test(
                "kinesis", "StopStreamEncryption", _stop_stream_encryption
            )
        )

        def _create_stream_with_tags():
            tags_name = _make_unique_name("PyStreamTags")
            kinesis_client.create_stream(
                StreamName=tags_name,
                ShardCount=1,
                Tags={"Environment": "test", "Owner": "test-user"},
            )
            try:
                kinesis_client.delete_stream(StreamName=tags_name)
            except Exception:
                pass

        results.append(
            await runner.run_test(
                "kinesis", "CreateStreamWithTags", _create_stream_with_tags
            )
        )

        def _increase_stream_retention():
            kinesis_client.increase_stream_retention_period(
                StreamName=stream_name, RetentionPeriodHours=48
            )

        results.append(
            await runner.run_test(
                "kinesis",
                "IncreaseStreamRetentionPeriod",
                _increase_stream_retention,
            )
        )

        def _decrease_stream_retention():
            kinesis_client.decrease_stream_retention_period(
                StreamName=stream_name, RetentionPeriodHours=24
            )

        results.append(
            await runner.run_test(
                "kinesis",
                "DecreaseStreamRetentionPeriod",
                _decrease_stream_retention,
            )
        )

        consumer_name = _make_unique_name("PyConsumer")

        def _register_stream_consumer():
            resp = kinesis_client.register_stream_consumer(
                StreamARN=stream_arn, ConsumerName=consumer_name
            )
            assert resp.get("Consumer"), "Consumer is nil"

        results.append(
            await runner.run_test(
                "kinesis", "RegisterStreamConsumer", _register_stream_consumer
            )
        )

        def _describe_stream_consumer():
            resp = kinesis_client.describe_stream_consumer(
                StreamARN=stream_arn, ConsumerName=consumer_name
            )
            assert resp.get("ConsumerDescription"), "ConsumerDescription is nil"
            assert resp["ConsumerDescription"]["ConsumerARN"], (
                "ConsumerDescription.ConsumerARN is nil"
            )

        results.append(
            await runner.run_test(
                "kinesis", "DescribeStreamConsumer", _describe_stream_consumer
            )
        )

        def _list_stream_consumers():
            resp = kinesis_client.list_stream_consumers(StreamARN=stream_arn)
            assert resp.get("Consumers") is not None

        results.append(
            await runner.run_test(
                "kinesis", "ListStreamConsumers", _list_stream_consumers
            )
        )

        def _deregister_stream_consumer():
            kinesis_client.deregister_stream_consumer(
                StreamARN=stream_arn, ConsumerName=consumer_name
            )

        results.append(
            await runner.run_test(
                "kinesis", "DeregisterStreamConsumer", _deregister_stream_consumer
            )
        )

        def _update_stream_mode():
            mode_name = _make_unique_name("PyStreamMode")
            kinesis_client.create_stream(StreamName=mode_name, ShardCount=1)
            try:
                time.sleep(1)
                desc_resp = kinesis_client.describe_stream(StreamName=mode_name)
                mode_arn = desc_resp["StreamDescription"]["StreamARN"]
                kinesis_client.update_stream_mode(
                    StreamARN=mode_arn,
                    StreamModeDetails={"StreamMode": "ON_DEMAND"},
                )
            finally:
                try:
                    kinesis_client.delete_stream(StreamName=mode_name)
                except Exception:
                    pass

        results.append(
            await runner.run_test("kinesis", "UpdateStreamMode", _update_stream_mode)
        )

        def _update_shard_count():
            resp = kinesis_client.update_shard_count(
                StreamName=stream_name,
                TargetShardCount=2,
                ScalingType="UNIFORM_SCALING",
            )
            assert resp.get("CurrentShardCount") is not None

        results.append(
            await runner.run_test("kinesis", "UpdateShardCount", _update_shard_count)
        )

        def _merge_shards():
            merge_name = _make_unique_name("PyStreamMerge")
            kinesis_client.create_stream(StreamName=merge_name, ShardCount=2)
            try:
                time.sleep(1)
                resp = kinesis_client.list_shards(StreamName=merge_name)
                open_shards = [
                    s
                    for s in resp["Shards"]
                    if not s.get("SequenceNumberRange", {}).get("EndingSequenceNumber")
                ]
                assert len(open_shards) >= 2, (
                    f"need at least 2 open shards for merge, got {len(open_shards)}"
                )
                kinesis_client.merge_shards(
                    StreamName=merge_name,
                    ShardToMerge=open_shards[0]["ShardId"],
                    AdjacentShardToMerge=open_shards[1]["ShardId"],
                )
            finally:
                try:
                    kinesis_client.delete_stream(StreamName=merge_name)
                except Exception:
                    pass

        results.append(await runner.run_test("kinesis", "MergeShards", _merge_shards))

        def _split_shard():
            resp = kinesis_client.list_shards(StreamName=stream_name)
            open_shard = None
            for s in resp["Shards"]:
                if not s.get("SequenceNumberRange", {}).get("EndingSequenceNumber"):
                    open_shard = s
                    break
            assert open_shard is not None, "no open shard found for split"
            kinesis_client.split_shard(
                StreamName=stream_name,
                ShardToSplit=open_shard["ShardId"],
                NewStartingHashKey="9223372036854775808",
            )

        results.append(await runner.run_test("kinesis", "SplitShard", _split_shard))

        def _describe_limits():
            resp = kinesis_client.describe_limits()
            assert resp.get("ShardLimit") is not None

        results.append(
            await runner.run_test("kinesis", "DescribeLimits", _describe_limits)
        )

        def _delete_stream():
            nonlocal stream_created
            kinesis_client.delete_stream(StreamName=stream_name)
            stream_created = False

        results.append(await runner.run_test("kinesis", "DeleteStream", _delete_stream))

        def _list_shards_with_exclusive_start():
            list_name = _make_unique_name("PyStreamList")
            kinesis_client.create_stream(StreamName=list_name, ShardCount=1)
            try:
                time.sleep(1)
                resp = kinesis_client.list_shards(StreamName=list_name)
                assert resp.get("Shards") is not None
            finally:
                try:
                    kinesis_client.delete_stream(StreamName=list_name)
                except Exception:
                    pass

        results.append(
            await runner.run_test(
                "kinesis",
                "ListShardsWithExclusiveStart",
                _list_shards_with_exclusive_start,
            )
        )

    finally:
        try:
            if stream_created:
                kinesis_client.delete_stream(StreamName=stream_name)
        except Exception:
            pass

    def _describe_stream_nonexistent():
        try:
            kinesis_client.describe_stream(StreamName="NonExistentStream_xyz_12345")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "kinesis",
            "DescribeStream_NonExistent",
            _describe_stream_nonexistent,
        )
    )

    def _put_record_nonexistent():
        try:
            kinesis_client.put_record(
                StreamName="NonExistentStream_xyz_12345",
                Data=b"test",
                PartitionKey="key",
            )
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "kinesis", "PutRecord_NonExistent", _put_record_nonexistent
        )
    )

    def _delete_stream_nonexistent():
        try:
            kinesis_client.delete_stream(StreamName="nonexistent-stream-xyz")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "kinesis", "DeleteStream_NonExistent", _delete_stream_nonexistent
        )
    )

    def _create_stream_duplicate():
        dup_name = _make_unique_name("PyDupStream")
        kinesis_client.create_stream(StreamName=dup_name, ShardCount=1)
        try:
            try:
                kinesis_client.create_stream(StreamName=dup_name, ShardCount=1)
                raise AssertionError("expected error for duplicate stream name")
            except ClientError as e:
                assert e.response["Error"]["Code"] == "ResourceInUseException"
        finally:
            try:
                kinesis_client.delete_stream(StreamName=dup_name)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "kinesis", "CreateStream_DuplicateName", _create_stream_duplicate
        )
    )

    def _put_record_get_records_roundtrip():
        rt_name = _make_unique_name("PyRTStream")
        kinesis_client.create_stream(StreamName=rt_name, ShardCount=1)
        try:
            time.sleep(1)
            test_data = b"roundtrip-kinesis-data-verify"
            put_resp = kinesis_client.put_record(
                StreamName=rt_name,
                Data=test_data,
                PartitionKey="partition-1",
            )
            assert put_resp.get("SequenceNumber"), "sequence number is nil"

            desc_resp = kinesis_client.describe_stream(StreamName=rt_name)
            shards = desc_resp["StreamDescription"]["Shards"]
            assert len(shards) > 0, "no shards"

            iter_resp = kinesis_client.get_shard_iterator(
                StreamName=rt_name,
                ShardId=shards[0]["ShardId"],
                ShardIteratorType="TRIM_HORIZON",
            )
            get_resp = kinesis_client.get_records(
                ShardIterator=iter_resp["ShardIterator"]
            )
            assert len(get_resp.get("Records", [])) > 0, "no records returned"
            assert get_resp["Records"][0]["Data"] == test_data, (
                f"data mismatch: got {get_resp['Records'][0]['Data']!r}, want {test_data!r}"
            )
        finally:
            try:
                kinesis_client.delete_stream(StreamName=rt_name)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "kinesis",
            "PutRecord_GetRecords_Roundtrip",
            _put_record_get_records_roundtrip,
        )
    )

    def _list_shards_nonexistent_stream():
        try:
            kinesis_client.list_shards(StreamName="nonexistent-stream-xyz")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "kinesis",
            "ListShards_NonExistentStream",
            _list_shards_nonexistent_stream,
        )
    )

    return results
