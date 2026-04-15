import {
  CreateFunctionCommand,
  DeleteFunctionCommand,
  CreateEventSourceMappingCommand,
  GetEventSourceMappingCommand,
  UpdateEventSourceMappingCommand,
  ListEventSourceMappingsCommand,
  DeleteEventSourceMappingCommand,
} from '@aws-sdk/client-lambda';
import { LambdaTestContext, lambdaTrustPolicy } from './context.js';
import { assertErrorContains, createIAMRole, deleteIAMRole, safeCleanup } from '../../helpers.js';

export async function runEventSourceTests(ctx: LambdaTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, iamClient, ts } = ctx;
  const results: import('../../runner.js').TestResult[] = [];
  const esmFuncName = `EsmFunc-${ts}`;
  const esmRoleName = `EsmRole-${ts}`;
  const esmRole = `arn:aws:iam::000000000000:role/${esmRoleName}`;
  const esmCode = new TextEncoder().encode('exports.handler = async () => { return 1; };');
  const esmEventSourceArn = 'arn:aws:sqs:us-east-1:000000000000:test-queue';

  let setupOk = false;
  await createIAMRole(iamClient, esmRoleName, lambdaTrustPolicy);
  try {
    await client.send(new CreateFunctionCommand({
      FunctionName: esmFuncName, Runtime: 'nodejs22.x', Role: esmRole, Handler: 'index.handler', Code: { ZipFile: esmCode },
    }));
    setupOk = true;
  } catch { /* setup failed */ }
  if (!setupOk) {
    await deleteIAMRole(iamClient, esmRoleName);
    return results;
  }

  let esmUUID = '';

  try {
    results.push(await runner.runTest('lambda', 'CreateEventSourceMapping', async () => {
      const resp = await client.send(new CreateEventSourceMappingCommand({
        FunctionName: esmFuncName,
        EventSourceArn: esmEventSourceArn,
        BatchSize: 10,
        Enabled: true,
      }));
      if (!resp.UUID || resp.UUID === '') throw new Error('UUID to be defined');
      esmUUID = resp.UUID;
    }));

    results.push(await runner.runTest('lambda', 'GetEventSourceMapping', async () => {
      if (!esmUUID) throw new Error('no UUID from CreateEventSourceMapping');
      const resp = await client.send(new GetEventSourceMappingCommand({ UUID: esmUUID }));
      if (!resp.FunctionArn) throw new Error('FunctionArn to be defined');
      if (resp.EventSourceArn !== esmEventSourceArn) throw new Error(`EventSourceArn mismatch, got ${resp.EventSourceArn}`);
    }));

    results.push(await runner.runTest('lambda', 'UpdateEventSourceMapping', async () => {
      if (!esmUUID) throw new Error('no UUID from CreateEventSourceMapping');
      const resp = await client.send(new UpdateEventSourceMappingCommand({
        UUID: esmUUID, BatchSize: 50, Enabled: false,
      }));
      if (resp.BatchSize !== 50) throw new Error(`BatchSize not updated, got ${resp.BatchSize}`);
    }));

    results.push(await runner.runTest('lambda', 'ListEventSourceMappings', async () => {
      const resp = await client.send(new ListEventSourceMappingsCommand({ FunctionName: esmFuncName }));
      if (!resp.EventSourceMappings) throw new Error('event source mappings list to be defined');
      if (resp.EventSourceMappings.length === 0) throw new Error('expected at least 1 event source mapping');
    }));

    results.push(await runner.runTest('lambda', 'DeleteEventSourceMapping', async () => {
      if (!esmUUID) throw new Error('no UUID from CreateEventSourceMapping');
      await client.send(new DeleteEventSourceMappingCommand({ UUID: esmUUID }));
    }));

    results.push(await runner.runTest('lambda', 'GetEventSourceMapping_NonExistent', async () => {
      try {
        await client.send(new GetEventSourceMappingCommand({
          UUID: '00000000-0000-0000-0000-000000000000',
        }));
        throw new Error('expected error for non-existent event source mapping');
      } catch (err) {
        assertErrorContains(err, 'ResourceNotFoundException');
      }
    }));
  } finally {
    await safeCleanup(async () => { await client.send(new DeleteFunctionCommand({ FunctionName: esmFuncName })); });
    await deleteIAMRole(iamClient, esmRoleName);
  }

  return results;
}
