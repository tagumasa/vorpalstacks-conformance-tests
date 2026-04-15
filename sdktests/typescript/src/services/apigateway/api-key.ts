import {
  CreateApiKeyCommand,
  GetApiKeysCommand,
  GetApiKeyCommand,
  UpdateApiKeyCommand,
  DeleteApiKeyCommand,
} from '@aws-sdk/client-api-gateway';
import type { TestResult } from '../../runner.js';
import { ApiGatewayTestContext } from './context.js';

export async function runApiKeyTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'CreateApiKey', async () => {
    const resp = await client.send(new CreateApiKeyCommand({
      name: 'test-api-key',
      description: 'Test API key',
      enabled: true,
      tags: { env: 'test' },
    }));
    if (!resp.id) throw new Error('expected api key ID to be defined');
    if (!resp.value) throw new Error('expected api key value to be defined');
    if (!resp.enabled) throw new Error('expected enabled=true');
    ctx.apiKeyValue = resp.value;
  }));

  results.push(await runner.runTest('apigateway', 'GetApiKeys', async () => {
    const resp = await client.send(new GetApiKeysCommand({ limit: 100 }));
    if (!resp.items || resp.items.length === 0) throw new Error('expected at least 1 api key');
    let found = false;
    for (const item of resp.items) {
      if (item.name === 'test-api-key') {
        ctx.apiKeyID = item.id!;
        found = true;
        break;
      }
    }
    if (!found) throw new Error('test-api-key not found');
  }));

  results.push(await runner.runTest('apigateway', 'GetApiKey', async () => {
    if (!ctx.apiKeyID) throw new Error('api key ID not available');
    const resp = await client.send(new GetApiKeyCommand({
      apiKey: ctx.apiKeyID,
      includeValue: true,
    }));
    if (resp.name !== 'test-api-key') throw new Error(`name mismatch, got ${resp.name}`);
    if (resp.value !== ctx.apiKeyValue) throw new Error(`value mismatch, got ${resp.value}`);
  }));

  results.push(await runner.runTest('apigateway', 'UpdateApiKey', async () => {
    if (!ctx.apiKeyID) throw new Error('api key ID not available');
    const resp = await client.send(new UpdateApiKeyCommand({
      apiKey: ctx.apiKeyID,
      patchOperations: [
        { op: 'replace', path: '/name', value: 'updated-api-key' },
      ],
    }));
    if (resp.name !== 'updated-api-key') throw new Error(`name not updated, got ${resp.name}`);
  }));

  results.push(await runner.runTest('apigateway', 'DeleteApiKey', async () => {
    if (!ctx.apiKeyID) throw new Error('api key ID not available');
    await client.send(new DeleteApiKeyCommand({ apiKey: ctx.apiKeyID }));
  }));

  return results;
}
