import {
  PutMethodCommand,
  GetMethodCommand,
  UpdateMethodCommand,
  DeleteMethodCommand,
  PutMethodResponseCommand,
  GetMethodResponseCommand,
  DeleteMethodResponseCommand,
} from '@aws-sdk/client-api-gateway';
import type { TestResult } from '../../runner.js';
import { ApiGatewayTestContext } from './context.js';

export async function runMethodTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'PutMethod', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    const resp = await client.send(new PutMethodCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
      httpMethod: 'GET',
      authorizationType: 'NONE',
      apiKeyRequired: false,
    }));
    if (resp.httpMethod !== 'GET') throw new Error(`httpMethod mismatch, got ${resp.httpMethod}`);
    if (resp.authorizationType !== 'NONE') throw new Error(`authorizationType mismatch, got ${resp.authorizationType}`);
  }));

  results.push(await runner.runTest('apigateway', 'GetMethod', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    const resp = await client.send(new GetMethodCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
      httpMethod: 'GET',
    }));
    if (resp.httpMethod !== 'GET') throw new Error(`httpMethod mismatch, got ${resp.httpMethod}`);
  }));

  results.push(await runner.runTest('apigateway', 'UpdateMethod', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    const resp = await client.send(new UpdateMethodCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
      httpMethod: 'GET',
      patchOperations: [
        { op: 'replace', path: '/authorizationType', value: 'AWS_IAM' },
      ],
    }));
    if (resp.authorizationType !== 'AWS_IAM') throw new Error(`authorizationType not updated, got ${resp.authorizationType}`);
  }));

  results.push(await runner.runTest('apigateway', 'PutMethodResponse', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    const resp = await client.send(new PutMethodResponseCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
      httpMethod: 'GET',
      statusCode: '200',
      responseModels: { 'application/json': 'Empty' },
    }));
    if (resp.statusCode !== '200') throw new Error(`statusCode mismatch, got ${resp.statusCode}`);
  }));

  results.push(await runner.runTest('apigateway', 'GetMethodResponse', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    const resp = await client.send(new GetMethodResponseCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
      httpMethod: 'GET',
      statusCode: '200',
    }));
    if (resp.statusCode !== '200') throw new Error(`statusCode mismatch, got ${resp.statusCode}`);
  }));

  results.push(await runner.runTest('apigateway', 'DeleteMethodResponse', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    await client.send(new DeleteMethodResponseCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
      httpMethod: 'GET',
      statusCode: '200',
    }));
  }));

  return results;
}

export async function runDeleteMethodTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'DeleteMethod', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    await client.send(new DeleteMethodCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
      httpMethod: 'GET',
    }));
  }));

  return results;
}
