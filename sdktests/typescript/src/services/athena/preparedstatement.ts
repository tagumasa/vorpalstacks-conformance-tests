import {
  CreateWorkGroupCommand,
  DeleteWorkGroupCommand,
  CreatePreparedStatementCommand,
  GetPreparedStatementCommand,
  UpdatePreparedStatementCommand,
  DeletePreparedStatementCommand,
  ListPreparedStatementsCommand,
  BatchGetPreparedStatementCommand,
} from '@aws-sdk/client-athena';
import type { TestResult } from '../../runner.js';
import { makeUniqueName, assertThrows, safeCleanup } from '../../helpers.js';
import type { AthenaTestContext } from './context.js';

export async function runPreparedStatementTests(ctx: AthenaTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;
  const svc = 'athena';

  results.push(await runner.runTest(svc, 'CreatePreparedStatement', async () => {
    await client.send(new CreatePreparedStatementCommand({
      StatementName: ctx.psName,
      WorkGroup: ctx.psWorkGroup,
      QueryStatement: 'SELECT * FROM t WHERE id = ?',
      Description: 'Test prepared statement',
    }));
  }));

  results.push(await runner.runTest(svc, 'GetPreparedStatement', async () => {
    const resp = await client.send(new GetPreparedStatementCommand({
      StatementName: ctx.psName,
      WorkGroup: ctx.psWorkGroup,
    }));
    if (!resp.PreparedStatement) throw new Error('PreparedStatement to be defined');
  }));

  results.push(await runner.runTest(svc, 'ListPreparedStatements', async () => {
    const resp = await client.send(new ListPreparedStatementsCommand({
      WorkGroup: ctx.psWorkGroup,
    }));
    if (!resp.PreparedStatements) throw new Error('PreparedStatements to be defined');
  }));

  results.push(await runner.runTest(svc, 'UpdatePreparedStatement', async () => {
    await client.send(new UpdatePreparedStatementCommand({
      StatementName: ctx.psName,
      WorkGroup: ctx.psWorkGroup,
      QueryStatement: 'SELECT * FROM t WHERE id = ? AND status = ?',
      Description: 'Updated prepared statement',
    }));
  }));

  results.push(await runner.runTest(svc, 'GetPreparedStatement_AfterUpdate', async () => {
    const resp = await client.send(new GetPreparedStatementCommand({
      StatementName: ctx.psName,
      WorkGroup: ctx.psWorkGroup,
    }));
    const ps = resp.PreparedStatement;
    if (ps?.QueryStatement !== 'SELECT * FROM t WHERE id = ? AND status = ?') {
      throw new Error(`QueryStatement mismatch: ${ps?.QueryStatement}`);
    }
    if (ps?.Description !== 'Updated prepared statement') {
      throw new Error(`Description mismatch: ${ps?.Description}`);
    }
  }));

  results.push(await runner.runTest(svc, 'BatchGetPreparedStatement', async () => {
    const resp = await client.send(new BatchGetPreparedStatementCommand({
      PreparedStatementNames: [ctx.psName],
      WorkGroup: ctx.psWorkGroup,
    }));
    if (!resp.PreparedStatements || resp.PreparedStatements.length === 0) {
      throw new Error('PreparedStatements to be non-empty');
    }
    if (resp.PreparedStatements[0].StatementName !== ctx.psName) {
      throw new Error('StatementName mismatch');
    }
  }));

  results.push(await runner.runTest(svc, 'DeletePreparedStatement', async () => {
    await client.send(new DeletePreparedStatementCommand({
      StatementName: ctx.psName,
      WorkGroup: ctx.psWorkGroup,
    }));
  }));

  results.push(await runner.runTest(svc, 'DeletePreparedStatement_NonExistent', async () => {
    try {
      await client.send(new DeletePreparedStatementCommand({
        StatementName: 'nonexistent_ps_xyz',
        WorkGroup: ctx.psWorkGroup,
      }));
      throw new Error('expected an error');
    } catch (err: unknown) {
      const name = err instanceof Error ? err.name : '';
      if (name !== 'ResourceNotFoundException' && name !== 'InvalidRequestException') {
        throw new Error(`Expected ResourceNotFoundException or InvalidRequestException, got ${name}`);
      }
    }
  }));

  results.push(await runner.runTest(svc, 'CreatePreparedStatement_Duplicate', async () => {
    const dupName = makeUniqueName('dupsps');
    try {
      await client.send(new CreatePreparedStatementCommand({
        StatementName: dupName,
        WorkGroup: ctx.psWorkGroup,
        QueryStatement: 'SELECT 1',
      }));
      await assertThrows(
        () => client.send(new CreatePreparedStatementCommand({
          StatementName: dupName,
          WorkGroup: ctx.psWorkGroup,
          QueryStatement: 'SELECT 2',
        })),
        'InvalidRequestException',
      );
    } finally {
      await safeCleanup(() => client.send(new DeletePreparedStatementCommand({ StatementName: dupName, WorkGroup: ctx.psWorkGroup })));
    }
  }));

  return results;
}

export async function runPreparedStatementFinallyTests(ctx: AthenaTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;
  const svc = 'athena';

  results.push(await runner.runTest(svc, 'GetPreparedStatement_NonExistent', async () => {
    const psWgName = makeUniqueName('athena-psne');
    try {
      await client.send(new CreateWorkGroupCommand({ Name: psWgName }));
      await assertThrows(async () => {
        await client.send(new GetPreparedStatementCommand({
          StatementName: 'nonexistent-ps', WorkGroup: psWgName,
        }));
      }, 'InvalidRequestException');
    } finally {
      await safeCleanup(() => client.send(new DeleteWorkGroupCommand({ WorkGroup: psWgName })));
    }
  }));

  const psWgName = makeUniqueName('athena-pswg');
  results.push(await runner.runTest(svc, 'PreparedStatement_CreateWG', async () => {
    await client.send(new CreateWorkGroupCommand({ Name: psWgName }));
  }));

  const ps2Name = makeUniqueName('athena-ps2');
  results.push(await runner.runTest(svc, 'CreatePreparedStatement_Second', async () => {
    await client.send(new CreatePreparedStatementCommand({
      StatementName: ps2Name, WorkGroup: psWgName, QueryStatement: 'SELECT 1',
    }));
  }));

  results.push(await runner.runTest(svc, 'DeleteWorkGroup_PSCleanup', async () => {
    await client.send(new DeleteWorkGroupCommand({ WorkGroup: psWgName }));
  }));

  return results;
}
