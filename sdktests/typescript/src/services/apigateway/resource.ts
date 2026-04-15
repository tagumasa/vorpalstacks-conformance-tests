import {
  CreateResourceCommand,
  GetResourceCommand,
  GetResourcesCommand,
  UpdateResourceCommand,
  DeleteResourceCommand,
} from '@aws-sdk/client-api-gateway';
import type { TestResult } from '../../runner.js';
import { ApiGatewayTestContext } from './context.js';

export async function runResourceTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'CreateResource', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new CreateResourceCommand({
      restApiId: ctx.apiID,
      parentId: ctx.apiID,
      pathPart: 'test',
    }));
    if (!resp.id) throw new Error('expected resource ID to be defined');
    ctx.resourceID = resp.id;
  }));

  results.push(await runner.runTest('apigateway', 'GetResource', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    const resp = await client.send(new GetResourceCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
    }));
    if (resp.id !== ctx.resourceID) throw new Error(`resource ID mismatch, got ${resp.id}`);
    if (resp.path !== '/test') throw new Error(`path mismatch, got ${resp.path}`);
  }));

  results.push(await runner.runTest('apigateway', 'GetResources', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new GetResourcesCommand({ restApiId: ctx.apiID }));
    if (!resp.items || resp.items.length < 2) {
      throw new Error(`expected at least 2 resources, got ${resp.items?.length ?? 0}`);
    }
  }));

  results.push(await runner.runTest('apigateway', 'UpdateResource', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    const resp = await client.send(new UpdateResourceCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
      patchOperations: [
        { op: 'replace', path: '/pathPart', value: 'items' },
      ],
    }));
    if (resp.path !== '/items') throw new Error(`path not updated, got ${resp.path}`);
  }));

  return results;
}

export async function runDeleteResourceTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'DeleteResource', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    await client.send(new DeleteResourceCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
    }));
  }));

  return results;
}
