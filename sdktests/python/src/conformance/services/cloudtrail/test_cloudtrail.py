import pytest


@pytest.fixture(scope="class")
def trail(cloudtrail_client, unique_name):
    trail_name = unique_name("trail")
    cloudtrail_client.create_trail(
        Name=trail_name,
        S3BucketName="test-bucket",
        IncludeGlobalServiceEvents=True,
        IsMultiRegionTrail=True,
    )
    yield trail_name
    try:
        cloudtrail_client.delete_trail(Name=trail_name)
    except Exception:
        pass


class TestListTrails:
    def test_list_trails(self, cloudtrail_client):
        cloudtrail_client.list_trails()


class TestCreateTrail:
    def test_create_trail(self, cloudtrail_client, trail):
        cloudtrail_client.get_trail(Name=trail)

    def test_content_verify(self, cloudtrail_client, unique_name):
        verify_trail_name = unique_name("vtrail")
        resp = cloudtrail_client.create_trail(
            Name=verify_trail_name,
            S3BucketName="verify-bucket",
            IncludeGlobalServiceEvents=True,
            IsMultiRegionTrail=False,
        )
        assert resp.get("Name") == verify_trail_name, "trail name mismatch"
        assert resp.get("S3BucketName") == "verify-bucket", "S3 bucket name mismatch"
        try:
            cloudtrail_client.delete_trail(Name=verify_trail_name)
        except Exception:
            pass


class TestGetTrail:
    def test_get_trail(self, cloudtrail_client, trail):
        cloudtrail_client.get_trail(Name=trail)

    def test_nonexistent(self, cloudtrail_client):
        with pytest.raises(Exception):
            cloudtrail_client.get_trail(Name="nonexistent-trail-xyz")


class TestDescribeTrails:
    def test_describe_trails(self, cloudtrail_client, trail):
        cloudtrail_client.describe_trails(trailNameList=[trail])

    def test_nonexistent(self, cloudtrail_client):
        resp = cloudtrail_client.describe_trails(
            trailNameList=["nonexistent-trail-xyz"]
        )
        assert not resp.get("TrailList") or len(resp["TrailList"]) == 0, (
            f"expected empty trail list, got {len(resp['TrailList'])}"
        )


class TestLogging:
    def test_start_logging(self, cloudtrail_client, trail):
        cloudtrail_client.start_logging(Name=trail)

    def test_stop_logging(self, cloudtrail_client, trail):
        cloudtrail_client.stop_logging(Name=trail)

    def test_get_trail_status(self, cloudtrail_client, trail):
        cloudtrail_client.get_trail_status(Name=trail)

    def test_start_logging_nonexistent(self, cloudtrail_client):
        with pytest.raises(Exception):
            cloudtrail_client.start_logging(Name="nonexistent-trail-xyz")


class TestUpdateTrail:
    def test_update_trail(self, cloudtrail_client, trail):
        cloudtrail_client.update_trail(Name=trail, S3BucketName="updated-bucket")

    def test_verify_change(self, cloudtrail_client, unique_name):
        verify_trail_name = unique_name("vtrail")
        cloudtrail_client.create_trail(
            Name=verify_trail_name,
            S3BucketName="verify-bucket",
            IncludeGlobalServiceEvents=True,
            IsMultiRegionTrail=False,
        )
        try:
            cloudtrail_client.update_trail(
                Name=verify_trail_name,
                S3BucketName="updated-verify-bucket",
            )
            get_resp = cloudtrail_client.get_trail(Name=verify_trail_name)
            assert (
                get_resp.get("Trail", {}).get("S3BucketName") == "updated-verify-bucket"
            ), (
                f"S3 bucket name not updated, got {get_resp.get('Trail', {}).get('S3BucketName')}"
            )
        finally:
            try:
                cloudtrail_client.delete_trail(Name=verify_trail_name)
            except Exception:
                pass


