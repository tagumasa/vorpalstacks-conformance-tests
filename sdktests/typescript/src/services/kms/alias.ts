import type { KMSClient } from '@aws-sdk/client-kms';
import type { TestRunner, TestResult } from '../../runner.js';
import { safeCleanup, assertErrorContains } from '../../helpers.js';
import { CreateAliasCommand, ListAliasesCommand, DeleteAliasCommand, UpdateAliasCommand } from '@aws-sdk/client-kms';
import type { KmsState } from './context.js';

export async function runAliasTests(
  runner: TestRunner,
  client: KMSClient,
  state: KmsState,
  keyAlias: string,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('kms', 'CreateAlias', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new CreateAliasCommand({ AliasName: keyAlias, TargetKeyId: state.keyID }));
  }));

  results.push(await runner.runTest('kms', 'ListAliases', async () => {
    const resp = await client.send(new ListAliasesCommand({}));
    if (!resp.Aliases) throw new Error('expected Aliases to be defined');
  }));

  results.push(await runner.runTest('kms', 'UpdateAlias', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new UpdateAliasCommand({ AliasName: keyAlias, TargetKeyId: state.keyID }));
    const listResp = await client.send(new ListAliasesCommand({}));
    if (!listResp.Aliases?.some(a => a.AliasName === keyAlias)) {
      throw new Error(`alias ${keyAlias} not found after update`);
    }
  }));

  results.push(await runner.runTest('kms', 'DeleteAlias', async () => {
    await client.send(new DeleteAliasCommand({ AliasName: keyAlias }));
  }));

  results.push(await runner.runTest('kms', 'CreateAlias_Duplicate', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const dupAlias = `alias/dup-test-${Date.now()}`;
    await client.send(new CreateAliasCommand({ AliasName: dupAlias, TargetKeyId: state.keyID }));
    try {
      await client.send(new CreateAliasCommand({ AliasName: dupAlias, TargetKeyId: state.keyID }));
      throw new Error('expected error for duplicate alias');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for duplicate alias') throw err;
      assertErrorContains(err, 'AlreadyExistsException');
    } finally {
      await safeCleanup(() => client.send(new DeleteAliasCommand({ AliasName: dupAlias })));
    }
  }));

  results.push(await runner.runTest('kms', 'ListAliases_ContainsCreated', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const testAlias = `alias/list-test-${Date.now()}`;
    await client.send(new CreateAliasCommand({ AliasName: testAlias, TargetKeyId: state.keyID }));
    try {
      const resp = await client.send(new ListAliasesCommand({}));
      const found = resp.Aliases?.find(a => a.AliasName === testAlias);
      if (!found) throw new Error(`created alias ${testAlias} not found`);
      if (found.TargetKeyId !== state.keyID) throw new Error('alias target key mismatch');
    } finally {
      await safeCleanup(() => client.send(new DeleteAliasCommand({ AliasName: testAlias })));
    }
  }));

  results.push(await runner.runTest('kms', 'ListAliases_FilterByKeyID', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const filterAlias = `alias/filter-test-${Date.now()}`;
    await client.send(new CreateAliasCommand({ AliasName: filterAlias, TargetKeyId: state.keyID }));
    try {
      const resp = await client.send(new ListAliasesCommand({ KeyId: state.keyID }));
      for (const a of resp.Aliases ?? []) {
        if (a.AliasName?.startsWith('alias/aws/')) continue;
        if (a.TargetKeyId && a.TargetKeyId !== state.keyID) {
          throw new Error(`alias ${a.AliasName} has wrong target key ${a.TargetKeyId}`);
        }
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteAliasCommand({ AliasName: filterAlias })));
    }
  }));

  results.push(await runner.runTest('kms', 'DeleteAlias_NonExistent', async () => {
    try {
      await client.send(new DeleteAliasCommand({ AliasName: 'alias/nonexistent-test-alias' }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
      assertErrorContains(err, 'NotFoundException');
    }
  }));

  results.push(await runner.runTest('kms', 'CreateAlias_AliasAWSReserved', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    try {
      await client.send(new CreateAliasCommand({ AliasName: 'alias/aws/test', TargetKeyId: state.keyID }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
    }
  }));

  results.push(await runner.runTest('kms', 'CreateAlias_WithoutPrefix', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    try {
      await client.send(new CreateAliasCommand({ AliasName: 'no-prefix-alias', TargetKeyId: state.keyID }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
    }
  }));

  return results;
}
