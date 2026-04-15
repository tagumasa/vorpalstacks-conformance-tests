import {
  CreateDomainNameCommand,
  GetDomainNamesCommand,
  GetDomainNameCommand,
  UpdateDomainNameCommand,
  CreateBasePathMappingCommand,
  GetBasePathMappingsCommand,
  GetBasePathMappingCommand,
  UpdateBasePathMappingCommand,
  DeleteBasePathMappingCommand,
  DeleteDomainNameCommand,
} from '@aws-sdk/client-api-gateway';
import type { TestResult } from '../../runner.js';
import { ApiGatewayTestContext } from './context.js';

export async function runDomainNameTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'CreateDomainName', async () => {
    const domain = `test-${Date.now()}.example.com`;
    const resp = await client.send(new CreateDomainNameCommand({
      domainName: domain,
      certificateName: 'test-cert',
      tags: { domain: 'test' },
    }));
    if (resp.domainName !== domain) throw new Error(`domain name mismatch, got ${resp.domainName}`);
    if (!resp.domainNameId) throw new Error('expected domain name ID to be defined');
  }));

  results.push(await runner.runTest('apigateway', 'GetDomainNames', async () => {
    const resp = await client.send(new GetDomainNamesCommand({ limit: 100 }));
    if (!resp.items || resp.items.length === 0) throw new Error('expected at least 1 domain name');
    ctx.domainName = resp.items[0].domainName!;
  }));

  results.push(await runner.runTest('apigateway', 'GetDomainName', async () => {
    if (!ctx.domainName) throw new Error('domain name not available');
    const resp = await client.send(new GetDomainNameCommand({ domainName: ctx.domainName }));
    if (resp.domainName !== ctx.domainName) throw new Error(`domain name mismatch, got ${resp.domainName}`);
  }));

  results.push(await runner.runTest('apigateway', 'UpdateDomainName', async () => {
    if (!ctx.domainName) throw new Error('domain name not available');
    const resp = await client.send(new UpdateDomainNameCommand({
      domainName: ctx.domainName,
      patchOperations: [
        { op: 'replace', path: '/certificateName', value: 'updated-cert' },
      ],
    }));
    if (resp.certificateName !== 'updated-cert') throw new Error(`certificateName not updated, got ${resp.certificateName}`);
  }));

  results.push(await runner.runTest('apigateway', 'CreateBasePathMapping', async () => {
    if (!ctx.apiID || !ctx.domainName) throw new Error('API ID or domain name not available');
    const resp = await client.send(new CreateBasePathMappingCommand({
      domainName: ctx.domainName,
      restApiId: ctx.apiID,
      basePath: 'v1',
      stage: 'prod',
    }));
    if (resp.basePath !== 'v1') throw new Error(`basePath mismatch, got ${resp.basePath}`);
    if (resp.restApiId !== ctx.apiID) throw new Error(`restApiId mismatch, got ${resp.restApiId}`);
  }));

  results.push(await runner.runTest('apigateway', 'GetBasePathMappings', async () => {
    if (!ctx.domainName) throw new Error('domain name not available');
    const resp = await client.send(new GetBasePathMappingsCommand({
      domainName: ctx.domainName,
      limit: 100,
    }));
    if (!resp.items || resp.items.length === 0) throw new Error('expected at least 1 base path mapping');
  }));

  results.push(await runner.runTest('apigateway', 'GetBasePathMapping', async () => {
    if (!ctx.domainName) throw new Error('domain name not available');
    const resp = await client.send(new GetBasePathMappingCommand({
      domainName: ctx.domainName,
      basePath: 'v1',
    }));
    if (resp.basePath !== 'v1') throw new Error(`basePath mismatch, got ${resp.basePath}`);
  }));

  results.push(await runner.runTest('apigateway', 'UpdateBasePathMapping', async () => {
    if (!ctx.domainName) throw new Error('domain name not available');
    const resp = await client.send(new UpdateBasePathMappingCommand({
      domainName: ctx.domainName,
      basePath: 'v1',
      patchOperations: [
        { op: 'replace', path: '/stage', value: 'staging' },
      ],
    }));
    if (resp.stage !== 'staging') throw new Error(`stage not updated, got ${resp.stage}`);
  }));

  results.push(await runner.runTest('apigateway', 'DeleteBasePathMapping', async () => {
    if (!ctx.domainName) throw new Error('domain name not available');
    await client.send(new DeleteBasePathMappingCommand({
      domainName: ctx.domainName,
      basePath: 'v1',
    }));
  }));

  results.push(await runner.runTest('apigateway', 'DeleteDomainName', async () => {
    if (!ctx.domainName) throw new Error('domain name not available');
    await client.send(new DeleteDomainNameCommand({ domainName: ctx.domainName }));
  }));

  return results;
}
