import type { KMSClient } from '@aws-sdk/client-kms';
import type { TestRunner, TestResult } from '../../runner.js';
import { safeCleanup, assertErrorContains } from '../../helpers.js';
import {
  PutKeyPolicyCommand, GetKeyPolicyCommand, ListKeyPoliciesCommand,
  CreateGrantCommand, ListGrantsCommand, ListRetirableGrantsCommand,
  RetireGrantCommand, RevokeGrantCommand,
  TagResourceCommand, ListResourceTagsCommand, UntagResourceCommand,
  EnableKeyRotationCommand, GetKeyRotationStatusCommand, DisableKeyRotationCommand,
  ListKeyRotationsCommand,
  DisableKeyCommand, EnableKeyCommand,
  ScheduleKeyDeletionCommand, GetPublicKeyCommand,
} from '@aws-sdk/client-kms';
import type { KmsState } from './context.js';

export async function runPolicyGrantTagRotationTests(
  runner: TestRunner,
  client: KMSClient,
  state: KmsState,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('kms', 'PutKeyPolicy', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new PutKeyPolicyCommand({
      KeyId: state.keyID, PolicyName: 'default',
      Policy: '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"AWS":"*"},"Action":"kms:*","Resource":"*"}]}',
    }));
  }));

  results.push(await runner.runTest('kms', 'GetKeyPolicy', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const resp = await client.send(new GetKeyPolicyCommand({ KeyId: state.keyID, PolicyName: 'default' }));
    if (!resp.Policy) throw new Error('expected Policy to be defined');
  }));

  results.push(await runner.runTest('kms', 'ListKeyPolicies', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const resp = await client.send(new ListKeyPoliciesCommand({ KeyId: state.keyID }));
    if (!resp.PolicyNames) throw new Error('expected PolicyNames to be defined');
  }));

  results.push(await runner.runTest('kms', 'GetKeyPolicy_ContentVerify', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const policy = '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"AWS":"*"},"Action":"kms:*","Resource":"*"}]}';
    await client.send(new PutKeyPolicyCommand({ KeyId: state.keyID, PolicyName: 'default', Policy: policy }));
    const resp = await client.send(new GetKeyPolicyCommand({ KeyId: state.keyID, PolicyName: 'default' }));
    if (resp.Policy !== policy) throw new Error('policy content mismatch');
  }));

  results.push(await runner.runTest('kms', 'PutKeyPolicy_InvalidJSON', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    try {
      await client.send(new PutKeyPolicyCommand({
        KeyId: state.keyID, PolicyName: 'default', Policy: 'not valid json {{{',
      }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
    }
  }));

  results.push(await runner.runTest('kms', 'CreateGrant', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const resp = await client.send(new CreateGrantCommand({
      KeyId: state.keyID,
      GranteePrincipal: 'arn:aws:iam::000000000000:user/TestUser',
      Operations: ['Encrypt'],
    }));
    if (!resp.GrantToken) throw new Error('expected GrantToken to be defined');
    if (!resp.GrantId) throw new Error('expected GrantId to be defined');
  }));

  results.push(await runner.runTest('kms', 'ListGrants', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const resp = await client.send(new ListGrantsCommand({ KeyId: state.keyID }));
    if (!resp.Grants) throw new Error('expected Grants to be defined');
  }));

  results.push(await runner.runTest('kms', 'ListRetirableGrants', async () => {
    const resp = await client.send(new ListRetirableGrantsCommand({
      RetiringPrincipal: 'arn:aws:iam::000000000000:user/TestUser',
    }));
    if (!resp.Grants) throw new Error('expected Grants to be defined');
  }));

  results.push(await runner.runTest('kms', 'RetireGrant', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const createResp = await client.send(new CreateGrantCommand({
      KeyId: state.keyID,
      GranteePrincipal: 'arn:aws:iam::000000000000:user/RetireUser',
      Operations: ['Decrypt'],
    }));
    await client.send(new RetireGrantCommand({ GrantId: createResp.GrantId, KeyId: state.keyID }));
  }));

  results.push(await runner.runTest('kms', 'RevokeGrant', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const createResp = await client.send(new CreateGrantCommand({
      KeyId: state.keyID,
      GranteePrincipal: 'arn:aws:iam::000000000000:user/RevokeUser',
      Operations: ['Encrypt'],
    }));
    await client.send(new RevokeGrantCommand({ KeyId: state.keyID, GrantId: createResp.GrantId }));
    const listResp = await client.send(new ListGrantsCommand({ KeyId: state.keyID }));
    if (listResp.Grants?.some(g => g.GrantId === createResp.GrantId)) {
      throw new Error('revoked grant still in list');
    }
  }));

  results.push(await runner.runTest('kms', 'TagResource', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new TagResourceCommand({
      KeyId: state.keyID,
      Tags: [
        { TagKey: 'Environment', TagValue: 'test' },
        { TagKey: 'Project', TagValue: 'sdk-tests' },
      ],
    }));
  }));

  results.push(await runner.runTest('kms', 'ListResourceTags', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const resp = await client.send(new ListResourceTagsCommand({ KeyId: state.keyID }));
    if (!resp.Tags) throw new Error('expected Tags to be defined');
  }));

  results.push(await runner.runTest('kms', 'UntagResource', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new UntagResourceCommand({ KeyId: state.keyID, TagKeys: ['Environment'] }));
  }));

  results.push(await runner.runTest('kms', 'TagResource_ByAlias', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const { CreateAliasCommand, DeleteAliasCommand } = await import('@aws-sdk/client-kms');
    const tagAlias = `alias/tag-test-${Date.now()}`;
    await client.send(new CreateAliasCommand({ AliasName: tagAlias, TargetKeyId: state.keyID }));
    try {
      await client.send(new TagResourceCommand({
        KeyId: tagAlias, Tags: [{ TagKey: 'AliasTag', TagValue: 'test-value' }],
      }));
      const tagResp = await client.send(new ListResourceTagsCommand({ KeyId: tagAlias }));
      if (!tagResp.Tags?.some(t => t.TagKey === 'AliasTag')) throw new Error('tag set via alias not found');
    } finally {
      await safeCleanup(() => client.send(new DeleteAliasCommand({ AliasName: tagAlias })));
      await client.send(new UntagResourceCommand({ KeyId: state.keyID, TagKeys: ['AliasTag'] })).catch(() => {});
    }
  }));

  results.push(await runner.runTest('kms', 'ListResourceTags_ContentVerify', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new TagResourceCommand({
      KeyId: state.keyID,
      Tags: [{ TagKey: 'Env', TagValue: 'prod' }, { TagKey: 'Team', TagValue: 'backend' }],
    }));
    try {
      const resp = await client.send(new ListResourceTagsCommand({ KeyId: state.keyID }));
      const found = resp.Tags?.filter(t =>
        (t.TagKey === 'Env' && t.TagValue === 'prod') || (t.TagKey === 'Team' && t.TagValue === 'backend')
      ).length ?? 0;
      if (found < 2) throw new Error(`expected to find 2 tags, found ${found}`);
    } finally {
      await client.send(new UntagResourceCommand({ KeyId: state.keyID, TagKeys: ['Env', 'Team'] })).catch(() => {});
    }
  }));

  results.push(await runner.runTest('kms', 'EnableKeyRotation', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new EnableKeyRotationCommand({ KeyId: state.keyID }));
  }));

  results.push(await runner.runTest('kms', 'GetKeyRotationStatus', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new GetKeyRotationStatusCommand({ KeyId: state.keyID }));
  }));

  results.push(await runner.runTest('kms', 'GetKeyRotationStatus_ContentVerify', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const resp = await client.send(new GetKeyRotationStatusCommand({ KeyId: state.keyID }));
    if (!resp.KeyRotationEnabled) throw new Error('expected KeyRotationEnabled=true');
    if (resp.RotationPeriodInDays !== 365) throw new Error(`expected RotationPeriodInDays=365, got ${resp.RotationPeriodInDays}`);
  }));

  results.push(await runner.runTest('kms', 'DisableKeyRotation', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const resp = await client.send(new DisableKeyRotationCommand({ KeyId: state.keyID }));
    if (!resp) throw new Error('expected response to be defined');
  }));

  results.push(await runner.runTest('kms', 'GetKeyRotationStatus_DisabledRotation', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new DisableKeyRotationCommand({ KeyId: state.keyID }));
    const resp = await client.send(new GetKeyRotationStatusCommand({ KeyId: state.keyID }));
    if (resp.KeyRotationEnabled) throw new Error('expected KeyRotationEnabled=false');
  }));

  results.push(await runner.runTest('kms', 'ListKeyRotations', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new EnableKeyRotationCommand({ KeyId: state.keyID }));
    const resp = await client.send(new ListKeyRotationsCommand({ KeyId: state.keyID }));
    if (!resp.Rotations) throw new Error('expected Rotations to be defined');
    if (resp.Rotations.length !== 1) throw new Error(`expected 1 rotation, got ${resp.Rotations.length}`);
    if (resp.Rotations[0].RotationType !== 'AUTOMATIC') {
      throw new Error(`expected RotationType=AUTOMATIC, got ${resp.Rotations[0].RotationType}`);
    }
  }));

  const fakeKeyID = 'arn:aws:kms:us-east-1:000000000000:key/ffffffff-ffff-ffff-ffff-ffffffffffff';

  results.push(await runner.runTest('kms', 'DisableKey_NonExistent', async () => {
    try {
      await client.send(new DisableKeyCommand({ KeyId: fakeKeyID }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
      assertErrorContains(err, 'NotFoundException');
    }
  }));

  results.push(await runner.runTest('kms', 'EnableKey_NonExistent', async () => {
    try {
      await client.send(new EnableKeyCommand({ KeyId: fakeKeyID }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
      assertErrorContains(err, 'NotFoundException');
    }
  }));

  results.push(await runner.runTest('kms', 'ScheduleKeyDeletion_NonExistent', async () => {
    try {
      await client.send(new ScheduleKeyDeletionCommand({ KeyId: fakeKeyID, PendingWindowInDays: 7 }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
    }
  }));

  results.push(await runner.runTest('kms', 'GetPublicKey_NonExistent', async () => {
    try {
      await client.send(new GetPublicKeyCommand({ KeyId: fakeKeyID }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
    }
  }));

  results.push(await runner.runTest('kms', 'ListGrants_NonExistent', async () => {
    try {
      await client.send(new ListGrantsCommand({ KeyId: fakeKeyID }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
    }
  }));

  return results;
}
