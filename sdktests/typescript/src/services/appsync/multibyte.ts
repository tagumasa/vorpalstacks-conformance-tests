import {
  AppSyncClient,
  CreateGraphqlApiCommand,
  TagResourceCommand,
  ListTagsForResourceCommand,
  UntagResourceCommand,
  DeleteGraphqlApiCommand,
} from '@aws-sdk/client-appsync';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runMultibyteTests(
  runner: TestRunner,
  client: AppSyncClient,
  _gqlApiId: string,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('appsync', 'MultiByteTagResource', async () => {
    const apiName = makeUniqueName('MBGraphqlApi');
    const resp = await client.send(new CreateGraphqlApiCommand({
      name: apiName,
      authenticationType: 'API_KEY',
    }));
    const apiId = resp.graphqlApi?.apiId;
    if (!apiId) throw new Error('expected apiId');
    const apiArn = resp.graphqlApi?.arn;
    if (!apiArn) throw new Error('expected arn');
    try {
      const tags: Record<string, string> = {
        '環境': 'テスト環境',
        '说明': '简体中文标签',
        '說明': '繁體中文標籤',
      };
      await client.send(new TagResourceCommand({
        resourceArn: apiArn,
        tags,
      }));
      const listResp = await client.send(new ListTagsForResourceCommand({ resourceArn: apiArn }));
      for (const [k, v] of Object.entries(tags)) {
        if (listResp.tags?.[k] !== v) {
          throw new Error(`tag not found: ${k}=${v}, got ${JSON.stringify(listResp.tags?.[k])}`);
        }
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteGraphqlApiCommand({ apiId })) as unknown as Promise<void>);
    }
  }));

  return results;
}
