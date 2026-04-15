import type { KMSClient } from '@aws-sdk/client-kms';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName } from '../../helpers.js';
import { CreateKeyCommand, DescribeKeyCommand } from '@aws-sdk/client-kms';
import type { KmsState } from './context.js';

export async function runKeyCrudTests(
  runner: TestRunner,
  client: KMSClient,
  state: KmsState,
  keyDescription: string,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('kms', 'CreateKey', async () => {
    const resp = await client.send(new CreateKeyCommand({ Description: keyDescription }));
    if (!resp.KeyMetadata?.KeyId) throw new Error('expected KeyMetadata.KeyId to be defined');
    state.keyID = resp.KeyMetadata.KeyId;
  }));

  results.push(await runner.runTest('kms', 'DescribeKey', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const resp = await client.send(new DescribeKeyCommand({ KeyId: state.keyID }));
    if (!resp.KeyMetadata) throw new Error('expected KeyMetadata to be defined');
  }));

  return results;
}

export async function runKeyContentTests(
  runner: TestRunner,
  client: KMSClient,
  state: KmsState,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { EnableKeyCommand, DisableKeyCommand, UpdateKeyDescriptionCommand, DescribeKeyCommand, ScheduleKeyDeletionCommand, CancelKeyDeletionCommand, CreateKeyCommand } = await import('@aws-sdk/client-kms');
  const { safeCleanup, assertErrorContains } = await import('../../helpers.js');

  results.push(await runner.runTest('kms', 'EnableKey', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new EnableKeyCommand({ KeyId: state.keyID }));
  }));

  results.push(await runner.runTest('kms', 'DisableKey', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new DisableKeyCommand({ KeyId: state.keyID }));
  }));

  results.push(await runner.runTest('kms', 'EnableKey', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new EnableKeyCommand({ KeyId: state.keyID }));
  }));

  results.push(await runner.runTest('kms', 'UpdateKeyDescription', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new UpdateKeyDescriptionCommand({
      KeyId: state.keyID, Description: makeUniqueName('Updated Key'),
    }));
  }));

  results.push(await runner.runTest('kms', 'CreateKey_ContentVerify', async () => {
    const desc = makeUniqueName('ContentVerify');
    const resp = await client.send(new CreateKeyCommand({ Description: desc }));
    const m = resp.KeyMetadata!;
    if (!m.KeyId) throw new Error('expected KeyId to be defined');
    if (!m.Arn) throw new Error('expected Arn to be defined');
    if (m.KeyState !== 'Enabled') throw new Error(`expected KeyState=Enabled, got ${m.KeyState}`);
    if (!m.Enabled) throw new Error('expected Enabled=true');
    if (m.KeyUsage !== 'ENCRYPT_DECRYPT') throw new Error(`expected KeyUsage=ENCRYPT_DECRYPT, got ${m.KeyUsage}`);
    if (m.KeySpec !== 'SYMMETRIC_DEFAULT') throw new Error(`expected KeySpec=SYMMETRIC_DEFAULT, got ${m.KeySpec}`);
    if (m.Origin !== 'AWS_KMS') throw new Error(`expected Origin=AWS_KMS, got ${m.Origin}`);
    if (m.KeyManager !== 'CUSTOMER') throw new Error(`expected KeyManager=CUSTOMER, got ${m.KeyManager}`);
    if (m.Description !== desc) throw new Error('Description mismatch');
    if (!m.EncryptionAlgorithms?.length) throw new Error('EncryptionAlgorithms is empty');
    await safeCleanup(() => client.send(new ScheduleKeyDeletionCommand({ KeyId: m.KeyId!, PendingWindowInDays: 7 })));
  }));

  results.push(await runner.runTest('kms', 'CreateKey_MultiRegion', async () => {
    const resp = await client.send(new CreateKeyCommand({
      Description: 'Multi-Region Key', MultiRegion: true,
    }));
    if (!resp.KeyMetadata?.MultiRegion) throw new Error('expected MultiRegion=true');
    await safeCleanup(() => client.send(new ScheduleKeyDeletionCommand({
      KeyId: resp.KeyMetadata!.KeyId!, PendingWindowInDays: 7,
    })));
  }));

  results.push(await runner.runTest('kms', 'CreateKey_WithTags', async () => {
    const { TagResourceCommand, ListResourceTagsCommand } = await import('@aws-sdk/client-kms');
    const resp = await client.send(new CreateKeyCommand({
      Description: 'Key with tags', Tags: [{ TagKey: 'Purpose', TagValue: 'testing' }],
    }));
    const tagResp = await client.send(new ListResourceTagsCommand({ KeyId: resp.KeyMetadata!.KeyId! }));
    if (!tagResp.Tags?.some(t => t.TagKey === 'Purpose' && t.TagValue === 'testing')) {
      throw new Error('tag not found after create');
    }
    await safeCleanup(() => client.send(new ScheduleKeyDeletionCommand({
      KeyId: resp.KeyMetadata!.KeyId!, PendingWindowInDays: 7,
    })));
  }));

  results.push(await runner.runTest('kms', 'CreateKey_ExternalOrigin', async () => {
    const resp = await client.send(new CreateKeyCommand({
      Description: 'External origin key', Origin: 'EXTERNAL',
    }));
    if (resp.KeyMetadata?.Origin !== 'EXTERNAL') {
      throw new Error(`expected Origin=EXTERNAL, got ${resp.KeyMetadata?.Origin}`);
    }
    await safeCleanup(() => client.send(new ScheduleKeyDeletionCommand({
      KeyId: resp.KeyMetadata!.KeyId!, PendingWindowInDays: 7,
    })));
  }));

  results.push(await runner.runTest('kms', 'CreateKey_InvalidKeyUsageKeySpec', async () => {
    try {
      await client.send(new CreateKeyCommand({
        Description: 'Invalid combo', KeyUsage: 'ENCRYPT_DECRYPT', KeySpec: 'HMAC_256',
      }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
    }
  }));

  results.push(await runner.runTest('kms', 'ScheduleKeyDeletion', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const resp = await client.send(new ScheduleKeyDeletionCommand({
      KeyId: state.keyID, PendingWindowInDays: 7,
    }));
    if (!resp.DeletionDate) throw new Error('expected DeletionDate to be defined');
    if (resp.KeyState !== 'PendingDeletion') throw new Error(`expected KeyState=PendingDeletion, got ${resp.KeyState}`);
  }));

  results.push(await runner.runTest('kms', 'ScheduleKeyDeletion_ReturnsKeyID', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const resp = await client.send(new ScheduleKeyDeletionCommand({ KeyId: state.keyID, PendingWindowInDays: 7 }));
    if (resp.KeyId !== state.keyID) throw new Error(`expected KeyId=${state.keyID}, got ${resp.KeyId}`);
    if (!resp.DeletionDate) throw new Error('expected DeletionDate to be defined');
    if (resp.PendingWindowInDays !== 7) throw new Error('expected PendingWindowInDays=7');
  }));

  results.push(await runner.runTest('kms', 'CancelKeyDeletion', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const resp = await client.send(new CancelKeyDeletionCommand({ KeyId: state.keyID }));
    if (!resp.KeyId) throw new Error('expected KeyId in response to be defined');
  }));

  results.push(await runner.runTest('kms', 'CancelKeyDeletion_RestoresEnabledState', async () => {
    const createResp = await client.send(new CreateKeyCommand({ Description: makeUniqueName('CancelRestore') }));
    const newKeyID = createResp.KeyMetadata?.KeyId!;
    try {
      await client.send(new DisableKeyCommand({ KeyId: newKeyID }));
      await client.send(new ScheduleKeyDeletionCommand({ KeyId: newKeyID, PendingWindowInDays: 7 }));
      await client.send(new CancelKeyDeletionCommand({ KeyId: newKeyID }));
      const descResp = await client.send(new DescribeKeyCommand({ KeyId: newKeyID }));
      if (descResp.KeyMetadata?.Enabled) throw new Error('expected key to be Disabled after cancel');
      if (descResp.KeyMetadata?.KeyState !== 'Disabled') {
        throw new Error(`expected KeyState=Disabled, got ${descResp.KeyMetadata?.KeyState}`);
      }
    } finally {
      await safeCleanup(() => client.send(new ScheduleKeyDeletionCommand({ KeyId: newKeyID, PendingWindowInDays: 7 })));
    }
  }));

  results.push(await runner.runTest('kms', 'ScheduleKeyDeletion_InvalidWindow', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    try {
      await client.send(new ScheduleKeyDeletionCommand({ KeyId: state.keyID, PendingWindowInDays: 3 }));
      throw new Error('expected error for invalid pending window');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for invalid pending window') throw err;
    }
  }));

  results.push(await runner.runTest('kms', 'UpdateKeyDescription_VerifyChange', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const newDesc = makeUniqueName('Verified');
    await client.send(new UpdateKeyDescriptionCommand({ KeyId: state.keyID, Description: newDesc }));
    const resp = await client.send(new DescribeKeyCommand({ KeyId: state.keyID }));
    if (resp.KeyMetadata?.Description !== newDesc) throw new Error('description not updated');
  }));

  results.push(await runner.runTest('kms', 'DescribeKey_NonExistentKey', async () => {
    try {
      await client.send(new DescribeKeyCommand({
        KeyId: 'arn:aws:kms:us-east-1:000000000000:key/00000000-0000-0000-0000-000000000000',
      }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
      assertErrorContains(err, 'NotFoundException');
    }
  }));

  results.push(await runner.runTest('kms', 'DescribeKey_ByAlias', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const { CreateAliasCommand, DeleteAliasCommand } = await import('@aws-sdk/client-kms');
    const testAlias = `alias/desc-test-${Date.now()}`;
    await client.send(new CreateAliasCommand({ AliasName: testAlias, TargetKeyId: state.keyID }));
    try {
      const resp = await client.send(new DescribeKeyCommand({ KeyId: testAlias }));
      if (resp.KeyMetadata?.KeyId !== state.keyID) throw new Error('key ID mismatch when describing by alias');
    } finally {
      await safeCleanup(() => client.send(new DeleteAliasCommand({ AliasName: testAlias })));
    }
  }));

  results.push(await runner.runTest('kms', 'ListKeys_Basic', async () => {
    const { ListKeysCommand } = await import('@aws-sdk/client-kms');
    const resp = await client.send(new ListKeysCommand({}));
    if (!resp.Keys?.length) throw new Error('expected at least one key');
  }));

  results.push(await runner.runTest('kms', 'ListKeys_ContainsCreatedKey', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const { ListKeysCommand } = await import('@aws-sdk/client-kms');
    const resp = await client.send(new ListKeysCommand({}));
    const found = resp.Keys?.find(k => k.KeyId === state.keyID);
    if (!found) throw new Error(`created key ${state.keyID} not found in ListKeys`);
    if (!found.KeyArn) throw new Error('expected KeyArn to be defined');
  }));

  results.push(await runner.runTest('kms', 'ListKeys_Pagination', async () => {
    const pgTs = `${Date.now()}`;
    const pgKeyIDs: string[] = [];
    try {
      for (let i = 0; i < 5; i++) {
        const resp = await client.send(new CreateKeyCommand({
          Description: `pag-key-${pgTs}-${i}`,
        }));
        const id = resp.KeyMetadata?.KeyId;
        if (!id) throw new Error('expected KeyId');
        pgKeyIDs.push(id);
      }
      const allKeys: string[] = [];
      let marker: string | undefined;
      do {
        const resp = await client.send(new (await import('@aws-sdk/client-kms')).ListKeysCommand({ Marker: marker, Limit: 2 }));
        for (const k of resp.Keys ?? []) {
          if (k.KeyId) allKeys.push(k.KeyId);
        }
        marker = (resp.Truncated && resp.NextMarker) ? resp.NextMarker : undefined;
      } while (marker);
      if (allKeys.length < 5) throw new Error(`expected at least 5 keys across pages, got ${allKeys.length}`);
    } finally {
      for (const kid of pgKeyIDs) {
        await safeCleanup(() => client.send(new ScheduleKeyDeletionCommand({ KeyId: kid, PendingWindowInDays: 7 })));
      }
    }
  }));

  return results;
}
