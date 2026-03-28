import {
  WAFV2Client,
  ListWebACLsCommand,
  CreateIPSetCommand,
  GetIPSetCommand,
  ListIPSetsCommand,
  UpdateIPSetCommand,
  DeleteIPSetCommand,
  CreateRegexPatternSetCommand,
  GetRegexPatternSetCommand,
  ListRegexPatternSetsCommand,
  DeleteRegexPatternSetCommand,
  CreateRuleGroupCommand,
  GetRuleGroupCommand,
  ListRuleGroupsCommand,
  DeleteRuleGroupCommand,
  ListAvailableManagedRuleGroupsCommand,
  ListTagsForResourceCommand,
} from "@aws-sdk/client-wafv2";
import { TestRunner, TestResult } from "../runner";

export async function runWAFTests(
  runner: TestRunner,
  client: WAFV2Client,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const scope = "REGIONAL";
  const ipSetName = `test-ipset-${Date.now()}`;
  const ipSetDescription = "Test IP Set";
  let ipSetId = "";
  let ipSetLockToken = "";
  let ipSetARN = "";

  results.push(await runner.runTest("waf", "ListWebACLs", async () => {
    await client.send(new ListWebACLsCommand({ Scope: scope }));
  }));

  results.push(await runner.runTest("waf", "CreateIPSet", async () => {
    const resp = await client.send(new CreateIPSetCommand({
      Name: ipSetName,
      Description: ipSetDescription,
      Scope: scope,
      IPAddressVersion: "IPV4",
      Addresses: ["10.0.0.0/24"],
    }));
    if (resp.Summary?.Id) {
      ipSetId = resp.Summary.Id;
    }
    if (resp.Summary?.LockToken) {
      ipSetLockToken = resp.Summary.LockToken;
    }
    if (resp.Summary?.ARN) {
      ipSetARN = resp.Summary.ARN;
    }
  }));

  results.push(await runner.runTest("waf", "GetIPSet", async () => {
    const resp = await client.send(new GetIPSetCommand({
      Id: ipSetId,
      Scope: scope,
      Name: ipSetName,
    }));
    if (resp.LockToken) {
      ipSetLockToken = resp.LockToken;
    }
  }));

  results.push(await runner.runTest("waf", "ListIPSets", async () => {
    await client.send(new ListIPSetsCommand({ Scope: scope }));
  }));

  results.push(await runner.runTest("waf", "ListTagsForResource", async () => {
    await client.send(new ListTagsForResourceCommand({
      ResourceARN: ipSetARN,
    }));
  }));

  results.push(await runner.runTest("waf", "UpdateIPSet", async () => {
    const resp = await client.send(new GetIPSetCommand({
      Id: ipSetId,
      Scope: scope,
      Name: ipSetName,
    }));
    const currentLockToken = resp.LockToken || ipSetLockToken;
    await client.send(new UpdateIPSetCommand({
      Id: ipSetId,
      Scope: scope,
      Name: ipSetName,
      Addresses: ["10.0.0.0/24", "192.168.0.0/24"],
      LockToken: currentLockToken,
    }));
  }));

  results.push(await runner.runTest("waf", "DeleteIPSet", async () => {
    const resp = await client.send(new GetIPSetCommand({
      Id: ipSetId,
      Scope: scope,
      Name: ipSetName,
    }));
    await client.send(new DeleteIPSetCommand({
      Id: ipSetId,
      Scope: scope,
      Name: ipSetName,
      LockToken: resp.LockToken || ipSetLockToken,
    }));
  }));

  const regexPatternSetName = `test-regex-${Date.now()}`;
  let regexPatternSetId = "";
  results.push(await runner.runTest("waf", "CreateRegexPatternSet", async () => {
    const resp = await client.send(new CreateRegexPatternSetCommand({
      Name: regexPatternSetName,
      Description: "Test Regex Pattern Set",
      Scope: scope,
      RegularExpressionList: [{ RegexString: "^test-.*" }],
    }));
    if (resp.Summary?.Id) {
      regexPatternSetId = resp.Summary.Id;
    }
  }));

  results.push(await runner.runTest("waf", "GetRegexPatternSet", async () => {
    await client.send(new GetRegexPatternSetCommand({
      Name: regexPatternSetName,
      Scope: scope,
      Id: regexPatternSetId,
    }));
  }));

  results.push(await runner.runTest("waf", "ListRegexPatternSets", async () => {
    await client.send(new ListRegexPatternSetsCommand({ Scope: scope }));
  }));

  results.push(await runner.runTest("waf", "DeleteRegexPatternSet", async () => {
    const getResp = await client.send(new GetRegexPatternSetCommand({
      Name: regexPatternSetName,
      Scope: scope,
      Id: regexPatternSetId,
    }));
    const lockToken = getResp.LockToken || "";
    await client.send(new DeleteRegexPatternSetCommand({
      Name: regexPatternSetName,
      Scope: scope,
      Id: regexPatternSetId,
      LockToken: lockToken,
    }));
  }));

  const ruleGroupName = `test-rulegroup-${Date.now()}`;
  let ruleGroupId = "";
  results.push(await runner.runTest("waf", "CreateRuleGroup", async () => {
    const resp = await client.send(new CreateRuleGroupCommand({
      Name: ruleGroupName,
      Description: "Test Rule Group",
      Scope: scope,
      Capacity: 10,
      Rules: [{
        Name: "test-rule",
        Priority: 1,
        Action: { Allow: {} },
        Statement: {
          ByteMatchStatement: {
            FieldToMatch: { UriPath: {} },
            PositionalConstraint: "STARTS_WITH",
            SearchString: Uint8Array.from([116, 101, 115, 116]),
            TextTransformations: [{
              Priority: 0,
              Type: "NONE",
            }],
          },
        },
        VisibilityConfig: {
          SampledRequestsEnabled: true,
          CloudWatchMetricsEnabled: true,
          MetricName: "test-rule-metric",
        },
      }],
      VisibilityConfig: {
        SampledRequestsEnabled: true,
        CloudWatchMetricsEnabled: true,
        MetricName: "test-rulegroup-metric",
      },
    }));
    if (resp.Summary?.Id) {
      ruleGroupId = resp.Summary.Id;
    }
  }));

  results.push(await runner.runTest("waf", "GetRuleGroup", async () => {
    await client.send(new GetRuleGroupCommand({
      Name: ruleGroupName,
      Scope: scope,
      Id: ruleGroupId,
    }));
  }));

  results.push(await runner.runTest("waf", "ListRuleGroups", async () => {
    await client.send(new ListRuleGroupsCommand({ Scope: scope }));
  }));

  results.push(await runner.runTest("waf", "DeleteRuleGroup", async () => {
    const getResp = await client.send(new GetRuleGroupCommand({
      Name: ruleGroupName,
      Scope: scope,
      Id: ruleGroupId,
    }));
    const lockToken = getResp.LockToken || "";
    await client.send(new DeleteRuleGroupCommand({
      Name: ruleGroupName,
      Scope: scope,
      Id: ruleGroupId,
      LockToken: lockToken,
    }));
  }));

  results.push(await runner.runTest("waf", "ListAvailableManagedRuleGroups", async () => {
    await client.send(new ListAvailableManagedRuleGroupsCommand({ Scope: scope }));
  }));

  results.push(await runner.runTest("waf", "GetIPSet_NonExistent", async () => {
    try {
      await client.send(new GetIPSetCommand({
        Id: "nonexistent-ipset-xyz",
        Scope: scope,
        Name: "nonexistent-ipset-xyz",
      }));
      throw new Error("expected error for non-existent IP set");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for non-existent IP set") {
        throw err;
      }
    }
  }));

  results.push(await runner.runTest("waf", "DeleteIPSet_NonExistent", async () => {
    try {
      await client.send(new DeleteIPSetCommand({
        Id: "nonexistent-ipset-xyz",
        Scope: scope,
        Name: "nonexistent-ipset-xyz",
        LockToken: "fake-lock-token",
      }));
      throw new Error("expected error for non-existent IP set");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for non-existent IP set") {
        throw err;
      }
    }
  }));

  results.push(await runner.runTest("waf", "GetRegexPatternSet_NonExistent", async () => {
    try {
      await client.send(new GetRegexPatternSetCommand({
        Name: "nonexistent-regex-xyz",
        Scope: scope,
        Id: "nonexistent-regex-xyz",
      }));
      throw new Error("expected error for non-existent regex pattern set");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for non-existent regex pattern set") {
        throw err;
      }
    }
  }));

  results.push(await runner.runTest("waf", "GetRuleGroup_NonExistent", async () => {
    try {
      await client.send(new GetRuleGroupCommand({
        Name: "nonexistent-rulegroup-xyz",
        Scope: scope,
        Id: "nonexistent-rulegroup-xyz",
      }));
      throw new Error("expected error for non-existent rule group");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for non-existent rule group") {
        throw err;
      }
    }
  }));

  results.push(await runner.runTest("waf", "ListIPSets_ContainsCreated", async () => {
    const listName = `verify-ipset-${Date.now()}`;
    const createResp = await client.send(new CreateIPSetCommand({
      Name: listName,
      Description: "Verify IP Set",
      Scope: scope,
      IPAddressVersion: "IPV4",
      Addresses: ["10.0.0.0/24"],
    }));
    const listResp = await client.send(new ListIPSetsCommand({ Scope: scope }));
    const found = listResp.IPSets?.some(
      (ipset: { Name?: string }) => ipset.Name === listName
    );
    if (!found) {
      throw new Error("created IP set not found in list");
    }
    try {
      await client.send(new DeleteIPSetCommand({
        Id: createResp.Summary?.Id || "",
        Scope: scope,
        Name: listName,
        LockToken: createResp.Summary?.LockToken || "",
      }));
    } catch {
      // ignore cleanup
    }
  }));

  return results;
}