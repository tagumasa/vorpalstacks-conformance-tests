import pytest


@pytest.fixture(scope="class")
def hosted_zone(route53_client, unique_name):
    domain_name = unique_name("hz") + ".com."
    resp = route53_client.create_hosted_zone(
        Name=domain_name,
        CallerReference=unique_name("ref"),
    )
    hz_id = resp.get("HostedZone", {}).get("Id")
    yield hz_id, domain_name
    if hz_id:
        try:
            route53_client.delete_hosted_zone(Id=hz_id)
        except Exception:
            pass


class TestListHostedZones:
    def test_list_hosted_zones(self, route53_client):
        route53_client.list_hosted_zones(MaxItems="10")


class TestCreateHostedZone:
    def test_create_hosted_zone(self, route53_client, unique_name):
        domain_name = unique_name("hz") + ".com."
        resp = route53_client.create_hosted_zone(
            Name=domain_name,
            CallerReference=unique_name("ref"),
        )
        assert resp.get("HostedZone", {}).get("Id"), "hosted zone id is nil"
        route53_client.delete_hosted_zone(Id=resp["HostedZone"]["Id"])

    def test_content_verify(self, route53_client, unique_name):
        verify_domain = unique_name("vz") + ".com."
        verify_ref = unique_name("ref")
        resp = route53_client.create_hosted_zone(
            Name=verify_domain,
            CallerReference=verify_ref,
        )
        hz_id = resp.get("HostedZone", {}).get("Id")
        assert hz_id, "hosted zone id is nil"
        try:
            assert resp.get("HostedZone", {}).get("Name") == verify_domain, (
                f"domain name mismatch: got {resp.get('HostedZone', {}).get('Name')}, want {verify_domain}"
            )
            get_resp = route53_client.get_hosted_zone(Id=hz_id)
            assert get_resp.get("HostedZone", {}).get("Name") == verify_domain, (
                "get domain name mismatch"
            )
        finally:
            route53_client.delete_hosted_zone(Id=hz_id)


class TestGetHostedZone:
    def test_get_hosted_zone(self, route53_client, hosted_zone):
        hz_id, _ = hosted_zone
        route53_client.get_hosted_zone(Id=hz_id)

    def test_nonexistent(self, route53_client):
        with pytest.raises(Exception):
            route53_client.get_hosted_zone(Id="Z00000000000000000000")


class TestDeleteHostedZone:
    def test_delete_hosted_zone(self, route53_client, unique_name):
        domain_name = unique_name("hz") + ".com."
        resp = route53_client.create_hosted_zone(
            Name=domain_name,
            CallerReference=unique_name("ref"),
        )
        hz_id = resp.get("HostedZone", {}).get("Id")
        if hz_id:
            route53_client.delete_hosted_zone(Id=hz_id)

    def test_nonexistent(self, route53_client):
        with pytest.raises(Exception):
            route53_client.delete_hosted_zone(Id="Z00000000000000000000")


class TestResourceRecordSets:
    def test_list_resource_record_sets(self, route53_client, hosted_zone):
        hz_id, _ = hosted_zone
        route53_client.list_resource_record_sets(HostedZoneId=hz_id, MaxItems="10")

    def test_create(self, route53_client, hosted_zone):
        hz_id, domain_name = hosted_zone
        route53_client.change_resource_record_sets(
            HostedZoneId=hz_id,
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

    def test_delete(self, route53_client, hosted_zone):
        hz_id, domain_name = hosted_zone
        route53_client.change_resource_record_sets(
            HostedZoneId=hz_id,
            ChangeBatch={
                "Changes": [
                    {
                        "Action": "CREATE",
                        "ResourceRecordSet": {
                            "Name": f"del.{domain_name}",
                            "Type": "A",
                            "TTL": 300,
                            "ResourceRecords": [{"Value": "192.0.2.1"}],
                        },
                    },
                ],
            },
        )
        route53_client.change_resource_record_sets(
            HostedZoneId=hz_id,
            ChangeBatch={
                "Changes": [
                    {
                        "Action": "DELETE",
                        "ResourceRecordSet": {
                            "Name": f"del.{domain_name}",
                            "Type": "A",
                            "TTL": 300,
                            "ResourceRecords": [{"Value": "192.0.2.1"}],
                        },
                    },
                ],
            },
        )


class TestGetChange:
    def test_get_change(self, route53_client, hosted_zone):
        hz_id, domain_name = hosted_zone
        resp = route53_client.change_resource_record_sets(
            HostedZoneId=hz_id,
            ChangeBatch={
                "Changes": [
                    {
                        "Action": "CREATE",
                        "ResourceRecordSet": {
                            "Name": f"change.{domain_name}",
                            "Type": "A",
                            "TTL": 300,
                            "ResourceRecords": [{"Value": "192.0.2.1"}],
                        },
                    },
                ],
            },
        )
        change_id = resp.get("ChangeInfo", {}).get("Id")
        if change_id:
            route53_client.get_change(Id=change_id)

    def test_nonexistent(self, route53_client):
        with pytest.raises(Exception):
            route53_client.get_change(Id="C0000000000000000000000000")


class TestGetDNSSEC:
    def test_get_dnssec(self, route53_client, hosted_zone):
        hz_id, _ = hosted_zone
        route53_client.get_dnssec(HostedZoneId=hz_id)


class TestListReusableDelegationSets:
    def test_list_reusable_delegation_sets(self, route53_client):
        route53_client.list_reusable_delegation_sets(MaxItems="10")
