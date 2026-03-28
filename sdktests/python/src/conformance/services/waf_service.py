import time
from ..runner import TestRunner, TestResult


async def run_waf_tests(
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
    client = session.client("wafv2", endpoint_url=endpoint, region_name=region)

    scope = "REGIONAL"
    ip_set_name = f"test-ipset-{int(time.time() * 1000)}"
    ip_set_description = "Test IP Set"
    ip_set_id = ""
    ip_set_lock_token = ""
    ip_set_arn = ""

    def _list_web_acls():
        client.list_web_acls(Scope=scope)

    results.append(await runner.run_test("waf", "ListWebACLs", _list_web_acls))

    def _create_ip_set():
        nonlocal ip_set_id, ip_set_lock_token, ip_set_arn
        resp = client.create_ip_set(
            Name=ip_set_name,
            Description=ip_set_description,
            Scope=scope,
            IPAddressVersion="IPV4",
            Addresses=["10.0.0.0/24"],
        )
        ip_set_id = resp.get("Summary", {}).get("Id", "")
        ip_set_lock_token = resp.get("Summary", {}).get("LockToken", "")
        ip_set_arn = resp.get("Summary", {}).get("ARN", "")

    results.append(await runner.run_test("waf", "CreateIPSet", _create_ip_set))

    def _get_ip_set():
        nonlocal ip_set_lock_token
        resp = client.get_ip_set(
            Id=ip_set_id,
            Scope=scope,
            Name=ip_set_name,
        )
        ip_set_lock_token = resp.get("LockToken", ip_set_lock_token)

    results.append(await runner.run_test("waf", "GetIPSet", _get_ip_set))

    def _list_ip_sets():
        client.list_ip_sets(Scope=scope)

    results.append(await runner.run_test("waf", "ListIPSets", _list_ip_sets))

    def _list_tags_for_resource():
        client.list_tags_for_resource(ResourceARN=ip_set_arn)

    results.append(
        await runner.run_test("waf", "ListTagsForResource", _list_tags_for_resource)
    )

    def _update_ip_set():
        nonlocal ip_set_lock_token
        resp = client.get_ip_set(
            Id=ip_set_id,
            Scope=scope,
            Name=ip_set_name,
        )
        current_lock_token = resp.get("LockToken", ip_set_lock_token)
        client.update_ip_set(
            Id=ip_set_id,
            Scope=scope,
            Name=ip_set_name,
            Addresses=["10.0.0.0/24", "192.168.0.0/24"],
            LockToken=current_lock_token,
        )

    results.append(await runner.run_test("waf", "UpdateIPSet", _update_ip_set))

    def _delete_ip_set():
        resp = client.get_ip_set(
            Id=ip_set_id,
            Scope=scope,
            Name=ip_set_name,
        )
        client.delete_ip_set(
            Id=ip_set_id,
            Scope=scope,
            Name=ip_set_name,
            LockToken=resp.get("LockToken", ip_set_lock_token),
        )

    results.append(await runner.run_test("waf", "DeleteIPSet", _delete_ip_set))

    regex_pattern_set_name = f"test-regex-{int(time.time() * 1000)}"
    regex_pattern_set_id = ""
    regex_pattern_set_lock_token = ""

    def _create_regex_pattern_set():
        nonlocal regex_pattern_set_id, regex_pattern_set_lock_token
        resp = client.create_regex_pattern_set(
            Name=regex_pattern_set_name,
            Description="Test Regex Pattern Set",
            Scope=scope,
            RegularExpressionList=[{"RegexString": "^test-.*"}],
        )
        regex_pattern_set_id = resp.get("Summary", {}).get("Id", "")
        regex_pattern_set_lock_token = resp.get("Summary", {}).get("LockToken", "")

    results.append(
        await runner.run_test("waf", "CreateRegexPatternSet", _create_regex_pattern_set)
    )

    def _get_regex_pattern_set():
        nonlocal regex_pattern_set_lock_token
        resp = client.get_regex_pattern_set(
            Name=regex_pattern_set_name,
            Scope=scope,
            Id=regex_pattern_set_id,
        )
        regex_pattern_set_lock_token = resp.get(
            "LockToken", regex_pattern_set_lock_token
        )

    results.append(
        await runner.run_test("waf", "GetRegexPatternSet", _get_regex_pattern_set)
    )

    def _list_regex_pattern_sets():
        client.list_regex_pattern_sets(Scope=scope)

    results.append(
        await runner.run_test("waf", "ListRegexPatternSets", _list_regex_pattern_sets)
    )

    def _delete_regex_pattern_set():
        resp = client.get_regex_pattern_set(
            Name=regex_pattern_set_name,
            Scope=scope,
            Id=regex_pattern_set_id,
        )
        client.delete_regex_pattern_set(
            Name=regex_pattern_set_name,
            Scope=scope,
            Id=regex_pattern_set_id,
            LockToken=resp.get("LockToken", regex_pattern_set_lock_token),
        )

    results.append(
        await runner.run_test("waf", "DeleteRegexPatternSet", _delete_regex_pattern_set)
    )

    rule_group_name = f"test-rulegroup-{int(time.time() * 1000)}"
    rule_group_id = ""
    rule_group_lock_token = ""

    def _create_rule_group():
        nonlocal rule_group_id, rule_group_lock_token
        resp = client.create_rule_group(
            Name=rule_group_name,
            Description="Test Rule Group",
            Scope=scope,
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
        rule_group_id = resp.get("Summary", {}).get("Id", "")
        rule_group_lock_token = resp.get("Summary", {}).get("LockToken", "")

    results.append(await runner.run_test("waf", "CreateRuleGroup", _create_rule_group))

    def _get_rule_group():
        nonlocal rule_group_lock_token
        resp = client.get_rule_group(
            Name=rule_group_name,
            Scope=scope,
            Id=rule_group_id,
        )
        rule_group_lock_token = resp.get("LockToken", rule_group_lock_token)

    results.append(await runner.run_test("waf", "GetRuleGroup", _get_rule_group))

    def _list_rule_groups():
        client.list_rule_groups(Scope=scope)

    results.append(await runner.run_test("waf", "ListRuleGroups", _list_rule_groups))

    def _delete_rule_group():
        resp = client.get_rule_group(
            Name=rule_group_name,
            Scope=scope,
            Id=rule_group_id,
        )
        client.delete_rule_group(
            Name=rule_group_name,
            Scope=scope,
            Id=rule_group_id,
            LockToken=resp.get("LockToken", rule_group_lock_token),
        )

    results.append(await runner.run_test("waf", "DeleteRuleGroup", _delete_rule_group))

    def _list_available_managed_rule_groups():
        client.list_available_managed_rule_groups(Scope=scope)

    results.append(
        await runner.run_test(
            "waf", "ListAvailableManagedRuleGroups", _list_available_managed_rule_groups
        )
    )

    def _get_ip_set_nonexistent():
        try:
            client.get_ip_set(
                Id="nonexistent-ipset-xyz",
                Scope=scope,
                Name="nonexistent-ipset-xyz",
            )
            raise Exception("expected error for non-existent IP set")
        except Exception as e:
            if str(e) == "expected error for non-existent IP set":
                raise

    results.append(
        await runner.run_test("waf", "GetIPSet_NonExistent", _get_ip_set_nonexistent)
    )

    def _delete_ip_set_nonexistent():
        try:
            client.delete_ip_set(
                Id="nonexistent-ipset-xyz",
                Scope=scope,
                Name="nonexistent-ipset-xyz",
                LockToken="fake-lock-token",
            )
            raise Exception("expected error for non-existent IP set")
        except Exception as e:
            if str(e) == "expected error for non-existent IP set":
                raise

    results.append(
        await runner.run_test(
            "waf", "DeleteIPSet_NonExistent", _delete_ip_set_nonexistent
        )
    )

    def _get_regex_pattern_set_nonexistent():
        try:
            client.get_regex_pattern_set(
                Name="nonexistent-regex-xyz",
                Scope=scope,
                Id="nonexistent-regex-xyz",
            )
            raise Exception("expected error for non-existent regex pattern set")
        except Exception as e:
            if str(e) == "expected error for non-existent regex pattern set":
                raise

    results.append(
        await runner.run_test(
            "waf", "GetRegexPatternSet_NonExistent", _get_regex_pattern_set_nonexistent
        )
    )

    def _get_rule_group_nonexistent():
        try:
            client.get_rule_group(
                Name="nonexistent-rulegroup-xyz",
                Scope=scope,
                Id="nonexistent-rulegroup-xyz",
            )
            raise Exception("expected error for non-existent rule group")
        except Exception as e:
            if str(e) == "expected error for non-existent rule group":
                raise

    results.append(
        await runner.run_test(
            "waf", "GetRuleGroup_NonExistent", _get_rule_group_nonexistent
        )
    )

    def _list_ip_sets_contains_created():
        list_name = f"verify-ipset-{int(time.time() * 1000)}"
        create_resp = client.create_ip_set(
            Name=list_name,
            Description="Verify IP Set",
            Scope=scope,
            IPAddressVersion="IPV4",
            Addresses=["10.0.0.0/24"],
        )
        list_resp = client.list_ip_sets(Scope=scope)
        found = any(
            ipset.get("Name") == list_name for ipset in list_resp.get("IPSets", [])
        )
        if not found:
            raise Exception("created IP set not found in list")
        try:
            client.delete_ip_set(
                Id=create_resp.get("Summary", {}).get("Id", ""),
                Scope=scope,
                Name=list_name,
                LockToken=create_resp.get("Summary", {}).get("LockToken", ""),
            )
        except Exception:
            pass

    results.append(
        await runner.run_test(
            "waf", "ListIPSets_ContainsCreated", _list_ip_sets_contains_created
        )
    )

    return results
