import {
  SecretsManagerClient,
  CreateSecretCommand,
  DeleteSecretCommand,
  DescribeSecretCommand,
  ListSecretsCommand,
  BatchGetSecretValueCommand,
  TagResourceCommand,
  UntagResourceCommand,
} from '@aws-sdk/client-secrets-manager';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runListBatchTagTests(
  client: SecretsManagerClient,
  runner: TestRunner,
  results: TestResult[],
): Promise<void> {
  const s = 'secretsmanager';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(s, name, fn));

  await r('ListSecrets_ContainsCreated', async () => {
    const secName = makeUniqueName('ListVerify');
    await client.send(new CreateSecretCommand({ Name: secName, SecretString: 'list-test' }));
    try {
      const resp = await client.send(new ListSecretsCommand({}));
      const found = resp.SecretList?.some((s) => s.Name === secName);
      if (!found) throw new Error('created secret not found in ListSecrets');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });

  await r('ListSecrets_Filters', async () => {
    const prefix = makeUniqueName('FilterTest') + '-';
    await client.send(new CreateSecretCommand({ Name: prefix + 'alpha', SecretString: 'a' }));
    try {
      await client.send(new CreateSecretCommand({ Name: prefix + 'beta', SecretString: 'b' }));
      try {
        const resp = await client.send(new ListSecretsCommand({
          Filters: [{ Key: 'name', Values: [prefix + 'alpha'] }],
        }));
        if (!resp.SecretList || resp.SecretList.length !== 1) {
          throw new Error(`expected 1 secret, got ${resp.SecretList?.length}`);
        }
        if (resp.SecretList[0].Name !== prefix + 'alpha') throw new Error('wrong secret returned');
      } finally {
        await safeCleanup(async () => {
          await client.send(new DeleteSecretCommand({ SecretId: prefix + 'beta', ForceDeleteWithoutRecovery: true }));
        });
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: prefix + 'alpha', ForceDeleteWithoutRecovery: true }));
      });
    }
  });

  await r('ListSecrets_Pagination', async () => {
    const pgTs = String(Date.now());
    const pgSecrets: string[] = [];
    for (let i = 0; i < 5; i++) {
      const name = `PagSecret-${pgTs}-${i}`;
      try {
        await client.send(new CreateSecretCommand({ Name: name, SecretString: 'pagval' }));
        pgSecrets.push(name);
      } catch (e) {
        for (const sn of pgSecrets) {
          await safeCleanup(async () => {
            await client.send(new DeleteSecretCommand({ SecretId: sn, ForceDeleteWithoutRecovery: true }));
          });
        }
        throw new Error(`create secret ${name}: ${e instanceof Error ? e.message : String(e)}`);
      }
    }

    try {
      const allSecrets: string[] = [];
      let nextToken: string | undefined;
      while (true) {
        const resp = await client.send(new ListSecretsCommand({ MaxResults: 2, NextToken: nextToken }));
        for (const sec of resp.SecretList ?? []) {
          if (sec.Name && sec.Name.includes(`PagSecret-${pgTs}`)) allSecrets.push(sec.Name);
        }
        if (resp.NextToken) { nextToken = resp.NextToken; } else { break; }
      }
      if (allSecrets.length !== 5) throw new Error(`expected 5 paginated secrets, got ${allSecrets.length}`);
    } finally {
      for (const sn of pgSecrets) {
        await safeCleanup(async () => {
          await client.send(new DeleteSecretCommand({ SecretId: sn, ForceDeleteWithoutRecovery: true }));
        });
      }
    }
  });

  await r('BatchGetSecretValue_Basic', async () => {
    const sec1 = makeUniqueName('Batch1');
    const sec2 = makeUniqueName('Batch2');
    for (const name of [sec1, sec2]) {
      await client.send(new CreateSecretCommand({ Name: name, SecretString: 'batch-value-' + name }));
    }
    try {
      const resp = await client.send(new BatchGetSecretValueCommand({ SecretIdList: [sec1, sec2] }));
      if (!resp.SecretValues || resp.SecretValues.length !== 2) {
        throw new Error(`expected 2 secret values, got ${resp.SecretValues?.length}`);
      }
    } finally {
      for (const name of [sec1, sec2]) {
        await safeCleanup(async () => {
          await client.send(new DeleteSecretCommand({ SecretId: name, ForceDeleteWithoutRecovery: true }));
        });
      }
    }
  });

  await r('BatchGetSecretValue_NonExistent', async () => {
    const secName = makeUniqueName('BatchNE');
    await client.send(new CreateSecretCommand({ Name: secName, SecretString: 'exists' }));
    try {
      const resp = await client.send(new BatchGetSecretValueCommand({ SecretIdList: [secName, 'nonexistent-batch-secret'] }));
      if (!resp.SecretValues || resp.SecretValues.length !== 1) {
        throw new Error(`expected 1 secret value, got ${resp.SecretValues?.length}`);
      }
      if (!resp.Errors || resp.Errors.length !== 1) {
        throw new Error(`expected 1 error, got ${resp.Errors?.length}`);
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });

  await r('UntagResource_Basic', async () => {
    const secName = makeUniqueName('UntagTest');
    await client.send(new CreateSecretCommand({
      Name: secName,
      Tags: [{ Key: 'env', Value: 'test' }, { Key: 'team', Value: 'dev' }],
    }));
    try {
      await client.send(new UntagResourceCommand({ SecretId: secName, TagKeys: ['env'] }));
      const descResp = await client.send(new DescribeSecretCommand({ SecretId: secName }));
      for (const t of descResp.Tags ?? []) {
        if (t.Key === 'env') throw new Error('env tag should have been removed');
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });

  await r('DescribeSecret_Tags', async () => {
    const secName = makeUniqueName('ListTags');
    await client.send(new CreateSecretCommand({
      Name: secName,
      Tags: [{ Key: 'key1', Value: 'val1' }, { Key: 'key2', Value: 'val2' }],
    }));
    try {
      const resp = await client.send(new DescribeSecretCommand({ SecretId: secName }));
      if (!resp.Tags || resp.Tags.length !== 2) throw new Error(`expected 2 tags, got ${resp.Tags?.length}`);
      const tagMap: Record<string, string> = {};
      for (const t of resp.Tags) { if (t.Key) tagMap[t.Key] = t.Value ?? ''; }
      if (tagMap['key1'] !== 'val1' || tagMap['key2'] !== 'val2') throw new Error(`tag content mismatch: ${JSON.stringify(tagMap)}`);
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });

  await r('CreateSecret_WithTags', async () => {
    const secName = makeUniqueName('TagCreate');
    await client.send(new CreateSecretCommand({
      Name: secName, SecretString: 'tagged', Tags: [{ Key: 'Owner', Value: 'test-suite' }],
    }));
    try {
      const resp = await client.send(new DescribeSecretCommand({ SecretId: secName }));
      const found = resp.Tags?.some((t) => t.Key === 'Owner' && t.Value === 'test-suite');
      if (!found) throw new Error('Owner tag not found in DescribeSecret');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteSecretCommand({ SecretId: secName, ForceDeleteWithoutRecovery: true }));
      });
    }
  });
}
