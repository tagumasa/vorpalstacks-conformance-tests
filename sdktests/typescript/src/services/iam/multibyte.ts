import { IAMClient, CreateUserCommand, TagUserCommand, ListUserTagsCommand, UntagUserCommand, DeleteUserCommand } from '@aws-sdk/client-iam';
import type { IAMTestContext } from './context.js';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runMultibyteTests(
  _iamCtx: IAMTestContext,
  runner: TestRunner,
): Promise<TestResult[]> {
  const { client } = _iamCtx;
  const results: TestResult[] = [];

  results.push(await runner.runTest('iam', 'MultiByteTag', async () => {
    const userName = makeUniqueName('MBUser');
    await client.send(new CreateUserCommand({ UserName: userName }));
    try {
      const tags = [
        { Key: '環境', Value: 'テスト環境' },
        { Key: '说明', Value: '简体中文标签' },
        { Key: '說明', Value: '繁體中文標籤' },
      ];
      await client.send(new TagUserCommand({ UserName: userName, Tags: tags }));
      try {
        const resp = await client.send(new ListUserTagsCommand({ UserName: userName }));
        for (const tag of tags) {
          const found = resp.Tags?.some((t) => t.Key === tag.Key && t.Value === tag.Value);
          if (!found) {
            throw new Error(`tag not found: ${tag.Key}=${tag.Value}`);
          }
        }
      } finally {
        await safeCleanup(() => client.send(new UntagUserCommand({ UserName: userName, TagKeys: tags.map((t) => t.Key) })));
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteUserCommand({ UserName: userName })));
    }
  }));

  return results;
}
