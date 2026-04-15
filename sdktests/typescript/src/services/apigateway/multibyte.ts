import {
  APIGatewayClient,
  CreateRestApiCommand,
  CreateResourceCommand,
  PutMethodCommand,
  PutIntegrationCommand,
  DeleteRestApiCommand,
  TestInvokeMethodCommand,
} from '@aws-sdk/client-api-gateway';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runMultibyteTests(
  runner: TestRunner,
  client: APIGatewayClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('apigateway', 'MultiByteRequestBody', async () => {
    const apiName = makeUniqueName('MBApi');
    const createResp = await client.send(new CreateRestApiCommand({ name: apiName }));
    const apiId = createResp.id!;
    const rootId = createResp.id!;
    try {
      const resourceResp = await client.send(new CreateResourceCommand({
        restApiId: apiId,
        parentId: rootId,
        pathPart: 'test',
      }));
      const resourceId = resourceResp.id!;
      await client.send(new PutMethodCommand({
        restApiId: apiId,
        resourceId,
        httpMethod: 'POST',
        authorizationType: 'NONE',
      }));
      await client.send(new PutIntegrationCommand({
        restApiId: apiId,
        resourceId,
        httpMethod: 'POST',
        type: 'MOCK',
        requestTemplates: { 'application/json': '{"statusCode": 200}' },
      }));
      const bodies = [
        { label: 'ja', body: '{"message":"こんにちは！APIテスト。"}' },
        { label: 'zh', body: '{"message":"简体中文API测试！"}' },
        { label: 'tw', body: '{"message":"繁體中文API測試！"}' },
      ];
      for (const { label, body } of bodies) {
        const resp = await client.send(new TestInvokeMethodCommand({
          restApiId: apiId,
          resourceId,
          httpMethod: 'POST',
          body,
        }));
        if (resp.status !== 200) {
          throw new Error(`test invoke failed for ${label}: status ${resp.status}`);
        }
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteRestApiCommand({ restApiId: apiId })));
    }
  }));

  return results;
}
