import { CloudWatchClient } from '@aws-sdk/client-cloudwatch';
import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { runMetricDataTests } from './metric-data.js';
import { runAlarmCrudTests } from './alarm-crud.js';
import { runAlarmStateTests } from './alarm-state.js';
import { runTagAndActionTests } from './tags-and-actions.js';
import { runCompositeAlarmTests } from './composite-alarm.js';

export function registerCloudWatch(): ServiceRegistration {
  return {
    name: 'cloudwatch',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new CloudWatchClient({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });

      const results: TestResult[] = [];
      const batches = [
        runMetricDataTests,
        runAlarmCrudTests,
        runAlarmStateTests,
        runTagAndActionTests,
        runCompositeAlarmTests,
      ];

      for (const batch of batches) {
        results.push(...await batch(runner, client));
      }

      return results;
    },
  };
}
