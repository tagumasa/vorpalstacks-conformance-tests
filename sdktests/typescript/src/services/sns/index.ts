import { SNSClient } from '@aws-sdk/client-sns';
import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import type { TopicState } from './context.js';
import { runTopicLifecycleTests } from './topic-lifecycle.js';
import { runAdvancedTests } from './advanced.js';
import { runErrorAndEdgeTests } from './error-and-edge.js';
import { runMultibyteTests } from './multibyte.js';

export function registerSNS(): ServiceRegistration {
  return {
    name: 'sns',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new SNSClient({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });
      const results: TestResult[] = [];

      const state: TopicState = {
        topicArn: '',
        subscriptionArn: '',
        fifoTopicArn: '',
      };

      results.push(...await runTopicLifecycleTests(runner, client, state));
      results.push(...await runAdvancedTests(runner, client));
      results.push(...await runErrorAndEdgeTests(runner, client, state));
      results.push(...await runMultibyteTests(runner, client, state.topicArn));

      return results;
    },
  };
}
