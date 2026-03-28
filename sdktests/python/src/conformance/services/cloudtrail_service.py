import time
from ..runner import TestRunner, TestResult


async def run_cloudtrail_tests(
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
    client = session.client("cloudtrail", endpoint_url=endpoint, region_name=region)

    trail_name = f"test-trail-{int(time.time() * 1000)}"

    def _list_trails():
        client.list_trails()

    results.append(await runner.run_test("cloudtrail", "ListTrails", _list_trails))

    def _create_trail():
        client.create_trail(
            Name=trail_name,
            S3BucketName="test-bucket",
            IncludeGlobalServiceEvents=True,
            IsMultiRegionTrail=True,
        )

    results.append(await runner.run_test("cloudtrail", "CreateTrail", _create_trail))

    def _get_trail():
        client.get_trail(Name=trail_name)

    results.append(await runner.run_test("cloudtrail", "GetTrail", _get_trail))

    def _describe_trails():
        client.describe_trails(trailNameList=[trail_name])

    results.append(
        await runner.run_test("cloudtrail", "DescribeTrails", _describe_trails)
    )

    def _start_logging():
        client.start_logging(Name=trail_name)

    results.append(await runner.run_test("cloudtrail", "StartLogging", _start_logging))

    def _stop_logging():
        client.stop_logging(Name=trail_name)

    results.append(await runner.run_test("cloudtrail", "StopLogging", _stop_logging))

    def _get_trail_status():
        client.get_trail_status(Name=trail_name)

    results.append(
        await runner.run_test("cloudtrail", "GetTrailStatus", _get_trail_status)
    )

    def _update_trail():
        client.update_trail(
            Name=trail_name,
            S3BucketName="updated-bucket",
        )

    results.append(await runner.run_test("cloudtrail", "UpdateTrail", _update_trail))

    def _get_event_selectors():
        client.get_event_selectors(TrailName=trail_name)

    results.append(
        await runner.run_test("cloudtrail", "GetEventSelectors", _get_event_selectors)
    )

    def _put_event_selectors():
        client.put_event_selectors(
            TrailName=trail_name,
            EventSelectors=[
                {
                    "ReadWriteType": "All",
                    "IncludeManagementEvents": True,
                },
            ],
        )

    results.append(
        await runner.run_test("cloudtrail", "PutEventSelectors", _put_event_selectors)
    )

    get_trail_resp = client.get_trail(Name=trail_name)
    trail_arn = (
        get_trail_resp.get("Trail", {}).get("TrailARN", "")
        if get_trail_resp.get("Trail")
        else ""
    )
    resource_id = trail_arn if trail_arn else trail_name

    def _add_tags():
        client.add_tags(
            ResourceId=resource_id,
            TagsList=[
                {"Key": "Environment", "Value": "test"},
                {"Key": "Owner", "Value": "test-user"},
            ],
        )

    results.append(await runner.run_test("cloudtrail", "AddTags", _add_tags))

    def _list_tags():
        client.list_tags(ResourceIdList=[resource_id])

    results.append(await runner.run_test("cloudtrail", "ListTags", _list_tags))

    def _remove_tags():
        client.remove_tags(
            ResourceId=resource_id,
            TagsList=[{"Key": "Environment"}],
        )

    results.append(await runner.run_test("cloudtrail", "RemoveTags", _remove_tags))

    def _lookup_events():
        client.lookup_events(MaxResults=10)

    results.append(await runner.run_test("cloudtrail", "LookupEvents", _lookup_events))

    def _delete_trail():
        client.delete_trail(Name=trail_name)

    results.append(await runner.run_test("cloudtrail", "DeleteTrail", _delete_trail))

    def _get_trail_nonexistent():
        try:
            client.get_trail(Name="nonexistent-trail-xyz")
            raise Exception("expected error for non-existent trail")
        except Exception as e:
            if str(e) == "expected error for non-existent trail":
                raise

    results.append(
        await runner.run_test(
            "cloudtrail", "GetTrail_NonExistent", _get_trail_nonexistent
        )
    )

    def _delete_trail_nonexistent():
        try:
            client.delete_trail(Name="nonexistent-trail-xyz")
            raise Exception("expected error for non-existent trail")
        except Exception as e:
            if str(e) == "expected error for non-existent trail":
                raise

    results.append(
        await runner.run_test(
            "cloudtrail", "DeleteTrail_NonExistent", _delete_trail_nonexistent
        )
    )

    def _start_logging_nonexistent():
        try:
            client.start_logging(Name="nonexistent-trail-xyz")
            raise Exception("expected error for non-existent trail")
        except Exception as e:
            if str(e) == "expected error for non-existent trail":
                raise

    results.append(
        await runner.run_test(
            "cloudtrail", "StartLogging_NonExistent", _start_logging_nonexistent
        )
    )

    def _describe_trails_nonexistent():
        resp = client.describe_trails(trailNameList=["nonexistent-trail-xyz"])
        if resp.get("TrailList") and len(resp["TrailList"]) != 0:
            raise Exception(f"expected empty trail list, got {len(resp['TrailList'])}")

    results.append(
        await runner.run_test(
            "cloudtrail", "DescribeTrails_NonExistent", _describe_trails_nonexistent
        )
    )

    verify_trail_name = f"verify-trail-{int(time.time() * 1000)}"

    def _create_trail_content_verify():
        resp = client.create_trail(
            Name=verify_trail_name,
            S3BucketName="verify-bucket",
            IncludeGlobalServiceEvents=True,
            IsMultiRegionTrail=False,
        )
        if resp.get("Name") != verify_trail_name:
            raise Exception("trail name mismatch")
        if resp.get("S3BucketName") != "verify-bucket":
            raise Exception("S3 bucket name mismatch")

    results.append(
        await runner.run_test(
            "cloudtrail", "CreateTrail_ContentVerify", _create_trail_content_verify
        )
    )

    def _update_trail_verify_change():
        client.update_trail(
            Name=verify_trail_name,
            S3BucketName="updated-verify-bucket",
        )
        get_resp = client.get_trail(Name=verify_trail_name)
        if get_resp.get("Trail", {}).get("S3BucketName") != "updated-verify-bucket":
            raise Exception(
                f"S3 bucket name not updated, "
                f"got {get_resp.get('Trail', {}).get('S3BucketName')}"
            )

    results.append(
        await runner.run_test(
            "cloudtrail", "UpdateTrail_VerifyChange", _update_trail_verify_change
        )
    )

    def _put_event_selectors_verify_content():
        client.put_event_selectors(
            TrailName=verify_trail_name,
            EventSelectors=[
                {
                    "ReadWriteType": "ReadOnly",
                    "IncludeManagementEvents": False,
                    "DataResources": [
                        {
                            "Type": "AWS::S3::Object",
                            "Values": ["arn:aws:s3:::"],
                        },
                    ],
                },
            ],
        )
        get_resp = client.get_event_selectors(TrailName=verify_trail_name)
        selectors = get_resp.get("EventSelectors", [])
        if len(selectors) != 1:
            raise Exception(f"expected 1 event selector, got {len(selectors)}")
        if selectors[0].get("ReadWriteType") != "ReadOnly":
            raise Exception(
                f"ReadWriteType mismatch, got {selectors[0].get('ReadWriteType')}"
            )

    results.append(
        await runner.run_test(
            "cloudtrail",
            "PutEventSelectors_VerifyContent",
            _put_event_selectors_verify_content,
        )
    )

    try:
        client.delete_trail(Name=verify_trail_name)
    except Exception:
        pass

    return results
