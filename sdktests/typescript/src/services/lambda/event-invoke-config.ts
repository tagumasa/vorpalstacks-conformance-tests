import {
  CreateFunctionCommand,
  DeleteFunctionCommand,
  PutFunctionEventInvokeConfigCommand,
  GetFunctionEventInvokeConfigCommand,
  ListFunctionEventInvokeConfigsCommand,
  DeleteFunctionEventInvokeConfigCommand,
} from '@aws-sdk/client-lambda';
import { LambdaTestContext, lambdaTrustPolicy } from './context.js';
import { createIAMRole, deleteIAMRole, safeCleanup } from '../../helpers.js';

export async function runEventInvokeConfigTests(ctx: LambdaTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, iamClient, ts } = ctx;
  const results: import('../../runner.js').TestResult[] = [];
  const eicFuncName = `EicFunc-${ts}`;
  const eicRoleName = `EicRole-${ts}`;
  const eicRole = `arn:aws:iam::000000000000:role/${eicRoleName}`;
  const eicCode = new TextEncoder().encode('exports.handler = async () => { return 1; };');

  await createIAMRole(iamClient, eicRoleName, lambdaTrustPolicy);
  let funcCreated = false;

  try {
    await client.send(new CreateFunctionCommand({
      FunctionName: eicFuncName, Runtime: 'nodejs22.x', Role: eicRole, Handler: 'index.handler', Code: { ZipFile: eicCode },
    }));
    funcCreated = true;

    results.push(await runner.runTest('lambda', 'PutFunctionEventInvokeConfig', async () => {
      const resp = await client.send(new PutFunctionEventInvokeConfigCommand({
        FunctionName: eicFuncName,
        MaximumEventAgeInSeconds: 3600,
        MaximumRetryAttempts: 2,
      }));
      if (!resp.LastModified) throw new Error('LastModified to be defined');
    }));

    results.push(await runner.runTest('lambda', 'GetFunctionEventInvokeConfig', async () => {
      const resp = await client.send(new GetFunctionEventInvokeConfigCommand({ FunctionName: eicFuncName }));
      if (resp.MaximumEventAgeInSeconds !== 3600) throw new Error(`MaximumEventAgeInSeconds mismatch, got ${resp.MaximumEventAgeInSeconds}`);
      if (resp.MaximumRetryAttempts !== 2) throw new Error(`MaximumRetryAttempts mismatch, got ${resp.MaximumRetryAttempts}`);
    }));

    results.push(await runner.runTest('lambda', 'ListFunctionEventInvokeConfigs', async () => {
      const resp = await client.send(new ListFunctionEventInvokeConfigsCommand({ FunctionName: eicFuncName }));
      if (!resp.FunctionEventInvokeConfigs) throw new Error('configs list to be defined');
      if (resp.FunctionEventInvokeConfigs.length === 0) throw new Error('expected at least 1 config');
    }));

    results.push(await runner.runTest('lambda', 'DeleteFunctionEventInvokeConfig', async () => {
      await client.send(new DeleteFunctionEventInvokeConfigCommand({ FunctionName: eicFuncName }));
    }));
  } finally {
    if (funcCreated) {
      await safeCleanup(async () => { await client.send(new DeleteFunctionCommand({ FunctionName: eicFuncName })); });
    }
    await deleteIAMRole(iamClient, eicRoleName);
  }

  return results;
}
