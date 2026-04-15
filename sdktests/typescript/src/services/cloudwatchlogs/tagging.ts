import {
  CloudWatchLogsClient,
  CreateLogGroupCommand,
  DeleteLogGroupCommand,
  DescribeLogGroupsCommand,
  TagResourceCommand,
  ListTagsForResourceCommand,
  UntagResourceCommand,
  TagLogGroupCommand,
  ListTagsLogGroupCommand,
  CreateLogGroupCommand as CreateLogGroupWithTagsCommand,
} from '@aws-sdk/client-cloudwatch-logs';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runTaggingTests(
  runner: TestRunner,
  client: CloudWatchLogsClient,
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('logs', 'TagResource_Basic', async () => {
    const tgName = makeUniqueName('TagGroup');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: tgName }));
      const descResp = await client.send(new DescribeLogGroupsCommand({ logGroupNamePrefix: tgName }));
      if (!descResp.logGroups?.length) throw new Error('log group arn not found');
      const groupArn = (descResp.logGroups[0] as { logGroupArn?: string }).logGroupArn;
      if (!groupArn) throw new Error('log group arn not found');
      await client.send(new TagResourceCommand({
        resourceArn: groupArn,
        tags: { Environment: 'test', Team: 'vorpalstacks' },
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: tgName })));
    }
  }));

  results.push(await runner.runTest('logs', 'ListTagsForResource_Basic', async () => {
    const ltName = makeUniqueName('ListTagGroup');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: ltName }));
      const descResp = await client.send(new DescribeLogGroupsCommand({ logGroupNamePrefix: ltName }));
      if (!descResp.logGroups?.length) throw new Error('log group arn not found');
      const groupArn = (descResp.logGroups[0] as { logGroupArn?: string }).logGroupArn;
      if (!groupArn) throw new Error('log group arn not found');
      await client.send(new TagResourceCommand({
        resourceArn: groupArn,
        tags: { Key1: 'Value1', Key2: 'Value2' },
      }));
      const tagResp = await client.send(new ListTagsForResourceCommand({ resourceArn: groupArn }));
      if (!tagResp.tags) throw new Error('expected tags to be defined');
      if (tagResp.tags.Key1 !== 'Value1') throw new Error(`Key1 mismatch: got ${tagResp.tags.Key1}`);
      if (tagResp.tags.Key2 !== 'Value2') throw new Error(`Key2 mismatch: got ${tagResp.tags.Key2}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: ltName })));
    }
  }));

  results.push(await runner.runTest('logs', 'UntagResource_Basic', async () => {
    const utName = makeUniqueName('UntagGroup');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: utName }));
      const descResp = await client.send(new DescribeLogGroupsCommand({ logGroupNamePrefix: utName }));
      if (!descResp.logGroups?.length) throw new Error('log group arn not found');
      const groupArn = (descResp.logGroups[0] as { logGroupArn?: string }).logGroupArn;
      if (!groupArn) throw new Error('log group arn not found');
      await client.send(new TagResourceCommand({
        resourceArn: groupArn,
        tags: { RemoveMe: 'yes', KeepMe: 'no', KeepMeToo: 'also-no' },
      }));
      await client.send(new UntagResourceCommand({ resourceArn: groupArn, tagKeys: ['RemoveMe'] }));
      const tagResp = await client.send(new ListTagsForResourceCommand({ resourceArn: groupArn }));
      if (tagResp.tags?.RemoveMe !== undefined) throw new Error('RemoveMe tag should have been removed');
      if (tagResp.tags?.KeepMe !== 'no') throw new Error('KeepMe tag should still exist');
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: utName })));
    }
  }));

  results.push(await runner.runTest('logs', 'TagLogGroup_Basic', async () => {
    const tlgName = makeUniqueName('TagLGGroup');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: tlgName }));
      await client.send(new TagLogGroupCommand({
        logGroupName: tlgName,
        tags: { DeprecatedTag: 'yes' },
      }));
      const tagResp = await client.send(new ListTagsLogGroupCommand({ logGroupName: tlgName }));
      if (!tagResp.tags) throw new Error('expected tags to be defined');
      if (tagResp.tags.DeprecatedTag !== 'yes') throw new Error(`DeprecatedTag mismatch: got ${tagResp.tags.DeprecatedTag}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: tlgName })));
    }
  }));

  results.push(await runner.runTest('logs', 'CreateLogGroup_WithTags', async () => {
    const cwtName = makeUniqueName('CWTagGroup');
    try {
      await client.send(new CreateLogGroupWithTagsCommand({
        logGroupName: cwtName,
        tags: { CreatedBy: 'sdk-test', CreatedAt: '2026-04-02', Automated: 'true' },
      }));
      const tagResp = await client.send(new ListTagsLogGroupCommand({ logGroupName: cwtName }));
      const tagKeys = Object.keys(tagResp.tags ?? {});
      if (tagKeys.length !== 3) throw new Error(`expected 3 tags, got ${tagKeys.length}`);
      if (tagResp.tags?.CreatedBy !== 'sdk-test') throw new Error('CreatedBy mismatch');
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: cwtName })));
    }
  }));
}
