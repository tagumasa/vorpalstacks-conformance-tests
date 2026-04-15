import {
  CreateRestApiCommand,
  GetRestApisCommand,
  GetRestApiCommand,
  UpdateRestApiCommand,
  DeleteRestApiCommand,
} from '@aws-sdk/client-api-gateway';
import type { TestResult } from '../../runner.js';
import { ApiGatewayTestContext } from './context.js';

export async function runRestApiTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'CreateRestApi', async () => {
    const resp = await client.send(new CreateRestApiCommand({
      name: ctx.apiName,
      description: 'Test API',
    }));
    if (!resp.id) throw new Error('expected API ID to be defined');
    ctx.apiID = resp.id;
  }));

  results.push(await runner.runTest('apigateway', 'GetRestApis', async () => {
    const resp = await client.send(new GetRestApisCommand({ limit: 500 }));
    if (!resp.items) throw new Error('expected items list to be defined');
    let found = false;
    for (const item of resp.items) {
      if (item.name === ctx.apiName) {
        ctx.apiID = item.id!;
        found = true;
        break;
      }
    }
    if (!found) throw new Error('API not found');
  }));

  results.push(await runner.runTest('apigateway', 'GetRestApi', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new GetRestApiCommand({ restApiId: ctx.apiID }));
    if (resp.name !== ctx.apiName) throw new Error(`name mismatch, got ${resp.name}`);
  }));

  results.push(await runner.runTest('apigateway', 'UpdateRestApi', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new UpdateRestApiCommand({
      restApiId: ctx.apiID,
      patchOperations: [
        { op: 'replace', path: '/description', value: 'Updated API' },
      ],
    }));
    if (!resp) throw new Error('expected response to be defined');
  }));

  return results;
}

export async function runDeleteRestApiTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'DeleteRestApi', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new DeleteRestApiCommand({ restApiId: ctx.apiID }));
    if (!resp) throw new Error('expected response to be defined');
  }));

  return results;
}
