import {
  CreateFunctionCommand,
  DeleteFunctionCommand,
  ListFunctionsCommand,
} from '@aws-sdk/client-lambda';
import { LambdaTestContext } from './context.js';
import { safeCleanup } from '../../helpers.js';

export async function runPaginationTests(ctx: LambdaTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, ts, roleArn, functionCode } = ctx;
  const results: import('../../runner.js').TestResult[] = [];
  const pgTs = ts;

  results.push(await runner.runTest('lambda', 'ListFunctions_Pagination', async () => {
    const pgFuncs: string[] = [];

    for (const i of [0, 1, 2, 3, 4]) {
      const name = `PagFunc-${pgTs}-${i}`;
      try {
        await client.send(new CreateFunctionCommand({
          FunctionName: name, Runtime: 'nodejs22.x', Role: roleArn, Handler: 'index.handler', Code: { ZipFile: functionCode },
        }));
        pgFuncs.push(name);
      } catch (err) {
        for (const n of pgFuncs) {
          await safeCleanup(async () => { await client.send(new DeleteFunctionCommand({ FunctionName: n })); });
        }
        throw err;
      }
    }

    const allFuncs: string[] = [];
    let marker: string | undefined;
    try {
      do {
        const resp = await client.send(new ListFunctionsCommand({ Marker: marker, MaxItems: 2 }));
        if (!resp.Functions) throw new Error('expected Functions to be defined');
        for (const f of resp.Functions) {
          if (f.FunctionName && f.FunctionName.startsWith(`PagFunc-${pgTs}`)) {
            allFuncs.push(f.FunctionName);
          }
        }
        marker = resp.NextMarker || undefined;
      } while (marker);
    } finally {
      for (const n of pgFuncs) {
        await safeCleanup(async () => { await client.send(new DeleteFunctionCommand({ FunctionName: n })); });
      }
    }

    if (allFuncs.length !== 5) {
      throw new Error(`expected 5 paginated functions, got ${allFuncs.length}`);
    }
  }));

  return results;
}
