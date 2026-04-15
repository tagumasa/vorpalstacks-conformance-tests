import {
  CreateResourceCommand,
  PutMethodCommand,
  PutIntegrationCommand,
  TestInvokeMethodCommand,
} from '@aws-sdk/client-api-gateway';
import type { TestResult } from '../../runner.js';
import { ApiGatewayTestContext } from './context.js';

export async function runTestInvokeTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'TestInvokeMethod', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');

    const resResp = await client.send(new CreateResourceCommand({
      restApiId: ctx.apiID,
      parentId: ctx.apiID,
      pathPart: 'mock',
    }));
    const resId = resResp.id!;

    await client.send(new PutMethodCommand({
      restApiId: ctx.apiID,
      resourceId: resId,
      httpMethod: 'POST',
      authorizationType: 'NONE',
    }));

    await client.send(new PutIntegrationCommand({
      restApiId: ctx.apiID,
      resourceId: resId,
      httpMethod: 'POST',
      type: 'MOCK',
      requestTemplates: { 'application/json': '{"statusCode": 200}' },
    }));

    const resp = await client.send(new TestInvokeMethodCommand({
      restApiId: ctx.apiID,
      resourceId: resId,
      httpMethod: 'POST',
      body: '{"test": "data"}',
    }));

    if (resp.status !== 200) throw new Error(`expected status 200, got ${resp.status}`);
    if (!resp.log) throw new Error('expected log to be defined');
  }));

  return results;
}
