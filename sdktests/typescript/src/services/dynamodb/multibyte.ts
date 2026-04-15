import { DynamoDBClient, PutItemCommand, GetItemCommand } from '@aws-sdk/client-dynamodb';
import type { DynamoDBTestContext } from './context.js';
import type { TestRunner, TestResult } from '../../runner.js';

export async function runMultibyteTests(
  ctx: DynamoDBTestContext,
  runner: TestRunner,
): Promise<TestResult[]> {
  const { client, tableName } = ctx;
  const results: TestResult[] = [];

  results.push(await runner.runTest('dynamodb', 'MultiByteItem', async () => {
    const mbItems: Array<{ id: string; data: string }> = [
      { id: 'mb-ja', data: 'パスワード＝虹色の空2026！カタカナ・ひらがな・漢字の混在' },
      { id: 'mb-zh', data: '数据库连接串：主机=localhost;端口=3306;密码=测试@123' },
      { id: 'mb-tw', data: '憑證內容：帳號＝繁體使用者；金鑰＝ＡＢＣｄｅｆ' },
    ];
    for (const item of mbItems) {
      await client.send(new PutItemCommand({
        TableName: tableName,
        Item: { id: { S: item.id }, data: { S: item.data } },
      }));
      const resp = await client.send(new GetItemCommand({
        TableName: tableName,
        Key: { id: { S: item.id } },
      }));
      if (resp.Item?.data?.S !== item.data) {
        throw new Error(`multibyte item mismatch for ${item.id}: expected ${JSON.stringify(item.data)}, got ${JSON.stringify(resp.Item?.data?.S)}`);
      }
    }
  }));

  return results;
}
