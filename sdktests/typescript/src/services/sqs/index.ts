import { SQSClient } from '@aws-sdk/client-sqs';
import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { runQueueCrudTests } from './queue.js';
import { runMessageAndAdvancedTests } from './message.js';
import { runMultibyteTests } from './multibyte.js';

export function registerSQS(): ServiceRegistration {
  return {
    name: 'sqs',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new SQSClient({ endpoint: ctx.endpoint, region: ctx.region, credentials: ctx.credentials });
      const ts = String(Date.now());
      const queueName = `TestQueue-${ts}`;
      const batchQueueName = `TestBatchQueue-${ts}`;

      const crudResult = await runQueueCrudTests(runner, client, ts, queueName);
      const msgResults = await runMessageAndAdvancedTests(runner, client, ts, crudResult.qUrl, batchQueueName);
      const mbResults = await runMultibyteTests(runner, client, crudResult.qUrl);

      return [...crudResult.results, ...msgResults, ...mbResults];
    },
  };
}
