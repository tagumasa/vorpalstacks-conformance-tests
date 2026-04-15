import {
  PutIntegrationCommand,
  GetIntegrationCommand,
  UpdateIntegrationCommand,
  DeleteIntegrationCommand,
  PutIntegrationResponseCommand,
  GetIntegrationResponseCommand,
  UpdateIntegrationResponseCommand,
  DeleteIntegrationResponseCommand,
} from '@aws-sdk/client-api-gateway';
import type { TestResult } from '../../runner.js';
import { ApiGatewayTestContext } from './context.js';

export async function runIntegrationTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'PutIntegration', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    const resp = await client.send(new PutIntegrationCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
      httpMethod: 'GET',
      type: 'MOCK',
      requestTemplates: { 'application/json': '{"statusCode": 200}' },
    }));
    if (resp.type !== 'MOCK') throw new Error(`type mismatch, got ${resp.type}`);
  }));

  results.push(await runner.runTest('apigateway', 'GetIntegration', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    const resp = await client.send(new GetIntegrationCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
      httpMethod: 'GET',
    }));
    if (resp.type !== 'MOCK') throw new Error(`type mismatch, got ${resp.type}`);
  }));

  results.push(await runner.runTest('apigateway', 'UpdateIntegration', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    const resp = await client.send(new UpdateIntegrationCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
      httpMethod: 'GET',
      patchOperations: [
        { op: 'replace', path: '/timeoutInMillis', value: '5000' },
      ],
    }));
    if (resp.timeoutInMillis !== 5000) throw new Error(`timeoutInMillis not updated, got ${resp.timeoutInMillis}`);
  }));

  results.push(await runner.runTest('apigateway', 'PutIntegrationResponse', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    const resp = await client.send(new PutIntegrationResponseCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
      httpMethod: 'GET',
      statusCode: '200',
      responseTemplates: { 'application/json': '{"message": "ok"}' },
      selectionPattern: '2\\d{2}',
    }));
    if (resp.statusCode !== '200') throw new Error(`statusCode mismatch, got ${resp.statusCode}`);
  }));

  results.push(await runner.runTest('apigateway', 'GetIntegrationResponse', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    const resp = await client.send(new GetIntegrationResponseCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
      httpMethod: 'GET',
      statusCode: '200',
    }));
    if (resp.statusCode !== '200') throw new Error(`statusCode mismatch, got ${resp.statusCode}`);
  }));

  results.push(await runner.runTest('apigateway', 'UpdateIntegrationResponse', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    const resp = await client.send(new UpdateIntegrationResponseCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
      httpMethod: 'GET',
      statusCode: '200',
      patchOperations: [
        { op: 'replace', path: '/selectionPattern', value: 'ok' },
      ],
    }));
    if (resp.selectionPattern !== 'ok') throw new Error(`selectionPattern not updated, got ${resp.selectionPattern}`);
  }));

  return results;
}

export async function runDeleteIntegrationTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'DeleteIntegrationResponse', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    await client.send(new DeleteIntegrationResponseCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
      httpMethod: 'GET',
      statusCode: '200',
    }));
  }));

  results.push(await runner.runTest('apigateway', 'DeleteIntegration', async () => {
    if (!ctx.apiID || !ctx.resourceID) throw new Error('API ID or resource ID not available');
    await client.send(new DeleteIntegrationCommand({
      restApiId: ctx.apiID,
      resourceId: ctx.resourceID,
      httpMethod: 'GET',
    }));
  }));

  return results;
}
