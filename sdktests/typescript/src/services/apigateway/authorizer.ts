import {
  CreateAuthorizerCommand,
  GetAuthorizerCommand,
  UpdateAuthorizerCommand,
  GetAuthorizersCommand,
  TestInvokeAuthorizerCommand,
  DeleteAuthorizerCommand,
} from '@aws-sdk/client-api-gateway';
import type { TestResult } from '../../runner.js';
import { ApiGatewayTestContext } from './context.js';

export async function runAuthorizerTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'CreateAuthorizer', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new CreateAuthorizerCommand({
      restApiId: ctx.apiID,
      name: 'test-authorizer',
      type: 'TOKEN',
      authorizerUri: 'https://example.com/auth',
      identitySource: 'method.request.header.Authorization',
      authorizerResultTtlInSeconds: 300,
    }));
    if (!resp.id) throw new Error('expected authorizer ID to be defined');
    if (resp.type !== 'TOKEN') throw new Error(`type mismatch, got ${resp.type}`);
    ctx.authorizerID = resp.id;
  }));

  results.push(await runner.runTest('apigateway', 'GetAuthorizer', async () => {
    if (!ctx.apiID || !ctx.authorizerID) throw new Error('API ID or authorizer ID not available');
    const resp = await client.send(new GetAuthorizerCommand({
      restApiId: ctx.apiID,
      authorizerId: ctx.authorizerID,
    }));
    if (resp.name !== 'test-authorizer') throw new Error(`name mismatch, got ${resp.name}`);
  }));

  results.push(await runner.runTest('apigateway', 'UpdateAuthorizer', async () => {
    if (!ctx.apiID || !ctx.authorizerID) throw new Error('API ID or authorizer ID not available');
    const resp = await client.send(new UpdateAuthorizerCommand({
      restApiId: ctx.apiID,
      authorizerId: ctx.authorizerID,
      patchOperations: [
        { op: 'replace', path: '/name', value: 'updated-authorizer' },
      ],
    }));
    if (resp.name !== 'updated-authorizer') throw new Error(`name not updated, got ${resp.name}`);
  }));

  results.push(await runner.runTest('apigateway', 'GetAuthorizers', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new GetAuthorizersCommand({
      restApiId: ctx.apiID,
      limit: 100,
    }));
    if (!resp.items || resp.items.length === 0) throw new Error('expected at least 1 authorizer');
  }));

  results.push(await runner.runTest('apigateway', 'TestInvokeAuthorizer', async () => {
    if (!ctx.apiID || !ctx.authorizerID) throw new Error('API ID or authorizer ID not available');
    const resp = await client.send(new TestInvokeAuthorizerCommand({
      restApiId: ctx.apiID,
      authorizerId: ctx.authorizerID,
      headers: { Authorization: 'Bearer test-token' },
    }));
    if (resp.clientStatus !== 200) throw new Error(`expected clientStatus 200, got ${resp.clientStatus}`);
    if (!resp.policy) throw new Error('expected policy to be defined');
  }));

  results.push(await runner.runTest('apigateway', 'DeleteAuthorizer', async () => {
    if (!ctx.apiID || !ctx.authorizerID) throw new Error('API ID or authorizer ID not available');
    await client.send(new DeleteAuthorizerCommand({
      restApiId: ctx.apiID,
      authorizerId: ctx.authorizerID,
    }));
  }));

  return results;
}
