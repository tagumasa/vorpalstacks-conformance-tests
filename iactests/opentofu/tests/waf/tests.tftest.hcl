run "create_ipset" {
  command = apply

  assert {
    condition     = aws_waf_ipset.test.name == "test-ipset"
    error_message = "IPSet name should be test-ipset"
  }

  assert {
    condition     = length(aws_waf_ipset.test.ip_set_descriptors) == 2
    error_message = "IPSet should have 2 descriptors"
  }
}

run "create_regex_pattern_set" {
  command = apply

  assert {
    condition     = aws_waf_regex_pattern_set.test.name == "test-regex-set"
    error_message = "Regex pattern set name should be test-regex-set"
  }

  assert {
    condition     = length(aws_waf_regex_pattern_set.test.regex_pattern_strings) == 1
    error_message = "Regex pattern set should have 1 pattern"
  }
}

run "create_rule" {
  command = apply

  assert {
    condition     = aws_waf_rule.test.name == "test-rule"
    error_message = "Rule name should be test-rule"
  }
}

run "create_web_acl" {
  command = apply

  assert {
    condition     = aws_waf_web_acl.test.name == "test-webacl"
    error_message = "WebACL name should be test-webacl"
  }

  assert {
    condition     = aws_waf_web_acl.test.default_action[0].type == "ALLOW"
    error_message = "Default action should be ALLOW"
  }

  assert {
    condition     = length(aws_waf_web_acl.test.rules) == 1
    error_message = "WebACL should have 1 rule"
  }
}
