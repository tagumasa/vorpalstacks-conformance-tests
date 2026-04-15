import type { ACMClient } from '@aws-sdk/client-acm';
import type { TestRunner, TestResult } from '../../runner.js';
import { safeCleanup } from '../../helpers.js';
import {
  RequestCertificateCommand,
  DeleteCertificateCommand,
  DescribeCertificateCommand,
} from '@aws-sdk/client-acm';

const SVC = 'acm';

function uniqueDomain(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 99999)}.com`;
}

export async function runCertificateRequestTests(
  runner: TestRunner,
  client: ACMClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest(SVC, 'RequestCertificate_WithSubjectAlternativeNames', async () => {
    const domain = uniqueDomain('san-test');
    const resp = await client.send(new RequestCertificateCommand({
      DomainName: domain,
      ValidationMethod: 'DNS',
      SubjectAlternativeNames: [`www.${domain}`, `api.${domain}`],
    }));
    try {
      const desc = await client.send(new DescribeCertificateCommand({ CertificateArn: resp.CertificateArn }));
      const sans = desc.Certificate?.SubjectAlternativeNames;
      if (sans?.length !== 2) throw new Error(`expected 2 SANs, got ${sans?.length}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: resp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'RequestCertificate_WithOptions', async () => {
    const domain = uniqueDomain('opts-test');
    const resp = await client.send(new RequestCertificateCommand({
      DomainName: domain,
      ValidationMethod: 'DNS',
      Options: { CertificateTransparencyLoggingPreference: 'DISABLED' },
    }));
    try {
      const desc = await client.send(new DescribeCertificateCommand({ CertificateArn: resp.CertificateArn }));
      const opts = desc.Certificate?.Options;
      if (!opts) throw new Error('expected Options to be defined');
      if (opts.CertificateTransparencyLoggingPreference !== 'DISABLED') {
        throw new Error(`expected DISABLED, got ${opts.CertificateTransparencyLoggingPreference}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: resp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'RequestCertificate_WithTags', async () => {
    const domain = uniqueDomain('tag-test');
    const resp = await client.send(new RequestCertificateCommand({
      DomainName: domain,
      ValidationMethod: 'DNS',
      Tags: [
        { Key: 'Env', Value: 'prod' },
        { Key: 'Team', Value: 'platform' },
      ],
    }));
    try {
      const tagResp = await client.send(
        new (await import('@aws-sdk/client-acm')).ListTagsForCertificateCommand({ CertificateArn: resp.CertificateArn }),
      );
      if (tagResp.Tags?.length !== 2) throw new Error(`expected 2 tags, got ${tagResp.Tags?.length}`);
      const tagMap = new Map(tagResp.Tags!.map(t => [t.Key, t.Value]));
      if (tagMap.get('Env') !== 'prod') throw new Error(`expected Env=prod, got ${tagMap.get('Env')}`);
      if (tagMap.get('Team') !== 'platform') throw new Error(`expected Team=platform, got ${tagMap.get('Team')}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: resp.CertificateArn })));
    }
  }));

  return results;
}

export async function runDeleteTests(
  runner: TestRunner,
  client: ACMClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const NONEXISTENT_ARN = 'arn:aws:acm:us-east-1:123456789012:certificate/nonexistent';

  results.push(await runner.runTest(SVC, 'DeleteCertificate_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new DeleteCertificateCommand({ CertificateArn: NONEXISTENT_ARN }));
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

export async function runListTests(
  runner: TestRunner,
  client: ACMClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { ImportCertificateCommand, ListCertificatesCommand } = await import('@aws-sdk/client-acm');
  const { TEST_CERT_PEM, TEST_KEY_PEM, TEST_CHAIN_PEM } = await import('./constants.js');

  results.push(await runner.runTest(SVC, 'ListCertificates', async () => {
    const resp = await client.send(new ListCertificatesCommand({ MaxItems: 10 }));
    if (!resp.CertificateSummaryList) throw new Error('expected CertificateSummaryList to be defined');
  }));

  results.push(await runner.runTest(SVC, 'ListCertificates_Pagination', async () => {
    const arns: string[] = [];
    const base = Date.now();
    const { RequestCertificateCommand: ReqCmd, DeleteCertificateCommand: DelCmd } = await import('@aws-sdk/client-acm');
    for (let i = 0; i < 3; i++) {
      const resp = await client.send(new ReqCmd({
        DomainName: `page-${base}-${i}.com`,
        ValidationMethod: 'DNS',
      }));
      if (resp.CertificateArn) arns.push(resp.CertificateArn);
    }
    try {
      const page1 = await client.send(new ListCertificatesCommand({ MaxItems: 2 }));
      if (page1.CertificateSummaryList?.length !== 2) {
        throw new Error(`expected 2, got ${page1.CertificateSummaryList?.length}`);
      }
      if (!page1.NextToken) throw new Error('expected non-empty NextToken');
      const page2 = await client.send(new ListCertificatesCommand({ NextToken: page1.NextToken, MaxItems: 2 }));
      if (!page2.CertificateSummaryList?.length) {
        throw new Error(`expected at least 1 on page 2, got ${page2.CertificateSummaryList?.length}`);
      }
    } finally {
      for (const arn of arns) {
        await safeCleanup(() => client.send(new DelCmd({ CertificateArn: arn })));
      }
    }
  }));

  results.push(await runner.runTest(SVC, 'ListCertificates_CertificateStatusesFilter', async () => {
    const importResp = await client.send(new ImportCertificateCommand({
      Certificate: Buffer.from(TEST_CERT_PEM),
      PrivateKey: Buffer.from(TEST_KEY_PEM),
    }));
    const importArn = importResp.CertificateArn!;
    try {
      const resp = await client.send(new ListCertificatesCommand({
        CertificateStatuses: ['ISSUED'],
      }));
      const found = resp.CertificateSummaryList?.some(s => s.CertificateArn === importArn);
      if (!found) throw new Error('imported ISSUED cert not found in filtered list');
      const allIssued = resp.CertificateSummaryList?.every(s => s.Status === 'ISSUED');
      if (!allIssued) throw new Error('found non-ISSUED cert in filtered list');
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: importArn })));
    }
  }));

  return results;
}
