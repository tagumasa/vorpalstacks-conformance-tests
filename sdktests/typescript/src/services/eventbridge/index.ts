import { EventBridgeClient } from '@aws-sdk/client-eventbridge';
import type { ServiceRegistration, ServiceContext, TestRunner } from '../../runner.js';
import { runCrudTests } from './crud.js';
import { runErrorTests } from './error-tests.js';
import { runArchiveTests } from './archives.js';
import { runConnectionTests } from './connections.js';
import { runApiDestinationTests } from './api-destinations.js';
import { runReplayTests } from './replays.js';
import { runOtherTests } from './other.js';
import { runMultibyteTests } from './multibyte.js';

export function registerEventBridge(): ServiceRegistration {
  return {
    name: 'eventbridge',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext) => {
      const client = new EventBridgeClient({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });

      const results = [
        ...(await runCrudTests(runner, client, ctx)),
        ...(await runErrorTests(runner, client, ctx)),
        ...(await runArchiveTests(runner, client, ctx)),
        ...(await runConnectionTests(runner, client, ctx)),
        ...(await runApiDestinationTests(runner, client, ctx)),
        ...(await runReplayTests(runner, client, ctx)),
        ...(await runOtherTests(runner, client, ctx)),
        ...(await runMultibyteTests(runner, client, ctx)),
      ];

      return results;
    },
  };
}
