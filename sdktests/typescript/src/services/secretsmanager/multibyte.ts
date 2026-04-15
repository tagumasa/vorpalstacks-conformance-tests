import {
  SecretsManagerClient,
  CreateSecretCommand,
  GetSecretValueCommand,
  DeleteSecretCommand,
} from '@aws-sdk/client-secrets-manager';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runMultibyteTests(
  client: SecretsManagerClient,
  runner: TestRunner,
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('secretsmanager', 'MultiByteSecret', async () => {
    const cases: Array<{ label: string; value: string }> = [
      { label: 'ja', value: '日本語テストシークレット' },
      { label: 'zh', value: '简体中文测试机密' },
      { label: 'tw', value: '繁體中文測試機密' },
    ];
    for (const { label, value } of cases) {
      const name = makeUniqueName(`MultiByte-${label}`);
      await client.send(new CreateSecretCommand({ Name: name, SecretString: value }));
      try {
        const resp = await client.send(new GetSecretValueCommand({ SecretId: name }));
        if (resp.SecretString !== value) {
          throw new Error(`Mismatch for ${label}: expected ${JSON.stringify(value)}, got ${JSON.stringify(resp.SecretString)}`);
        }
      } finally {
        await safeCleanup(async () => {
          await client.send(new DeleteSecretCommand({ SecretId: name, ForceDeleteWithoutRecovery: true }));
        });
      }
    }
  }));
}
