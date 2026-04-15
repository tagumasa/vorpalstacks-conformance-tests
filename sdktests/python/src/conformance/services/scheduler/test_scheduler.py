import json

import pytest


@pytest.fixture(scope="class")
def scheduler_role(iam_client, unique_name):
    account_id = "123456789012"
    role_name = unique_name("role")
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
    yield role_name, role_arn
    try:
        iam_client.delete_role(RoleName=role_name)
    except Exception:
        pass


class TestCreateSchedule:
    def test_create_schedule(self, scheduler_client, unique_name, scheduler_role):
        schedule_name = unique_name("sched")
        _, role_arn = scheduler_role
        scheduler_client.create_schedule(
            Name=schedule_name,
            ScheduleExpression="rate(1 day)",
            FlexibleTimeWindow={"Mode": "OFF"},
            Target={"Arn": role_arn, "RoleArn": role_arn},
        )

    def test_duplicate_name(self, scheduler_client, unique_name, scheduler_role):
        dup_name = unique_name("dup")
        _, role_arn = scheduler_role
        scheduler_client.create_schedule(
            Name=dup_name,
            ScheduleExpression="rate(1 day)",
            FlexibleTimeWindow={"Mode": "OFF"},
            Target={"Arn": role_arn, "RoleArn": role_arn},
        )
        try:
            with pytest.raises(Exception):
                scheduler_client.create_schedule(
                    Name=dup_name,
                    ScheduleExpression="rate(1 day)",
                    FlexibleTimeWindow={"Mode": "OFF"},
                    Target={"Arn": role_arn, "RoleArn": role_arn},
                )
        finally:
            try:
                scheduler_client.delete_schedule(Name=dup_name)
            except Exception:
                pass


class TestGetSchedule:
    def test_get_schedule(self, scheduler_client, unique_name, scheduler_role):
        schedule_name = unique_name("sched")
        _, role_arn = scheduler_role
        scheduler_client.create_schedule(
            Name=schedule_name,
            ScheduleExpression="rate(1 day)",
            FlexibleTimeWindow={"Mode": "OFF"},
            Target={"Arn": role_arn, "RoleArn": role_arn},
        )
        resp = scheduler_client.get_schedule(Name=schedule_name)
        assert resp.get("Name") == schedule_name, "Schedule Name mismatch"

    def test_nonexistent(self, scheduler_client):
        with pytest.raises(Exception):
            scheduler_client.get_schedule(Name="nonexistent-schedule-xyz")


class TestListSchedules:
    def test_list_schedules(self, scheduler_client):
        resp = scheduler_client.list_schedules()
        assert resp.get("Schedules") is not None


class TestUpdateSchedule:
    def test_update_schedule(self, scheduler_client, unique_name, scheduler_role):
        schedule_name = unique_name("sched")
        _, role_arn = scheduler_role
        scheduler_client.create_schedule(
            Name=schedule_name,
            ScheduleExpression="rate(1 day)",
            FlexibleTimeWindow={"Mode": "OFF"},
            Target={"Arn": role_arn, "RoleArn": role_arn},
        )
        scheduler_client.update_schedule(
            Name=schedule_name,
            ScheduleExpression="rate(2 days)",
            FlexibleTimeWindow={"Mode": "OFF"},
            Target={"Arn": role_arn, "RoleArn": role_arn},
        )

    def test_verify_expression(self, scheduler_client, unique_name, scheduler_role):
        verify_name = unique_name("vsched")
        _, role_arn = scheduler_role
        scheduler_client.create_schedule(
            Name=verify_name,
            ScheduleExpression="rate(1 day)",
            FlexibleTimeWindow={"Mode": "OFF"},
            Target={"Arn": role_arn, "RoleArn": role_arn},
        )
        scheduler_client.update_schedule(
            Name=verify_name,
            ScheduleExpression="rate(3 days)",
            FlexibleTimeWindow={"Mode": "OFF"},
            Target={"Arn": role_arn, "RoleArn": role_arn},
        )
        get_resp = scheduler_client.get_schedule(Name=verify_name)
        assert "3 days" in get_resp.get("ScheduleExpression", ""), (
            f"schedule expression not updated, got {get_resp.get('ScheduleExpression')}"
        )
        scheduler_client.delete_schedule(Name=verify_name)


class TestTags:
    def test_tag_resource(self, scheduler_client, unique_name, scheduler_role):
        schedule_name = unique_name("sched")
        _, role_arn = scheduler_role
        scheduler_client.create_schedule(
            Name=schedule_name,
            ScheduleExpression="rate(1 day)",
            FlexibleTimeWindow={"Mode": "OFF"},
            Target={"Arn": role_arn, "RoleArn": role_arn},
        )
        get_sched_resp = scheduler_client.get_schedule(Name=schedule_name)
        schedule_arn = get_sched_resp.get("Arn", "")
        scheduler_client.tag_resource(
            ResourceArn=schedule_arn,
            Tags=[
                {"Key": "Environment", "Value": "test"},
                {"Key": "Owner", "Value": "test-user"},
            ],
        )

    def test_list_tags_for_resource(
        self, scheduler_client, unique_name, scheduler_role
    ):
        schedule_name = unique_name("sched")
        _, role_arn = scheduler_role
        scheduler_client.create_schedule(
            Name=schedule_name,
            ScheduleExpression="rate(1 day)",
            FlexibleTimeWindow={"Mode": "OFF"},
            Target={"Arn": role_arn, "RoleArn": role_arn},
        )
        get_sched_resp = scheduler_client.get_schedule(Name=schedule_name)
        schedule_arn = get_sched_resp.get("Arn", "")
        resp = scheduler_client.list_tags_for_resource(ResourceArn=schedule_arn)
        tags = resp.get("Tags", [])
        assert isinstance(tags, list)

    def test_untag_resource(self, scheduler_client, unique_name, scheduler_role):
        schedule_name = unique_name("sched")
        _, role_arn = scheduler_role
        scheduler_client.create_schedule(
            Name=schedule_name,
            ScheduleExpression="rate(1 day)",
            FlexibleTimeWindow={"Mode": "OFF"},
            Target={"Arn": role_arn, "RoleArn": role_arn},
        )
        get_sched_resp = scheduler_client.get_schedule(Name=schedule_name)
        schedule_arn = get_sched_resp.get("Arn", "")
        scheduler_client.untag_resource(
            ResourceArn=schedule_arn,
            TagKeys=["Environment"],
        )


class TestDeleteSchedule:
    def test_delete_schedule(self, scheduler_client, unique_name, scheduler_role):
        schedule_name = unique_name("sched")
        _, role_arn = scheduler_role
        scheduler_client.create_schedule(
            Name=schedule_name,
            ScheduleExpression="rate(1 day)",
            FlexibleTimeWindow={"Mode": "OFF"},
            Target={"Arn": role_arn, "RoleArn": role_arn},
        )
        scheduler_client.delete_schedule(Name=schedule_name)

    def test_nonexistent(self, scheduler_client):
        with pytest.raises(Exception):
            scheduler_client.delete_schedule(Name="nonexistent-schedule-xyz")
