import time

import pytest

from botocore.exceptions import ClientError

from conformance.conftest import assert_client_error


@pytest.fixture(scope="class")
def log_group(cloudwatchlogs_client, unique_name):
    log_group_name = unique_name("lg")
    log_stream_name = unique_name("ls")
    cloudwatchlogs_client.create_log_group(logGroupName=log_group_name)
    yield log_group_name, log_stream_name
    try:
        cloudwatchlogs_client.delete_log_group(logGroupName=log_group_name)
    except Exception:
        pass


class TestCreateLogGroup:
    def test_create_log_group(self, cloudwatchlogs_client, unique_name):
        log_group_name = unique_name("create_lg")
        cloudwatchlogs_client.create_log_group(logGroupName=log_group_name)
        try:
            cloudwatchlogs_client.delete_log_group(logGroupName=log_group_name)
        except Exception:
            pass

    def test_duplicate(self, cloudwatchlogs_client, unique_name):
        dup_group_name = unique_name("dup")
        cloudwatchlogs_client.create_log_group(logGroupName=dup_group_name)
        with pytest.raises(ClientError) as exc_info:
            cloudwatchlogs_client.create_log_group(logGroupName=dup_group_name)
        assert_client_error(exc_info, "ResourceAlreadyExistsException")
        try:
            cloudwatchlogs_client.delete_log_group(logGroupName=dup_group_name)
        except Exception:
            pass


class TestDeleteLogGroup:
    def test_delete_log_group(self, cloudwatchlogs_client, log_group):
        log_group_name, _ = log_group
        cloudwatchlogs_client.delete_log_group(logGroupName=log_group_name)

    def test_nonexistent(self, cloudwatchlogs_client):
        with pytest.raises(ClientError) as exc_info:
            cloudwatchlogs_client.delete_log_group(
                logGroupName="nonexistent-log-group-xyz"
            )
        assert_client_error(exc_info, "ResourceNotFoundException")


class TestDescribeLogGroups:
    def test_describe_log_groups(self, cloudwatchlogs_client):
        resp = cloudwatchlogs_client.describe_log_groups()
        assert resp.get("logGroups") is not None

    def test_contains_created(self, cloudwatchlogs_client, unique_name):
        dlg_name = unique_name("dlg")
        try:
            cloudwatchlogs_client.create_log_group(logGroupName=dlg_name)
            resp = cloudwatchlogs_client.describe_log_groups(
                logGroupNamePrefix=dlg_name
            )
            assert len(resp.get("logGroups", [])) == 1, (
                f"expected 1 log group, got {len(resp.get('logGroups', []))}"
            )
            assert resp["logGroups"][0].get("logGroupName") == dlg_name, (
                "log group name mismatch"
            )
        finally:
            try:
                cloudwatchlogs_client.delete_log_group(logGroupName=dlg_name)
            except Exception:
                pass


class TestDescribeLogStreams:
    def test_describe_log_streams(self, cloudwatchlogs_client, log_group):
        log_group_name, _ = log_group
        resp = cloudwatchlogs_client.describe_log_streams(logGroupName=log_group_name)
        assert resp.get("logStreams") is not None


class TestLogStream:
    def test_create_log_stream(self, cloudwatchlogs_client, log_group):
        log_group_name, log_stream_name = log_group
        cloudwatchlogs_client.create_log_stream(
            logGroupName=log_group_name, logStreamName=log_stream_name
        )

    def test_delete_log_stream(self, cloudwatchlogs_client, log_group):
        log_group_name, log_stream_name = log_group
        cloudwatchlogs_client.delete_log_stream(
            logGroupName=log_group_name, logStreamName=log_stream_name
        )


class TestPutLogEvents:
    def test_put_log_events(self, cloudwatchlogs_client, log_group):
        log_group_name, log_stream_name = log_group
        cloudwatchlogs_client.create_log_stream(
            logGroupName=log_group_name, logStreamName=log_stream_name
        )
        cloudwatchlogs_client.put_log_events(
            logGroupName=log_group_name,
            logStreamName=log_stream_name,
            logEvents=[
                {"message": "Test log message", "timestamp": int(time.time() * 1000)}
            ],
        )

    def test_get_log_events_roundtrip(self, cloudwatchlogs_client, unique_name):
        rt_group_name = unique_name("rtg")
        rt_stream_name = unique_name("rts")
        try:
            cloudwatchlogs_client.create_log_group(logGroupName=rt_group_name)
            cloudwatchlogs_client.create_log_stream(
                logGroupName=rt_group_name, logStreamName=rt_stream_name
            )
            test_message = "roundtrip-log-message-verify-12345"
            ts = int(time.time() * 1000)
            cloudwatchlogs_client.put_log_events(
                logGroupName=rt_group_name,
                logStreamName=rt_stream_name,
                logEvents=[{"message": test_message, "timestamp": ts}],
            )
            resp = cloudwatchlogs_client.get_log_events(
                logGroupName=rt_group_name, logStreamName=rt_stream_name
            )
            assert len(resp.get("events", [])) > 0, "no events returned"
            assert resp["events"][0].get("message") == test_message, (
                f"message mismatch: got {resp['events'][0].get('message')}, want {test_message}"
            )
        finally:
            try:
                cloudwatchlogs_client.delete_log_group(logGroupName=rt_group_name)
            except Exception:
                pass


class TestGetLogEvents:
    def test_get_log_events(self, cloudwatchlogs_client, log_group):
        log_group_name, log_stream_name = log_group
        cloudwatchlogs_client.create_log_stream(
            logGroupName=log_group_name, logStreamName=log_stream_name
        )
        cloudwatchlogs_client.put_log_events(
            logGroupName=log_group_name,
            logStreamName=log_stream_name,
            logEvents=[
                {"message": "Test log message", "timestamp": int(time.time() * 1000)}
            ],
        )
        resp = cloudwatchlogs_client.get_log_events(
            logGroupName=log_group_name, logStreamName=log_stream_name
        )
        assert resp.get("events") is not None


class TestFilterLogEvents:
    def test_filter_log_events(self, cloudwatchlogs_client, log_group):
        log_group_name, _ = log_group
        resp = cloudwatchlogs_client.filter_log_events(logGroupName=log_group_name)
        assert resp.get("events") is not None


class TestPutRetentionPolicy:
    def test_put_retention_policy(self, cloudwatchlogs_client, log_group):
        log_group_name, _ = log_group
        cloudwatchlogs_client.put_retention_policy(
            logGroupName=log_group_name, retentionInDays=7
        )
