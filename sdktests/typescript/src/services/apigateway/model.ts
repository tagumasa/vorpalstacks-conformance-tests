import {
  CreateModelCommand,
  GetModelCommand,
  UpdateModelCommand,
  GetModelsCommand,
  DeleteModelCommand,
} from '@aws-sdk/client-api-gateway';
import type { TestResult } from '../../runner.js';
import { ApiGatewayTestContext } from './context.js';

export async function runModelTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'CreateModel', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new CreateModelCommand({
      restApiId: ctx.apiID,
      name: 'UserModel',
      contentType: 'application/json',
      description: 'User model',
      schema: '{"type":"object"}',
    }));
    if (!resp.id) throw new Error('expected model ID to be defined');
    if (resp.name !== 'UserModel') throw new Error(`name mismatch, got ${resp.name}`);
  }));

  results.push(await runner.runTest('apigateway', 'GetModel', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new GetModelCommand({
      restApiId: ctx.apiID,
      modelName: 'UserModel',
    }));
    if (resp.name !== 'UserModel') throw new Error(`name mismatch, got ${resp.name}`);
    if (resp.schema !== '{"type":"object"}') throw new Error(`schema mismatch, got ${resp.schema}`);
  }));

  results.push(await runner.runTest('apigateway', 'UpdateModel', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new UpdateModelCommand({
      restApiId: ctx.apiID,
      modelName: 'UserModel',
      patchOperations: [
        { op: 'replace', path: '/description', value: 'updated model' },
      ],
    }));
    if (resp.description !== 'updated model') throw new Error(`description not updated, got ${resp.description}`);
  }));

  results.push(await runner.runTest('apigateway', 'GetModels', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new GetModelsCommand({
      restApiId: ctx.apiID,
      limit: 100,
    }));
    if (!resp.items || resp.items.length === 0) throw new Error('expected at least 1 model');
  }));

  results.push(await runner.runTest('apigateway', 'DeleteModel', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    await client.send(new DeleteModelCommand({
      restApiId: ctx.apiID,
      modelName: 'UserModel',
    }));
  }));

  return results;
}
