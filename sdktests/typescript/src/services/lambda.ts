import {
  LambdaClient,
  CreateFunctionCommand,
  GetFunctionCommand,
  ListFunctionsCommand,
  InvokeCommand,
  DeleteFunctionCommand,
  GetFunctionConfigurationCommand,
  UpdateFunctionCodeCommand,
  UpdateFunctionConfigurationCommand,
  PublishVersionCommand,
  ListVersionsByFunctionCommand,
  CreateAliasCommand,
  GetAliasCommand,
  UpdateAliasCommand,
  ListAliasesCommand,
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
} from '@aws-sdk/client-lambda';
import { IAMClient, CreateRoleCommand, DeleteRoleCommand } from '@aws-sdk/client-iam';
import { ResourceNotFoundException, ResourceConflictException } from '@aws-sdk/client-lambda';
import { TestRunner } from '../runner.js';
import { TestResult } from '../runner.js';

function makeUniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 999999)}`;
}

export async function runLambdaTests(
  runner: TestRunner,
  lambdaClient: LambdaClient,
  iamClient: IAMClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const functionName = makeUniqueName('TSFunc');
  const roleName = makeUniqueName('TSRole');
  const functionCode = Buffer.from(
    'exports.handler = async (event) => { return { statusCode: 200, body: JSON.stringify({ result: "ok" }) }; };'
  );

  const trustPolicy = JSON.stringify({
    Version: '2012-10-17',
    Statement: [
      {
        Effect: 'Allow',
        Principal: { Service: 'lambda.amazonaws.com' },
        Action: 'sts:AssumeRole',
      },
    ],
  });

  let roleArn = '';
  try {
    await iamClient.send(
      new CreateRoleCommand({
        RoleName: roleName,
        AssumeRolePolicyDocument: trustPolicy,
      })
    );
    roleArn = `arn:aws:iam::000000000000:role/${roleName}`;
  } catch {
    roleArn = `arn:aws:iam::000000000000:role/${roleName}`;
  }

  try {
    // CreateFunction
    results.push(
      await runner.runTest('lambda', 'CreateFunction', async () => {
        await lambdaClient.send(
          new CreateFunctionCommand({
            FunctionName: functionName,
            Runtime: 'nodejs22.x',
            Role: roleArn,
            Handler: 'index.handler',
            Code: { ZipFile: functionCode },
          })
        );
      })
    );

    // GetFunction
    results.push(
      await runner.runTest('lambda', 'GetFunction', async () => {
        const resp = await lambdaClient.send(
          new GetFunctionCommand({ FunctionName: functionName })
        );
        if (!resp.Configuration) throw new Error('Configuration is null');
      })
    );

    // GetFunctionConfiguration
    results.push(
      await runner.runTest('lambda', 'GetFunctionConfiguration', async () => {
        const resp = await lambdaClient.send(
          new GetFunctionConfigurationCommand({ FunctionName: functionName })
        );
        if (!resp.FunctionName) throw new Error('function name is nil');
      })
    );

    // ListFunctions
    results.push(
      await runner.runTest('lambda', 'ListFunctions', async () => {
        const resp = await lambdaClient.send(new ListFunctionsCommand({}));
        if (!resp.Functions) throw new Error('Functions list is null');
      })
    );

    // UpdateFunctionCode
    results.push(
      await runner.runTest('lambda', 'UpdateFunctionCode', async () => {
        const newCode = Buffer.from(
          'exports.handler = async (event) => { return { statusCode: 200, body: "Updated" }; };'
        );
        const resp = await lambdaClient.send(
          new UpdateFunctionCodeCommand({
            FunctionName: functionName,
            ZipFile: newCode,
          })
        );
        if (!resp.LastModified) throw new Error('LastModified is nil');
      })
    );

    // UpdateFunctionConfiguration
    results.push(
      await runner.runTest('lambda', 'UpdateFunctionConfiguration', async () => {
        const resp = await lambdaClient.send(
          new UpdateFunctionConfigurationCommand({
            FunctionName: functionName,
            Description: 'Updated function',
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // PublishVersion
    results.push(
      await runner.runTest('lambda', 'PublishVersion', async () => {
        const resp = await lambdaClient.send(
          new PublishVersionCommand({ FunctionName: functionName })
        );
        if (!resp.Version) throw new Error('Version is nil');
      })
    );

    // ListVersionsByFunction
    results.push(
      await runner.runTest('lambda', 'ListVersionsByFunction', async () => {
        const resp = await lambdaClient.send(
          new ListVersionsByFunctionCommand({ FunctionName: functionName })
        );
        if (!resp.Versions) throw new Error('versions list is nil');
      })
    );

    // CreateAlias
    results.push(
      await runner.runTest('lambda', 'CreateAlias', async () => {
        const resp = await lambdaClient.send(
          new CreateAliasCommand({
            FunctionName: functionName,
            Name: 'live',
            FunctionVersion: '$LATEST',
          })
        );
        if (!resp.AliasArn) throw new Error('AliasArn is nil');
      })
    );

    // GetAlias
    results.push(
      await runner.runTest('lambda', 'GetAlias', async () => {
        const resp = await lambdaClient.send(
          new GetAliasCommand({
            FunctionName: functionName,
            Name: 'live',
          })
        );
        if (!resp.Name) throw new Error('alias name is nil');
      })
    );

    // UpdateAlias
    results.push(
      await runner.runTest('lambda', 'UpdateAlias', async () => {
        const resp = await lambdaClient.send(
          new UpdateAliasCommand({
            FunctionName: functionName,
            Name: 'live',
            Description: 'Production alias',
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // ListAliases
    results.push(
      await runner.runTest('lambda', 'ListAliases', async () => {
        const resp = await lambdaClient.send(
          new ListAliasesCommand({ FunctionName: functionName })
        );
        if (!resp.Aliases) throw new Error('aliases list is nil');
      })
    );

    // Invoke
    results.push(
      await runner.runTest('lambda', 'Invoke', async () => {
        const resp = await lambdaClient.send(
          new InvokeCommand({ FunctionName: functionName })
        );
        if (!resp.StatusCode || resp.StatusCode === 0) throw new Error('StatusCode is zero');
      })
    );

    // PutFunctionConcurrency
    results.push(
      await runner.runTest('lambda', 'PutFunctionConcurrency', async () => {
        const resp = await lambdaClient.send(
          new PutFunctionConcurrencyCommand({
            FunctionName: functionName,
            ReservedConcurrentExecutions: 10,
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // GetFunctionConcurrency
    results.push(
      await runner.runTest('lambda', 'GetFunctionConcurrency', async () => {
        const resp = await lambdaClient.send(
          new GetFunctionConcurrencyCommand({ FunctionName: functionName })
        );
        if (resp.ReservedConcurrentExecutions === undefined) throw new Error('concurrency is undefined');
      })
    );

    // DeleteFunctionConcurrency
    results.push(
      await runner.runTest('lambda', 'DeleteFunctionConcurrency', async () => {
        const resp = await lambdaClient.send(
          new DeleteFunctionConcurrencyCommand({ FunctionName: functionName })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // AddPermission
    const statementId = makeUniqueName('stmt');
    results.push(
      await runner.runTest('lambda', 'AddPermission', async () => {
        const resp = await lambdaClient.send(
          new AddPermissionCommand({
            FunctionName: functionName,
            StatementId: statementId,
            Action: 'lambda:InvokeFunction',
            Principal: 'apigateway.amazonaws.com',
          })
        );
        if (!resp) throw new Error('AddPermission response is nil');
      })
    );

    // GetPolicy
    results.push(
      await runner.runTest('lambda', 'GetPolicy', async () => {
        const resp = await lambdaClient.send(
          new GetPolicyCommand({ FunctionName: functionName })
        );
        if (!resp.Policy || resp.Policy === '') throw new Error('policy is empty');
      })
    );

    // RemovePermission
    results.push(
      await runner.runTest('lambda', 'RemovePermission', async () => {
        const removeStatementId = makeUniqueName('stmt');
        await lambdaClient.send(
          new AddPermissionCommand({
            FunctionName: functionName,
            StatementId: removeStatementId,
            Action: 'lambda:InvokeFunction',
            Principal: 'apigateway.amazonaws.com',
          })
        );
        await lambdaClient.send(
          new RemovePermissionCommand({
            FunctionName: functionName,
            StatementId: removeStatementId,
          })
        );
      })
    );

    // TagResource
    const functionARN = `arn:aws:lambda:${region}:000000000000:function:${functionName}`;
    results.push(
      await runner.runTest('lambda', 'TagResource', async () => {
        const resp = await lambdaClient.send(
          new TagResourceCommand({
            Resource: functionARN,
            Tags: { Environment: 'test', Project: 'sdk-tests' },
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // ListTags
    results.push(
      await runner.runTest('lambda', 'ListTags', async () => {
        const resp = await lambdaClient.send(
          new ListTagsCommand({ Resource: functionARN })
        );
        if (!resp.Tags) throw new Error('tags is nil');
      })
    );

    // UntagResource
    results.push(
      await runner.runTest('lambda', 'UntagResource', async () => {
        const resp = await lambdaClient.send(
          new UntagResourceCommand({
            Resource: functionARN,
            TagKeys: ['Environment'],
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // DeleteAlias
    results.push(
      await runner.runTest('lambda', 'DeleteAlias', async () => {
        const resp = await lambdaClient.send(
          new DeleteAliasCommand({
            FunctionName: functionName,
            Name: 'live',
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // GetAccountSettings
    results.push(
      await runner.runTest('lambda', 'GetAccountSettings', async () => {
        const resp = await lambdaClient.send(new GetAccountSettingsCommand({}));
        if (!resp.AccountLimit) throw new Error('account limit is nil');
      })
    );

    // DeleteFunction
    results.push(
      await runner.runTest('lambda', 'DeleteFunction', async () => {
        const resp = await lambdaClient.send(
          new DeleteFunctionCommand({ FunctionName: functionName })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

  } finally {
    try {
      await lambdaClient.send(new DeleteFunctionCommand({ FunctionName: functionName }));
    } catch { /* ignore */ }
    try {
      await iamClient.send(new DeleteRoleCommand({ RoleName: roleName }));
    } catch { /* ignore */ }
  }

  // === ERROR / EDGE CASE TESTS ===

  // GetFunction_NonExistent
  results.push(
    await runner.runTest('lambda', 'GetFunction_NonExistent', async () => {
      try {
        await lambdaClient.send(
          new GetFunctionCommand({ FunctionName: 'NoSuchFunction_xyz_12345' })
        );
        throw new Error('Expected ResourceNotFoundException but got none');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  // Invoke_NonExistent
  results.push(
    await runner.runTest('lambda', 'Invoke_NonExistent', async () => {
      try {
        await lambdaClient.send(
          new InvokeCommand({ FunctionName: 'NoSuchFunction_xyz_12345' })
        );
        throw new Error('Expected ResourceNotFoundException but got none');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  // UpdateFunctionCode_NonExistent
  results.push(
    await runner.runTest('lambda', 'UpdateFunctionCode_NonExistent', async () => {
      try {
        await lambdaClient.send(
          new UpdateFunctionCodeCommand({
            FunctionName: 'NoSuchFunction_xyz_12345',
            ZipFile: Buffer.from('code'),
          })
        );
        throw new Error('Expected ResourceNotFoundException but got none');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  // DeleteFunction_NonExistent
  results.push(
    await runner.runTest('lambda', 'DeleteFunction_NonExistent', async () => {
      try {
        await lambdaClient.send(
          new DeleteFunctionCommand({ FunctionName: 'NoSuchFunction_xyz_12345' })
        );
        throw new Error('Expected ResourceNotFoundException but got none');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  // CreateFunction_DuplicateName
  results.push(
    await runner.runTest('lambda', 'CreateFunction_DuplicateName', async () => {
      const dupName = makeUniqueName('DupFunc');
      const dupRoleName = makeUniqueName('DupRole');
      const dupRole = `arn:aws:iam::000000000000:role/${dupRoleName}`;
      const dupCode = Buffer.from('exports.handler = async () => { return 1; };');
      try {
        await iamClient.send(
          new CreateRoleCommand({
            RoleName: dupRoleName,
            AssumeRolePolicyDocument: trustPolicy,
          })
        );
        await lambdaClient.send(
          new CreateFunctionCommand({
            FunctionName: dupName,
            Runtime: 'nodejs22.x',
            Role: dupRole,
            Handler: 'index.handler',
            Code: { ZipFile: dupCode },
          })
        );
        try {
          await lambdaClient.send(
            new CreateFunctionCommand({
              FunctionName: dupName,
              Runtime: 'nodejs22.x',
              Role: dupRole,
              Handler: 'index.handler',
              Code: { ZipFile: dupCode },
            })
          );
          throw new Error('expected error for duplicate function name');
        } catch (err) {
          if (!(err instanceof ResourceConflictException)) {
            const name = err instanceof Error ? err.constructor.name : String(err);
            throw new Error(`Expected ResourceConflictException, got ${name}`);
          }
        }
      } finally {
        try { await lambdaClient.send(new DeleteFunctionCommand({ FunctionName: dupName })); } catch { /* ignore */ }
        try { await iamClient.send(new DeleteRoleCommand({ RoleName: dupRoleName })); } catch { /* ignore */ }
      }
    })
  );

  // Invoke_VerifyResponsePayload
  results.push(
    await runner.runTest('lambda', 'Invoke_VerifyResponsePayload', async () => {
      const invFunc = makeUniqueName('InvFunc');
      const invRoleName = makeUniqueName('InvRole');
      const invRole = `arn:aws:iam::000000000000:role/${invRoleName}`;
      const invCode = Buffer.from(
        'exports.handler = async (event) => { return { statusCode: 200, body: JSON.stringify({result: "ok"}) }; };'
      );
      try {
        await iamClient.send(
          new CreateRoleCommand({
            RoleName: invRoleName,
            AssumeRolePolicyDocument: trustPolicy,
          })
        );
        await lambdaClient.send(
          new CreateFunctionCommand({
            FunctionName: invFunc,
            Runtime: 'nodejs22.x',
            Role: invRole,
            Handler: 'index.handler',
            Code: { ZipFile: invCode },
          })
        );
        const resp = await lambdaClient.send(
          new InvokeCommand({ FunctionName: invFunc })
        );
        if (resp.StatusCode !== 200) throw new Error(`expected status 200, got ${resp.StatusCode}`);
        if (!resp.Payload || resp.Payload.length === 0) throw new Error('expected non-empty payload');
      } finally {
        try { await lambdaClient.send(new DeleteFunctionCommand({ FunctionName: invFunc })); } catch { /* ignore */ }
        try { await iamClient.send(new DeleteRoleCommand({ RoleName: invRoleName })); } catch { /* ignore */ }
      }
    })
  );

  // GetFunction_ContainsCodeConfig
  results.push(
    await runner.runTest('lambda', 'GetFunction_ContainsCodeConfig', async () => {
      const gfcFunc = makeUniqueName('GfcFunc');
      const gfcRoleName = makeUniqueName('GfcRole');
      const gfcRole = `arn:aws:iam::000000000000:role/${gfcRoleName}`;
      const gfcCode = Buffer.from('exports.handler = async () => { return 1; };');
      const gfcDesc = 'Test description for verification';
      try {
        await iamClient.send(
          new CreateRoleCommand({
            RoleName: gfcRoleName,
            AssumeRolePolicyDocument: trustPolicy,
          })
        );
        await lambdaClient.send(
          new CreateFunctionCommand({
            FunctionName: gfcFunc,
            Runtime: 'nodejs22.x',
            Role: gfcRole,
            Handler: 'index.handler',
            Code: { ZipFile: gfcCode },
            Description: gfcDesc,
            Timeout: 15,
            MemorySize: 256,
          })
        );
        const resp = await lambdaClient.send(
          new GetFunctionCommand({ FunctionName: gfcFunc })
        );
        if (!resp.Configuration) throw new Error('configuration is nil');
        if (resp.Configuration.Description !== gfcDesc) throw new Error('description mismatch');
        if (resp.Configuration.Timeout !== 15) throw new Error('timeout mismatch');
        if (resp.Configuration.MemorySize !== 256) throw new Error('memory size mismatch');
        if (!resp.Code?.Location) throw new Error('code location should not be nil');
      } finally {
        try { await lambdaClient.send(new DeleteFunctionCommand({ FunctionName: gfcFunc })); } catch { /* ignore */ }
        try { await iamClient.send(new DeleteRoleCommand({ RoleName: gfcRoleName })); } catch { /* ignore */ }
      }
    })
  );

  // PublishVersion_VerifyVersion
  results.push(
    await runner.runTest('lambda', 'PublishVersion_VerifyVersion', async () => {
      const pvFunc = makeUniqueName('PvFunc');
      const pvRoleName = makeUniqueName('PvRole');
      const pvRole = `arn:aws:iam::000000000000:role/${pvRoleName}`;
      const pvCode = Buffer.from('exports.handler = async () => { return 1; };');
      try {
        await iamClient.send(
          new CreateRoleCommand({
            RoleName: pvRoleName,
            AssumeRolePolicyDocument: trustPolicy,
          })
        );
        await lambdaClient.send(
          new CreateFunctionCommand({
            FunctionName: pvFunc,
            Runtime: 'nodejs22.x',
            Role: pvRole,
            Handler: 'index.handler',
            Code: { ZipFile: pvCode },
          })
        );
        const resp = await lambdaClient.send(
          new PublishVersionCommand({ FunctionName: pvFunc })
        );
        if (!resp.Version || resp.Version === '$LATEST') throw new Error(`published version should not be $LATEST, got ${resp.Version}`);
        if (resp.Version !== '1') throw new Error(`first published version should be 1, got ${resp.Version}`);
      } finally {
        try { await lambdaClient.send(new DeleteFunctionCommand({ FunctionName: pvFunc })); } catch { /* ignore */ }
        try { await iamClient.send(new DeleteRoleCommand({ RoleName: pvRoleName })); } catch { /* ignore */ }
      }
    })
  );

  // ListFunctions_ReturnsCreated
  results.push(
    await runner.runTest('lambda', 'ListFunctions_ReturnsCreated', async () => {
      const lfFunc = makeUniqueName('LfFunc');
      const lfRoleName = makeUniqueName('LfRole');
      const lfRole = `arn:aws:iam::000000000000:role/${lfRoleName}`;
      const lfCode = Buffer.from('exports.handler = async () => { return 1; };');
      try {
        await iamClient.send(
          new CreateRoleCommand({
            RoleName: lfRoleName,
            AssumeRolePolicyDocument: trustPolicy,
          })
        );
        await lambdaClient.send(
          new CreateFunctionCommand({
            FunctionName: lfFunc,
            Runtime: 'nodejs22.x',
            Role: lfRole,
            Handler: 'index.handler',
            Code: { ZipFile: lfCode },
          })
        );
        const resp = await lambdaClient.send(new ListFunctionsCommand({}));
        const found = resp.Functions?.some((f) => f.FunctionName === lfFunc);
        if (!found) throw new Error(`created function ${lfFunc} not found in ListFunctions`);
      } finally {
        try { await lambdaClient.send(new DeleteFunctionCommand({ FunctionName: lfFunc })); } catch { /* ignore */ }
        try { await iamClient.send(new DeleteRoleCommand({ RoleName: lfRoleName })); } catch { /* ignore */ }
      }
    })
  );

  // CreateAlias_DuplicateName
  results.push(
    await runner.runTest('lambda', 'CreateAlias_DuplicateName', async () => {
      const caFunc = makeUniqueName('CaFunc');
      const caRoleName = makeUniqueName('CaRole');
      const caRole = `arn:aws:iam::000000000000:role/${caRoleName}`;
      const caCode = Buffer.from('exports.handler = async () => { return 1; };');
      try {
        await iamClient.send(
          new CreateRoleCommand({
            RoleName: caRoleName,
            AssumeRolePolicyDocument: trustPolicy,
          })
        );
        await lambdaClient.send(
          new CreateFunctionCommand({
            FunctionName: caFunc,
            Runtime: 'nodejs22.x',
            Role: caRole,
            Handler: 'index.handler',
            Code: { ZipFile: caCode },
          })
        );
        await lambdaClient.send(
          new CreateAliasCommand({
            FunctionName: caFunc,
            Name: 'prod',
            FunctionVersion: '$LATEST',
          })
        );
        try {
          await lambdaClient.send(
            new CreateAliasCommand({
              FunctionName: caFunc,
              Name: 'prod',
              FunctionVersion: '$LATEST',
            })
          );
          throw new Error('expected error for duplicate alias name');
        } catch (err) {
          if (!(err instanceof ResourceConflictException)) {
            const name = err instanceof Error ? err.constructor.name : String(err);
            throw new Error(`Expected ResourceConflictException, got ${name}`);
          }
        }
      } finally {
        try { await lambdaClient.send(new DeleteFunctionCommand({ FunctionName: caFunc })); } catch { /* ignore */ }
        try { await iamClient.send(new DeleteRoleCommand({ RoleName: caRoleName })); } catch { /* ignore */ }
      }
    })
  );

  // UpdateFunctionConfiguration_VerifyUpdate
  results.push(
    await runner.runTest('lambda', 'UpdateFunctionConfiguration_VerifyUpdate', async () => {
      const ucFunc = makeUniqueName('UcFunc');
      const ucRoleName = makeUniqueName('UcRole');
      const ucRole = `arn:aws:iam::000000000000:role/${ucRoleName}`;
      const ucCode = Buffer.from('exports.handler = async () => { return 1; };');
      try {
        await iamClient.send(
          new CreateRoleCommand({
            RoleName: ucRoleName,
            AssumeRolePolicyDocument: trustPolicy,
          })
        );
        await lambdaClient.send(
          new CreateFunctionCommand({
            FunctionName: ucFunc,
            Runtime: 'nodejs22.x',
            Role: ucRole,
            Handler: 'index.handler',
            Code: { ZipFile: ucCode },
            Description: 'original',
          })
        );
        await lambdaClient.send(
          new UpdateFunctionConfigurationCommand({
            FunctionName: ucFunc,
            Description: 'updated description',
            Timeout: 30,
            MemorySize: 512,
          })
        );
        const resp = await lambdaClient.send(
          new GetFunctionConfigurationCommand({ FunctionName: ucFunc })
        );
        if (resp.Description !== 'updated description') throw new Error(`description not updated, got ${resp.Description}`);
        if (resp.Timeout !== 30) throw new Error(`timeout not updated, got ${resp.Timeout}`);
        if (resp.MemorySize !== 512) throw new Error(`memory size not updated, got ${resp.MemorySize}`);
      } finally {
        try { await lambdaClient.send(new DeleteFunctionCommand({ FunctionName: ucFunc })); } catch { /* ignore */ }
        try { await iamClient.send(new DeleteRoleCommand({ RoleName: ucRoleName })); } catch { /* ignore */ }
      }
    })
  );

  return results;
}
