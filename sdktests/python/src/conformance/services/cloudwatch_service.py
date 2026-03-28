import time
from ..runner import TestRunner, TestResult


async def run_cloudwatch_tests(
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
    client = session.client("cloudwatch", endpoint_url=endpoint, region_name=region)

    namespace = f"TestNamespace-{int(time.time() * 1000)}"
    metric_name = f"TestMetric-{int(time.time() * 1000)}"

    def _put_metric_data():
        client.put_metric_data(
            Namespace=namespace,
            MetricData=[
                {
                    "MetricName": metric_name,
                    "Value": 42.0,
                    "Timestamp": time.time(),
                },
            ],
        )

    results.append(
        await runner.run_test("cloudwatch", "PutMetricData", _put_metric_data)
    )

    def _list_metrics():
        resp = client.list_metrics(Namespace=namespace)
        if not resp.get("Metrics"):
            raise Exception("metrics list is nil")

    results.append(await runner.run_test("cloudwatch", "ListMetrics", _list_metrics))

    def _get_metric_statistics():
        now = time.time()
        client.get_metric_statistics(
            Namespace=namespace,
            MetricName=metric_name,
            StartTime=now - 3600,
            EndTime=now,
            Period=300,
            Statistics=["Average"],
        )

    results.append(
        await runner.run_test(
            "cloudwatch", "GetMetricStatistics", _get_metric_statistics
        )
    )

    alarm_name = f"TestAlarm-{int(time.time() * 1000)}"

    def _put_metric_alarm():
        client.put_metric_alarm(
            AlarmName=alarm_name,
            ComparisonOperator="GreaterThanThreshold",
            EvaluationPeriods=1,
            MetricName=metric_name,
            Namespace=namespace,
            Period=300,
            Threshold=50.0,
            Statistic="Average",
        )

    results.append(
        await runner.run_test("cloudwatch", "PutMetricAlarm", _put_metric_alarm)
    )

    def _describe_alarms():
        resp = client.describe_alarms()
        if not resp.get("MetricAlarms"):
            raise Exception("metric alarms list is nil")

    results.append(
        await runner.run_test("cloudwatch", "DescribeAlarms", _describe_alarms)
    )

    def _list_dashboards():
        client.list_dashboards()

    results.append(
        await runner.run_test("cloudwatch", "ListDashboards", _list_dashboards)
    )

    def _roundtrip_test():
        test_ns = f"RoundtripNS-{int(time.time() * 1000)}"
        test_metric = f"RoundtripMetric-{int(time.time() * 1000)}"
        now = time.time()

        client.put_metric_data(
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

        list_resp = client.list_metrics(Namespace=test_ns, MetricName=test_metric)
        if not list_resp.get("Metrics") or len(list_resp["Metrics"]) == 0:
            raise Exception("metric not found in ListMetrics")

        client.get_metric_statistics(
            Namespace=test_ns,
            MetricName=test_metric,
            StartTime=now - 600,
            EndTime=now + 60,
            Period=60,
            Statistics=["Sum"],
        )

    results.append(
        await runner.run_test(
            "cloudwatch", "PutMetricData_GetMetricStatistics_Roundtrip", _roundtrip_test
        )
    )

    def _describe_alarms_nonexistent():
        alarm_name = f"NonExistentAlarm-{int(time.time() * 1000)}"
        resp = client.describe_alarms(AlarmNames=[alarm_name])
        if resp.get("MetricAlarms") and len(resp["MetricAlarms"]) != 0:
            raise Exception(f"expected no alarms, got {len(resp['MetricAlarms'])}")

    results.append(
        await runner.run_test(
            "cloudwatch", "DescribeAlarms_NonExistent", _describe_alarms_nonexistent
        )
    )

    def _delete_alarm_test():
        alarm_name = f"DeleteAlarm-{int(time.time() * 1000)}"
        test_ns = f"AlarmNS-{int(time.time() * 1000)}"

        client.put_metric_alarm(
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

        desc_resp = client.describe_alarms(AlarmNames=[alarm_name])
        if not desc_resp.get("MetricAlarms") or len(desc_resp["MetricAlarms"]) != 1:
            raise Exception(
                f"expected 1 alarm, got {len(desc_resp.get('MetricAlarms', []))}"
            )
        if (
            desc_resp["MetricAlarms"][0].get("AlarmDescription")
            != "Test alarm for deletion"
        ):
            raise Exception("alarm description mismatch")

        client.delete_alarms(AlarmNames=[alarm_name])

    results.append(
        await runner.run_test(
            "cloudwatch", "PutMetricAlarm_DeleteAlarm", _delete_alarm_test
        )
    )

    return results
