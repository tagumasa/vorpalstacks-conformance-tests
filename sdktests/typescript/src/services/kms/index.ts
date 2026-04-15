import { KMSClient } from '@aws-sdk/client-kms';
import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName } from '../../helpers.js';
import { KmsState } from './context.js';
import { runKeyCrudTests, runKeyContentTests } from './key-crud.js';
import { runAliasTests } from './alias.js';
import { runCryptoTests } from './crypto-ops.js';
import { runSignVerifyTests } from './sign-verify-mac.js';
import { runPolicyGrantTagRotationTests } from './policy-grant-tag-rotation.js';
import { runMultibyteTests } from './multibyte.js';

export function registerKMS(): ServiceRegistration {
  return {
    name: 'kms',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new KMSClient({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });

      const keyDescription = makeUniqueName('Test Key');
      const keyAlias = `alias/test-key-${Date.now()}`;
      const state: KmsState = { keyID: '', rsaKeyID: '', hmacKeyID: '' };

      const results: TestResult[] = [];

      results.push(...await runKeyCrudTests(runner, client, state, keyDescription));
      results.push(...await runAliasTests(runner, client, state, keyAlias));
      results.push(...await runKeyContentTests(runner, client, state));
      results.push(...await runSignVerifyTests(runner, client, state));
      results.push(...await runCryptoTests(runner, client, state));
      results.push(...await runPolicyGrantTagRotationTests(runner, client, state));
      results.push(...await runMultibyteTests(runner, client, state));

      return results;
    },
  };
}
