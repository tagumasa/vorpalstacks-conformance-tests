import {
  WAFV2Client,
  CreateWebACLCommand,
  GetWebACLCommand,
  ListWebACLsCommand,
  UpdateWebACLCommand,
  DeleteWebACLCommand,
  ListTagsForResourceCommand,
  TagResourceCommand,
  UntagResourceCommand,
  PutLoggingConfigurationCommand,
  GetLoggingConfigurationCommand,
  DeleteLoggingConfigurationCommand,
  AssociateWebACLCommand,
  DisassociateWebACLCommand,
  GetWebACLForResourceCommand,
  ListResourcesForWebACLCommand,
  ListAvailableManagedRuleGroupsCommand,
  DescribeManagedRuleGroupCommand,
} from '@aws-sdk/client-wafv2';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, assertErrorContains } from '../../helpers.js';
import { SCOPE, type WebACLState } from './context.js';

export async function runWebACLTests(
  runner: TestRunner,
  client: WAFV2Client,
  aclState: WebACLState,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('wafv2', 'ListWebACLs_Empty', async () => {
    await client.send(new ListWebACLsCommand({ Scope: SCOPE, Limit: 10 }));
  }));

  results.push(await runner.runTest('wafv2', 'CreateWebACL', async () => {
    const resp = await client.send(new CreateWebACLCommand({
      Name: aclState.name, Scope: SCOPE,
      DefaultAction: { Allow: {} },
      VisibilityConfig: {
        SampledRequestsEnabled: true,
        CloudWatchMetricsEnabled: true,
        MetricName: 'webacl-test-metric',
      },
      Description: 'Test WebACL for SDK tests',
      Rules: [
        {
          Name: 'AllowTestRule',
          Priority: 0,
          Action: { Allow: {} },
          VisibilityConfig: {
            SampledRequestsEnabled: true,
            CloudWatchMetricsEnabled: true,
            MetricName: 'allow-test-rule-metric',
          },
          Statement: {
            ByteMatchStatement: {
              FieldToMatch: { UriPath: {} },
              PositionalConstraint: 'CONTAINS',
              SearchString: new TextEncoder().encode('/test-path'),
              TextTransformations: [{ Priority: 0, Type: 'NONE' }],
            },
          },
        },
      ],
      Tags: [{ Key: 'Name', Value: 'WebACLTest' }],
    }));
    if (!resp.Summary?.Id) throw new Error('expected Summary.Id to be defined');
    aclState.id = resp.Summary.Id;
    aclState.arn = resp.Summary.ARN ?? '';
    aclState.lockToken = resp.Summary.LockToken ?? '';
  }));

  results.push(await runner.runTest('wafv2', 'GetWebACL', async () => {
    const resp = await client.send(new GetWebACLCommand({
      Name: aclState.name, Scope: SCOPE, Id: aclState.id,
    }));
    if (!resp.WebACL) throw new Error('expected WebACL to be defined');
    if (resp.WebACL.Name !== aclState.name) {
      throw new Error(`expected name '${aclState.name}', got '${resp.WebACL.Name}'`);
    }
    if (!resp.LockToken) throw new Error('expected LockToken to be defined');
    if (!resp.WebACL.DefaultAction?.Allow) throw new Error('expected default Allow action');
    if (!resp.WebACL.VisibilityConfig) throw new Error('expected VisibilityConfig to be defined');
  }));

  results.push(await runner.runTest('wafv2', 'ListWebACLs_ContainsCreated', async () => {
    const resp = await client.send(new ListWebACLsCommand({ Scope: SCOPE }));
    const found = resp.WebACLs?.some(s => s.Id === aclState.id && s.Name === aclState.name);
    if (!found) throw new Error('WebACL not found in list');
  }));

  results.push(await runner.runTest('wafv2', 'UpdateWebACL', async () => {
    const resp = await client.send(new UpdateWebACLCommand({
      Name: aclState.name, Scope: SCOPE, Id: aclState.id,
      LockToken: aclState.lockToken,
      DefaultAction: { Block: {} },
      VisibilityConfig: {
        SampledRequestsEnabled: true,
        CloudWatchMetricsEnabled: true,
        MetricName: 'updated-webacl-metric',
      },
    }));
    if (resp.NextLockToken) aclState.lockToken = resp.NextLockToken;
  }));

  results.push(await runner.runTest('wafv2', 'UpdateWebACL_ContentVerify', async () => {
    const resp = await client.send(new GetWebACLCommand({
      Name: aclState.name, Scope: SCOPE, Id: aclState.id,
    }));
    if (!resp.WebACL?.DefaultAction?.Block) throw new Error('expected default Block action after update');
    if (resp.WebACL.VisibilityConfig?.MetricName !== 'updated-webacl-metric') {
      throw new Error(`expected updated metric name, got '${resp.WebACL.VisibilityConfig?.MetricName}'`);
    }
  }));

  results.push(await runner.runTest('wafv2', 'UpdateWebACL_StaleLockToken', async () => {
    try {
      await client.send(new UpdateWebACLCommand({
        Name: aclState.name, Scope: SCOPE, Id: aclState.id,
        LockToken: 'stale-lock-token',
        DefaultAction: { Allow: {} },
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

  results.push(await runner.runTest('wafv2', 'TagResource_WebACL', async () => {
    await client.send(new TagResourceCommand({
      ResourceARN: aclState.arn,
      Tags: [{ Key: 'Team', Value: 'Security' }, { Key: 'Env', Value: 'Production' }],
    }));
  }));

  results.push(await runner.runTest('wafv2', 'ListTagsForResource_WebACL', async () => {
    const resp = await client.send(new ListTagsForResourceCommand({ ResourceARN: aclState.arn }));
    if (!resp.TagInfoForResource?.TagList) throw new Error('expected tags');
    if (resp.TagInfoForResource.TagList.length < 3) {
      throw new Error(`expected at least 3 tags, got ${resp.TagInfoForResource.TagList.length}`);
    }
  }));

  results.push(await runner.runTest('wafv2', 'UntagResource_WebACL', async () => {
    await client.send(new UntagResourceCommand({
      ResourceARN: aclState.arn, TagKeys: ['Env'],
    }));
  }));

  results.push(await runner.runTest('wafv2', 'UntagResource_Verify', async () => {
    const resp = await client.send(new ListTagsForResourceCommand({ ResourceARN: aclState.arn }));
    const envTag = resp.TagInfoForResource?.TagList?.find(t => t.Key === 'Env');
    if (envTag) throw new Error("tag 'Env' should have been removed");
  }));

  results.push(await runner.runTest('wafv2', 'PutLoggingConfiguration', async () => {
    const resp = await client.send(new PutLoggingConfigurationCommand({
      LoggingConfiguration: {
        ResourceArn: aclState.arn,
        LogDestinationConfigs: ['arn:aws:logs:us-east-1:123456789012:log-group:/aws/waf/test-log'],
      },
    }));
    if (!resp.LoggingConfiguration) throw new Error('expected LoggingConfiguration to be defined');
    if ((resp.LoggingConfiguration.LogDestinationConfigs?.length ?? 0) !== 1) {
      throw new Error(`expected 1 log destination, got ${resp.LoggingConfiguration.LogDestinationConfigs?.length ?? 0}`);
    }
  }));

  results.push(await runner.runTest('wafv2', 'GetLoggingConfiguration', async () => {
    const resp = await client.send(new GetLoggingConfigurationCommand({ ResourceArn: aclState.arn }));
    if (!resp.LoggingConfiguration) throw new Error('expected LoggingConfiguration to be defined');
    if (resp.LoggingConfiguration.ResourceArn !== aclState.arn) {
      throw new Error('ResourceArn mismatch');
    }
  }));

  results.push(await runner.runTest('wafv2', 'DeleteLoggingConfiguration', async () => {
    await client.send(new DeleteLoggingConfigurationCommand({ ResourceArn: aclState.arn }));
  }));

  results.push(await runner.runTest('wafv2', 'GetLoggingConfiguration_AfterDelete', async () => {
    try {
      await client.send(new GetLoggingConfigurationCommand({ ResourceArn: aclState.arn }));
      throw new Error('expected error after deleting logging config');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error after deleting logging config') throw err;
    }
  }));

  const fakeResourceARN = `arn:aws:elasticloadbalancing:us-east-1:123456789012:loadbalancer/app/test-lb/${Date.now()}`;

  results.push(await runner.runTest('wafv2', 'AssociateWebACL', async () => {
    await client.send(new AssociateWebACLCommand({
      WebACLArn: aclState.arn, ResourceArn: fakeResourceARN,
    }));
  }));

  results.push(await runner.runTest('wafv2', 'GetWebACLForResource', async () => {
    const resp = await client.send(new GetWebACLForResourceCommand({ ResourceArn: fakeResourceARN }));
    if (!resp.WebACL) throw new Error('expected WebACL to be defined');
    if (resp.WebACL.Name !== aclState.name) {
      throw new Error(`expected WebACL name '${aclState.name}', got '${resp.WebACL.Name}'`);
    }
  }));

  results.push(await runner.runTest('wafv2', 'ListResourcesForWebACL', async () => {
    const resp = await client.send(new ListResourcesForWebACLCommand({ WebACLArn: aclState.arn }));
    if (!resp.ResourceArns?.length) throw new Error('expected at least 1 resource ARN');
    if (!resp.ResourceArns.includes(fakeResourceARN)) throw new Error('resource ARN not found in list');
  }));

  results.push(await runner.runTest('wafv2', 'DisassociateWebACL', async () => {
    await client.send(new DisassociateWebACLCommand({ ResourceArn: fakeResourceARN }));
  }));

  results.push(await runner.runTest('wafv2', 'GetWebACLForResource_AfterDisassociate', async () => {
    try {
      await client.send(new GetWebACLForResourceCommand({ ResourceArn: fakeResourceARN }));
      throw new Error('expected error after disassociation');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error after disassociation') throw err;
    }
  }));

  results.push(await runner.runTest('wafv2', 'ListAvailableManagedRuleGroups', async () => {
    const resp = await client.send(new ListAvailableManagedRuleGroupsCommand({ Scope: SCOPE, Limit: 10 }));
    if (!resp.ManagedRuleGroups) throw new Error('expected ManagedRuleGroups to be defined');
  }));

  results.push(await runner.runTest('wafv2', 'DescribeManagedRuleGroup', async () => {
    const resp = await client.send(new DescribeManagedRuleGroupCommand({
      Name: 'AWSManagedRulesCommonRuleSet', VendorName: 'AWS', Scope: SCOPE,
    }));
    if (!resp.Capacity) throw new Error('expected non-zero capacity');
    if (!resp.LabelNamespace) throw new Error('expected LabelNamespace to be defined');
  }));

  results.push(await runner.runTest('wafv2', 'DescribeManagedRuleGroup_NotFound', async () => {
    try {
      await client.send(new DescribeManagedRuleGroupCommand({
        Name: 'NonExistentRuleGroup', VendorName: 'AWS', Scope: SCOPE,
      }));
      throw new Error('expected WAFNonexistentItemException');
    } catch (err) {
      assertErrorContains(err, 'WAFNonexistentItemException');
    }
  }));

  results.push(await runner.runTest('wafv2', 'DeleteWebACL', async () => {
    await client.send(new DeleteWebACLCommand({
      Name: aclState.name, Scope: SCOPE, Id: aclState.id, LockToken: aclState.lockToken,
    }));
  }));

  results.push(await runner.runTest('wafv2', 'GetWebACL_NonExistent', async () => {
    try {
      await client.send(new GetWebACLCommand({
        Name: aclState.name, Scope: SCOPE, Id: aclState.id,
      }));
      throw new Error('expected WAFNonexistentItemException');
    } catch (err) {
      assertErrorContains(err, 'WAFNonexistentItemException');
    }
  }));

  return results;
}
