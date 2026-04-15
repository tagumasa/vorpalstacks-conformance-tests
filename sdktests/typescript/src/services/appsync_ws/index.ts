import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { runConnectionTests } from './connection.js';
import { runSubscribeTests } from './subscribe.js';
import { runPublishTests } from './publish.js';
import { runUnsubscribeTests } from './unsubscribe.js';
import { runHTTPPublishTests } from './http_publish.js';

export function registerAppSyncWS(): ServiceRegistration {
  return {
    name: 'appsync_ws',
    category: 'ws',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const wsHost = ctx.endpoint.replace(/^https?:\/\//, '');
      const wsUrl = `ws://${wsHost.replace(/:\d+$/, ':8086')}/event/realtime`;
      const httpUrl = `http://${wsHost.replace(/:\d+$/, ':8086')}/event`;
      const results: TestResult[] = [];

      await runConnectionTests(runner, wsUrl, results);
      await runSubscribeTests(runner, wsUrl, results);
      await runPublishTests(runner, wsUrl, results);
      await runUnsubscribeTests(runner, wsUrl, results);
      await runHTTPPublishTests(runner, wsUrl, httpUrl, results);

      return results;
    },
  };
}
