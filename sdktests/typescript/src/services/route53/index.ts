import { Route53Client } from '@aws-sdk/client-route-53';
import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { runHostedZoneTests } from './hosted-zone.js';
import { runHealthCheckTests } from './health-check.js';
import { runAdvancedTests } from './advanced.js';

export function registerRoute53(): ServiceRegistration {
  return {
    name: 'route53',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new Route53Client({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });
      const results: TestResult[] = [];
      results.push(...await runHostedZoneTests(runner, client));
      results.push(...await runHealthCheckTests(runner, client));
      results.push(...await runAdvancedTests(runner, client));
      return results;
    },
  };
}
