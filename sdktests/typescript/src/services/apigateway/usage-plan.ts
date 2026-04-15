import {
  CreateUsagePlanCommand,
  GetUsagePlansCommand,
  GetUsagePlanCommand,
  UpdateUsagePlanCommand,
  DeleteUsagePlanCommand,
  CreateApiKeyCommand,
  DeleteApiKeyCommand,
  CreateUsagePlanKeyCommand,
  GetUsagePlanKeyCommand,
  GetUsagePlanKeysCommand,
  DeleteUsagePlanKeyCommand,
  GetUsageCommand,
} from '@aws-sdk/client-api-gateway';
import type { TestResult } from '../../runner.js';
import { ApiGatewayTestContext } from './context.js';
import { safeCleanup } from '../../helpers.js';

export async function runUsagePlanTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'CreateUsagePlan', async () => {
    const resp = await client.send(new CreateUsagePlanCommand({
      name: 'test-usage-plan',
      description: 'Test usage plan',
      throttle: { burstLimit: 10, rateLimit: 5.0 },
      quota: { limit: 1000, period: 'MONTH' },
      tags: { team: 'backend' },
    }));
    if (!resp.id) throw new Error('expected usage plan ID to be defined');
    if (!resp.throttle || resp.throttle.burstLimit !== 10) throw new Error('throttle burstLimit mismatch');
    if (!resp.quota || resp.quota.period !== 'MONTH') throw new Error('quota period mismatch');
  }));

  results.push(await runner.runTest('apigateway', 'GetUsagePlans', async () => {
    const resp = await client.send(new GetUsagePlansCommand({ limit: 100 }));
    if (!resp.items || resp.items.length === 0) throw new Error('expected at least 1 usage plan');
    let found = false;
    for (const item of resp.items) {
      if (item.name === 'test-usage-plan') {
        ctx.usagePlanID = item.id!;
        found = true;
        break;
      }
    }
    if (!found) throw new Error('test-usage-plan not found');
  }));

  results.push(await runner.runTest('apigateway', 'GetUsagePlan', async () => {
    if (!ctx.usagePlanID) throw new Error('usage plan ID not available');
    const resp = await client.send(new GetUsagePlanCommand({ usagePlanId: ctx.usagePlanID }));
    if (resp.name !== 'test-usage-plan') throw new Error(`name mismatch, got ${resp.name}`);
  }));

  results.push(await runner.runTest('apigateway', 'UpdateUsagePlan', async () => {
    if (!ctx.usagePlanID) throw new Error('usage plan ID not available');
    const resp = await client.send(new UpdateUsagePlanCommand({
      usagePlanId: ctx.usagePlanID,
      patchOperations: [
        { op: 'replace', path: '/name', value: 'updated-usage-plan' },
      ],
    }));
    if (resp.name !== 'updated-usage-plan') throw new Error(`name not updated, got ${resp.name}`);
  }));

  results.push(await runner.runTest('apigateway', 'DeleteUsagePlan', async () => {
    if (!ctx.usagePlanID) throw new Error('usage plan ID not available');
    await client.send(new DeleteUsagePlanCommand({ usagePlanId: ctx.usagePlanID }));
  }));

  results.push(await runner.runTest('apigateway', 'CreateUsagePlanKey_Lifecycle', async () => {
    const keyResp = await client.send(new CreateApiKeyCommand({
      name: 'upk-test-key',
      enabled: true,
    }));
    const keyId = keyResp.id!;

    const upResp = await client.send(new CreateUsagePlanCommand({
      name: 'upk-test-plan',
    }));
    const upId = upResp.id!;

    try {
      const upkResp = await client.send(new CreateUsagePlanKeyCommand({
        usagePlanId: upId,
        keyId: keyId,
        keyType: 'API_KEY',
      }));
      if (!upkResp.id) throw new Error('expected usage plan key ID to be defined');

      const getResp = await client.send(new GetUsagePlanKeyCommand({
        usagePlanId: upId,
        keyId: keyId,
      }));
      if (getResp.type !== 'API_KEY') throw new Error(`type mismatch, got ${getResp.type}`);

      const keysResp = await client.send(new GetUsagePlanKeysCommand({
        usagePlanId: upId,
        limit: 100,
      }));
      if (!keysResp.items || keysResp.items.length === 0) throw new Error('expected at least 1 usage plan key');

      await client.send(new DeleteUsagePlanKeyCommand({
        usagePlanId: upId,
        keyId: keyId,
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteUsagePlanCommand({ usagePlanId: upId })));
      await safeCleanup(() => client.send(new DeleteApiKeyCommand({ apiKey: keyId })));
    }
  }));

  results.push(await runner.runTest('apigateway', 'GetUsage', async () => {
    const planName = `usage-plan-${Date.now()}`;
    const upResp = await client.send(new CreateUsagePlanCommand({ name: planName }));
    const upId = upResp.id!;

    try {
      const now = new Date();
      const startDate = new Date(now.getFullYear(), now.getMonth() - 1, now.getDate());
      const endDate = new Date(now.getFullYear(), now.getMonth(), now.getDate());
      const fmt = (d: Date) => d.toISOString().slice(0, 10);

      const resp = await client.send(new GetUsageCommand({
        usagePlanId: upId,
        startDate: fmt(startDate),
        endDate: fmt(endDate),
      }));
      if (resp.usagePlanId !== upId) throw new Error('usagePlanId mismatch');
    } finally {
      await safeCleanup(() => client.send(new DeleteUsagePlanCommand({ usagePlanId: upId })));
    }
  }));

  return results;
}
