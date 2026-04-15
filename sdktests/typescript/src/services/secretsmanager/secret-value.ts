import {
  SecretsManagerClient,
  CreateSecretCommand,
  DescribeSecretCommand,
  GetSecretValueCommand,
  UpdateSecretCommand,
  DeleteSecretCommand,
  PutSecretValueCommand,
} from '@aws-sdk/client-secrets-manager';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup, assertErrorContains } from '../../helpers.js';

export async function runSecretValueTests(
  client: SecretsManagerClient,
  runner: TestRunner,
  results: TestResult[],
): Promise<void> {
  const s = 'secretsmanager';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(s, name, fn));

  await r('GetSecretValue_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new GetSecretValueCommand({
        SecretId: 'arn:aws:secretsmanager:us-east-1:000000000000:secret:nonexistent-secret-xyz',
      }));
    } catch (e) { err = e; }
    assertErrorContains(err, 'ResourceNotFoundException');
  });

  await r('DescribeSecret_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new DescribeSecretCommand({
        SecretId: 'arn:aws:secretsmanager:us-east-1:000000000000:secret:nonexistent-secret-xyz',
      }));
    } catch (e) { err = e; }
    assertErrorContains(err, 'ResourceNotFoundException');
  });

  await r('DeleteSecret_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new DeleteSecretCommand({
        SecretId: 'arn:aws:secretsmanager:us-east-1:000000000000:secret:nonexistent-xyz',
        ForceDeleteWithoutRecovery: true,
      }));
    } catch (e) { err = e; }
    assertErrorContains(err, 'ResourceNotFoundException');
  });

  await r('CreateSecret_Duplicate', async () => {
    const dupName = makeUniqueName('DupSecret');
    await client.send(new CreateSecretCommand({ Name: dupName, SecretString: 'initial-value' }));
    try {
      let err: unknown;
      try {
        await client.send(new CreateSecretCommand({ Name: dupName, SecretString: 'duplicate-value' }));
      } catch (e) { err = e; }
      if (!err) throw new Error('expected error for duplicate secret name');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: dupName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });

  await r('GetSecretValue_ContentVerify', async () => {
    const secName = makeUniqueName('VerifySecret');
    const secValue = 'my-verified-secret-123';
    await client.send(new CreateSecretCommand({ Name: secName, SecretString: secValue }));
    try {
      const resp = await client.send(new GetSecretValueCommand({ SecretId: secName }));
      if (resp.SecretString !== secValue) {
        throw new Error(`secret value mismatch: got ${JSON.stringify(resp.SecretString)}, want ${JSON.stringify(secValue)}`);
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });

  await r('UpdateSecret_ContentVerify', async () => {
    const secName = makeUniqueName('UpdateVerify');
    await client.send(new CreateSecretCommand({ Name: secName, SecretString: 'original-value' }));
    try {
      const updatedValue = 'updated-secret-value-456';
      await client.send(new UpdateSecretCommand({ SecretId: secName, SecretString: updatedValue }));
      const resp = await client.send(new GetSecretValueCommand({ SecretId: secName }));
      if (resp.SecretString !== updatedValue) {
        throw new Error(`secret value not updated: got ${JSON.stringify(resp.SecretString)}, want ${JSON.stringify(updatedValue)}`);
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });

  await r('PutSecretValue_Basic', async () => {
    const secName = makeUniqueName('PutValue');
    await client.send(new CreateSecretCommand({ Name: secName, SecretString: 'initial' }));
    try {
      const resp = await client.send(new PutSecretValueCommand({ SecretId: secName, SecretString: 'new-value' }));
      if (!resp.VersionId) throw new Error('version ID to be defined');
      const getResp = await client.send(new GetSecretValueCommand({ SecretId: secName }));
      if (getResp.SecretString !== 'new-value') throw new Error(`value mismatch: got ${JSON.stringify(getResp.SecretString)}`);
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });

  await r('PutSecretValue_ContentVerify', async () => {
    const secName = makeUniqueName('PutVerify');
    await client.send(new CreateSecretCommand({ Name: secName, SecretString: 'v1' }));
    try {
      await client.send(new PutSecretValueCommand({ SecretId: secName, SecretString: 'v2' }));
      await client.send(new PutSecretValueCommand({ SecretId: secName, SecretString: 'v3' }));
      const { ListSecretVersionIdsCommand } = await import('@aws-sdk/client-secrets-manager');
      const verResp = await client.send(new ListSecretVersionIdsCommand({ SecretId: secName }));
      if (!verResp.Versions || verResp.Versions.length !== 3) {
        throw new Error(`expected 3 versions, got ${verResp.Versions?.length}`);
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });

  await r('CreateSecret_WithDescription', async () => {
    const secName = makeUniqueName('DescTest');
    const desc = 'My test secret description';
    await client.send(new CreateSecretCommand({ Name: secName, SecretString: 'desc-value', Description: desc }));
    try {
      const resp = await client.send(new DescribeSecretCommand({ SecretId: secName }));
      if (resp.Description !== desc) {
        throw new Error(`description mismatch: got ${JSON.stringify(resp.Description)}, want ${JSON.stringify(desc)}`);
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });

  await r('DescribeSecret_ContentVerify', async () => {
    const secName = makeUniqueName('DescVerify');
    const createResp = await client.send(new CreateSecretCommand({
      Name: secName, SecretString: 'desc-content', Description: 'test description',
    }));
    try {
      const resp = await client.send(new DescribeSecretCommand({ SecretId: secName }));
      if (resp.Name !== secName) throw new Error('name mismatch');
      if (resp.ARN !== createResp.ARN) throw new Error('ARN mismatch');
      if (resp.Description !== 'test description') throw new Error('description mismatch');
      if (!resp.CreatedDate) throw new Error('CreatedDate to be defined');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });

  await r('CreateSecret_Binary', async () => {
    const secName = makeUniqueName('BinaryTest');
    const binaryData = new Uint8Array([0x01, 0x02, 0x03, 0x04, 0x05]);
    await client.send(new CreateSecretCommand({ Name: secName, SecretBinary: binaryData }));
    try {
      const resp = await client.send(new GetSecretValueCommand({ SecretId: secName }));
      if (!resp.SecretBinary) throw new Error('SecretBinary to be defined');
      const respStr = Buffer.from(resp.SecretBinary).toString('binary');
      const expStr = Buffer.from(binaryData).toString('binary');
      if (respStr !== expStr) throw new Error('binary data mismatch');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });

  await r('UpdateSecret_ClearDescription', async () => {
    const secName = makeUniqueName('ClearDesc');
    await client.send(new CreateSecretCommand({ Name: secName, SecretString: 'clear-desc', Description: 'initial description' }));
    try {
      await client.send(new UpdateSecretCommand({ SecretId: secName, Description: '' }));
      const resp = await client.send(new DescribeSecretCommand({ SecretId: secName }));
      if (resp.Description) throw new Error(`description should be cleared, got ${JSON.stringify(resp.Description)}`);
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });
}
