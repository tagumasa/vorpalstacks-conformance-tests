import pytest
from botocore.exceptions import ClientError

from conformance.conftest import assert_client_error


@pytest.fixture(scope="module")
def api_id(apigateway_client, unique_name):
    name = unique_name("TestAPI")
    resp = apigateway_client.create_rest_api(name=name, description="Test API")
    aid = resp.get("id", "")
    yield aid
    if aid:
        try:
            apigateway_client.delete_rest_api(restApiId=aid)
        except Exception:
            pass


@pytest.fixture(scope="module")
def deployment_id(apigateway_client, api_id):
    resp = apigateway_client.create_deployment(restApiId=api_id)
    return resp.get("id", "")


class TestRestApiLifecycle:
    def test_create_rest_api(self, api_id):
        assert api_id

    def test_get_rest_apis(self, apigateway_client, api_id):
        resp = apigateway_client.get_rest_apis(limit=100)
        assert resp.get("items") is not None
        found = any(item.get("id") == api_id for item in resp.get("items", []))
        assert found, "API not found in list"

    def test_get_rest_api(self, apigateway_client, api_id):
        resp = apigateway_client.get_rest_api(restApiId=api_id)
        assert resp.get("name") is not None

    def test_update_rest_api(self, apigateway_client, api_id):
        apigateway_client.update_rest_api(
            restApiId=api_id,
            patchOperations=[
                {"op": "replace", "path": "/description", "value": "Updated API"}
            ],
        )

    def test_create_resource(self, apigateway_client, api_id):
        resp = apigateway_client.create_resource(
            restApiId=api_id, parentId=api_id, pathPart="test"
        )
        assert resp.get("id") is not None

    def test_get_resources(self, apigateway_client, api_id):
        resp = apigateway_client.get_resources(restApiId=api_id)
        assert resp.get("items") is not None

    def test_delete_rest_api(self, apigateway_client, unique_name):
        del_api_name = unique_name("DelAPI")
        create_resp = apigateway_client.create_rest_api(name=del_api_name)
        del_api_id = create_resp.get("id", "")
        try:
            apigateway_client.delete_rest_api(restApiId=del_api_id)
        except Exception:
            pass


class TestDeployment:
    def test_create_deployment(self, apigateway_client, api_id, deployment_id):
        assert deployment_id

    def test_get_deployments(self, apigateway_client, api_id):
        resp = apigateway_client.get_deployments(restApiId=api_id)
        assert resp.get("items") is not None


class TestStage:
    def test_create_stage(self, apigateway_client, api_id, deployment_id):
        apigateway_client.create_stage(
            restApiId=api_id,
            stageName="test",
            deploymentId=deployment_id,
        )

    def test_get_stage(self, apigateway_client, api_id):
        resp = apigateway_client.get_stage(restApiId=api_id, stageName="test")
        assert resp.get("stageName") is not None

    def test_get_stages(self, apigateway_client, api_id):
        resp = apigateway_client.get_stages(restApiId=api_id)
        assert resp.get("item") is not None


class TestErrorCases:
    def test_get_rest_api_nonexistent(self, apigateway_client):
        with pytest.raises(ClientError) as exc_info:
            apigateway_client.get_rest_api(restApiId="nonexistent_xyz")
        assert_client_error(exc_info, "NotFoundException")

    def test_delete_rest_api_nonexistent(self, apigateway_client):
        with pytest.raises(ClientError) as exc_info:
            apigateway_client.delete_rest_api(restApiId="nonexistent_xyz")
        assert_client_error(exc_info, "NotFoundException")

    def test_get_stage_nonexistent(self, apigateway_client, unique_name):
        tmp_api_name = unique_name("TmpAPI")
        tmp_api_id = ""
        try:
            create_resp = apigateway_client.create_rest_api(name=tmp_api_name)
            tmp_api_id = create_resp.get("id", "")
            with pytest.raises(ClientError) as exc_info:
                apigateway_client.get_stage(
                    restApiId=tmp_api_id, stageName="nonexistent_stage"
                )
            assert_client_error(exc_info, "NotFoundException")
        finally:
            if tmp_api_id:
                try:
                    apigateway_client.delete_rest_api(restApiId=tmp_api_id)
                except Exception:
                    pass


class TestVerification:
    def test_update_rest_api_verify_update(self, apigateway_client, unique_name):
        ua_api_name = unique_name("UaAPI")
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
            assert resp.get("description") == new_desc
        finally:
            if ua_api_id:
                try:
                    apigateway_client.delete_rest_api(restApiId=ua_api_id)
                except Exception:
                    pass

    def test_create_resource_nested_path(self, apigateway_client, unique_name):
        cr_api_name = unique_name("CrAPI")
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
            assert user_id_resp.get("path") == "/users/{userId}"
            res_resp = apigateway_client.get_resources(restApiId=cr_api_id)
            assert len(res_resp.get("items", [])) >= 3
        finally:
            if cr_api_id:
                try:
                    apigateway_client.delete_rest_api(restApiId=cr_api_id)
                except Exception:
                    pass

    def test_create_stage_verify_config(self, apigateway_client, unique_name):
        cs_api_name = unique_name("CsAPI")
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
            assert resp.get("description") == stage_desc
            assert resp.get("deploymentId") == cs_deployment_id
        finally:
            if cs_api_id:
                try:
                    apigateway_client.delete_rest_api(restApiId=cs_api_id)
                except Exception:
                    pass

    def test_get_rest_apis_contains_created(self, apigateway_client, unique_name):
        ga_api_name = unique_name("GaAPI")
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
                    assert item.get("description") == ga_desc
                    break
            assert found
        finally:
            if ga_api_id:
                try:
                    apigateway_client.delete_rest_api(restApiId=ga_api_id)
                except Exception:
                    pass
