import time
import uuid
from botocore.exceptions import ClientError
from ..runner import TestRunner, TestResult


def _make_unique_name(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"


async def run_cloudwatchlogs_tests(
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
    logs_client = session.client("logs", endpoint_url=endpoint, region_name=region)

    log_group_name = _make_unique_name("TestLogGroup")
    log_stream_name = _make_unique_name("TestLogStream")

    try:

        def _create_log_group():
            logs_client.create_log_group(logGroupName=log_group_name)

        results.append(
            await runner.run_test("logs", "CreateLogGroup", _create_log_group)
        )

        def _describe_log_groups():
            resp = logs_client.describe_log_groups()
            assert resp.get("logGroups") is not None

        results.append(
            await runner.run_test("logs", "DescribeLogGroups", _describe_log_groups)
        )

        def _describe_log_streams():
            resp = logs_client.describe_log_streams(logGroupName=log_group_name)
            assert resp.get("logStreams") is not None

        results.append(
            await runner.run_test("logs", "DescribeLogStreams", _describe_log_streams)
        )

        def _create_log_stream():
            logs_client.create_log_stream(
                logGroupName=log_group_name, logStreamName=log_stream_name
            )

        results.append(
            await runner.run_test("logs", "CreateLogStream", _create_log_stream)
        )

        def _put_log_events():
            logs_client.put_log_events(
                logGroupName=log_group_name,
                logStreamName=log_stream_name,
                logEvents=[
                    {
                        "message": "Test log message",
                        "timestamp": int(time.time() * 1000),
                    }
                ],
            )

        results.append(await runner.run_test("logs", "PutLogEvents", _put_log_events))

        def _get_log_events():
            resp = logs_client.get_log_events(
                logGroupName=log_group_name, logStreamName=log_stream_name
            )
            assert resp.get("events") is not None

        results.append(await runner.run_test("logs", "GetLogEvents", _get_log_events))

        def _filter_log_events():
            resp = logs_client.filter_log_events(logGroupName=log_group_name)
            assert resp.get("events") is not None

        results.append(
            await runner.run_test("logs", "FilterLogEvents", _filter_log_events)
        )

        def _put_retention_policy():
            logs_client.put_retention_policy(
                logGroupName=log_group_name, retentionInDays=7
            )

        results.append(
            await runner.run_test("logs", "PutRetentionPolicy", _put_retention_policy)
        )

        def _delete_log_stream():
            logs_client.delete_log_stream(
                logGroupName=log_group_name, logStreamName=log_stream_name
            )

        results.append(
            await runner.run_test("logs", "DeleteLogStream", _delete_log_stream)
        )

        def _delete_log_group():
            logs_client.delete_log_group(logGroupName=log_group_name)

        results.append(
            await runner.run_test("logs", "DeleteLogGroup", _delete_log_group)
        )

    finally:
        try:
            logs_client.delete_log_group(logGroupName=log_group_name)
        except Exception:
            pass

    dup_group_name = _make_unique_name("DupLogGroup")

    def _create_log_group_duplicate():
        nonlocal dup_group_name
        try:
            logs_client.create_log_group(logGroupName=dup_group_name)
        except Exception:
            pass

        try:
            logs_client.create_log_group(logGroupName=dup_group_name)
            raise AssertionError("Expected ResourceAlreadyExistsException but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceAlreadyExistsException"
        finally:
            try:
                logs_client.delete_log_group(logGroupName=dup_group_name)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "logs", "CreateLogGroup_Duplicate", _create_log_group_duplicate
        )
    )

    def _delete_log_group_nonexistent():
        try:
            logs_client.delete_log_group(logGroupName="nonexistent-log-group-xyz")
            raise AssertionError("Expected error but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "logs", "DeleteLogGroup_NonExistent", _delete_log_group_nonexistent
        )
    )

    rt_group_name = _make_unique_name("RTLogGroup")
    rt_stream_name = _make_unique_name("RTLogStream")

    def _put_log_events_get_log_events_roundtrip():
        nonlocal rt_group_name, rt_stream_name
        try:
            logs_client.create_log_group(logGroupName=rt_group_name)
            logs_client.create_log_stream(
                logGroupName=rt_group_name, logStreamName=rt_stream_name
            )

            test_message = "roundtrip-log-message-verify-12345"
            ts = int(time.time() * 1000)
            logs_client.put_log_events(
                logGroupName=rt_group_name,
                logStreamName=rt_stream_name,
                logEvents=[{"message": test_message, "timestamp": ts}],
            )

            resp = logs_client.get_log_events(
                logGroupName=rt_group_name, logStreamName=rt_stream_name
            )
            assert len(resp.get("events", [])) > 0, "no events returned"
            assert resp["events"][0].get("message") == test_message, (
                f"message mismatch: got {resp['events'][0].get('message')}, want {test_message}"
            )
        finally:
            try:
                logs_client.delete_log_group(logGroupName=rt_group_name)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "logs",
            "PutLogEvents_GetLogEvents_Roundtrip",
            _put_log_events_get_log_events_roundtrip,
        )
    )

    dlg_name = _make_unique_name("DLGGroup")

    def _describe_log_groups_contains_created():
        nonlocal dlg_name
        try:
            logs_client.create_log_group(logGroupName=dlg_name)
            resp = logs_client.describe_log_groups(logGroupNamePrefix=dlg_name)
            assert len(resp.get("logGroups", [])) == 1, (
                f"expected 1 log group, got {len(resp.get('logGroups', []))}"
            )
            assert resp["logGroups"][0].get("logGroupName") == dlg_name, (
                "log group name mismatch"
            )
        finally:
            try:
                logs_client.delete_log_group(logGroupName=dlg_name)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "logs",
            "DescribeLogGroups_ContainsCreated",
            _describe_log_groups_contains_created,
        )
    )

    return results
