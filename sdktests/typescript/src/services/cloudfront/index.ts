import { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { createCloudFrontTestContext } from './context.js';
import { runDistributionTests } from './distribution.js';
import { runDistributionTagsTests } from './distribution-tags.js';
import { runInvalidationTests } from './invalidation.js';
import { runOriginAccessControlTests } from './origin-access-control.js';
import { runCachePolicyTests } from './cache-policy.js';
import { runOriginRequestPolicyTests } from './origin-request-policy.js';
import { runResponseHeadersPolicyTests } from './response-headers-policy.js';
import { runKeyGroupsTests } from './key-groups.js';

export function registerCloudFront(): ServiceRegistration {
  return {
    name: "cloudfront",
    category: "sdk",
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const cfCtx = createCloudFrontTestContext(ctx);
      const allResults: TestResult[] = [];

      allResults.push(...await runDistributionTests(cfCtx, runner));
      allResults.push(...await runDistributionTagsTests(cfCtx, runner));
      allResults.push(...await runInvalidationTests(cfCtx, runner));
      allResults.push(...await runOriginAccessControlTests(cfCtx, runner));
      allResults.push(...await runCachePolicyTests(cfCtx, runner));
      allResults.push(...await runOriginRequestPolicyTests(cfCtx, runner));
      allResults.push(...await runResponseHeadersPolicyTests(cfCtx, runner));
      allResults.push(...await runKeyGroupsTests(cfCtx, runner));

      return allResults;
    },
  };
}
