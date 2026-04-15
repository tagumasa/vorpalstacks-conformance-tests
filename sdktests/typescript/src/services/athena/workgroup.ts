import {
  ListWorkGroupsCommand,
  CreateWorkGroupCommand,
  GetWorkGroupCommand,
  UpdateWorkGroupCommand,
  DeleteWorkGroupCommand,
} from '@aws-sdk/client-athena';
import type { TestResult } from '../../runner.js';
import { makeUniqueName, assertThrows, safeCleanup } from '../../helpers.js';
import type { AthenaTestContext } from './context.js';

export async function runWorkGroupTests(ctx: AthenaTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;
  const svc = 'athena';

  results.push(await runner.runTest(svc, 'ListWorkGroups', async () => {
    const resp = await client.send(new ListWorkGroupsCommand({ MaxResults: 10 }));
    if (!resp.WorkGroups) throw new Error('WorkGroups to be defined');
  }));

  results.push(await runner.runTest(svc, 'ListWorkGroups_Pagination', async () => {
    let count = 0;
    let nextToken: string | undefined;
    do {
      const resp = await client.send(new ListWorkGroupsCommand({
        MaxResults: 1,
        NextToken: nextToken,
      }));
      count += resp.WorkGroups?.length ?? 0;
      nextToken = resp.NextToken;
    } while (nextToken);
    if (count < 1) throw new Error('expected at least 1 work group');
  }));

  results.push(await runner.runTest(svc, 'CreateWorkGroup', async () => {
    await client.send(new CreateWorkGroupCommand({
      Name: ctx.wgName,
      Configuration: {
        ResultConfiguration: {
          OutputLocation: 's3://test-bucket/athena/',
        },
      },
    }));
  }));

  results.push(await runner.runTest(svc, 'GetWorkGroup', async () => {
    const resp = await client.send(new GetWorkGroupCommand({ WorkGroup: ctx.wgName }));
    if (!resp.WorkGroup) throw new Error('WorkGroup to be defined');
    if (resp.WorkGroup.Name !== ctx.wgName) throw new Error('name mismatch');
  }));

  results.push(await runner.runTest(svc, 'UpdateWorkGroup', async () => {
    await client.send(new UpdateWorkGroupCommand({
      WorkGroup: ctx.wgName,
      Description: 'Updated work group',
    }));
  }));

  results.push(await runner.runTest(svc, 'DeleteWorkGroup', async () => {
    await client.send(new DeleteWorkGroupCommand({
      WorkGroup: ctx.wgName,
      RecursiveDeleteOption: true,
    }));
  }));

  results.push(await runner.runTest(svc, 'GetWorkGroup_NonExistent', async () => {
    await assertThrows(() => client.send(new GetWorkGroupCommand({ WorkGroup: 'nonexistent_wg_xyz' })), 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest(svc, 'DeleteWorkGroup_NonExistent', async () => {
    await assertThrows(() => client.send(new DeleteWorkGroupCommand({ WorkGroup: 'nonexistent_wg_xyz' })), 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest(svc, 'CreateWorkGroup_Duplicate', async () => {
    const dupName = makeUniqueName('dupwg');
    try {
      await client.send(new CreateWorkGroupCommand({ Name: dupName }));
      try {
        await client.send(new CreateWorkGroupCommand({ Name: dupName }));
        throw new Error('expected an error');
      } catch (err: unknown) {
        const name = err instanceof Error ? err.name : '';
        if (name !== 'InvalidRequestException' && name !== 'ResourceAlreadyExistsException') {
          throw new Error(`Expected InvalidRequestException or ResourceAlreadyExistsException, got ${name}`);
        }
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteWorkGroupCommand({ WorkGroup: dupName, RecursiveDeleteOption: true })));
    }
  }));

  return results;
}
