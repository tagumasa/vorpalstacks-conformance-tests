import {
  CreateFunctionCommand,
  GetFunctionCommand,
  GetFunctionConfigurationCommand,
  ListFunctionsCommand,
  UpdateFunctionCodeCommand,
  UpdateFunctionConfigurationCommand,
  PublishVersionCommand,
  ListVersionsByFunctionCommand,
  CreateAliasCommand,
  GetAliasCommand,
  UpdateAliasCommand,
  ListAliasesCommand,
  InvokeCommand,
  PutFunctionConcurrencyCommand,
  GetFunctionConcurrencyCommand,
  DeleteFunctionConcurrencyCommand,
  AddPermissionCommand,
  GetPolicyCommand,
  RemovePermissionCommand,
  TagResourceCommand,
  ListTagsCommand,
  UntagResourceCommand,
  DeleteAliasCommand,
  GetAccountSettingsCommand,
  DeleteFunctionCommand,
} from '@aws-sdk/client-lambda';
import { LambdaTestContext, lambdaTrustPolicy } from './context.js';
import { assertErrorContains, createIAMRole, deleteIAMRole, safeCleanup } from '../../helpers.js';

export async function runFunctionTests(ctx: LambdaTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, ts, functionName, roleArn, functionCode } = ctx;
  const results: import('../../runner.js').TestResult[] = [];
  let addPermStatementID = '';

  results.push(await runner.runTest('lambda', 'CreateFunction', async () => {
    await client.send(new CreateFunctionCommand({
      FunctionName: functionName,
      Runtime: 'nodejs22.x',
      Role: roleArn,
      Handler: 'index.handler',
      Code: { ZipFile: functionCode },
    }));
  }));

  results.push(await runner.runTest('lambda', 'GetFunction', async () => {
    const resp = await client.send(new GetFunctionCommand({ FunctionName: functionName }));
    if (!resp.Configuration) throw new Error('configuration to be defined');
  }));

  results.push(await runner.runTest('lambda', 'GetFunctionConfiguration', async () => {
    const resp = await client.send(new GetFunctionConfigurationCommand({ FunctionName: functionName }));
    if (!resp.FunctionName) throw new Error('function name to be defined');
  }));

  results.push(await runner.runTest('lambda', 'ListFunctions', async () => {
    const resp = await client.send(new ListFunctionsCommand({}));
    if (!resp.Functions) throw new Error('functions list to be defined');
  }));

  results.push(await runner.runTest('lambda', 'UpdateFunctionCode', async () => {
    const newCode = new TextEncoder().encode('exports.handler = async (event) => { return { statusCode: 200, body: "Updated" }; };');
    const resp = await client.send(new UpdateFunctionCodeCommand({
      FunctionName: functionName,
      ZipFile: newCode,
    }));
    if (!resp.LastModified) throw new Error('LastModified to be defined');
  }));

  results.push(await runner.runTest('lambda', 'UpdateFunctionConfiguration', async () => {
    const resp = await client.send(new UpdateFunctionConfigurationCommand({
      FunctionName: functionName,
      Description: 'Updated function',
    }));
    if (!resp) throw new Error('response to be defined');
  }));

  results.push(await runner.runTest('lambda', 'PublishVersion', async () => {
    const resp = await client.send(new PublishVersionCommand({ FunctionName: functionName }));
    if (!resp.Version) throw new Error('Version to be defined');
  }));

  results.push(await runner.runTest('lambda', 'ListVersionsByFunction', async () => {
    const resp = await client.send(new ListVersionsByFunctionCommand({ FunctionName: functionName }));
    if (!resp.Versions) throw new Error('versions list to be defined');
  }));

  results.push(await runner.runTest('lambda', 'CreateAlias', async () => {
    const resp = await client.send(new CreateAliasCommand({
      FunctionName: functionName,
      Name: 'live',
      FunctionVersion: '$LATEST',
    }));
    if (!resp.AliasArn) throw new Error('AliasArn to be defined');
  }));

  results.push(await runner.runTest('lambda', 'GetAlias', async () => {
    const resp = await client.send(new GetAliasCommand({
      FunctionName: functionName,
      Name: 'live',
    }));
    if (!resp.Name) throw new Error('alias name to be defined');
  }));

  results.push(await runner.runTest('lambda', 'UpdateAlias', async () => {
    const resp = await client.send(new UpdateAliasCommand({
      FunctionName: functionName,
      Name: 'live',
      Description: 'Production alias',
    }));
    if (!resp) throw new Error('response to be defined');
  }));

  results.push(await runner.runTest('lambda', 'ListAliases', async () => {
    const resp = await client.send(new ListAliasesCommand({ FunctionName: functionName }));
    if (!resp.Aliases) throw new Error('aliases list to be defined');
  }));

  results.push(await runner.runTest('lambda', 'Invoke', async () => {
    const resp = await client.send(new InvokeCommand({ FunctionName: functionName }));
    if (resp.StatusCode === 0) throw new Error('status code is zero');
  }));

  results.push(await runner.runTest('lambda', 'PutFunctionConcurrency', async () => {
    const resp = await client.send(new PutFunctionConcurrencyCommand({
      FunctionName: functionName,
      ReservedConcurrentExecutions: 10,
    }));
    if (!resp) throw new Error('response to be defined');
  }));

  results.push(await runner.runTest('lambda', 'GetFunctionConcurrency', async () => {
    const resp = await client.send(new GetFunctionConcurrencyCommand({ FunctionName: functionName }));
    if (resp.ReservedConcurrentExecutions === undefined) throw new Error('concurrency to be defined');
  }));

  results.push(await runner.runTest('lambda', 'DeleteFunctionConcurrency', async () => {
    const resp = await client.send(new DeleteFunctionConcurrencyCommand({ FunctionName: functionName }));
    if (!resp) throw new Error('response to be defined');
  }));

  results.push(await runner.runTest('lambda', 'AddPermission', async () => {
    addPermStatementID = `stmt-${Date.now()}`;
    const resp = await client.send(new AddPermissionCommand({
      FunctionName: functionName,
      StatementId: addPermStatementID,
      Action: 'lambda:InvokeFunction',
      Principal: 'apigateway.amazonaws.com',
    }));
    if (!resp) throw new Error('AddPermission response to be defined');
  }));

  results.push(await runner.runTest('lambda', 'GetPolicy', async () => {
    const resp = await client.send(new GetPolicyCommand({ FunctionName: functionName }));
    if (!resp.Policy || resp.Policy === '') throw new Error('policy is empty');
  }));

  results.push(await runner.runTest('lambda', 'RemovePermission', async () => {
    const statementID = `stmt-${Date.now()}`;
    await client.send(new AddPermissionCommand({
      FunctionName: functionName,
      StatementId: statementID,
      Action: 'lambda:InvokeFunction',
      Principal: 'apigateway.amazonaws.com',
    }));
    await client.send(new RemovePermissionCommand({
      FunctionName: functionName,
      StatementId: statementID,
    }));
  }));

  const functionArn = `arn:aws:lambda:${ctx.iamClient.config.region || 'us-east-1'}:000000000000:function:${functionName}`;

  results.push(await runner.runTest('lambda', 'TagResource', async () => {
    const resp = await client.send(new TagResourceCommand({
      Resource: functionArn,
      Tags: { Environment: 'test', Project: 'sdk-tests' },
    }));
    if (!resp) throw new Error('response to be defined');
  }));

  results.push(await runner.runTest('lambda', 'ListTags', async () => {
    const resp = await client.send(new ListTagsCommand({ Resource: functionArn }));
    if (!resp.Tags) throw new Error('tags to be defined');
  }));

  results.push(await runner.runTest('lambda', 'UntagResource', async () => {
    const resp = await client.send(new UntagResourceCommand({
      Resource: functionArn,
      TagKeys: ['Environment'],
    }));
    if (!resp) throw new Error('response to be defined');
  }));

  results.push(await runner.runTest('lambda', 'DeleteAlias', async () => {
    const resp = await client.send(new DeleteAliasCommand({
      FunctionName: functionName,
      Name: 'live',
    }));
    if (!resp) throw new Error('response to be defined');
  }));

  results.push(await runner.runTest('lambda', 'GetAccountSettings', async () => {
    const resp = await client.send(new GetAccountSettingsCommand({}));
    if (!resp.AccountLimit) throw new Error('account limit to be defined');
  }));

  results.push(await runner.runTest('lambda', 'DeleteFunction', async () => {
    if (addPermStatementID) {
      await safeCleanup(async () => {
        await client.send(new RemovePermissionCommand({
          FunctionName: functionName,
          StatementId: addPermStatementID,
        }));
      });
    }
    const resp = await client.send(new DeleteFunctionCommand({ FunctionName: functionName }));
    if (!resp) throw new Error('response to be defined');
  }));

  return results;
}

