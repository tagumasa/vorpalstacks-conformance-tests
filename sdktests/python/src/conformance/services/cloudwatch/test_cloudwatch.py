import time

import pytest


class TestPutMetricData:
    def test_put_metric_data(self, cloudwatch_client, unique_name):
        namespace = unique_name("ns")
        metric_name = unique_name("metric")
        cloudwatch_client.put_metric_data(
            Namespace=namespace,
            MetricData=[
                {"MetricName": metric_name, "Value": 42.0, "Timestamp": time.time()},
            ],
        )

    def test_roundtrip(self, cloudwatch_client, unique_name):
        test_ns = unique_name("rns")
        test_metric = unique_name("rmetric")
        now = time.time()
        cloudwatch_client.put_metric_data(
            Namespace=test_ns,
            MetricData=[
                {
                    "MetricName": test_metric,
                    "Value": 42.0,
                    "Unit": "None",
                    "Timestamp": now - 300,
                },
                {
                    "MetricName": test_metric,
                    "Value": 58.0,
                    "Unit": "None",
                    "Timestamp": now - 120,
                },
            ],
        )
        list_resp = cloudwatch_client.list_metrics(
            Namespace=test_ns, MetricName=test_metric
        )
        assert list_resp.get("Metrics") and len(list_resp["Metrics"]) > 0, (
            "metric not found in ListMetrics"
        )
        cloudwatch_client.get_metric_statistics(
            Namespace=test_ns,
            MetricName=test_metric,
            StartTime=now - 600,
            EndTime=now + 60,
            Period=60,
            Statistics=["Sum"],
        )


class TestListMetrics:
    def test_list_metrics(self, cloudwatch_client, unique_name):
        namespace = unique_name("ns")
        metric_name = unique_name("metric")
        cloudwatch_client.put_metric_data(
            Namespace=namespace,
            MetricData=[
                {"MetricName": metric_name, "Value": 42.0, "Timestamp": time.time()}
            ],
        )
        resp = cloudwatch_client.list_metrics(Namespace=namespace)
        assert resp.get("Metrics"), "metrics list is nil"


class TestGetMetricStatistics:
    def test_get_metric_statistics(self, cloudwatch_client, unique_name):
        namespace = unique_name("ns")
        metric_name = unique_name("metric")
        cloudwatch_client.put_metric_data(
            Namespace=namespace,
            MetricData=[
                {"MetricName": metric_name, "Value": 42.0, "Timestamp": time.time()}
            ],
        )
        now = time.time()
        cloudwatch_client.get_metric_statistics(
            Namespace=namespace,
            MetricName=metric_name,
            StartTime=now - 3600,
            EndTime=now,
            Period=300,
            Statistics=["Average"],
        )


class TestAlarms:
    def test_put_metric_alarm(self, cloudwatch_client, unique_name):
        alarm_name = unique_name("alarm")
        namespace = unique_name("ns")
        metric_name = unique_name("metric")
        cloudwatch_client.put_metric_alarm(
            AlarmName=alarm_name,
            ComparisonOperator="GreaterThanThreshold",
            EvaluationPeriods=1,
            MetricName=metric_name,
            Namespace=namespace,
            Period=300,
            Threshold=50.0,
            Statistic="Average",
        )

    def test_describe_alarms(self, cloudwatch_client):
        resp = cloudwatch_client.describe_alarms()
        assert resp.get("MetricAlarms"), "metric alarms list is nil"

    def test_describe_alarms_nonexistent(self, cloudwatch_client, unique_name):
        alarm_name = unique_name("alarm")
        resp = cloudwatch_client.describe_alarms(AlarmNames=[alarm_name])
        assert not resp.get("MetricAlarms") or len(resp["MetricAlarms"]) == 0, (
            f"expected no alarms, got {len(resp['MetricAlarms'])}"
        )

    def test_delete_alarm(self, cloudwatch_client, unique_name):
        alarm_name = unique_name("alarm")
        test_ns = unique_name("ns")
        cloudwatch_client.put_metric_alarm(
            AlarmName=alarm_name,
            ComparisonOperator="GreaterThanThreshold",
            EvaluationPeriods=1,
            MetricName="TestMetric",
            Namespace=test_ns,
            Period=300,
            Threshold=50.0,
            Statistic="Average",
            AlarmDescription="Test alarm for deletion",
        )
        desc_resp = cloudwatch_client.describe_alarms(AlarmNames=[alarm_name])
        assert desc_resp.get("MetricAlarms") and len(desc_resp["MetricAlarms"]) == 1, (
            f"expected 1 alarm, got {len(desc_resp.get('MetricAlarms', []))}"
        )
        assert (
            desc_resp["MetricAlarms"][0].get("AlarmDescription")
            == "Test alarm for deletion"
        ), "alarm description mismatch"
        cloudwatch_client.delete_alarms(AlarmNames=[alarm_name])


class TestListDashboards:
    def test_list_dashboards(self, cloudwatch_client):
        cloudwatch_client.list_dashboards()
