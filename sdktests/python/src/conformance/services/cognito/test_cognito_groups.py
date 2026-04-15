import pytest


@pytest.fixture(scope="module")
def group_pool(cognito_client, unique_name):
    pool_name = unique_name("PyGrpPool")
    resp = cognito_client.create_user_pool(PoolName=pool_name)
    pool_id = resp["UserPool"]["Id"]
    yield pool_id
    try:
        cognito_client.delete_user_pool(UserPoolId=pool_id)
    except Exception:
        pass


class TestCreateGroup:
    def test_create_group(self, cognito_client, group_pool, unique_name):
        group_name = unique_name("PyGroup")
        resp = cognito_client.create_group(GroupName=group_name, UserPoolId=group_pool)
        assert resp["Group"]["GroupName"] == group_name


class TestListGroups:
    def test_list_groups(self, cognito_client, group_pool):
        resp = cognito_client.list_groups(UserPoolId=group_pool)
        assert len(resp.get("Groups", [])) > 0

    def test_list_groups_contains_created(self, cognito_client, unique_name):
        pool_name = unique_name("PyGrpPool")
        resp = cognito_client.create_user_pool(PoolName=pool_name)
        pool_id = resp["UserPool"]["Id"]
        try:
            test_grp = unique_name("test-grp")
            cognito_client.create_group(
                GroupName=test_grp,
                UserPoolId=pool_id,
                Description="Test group description",
            )
            list_resp = cognito_client.list_groups(UserPoolId=pool_id)
            found = False
            for g in list_resp["Groups"]:
                if g["GroupName"] == test_grp:
                    found = True
                    assert g.get("Description") == "Test group description"
                    break
            assert found, "created group not found in ListGroups"
        finally:
            try:
                cognito_client.delete_user_pool(UserPoolId=pool_id)
            except Exception:
                pass


class TestDeleteGroup:
    def test_delete_group(self, cognito_client, group_pool, unique_name):
        group_name = unique_name("PyGroupDel")
        cognito_client.create_group(GroupName=group_name, UserPoolId=group_pool)
        cognito_client.delete_group(GroupName=group_name, UserPoolId=group_pool)
