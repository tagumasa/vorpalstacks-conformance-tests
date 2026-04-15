import { TimestreamWriteClient } from '@aws-sdk/client-timestream-write';
import { TimestreamQueryClient } from '@aws-sdk/client-timestream-query';
import { IAMClient } from '@aws-sdk/client-iam';
import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName } from '../../helpers.js';
import type { WriteCoreState } from './write-core.js';
import { runWriteCoreTests } from './write-core.js';
import { runTagsAndBatchTests } from './tags-and-batch.js';
import { runScheduledQueryTests } from './scheduled-query.js';

export function registerTimestream(): ServiceRegistration {
  return {
    name: 'timestream',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const clientConfig = { endpoint: ctx.endpoint, region: ctx.region, credentials: ctx.credentials };
      const writeClient = new TimestreamWriteClient(clientConfig);
      const queryClient = new TimestreamQueryClient(clientConfig);
      const iamClient = new IAMClient(clientConfig);

      const results: TestResult[] = [];

      const state: WriteCoreState = {
        region: ctx.region,
        db: makeUniqueName('ts-db'),
        tbl: makeUniqueName('ts-tbl'),
        dbCreated: false,
        tblCreated: false,
      };

      try {
        results.push(...await runWriteCoreTests(runner, writeClient, state));
        results.push(...await runTagsAndBatchTests(runner, writeClient, ctx.region));
        results.push(...await runScheduledQueryTests(runner, writeClient, queryClient, iamClient));
      } catch {
        // individual test errors are captured in results
      }

      return results;
    },
  };
}
