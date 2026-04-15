import { STSClient } from '@aws-sdk/client-sts';
import { IAMClient, CreateRoleCommand, DeleteRoleCommand } from '@aws-sdk/client-iam';
import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { safeCleanup } from '../../helpers.js';
import { runBasicStsTests } from './basic.js';
import { runAssumeRoleTests } from './assume-role.js';

export function registerSTS(): ServiceRegistration {
  return {
    name: 'sts',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new STSClient({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });
      const iamClient = new IAMClient({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });

      const suffix = String(Date.now() % 1000000);
      const roleName = `TestRole-${suffix}`;
      const samlRoleName = `SAMLRole-${suffix}`;
      const webIdRoleName = `WebIdRole-${suffix}`;

      const trustPolicies = {
        assumeRole: JSON.stringify({
          Version: '2012-10-17',
          Statement: [{ Effect: 'Allow', Principal: { AWS: 'arn:aws:iam::000000000000:root' }, Action: 'sts:AssumeRole' }],
        }),
        saml: JSON.stringify({
          Version: '2012-10-17',
          Statement: [{ Effect: 'Allow', Principal: { Federated: 'arn:aws:iam::000000000000:saml-provider/TestProvider' }, Action: 'sts:AssumeRoleWithSAML' }],
        }),
        webId: JSON.stringify({
          Version: '2012-10-17',
          Statement: [{ Effect: 'Allow', Principal: { Federated: 'arn:aws:iam::000000000000:oidc-provider/example.com' }, Action: 'sts:AssumeRoleWithWebIdentity' }],
        }),
      };

      const roles = [
        { name: roleName, policy: trustPolicies.assumeRole },
        { name: samlRoleName, policy: trustPolicies.saml },
        { name: webIdRoleName, policy: trustPolicies.webId },
      ];

      for (const role of roles) {
        try {
          await iamClient.send(new CreateRoleCommand({
            RoleName: role.name,
            AssumeRolePolicyDocument: role.policy,
          }));
        } catch { /* may already exist */ }
      }

      try {
        const roleARN = `arn:aws:iam::000000000000:role/${roleName}`;
        const samlRoleARN = `arn:aws:iam::000000000000:role/${samlRoleName}`;
        const webIdRoleARN = `arn:aws:iam::000000000000:role/${webIdRoleName}`;
        const samlProviderARN = 'arn:aws:iam::000000000000:saml-provider/TestProvider';
        const dummySAMLAssertion = 'VGhpcyBpcyBhIGR1bW15IFNBTUwgYXNzZXJ0aW9u';

        const basicResults = await runBasicStsTests(runner, client);
        const assumeResults = await runAssumeRoleTests(
          runner, client, roleARN, samlRoleARN, webIdRoleARN, samlProviderARN, dummySAMLAssertion,
        );

        return [...basicResults, ...assumeResults];
      } finally {
        for (const role of roles) {
          await safeCleanup(() => iamClient.send(new DeleteRoleCommand({ RoleName: role.name })));
        }
      }
    },
  };
}