export async function runFunctionVerifyTests(ctx: LambdaTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, iamClient, ts, roleArn, functionCode } = ctx;
  const results: import('../../runner.js').TestResult[] = [];

  results.push(await runner.runTest('lambda', 'Invoke_VerifyResponsePayload', async () => {
    const invFunc = `InvFunc-${ts}`;
    const invRoleName = `InvRole-${ts}`;
    const invRole = `arn:aws:iam::000000000000:role/${invRoleName}`;
    const invCode = new TextEncoder().encode('exports.handler = async (event) => { return { statusCode: 200, body: JSON.stringify({result: "ok"}) }; };');
    await createIAMRole(iamClient, invRoleName, lambdaTrustPolicy);
    try {
      await client.send(new CreateFunctionCommand({
        FunctionName: invFunc, Runtime: 'nodejs22.x', Role: invRole, Handler: 'index.handler', Code: { ZipFile: invCode },
      }));
      try {
        const resp = await client.send(new InvokeCommand({ FunctionName: invFunc }));
        if (resp.StatusCode !== 200) throw new Error(`expected status 200, got ${resp.StatusCode}`);
        if (!resp.Payload || resp.Payload.length === 0) throw new Error('expected non-empty payload');
        const payload = new TextDecoder().decode(resp.Payload);
        if (payload === '') throw new Error('payload should not be empty');
      } finally {
        await safeCleanup(async () => { await client.send(new DeleteFunctionCommand({ FunctionName: invFunc })); });
      }
    } finally {
      await deleteIAMRole(iamClient, invRoleName);
    }
  }));

  results.push(await runner.runTest('lambda', 'GetFunction_ContainsCodeConfig', async () => {
    const gfcFunc = `GfcFunc-${ts}`;
    const gfcRoleName = `GfcRole-${ts}`;
    const gfcRole = `arn:aws:iam::000000000000:role/${gfcRoleName}`;
    const gfcCode = new TextEncoder().encode('exports.handler = async () => { return 1; };');
    const gfcDesc = 'Test description for verification';
    await createIAMRole(iamClient, gfcRoleName, lambdaTrustPolicy);
    try {
      await client.send(new CreateFunctionCommand({
        FunctionName: gfcFunc, Runtime: 'nodejs22.x', Role: gfcRole, Handler: 'index.handler',
        Code: { ZipFile: gfcCode }, Description: gfcDesc, Timeout: 15, MemorySize: 256,
      }));
      try {
        const resp = await client.send(new GetFunctionCommand({ FunctionName: gfcFunc }));
        if (!resp.Configuration) throw new Error('configuration to be defined');
        if (resp.Configuration.Description !== gfcDesc) throw new Error(`description mismatch, got ${resp.Configuration.Description}`);
        if (resp.Configuration.Timeout !== 15) throw new Error(`timeout mismatch, got ${resp.Configuration.Timeout}`);
        if (resp.Configuration.MemorySize !== 256) throw new Error(`memory size mismatch, got ${resp.Configuration.MemorySize}`);
        if (resp.Configuration.Runtime !== 'nodejs22.x') throw new Error(`runtime mismatch, got ${resp.Configuration.Runtime}`);
        if (!resp.Code || !resp.Code.Location) throw new Error('code location should not be nil');
      } finally {
        await safeCleanup(async () => { await client.send(new DeleteFunctionCommand({ FunctionName: gfcFunc })); });
      }
    } finally {
      await deleteIAMRole(iamClient, gfcRoleName);
    }
  }));

  results.push(await runner.runTest('lambda', 'PublishVersion_VerifyVersion', async () => {
    const pvFunc = `PvFunc-${ts}`;
    const pvRoleName = `PvRole-${ts}`;
    const pvRole = `arn:aws:iam::000000000000:role/${pvRoleName}`;
    const pvCode = new TextEncoder().encode('exports.handler = async () => { return 1; };');
    await createIAMRole(iamClient, pvRoleName, lambdaTrustPolicy);
    try {
      await client.send(new CreateFunctionCommand({
        FunctionName: pvFunc, Runtime: 'nodejs22.x', Role: pvRole, Handler: 'index.handler', Code: { ZipFile: pvCode },
      }));
      try {
        const resp = await client.send(new PublishVersionCommand({ FunctionName: pvFunc }));
        if (!resp.Version || resp.Version === '$LATEST') throw new Error(`published version should not be $LATEST, got ${resp.Version}`);
        if (resp.Version !== '1') throw new Error(`first published version should be 1, got ${resp.Version}`);
      } finally {
        await safeCleanup(async () => { await client.send(new DeleteFunctionCommand({ FunctionName: pvFunc })); });
      }
    } finally {
      await deleteIAMRole(iamClient, pvRoleName);
    }
  }));

  results.push(await runner.runTest('lambda', 'ListFunctions_ReturnsCreated', async () => {
    const lfFunc = `LfFunc-${ts}`;
    const lfRoleName = `LfRole-${ts}`;
    const lfRole = `arn:aws:iam::000000000000:role/${lfRoleName}`;
    const lfCode = new TextEncoder().encode('exports.handler = async () => { return 1; };');
    await createIAMRole(iamClient, lfRoleName, lambdaTrustPolicy);
    try {
      await client.send(new CreateFunctionCommand({
        FunctionName: lfFunc, Runtime: 'nodejs22.x', Role: lfRole, Handler: 'index.handler', Code: { ZipFile: lfCode },
      }));
      try {
        const resp = await client.send(new ListFunctionsCommand({}));
        const found = resp.Functions?.some((f) => {
          if (f.FunctionName !== lfFunc) return false;
          if (f.Runtime !== 'nodejs22.x') throw new Error(`runtime mismatch in list, got ${f.Runtime}`);
          if (f.Handler !== 'index.handler') throw new Error(`handler mismatch in list, got ${f.Handler}`);
          return true;
        });
        if (!found) throw new Error(`created function ${lfFunc} not found in ListFunctions`);
      } finally {
        await safeCleanup(async () => { await client.send(new DeleteFunctionCommand({ FunctionName: lfFunc })); });
      }
    } finally {
      await deleteIAMRole(iamClient, lfRoleName);
    }
  }));

  results.push(await runner.runTest('lambda', 'CreateAlias_DuplicateName', async () => {
    const caFunc = `CaFunc-${ts}`;
    const caRoleName = `CaRole-${ts}`;
    const caRole = `arn:aws:iam::000000000000:role/${caRoleName}`;
    const caCode = new TextEncoder().encode('exports.handler = async () => { return 1; };');
    await createIAMRole(iamClient, caRoleName, lambdaTrustPolicy);
    try {
      await client.send(new CreateFunctionCommand({
        FunctionName: caFunc, Runtime: 'nodejs22.x', Role: caRole, Handler: 'index.handler', Code: { ZipFile: caCode },
      }));
      try {
        await client.send(new CreateAliasCommand({
          FunctionName: caFunc, Name: 'prod', FunctionVersion: '$LATEST',
        }));
        try {
          await client.send(new CreateAliasCommand({
            FunctionName: caFunc, Name: 'prod', FunctionVersion: '$LATEST',
          }));
          throw new Error('expected error for duplicate alias name');
        } catch (err) {
          assertErrorContains(err, 'ResourceConflictException');
        }
      } finally {
        await safeCleanup(async () => { await client.send(new DeleteFunctionCommand({ FunctionName: caFunc })); });
      }
    } finally {
      await deleteIAMRole(iamClient, caRoleName);
    }
  }));

  results.push(await runner.runTest('lambda', 'UpdateFunctionConfiguration_VerifyUpdate', async () => {
    const ucFunc = `UcFunc-${ts}`;
    const ucRoleName = `UcRole-${ts}`;
    const ucRole = `arn:aws:iam::000000000000:role/${ucRoleName}`;
    const ucCode = new TextEncoder().encode('exports.handler = async () => { return 1; };');
    await createIAMRole(iamClient, ucRoleName, lambdaTrustPolicy);
    try {
      await client.send(new CreateFunctionCommand({
        FunctionName: ucFunc, Runtime: 'nodejs22.x', Role: ucRole, Handler: 'index.handler',
        Code: { ZipFile: ucCode }, Description: 'original',
      }));
      try {
        const newDesc = 'updated description';
        await client.send(new UpdateFunctionConfigurationCommand({
          FunctionName: ucFunc, Description: newDesc, Timeout: 30, MemorySize: 512,
        }));
        const resp = await client.send(new GetFunctionConfigurationCommand({ FunctionName: ucFunc }));
        if (resp.Description !== newDesc) throw new Error(`description not updated, got ${resp.Description}`);
        if (resp.Timeout !== 30) throw new Error(`timeout not updated, got ${resp.Timeout}`);
        if (resp.MemorySize !== 512) throw new Error(`memory size not updated, got ${resp.MemorySize}`);
      } finally {
        await safeCleanup(async () => { await client.send(new DeleteFunctionCommand({ FunctionName: ucFunc })); });
      }
    } finally {
      await deleteIAMRole(iamClient, ucRoleName);
    }
  }));

  results.push(await runner.runTest('lambda', 'CreateFunction_DuplicateName', async () => {
    const dupName = `DupFunc-${ts}`;
    const dupRoleName = `DupRole-${ts}`;
    const dupRole = `arn:aws:iam::000000000000:role/${dupRoleName}`;
    const dupCode = new TextEncoder().encode('exports.handler = async () => { return 1; };');
    await createIAMRole(iamClient, dupRoleName, lambdaTrustPolicy);
    try {
      await client.send(new CreateFunctionCommand({
        FunctionName: dupName, Runtime: 'nodejs22.x', Role: dupRole, Handler: 'index.handler', Code: { ZipFile: dupCode },
      }));
      try {
        await client.send(new CreateFunctionCommand({
          FunctionName: dupName, Runtime: 'nodejs22.x', Role: dupRole, Handler: 'index.handler', Code: { ZipFile: dupCode },
        }));
        throw new Error('expected error for duplicate function name');
      } catch (err) {
        assertErrorContains(err, 'ResourceConflictException');
      }
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteFunctionCommand({ FunctionName: dupName })); });
      await deleteIAMRole(iamClient, dupRoleName);
    }
  }));

  return results;
}
