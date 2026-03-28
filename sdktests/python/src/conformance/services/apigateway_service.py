import time
import uuid
from botocore.exceptions import ClientError
from ..runner import TestRunner, TestResult


def _make_unique_name(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"


async def run_apigateway_tests(
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
    apigateway_client = session.client(
        "apigateway", endpoint_url=endpoint, region_name=region
    )

    api_name = _make_unique_name("TestAPI")
    api_id = ""
    deployment_id = ""

    def _create_rest_api():
        nonlocal api_id
        resp = apigateway_client.create_rest_api(name=api_name, description="Test API")
        api_id = resp.get("id", "")

    results.append(
        await runner.run_test("apigateway", "CreateRestApi", _create_rest_api)
    )

    def _get_rest_apis():
        nonlocal api_id
        resp = apigateway_client.get_rest_apis(limit=100)
        assert resp.get("items") is not None
        for item in resp.get("items", []):
            if item.get("name") == api_name:
                api_id = item.get("id", "")
                break
        if not api_id:
            raise AssertionError("API not found")

    results.append(await runner.run_test("apigateway", "GetRestApis", _get_rest_apis))

    def _get_rest_api():
        assert api_id, "API ID not available"
        resp = apigateway_client.get_rest_api(restApiId=api_id)
        assert resp.get("name") is not None

    results.append(await runner.run_test("apigateway", "GetRestApi", _get_rest_api))

    def _update_rest_api():
        assert api_id, "API ID not available"
        apigateway_client.update_rest_api(
            restApiId=api_id,
            patchOperations=[
                {"op": "replace", "path": "/description", "value": "Updated API"}
            ],
        )

    results.append(
        await runner.run_test("apigateway", "UpdateRestApi", _update_rest_api)
    )

    def _create_resource():
        assert api_id, "API ID not available"
        resp = apigateway_client.create_resource(
            restApiId=api_id, parentId=api_id, pathPart="test"
        )
        assert resp.get("id") is not None

    results.append(
        await runner.run_test("apigateway", "CreateResource", _create_resource)
    )

    def _get_resources():
        assert api_id, "API ID not available"
        resp = apigateway_client.get_resources(restApiId=api_id)
        assert resp.get("items") is not None

    results.append(await runner.run_test("apigateway", "GetResources", _get_resources))

    def _create_deployment():
        nonlocal deployment_id
        assert api_id, "API ID not available"
        resp = apigateway_client.create_deployment(restApiId=api_id)
        deployment_id = resp.get("id", "")

    results.append(
        await runner.run_test("apigateway", "CreateDeployment", _create_deployment)
    )

    def _get_deployments():
        assert api_id, "API ID not available"
        resp = apigateway_client.get_deployments(restApiId=api_id)
        assert resp.get("items") is not None

    results.append(
        await runner.run_test("apigateway", "GetDeployments", _get_deployments)
    )

    def _create_stage():
        assert api_id, "API ID not available"
        assert deployment_id, "Deployment ID not available"
        apigateway_client.create_stage(
            restApiId=api_id,
            stageName="test",
            deploymentId=deployment_id,
        )

    results.append(await runner.run_test("apigateway", "CreateStage", _create_stage))

    def _get_stage():
        assert api_id, "API ID not available"
        resp = apigateway_client.get_stage(restApiId=api_id, stageName="test")
        assert resp.get("stageName") is not None

    results.append(await runner.run_test("apigateway", "GetStage", _get_stage))

    def _get_stages():
        assert api_id, "API ID not available"
        resp = apigateway_client.get_stages(restApiId=api_id)
        assert resp.get("item") is not None

    results.append(await runner.run_test("apigateway", "GetStages", _get_stages))

    def _delete_rest_api():
        assert api_id, "API ID not available"
        apigateway_client.delete_rest_api(restApiId=api_id)

    results.append(
        await runner.run_test("apigateway", "DeleteRestApi", _delete_rest_api)
    )

    def _get_rest_api_nonexistent():
        try:
            apigateway_client.get_rest_api(restApiId="nonexistent_xyz")
            raise AssertionError("Expected NotFoundException but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "NotFoundException"

    results.append(
        await runner.run_test(
            "apigateway", "GetRestApi_NonExistent", _get_rest_api_nonexistent
        )
    )

    def _delete_rest_api_nonexistent():
        try:
            apigateway_client.delete_rest_api(restApiId="nonexistent_xyz")
            raise AssertionError("Expected NotFoundException but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "NotFoundException"

    results.append(
        await runner.run_test(
            "apigateway", "DeleteRestApi_NonExistent", _delete_rest_api_nonexistent
        )
    )

    tmp_api_name = _make_unique_name("TmpAPI")

    def _get_stage_nonexistent():
        nonlocal tmp_api_name
        tmp_api_id = ""
        try:
            create_resp = apigateway_client.create_rest_api(name=tmp_api_name)
            tmp_api_id = create_resp.get("id", "")
            apigateway_client.get_stage(
                restApiId=tmp_api_id, stageName="nonexistent_stage"
            )
            raise AssertionError("Expected NotFoundException but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "NotFoundException"
        finally:
            if tmp_api_id:
                try:
                    apigateway_client.delete_rest_api(restApiId=tmp_api_id)
                except Exception:
                    pass

    results.append(
        await runner.run_test(
            "apigateway", "GetStage_NonExistent", _get_stage_nonexistent
        )
    )

    ua_api_name = _make_unique_name("UaAPI")

    def _update_rest_api_verify_update():
        nonlocal ua_api_name
        ua_api_id = ""
        new_desc = "updated description v2"
        try:
            create_resp = apigateway_client.create_rest_api(
                name=ua_api_name, description="original desc"
            )
            ua_api_id = create_resp.get("id", "")
            apigateway_client.update_rest_api(
                restApiId=ua_api_id,
                patchOperations=[
                    {"op": "replace", "path": "/description", "value": new_desc}
                ],
            )
            resp = apigateway_client.get_rest_api(restApiId=ua_api_id)
            assert resp.get("description") == new_desc, (
                f"description not updated, got {resp.get('description')}"
            )
        finally:
            if ua_api_id:
                try:
                    apigateway_client.delete_rest_api(restApiId=ua_api_id)
                except Exception:
                    pass

    results.append(
        await runner.run_test(
            "apigateway", "UpdateRestApi_VerifyUpdate", _update_rest_api_verify_update
        )
    )

    cr_api_name = _make_unique_name("CrAPI")

    def _create_resource_nested_path():
        nonlocal cr_api_name
        cr_api_id = ""
        try:
            create_resp = apigateway_client.create_rest_api(name=cr_api_name)
            cr_api_id = create_resp.get("id", "")
            users_resp = apigateway_client.create_resource(
                restApiId=cr_api_id, parentId=cr_api_id, pathPart="users"
            )
            user_id_resp = apigateway_client.create_resource(
                restApiId=cr_api_id, parentId=users_resp.get("id"), pathPart="{userId}"
            )
            assert user_id_resp.get("path") == "/users/{userId}", (
                f"nested path mismatch, got {user_id_resp.get('path')}"
            )
            res_resp = apigateway_client.get_resources(restApiId=cr_api_id)
            assert len(res_resp.get("items", [])) >= 3, (
                f"expected at least 3 resources, got {len(res_resp.get('items', []))}"
            )
        finally:
            if cr_api_id:
                try:
                    apigateway_client.delete_rest_api(restApiId=cr_api_id)
                except Exception:
                    pass

    results.append(
        await runner.run_test(
            "apigateway", "CreateResource_NestedPath", _create_resource_nested_path
        )
    )

    cs_api_name = _make_unique_name("CsAPI")

    def _create_stage_verify_config():
        nonlocal cs_api_name
        cs_api_id = ""
        cs_deployment_id = ""
        stage_desc = "test stage description"
        try:
            create_resp = apigateway_client.create_rest_api(name=cs_api_name)
            cs_api_id = create_resp.get("id", "")
            dep_resp = apigateway_client.create_deployment(restApiId=cs_api_id)
            cs_deployment_id = dep_resp.get("id", "")
            apigateway_client.create_stage(
                restApiId=cs_api_id,
                stageName="v1",
                deploymentId=cs_deployment_id,
                description=stage_desc,
            )
            resp = apigateway_client.get_stage(restApiId=cs_api_id, stageName="v1")
            assert resp.get("description") == stage_desc, (
                f"stage description mismatch, got {resp.get('description')}"
            )
            assert resp.get("deploymentId") == cs_deployment_id, (
                f"deployment ID mismatch, got {resp.get('deploymentId')}"
            )
        finally:
            if cs_api_id:
                try:
                    apigateway_client.delete_rest_api(restApiId=cs_api_id)
                except Exception:
                    pass

    results.append(
        await runner.run_test(
            "apigateway", "CreateStage_VerifyConfig", _create_stage_verify_config
        )
    )

    ga_api_name = _make_unique_name("GaAPI")

    def _get_rest_apis_contains_created():
        nonlocal ga_api_name
        ga_api_id = ""
        ga_desc = "searchable description"
        try:
            create_resp = apigateway_client.create_rest_api(
                name=ga_api_name, description=ga_desc
            )
            ga_api_id = create_resp.get("id", "")
            resp = apigateway_client.get_rest_apis(limit=500)
            found = False
            for item in resp.get("items", []):
                if item.get("name") == ga_api_name:
                    found = True
                    assert item.get("description") == ga_desc, (
                        f"description mismatch in list, got {item.get('description')}"
                    )
                    break
            assert found, f"created API {ga_api_name} not found in GetRestApis"
        finally:
            if ga_api_id:
                try:
                    apigateway_client.delete_rest_api(restApiId=ga_api_id)
                except Exception:
                    pass

    results.append(
        await runner.run_test(
            "apigateway", "GetRestApis_ContainsCreated", _get_rest_apis_contains_created
        )
    )

    return results
