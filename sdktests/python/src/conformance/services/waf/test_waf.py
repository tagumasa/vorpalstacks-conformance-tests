import time

import pytest

SCOPE = "REGIONAL"


@pytest.fixture(scope="module")
def ip_set(waf_client):
    name = f"test-ipset-{int(time.time() * 1000)}"
    resp = waf_client.create_ip_set(
        Name=name,
        Description="Test IP Set",
        Scope=SCOPE,
        IPAddressVersion="IPV4",
        Addresses=["10.0.0.0/24"],
    )
    ip_set_id = resp.get("Summary", {}).get("Id", "")
    ip_set_arn = resp.get("Summary", {}).get("ARN", "")
    yield name, ip_set_id, ip_set_arn
    if ip_set_id:
        try:
            get_resp = waf_client.get_ip_set(Id=ip_set_id, Scope=SCOPE, Name=name)
            waf_client.delete_ip_set(
                Id=ip_set_id,
                Scope=SCOPE,
                Name=name,
                LockToken=get_resp.get("LockToken", ""),
            )
        except Exception:
            pass


@pytest.fixture(scope="module")
def regex_pattern_set(waf_client):
    name = f"test-regex-{int(time.time() * 1000)}"
    resp = waf_client.create_regex_pattern_set(
        Name=name,
        Description="Test Regex Pattern Set",
        Scope=SCOPE,
        RegularExpressionList=[{"RegexString": "^test-.*"}],
    )
    rps_id = resp.get("Summary", {}).get("Id", "")
    yield name, rps_id
    if rps_id:
        try:
            get_resp = waf_client.get_regex_pattern_set(
                Name=name, Scope=SCOPE, Id=rps_id
            )
            waf_client.delete_regex_pattern_set(
                Name=name,
                Scope=SCOPE,
                Id=rps_id,
                LockToken=get_resp.get("LockToken", ""),
            )
        except Exception:
            pass


@pytest.fixture(scope="module")
def rule_group(waf_client):
    name = f"test-rulegroup-{int(time.time() * 1000)}"
    resp = waf_client.create_rule_group(
        Name=name,
        Description="Test Rule Group",
        Scope=SCOPE,
        Capacity=10,
        Rules=[
            {
                "Name": "test-rule",
                "Priority": 1,
                "Action": {"Allow": {}},
                "Statement": {
                    "ByteMatchStatement": {
                        "FieldToMatch": {"UriPath": {}},
                        "PositionalConstraint": "STARTS_WITH",
                        "SearchString": "test",
                        "TextTransformations": [{"Priority": 0, "Type": "NONE"}],
                    }
                },
                "VisibilityConfig": {
                    "SampledRequestsEnabled": True,
                    "CloudWatchMetricsEnabled": True,
                    "MetricName": "test-rule-metric",
                },
            }
        ],
        VisibilityConfig={
            "SampledRequestsEnabled": True,
            "CloudWatchMetricsEnabled": True,
            "MetricName": "test-rulegroup-metric",
        },
    )
    rg_id = resp.get("Summary", {}).get("Id", "")
    yield name, rg_id
    if rg_id:
        try:
            get_resp = waf_client.get_rule_group(Name=name, Scope=SCOPE, Id=rg_id)
            waf_client.delete_rule_group(
                Name=name,
                Scope=SCOPE,
                Id=rg_id,
                LockToken=get_resp.get("LockToken", ""),
            )
        except Exception:
            pass


class TestWebACLs:
    def test_list_web_acls(self, waf_client):
        waf_client.list_web_acls(Scope=SCOPE)


class TestIPSet:
    def test_create_ip_set(self, ip_set):
        _, ip_set_id, _ = ip_set
        assert ip_set_id

    def test_get_ip_set(self, waf_client, ip_set):
        name, ip_set_id, _ = ip_set
        waf_client.get_ip_set(Id=ip_set_id, Scope=SCOPE, Name=name)

    def test_list_ip_sets(self, waf_client):
        waf_client.list_ip_sets(Scope=SCOPE)

    def test_list_tags_for_resource(self, waf_client, ip_set):
        _, _, ip_set_arn = ip_set
        waf_client.list_tags_for_resource(ResourceARN=ip_set_arn)

    def test_update_ip_set(self, waf_client, ip_set):
        name, ip_set_id, _ = ip_set
        resp = waf_client.get_ip_set(Id=ip_set_id, Scope=SCOPE, Name=name)
        current_lock_token = resp.get("LockToken", "")
        waf_client.update_ip_set(
            Id=ip_set_id,
            Scope=SCOPE,
            Name=name,
            Addresses=["10.0.0.0/24", "192.168.0.0/24"],
            LockToken=current_lock_token,
        )

    def test_delete_ip_set(self, waf_client, ip_set):
        name, ip_set_id, _ = ip_set
        resp = waf_client.get_ip_set(Id=ip_set_id, Scope=SCOPE, Name=name)
        waf_client.delete_ip_set(
            Id=ip_set_id,
            Scope=SCOPE,
            Name=name,
            LockToken=resp.get("LockToken", ""),
        )


