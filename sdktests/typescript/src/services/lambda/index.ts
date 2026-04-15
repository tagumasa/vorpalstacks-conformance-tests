import { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { createLambdaTestContext, cleanupLambdaTestContext, LambdaTestContext } from './context.js';
import { runFunctionTests, runFunctionVerifyTests } from './function.js';
import { runLayerTests } from './layer.js';
import { runEventSourceTests } from './event-source.js';
import { runProvisionedConcurrencyTests } from './provisioned-concurrency.js';
import { runEventInvokeConfigTests } from './event-invoke-config.js';
import { runFunctionUrlTests } from './function-url.js';
import { runInvokeAsyncTests } from './invoke-async.js';
import { runErrorTests } from './error.js';
import { runPaginationTests } from './pagination.js';
import { runMultibyteTests } from './multibyte.js';

export function registerLambda(): ServiceRegistration {
  return {
    name: 'lambda',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const lambdaCtx = await createLambdaTestContext(ctx.endpoint, ctx.region, ctx.credentials);
      const allResults: TestResult[] = [];

      try {
        allResults.push(...await runFunctionTests(lambdaCtx, runner));
        allResults.push(...await runFunctionVerifyTests(lambdaCtx, runner));
        allResults.push(...await runLayerTests(lambdaCtx, runner));
        allResults.push(...await runEventSourceTests(lambdaCtx, runner));
        allResults.push(...await runProvisionedConcurrencyTests(lambdaCtx, runner));
        allResults.push(...await runEventInvokeConfigTests(lambdaCtx, runner));
        allResults.push(...await runFunctionUrlTests(lambdaCtx, runner));
        allResults.push(...await runInvokeAsyncTests(lambdaCtx, runner));
        allResults.push(...await runErrorTests(lambdaCtx, runner));
        allResults.push(...await runPaginationTests(lambdaCtx, runner));
        allResults.push(...await runMultibyteTests(lambdaCtx, runner));
      } finally {
        await cleanupLambdaTestContext(lambdaCtx);
      }

      return allResults;
    },
  };
}
