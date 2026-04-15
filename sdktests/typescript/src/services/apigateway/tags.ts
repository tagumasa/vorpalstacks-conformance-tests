import {
  CreateRestApiCommand,
  DeleteRestApiCommand,
  TagResourceCommand,
  GetTagsCommand,
  UntagResourceCommand,
} from '@aws-sdk/client-api-gateway';
import type { TestResult } from '../../runner.js';
import { ApiGatewayTestContext } from './context.js';
import { safeCleanup } from '../../helpers.js';

export async function runTagTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'TagResource_UntagResource_ListTags', async () => {
    const tagAPI = `TagAPI-${Date.now()}`;
    const createResp = await client.send(new CreateRestApiCommand({ name: tagAPI }));
    const apiId = createResp.id!;
    const arn = `arn:aws:apigateway:${ctx.region}::/restapis/${apiId}`;

    try {
      await client.send(new TagResourceCommand({
        resourceArn: arn,
        tags: { key1: 'value1', key2: 'value2' },
      }));

      const tagResp = await client.send(new GetTagsCommand({ resourceArn: arn }));
      if (!tagResp.tags || tagResp.tags['key1'] !== 'value1') {
        throw new Error(`tags mismatch, got ${JSON.stringify(tagResp.tags)}`);
      }

      await client.send(new UntagResourceCommand({
        resourceArn: arn,
        tagKeys: ['key2'],
      }));

      const tagResp2 = await client.send(new GetTagsCommand({ resourceArn: arn }));
      if (tagResp2.tags && 'key2' in tagResp2.tags) {
        throw new Error('key2 should have been removed');
      }
      if (!tagResp2.tags || tagResp2.tags['key1'] !== 'value1') {
        throw new Error('key1 should still exist');
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteRestApiCommand({ restApiId: apiId })));
    }
  }));

  return results;
}