class TestRegexPatternSet:
    def test_create_regex_pattern_set(self, regex_pattern_set):
        _, rps_id = regex_pattern_set
        assert rps_id

    def test_get_regex_pattern_set(self, waf_client, regex_pattern_set):
        name, rps_id = regex_pattern_set
        waf_client.get_regex_pattern_set(Name=name, Scope=SCOPE, Id=rps_id)

    def test_list_regex_pattern_sets(self, waf_client):
        waf_client.list_regex_pattern_sets(Scope=SCOPE)

    def test_delete_regex_pattern_set(self, waf_client, regex_pattern_set):
        name, rps_id = regex_pattern_set
        resp = waf_client.get_regex_pattern_set(Name=name, Scope=SCOPE, Id=rps_id)
        waf_client.delete_regex_pattern_set(
            Name=name,
            Scope=SCOPE,
            Id=rps_id,
            LockToken=resp.get("LockToken", ""),
        )


class TestRuleGroup:
    def test_create_rule_group(self, rule_group):
        _, rg_id = rule_group
        assert rg_id

    def test_get_rule_group(self, waf_client, rule_group):
        name, rg_id = rule_group
        waf_client.get_rule_group(Name=name, Scope=SCOPE, Id=rg_id)

    def test_list_rule_groups(self, waf_client):
        waf_client.list_rule_groups(Scope=SCOPE)

    def test_delete_rule_group(self, waf_client, rule_group):
        name, rg_id = rule_group
        resp = waf_client.get_rule_group(Name=name, Scope=SCOPE, Id=rg_id)
        waf_client.delete_rule_group(
            Name=name,
            Scope=SCOPE,
            Id=rg_id,
            LockToken=resp.get("LockToken", ""),
        )

    def test_list_available_managed_rule_groups(self, waf_client):
        waf_client.list_available_managed_rule_groups(Scope=SCOPE)


class TestErrorCases:
    def test_get_ip_set_nonexistent(self, waf_client):
        with pytest.raises(Exception):
            waf_client.get_ip_set(
                Id="nonexistent-ipset-xyz",
                Scope=SCOPE,
                Name="nonexistent-ipset-xyz",
            )

    def test_delete_ip_set_nonexistent(self, waf_client):
        with pytest.raises(Exception):
            waf_client.delete_ip_set(
                Id="nonexistent-ipset-xyz",
                Scope=SCOPE,
                Name="nonexistent-ipset-xyz",
                LockToken="fake-lock-token",
            )

    def test_get_regex_pattern_set_nonexistent(self, waf_client):
        with pytest.raises(Exception):
            waf_client.get_regex_pattern_set(
                Name="nonexistent-regex-xyz",
                Scope=SCOPE,
                Id="nonexistent-regex-xyz",
            )

    def test_get_rule_group_nonexistent(self, waf_client):
        with pytest.raises(Exception):
            waf_client.get_rule_group(
                Name="nonexistent-rulegroup-xyz",
                Scope=SCOPE,
                Id="nonexistent-rulegroup-xyz",
            )


class TestVerification:
    def test_list_ip_sets_contains_created(self, waf_client):
        list_name = f"verify-ipset-{int(time.time() * 1000)}"
        create_resp = waf_client.create_ip_set(
            Name=list_name,
            Description="Verify IP Set",
            Scope=SCOPE,
            IPAddressVersion="IPV4",
            Addresses=["10.0.0.0/24"],
        )
        list_resp = waf_client.list_ip_sets(Scope=SCOPE)
        found = any(
            ipset.get("Name") == list_name for ipset in list_resp.get("IPSets", [])
        )
        assert found
        try:
            waf_client.delete_ip_set(
                Id=create_resp.get("Summary", {}).get("Id", ""),
                Scope=SCOPE,
                Name=list_name,
                LockToken=create_resp.get("Summary", {}).get("LockToken", ""),
            )
        except Exception:
            pass
