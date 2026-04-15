import {
  CreateWorkGroupCommand,
  GetWorkGroupCommand,
  DeleteWorkGroupCommand,
  TagResourceCommand,
  UntagResourceCommand,
  ListTagsForResourceCommand,
} from '@aws-sdk/client-athena';
import type { TestResult } from '../../runner.js';
import { makeUniqueName } from '../../helpers.js';
import type { AthenaTestContext } from './context.js';

export async function runTaggingSetupTests(ctx: AthenaTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;
  const svc = 'athena';

  results.push(await runner.runTest(svc, 'TagResource_CreateWG', async () => {
    ctx.psWorkGroup = makeUniqueName('tagwg');
    await client.send(new CreateWorkGroupCommand({
      Name: ctx.psWorkGroup,
      Configuration: {
        ResultConfiguration: { OutputLocation: 's3://test-bucket/athena/' },
      },
      Tags: [{ Key: 'env', Value: 'test' }, { Key: 'team', Value: 'conformance' }],
    }));
  }));

  results.push(await runner.runTest(svc, 'ListTagsForResource', async () => {
    const wgResp = await client.send(new GetWorkGroupCommand({ WorkGroup: ctx.psWorkGroup }));
    const arn = wgResp.WorkGroup?.Configuration?.ResultConfiguration?.OutputLocation
      ? `arn:aws:athena:${ctx.svcCtx.region}:000000000000:workgroup/${ctx.psWorkGroup}`
      : '';
    if (!arn) throw new Error('could not construct ARN');
    ctx.wgArn = arn;
    const resp = await client.send(new ListTagsForResourceCommand({ ResourceARN: arn }));
    if (!resp.Tags || resp.Tags.length === 0) throw new Error('Tags to be non-empty');
    const keys = resp.Tags.map((t) => t.Key);
    if (!keys.includes('env') || !keys.includes('team')) throw new Error('expected tag keys not found');
  }));

  return results;
}

export async function runTaggingFinallyTests(ctx: AthenaTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;
  const svc = 'athena';

  const tagWgName = makeUniqueName('twg');

  let tagWgArn = '';

  results.push(await runner.runTest(svc, 'TagResource_Finally_CreateWG', async () => {
    await client.send(new CreateWorkGroupCommand({
      Name: tagWgName,
    }));
    tagWgArn = `arn:aws:athena:${ctx.svcCtx.region}:000000000000:workgroup/${tagWgName}`;
  }));

  results.push(await runner.runTest(svc, 'TagResource', async () => {
    if (!tagWgArn) throw new Error('no workgroup ARN');
    await client.send(new TagResourceCommand({
      ResourceARN: tagWgArn,
      Tags: [{ Key: 'env', Value: 'test' }, { Key: 'team', Value: 'athena' }],
    }));
  }));

  results.push(await runner.runTest(svc, 'UntagResource', async () => {
    if (!tagWgArn) throw new Error('no workgroup ARN');
    await client.send(new UntagResourceCommand({ ResourceARN: tagWgArn, TagKeys: ['env'] }));
  }));

  results.push(await runner.runTest(svc, 'ListTagsForResource_AfterUntag', async () => {
    if (!tagWgArn) throw new Error('no workgroup ARN');
    const resp = await client.send(new ListTagsForResourceCommand({ ResourceARN: tagWgArn }));
    for (const t of resp.Tags || []) {
      if (t.Key === 'env') throw new Error('env tag should have been removed');
    }
  }));

  results.push(await runner.runTest(svc, 'DeleteWorkGroup_TagCleanup', async () => {
    await client.send(new DeleteWorkGroupCommand({ WorkGroup: tagWgName }));
  }));

  return results;
}
