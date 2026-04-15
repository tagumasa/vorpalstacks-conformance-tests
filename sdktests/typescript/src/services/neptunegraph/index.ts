import { NeptuneGraphClient } from '@aws-sdk/client-neptune-graph';
import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import type { NeptuneGraphState } from './context.js';
import { runGraphCrudTests } from './graph-crud.js';
import { runTaskAndTagTests } from './tasks.js';
import { runDataPlaneAndCleanupTests } from './data-plane.js';

export function registerNeptuneGraph(): ServiceRegistration {
  return {
    name: 'neptunegraph',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new NeptuneGraphClient({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
        disableHostPrefix: true,
      });

      const state: NeptuneGraphState = {
        graphID: '',
        graphARN: '',
        snapshotID: '',
        importTaskID: '',
        exportTaskID: '',
        restoredGraphID: '',
      };

      const results: TestResult[] = [];
      results.push(...await runGraphCrudTests(runner, client, state));
      results.push(...await runTaskAndTagTests(runner, client, state));
      results.push(...await runDataPlaneAndCleanupTests(runner, client, state));
      return results;
    },
  };
}
