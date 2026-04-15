import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { registerCRUD } from './crud.js';
import { registerErrorTests } from './error-tests.js';
import { registerContentVerify } from './content-verify.js';
import { registerExpanded } from './expanded.js';
import { CloudTrailClient } from '@aws-sdk/client-cloudtrail';

export function registerCloudTrail(): ServiceRegistration {
  return {
    name: 'cloudtrail',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new CloudTrailClient({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });
      const results: TestResult[] = [];

      await registerCRUD(client, runner, results);
      await registerErrorTests(client, runner, results);
      await registerContentVerify(client, runner, results);
      await registerExpanded(client, runner, results);

      return results;
    },
  };
}
