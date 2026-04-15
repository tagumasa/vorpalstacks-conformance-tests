import {
  CreateFunctionCommand,
  DeleteFunctionCommand,
  PublishVersionCommand,
  PutProvisionedConcurrencyConfigCommand,
  GetProvisionedConcurrencyConfigCommand,
  ListProvisionedConcurrencyConfigsCommand,
  DeleteProvisionedConcurrencyConfigCommand,
} from '@aws-sdk/client-lambda';
import { LambdaTestContext, lambdaTrustPolicy } from './context.js';
import { assertErrorContains, createIAMRole, deleteIAMRole, safeCleanup } from '../../helpers.js';

export async function runProvisionedConcurrencyTests(ctx: LambdaTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, iamClient, ts } = ctx;
  const results: import('../../runner.js').TestResult[] = [];
  const pcFuncName = `PcFunc-${ts}`;
  const pcRoleName = `PcRole-${ts}`;
  const pcRole = `arn:aws:iam::000000000000:role/${pcRoleName}`;
  const pcCode = new TextEncoder().encode('exports.handler = async () => { return 1; };');

  await createIAMRole(iamClient, pcRoleName, lambdaTrustPolicy);
  let funcCreated = false;
  let pcVersion = '';

  try {
    await client.send(new CreateFunctionCommand({
      FunctionName: pcFuncName, Runtime: 'nodejs22.x', Role: pcRole, Handler: 'index.handler', Code: { ZipFile: pcCode },
    }));
    funcCreated = true;

    const publishResp = await client.send(new PublishVersionCommand({ FunctionName: pcFuncName }));
    if (!publishResp.Version) throw new Error('version to be defined');
    pcVersion = publishResp.Version;

    results.push(await runner.runTest('lambda', 'PutProvisionedConcurrencyConfig', async () => {
      const resp = await client.send(new PutProvisionedConcurrencyConfigCommand({
        FunctionName: pcFuncName,
        Qualifier: pcVersion,
        ProvisionedConcurrentExecutions: 5,
      }));
      if (resp.AllocatedProvisionedConcurrentExecutions === undefined) throw new Error('AllocatedProvisionedConcurrentExecutions to be defined');
    }));

    results.push(await runner.runTest('lambda', 'GetProvisionedConcurrencyConfig', async () => {
      const resp = await client.send(new GetProvisionedConcurrencyConfigCommand({
        FunctionName: pcFuncName, Qualifier: pcVersion,
      }));
      if (!resp.Status) throw new Error('Status is empty');
    }));

    results.push(await runner.runTest('lambda', 'ListProvisionedConcurrencyConfigs', async () => {
      const resp = await client.send(new ListProvisionedConcurrencyConfigsCommand({
        FunctionName: pcFuncName,
      }));
      if (!resp.ProvisionedConcurrencyConfigs) throw new Error('configs list to be defined');
      if (resp.ProvisionedConcurrencyConfigs.length === 0) throw new Error('expected at least 1 config');
    }));

    results.push(await runner.runTest('lambda', 'DeleteProvisionedConcurrencyConfig', async () => {
      await client.send(new DeleteProvisionedConcurrencyConfigCommand({
        FunctionName: pcFuncName, Qualifier: pcVersion,
      }));
    }));

    results.push(await runner.runTest('lambda', 'GetProvisionedConcurrencyConfig_NonExistent', async () => {
      try {
        await client.send(new GetProvisionedConcurrencyConfigCommand({
          FunctionName: pcFuncName, Qualifier: pcVersion,
        }));
        throw new Error('expected error for deleted provisioned concurrency config');
      } catch (err) {
        assertErrorContains(err, 'ResourceNotFoundException');
      }
    }));
  } finally {
    if (funcCreated) {
      await safeCleanup(async () => { await client.send(new DeleteFunctionCommand({ FunctionName: pcFuncName })); });
    }
    await deleteIAMRole(iamClient, pcRoleName);
  }

  return results;
}
