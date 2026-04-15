import type { ACMClient } from '@aws-sdk/client-acm';
import type { TestRunner, TestResult } from '../../runner.js';
import { safeCleanup } from '../../helpers.js';
import {
  RequestCertificateCommand,
  ImportCertificateCommand,
  DeleteCertificateCommand,
  RevokeCertificateCommand,
  DescribeCertificateCommand,
} from '@aws-sdk/client-acm';
import { TEST_CERT_PEM, TEST_KEY_PEM } from './constants.js';

const SVC = 'acm';
const NONEXISTENT_ARN = 'arn:aws:acm:us-east-1:123456789012:certificate/nonexistent';

function uniqueDomain(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 99999)}.com`;
}

export async function runRevokeTests(
  runner: TestRunner,
  client: ACMClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest(SVC, 'RevokeCertificate_ImportedCert', async () => {
    const importResp = await client.send(new ImportCertificateCommand({
      Certificate: Buffer.from(TEST_CERT_PEM),
      PrivateKey: Buffer.from(TEST_KEY_PEM),
    }));
    try {
      await client.send(new RevokeCertificateCommand({
        CertificateArn: importResp.CertificateArn,
        RevocationReason: 'KEY_COMPROMISE',
      }));
      const desc = await client.send(new DescribeCertificateCommand({ CertificateArn: importResp.CertificateArn }));
      if (desc.Certificate?.Status !== 'REVOKED') throw new Error(`expected REVOKED, got ${desc.Certificate?.Status}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: importResp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'RevokeCertificate_VerifyRevokedAt', async () => {
    const importResp = await client.send(new ImportCertificateCommand({
      Certificate: Buffer.from(TEST_CERT_PEM),
      PrivateKey: Buffer.from(TEST_KEY_PEM),
    }));
    try {
      await client.send(new RevokeCertificateCommand({
        CertificateArn: importResp.CertificateArn,
        RevocationReason: 'SUPERSEDED',
      }));
      const desc = await client.send(new DescribeCertificateCommand({ CertificateArn: importResp.CertificateArn }));
      if (!desc.Certificate?.RevokedAt) throw new Error('expected RevokedAt to be defined');
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: importResp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'RevokeCertificate_VerifyRevocationReason', async () => {
    const importResp = await client.send(new ImportCertificateCommand({
      Certificate: Buffer.from(TEST_CERT_PEM),
      PrivateKey: Buffer.from(TEST_KEY_PEM),
    }));
    try {
      await client.send(new RevokeCertificateCommand({
        CertificateArn: importResp.CertificateArn,
        RevocationReason: 'KEY_COMPROMISE',
      }));
      const desc = await client.send(new DescribeCertificateCommand({ CertificateArn: importResp.CertificateArn }));
      if (desc.Certificate?.RevocationReason !== 'KEY_COMPROMISE') {
        throw new Error(`expected KEY_COMPROMISE, got ${desc.Certificate?.RevocationReason}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: importResp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'RevokeCertificate_AlreadyRevoked', async () => {
    const importResp = await client.send(new ImportCertificateCommand({
      Certificate: Buffer.from(TEST_CERT_PEM),
      PrivateKey: Buffer.from(TEST_KEY_PEM),
    }));
    try {
      await client.send(new RevokeCertificateCommand({
        CertificateArn: importResp.CertificateArn,
        RevocationReason: 'KEY_COMPROMISE',
      }));
      let err: unknown;
      try {
        await client.send(new RevokeCertificateCommand({
          CertificateArn: importResp.CertificateArn,
          RevocationReason: 'KEY_COMPROMISE',
        }));
      } catch (e) {
        err = e;
      }
      if (!err) throw new Error('expected error for already revoked cert');
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: importResp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'RevokeCertificate_PendingValidation', async () => {
    const domain = uniqueDomain('revoke-pv');
    const resp = await client.send(new RequestCertificateCommand({
      DomainName: domain,
      ValidationMethod: 'DNS',
    }));
    try {
      await client.send(new RevokeCertificateCommand({
        CertificateArn: resp.CertificateArn,
        RevocationReason: 'KEY_COMPROMISE',
      }));
      const desc = await client.send(new DescribeCertificateCommand({ CertificateArn: resp.CertificateArn }));
      if (desc.Certificate?.Status !== 'REVOKED') throw new Error(`expected REVOKED, got ${desc.Certificate?.Status}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: resp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'RevokeCertificate_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new RevokeCertificateCommand({
        CertificateArn: NONEXISTENT_ARN,
        RevocationReason: 'KEY_COMPROMISE',
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

  return results;
}
