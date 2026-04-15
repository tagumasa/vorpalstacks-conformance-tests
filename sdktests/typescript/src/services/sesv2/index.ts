import { SESv2Client } from '@aws-sdk/client-sesv2';
import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import type { Sesv2State } from './context.js';
import { runConfigSetTests } from './config-set.js';
import { runEmailIdentityTests } from './email-identity.js';
import { runIndependentTests } from './independent.js';

export function registerSESv2(): ServiceRegistration {
  return {
    name: 'sesv2',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new SESv2Client({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });
      const results: TestResult[] = [];
      const state: Sesv2State = { region: ctx.region };

      results.push(...await runIndependentTests(runner, client));
      results.push(...await runConfigSetTests(runner, client, state));
      results.push(...await runEmailIdentityTests(runner, client));

      return results;
    },
  };
}
