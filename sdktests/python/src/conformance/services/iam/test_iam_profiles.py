import pytest


class TestCreateInstanceProfile:
    def test_create_instance_profile(self, iam_client, unique_name):
        profile_name = unique_name("PyProfile")
        resp = iam_client.create_instance_profile(InstanceProfileName=profile_name)
        assert resp.get("InstanceProfile")
        try:
            iam_client.delete_instance_profile(InstanceProfileName=profile_name)
        except Exception:
            pass


class TestGetInstanceProfile:
    def test_get_instance_profile(self, iam_client, unique_name):
        profile_name = unique_name("PyProfile")
        iam_client.create_instance_profile(InstanceProfileName=profile_name)
        try:
            resp = iam_client.get_instance_profile(InstanceProfileName=profile_name)
            assert resp.get("InstanceProfile")
        finally:
            try:
                iam_client.delete_instance_profile(InstanceProfileName=profile_name)
            except Exception:
                pass


class TestListInstanceProfiles:
    def test_list_instance_profiles(self, iam_client):
        resp = iam_client.list_instance_profiles()
        assert resp.get("InstanceProfiles") is not None


class TestAddRoleToInstanceProfile:
    def test_add_role_to_instance_profile(self, iam_client, iam_resources, unique_name):
        profile_name = unique_name("PyProfile")
        iam_client.create_instance_profile(InstanceProfileName=profile_name)
        try:
            iam_client.add_role_to_instance_profile(
                InstanceProfileName=profile_name,
                RoleName=iam_resources["role_name"],
            )
            iam_client.remove_role_from_instance_profile(
                InstanceProfileName=profile_name,
                RoleName=iam_resources["role_name"],
            )
        finally:
            try:
                iam_client.delete_instance_profile(InstanceProfileName=profile_name)
            except Exception:
                pass


class TestRemoveRoleFromInstanceProfile:
    def test_remove_role_from_instance_profile(
        self, iam_client, iam_resources, unique_name
    ):
        profile_name = unique_name("PyProfile")
        iam_client.create_instance_profile(InstanceProfileName=profile_name)
        try:
            iam_client.add_role_to_instance_profile(
                InstanceProfileName=profile_name,
                RoleName=iam_resources["role_name"],
            )
            iam_client.remove_role_from_instance_profile(
                InstanceProfileName=profile_name,
                RoleName=iam_resources["role_name"],
            )
        finally:
            try:
                iam_client.delete_instance_profile(InstanceProfileName=profile_name)
            except Exception:
                pass


class TestDeleteInstanceProfile:
    def test_delete_instance_profile(self, iam_client, unique_name):
        profile_name = unique_name("PyProfile")
        iam_client.create_instance_profile(InstanceProfileName=profile_name)
        iam_client.delete_instance_profile(InstanceProfileName=profile_name)
