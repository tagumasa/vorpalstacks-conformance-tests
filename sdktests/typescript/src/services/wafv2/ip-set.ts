import {
  WAFV2Client,
  CreateIPSetCommand,
  GetIPSetCommand,
  ListIPSetsCommand,
  UpdateIPSetCommand,
  DeleteIPSetCommand,
  ListTagsForResourceCommand,
} from '@aws-sdk/client-wafv2';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, assertErrorContains } from '../../helpers.js';
import { SCOPE } from './context.js';

export async function runIPSetTests(
  runner: TestRunner,
  client: WAFV2Client,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const ipSetName = makeUniqueName('ipset');
  let ipSetId = '';
  let ipSetARN = '';
  let ipSetLockToken = '';

  results.push(await runner.runTest('wafv2', 'ListIPSets_Empty', async () => {
    await client.send(new ListIPSetsCommand({ Scope: SCOPE, Limit: 10 }));
  }));

  results.push(await runner.runTest('wafv2', 'CreateIPSet', async () => {
    const resp = await client.send(new CreateIPSetCommand({
      Name: ipSetName,
      Scope: SCOPE,
      IPAddressVersion: 'IPV4',
      Addresses: [],
      Tags: [
        { Key: 'Environment', Value: 'test' },
        { Key: 'Owner', Value: 'sdk-tests' },
      ],
    }));
    if (!resp.Summary?.Id) throw new Error('expected Summary.Id to be defined');
    ipSetId = resp.Summary.Id;
    ipSetARN = resp.Summary.ARN ?? '';
    ipSetLockToken = resp.Summary.LockToken ?? '';
  }));

  results.push(await runner.runTest('wafv2', 'GetIPSet', async () => {
    const resp = await client.send(new GetIPSetCommand({
      Name: ipSetName, Scope: SCOPE, Id: ipSetId,
    }));
    if (!resp.IPSet) throw new Error('expected IPSet to be defined');
    if (resp.IPSet.IPAddressVersion !== 'IPV4') {
      throw new Error(`expected IPV4, got ${resp.IPSet.IPAddressVersion}`);
    }
    if (!resp.LockToken) throw new Error('expected LockToken to be defined');
  }));

  results.push(await runner.runTest('wafv2', 'ListIPSets_ContainsCreated', async () => {
    const resp = await client.send(new ListIPSetsCommand({ Scope: SCOPE }));
    const found = resp.IPSets?.some(s => s.Id === ipSetId && s.Name === ipSetName);
    if (!found) throw new Error('IP Set not found in list');
  }));

  results.push(await runner.runTest('wafv2', 'ListTagsForResource', async () => {
    const resp = await client.send(new ListTagsForResourceCommand({ ResourceARN: ipSetARN }));
    if (!resp.TagInfoForResource?.TagList) throw new Error('expected tags in response');
    if (resp.TagInfoForResource.TagList.length < 2) {
      throw new Error(`expected at least 2 tags, got ${resp.TagInfoForResource.TagList.length}`);
    }
  }));

  results.push(await runner.runTest('wafv2', 'UpdateIPSet', async () => {
    const resp = await client.send(new UpdateIPSetCommand({
      Name: ipSetName, Scope: SCOPE, Id: ipSetId,
      LockToken: ipSetLockToken,
      Addresses: ['192.0.2.0/24', '203.0.113.0/24'],
    }));
    if (resp.NextLockToken) ipSetLockToken = resp.NextLockToken;
  }));

  results.push(await runner.runTest('wafv2', 'UpdateIPSet_ContentVerify', async () => {
    const resp = await client.send(new GetIPSetCommand({
      Name: ipSetName, Scope: SCOPE, Id: ipSetId,
    }));
    if ((resp.IPSet?.Addresses?.length ?? 0) !== 2) {
      throw new Error(`expected 2 addresses, got ${resp.IPSet?.Addresses?.length ?? 0}`);
    }
  }));

  results.push(await runner.runTest('wafv2', 'DeleteIPSet', async () => {
    await client.send(new DeleteIPSetCommand({
      Name: ipSetName, Scope: SCOPE, Id: ipSetId, LockToken: ipSetLockToken,
    }));
  }));

  results.push(await runner.runTest('wafv2', 'GetIPSet_NonExistent', async () => {
    try {
      await client.send(new GetIPSetCommand({ Name: ipSetName, Scope: SCOPE, Id: ipSetId }));
      throw new Error('expected WAFNonexistentItemException');
    } catch (err) {
      assertErrorContains(err, 'WAFNonexistentItemException');
    }
  }));

  results.push(await runner.runTest('wafv2', 'DeleteIPSet_NonExistent', async () => {
    try {
      await client.send(new DeleteIPSetCommand({
        Name: ipSetName, Scope: SCOPE, Id: ipSetId, LockToken: 'fake-lock-token',
      }));
      throw new Error('expected WAFNonexistentItemException');
    } catch (err) {
      assertErrorContains(err, 'WAFNonexistentItemException');
    }
  }));

  return results;
}
