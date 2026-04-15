import pytest


class TestCreateGroup:
    def test_create_group(self, iam_resources):
        assert iam_resources["group_arn"]


class TestGetGroup:
    def test_get_group(self, iam_client, iam_resources):
        resp = iam_client.get_group(GroupName=iam_resources["group_name"])
        assert resp["Group"]
        assert resp["Group"]["Arn"]


class TestListGroups:
    def test_list_groups(self, iam_client, iam_resources):
        resp = iam_client.list_groups()
        assert resp["Groups"] is not None
        found = any(
            g["GroupName"] == iam_resources["group_name"] for g in resp["Groups"]
        )
        assert found, "Created group not found in list"


class TestAddUserToGroup:
    def test_add_user_to_group(self, iam_client, iam_resources):
        iam_client.add_user_to_group(
            GroupName=iam_resources["group_name"],
            UserName=iam_resources["user_name"],
        )


class TestListGroupsForUser:
    def test_list_groups_for_user(self, iam_client, iam_resources):
        try:
            iam_client.add_user_to_group(
                GroupName=iam_resources["group_name"],
                UserName=iam_resources["user_name"],
            )
        except Exception:
            pass
        resp = iam_client.list_groups_for_user(UserName=iam_resources["user_name"])
        assert resp["Groups"] is not None
        found = any(
            g["GroupName"] == iam_resources["group_name"] for g in resp["Groups"]
        )
        assert found, "User not in expected group"


class TestRemoveUserFromGroup:
    def test_remove_user_from_group(self, iam_client, iam_resources):
        try:
            iam_client.add_user_to_group(
                GroupName=iam_resources["group_name"],
                UserName=iam_resources["user_name"],
            )
        except Exception:
            pass
        iam_client.remove_user_from_group(
            GroupName=iam_resources["group_name"],
            UserName=iam_resources["user_name"],
        )


class TestDeleteGroup:
    def test_delete_group(self, iam_client, unique_name):
        group_name = unique_name("PyDelGroup")
        iam_client.create_group(GroupName=group_name)
        iam_client.delete_group(GroupName=group_name)
