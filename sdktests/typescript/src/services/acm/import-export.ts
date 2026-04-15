import type { ACMClient } from '@aws-sdk/client-acm';
import type { TestRunner, TestResult } from '../../runner.js';
import { safeCleanup } from '../../helpers.js';
import {
  ImportCertificateCommand,
  ExportCertificateCommand,
  DeleteCertificateCommand,
  RequestCertificateCommand,
  GetCertificateCommand,
} from '@aws-sdk/client-acm';
import { TEST_CERT_PEM, TEST_KEY_PEM, TEST_CHAIN_PEM } from './constants.js';

const SVC = 'acm';

function uniqueDomain(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 99999)}.com`;
}

export async function runImportExportTests(
  runner: TestRunner,
  client: ACMClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest(SVC, 'ImportCertificate', async () => {
    const resp = await client.send(new ImportCertificateCommand({
      Certificate: Buffer.from(TEST_CERT_PEM),
      PrivateKey: Buffer.from(TEST_KEY_PEM),
    }));
    try {
      if (!resp.CertificateArn) throw new Error('expected CertificateArn to be defined');
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: resp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'ImportCertificate_WithCertificateChain', async () => {
    const resp = await client.send(new ImportCertificateCommand({
      Certificate: Buffer.from(TEST_CERT_PEM),
      PrivateKey: Buffer.from(TEST_KEY_PEM),
      CertificateChain: Buffer.from(TEST_CHAIN_PEM),
    }));
    try {
      const getResp = await client.send(new GetCertificateCommand({ CertificateArn: resp.CertificateArn }));
      if (!getResp.CertificateChain) throw new Error('expected CertificateChain to be defined');
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: resp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'ExportCertificate', async () => {
    const importResp = await client.send(new ImportCertificateCommand({
      Certificate: Buffer.from(TEST_CERT_PEM),
      PrivateKey: Buffer.from(TEST_KEY_PEM),
    }));
    try {
      const exportResp = await client.send(new ExportCertificateCommand({
        CertificateArn: importResp.CertificateArn,
        Passphrase: Buffer.from('test-passphrase'),
      }));
      if (!exportResp.Certificate) throw new Error('expected Certificate to be defined');
      if (!exportResp.PrivateKey) throw new Error('expected PrivateKey to be defined');
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: importResp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'ExportCertificate_AMAZON_ISSUED_Error', async () => {
    const domain = uniqueDomain('export-ai');
    const resp = await client.send(new RequestCertificateCommand({
      DomainName: domain,
      ValidationMethod: 'DNS',
    }));
    try {
      let err: unknown;
      try {
        await client.send(new ExportCertificateCommand({
          CertificateArn: resp.CertificateArn,
          Passphrase: Buffer.from('test-passphrase'),
        }));
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
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: resp.CertificateArn })));
    }
  }));

  return results;
}
