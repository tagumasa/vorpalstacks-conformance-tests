import time
from ..runner import TestRunner, TestResult


async def run_route53_tests(
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
    client = session.client("route53", endpoint_url=endpoint, region_name=region)

    domain_name = f"example-{int(time.time() * 1000)}.com."
    hosted_zone_id = ""

    def _list_hosted_zones():
        client.list_hosted_zones(MaxItems="10")

    results.append(
        await runner.run_test("route53", "ListHostedZones", _list_hosted_zones)
    )

    def _create_hosted_zone():
        nonlocal hosted_zone_id
        resp = client.create_hosted_zone(
            Name=domain_name,
            CallerReference=f"ref-{int(time.time() * 1000)}",
        )
        if resp.get("HostedZone", {}).get("Id"):
            hosted_zone_id = resp["HostedZone"]["Id"]

    results.append(
        await runner.run_test("route53", "CreateHostedZone", _create_hosted_zone)
    )

    if hosted_zone_id:

        def _get_hosted_zone():
            client.get_hosted_zone(Id=hosted_zone_id)

        results.append(
            await runner.run_test("route53", "GetHostedZone", _get_hosted_zone)
        )

        def _list_resource_record_sets():
            client.list_resource_record_sets(HostedZoneId=hosted_zone_id, MaxItems="10")

        results.append(
            await runner.run_test(
                "route53", "ListResourceRecordSets", _list_resource_record_sets
            )
        )

        change_id = ""

        def _change_resource_record_sets_create():
            nonlocal change_id
            resp = client.change_resource_record_sets(
                HostedZoneId=hosted_zone_id,
                ChangeBatch={
                    "Changes": [
                        {
                            "Action": "CREATE",
                            "ResourceRecordSet": {
                                "Name": f"test.{domain_name}",
                                "Type": "A",
                                "TTL": 300,
                                "ResourceRecords": [{"Value": "192.0.2.1"}],
                            },
                        },
                    ],
                },
            )
            if resp.get("ChangeInfo", {}).get("Id"):
                change_id = resp["ChangeInfo"]["Id"]

        results.append(
            await runner.run_test(
                "route53",
                "ChangeResourceRecordSets_Create",
                _change_resource_record_sets_create,
            )
        )

        if change_id:

            def _get_change():
                client.get_change(Id=change_id)

            results.append(await runner.run_test("route53", "GetChange", _get_change))

        def _change_resource_record_sets_delete():
            client.change_resource_record_sets(
                HostedZoneId=hosted_zone_id,
                ChangeBatch={
                    "Changes": [
                        {
                            "Action": "DELETE",
                            "ResourceRecordSet": {
                                "Name": f"test.{domain_name}",
                                "Type": "A",
                                "TTL": 300,
                                "ResourceRecords": [{"Value": "192.0.2.1"}],
                            },
                        },
                    ],
                },
            )

        results.append(
            await runner.run_test(
                "route53",
                "ChangeResourceRecordSets_Delete",
                _change_resource_record_sets_delete,
            )
        )

        def _get_dnssec():
            client.get_dnssec(HostedZoneId=hosted_zone_id)

        results.append(await runner.run_test("route53", "GetDNSSEC", _get_dnssec))

        def _delete_hosted_zone():
            client.delete_hosted_zone(Id=hosted_zone_id)

        results.append(
            await runner.run_test("route53", "DeleteHostedZone", _delete_hosted_zone)
        )

    def _list_reusable_delegation_sets():
        client.list_reusable_delegation_sets(MaxItems="10")

    results.append(
        await runner.run_test(
            "route53", "ListReusableDelegationSets", _list_reusable_delegation_sets
        )
    )

    def _get_hosted_zone_nonexistent():
        try:
            client.get_hosted_zone(Id="Z00000000000000000000")
            raise Exception("expected error for non-existent hosted zone")
        except Exception as e:
            if str(e) == "expected error for non-existent hosted zone":
                raise

    results.append(
        await runner.run_test(
            "route53", "GetHostedZone_NonExistent", _get_hosted_zone_nonexistent
        )
    )

    def _delete_hosted_zone_nonexistent():
        try:
            client.delete_hosted_zone(Id="Z00000000000000000000")
            raise Exception("expected error for non-existent hosted zone")
        except Exception as e:
            if str(e) == "expected error for non-existent hosted zone":
                raise

    results.append(
        await runner.run_test(
            "route53", "DeleteHostedZone_NonExistent", _delete_hosted_zone_nonexistent
        )
    )

    def _get_change_nonexistent():
        try:
            client.get_change(Id="C0000000000000000000000000")
            raise Exception("expected error for non-existent change")
        except Exception as e:
            if str(e) == "expected error for non-existent change":
                raise

    results.append(
        await runner.run_test(
            "route53", "GetChange_NonExistent", _get_change_nonexistent
        )
    )

    def _create_hosted_zone_content_verify():
        verify_domain = f"verify-{int(time.time() * 1000)}.com."
        verify_ref = f"ref-{int(time.time() * 1000)}"
        resp = client.create_hosted_zone(
            Name=verify_domain,
            CallerReference=verify_ref,
        )
        hz_id = resp.get("HostedZone", {}).get("Id")
        if not hz_id:
            raise Exception("hosted zone id is nil")

        try:
            if resp.get("HostedZone", {}).get("Name") != verify_domain:
                raise Exception(
                    f"domain name mismatch: got {resp.get('HostedZone', {}).get('Name')}, "
                    f"want {verify_domain}"
                )

            get_resp = client.get_hosted_zone(Id=hz_id)
            if get_resp.get("HostedZone", {}).get("Name") != verify_domain:
                raise Exception("get domain name mismatch")
        finally:
            client.delete_hosted_zone(Id=hz_id)

    results.append(
        await runner.run_test(
            "route53",
            "CreateHostedZone_ContentVerify",
            _create_hosted_zone_content_verify,
        )
    )

    return results
