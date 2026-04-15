import {
  GetFunctionCommand,
  InvokeCommand,
  UpdateFunctionCodeCommand,
  DeleteFunctionCommand,
  CreateFunctionCommand,
  GetAliasCommand,
  GetLayerVersionCommand,
  GetFunctionUrlConfigCommand,
  PutFunctionEventInvokeConfigCommand,
} from '@aws-sdk/client-lambda';
import { LambdaTestContext, lambdaTrustPolicy } from './context.js';
import { assertErrorContains, createIAMRole, deleteIAMRole, safeCleanup } from '../../helpers.js';

export async function runErrorTests(ctx: LambdaTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, iamClient, ts, functionName } = ctx;
  const results: import('../../runner.js').TestResult[] = [];

  results.push(await runner.runTest('lambda', 'GetFunction_NonExistent', async () => {
    try {
      await client.send(new GetFunctionCommand({ FunctionName: 'NoSuchFunction_xyz_12345' }));
      throw new Error('expected error for non-existent function');
    } catch (err) {
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  }));

  results.push(await runner.runTest('lambda', 'Invoke_NonExistent', async () => {
    try {
      await client.send(new InvokeCommand({ FunctionName: 'NoSuchFunction_xyz_12345' }));
      throw new Error('expected error for non-existent function');
    } catch (err) {
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  }));

  results.push(await runner.runTest('lambda', 'UpdateFunctionCode_NonExistent', async () => {
    try {
      await client.send(new UpdateFunctionCodeCommand({
        FunctionName: 'NoSuchFunction_xyz_12345',
        ZipFile: new TextEncoder().encode('code'),
      }));
      throw new Error('expected error for non-existent function');
    } catch (err) {
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  }));

  results.push(await runner.runTest('lambda', 'DeleteFunction_NonExistent', async () => {
    try {
      await client.send(new DeleteFunctionCommand({ FunctionName: 'NoSuchFunction_xyz_12345' }));
      throw new Error('expected error for non-existent function');
    } catch (err) {
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  }));

  results.push(await runner.runTest('lambda', 'CreateFunction_InvalidRuntime', async () => {
    const invRtFuncName = `InvRtFunc-${ts}`;
    const invRtRoleName = `InvRtRole-${ts}`;
    const invRtRole = `arn:aws:iam::000000000000:role/${invRtRoleName}`;
    await createIAMRole(iamClient, invRtRoleName, lambdaTrustPolicy);
    try {
      try {
        await client.send(new CreateFunctionCommand({
          FunctionName: invRtFuncName,
          Runtime: 'invalid_runtime_99' as any,
          Role: invRtRole,
          Handler: 'index.handler',
          Code: { ZipFile: new TextEncoder().encode('code') },
        }));
        await safeCleanup(async () => { await client.send(new DeleteFunctionCommand({ FunctionName: invRtFuncName })); });
        throw new Error('expected error for invalid runtime');
      } catch (err) {
        assertErrorContains(err, 'InvalidParameterValueException');
      }
    } finally {
      await deleteIAMRole(iamClient, invRtRoleName);
    }
  }));

  results.push(await runner.runTest('lambda', 'GetAlias_NonExistent', async () => {
    try {
      await client.send(new GetAliasCommand({
        FunctionName: functionName,
        Name: 'nonexistent-alias-xyz',
      }));
      throw new Error('expected error for non-existent alias');
    } catch (err) {
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  }));

  results.push(await runner.runTest('lambda', 'GetLayerVersion_NonExistent', async () => {
    try {
      await client.send(new GetLayerVersionCommand({
        LayerName: 'nonexistent-layer-xyz',
        VersionNumber: 999,
      }));
      throw new Error('expected error for non-existent layer version');
    } catch (err) {
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  }));

  results.push(await runner.runTest('lambda', 'GetFunctionUrlConfig_NoConfig', async () => {
    const nofcFuncName = `NofcFunc-${ts}`;
    const nofcRoleName = `NofcRole-${ts}`;
    const nofcRole = `arn:aws:iam::000000000000:role/${nofcRoleName}`;
    await createIAMRole(iamClient, nofcRoleName, lambdaTrustPolicy);
    try {
      await client.send(new CreateFunctionCommand({
        FunctionName: nofcFuncName, Runtime: 'nodejs22.x', Role: nofcRole, Handler: 'index.handler',
        Code: { ZipFile: new TextEncoder().encode('code') },
      }));
      try {
        await client.send(new GetFunctionUrlConfigCommand({ FunctionName: nofcFuncName }));
        throw new Error('expected error when no URL config set');
      } catch (err) {
        assertErrorContains(err, 'ResourceNotFoundException');
      }
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteFunctionCommand({ FunctionName: nofcFuncName })); });
      await deleteIAMRole(iamClient, nofcRoleName);
    }
  }));

  results.push(await runner.runTest('lambda', 'PutFunctionEventInvokeConfig_NonExistent', async () => {
    try {
      await client.send(new PutFunctionEventInvokeConfigCommand({
        FunctionName: 'nonexistent-func-xyz-123',
        MaximumEventAgeInSeconds: 3600,
      }));
      throw new Error('expected error for non-existent function');
    } catch (err) {
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  }));

  return results;
}
