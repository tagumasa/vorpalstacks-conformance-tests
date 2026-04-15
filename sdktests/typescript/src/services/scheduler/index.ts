import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { runScheduleTests } from './schedule.js';
import { runScheduleGroupTests } from './schedule-group.js';

export function registerScheduler(): ServiceRegistration {
  return {
    name: 'scheduler',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const results: TestResult[] = [];
      results.push(...await runScheduleTests(runner, ctx));
      results.push(...await runScheduleGroupTests(runner, ctx));
      return results;
    },
  };
}
