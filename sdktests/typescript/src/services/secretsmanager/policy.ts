import {
  SecretsManagerClient,
  CreateSecretCommand,
  DescribeSecretCommand,
  DeleteSecretCommand,
  PutResourcePolicyCommand,
  GetResourcePolicyCommand,
  DeleteResourcePolicyCommand,
} from '@aws-sdk/client-secrets-manager';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup, assertErrorContains } from '../../helpers.js';

export async function runPolicyTests(
  client: SecretsManagerClient,
  runner: TestRunner,
  results: TestResult[],
): Promise<void> {
  const s = 'secretsmanager';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(s, name, fn));

  await r('GetResourcePolicy_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new GetResourcePolicyCommand({ SecretId: 'nonexistent-policy-secret' }));
    } catch (e) { err = e; }
    assertErrorContains(err, 'ResourceNotFoundException');
  });

  await r('PutResourcePolicy_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new PutResourcePolicyCommand({
        SecretId: 'nonexistent-policy-secret',
        ResourcePolicy: '{"Version":"2012-10-17"}',
      }));
    } catch (e) { err = e; }
    assertErrorContains(err, 'ResourceNotFoundException');
  });

  await r('PutResourcePolicy_Basic', async () => {
    const secName = makeUniqueName('PolicyTest');
    await client.send(new CreateSecretCommand({ Name: secName, SecretString: 'policy-test' }));
    try {
      const policy = '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":"*","Action":"secretsmanager:GetSecretValue","Resource":"*"}]}';
      await client.send(new PutResourcePolicyCommand({ SecretId: secName, ResourcePolicy: policy }));
      const getResp = await client.send(new GetResourcePolicyCommand({ SecretId: secName }));
      if (getResp.ResourcePolicy !== policy) throw new Error('policy mismatch');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });

  await r('DeleteResourcePolicy_Basic', async () => {
    const secName = makeUniqueName('DelPolicy');
    await client.send(new CreateSecretCommand({ Name: secName, SecretString: 'del-policy' }));
    try {
      const policy = '{"Version":"2012-10-17","Statement":[]}';
      await client.send(new PutResourcePolicyCommand({ SecretId: secName, ResourcePolicy: policy }));
      await client.send(new DeleteResourcePolicyCommand({ SecretId: secName }));
      const getResp = await client.send(new GetResourcePolicyCommand({ SecretId: secName }));
      if (getResp.ResourcePolicy) throw new Error('policy should be deleted');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });
}
