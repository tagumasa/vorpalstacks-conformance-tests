import {
  WAFV2Client,
  CreateIPSetCommand,
  UpdateIPSetCommand,
  DeleteIPSetCommand,
  ListIPSetsCommand,
} from '@aws-sdk/client-wafv2';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';
import { SCOPE } from './context.js';

export async function runEdgeCaseTests(
  runner: TestRunner,
  client: WAFV2Client,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const verifyIPSetName = makeUniqueName('verify-ipset');
  let verifyIPSetId = '';
  let verifyIPSetLockToken = '';

  results.push(await runner.runTest('wafv2', 'UpdateIPSet_StaleLockToken', async () => {
    const createResp = await client.send(new CreateIPSetCommand({
      Name: verifyIPSetName, Scope: SCOPE, IPAddressVersion: 'IPV4', Addresses: [],
    }));
    if (!createResp.Summary?.Id) throw new Error('expected Summary.Id to be defined');
    verifyIPSetId = createResp.Summary.Id;
    verifyIPSetLockToken = createResp.Summary.LockToken ?? '';

    try {
      await client.send(new UpdateIPSetCommand({
        Name: verifyIPSetName, Scope: SCOPE, Id: verifyIPSetId,
        LockToken: 'stale-token-should-fail', Addresses: ['192.168.0.0/16'],
      }));
      throw new Error('expected error for stale lock token');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for stale lock token') throw err;
    }
  }));

  await safeCleanup(() => client.send(new DeleteIPSetCommand({
    Name: verifyIPSetName, Scope: SCOPE, Id: verifyIPSetId, LockToken: verifyIPSetLockToken,
  })));

  results.push(await runner.runTest('wafv2', 'ListIPSets_Pagination', async () => {
    const pgPrefix = makeUniqueName('pgwaf');
    const pgSets: Array<{ id: string; name: string; lockToken: string }> = [];

    for (const suffix of ['0', '1', '2', '3', '4']) {
      const name = `${pgPrefix}-${suffix}`;
      const resp = await client.send(new CreateIPSetCommand({
        Name: name, Scope: SCOPE, IPAddressVersion: 'IPV4', Addresses: [],
      }));
      if (!resp.Summary?.Id) throw new Error(`expected Summary.Id for ${name} to be defined`);
      pgSets.push({ id: resp.Summary.Id, name, lockToken: resp.Summary.LockToken ?? '' });
    }

    const pgIDSet = new Set(pgSets.map(s => s.id));
    let foundCount = 0;
    let nextMarker: string | undefined;

    try {
      do {
        const resp = await client.send(new ListIPSetsCommand({
          Scope: SCOPE, Limit: 2, NextMarker: nextMarker,
        }));
        for (const s of resp.IPSets ?? []) {
          if (s.Id && pgIDSet.has(s.Id)) foundCount++;
        }
        nextMarker = resp.NextMarker;
      } while (nextMarker);
    } finally {
      for (const s of pgSets) {
        await safeCleanup(() => client.send(new DeleteIPSetCommand({
          Name: s.name, Scope: SCOPE, Id: s.id, LockToken: s.lockToken,
        })));
      }
    }

    if (foundCount !== 5) throw new Error(`expected 5 paginated IP sets, got ${foundCount}`);
  }));

  return results;
}
