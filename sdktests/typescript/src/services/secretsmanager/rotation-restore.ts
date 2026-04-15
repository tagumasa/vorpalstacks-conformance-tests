import {
  SecretsManagerClient,
  CreateSecretCommand,
  DeleteSecretCommand,
  DescribeSecretCommand,
  RestoreSecretCommand,
  UpdateSecretVersionStageCommand,
  PutSecretValueCommand,
  RotateSecretCommand,
  CancelRotateSecretCommand,
} from '@aws-sdk/client-secrets-manager';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup, assertErrorContains } from '../../helpers.js';

export async function runRotationRestoreStagingTests(
  client: SecretsManagerClient,
  runner: TestRunner,
  results: TestResult[],
): Promise<void> {
  const s = 'secretsmanager';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(s, name, fn));

  await r('RestoreSecret_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new RestoreSecretCommand({ SecretId: 'nonexistent-restore-xyz' }));
    } catch (e) { err = e; }
    assertErrorContains(err, 'ResourceNotFoundException');
  });

  await r('RestoreSecret_Basic', async () => {
    const secName = makeUniqueName('RestoreSec');
    await client.send(new CreateSecretCommand({ Name: secName, SecretString: 'restore-test' }));
    try {
      await client.send(new DeleteSecretCommand({ SecretId: secName }));
      const { ListSecretsCommand } = await import('@aws-sdk/client-secrets-manager');
      const listResp = await client.send(new ListSecretsCommand({}));
      const found = listResp.SecretList?.some((s) => s.Name === secName);
      if (found) throw new Error('soft-deleted secret should not appear in ListSecrets');
      await client.send(new RestoreSecretCommand({ SecretId: secName }));
      const getResp = await client.send(new (await import('@aws-sdk/client-secrets-manager')).GetSecretValueCommand({ SecretId: secName }));
      if (getResp.SecretString !== 'restore-test') throw new Error('value mismatch after restore');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });

  await r('UpdateSecretVersionStage_Basic', async () => {
    const secName = makeUniqueName('VersionStage');
    await client.send(new CreateSecretCommand({ Name: secName, SecretString: 'v1' }));
    try {
      const putResp = await client.send(new PutSecretValueCommand({ SecretId: secName, SecretString: 'v2' }));
      const v2VersionId = putResp.VersionId;
      if (!v2VersionId) throw new Error('v2 VersionId to be defined');

      const descResp = await client.send(new DescribeSecretCommand({ SecretId: secName }));
      if (!descResp.VersionIdsToStages) throw new Error('VersionIdsToStages to be defined');
      const stages = descResp.VersionIdsToStages[v2VersionId];
      if (!stages || !stages.includes('AWSCURRENT')) throw new Error('v2 should have AWSCURRENT stage');

      await client.send(new UpdateSecretVersionStageCommand({
        SecretId: secName, VersionStage: 'AWSCURRENT', MoveToVersionId: v2VersionId,
      }));

      const descResp2 = await client.send(new DescribeSecretCommand({ SecretId: secName }));
      if (!descResp2.VersionIdsToStages) throw new Error('VersionIdsToStages to be defined after update');
      const stages2 = descResp2.VersionIdsToStages[v2VersionId];
      if (!stages2 || !stages2.includes('AWSCURRENT')) throw new Error('v2 should still have AWSCURRENT');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });

  await r('RotateSecret_Basic', async () => {
    const secName = makeUniqueName('RotateTest');
    await client.send(new CreateSecretCommand({ Name: secName, SecretString: 'rotate-me' }));
    try {
      const resp = await client.send(new RotateSecretCommand({ SecretId: secName }));
      if (!resp.VersionId) throw new Error('version ID to be defined after rotation');
      const descResp = await client.send(new DescribeSecretCommand({ SecretId: secName }));
      if (!descResp.LastRotatedDate) throw new Error('LastRotatedDate should be set after rotation');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });

  await r('CancelRotateSecret_Basic', async () => {
    const secName = makeUniqueName('CancelRot');
    await client.send(new CreateSecretCommand({ Name: secName, SecretString: 'cancel-rotate' }));
    try {
      await client.send(new RotateSecretCommand({ SecretId: secName }));
      await client.send(new CancelRotateSecretCommand({ SecretId: secName }));
      const descResp = await client.send(new DescribeSecretCommand({ SecretId: secName }));
      if (descResp.RotationEnabled === true) throw new Error('rotation should be disabled after cancel');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });
}
