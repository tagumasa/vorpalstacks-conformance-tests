import {
  CognitoIdentityClient,
  DeleteIdentityPoolCommand,
} from '@aws-sdk/client-cognito-identity';
import { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { safeCleanup } from '../../helpers.js';
import type { CognitoIdentityTestContext } from './context.js';
import { runIdentityPoolTests } from './identity-pool.js';
import { runIdentityTests } from './identity.js';
import { runOpenIdTokenTests } from './openid-token.js';
import { runRolesTests } from './roles.js';
import { runTagsTests } from './tags.js';
import { runPrincipalTagsTests } from './principal-tags.js';
import { runDeveloperIdentityTests } from './developer-identity.js';

export function registerCognitoIdentity(): ServiceRegistration {
  return {
    name: 'cognito_identity',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new CognitoIdentityClient({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });
      const svc = 'cognito-identity';
      const testCtx: CognitoIdentityTestContext = {
        client,
        svc,
        poolId: '',
        poolArn: '',
        identityId: '',
      };
      const allResults: TestResult[] = [];

      try {
        allResults.push(...await runIdentityPoolTests(testCtx, runner));
        allResults.push(...await runIdentityTests(testCtx, runner));
        allResults.push(...await runOpenIdTokenTests(testCtx, runner));
        allResults.push(...await runRolesTests(testCtx, runner));
        allResults.push(...await runTagsTests(testCtx, runner));
        allResults.push(...await runPrincipalTagsTests(testCtx, runner));
        allResults.push(...await runDeveloperIdentityTests(testCtx, runner));

        allResults.push(await runner.runTest(svc, 'DeleteIdentityPool', async () => {
          await client.send(new DeleteIdentityPoolCommand({ IdentityPoolId: testCtx.poolId }));
        }));
      } finally {
        await safeCleanup(() => client.send(new DeleteIdentityPoolCommand({ IdentityPoolId: testCtx.poolId })));
      }

      return allResults;
    },
  };
}
