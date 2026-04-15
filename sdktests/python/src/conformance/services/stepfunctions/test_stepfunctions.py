import json

import pytest
from botocore.exceptions import ClientError

from conformance.conftest import assert_client_error


SFN_TRUST_POLICY = {
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Principal": {"Service": ["states.amazonaws.com"]},
            "Action": "sts:AssumeRole",
        }
    ],
}


@pytest.fixture(scope="module")
def iam_client(aws_session, endpoint, region):
    return aws_session.client("iam", endpoint_url=endpoint, region_name=region)


@pytest.fixture(scope="module")
def sfn_role_arn(iam_client, unique_name, region):
    role_name = unique_name("PySfnRole")
    try:
        iam_client.create_role(
            RoleName=role_name,
            AssumeRolePolicyDocument=json.dumps(SFN_TRUST_POLICY),
        )
    except Exception:
        pass
    arn = f"arn:aws:iam::000000000000:role/{role_name}"
    yield arn
    try:
        iam_client.delete_role(RoleName=role_name)
    except Exception:
        pass


@pytest.fixture(scope="module")
def state_machine_arn(stepfunctions_client, sfn_role_arn, unique_name):
    name = unique_name("PyStateMachine")
    definition = json.dumps(
        {
            "Comment": "Test state machine",
            "StartAt": "Pass",
            "States": {
                "Pass": {"Type": "Pass", "End": True},
            },
        }
    )
    resp = stepfunctions_client.create_state_machine(
        name=name,
        definition=definition,
        roleArn=sfn_role_arn,
    )
    arn = resp["stateMachineArn"]
    yield arn
    try:
        stepfunctions_client.delete_state_machine(stateMachineArn=arn)
    except Exception:
        pass


@pytest.fixture(scope="module")
def execution_arn(stepfunctions_client, state_machine_arn, unique_name):
    resp = stepfunctions_client.start_execution(
        stateMachineArn=state_machine_arn,
        input=json.dumps({"key": "value"}),
        name=unique_name("PyExecution"),
    )
    return resp["executionArn"]


@pytest.fixture(scope="module")
def activity_arn(stepfunctions_client, unique_name):
    name = unique_name("PyActivity")
    resp = stepfunctions_client.create_activity(name=name)
    arn = resp["activityArn"]
    yield arn
    try:
        stepfunctions_client.delete_activity(activityArn=arn)
    except Exception:
        pass


class TestStateMachineLifecycle:
    def test_create_state_machine(self, state_machine_arn):
        assert state_machine_arn

    def test_describe_state_machine(self, stepfunctions_client, state_machine_arn):
        resp = stepfunctions_client.describe_state_machine(
            stateMachineArn=state_machine_arn
        )
        assert resp.get("stateMachineArn")
        assert resp.get("definition")

    def test_list_state_machines(self, stepfunctions_client, state_machine_arn):
        resp = stepfunctions_client.list_state_machines()
        assert resp.get("stateMachines") is not None
        found = any(
            sm["stateMachineArn"] == state_machine_arn for sm in resp["stateMachines"]
        )
        assert found

    def test_update_state_machine(self, stepfunctions_client, state_machine_arn):
        stepfunctions_client.update_state_machine(
            stateMachineArn=state_machine_arn,
            definition=json.dumps(
                {
                    "Comment": "Updated state machine",
                    "StartAt": "Pass",
                    "States": {
                        "Pass": {"Type": "Pass", "End": True},
                    },
                }
            ),
        )


class TestExecution:
    def test_start_execution(self, execution_arn):
        assert execution_arn

    def test_describe_execution(self, stepfunctions_client, execution_arn):
        resp = stepfunctions_client.describe_execution(executionArn=execution_arn)
        assert resp.get("executionArn")
        assert resp.get("status")

    def test_list_executions(self, stepfunctions_client, state_machine_arn):
        resp = stepfunctions_client.list_executions(stateMachineArn=state_machine_arn)
        assert resp.get("executions") is not None

    def test_get_execution_history(self, stepfunctions_client, execution_arn):
        resp = stepfunctions_client.get_execution_history(executionArn=execution_arn)
        assert resp.get("events") is not None

    def test_stop_execution(self, stepfunctions_client, execution_arn):
        stepfunctions_client.stop_execution(
            executionArn=execution_arn,
            error="TestError",
            cause="Test cause for stopping",
        )


