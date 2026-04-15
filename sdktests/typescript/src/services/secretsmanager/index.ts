import { SecretsManagerClient } from '@aws-sdk/client-secrets-manager';
import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName } from '../../helpers.js';
import { runSecretLifecycleTests } from './secret-lifecycle.js';
import { runPasswordAndPolicyValidationTests } from './password-and-validation.js';
import { runSecretValueTests } from './secret-value.js';
import { runPolicyTests } from './policy.js';
import { runRotationRestoreStagingTests } from './rotation-restore.js';
import { runListBatchTagTests } from './list-batch-tags.js';
import { runMultibyteTests } from './multibyte.js';

export function registerSecretsManager(): ServiceRegistration {
  return {
    name: 'secretsmanager',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new SecretsManagerClient({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });

      const results: TestResult[] = [];

      await runSecretLifecycleTests(client, runner, results);
      await runPasswordAndPolicyValidationTests(client, runner, results);
      await runSecretValueTests(client, runner, results);
      await runPolicyTests(client, runner, results);
      await runRotationRestoreStagingTests(client, runner, results);
      await runListBatchTagTests(client, runner, results);
      await runMultibyteTests(client, runner, results);

      return results;
    },
  };
}