class TestEventSelectors:
    def test_get_event_selectors(self, cloudtrail_client, trail):
        cloudtrail_client.get_event_selectors(TrailName=trail)

    def test_put_event_selectors(self, cloudtrail_client, trail):
        cloudtrail_client.put_event_selectors(
            TrailName=trail,
            EventSelectors=[
                {"ReadWriteType": "All", "IncludeManagementEvents": True},
            ],
        )

    def test_verify_content(self, cloudtrail_client, unique_name):
        verify_trail_name = unique_name("vtrail")
        cloudtrail_client.create_trail(
            Name=verify_trail_name,
            S3BucketName="verify-bucket",
            IncludeGlobalServiceEvents=True,
            IsMultiRegionTrail=False,
        )
        try:
            cloudtrail_client.put_event_selectors(
                TrailName=verify_trail_name,
                EventSelectors=[
                    {
                        "ReadWriteType": "ReadOnly",
                        "IncludeManagementEvents": False,
                        "DataResources": [
                            {"Type": "AWS::S3::Object", "Values": ["arn:aws:s3:::"]},
                        ],
                    },
                ],
            )
            get_resp = cloudtrail_client.get_event_selectors(
                TrailName=verify_trail_name
            )
            selectors = get_resp.get("EventSelectors", [])
            assert len(selectors) == 1, (
                f"expected 1 event selector, got {len(selectors)}"
            )
            assert selectors[0].get("ReadWriteType") == "ReadOnly", (
                f"ReadWriteType mismatch, got {selectors[0].get('ReadWriteType')}"
            )
        finally:
            try:
                cloudtrail_client.delete_trail(Name=verify_trail_name)
            except Exception:
                pass


class TestTags:
    def test_add_tags(self, cloudtrail_client, trail):
        get_trail_resp = cloudtrail_client.get_trail(Name=trail)
        trail_arn = (
            get_trail_resp.get("Trail", {}).get("TrailARN", "")
            if get_trail_resp.get("Trail")
            else ""
        )
        resource_id = trail_arn if trail_arn else trail
        cloudtrail_client.add_tags(
            ResourceId=resource_id,
            TagsList=[
                {"Key": "Environment", "Value": "test"},
                {"Key": "Owner", "Value": "test-user"},
            ],
        )

    def test_list_tags(self, cloudtrail_client, trail):
        get_trail_resp = cloudtrail_client.get_trail(Name=trail)
        trail_arn = (
            get_trail_resp.get("Trail", {}).get("TrailARN", "")
            if get_trail_resp.get("Trail")
            else ""
        )
        resource_id = trail_arn if trail_arn else trail
        cloudtrail_client.list_tags(ResourceIdList=[resource_id])

    def test_remove_tags(self, cloudtrail_client, trail):
        get_trail_resp = cloudtrail_client.get_trail(Name=trail)
        trail_arn = (
            get_trail_resp.get("Trail", {}).get("TrailARN", "")
            if get_trail_resp.get("Trail")
            else ""
        )
        resource_id = trail_arn if trail_arn else trail
        cloudtrail_client.remove_tags(
            ResourceId=resource_id,
            TagsList=[{"Key": "Environment"}],
        )


class TestLookupEvents:
    def test_lookup_events(self, cloudtrail_client):
        cloudtrail_client.lookup_events(MaxResults=10)


class TestDeleteTrail:
    def test_delete_trail(self, cloudtrail_client, unique_name):
        trail_name = unique_name("trail")
        cloudtrail_client.create_trail(
            Name=trail_name,
            S3BucketName="test-bucket",
            IncludeGlobalServiceEvents=True,
            IsMultiRegionTrail=True,
        )
        cloudtrail_client.delete_trail(Name=trail_name)

    def test_nonexistent(self, cloudtrail_client):
        with pytest.raises(Exception):
            cloudtrail_client.delete_trail(Name="nonexistent-trail-xyz")
