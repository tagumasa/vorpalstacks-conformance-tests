import { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { createS3TestContext, S3TestContext } from './context.js';
import { runBucketCoreTests } from './bucket-core.js';
import { runObjectDataTests } from './object-data.js';
import { runBucketConfigTests } from './bucket-config.js';
import { runObjectConfigTests } from './object-config.js';
import { runMultipartTests } from './multipart.js';
import { runSelectTests } from './select.js';
import { runErrorTests } from './error.js';
import { runMultibyteTests } from './multibyte.js';
import { runDeleteTests } from './delete.js';

export function registerS3(): ServiceRegistration {
  return {
    name: 's3',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const s3Ctx = await createS3TestContext(ctx.endpoint, ctx.region, ctx.credentials);
      const allResults: TestResult[] = [];

      try {
        allResults.push(...await runBucketCoreTests(s3Ctx, runner));
        allResults.push(...await runObjectDataTests(s3Ctx, runner));
        allResults.push(...await runBucketConfigTests(s3Ctx, runner));
        allResults.push(...await runObjectConfigTests(s3Ctx, runner));
        allResults.push(...await runMultipartTests(s3Ctx, runner));
        allResults.push(...await runSelectTests(s3Ctx, runner));
        allResults.push(...await runErrorTests(s3Ctx, runner));
        allResults.push(...await runMultibyteTests(s3Ctx, runner));
        allResults.push(...await runDeleteTests(s3Ctx, runner));
      } catch (err) {
        allResults.push({
          service: 's3',
          testName: 'UnexpectedError',
          status: 'FAIL',
          error: err instanceof Error ? err.message : String(err),
          durationMs: 0,
        });
      }

      return allResults;
    },
  };
}
