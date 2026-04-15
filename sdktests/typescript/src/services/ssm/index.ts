import { SSMClient } from '@aws-sdk/client-ssm';
import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { runParameterTests } from './parameter.js';
import { runAdvancedParameterTests } from './advanced.js';
import { runMultibyteTests } from './multibyte.js';

export function registerSSM(): ServiceRegistration {
  return {
    name: 'ssm',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new SSMClient({ endpoint: ctx.endpoint, region: ctx.region, credentials: ctx.credentials });
      const results: TestResult[] = [];
      results.push(...await runParameterTests(runner, client));
      results.push(...await runAdvancedParameterTests(runner, client));
      results.push(...await runMultibyteTests(runner, client));
      return results;
    },
  };
}
