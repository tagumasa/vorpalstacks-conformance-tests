import { KinesisClient } from '@aws-sdk/client-kinesis';
import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName } from '../../helpers.js';
import type { StreamState } from './context.js';
import { runStreamLifecycleTests } from './stream-lifecycle.js';
import { runIndependentTests } from './independent.js';

export function registerKinesis(): ServiceRegistration {
  return {
    name: 'kinesis',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new KinesisClient({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });
      const results: TestResult[] = [];

      const state: StreamState = {
        name: makeUniqueName('ts-kinesis'),
        arn: '',
        shardIds: [],
        created: false,
      };

      results.push(...await runStreamLifecycleTests(runner, client, state));
      results.push(...await runIndependentTests(runner, client));

      return results;
    },
  };
}
