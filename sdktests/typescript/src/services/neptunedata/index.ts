import {
  NeptunedataClient as NeptuneDataClient,
  ExecuteOpenCypherQueryCommand,
  ExecuteGremlinQueryCommand,
  ExecuteFastResetCommand,
  GetLoaderJobStatusCommand,
  CancelLoaderJobCommand,
} from '@aws-sdk/client-neptunedata';
import { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { runEngineAndLoaderTests } from './engine-and-loader.js';
import { runCypherTests } from './cypher-advanced.js';
import { runGremlinTests } from './gremlin-advanced.js';

export function registerNeptuneData(): ServiceRegistration {
  return {
    name: 'neptunedata',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new NeptuneDataClient({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });
      const results: TestResult[] = [];

      await runEngineAndLoaderTests(client, runner, results);
      await runCypherTests(client, runner, results);
      await runGremlinTests(client, runner, results);

      return results;
    },
  };
}
