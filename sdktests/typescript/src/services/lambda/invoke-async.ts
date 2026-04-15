import {
  CreateFunctionCommand,
  DeleteFunctionCommand,
  InvokeAsyncCommand,
  InvokeWithResponseStreamCommand,
} from '@aws-sdk/client-lambda';
import { LambdaTestContext, lambdaTrustPolicy } from './context.js';
import { createIAMRole, deleteIAMRole, safeCleanup } from '../../helpers.js';

export async function runInvokeAsyncTests(ctx: LambdaTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, iamClient, ts } = ctx;
  const results: import('../../runner.js').TestResult[] = [];
  const iaFuncName = `IaFunc-${ts}`;
  const iaRoleName = `IaRole-${ts}`;
  const iaRole = `arn:aws:iam::000000000000:role/${iaRoleName}`;
  const iaCode = new TextEncoder().encode('exports.handler = async () => { return { statusCode: 200 }; };');

  await createIAMRole(iamClient, iaRoleName, lambdaTrustPolicy);
  let funcCreated = false;

  try {
    await client.send(new CreateFunctionCommand({
      FunctionName: iaFuncName, Runtime: 'nodejs22.x', Role: iaRole, Handler: 'index.handler', Code: { ZipFile: iaCode },
    }));
    funcCreated = true;

    results.push(await runner.runTest('lambda', 'InvokeAsync', async () => {
      const resp = await client.send(new InvokeAsyncCommand({
        FunctionName: iaFuncName,
        InvokeArgs: new TextEncoder().encode('{"test": true}'),
      }));
      if (resp.Status !== 202) throw new Error(`expected status 202, got ${resp.Status}`);
    }));

    results.push(await runner.runTest('lambda', 'InvokeWithResponseStream', async () => {
      try {
        const resp = await client.send(new InvokeWithResponseStreamCommand({ FunctionName: iaFuncName }));
        if (resp.StatusCode !== 200) throw new Error(`expected status 200, got ${resp.StatusCode}`);
        if (!resp.ResponseStreamContentType) throw new Error('ResponseStreamContentType to be defined');
      } catch (err: unknown) {
        const httpStatus = (err as { $response?: { statusCode?: number } }).$response?.statusCode;
        if (httpStatus === 200) {
          const msg = err instanceof Error ? err.message : String(err);
          if (msg.includes('Truncated event message')) {
            throw new Error(
              'SERVER BUG: InvokeWithResponseStream returns content-type application/json instead of application/vnd.amazon.eventstream. ' +
              'The handler returns map[string]interface{} instead of implementing response.StreamableResponse. ' +
              `Raw error: ${msg}`,
            );
          }
        }
        throw err;
      }
    }));
  } finally {
    if (funcCreated) {
      await safeCleanup(async () => { await client.send(new DeleteFunctionCommand({ FunctionName: iaFuncName })); });
    }
    await deleteIAMRole(iamClient, iaRoleName);
  }

  return results;
}
