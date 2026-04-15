import { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { createIAMTestContext } from './context.js';
import { runUserTests } from './user.js';
import { runGroupTests } from './group.js';
import { runRoleTests } from './role.js';
import { runPolicyTests } from './policy.js';
import { runInstanceProfileTests } from './instance-profile.js';
import { runAccountTests } from './account.js';
import { runSAMLTests } from './saml.js';
import { runMFATests } from './mfa.js';
import { runDeleteTests } from './delete.js';
import { runMultibyteTests } from './multibyte.js';

export function registerIAM(): ServiceRegistration {
  return {
    name: 'iam',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const iamCtx = createIAMTestContext(ctx.endpoint, ctx.region, ctx.credentials);
      const allResults: TestResult[] = [];

      allResults.push(...await runUserTests(iamCtx, runner));
      allResults.push(...await runGroupTests(iamCtx, runner));
      allResults.push(...await runRoleTests(iamCtx, runner));
      allResults.push(...await runPolicyTests(iamCtx, runner));
      allResults.push(...await runInstanceProfileTests(iamCtx, runner));
      allResults.push(...await runAccountTests(iamCtx, runner));
      allResults.push(...await runSAMLTests(iamCtx, runner));
      allResults.push(...await runMFATests(iamCtx, runner));
      allResults.push(...await runDeleteTests(iamCtx, runner));
      allResults.push(...await runMultibyteTests(iamCtx, runner));

      return allResults;
    },
  };
}
