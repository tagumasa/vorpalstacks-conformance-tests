import {
  SchedulerClient,
  CreateScheduleGroupCommand,
  GetScheduleGroupCommand,
  DeleteScheduleGroupCommand,
  ListScheduleGroupsCommand,
  CreateScheduleCommand,
  GetScheduleCommand,
  DeleteScheduleCommand,
  TagResourceCommand,
  ListTagsForResourceCommand,
} from '@aws-sdk/client-scheduler';
import type { TestRunner, TestResult, ServiceContext } from '../../runner.js';
import { safeCleanup, createIAMRole, deleteIAMRole } from '../../helpers.js';
import { IAMClient } from '@aws-sdk/client-iam';

const SVC = 'scheduler';
const TRUST_POLICY = JSON.stringify({
  Version: '2012-10-17',
  Statement: [{ Effect: 'Allow', Principal: { Service: 'scheduler.amazonaws.com' }, Action: 'sts:AssumeRole' }],
});

function uniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 99999)}`;
}

export async function runScheduleGroupTests(
  runner: TestRunner,
  ctx: ServiceContext,
): Promise<TestResult[]> {
  const client = new SchedulerClient({
    endpoint: ctx.endpoint,
    region: ctx.region,
    credentials: ctx.credentials,
  });
  const iamClient = new IAMClient({
    endpoint: ctx.endpoint,
    region: ctx.region,
    credentials: ctx.credentials,
  });
  const results: TestResult[] = [];
  const lambdaARN = `arn:aws:lambda:${ctx.region}:000000000000:function:TestFunction`;
  const makeFlexibleWindow = () => ({ Mode: 'OFF' as const });

  const groupName = uniqueName('TestGroup');

  results.push(await runner.runTest(SVC, 'CreateScheduleGroup', async () => {
    const resp = await client.send(new CreateScheduleGroupCommand({ Name: groupName }));
    if (!resp.ScheduleGroupArn) throw new Error('expected ScheduleGroupArn to be defined');
  }));

  results.push(await runner.runTest(SVC, 'GetScheduleGroup', async () => {
    const resp = await client.send(new GetScheduleGroupCommand({ Name: groupName }));
    if (resp.Name !== groupName) throw new Error(`expected ${groupName}, got ${resp.Name}`);
  }));

  results.push(await runner.runTest(SVC, 'ListScheduleGroups', async () => {
    const resp = await client.send(new ListScheduleGroupsCommand({}));
    if (!resp.ScheduleGroups) throw new Error('expected ScheduleGroups to be defined');
  }));

  results.push(await runner.runTest(SVC, 'GetScheduleGroup_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new GetScheduleGroupCommand({ Name: 'nonexistent-group-xyz' }));
    } catch (e) { err = e; }
    const name = (err as { name?: string })?.name ?? '';
    const msg = err instanceof Error ? err.message : '';
    if (!name.includes('ResourceNotFoundException') && !msg.includes('ResourceNotFoundException')) {
      throw new Error(`expected ResourceNotFoundException, got: ${msg || name || 'no error'}`);
    }
  }));

  results.push(await runner.runTest(SVC, 'CreateScheduleGroup_DuplicateName', async () => {
    const dupGroup = uniqueName('DupGroup');
    await client.send(new CreateScheduleGroupCommand({ Name: dupGroup }));
    try {
      let err: unknown;
      try {
        await client.send(new CreateScheduleGroupCommand({ Name: dupGroup }));
      } catch (e) { err = e; }
      if (!err) throw new Error('expected error for duplicate group name');
    } finally {
      await safeCleanup(() => client.send(new DeleteScheduleGroupCommand({ Name: dupGroup })));
    }
  }));

  results.push(await runner.runTest(SVC, 'ListScheduleGroups_ContainsCreated', async () => {
    const resp = await client.send(new ListScheduleGroupsCommand({}));
    const found = resp.ScheduleGroups?.some(g => g.Name === groupName);
    if (!found) throw new Error(`created group "${groupName}" not found in list`);
  }));

  const groupRole = uniqueName('GroupSchedRole');
  await createIAMRole(iamClient, groupRole, TRUST_POLICY);
  try {
    const groupRoleARN = `arn:aws:iam::000000000000:role/${groupRole}`;
    const schedName = uniqueName('GroupSched');

    results.push(await runner.runTest(SVC, 'CreateSchedule_WithGroupName', async () => {
      await client.send(new CreateScheduleCommand({
        Name: schedName,
        GroupName: groupName,
        ScheduleExpression: 'rate(30 minutes)',
        Target: { Arn: lambdaARN, RoleArn: groupRoleARN },
        FlexibleTimeWindow: makeFlexibleWindow(),
      }));
      const getResp = await client.send(new GetScheduleCommand({
        Name: schedName,
        GroupName: groupName,
      }));
      if (getResp.GroupName !== groupName) {
        throw new Error(`expected group ${groupName}, got ${getResp.GroupName}`);
      }
    }));
    await safeCleanup(() => client.send(new DeleteScheduleCommand({ Name: schedName, GroupName: groupName })));
  } finally {
    await deleteIAMRole(iamClient, groupRole);
  }

  results.push(await runner.runTest(SVC, 'TagResource_ScheduleGroup', async () => {
    const tagGroupName = uniqueName('TagGroup');
    const groupResp = await client.send(new CreateScheduleGroupCommand({ Name: tagGroupName }));
    try {
      await client.send(new TagResourceCommand({
        ResourceArn: groupResp.ScheduleGroupArn,
        Tags: [{ Key: 'Env', Value: 'prod' }],
      }));
      const tagResp = await client.send(new ListTagsForResourceCommand({
        ResourceArn: groupResp.ScheduleGroupArn,
      }));
      const found = tagResp.Tags?.some(t => t.Key === 'Env');
      if (!found) throw new Error('tag Env not found on schedule group');
    } finally {
      await safeCleanup(() => client.send(new DeleteScheduleGroupCommand({ Name: tagGroupName })));
    }
  }));

  results.push(await runner.runTest(SVC, 'ListTagsForResource_ScheduleGroup', async () => {
    const groupResp = await client.send(new GetScheduleGroupCommand({ Name: groupName }));
    await client.send(new ListTagsForResourceCommand({ ResourceArn: groupResp.Arn }));
  }));

  results.push(await runner.runTest(SVC, 'DeleteScheduleGroup_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new DeleteScheduleGroupCommand({ Name: 'nonexistent-group-xyz' }));
    } catch (e) { err = e; }
    const name = (err as { name?: string })?.name ?? '';
    const msg = err instanceof Error ? err.message : '';
    if (!name.includes('ResourceNotFoundException') && !msg.includes('ResourceNotFoundException')) {
      throw new Error(`expected ResourceNotFoundException, got: ${msg || name || 'no error'}`);
    }
  }));

  results.push(await runner.runTest(SVC, 'DeleteScheduleGroup', async () => {
    const delGroup = uniqueName('DelGroup');
    await client.send(new CreateScheduleGroupCommand({ Name: delGroup }));
    await client.send(new DeleteScheduleGroupCommand({ Name: delGroup }));
    let err: unknown;
    try {
      await client.send(new GetScheduleGroupCommand({ Name: delGroup }));
    } catch (e) { err = e; }
    if (!err) throw new Error('expected error after deleting group');
  }));

  await safeCleanup(() => client.send(new DeleteScheduleGroupCommand({ Name: groupName })));

  results.push(await runner.runTest(SVC, 'ListScheduleGroups_Pagination', async () => {
    const pgTs = Date.now();
    const pgGroups: string[] = [];
    for (const i of [0, 1, 2, 3, 4]) {
      const name = `PagGroup-${pgTs}-${i}`;
      await client.send(new CreateScheduleGroupCommand({ Name: name }));
      pgGroups.push(name);
    }
    try {
      const allGroups: string[] = [];
      let nextToken: string | undefined;
      do {
        const resp = await client.send(new ListScheduleGroupsCommand({
          MaxResults: 2,
          NextToken: nextToken,
        }));
        for (const g of resp.ScheduleGroups ?? []) {
          if (g.Name?.includes(`PagGroup-${pgTs}`)) {
            allGroups.push(g.Name);
          }
        }
        nextToken = resp.NextToken;
      } while (nextToken);

      if (allGroups.length !== 5) throw new Error(`expected 5 paginated groups, got ${allGroups.length}`);
    } finally {
      for (const gn of pgGroups) {
        await safeCleanup(() => client.send(new DeleteScheduleGroupCommand({ Name: gn })));
      }
    }
  }));

  return results;
}
