import {
  SFNClient,
  CreateStateMachineCommand,
  UpdateStateMachineCommand,
  DescribeStateMachineCommand,
  StartExecutionCommand,
  DescribeExecutionCommand,
  ListStateMachinesCommand,
  ValidateStateMachineDefinitionCommand,
} from '@aws-sdk/client-sfn';
import { IAMClient, CreateRoleCommand, DeleteRoleCommand } from '@aws-sdk/client-iam';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

const TRUST_POLICY = JSON.stringify({
  Version: '2012-10-17',
  Statement: [{ Effect: 'Allow', Principal: { Service: 'states.amazonaws.com' }, Action: 'sts:AssumeRole' }],
});

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export interface VerifySMState {
  name: string;
  arn: string;
}

export async function runDefinitionTests(
  runner: TestRunner,
  client: SFNClient,
  iamClient: IAMClient,
  ctx: { endpoint: string; region: string },
  results: TestResult[],
  executionARN: string,
  verify: VerifySMState,
): Promise<void> {
  results.push(await runner.runTest('stepfunctions', 'UpdateStateMachine_VerifyDefinition', async () => {
    const smName = makeUniqueName('VerifySM');
    const roleName = makeUniqueName('VerifyRole');
    try {
      await iamClient.send(new CreateRoleCommand({
        RoleName: roleName,
        AssumeRolePolicyDocument: TRUST_POLICY,
      }));
      const def1 = '{"Comment":"v1","StartAt":"A","States":{"A":{"Type":"Pass","End":true}}}';
      const createResp = await client.send(new CreateStateMachineCommand({
        name: smName,
        definition: def1,
        roleArn: `arn:aws:iam::000000000000:role/${roleName}`,
      }));
      if (!createResp.stateMachineArn) throw new Error('expected stateMachineArn to be defined');
      verify.arn = createResp.stateMachineArn;
      verify.name = smName;

      const def2 = '{"Comment":"v2","StartAt":"B","States":{"B":{"Type":"Pass","Result":"hello","End":true}}}';
      await client.send(new UpdateStateMachineCommand({
        stateMachineArn: verify.arn,
        definition: def2,
      }));
      const descResp = await client.send(new DescribeStateMachineCommand({
        stateMachineArn: verify.arn,
      }));
      if (descResp.definition !== def2) throw new Error(`definition not updated: got ${descResp.definition}, want ${def2}`);
    } finally {
      await safeCleanup(() => iamClient.send(new DeleteRoleCommand({ RoleName: roleName })));
    }
  }));

  results.push(await runner.runTest('stepfunctions', 'Execution_PassStateOutput', async () => {
    if (!verify.arn) throw new Error('state machine ARN not available');
    const startResp = await client.send(new StartExecutionCommand({
      stateMachineArn: verify.arn,
      input: '{"value":42}',
    }));
    if (!startResp.executionArn) throw new Error('expected executionArn to be defined');

    let attempts = 0;
    do {
      await sleep(500);
      const descResp = await client.send(new DescribeExecutionCommand({
        executionArn: startResp.executionArn,
      }));
      if (descResp.status === 'SUCCEEDED') {
        if (!descResp.output) throw new Error('expected execution output to be defined');
        if (descResp.output !== '"hello"') throw new Error(`expected output "\"hello\"", got ${descResp.output}`);
        return;
      }
      if (descResp.status === 'FAILED' || descResp.status === 'ABORTED') {
        throw new Error(`execution failed with status ${descResp.status}`);
      }
      attempts++;
    } while (attempts < 10);
    throw new Error('execution did not complete in time');
  }));

  results.push(await runner.runTest('stepfunctions', 'ListStateMachines_ContainsCreated', async () => {
    if (!verify.arn) throw new Error('state machine ARN not available');
    const resp = await client.send(new ListStateMachinesCommand({}));
    const found = resp.stateMachines?.some(
      (sm) => sm.stateMachineArn === verify.arn && sm.name === verify.name,
    );
    if (!found) throw new Error('state machine not found in list');
  }));

  results.push(await runner.runTest('stepfunctions', 'ValidateStateMachineDefinition_Valid', async () => {
    const resp = await client.send(new ValidateStateMachineDefinitionCommand({
      definition: '{"StartAt":"A","States":{"A":{"Type":"Pass","End":true}}}',
    }));
    if (resp.result !== 'OK') throw new Error(`expected OK, got ${resp.result}`);
  }));

  results.push(await runner.runTest('stepfunctions', 'ValidateStateMachineDefinition_Invalid', async () => {
    const resp = await client.send(new ValidateStateMachineDefinitionCommand({
      definition: '{"StartAt":"Missing","States":{}}',
    }));
    if (resp.result === 'OK') throw new Error('expected FAIL for invalid definition');
  }));

  results.push(await runner.runTest('stepfunctions', 'GetStateMachine', async () => {
    if (!verify.arn) throw new Error('state machine ARN not available');
    const formBody = new URLSearchParams();
    formBody.set('stateMachineArn', verify.arn);
    const resp = await fetch(ctx.endpoint, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
        'X-Amz-Target': 'AWSStepFunctions.GetStateMachine',
      },
      body: formBody.toString(),
    });
    if (resp.status !== 200) throw new Error(`GetStateMachine returned status ${resp.status}`);
    const result = (await resp.json()) as Record<string, unknown>;
    if (result.stateMachineArn !== verify.arn) throw new Error('state machine ARN mismatch');
  }));

  results.push(await runner.runTest('stepfunctions', 'DescribeStateMachineForExecution', async () => {
    if (!executionARN) throw new Error('no execution ARN available');
    const descExecResp = await client.send(new DescribeExecutionCommand({
      executionArn: executionARN,
    }));
    if (!descExecResp.stateMachineArn) throw new Error('expected stateMachineArn to be defined');
    if (descExecResp.status !== 'SUCCEEDED' && descExecResp.status !== 'RUNNING') {
      throw new Error(`execution not in suitable state: ${descExecResp.status}`);
    }
  }));

  results.push(await runner.runTest('stepfunctions', 'StartSyncExecution', async () => {
    if (!verify.arn) throw new Error('state machine ARN not available');
    const resp = await fetch(ctx.endpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-amz-json-1.0', 'X-Amz-Target': 'AWSStepFunctions.StartSyncExecution' },
      body: JSON.stringify({ stateMachineArn: verify.arn, input: '{"sync":true}' }),
    });
    const result = (await resp.json()) as Record<string, unknown>;
    if (resp.status !== 200) throw new Error(`status ${resp.status}: ${JSON.stringify(result)}`);
    if (result.status !== 'SUCCEEDED') throw new Error(`expected SUCCEEDED, got ${result.status}`);
  }));

  results.push(await runner.runTest('stepfunctions', 'TestState_Pass', async () => {
    const def = '{"StartAt":"TestPass","States":{"TestPass":{"Type":"Pass","Result":{"hello":"world"},"End":true}}}';
    const resp = await fetch(ctx.endpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-amz-json-1.0', 'X-Amz-Target': 'AWSStepFunctions.TestState' },
      body: JSON.stringify({ definition: def, stateName: 'TestPass', input: '{}' }),
    });
    const result = (await resp.json()) as Record<string, unknown>;
    if (resp.status !== 200) throw new Error(`status ${resp.status}: ${JSON.stringify(result)}`);
    if (result.status !== 'SUCCEEDED') throw new Error(`expected SUCCEEDED, got ${result.status}`);
    if (!('output' in result)) throw new Error('expected output to be present');
  }));
}
