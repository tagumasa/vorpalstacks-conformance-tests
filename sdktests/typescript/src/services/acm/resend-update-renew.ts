import type { ACMClient } from '@aws-sdk/client-acm';
import type { TestRunner, TestResult } from '../../runner.js';
import { safeCleanup } from '../../helpers.js';
import {
  RequestCertificateCommand,
  ImportCertificateCommand,
  DeleteCertificateCommand,
  ResendValidationEmailCommand,
  UpdateCertificateOptionsCommand,
  RenewCertificateCommand,
} from '@aws-sdk/client-acm';
import { TEST_CERT_PEM, TEST_KEY_PEM } from './constants.js';

const SVC = 'acm';
const NONEXISTENT_ARN = 'arn:aws:acm:us-east-1:123456789012:certificate/nonexistent';

function uniqueDomain(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 99999)}.com`;
}

export async function runResendUpdateRenewTests(
  runner: TestRunner,
  client: ACMClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest(SVC, 'ResendValidationEmail', async () => {
    const domain = uniqueDomain('resend');
    const resp = await client.send(new RequestCertificateCommand({
      DomainName: domain,
      ValidationMethod: 'EMAIL',
    }));
    try {
      await client.send(new ResendValidationEmailCommand({
        CertificateArn: resp.CertificateArn,
        Domain: domain,
        ValidationDomain: domain,
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: resp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'ResendValidationEmail_IssuedStatus', async () => {
    const importResp = await client.send(new ImportCertificateCommand({
      Certificate: Buffer.from(TEST_CERT_PEM),
      PrivateKey: Buffer.from(TEST_KEY_PEM),
    }));
    try {
      let err: unknown;
      try {
        await client.send(new ResendValidationEmailCommand({
          CertificateArn: importResp.CertificateArn,
          Domain: 'example.com',
          ValidationDomain: 'example.com',
        }));
      } catch (e) {
        err = e;
      }
      if (!err) throw new Error('expected InvalidStateException');
      const name = (err as { name?: string }).name ?? '';
      const msg = err instanceof Error ? err.message : '';
      if (!name.includes('InvalidStateException') && !msg.includes('InvalidStateException')) {
        throw new Error(`expected InvalidStateException, got: ${msg}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: importResp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'UpdateCertificateOptions_VerifyInDescribe', async () => {
    const domain = uniqueDomain('updopt');
    const { DescribeCertificateCommand } = await import('@aws-sdk/client-acm');
    const resp = await client.send(new RequestCertificateCommand({
      DomainName: domain,
      ValidationMethod: 'DNS',
    }));
    try {
      await client.send(new UpdateCertificateOptionsCommand({
        CertificateArn: resp.CertificateArn,
        Options: { CertificateTransparencyLoggingPreference: 'DISABLED' },
      }));
      const desc = await client.send(new DescribeCertificateCommand({ CertificateArn: resp.CertificateArn }));
      const opts = desc.Certificate?.Options;
      if (!opts) throw new Error('expected Options to be defined after update');
      if (opts.CertificateTransparencyLoggingPreference !== 'DISABLED') {
        throw new Error(`expected DISABLED, got ${opts.CertificateTransparencyLoggingPreference}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: resp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'UpdateCertificateOptions_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new UpdateCertificateOptionsCommand({
        CertificateArn: NONEXISTENT_ARN,
        Options: { CertificateTransparencyLoggingPreference: 'ENABLED' },
      }));
    } catch (e) {
      err = e;
    }
    if (!err) throw new Error('expected ResourceNotFoundException');
    const name = (err as { name?: string }).name ?? '';
    const msg = err instanceof Error ? err.message : '';
    if (!name.includes('ResourceNotFoundException') && !msg.includes('ResourceNotFoundException')) {
      throw new Error(`expected ResourceNotFoundException, got: ${msg}`);
    }
  }));

  results.push(await runner.runTest(SVC, 'RenewCertificate_ImportedCert_Error', async () => {
    const importResp = await client.send(new ImportCertificateCommand({
      Certificate: Buffer.from(TEST_CERT_PEM),
      PrivateKey: Buffer.from(TEST_KEY_PEM),
    }));
    try {
      let err: unknown;
      try {
        await client.send(new RenewCertificateCommand({ CertificateArn: importResp.CertificateArn }));
      } catch (e) {
        err = e;
      }
      if (!err) throw new Error('expected ValidationException');
      const name = (err as { name?: string }).name ?? '';
      const msg = err instanceof Error ? err.message : '';
      if (!name.includes('ValidationException') && !msg.includes('ValidationException')) {
        throw new Error(`expected ValidationException, got: ${msg}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: importResp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'RenewCertificate_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new RenewCertificateCommand({ CertificateArn: NONEXISTENT_ARN }));
    } catch (e) {
      err = e;
    }
    if (!err) throw new Error('expected ResourceNotFoundException');
    const name = (err as { name?: string }).name ?? '';
    const msg = err instanceof Error ? err.message : '';
    if (!name.includes('ResourceNotFoundException') && !msg.includes('ResourceNotFoundException')) {
      throw new Error(`expected ResourceNotFoundException, got: ${msg}`);
    }
  }));

  return results;
}
