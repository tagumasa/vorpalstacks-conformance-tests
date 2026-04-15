import {
  DeleteTableCommand,
} from '@aws-sdk/client-dynamodb';
import { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { createDynamoDBTestContext } from './context.js';
import { runTableTests } from './table.js';
import { runItemTests } from './item.js';
import { runQueryTests } from './query.js';
import { runBatchTests } from './batch.js';
import { runTransactionTests } from './transaction.js';
import { runPartiQLTests } from './partiql.js';
import { runMultibyteTests } from './multibyte.js';

export function registerDynamoDB(): ServiceRegistration {
  return {
    name: 'dynamodb',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const ddbCtx = createDynamoDBTestContext(ctx.endpoint, ctx.region, ctx.credentials);
      const allResults: TestResult[] = [];

      try {
        allResults.push(...await runTableTests(ddbCtx, runner));
        allResults.push(...await runItemTests(ddbCtx, runner));
        allResults.push(...await runQueryTests(ddbCtx, runner));
        allResults.push(...await runBatchTests(ddbCtx, runner));
        allResults.push(...await runTransactionTests(ddbCtx, runner));
        allResults.push(...await runPartiQLTests(ddbCtx, runner));
        allResults.push(...await runMultibyteTests(ddbCtx, runner));

        allResults.push(await runner.runTest('dynamodb', 'DeleteTable', async () => {
          await ddbCtx.client.send(new DeleteTableCommand({ TableName: ddbCtx.tableName }));
        }));
      } finally {
        try {
          await ddbCtx.client.send(new DeleteTableCommand({ TableName: ddbCtx.tableName }));
        } catch {
          // table already deleted or doesn't exist
        }
        try {
          await ddbCtx.client.send(new DeleteTableCommand({ TableName: ddbCtx.compTableName }));
        } catch {
          // table already deleted or doesn't exist
        }
      }

      return allResults;
    },
  };
}
