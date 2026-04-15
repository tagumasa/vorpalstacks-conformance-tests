import {
  CloudWatchLogsClient,
  DeleteLogGroupCommand,
} from '@aws-sdk/client-cloudwatch-logs';
import { IAMClient } from '@aws-sdk/client-iam';
import type { ServiceRegistration, ServiceContext, TestRunner } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';
import { runCRUDTests } from './crud.js';
import { runErrorAndRoundtripTests } from './error-tests.js';
import { runTaggingTests } from './tagging.js';
import { runFilterAndRetentionTests } from './filters.js';
import { runSubscriptionFilterTests } from './subscription.js';
import { runDestinationTests } from './destinations.js';
import { runPaginationTests } from './pagination.js';
import { runExtendedTests } from './extended.js';
import { runMultibyteTests } from './multibyte.js';

export function registerCloudWatchLogs(): ServiceRegistration {
  return {
    name: 'cloudwatchlogs',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext) => {
      const client = new CloudWatchLogsClient({ endpoint: ctx.endpoint, region: ctx.region, credentials: ctx.credentials });
      const iamClient = new IAMClient({ endpoint: ctx.endpoint, region: ctx.region, credentials: ctx.credentials });
      const results: Awaited<ReturnType<typeof runner.runTest>>[] = [];

      const logGroupName = makeUniqueName('TestLogGroup');
      const logStreamName = makeUniqueName('TestLogStream');

      try {
        await runCRUDTests(runner, client, logGroupName, logStreamName, results);
        await runErrorAndRoundtripTests(runner, client, results);
        await runTaggingTests(runner, client, results);
        await runFilterAndRetentionTests(runner, client, results);
        await runSubscriptionFilterTests(runner, client, iamClient, results);
        await runDestinationTests(runner, client, results);
        await runPaginationTests(runner, client, results);
        await runExtendedTests(runner, client, results);
        await runMultibyteTests(runner, client, logGroupName, results);
      } finally {
        await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName })));
      }

      return results;
    },
  };
}
