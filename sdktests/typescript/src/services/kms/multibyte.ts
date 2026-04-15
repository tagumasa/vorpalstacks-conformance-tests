import { KMSClient, EncryptCommand, DecryptCommand } from '@aws-sdk/client-kms';
import type { TestRunner, TestResult } from '../../runner.js';
import type { KmsState } from './context.js';

export async function runMultibyteTests(
  runner: TestRunner,
  client: KMSClient,
  state: KmsState,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('kms', 'MultiByteEncryptDecrypt', async () => {
    const plaintexts = [
      { label: 'ja', data: 'パスワード＝虹色の空2026！暗号化テスト。' },
      { label: 'zh', data: '数据库密码=测试@123！加密完成。' },
      { label: 'tw', data: '憑證金鑰＝繁體測試！解密成功。' },
    ];
    for (const { label, data } of plaintexts) {
      const plaintext = new TextEncoder().encode(data);
      const encResp = await client.send(new EncryptCommand({
        KeyId: state.keyID,
        Plaintext: plaintext,
      }));
      const decResp = await client.send(new DecryptCommand({
        CiphertextBlob: encResp.CiphertextBlob,
      }));
      const decrypted = new TextDecoder().decode(decResp.Plaintext);
      if (decrypted !== data) {
        throw new Error(`multibyte decrypt mismatch for ${label}: expected ${JSON.stringify(data)}, got ${JSON.stringify(decrypted)}`);
      }
    }
  }));

  return results;
}
