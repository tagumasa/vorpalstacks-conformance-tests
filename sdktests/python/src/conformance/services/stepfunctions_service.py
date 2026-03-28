import json
import time
import uuid
from botocore.exceptions import ClientError
from ..runner import TestRunner, TestResult


def _make_unique_name(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"


async def run_stepfunctions_tests(
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
    sfn_client = session.client(
        "stepfunctions", endpoint_url=endpoint, region_name=region
    )
    iam_client = session.client("iam", endpoint_url=endpoint, region_name=region)

    state_machine_name = _make_unique_name("PyStateMachine")
    definition = json.dumps(
        {
            "Comment": "Test state machine",
            "StartAt": "Pass",
            "States": {
                "Pass": {"Type": "Pass", "End": True},
            },
        }
    )
    sfn_role_name = _make_unique_name("PySfnRole")
    sfn_trust_policy = {
        "Version": "2012-10-17",
        "Statement": [
            {
                "Effect": "Allow",
                "Principal": {"Service": ["states.amazonaws.com"]},
                "Action": "sts:AssumeRole",
            }
        ],
    }
    try:
        iam_client.create_role(
            RoleName=sfn_role_name,
            AssumeRolePolicyDocument=json.dumps(sfn_trust_policy),
        )
    except Exception:
        pass
    sfn_role_arn = f"arn:aws:iam::000000000000:role/{sfn_role_name}"
    state_machine_arn = ""
    execution_arn = ""
    activity_arn = ""
    activity_name = _make_unique_name("PyActivity")

    try:

        def _create_state_machine():
            nonlocal state_machine_arn
            resp = sfn_client.create_state_machine(
                name=state_machine_name,
                definition=definition,
                roleArn=sfn_role_arn,
            )
            assert resp.get("stateMachineArn"), "stateMachineArn is null"
            state_machine_arn = resp["stateMachineArn"]

        results.append(
            await runner.run_test("sfn", "CreateStateMachine", _create_state_machine)
        )

        def _describe_state_machine():
            resp = sfn_client.describe_state_machine(stateMachineArn=state_machine_arn)
            assert resp.get("stateMachineArn"), "stateMachineArn is null"
            assert resp.get("definition"), "definition is null"

        results.append(
            await runner.run_test(
                "sfn", "DescribeStateMachine", _describe_state_machine
            )
        )

        def _list_state_machines():
            resp = sfn_client.list_state_machines()
            assert resp.get("stateMachines") is not None
            found = any(
                sm["stateMachineArn"] == state_machine_arn
                for sm in resp["stateMachines"]
            )
            assert found, "Created state machine not found in list"

        results.append(
            await runner.run_test("sfn", "ListStateMachines", _list_state_machines)
        )

        def _start_execution():
            nonlocal execution_arn
            resp = sfn_client.start_execution(
                stateMachineArn=state_machine_arn,
                input=json.dumps({"key": "value"}),
                name=_make_unique_name("PyExecution"),
            )
            assert resp.get("executionArn"), "executionArn is null"
            execution_arn = resp["executionArn"]

        results.append(await runner.run_test("sfn", "StartExecution", _start_execution))

        def _describe_execution():
            resp = sfn_client.describe_execution(executionArn=execution_arn)
            assert resp.get("executionArn"), "executionArn is null"
            assert resp.get("status"), "status is null"

        results.append(
            await runner.run_test("sfn", "DescribeExecution", _describe_execution)
        )

        def _list_executions():
            resp = sfn_client.list_executions(stateMachineArn=state_machine_arn)
            assert resp.get("executions") is not None

        results.append(await runner.run_test("sfn", "ListExecutions", _list_executions))

        def _get_execution_history():
            resp = sfn_client.get_execution_history(executionArn=execution_arn)
            assert resp.get("events") is not None

        results.append(
            await runner.run_test("sfn", "GetExecutionHistory", _get_execution_history)
        )

        def _stop_execution():
            sfn_client.stop_execution(
                executionArn=execution_arn,
                error="TestError",
                cause="Test cause for stopping",
            )

        results.append(await runner.run_test("sfn", "StopExecution", _stop_execution))

        def _update_state_machine():
            sfn_client.update_state_machine(
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

        results.append(
            await runner.run_test("sfn", "UpdateStateMachine", _update_state_machine)
        )

        def _create_activity():
            nonlocal activity_arn
            resp = sfn_client.create_activity(name=activity_name)
            assert resp.get("activityArn"), "activityArn is null"
            activity_arn = resp["activityArn"]

        results.append(await runner.run_test("sfn", "CreateActivity", _create_activity))

        def _describe_activity():
            resp = sfn_client.describe_activity(activityArn=activity_arn)
            assert resp.get("name"), "activity name is nil"

        results.append(
            await runner.run_test("sfn", "DescribeActivity", _describe_activity)
        )

        def _list_activities():
            resp = sfn_client.list_activities()
            assert resp.get("activities") is not None

        results.append(await runner.run_test("sfn", "ListActivities", _list_activities))

        def _tag_resource():
            sfn_client.tag_resource(
                resourceArn=state_machine_arn,
                tags=[{"key": "Environment", "value": "test"}],
            )

        results.append(await runner.run_test("sfn", "TagResource", _tag_resource))

        def _list_tags_for_resource():
            resp = sfn_client.list_tags_for_resource(resourceArn=state_machine_arn)
            assert resp.get("tags") is not None

        results.append(
            await runner.run_test("sfn", "ListTagsForResource", _list_tags_for_resource)
        )

        def _untag_resource():
            sfn_client.untag_resource(
                resourceArn=state_machine_arn, tagKeys=["Environment"]
            )

        results.append(await runner.run_test("sfn", "UntagResource", _untag_resource))

        def _delete_activity():
            sfn_client.delete_activity(activityArn=activity_arn)

        results.append(await runner.run_test("sfn", "DeleteActivity", _delete_activity))

        def _delete_state_machine():
            sfn_client.delete_state_machine(stateMachineArn=state_machine_arn)

        results.append(
            await runner.run_test("sfn", "DeleteStateMachine", _delete_state_machine)
        )

    finally:
        try:
            sfn_client.delete_state_machine(stateMachineArn=state_machine_arn)
        except Exception:
            pass
        try:
            sfn_client.delete_activity(activityArn=activity_arn)
        except Exception:
            pass
        try:
            iam_client.delete_role(RoleName=sfn_role_name)
        except Exception:
            pass

    def _describe_state_machine_nonexistent():
        try:
            sfn_client.describe_state_machine(
                stateMachineArn="arn:aws:states:us-east-1:000000000000:stateMachine:NonExistent_xyz_12345"
            )
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] in (
                "StateMachineDoesNotExist",
                "ResourceNotFound",
            )

    results.append(
        await runner.run_test(
            "sfn",
            "DescribeStateMachine_NonExistent",
            _describe_state_machine_nonexistent,
        )
    )

    def _start_execution_nonexistent():
        try:
            sfn_client.start_execution(
                stateMachineArn="arn:aws:states:us-east-1:000000000000:stateMachine:NonExistent_xyz_12345"
            )
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] in (
                "StateMachineDoesNotExist",
                "ResourceNotFound",
            )

    results.append(
        await runner.run_test(
            "sfn", "StartExecution_NonExistent", _start_execution_nonexistent
        )
    )

    def _describe_execution_nonexistent():
        try:
            sfn_client.describe_execution(
                executionArn="arn:aws:states:us-east-1:000000000000:execution:NonExistent_xyz_12345:abc123"
            )
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] in (
                "ExecutionDoesNotExist",
                "ResourceNotFound",
            )

    results.append(
        await runner.run_test(
            "sfn",
            "DescribeExecution_NonExistent",
            _describe_execution_nonexistent,
        )
    )

    def _delete_state_machine_nonexistent():
        try:
            sfn_client.delete_state_machine(
                stateMachineArn="arn:aws:states:us-east-1:000000000000:stateMachine:nonexistent-fake-arn"
            )
            raise AssertionError("Expected ClientError but got none")
        except ClientError:
            pass

    results.append(
        await runner.run_test(
            "sfn",
            "DeleteStateMachine_NonExistent",
            _delete_state_machine_nonexistent,
        )
    )

    def _describe_activity_nonexistent():
        try:
            sfn_client.describe_activity(
                activityArn="arn:aws:states:us-east-1:000000000000:activity:nonexistent-fake-arn"
            )
            raise AssertionError("Expected ClientError but got none")
        except ClientError:
            pass

    results.append(
        await runner.run_test(
            "sfn",
            "DescribeActivity_NonExistent",
            _describe_activity_nonexistent,
        )
    )

    def _update_state_machine_verify_definition():
        sm_name = _make_unique_name("PyVerifySM")
        verify_role = _make_unique_name("PyVerifyRole")
        verify_arn = f"arn:aws:iam::000000000000:role/{verify_role}"
        try:
            iam_client.create_role(
                RoleName=verify_role,
                AssumeRolePolicyDocument=json.dumps(sfn_trust_policy),
            )
            def1 = '{"Comment":"v1","StartAt":"A","States":{"A":{"Type":"Pass","End":true}}}'
            resp = sfn_client.create_state_machine(
                name=sm_name, definition=def1, roleArn=verify_arn
            )
            verify_arn_sm = resp["stateMachineArn"]

            def2 = '{"Comment":"v2","StartAt":"B","States":{"B":{"Type":"Pass","Result":"hello","End":true}}}'
            sfn_client.update_state_machine(
                stateMachineArn=verify_arn_sm, definition=def2
            )
            desc_resp = sfn_client.describe_state_machine(stateMachineArn=verify_arn_sm)
            assert desc_resp["definition"] == def2, "definition not updated"
        finally:
            try:
                sfn_client.delete_state_machine(
                    stateMachineArn=f"arn:aws:states:{region}:000000000000:stateMachine:{sm_name}"
                )
            except Exception:
                pass
            try:
                iam_client.delete_role(RoleName=verify_role)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "sfn",
            "UpdateStateMachine_VerifyDefinition",
            _update_state_machine_verify_definition,
        )
    )

    return results
