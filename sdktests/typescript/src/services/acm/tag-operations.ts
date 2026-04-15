import type { ACMClient } from '@aws-sdk/client-acm';
import type { TestRunner, TestResult } from '../../runner.js';
import { safeCleanup } from '../../helpers.js';
import {
  RequestCertificateCommand,
  DeleteCertificateCommand,
  AddTagsToCertificateCommand,
  RemoveTagsFromCertificateCommand,
  ListTagsForCertificateCommand,
} from '@aws-sdk/client-acm';

const SVC = 'acm';
const NONEXISTENT_ARN = 'arn:aws:acm:us-east-1:123456789012:certificate/nonexistent';

function uniqueDomain(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 99999)}.com`;
}

export async function runTagTests(
  runner: TestRunner,
  client: ACMClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest(SVC, 'AddTagsToCertificate_UpdateExistingTag', async () => {
    const domain = uniqueDomain('tagupd');
    const resp = await client.send(new RequestCertificateCommand({
      DomainName: domain,
      ValidationMethod: 'DNS',
    }));
    try {
      await client.send(new AddTagsToCertificateCommand({
        CertificateArn: resp.CertificateArn,
        Tags: [{ Key: 'Env', Value: 'dev' }],
      }));
      await client.send(new AddTagsToCertificateCommand({
        CertificateArn: resp.CertificateArn,
        Tags: [{ Key: 'Env', Value: 'prod' }],
      }));
      const tagResp = await client.send(new ListTagsForCertificateCommand({ CertificateArn: resp.CertificateArn }));
      const envTag = tagResp.Tags?.find(t => t.Key === 'Env');
      if (envTag?.Value !== 'prod') throw new Error(`expected Env=prod, got ${envTag?.Value}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: resp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'AddTagsToCertificate_VerifyContent', async () => {
    const domain = uniqueDomain('tagver');
    const resp = await client.send(new RequestCertificateCommand({
      DomainName: domain,
      ValidationMethod: 'DNS',
    }));
    try {
      await client.send(new AddTagsToCertificateCommand({
        CertificateArn: resp.CertificateArn,
        Tags: [{ Key: 'Key1', Value: 'Val1' }, { Key: 'Key2', Value: 'Val2' }],
      }));
      const tagResp = await client.send(new ListTagsForCertificateCommand({ CertificateArn: resp.CertificateArn }));
      if (tagResp.Tags?.length !== 2) throw new Error(`expected 2 tags, got ${tagResp.Tags?.length}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: resp.CertificateArn })));
    }
  }));

  results.push(await runner.runTest(SVC, 'RemoveTagsFromCertificate_VerifyEmpty', async () => {
    const domain = uniqueDomain('tagrm');
    const resp = await client.send(new RequestCertificateCommand({
      DomainName: domain,
      ValidationMethod: 'DNS',
    }));
    try {
      await client.send(new AddTagsToCertificateCommand({
        CertificateArn: resp.CertificateArn,
        Tags: [{ Key: 'X', Value: 'Y' }],
      }));
      await client.send(new RemoveTagsFromCertificateCommand({
        CertificateArn: resp.CertificateArn,
        Tags: [{ Key: 'X' }],
      }));
      const tagResp = await client.send(new ListTagsForCertificateCommand({ CertificateArn: resp.CertificateArn }));
      if (tagResp.Tags?.length) throw new Error(`expected 0 tags after removal, got ${tagResp.Tags.length}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteCertificateCommand({ CertificateArn: resp.CertificateArn })));
    }
  }));

  for (const op of ['AddTagsToCertificate', 'RemoveTagsFromCertificate', 'ListTagsForCertificate'] as const) {
    const CommandClass = op === 'AddTagsToCertificate' ? AddTagsToCertificateCommand
      : op === 'RemoveTagsFromCertificate' ? RemoveTagsFromCertificateCommand
      : ListTagsForCertificateCommand;
    const input = op === 'ListTagsForCertificate'
      ? { CertificateArn: NONEXISTENT_ARN }
      : { CertificateArn: NONEXISTENT_ARN, Tags: [{ Key: 'X' }] };
    results.push(await runner.runTest(SVC, `${op}_NonExistent`, async () => {
      let err: unknown;
      try {
        await client.send(new (CommandClass as any)(input));
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
  }

  return results;
}
