import type { ACMClient } from '@aws-sdk/client-acm';
import type { TestRunner, TestResult } from '../../runner.js';
import { safeCleanup } from '../../helpers.js';
import {
  DescribeCertificateCommand,
  ImportCertificateCommand,
  GetCertificateCommand,
  DeleteCertificateCommand,
} from '@aws-sdk/client-acm';
import { TEST_CERT_PEM, TEST_KEY_PEM, TEST_CHAIN_PEM } from './constants.js';

const SVC = 'acm';
const NONEXISTENT_ARN = 'arn:aws:acm:us-east-1:123456789012:certificate/nonexistent';

function uniqueDomain(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 99999)}.com`;
}

export async function runDescribeTests(
  runner: TestRunner,
  client: ACMClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest(SVC, 'DescribeCertificate_AMAZON_ISSUED_Fields', async () => {
    const domain = uniqueDomain('desc-ai');
    const { RequestCertificateCommand } = await import('@aws-sdk/client-acm');
    const resp = await client.send(new RequestCertificateCommand({
      DomainName: domain,
      ValidationMethod: 'DNS',
    }));
    try {
      const desc = await client.send(new DescribeCertificateCommand({ CertificateArn: resp.CertificateArn }));
      const cert = desc.Certificate;
      if (!cert) throw new Error('expected Certificate to be defined');
      if (cert.Status !== 'ISSUED') throw new Error(`expected ISSUED, got ${cert.Status}`);
      if (cert.Type !== 'AMAZON_ISSUED') throw new Error(`expected AMAZON_ISSUED, got ${cert.Type}`);
      if (cert.RenewalEligibility !== 'ELIGIBLE') throw new Error(`expected ELIGIBLE, got ${cert.RenewalEligibility}`);
      if (cert.DomainName !== domain) throw new Error(`expected ${domain}, got ${cert.DomainName}`);
      if (cert.KeyAlgorithm !== 'RSA_2048') throw new Error(`expected RSA_2048, got ${cert.KeyAlgorithm}`);
      if (!cert.CertificateArn?.includes('acm')) throw new Error(`CertificateArn missing or malformed: ${cert.CertificateArn}`);
      if (!cert.CreatedAt) throw new Error('expected CreatedAt to be defined');
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: resp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'DescribeCertificate_DomainValidationOptions_DNS', async () => {
    const domain = uniqueDomain('dv-dns');
    const { RequestCertificateCommand } = await import('@aws-sdk/client-acm');
    const resp = await client.send(new RequestCertificateCommand({
      DomainName: domain,
      ValidationMethod: 'DNS',
    }));
    try {
      const desc = await client.send(new DescribeCertificateCommand({ CertificateArn: resp.CertificateArn }));
      const dvos = desc.Certificate?.DomainValidationOptions;
      if (!dvos?.length) throw new Error(`expected 1 DVO, got ${dvos?.length}`);
      const dvo = dvos[0];
      if (dvo.ValidationMethod !== 'DNS') throw new Error(`expected DNS, got ${dvo.ValidationMethod}`);
      const rr = dvo.ResourceRecord;
      if (!rr) throw new Error('expected ResourceRecord to be defined');
      if (rr.Type !== 'CNAME') throw new Error(`expected CNAME, got ${rr.Type}`);
      if (!rr.Name?.includes(domain)) throw new Error(`ResourceRecord.Name should contain domain, got ${rr.Name}`);
      if (!rr.Value) throw new Error('expected ResourceRecord.Value to be defined');
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: resp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'DescribeCertificate_IMPORTED_Fields', async () => {
    const importResp = await client.send(new ImportCertificateCommand({
      Certificate: Buffer.from(TEST_CERT_PEM),
      PrivateKey: Buffer.from(TEST_KEY_PEM),
    }));
    try {
      const desc = await client.send(new DescribeCertificateCommand({ CertificateArn: importResp.CertificateArn }));
      const cert = desc.Certificate;
      if (!cert) throw new Error('expected Certificate to be defined');
      if (cert.Status !== 'ISSUED') throw new Error(`expected ISSUED, got ${cert.Status}`);
      if (cert.Type !== 'IMPORTED') throw new Error(`expected IMPORTED, got ${cert.Type}`);
      if (cert.RenewalEligibility !== 'INELIGIBLE') throw new Error(`expected INELIGIBLE, got ${cert.RenewalEligibility}`);
      if (!cert.ImportedAt) throw new Error('expected ImportedAt to be defined');
      if (!cert.NotBefore || !cert.NotAfter) throw new Error('expected NotBefore and NotAfter to be defined');
      if (cert.KeyAlgorithm !== 'RSA_2048') throw new Error(`expected RSA_2048, got ${cert.KeyAlgorithm}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: importResp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'DescribeCertificate_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new DescribeCertificateCommand({ CertificateArn: NONEXISTENT_ARN }));
    } catch (e) {
      err = e;
    }
    if (!err) throw new Error('expected ResourceNotFoundException');
    const msg = err instanceof Error ? err.message : String(err);
    const name = (err as { name?: string }).name ?? '';
    if (!msg.includes('ResourceNotFoundException') && !name.includes('ResourceNotFoundException')) {
      throw new Error(`expected ResourceNotFoundException, got: ${msg}`);
    }
  }));

  return results;
}

export async function runGetCertTests(
  runner: TestRunner,
  client: ACMClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest(SVC, 'GetCertificate_ImportedCert', async () => {
    const importResp = await client.send(new ImportCertificateCommand({
      Certificate: Buffer.from(TEST_CERT_PEM),
      PrivateKey: Buffer.from(TEST_KEY_PEM),
      CertificateChain: Buffer.from(TEST_CHAIN_PEM),
    }));
    try {
      const getResp = await client.send(new GetCertificateCommand({ CertificateArn: importResp.CertificateArn }));
      if (!getResp.Certificate) throw new Error('expected Certificate to be defined');
      if (!getResp.CertificateChain) throw new Error('expected CertificateChain to be defined');
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: importResp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'GetCertificate_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new GetCertificateCommand({ CertificateArn: NONEXISTENT_ARN }));
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