class TestActivity:
    def test_create_activity(self, activity_arn):
        assert activity_arn

    def test_describe_activity(self, stepfunctions_client, activity_arn):
        resp = stepfunctions_client.describe_activity(activityArn=activity_arn)
        assert resp.get("name")

    def test_list_activities(self, stepfunctions_client):
        resp = stepfunctions_client.list_activities()
        assert resp.get("activities") is not None

    def test_delete_activity(self, stepfunctions_client, activity_arn):
        stepfunctions_client.delete_activity(activityArn=activity_arn)


class TestTags:
    def test_tag_resource(self, stepfunctions_client, state_machine_arn):
        stepfunctions_client.tag_resource(
            resourceArn=state_machine_arn,
            tags=[{"key": "Environment", "value": "test"}],
        )

    def test_list_tags_for_resource(self, stepfunctions_client, state_machine_arn):
        resp = stepfunctions_client.list_tags_for_resource(
            resourceArn=state_machine_arn
        )
        assert resp.get("tags") is not None

    def test_untag_resource(self, stepfunctions_client, state_machine_arn):
        stepfunctions_client.untag_resource(
            resourceArn=state_machine_arn, tagKeys=["Environment"]
        )


class TestDeleteStateMachine:
    def test_delete_state_machine(self, stepfunctions_client, state_machine_arn):
        stepfunctions_client.delete_state_machine(stateMachineArn=state_machine_arn)


class TestErrorCases:
    def test_describe_state_machine_nonexistent(self, stepfunctions_client):
        with pytest.raises(ClientError) as exc_info:
            stepfunctions_client.describe_state_machine(
                stateMachineArn="arn:aws:states:us-east-1:000000000000:stateMachine:NonExistent_xyz_12345"
            )
        assert_client_error(exc_info, ("StateMachineDoesNotExist", "ResourceNotFound"))

    def test_start_execution_nonexistent(self, stepfunctions_client):
        with pytest.raises(ClientError) as exc_info:
            stepfunctions_client.start_execution(
                stateMachineArn="arn:aws:states:us-east-1:000000000000:stateMachine:NonExistent_xyz_12345"
            )
        assert_client_error(exc_info, ("StateMachineDoesNotExist", "ResourceNotFound"))

    def test_describe_execution_nonexistent(self, stepfunctions_client):
        with pytest.raises(ClientError) as exc_info:
            stepfunctions_client.describe_execution(
                executionArn="arn:aws:states:us-east-1:000000000000:execution:NonExistent_xyz_12345:abc123"
            )
        assert_client_error(exc_info, ("ExecutionDoesNotExist", "ResourceNotFound"))

    def test_delete_state_machine_nonexistent(self, stepfunctions_client):
        with pytest.raises(ClientError):
            stepfunctions_client.delete_state_machine(
                stateMachineArn="arn:aws:states:us-east-1:000000000000:stateMachine:nonexistent-fake-arn"
            )

    def test_describe_activity_nonexistent(self, stepfunctions_client):
        with pytest.raises(ClientError):
            stepfunctions_client.describe_activity(
                activityArn="arn:aws:states:us-east-1:000000000000:activity:nonexistent-fake-arn"
            )

    def test_update_state_machine_verify_definition(
        self, stepfunctions_client, iam_client, unique_name, region
    ):
        sm_name = unique_name("PyVerifySM")
        verify_role = unique_name("PyVerifyRole")
        verify_arn = f"arn:aws:iam::000000000000:role/{verify_role}"
        try:
            iam_client.create_role(
                RoleName=verify_role,
                AssumeRolePolicyDocument=json.dumps(SFN_TRUST_POLICY),
            )
            def1 = '{"Comment":"v1","StartAt":"A","States":{"A":{"Type":"Pass","End":true}}}'
            resp = stepfunctions_client.create_state_machine(
                name=sm_name, definition=def1, roleArn=verify_arn
            )
            verify_arn_sm = resp["stateMachineArn"]

            def2 = '{"Comment":"v2","StartAt":"B","States":{"B":{"Type":"Pass","Result":"hello","End":true}}}'
            stepfunctions_client.update_state_machine(
                stateMachineArn=verify_arn_sm, definition=def2
            )
            desc_resp = stepfunctions_client.describe_state_machine(
                stateMachineArn=verify_arn_sm
            )
            assert desc_resp["definition"] == def2
        finally:
            try:
                stepfunctions_client.delete_state_machine(
                    stateMachineArn=f"arn:aws:states:{region}:000000000000:stateMachine:{sm_name}"
                )
            except Exception:
                pass
            try:
                iam_client.delete_role(RoleName=verify_role)
            except Exception:
                pass
