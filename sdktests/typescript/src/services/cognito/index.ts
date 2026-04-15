import { CognitoIdentityProviderClient, DeleteUserPoolCommand } from '@aws-sdk/client-cognito-identity-provider';
import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { runPoolTests, runPoolErrorTests, deletePoolAndCleanup } from './pool.js';
import { runUserTests, runGroupTests, runAuthAndGroupMembershipTests, runUserErrorTests } from './user.js';
import { runIdentityProviderTests, runResourceServerTests, runTagTests, runIdpRsErrorTests } from './identity.js';

export function registerCognito(): ServiceRegistration {
  return {
    name: 'cognito',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new CognitoIdentityProviderClient({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });

      const results: TestResult[] = [];

      const { results: poolResults, userPoolId, clientId } = await runPoolTests(runner, client);
      results.push(...poolResults);

      if (userPoolId) {
        const userResults = await runUserTests(runner, client, userPoolId);
        results.push(...userResults);

        const groupResults = await runGroupTests(runner, client, userPoolId);
        results.push(...groupResults);

        const idpResults = await runIdentityProviderTests(runner, client, userPoolId);
        results.push(...idpResults);

        const rsResults = await runResourceServerTests(runner, client, userPoolId);
        results.push(...rsResults);

        const authResults = await runAuthAndGroupMembershipTests(runner, client, userPoolId, clientId);
        results.push(...authResults);

        results.push(await runner.runTest('cognito', 'DeleteUserPool', async () => {
          await client.send(new DeleteUserPoolCommand({ UserPoolId: userPoolId }));
        }));
      }

      const errorResults = await runPoolErrorTests(runner, client);
      results.push(...errorResults);

      const userErrorResults = await runUserErrorTests(runner, client);
      results.push(...userErrorResults);

      const tagResults = await runTagTests(runner, client);
      results.push(...tagResults);

      const idpRsErrorResults = await runIdpRsErrorTests(runner, client);
      results.push(...idpRsErrorResults);

      return results;
    },
  };
}
