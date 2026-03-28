import time
import json
from ..runner import TestRunner, TestResult


async def run_scheduler_tests(
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
    scheduler_client = session.client(
        "scheduler", endpoint_url=endpoint, region_name=region
    )
    iam_client = session.client("iam", endpoint_url=endpoint, region_name=region)

    schedule_name = f"test-schedule-{int(time.time() * 1000)}"
    account_id = "123456789012"
    role_name = f"scheduler-test-role-{int(time.time() * 1000)}"
    role_arn = f"arn:aws:iam::{account_id}:role/{role_name}"

    trust_policy = {
        "Version": "2012-10-17",
        "Statement": [
            {
                "Effect": "Allow",
                "Principal": {"Service": "scheduler.amazonaws.com"},
                "Action": "sts:AssumeRole",
            }
        ],
    }

    iam_client.create_role(
        RoleName=role_name,
        AssumeRolePolicyDocument=json.dumps(trust_policy),
    )

    def _create_schedule():
        scheduler_client.create_schedule(
            Name=schedule_name,
            ScheduleExpression="rate(1 day)",
            FlexibleTimeWindow={"Mode": "OFF"},
            Target={
                "Arn": role_arn,
                "RoleArn": role_arn,
            },
        )

    results.append(
        await runner.run_test("scheduler", "CreateSchedule", _create_schedule)
    )

    def _get_schedule():
        resp = scheduler_client.get_schedule(Name=schedule_name)
        assert resp.get("Name") == schedule_name, "Schedule Name mismatch"

    results.append(await runner.run_test("scheduler", "GetSchedule", _get_schedule))

    def _list_schedules():
        resp = scheduler_client.list_schedules()
        assert resp.get("Schedules") is not None

    results.append(await runner.run_test("scheduler", "ListSchedules", _list_schedules))

    def _update_schedule():
        scheduler_client.update_schedule(
            Name=schedule_name,
            ScheduleExpression="rate(2 days)",
            FlexibleTimeWindow={"Mode": "OFF"},
            Target={
                "Arn": role_arn,
                "RoleArn": role_arn,
            },
        )

    results.append(
        await runner.run_test("scheduler", "UpdateSchedule", _update_schedule)
    )

    get_sched_resp = scheduler_client.get_schedule(Name=schedule_name)
    schedule_arn = get_sched_resp.get("Arn", "")
    assert schedule_arn, "Schedule Arn is null"

    def _tag_resource():
        scheduler_client.tag_resource(
            ResourceArn=schedule_arn
            or f"arn:aws:scheduler:us-east-1:{account_id}:schedule/default/{schedule_name}",
            Tags=[
                {"Key": "Environment", "Value": "test"},
                {"Key": "Owner", "Value": "test-user"},
            ],
        )

    results.append(await runner.run_test("scheduler", "TagResource", _tag_resource))

    def _list_tags_for_resource():
        resp = scheduler_client.list_tags_for_resource(
            ResourceArn=schedule_arn
            or f"arn:aws:scheduler:us-east-1:{account_id}:schedule/default/{schedule_name}",
        )
        assert resp.get("Tags") is not None

    results.append(
        await runner.run_test(
            "scheduler", "ListTagsForResource", _list_tags_for_resource
        )
    )

    def _untag_resource():
        scheduler_client.untag_resource(
            ResourceArn=schedule_arn
            or f"arn:aws:scheduler:us-east-1:{account_id}:schedule/default/{schedule_name}",
            TagKeys=["Environment"],
        )

    results.append(await runner.run_test("scheduler", "UntagResource", _untag_resource))

    def _delete_schedule():
        scheduler_client.delete_schedule(Name=schedule_name)

    results.append(
        await runner.run_test("scheduler", "DeleteSchedule", _delete_schedule)
    )

    def _get_schedule_nonexistent():
        try:
            scheduler_client.get_schedule(Name="nonexistent-schedule-xyz")
            raise Exception("expected error for non-existent schedule")
        except Exception as e:
            if str(e) == "expected error for non-existent schedule":
                raise

    results.append(
        await runner.run_test(
            "scheduler", "GetSchedule_NonExistent", _get_schedule_nonexistent
        )
    )

    def _delete_schedule_nonexistent():
        try:
            scheduler_client.delete_schedule(Name="nonexistent-schedule-xyz")
            raise Exception("expected error for non-existent schedule")
        except Exception as e:
            if str(e) == "expected error for non-existent schedule":
                raise

    results.append(
        await runner.run_test(
            "scheduler", "DeleteSchedule_NonExistent", _delete_schedule_nonexistent
        )
    )

    dup_name = f"dup-schedule-{int(time.time() * 1000)}"

    def _create_schedule_duplicate_name():
        scheduler_client.create_schedule(
            Name=dup_name,
            ScheduleExpression="rate(1 day)",
            FlexibleTimeWindow={"Mode": "OFF"},
            Target={
                "Arn": role_arn,
                "RoleArn": role_arn,
            },
        )
        try:
            scheduler_client.create_schedule(
                Name=dup_name,
                ScheduleExpression="rate(1 day)",
                FlexibleTimeWindow={"Mode": "OFF"},
                Target={
                    "Arn": role_arn,
                    "RoleArn": role_arn,
                },
            )
            raise Exception("expected error for duplicate schedule name")
        except Exception as e:
            if str(e) == "expected error for duplicate schedule name":
                raise
        finally:
            try:
                scheduler_client.delete_schedule(Name=dup_name)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "scheduler", "CreateSchedule_DuplicateName", _create_schedule_duplicate_name
        )
    )

    verify_name = f"verify-schedule-{int(time.time() * 1000)}"

    def _update_schedule_verify_expression():
        scheduler_client.create_schedule(
            Name=verify_name,
            ScheduleExpression="rate(1 day)",
            FlexibleTimeWindow={"Mode": "OFF"},
            Target={
                "Arn": role_arn,
                "RoleArn": role_arn,
            },
        )
        scheduler_client.update_schedule(
            Name=verify_name,
            ScheduleExpression="rate(3 days)",
            FlexibleTimeWindow={"Mode": "OFF"},
            Target={
                "Arn": role_arn,
                "RoleArn": role_arn,
            },
        )
        get_resp = scheduler_client.get_schedule(Name=verify_name)
        if "3 days" not in get_resp.get("ScheduleExpression", ""):
            raise Exception(
                f"schedule expression not updated, got {get_resp.get('ScheduleExpression')}"
            )
        scheduler_client.delete_schedule(Name=verify_name)

    results.append(
        await runner.run_test(
            "scheduler",
            "UpdateSchedule_VerifyExpression",
            _update_schedule_verify_expression,
        )
    )

    try:
        iam_client.delete_role(RoleName=role_name)
    except Exception:
        pass

    return results
