import {
  CreateFunctionCommand,
  DeleteFunctionCommand,
  CreateFunctionUrlConfigCommand,
  GetFunctionUrlConfigCommand,
  UpdateFunctionUrlConfigCommand,
  ListFunctionUrlConfigsCommand,
  DeleteFunctionUrlConfigCommand,
} from '@aws-sdk/client-lambda';
import { LambdaTestContext, lambdaTrustPolicy } from './context.js';
import { createIAMRole, deleteIAMRole, safeCleanup } from '../../helpers.js';

export async function runFunctionUrlTests(ctx: LambdaTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, iamClient, ts } = ctx;
  const results: import('../../runner.js').TestResult[] = [];
  const furlFuncName = `FurlFunc-${ts}`;
  const furlRoleName = `FurlRole-${ts}`;
  const furlRole = `arn:aws:iam::000000000000:role/${furlRoleName}`;
  const furlCode = new TextEncoder().encode('exports.handler = async () => { return 1; };');

  await createIAMRole(iamClient, furlRoleName, lambdaTrustPolicy);
  let funcCreated = false;

  try {
    await client.send(new CreateFunctionCommand({
      FunctionName: furlFuncName, Runtime: 'nodejs22.x', Role: furlRole, Handler: 'index.handler', Code: { ZipFile: furlCode },
    }));
    funcCreated = true;

    results.push(await runner.runTest('lambda', 'CreateFunctionUrlConfig', async () => {
      const resp = await client.send(new CreateFunctionUrlConfigCommand({
        FunctionName: furlFuncName,
        AuthType: 'NONE',
      }));
      if (!resp.FunctionUrl || resp.FunctionUrl === '') throw new Error('FunctionUrl to be defined');
      if (resp.AuthType !== 'NONE') throw new Error(`AuthType mismatch, got ${resp.AuthType}`);
    }));

    results.push(await runner.runTest('lambda', 'GetFunctionUrlConfig', async () => {
      const resp = await client.send(new GetFunctionUrlConfigCommand({ FunctionName: furlFuncName }));
      if (!resp.FunctionUrl || resp.FunctionUrl === '') throw new Error('FunctionUrl to be defined');
    }));

    results.push(await runner.runTest('lambda', 'UpdateFunctionUrlConfig', async () => {
      const resp = await client.send(new UpdateFunctionUrlConfigCommand({
        FunctionName: furlFuncName,
        AuthType: 'AWS_IAM',
      }));
      if (resp.AuthType !== 'AWS_IAM') throw new Error(`AuthType not updated, got ${resp.AuthType}`);
    }));

    results.push(await runner.runTest('lambda', 'ListFunctionUrlConfigs', async () => {
      const resp = await client.send(new ListFunctionUrlConfigsCommand({ FunctionName: furlFuncName }));
      if (!resp.FunctionUrlConfigs) throw new Error('url configs list to be defined');
      if (resp.FunctionUrlConfigs.length === 0) throw new Error('expected at least 1 url config');
    }));

    results.push(await runner.runTest('lambda', 'DeleteFunctionUrlConfig', async () => {
      await client.send(new DeleteFunctionUrlConfigCommand({ FunctionName: furlFuncName }));
    }));
  } finally {
    if (funcCreated) {
      await safeCleanup(async () => { await client.send(new DeleteFunctionCommand({ FunctionName: furlFuncName })); });
    }
    await deleteIAMRole(iamClient, furlRoleName);
  }

  return results;
}
