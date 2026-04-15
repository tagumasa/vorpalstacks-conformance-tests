import {
  ListTagsForResourceCommand,
  TagResourceCommand,
  UntagResourceCommand,
} from '@aws-sdk/client-cognito-identity';
import type { TestRunner, TestResult } from '../../runner.js';
import type { CognitoIdentityTestContext } from './context.js';

export async function runTagsTests(ctx: CognitoIdentityTestContext, runner: TestRunner): Promise<TestResult[]> {
  const { client, svc } = ctx;
  const results: TestResult[] = [];
  const poolArn = `arn:aws:cognito-identity:${ctx.client.config.region}:000000000000:identitypool/${ctx.poolId}`;

  results.push(await runner.runTest(svc, 'TagResource', async () => {
    if (!ctx.poolId) throw new Error('no pool ID');
    await client.send(new TagResourceCommand({
      ResourceArn: poolArn,
      Tags: { Environment: 'test', Team: 'platform' },
    }));
  }));

  results.push(await runner.runTest(svc, 'UntagResource', async () => {
    await client.send(new UntagResourceCommand({
      ResourceArn: poolArn,
      TagKeys: ['Team'],
    }));
  }));

  results.push(await runner.runTest(svc, 'ListTagsForResource', async () => {
    const resp = await client.send(new ListTagsForResourceCommand({ ResourceArn: poolArn }));
    if (!resp.Tags) throw new Error('Tags to be defined');
    if (resp.Tags['Environment'] !== 'test') throw new Error('Environment tag not found');
    if (resp.Tags['Team']) throw new Error('Team tag should be removed');
  }));

  return results;
}
