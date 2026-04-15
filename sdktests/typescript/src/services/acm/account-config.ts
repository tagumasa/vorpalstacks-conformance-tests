import type { ACMClient } from '@aws-sdk/client-acm';
import type { TestRunner, TestResult } from '../../runner.js';
import {
  PutAccountConfigurationCommand,
  GetAccountConfigurationCommand,
} from '@aws-sdk/client-acm';

const SVC = 'acm';

export async function runAccountConfigTests(
  runner: TestRunner,
  client: ACMClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest(SVC, 'GetAccountConfiguration_DefaultValues', async () => {
    await client.send(new PutAccountConfigurationCommand({
      IdempotencyToken: `reset-${Date.now()}`,
      ExpiryEvents: { DaysBeforeExpiry: 45 },
    }));
    const resp = await client.send(new GetAccountConfigurationCommand({}));
    if (!resp.ExpiryEvents) throw new Error('expected ExpiryEvents to be defined');
    if (resp.ExpiryEvents.DaysBeforeExpiry !== 45) {
      throw new Error(`expected 45, got ${resp.ExpiryEvents.DaysBeforeExpiry}`);
    }
  }));

  results.push(await runner.runTest(SVC, 'GetAccountConfiguration_RoundTrip', async () => {
    await client.send(new PutAccountConfigurationCommand({
      IdempotencyToken: `rt-${Date.now()}`,
      ExpiryEvents: { DaysBeforeExpiry: 30 },
    }));
    const resp = await client.send(new GetAccountConfigurationCommand({}));
    if (!resp.ExpiryEvents) throw new Error('expected ExpiryEvents to be defined');
    if (resp.ExpiryEvents.DaysBeforeExpiry !== 30) {
      throw new Error(`expected 30, got ${resp.ExpiryEvents.DaysBeforeExpiry}`);
    }
  }));

  return results;
}
