import {
  WAFV2Client,
  CreateRuleGroupCommand,
  GetRuleGroupCommand,
  UpdateRuleGroupCommand,
  DeleteRuleGroupCommand,
  ListRuleGroupsCommand,
} from '@aws-sdk/client-wafv2';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, assertErrorContains } from '../../helpers.js';
import { SCOPE } from './context.js';

export async function runRuleGroupTests(
  runner: TestRunner,
  client: WAFV2Client,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const groupName = makeUniqueName('rulegroup');
  let groupId = '';
  let groupLockToken = '';

  results.push(await runner.runTest('wafv2', 'CreateRuleGroup', async () => {
    const resp = await client.send(new CreateRuleGroupCommand({
      Name: groupName, Scope: SCOPE, Capacity: 100,
      VisibilityConfig: {
        SampledRequestsEnabled: false,
        CloudWatchMetricsEnabled: false,
        MetricName: 'test-metric',
      },
    }));
    if (!resp.Summary?.Id) throw new Error('expected Summary.Id to be defined');
    groupId = resp.Summary.Id;
    groupLockToken = resp.Summary.LockToken ?? '';
  }));

  results.push(await runner.runTest('wafv2', 'GetRuleGroup', async () => {
    const resp = await client.send(new GetRuleGroupCommand({
      Name: groupName, Scope: SCOPE, Id: groupId,
    }));
    if (!resp.RuleGroup) throw new Error('expected RuleGroup to be defined');
    if (!resp.LockToken) throw new Error('expected LockToken to be defined');
    if (resp.RuleGroup.Capacity !== 100) {
      throw new Error(`expected capacity 100, got ${resp.RuleGroup.Capacity}`);
    }
  }));

  results.push(await runner.runTest('wafv2', 'ListRuleGroups', async () => {
    const resp = await client.send(new ListRuleGroupsCommand({ Scope: SCOPE, Limit: 10 }));
    if (!resp.RuleGroups) throw new Error('expected RuleGroups to be defined');
  }));

  results.push(await runner.runTest('wafv2', 'UpdateRuleGroup', async () => {
    const resp = await client.send(new UpdateRuleGroupCommand({
      Name: groupName, Scope: SCOPE, Id: groupId,
      LockToken: groupLockToken,
      VisibilityConfig: {
        SampledRequestsEnabled: true,
        CloudWatchMetricsEnabled: true,
        MetricName: 'updated-metric',
      },
    }));
    if (resp.NextLockToken) groupLockToken = resp.NextLockToken;
  }));

  results.push(await runner.runTest('wafv2', 'UpdateRuleGroup_ContentVerify', async () => {
    const resp = await client.send(new GetRuleGroupCommand({
      Name: groupName, Scope: SCOPE, Id: groupId,
    }));
    const vc = resp.RuleGroup?.VisibilityConfig;
    if (!vc) throw new Error('expected VisibilityConfig to be defined');
    if (vc.MetricName !== 'updated-metric') {
      throw new Error(`expected metric name 'updated-metric', got '${vc.MetricName}'`);
    }
  }));

  results.push(await runner.runTest('wafv2', 'UpdateRuleGroup_StaleLockToken', async () => {
    try {
      await client.send(new UpdateRuleGroupCommand({
        Name: groupName, Scope: SCOPE, Id: groupId,
        LockToken: 'stale-token-should-fail',
        VisibilityConfig: {
          SampledRequestsEnabled: false,
          CloudWatchMetricsEnabled: false,
          MetricName: 'bad-metric',
        },
      }));
      throw new Error('expected error for stale lock token');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for stale lock token') throw err;
    }
  }));

  results.push(await runner.runTest('wafv2', 'DeleteRuleGroup', async () => {
    await client.send(new DeleteRuleGroupCommand({
      Name: groupName, Scope: SCOPE, Id: groupId, LockToken: groupLockToken,
    }));
  }));

  results.push(await runner.runTest('wafv2', 'GetRuleGroup_NonExistent', async () => {
    try {
      await client.send(new GetRuleGroupCommand({ Name: groupName, Scope: SCOPE, Id: groupId }));
      throw new Error('expected WAFNonexistentItemException');
    } catch (err) {
      assertErrorContains(err, 'WAFNonexistentItemException');
    }
  }));

  return results;
}
