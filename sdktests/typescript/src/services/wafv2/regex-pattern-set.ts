import {
  WAFV2Client,
  CreateRegexPatternSetCommand,
  GetRegexPatternSetCommand,
  UpdateRegexPatternSetCommand,
  DeleteRegexPatternSetCommand,
  ListRegexPatternSetsCommand,
} from '@aws-sdk/client-wafv2';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, assertErrorContains } from '../../helpers.js';
import { SCOPE } from './context.js';

export async function runRegexPatternSetTests(
  runner: TestRunner,
  client: WAFV2Client,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const setName = makeUniqueName('regex');
  let setId = '';
  let setLockToken = '';

  results.push(await runner.runTest('wafv2', 'CreateRegexPatternSet', async () => {
    const resp = await client.send(new CreateRegexPatternSetCommand({
      Name: setName,
      Scope: SCOPE,
      RegularExpressionList: [
        { RegexString: '[a-z]+@[a-z]+\\.[a-z]+' },
      ],
    }));
    if (!resp.Summary?.Id) throw new Error('expected Summary.Id to be defined');
    setId = resp.Summary.Id;
    setLockToken = resp.Summary.LockToken ?? '';
  }));

  results.push(await runner.runTest('wafv2', 'GetRegexPatternSet', async () => {
    const resp = await client.send(new GetRegexPatternSetCommand({
      Name: setName, Scope: SCOPE, Id: setId,
    }));
    if (!resp.RegexPatternSet) throw new Error('expected RegexPatternSet to be defined');
    if (!resp.LockToken) throw new Error('expected LockToken to be defined');
  }));

  results.push(await runner.runTest('wafv2', 'ListRegexPatternSets', async () => {
    const resp = await client.send(new ListRegexPatternSetsCommand({ Scope: SCOPE, Limit: 10 }));
    if (!resp.RegexPatternSets) throw new Error('expected RegexPatternSets to be defined');
  }));

  results.push(await runner.runTest('wafv2', 'UpdateRegexPatternSet', async () => {
    const resp = await client.send(new UpdateRegexPatternSetCommand({
      Name: setName, Scope: SCOPE, Id: setId,
      LockToken: setLockToken,
      RegularExpressionList: [
        { RegexString: '\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}' },
        { RegexString: '[0-9a-f]{2}:[0-9a-f]{2}:[0-9a-f]{2}' },
      ],
    }));
    if (resp.NextLockToken) setLockToken = resp.NextLockToken;
  }));

  results.push(await runner.runTest('wafv2', 'UpdateRegexPatternSet_ContentVerify', async () => {
    const resp = await client.send(new GetRegexPatternSetCommand({
      Name: setName, Scope: SCOPE, Id: setId,
    }));
    if ((resp.RegexPatternSet?.RegularExpressionList?.length ?? 0) !== 2) {
      throw new Error(`expected 2 patterns, got ${resp.RegexPatternSet?.RegularExpressionList?.length ?? 0}`);
    }
  }));

  results.push(await runner.runTest('wafv2', 'DeleteRegexPatternSet', async () => {
    await client.send(new DeleteRegexPatternSetCommand({
      Name: setName, Scope: SCOPE, Id: setId, LockToken: setLockToken,
    }));
  }));

  results.push(await runner.runTest('wafv2', 'GetRegexPatternSet_NonExistent', async () => {
    try {
      await client.send(new GetRegexPatternSetCommand({ Name: setName, Scope: SCOPE, Id: setId }));
      throw new Error('expected WAFNonexistentItemException');
    } catch (err) {
      assertErrorContains(err, 'WAFNonexistentItemException');
    }
  }));

  return results;
}
