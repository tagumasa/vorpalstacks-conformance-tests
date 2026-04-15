import { ACMClient } from '@aws-sdk/client-acm';
import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { runCertificateRequestTests, runDeleteTests, runListTests } from './certificate-crud.js';
import { runDescribeTests, runGetCertTests } from './describe-get.js';
import { runTagTests } from './tag-operations.js';
import { runResendUpdateRenewTests } from './resend-update-renew.js';
import { runRevokeTests } from './revoke.js';
import { runImportExportTests } from './import-export.js';
import { runAccountConfigTests } from './account-config.js';

export function registerACM(): ServiceRegistration {
  return {
    name: 'acm',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new ACMClient({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });

      const results: TestResult[] = [];
      const batches = [
        runCertificateRequestTests,
        runDescribeTests,
        runGetCertTests,
        runListTests,
        runDeleteTests,
        runTagTests,
        runResendUpdateRenewTests,
        runRevokeTests,
        runImportExportTests,
        runAccountConfigTests,
      ];

      for (const batch of batches) {
        results.push(...await batch(runner, client));
      }

      return results;
    },
  };
}
