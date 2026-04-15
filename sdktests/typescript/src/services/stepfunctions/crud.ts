import {
  SFNClient,
  CreateStateMachineCommand,
  DescribeStateMachineCommand,
  ListStateMachinesCommand,
  UpdateStateMachineCommand,
  StartExecutionCommand,
  DescribeExecutionCommand,
  ListExecutionsCommand,
  GetExecutionHistoryCommand,
  CreateActivityCommand,
  DescribeActivityCommand,
  ListActivitiesCommand,
  DeleteActivityCommand,
  DeleteStateMachineCommand,
  TagResourceCommand,
  ListTagsForResourceCommand,
  UntagResourceCommand,
} from '@aws-sdk/client-sfn';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

interface SharedState {
  stateMachineARN: string;
  executionARN: string;
  activityARN: string;
}

export async function runCRUDTests(
  runner: TestRunner,
  client: SFNClient,
  smName: string,
  smARN: string,
  definition: string,
  activityName: string,
  results: TestResult[],
  state: SharedState,
): Promise<void> {
  results.push(await runner.runTest('stepfunctions', 'CreateStateMachine', async () => {
    const resp = await client.send(new CreateStateMachineCommand({
      name: smName,
      definition,
      roleArn: smARN,
    }));
    if (!resp.stateMachineArn) throw new Error('expected stateMachineArn to be defined');
    state.stateMachineARN = resp.stateMachineArn;
  }));

  results.push(await runner.runTest('stepfunctions', 'DescribeStateMachine', async () => {
    const resp = await client.send(new DescribeStateMachineCommand({
      stateMachineArn: state.stateMachineARN,
    }));
    if (!resp.stateMachineArn) throw new Error('expected stateMachineArn to be defined');
  }));

  results.push(await runner.runTest('stepfunctions', 'ListStateMachines', async () => {
    const resp = await client.send(new ListStateMachinesCommand({}));
    if (!resp.stateMachines?.length) throw new Error('expected stateMachines to be non-empty');
  }));

  results.push(await runner.runTest('stepfunctions', 'StartExecution', async () => {
    const resp = await client.send(new StartExecutionCommand({
      stateMachineArn: state.stateMachineARN,
      input: JSON.stringify({ message: 'test' }),
    }));
    if (!resp.executionArn) throw new Error('expected executionArn to be defined');
  }));

  results.push(await runner.runTest('stepfunctions', 'ListExecutions', async () => {
    const resp = await client.send(new ListExecutionsCommand({
      stateMachineArn: state.stateMachineARN,
    }));
    if (!resp.executions?.length) throw new Error('expected executions to be non-empty');
    state.executionARN = resp.executions[0].executionArn ?? '';
  }));

  results.push(await runner.runTest('stepfunctions', 'DescribeExecution', async () => {
    if (!state.executionARN) throw new Error('no execution ARN available');
    const resp = await client.send(new DescribeExecutionCommand({
      executionArn: state.executionARN,
    }));
    if (!resp.status) throw new Error('expected execution status to be defined');
  }));

  results.push(await runner.runTest('stepfunctions', 'GetExecutionHistory', async () => {
    if (!state.executionARN) throw new Error('no execution ARN available');
    const resp = await client.send(new GetExecutionHistoryCommand({
      executionArn: state.executionARN,
    }));
    if (!resp.events?.length) throw new Error('expected events to be non-empty');
  }));

  results.push(await runner.runTest('stepfunctions', 'UpdateStateMachine', async () => {
    await client.send(new UpdateStateMachineCommand({
      stateMachineArn: state.stateMachineARN,
      definition,
    }));
  }));

  results.push(await runner.runTest('stepfunctions', 'CreateActivity', async () => {
    const resp = await client.send(new CreateActivityCommand({ name: activityName }));
    if (!resp.activityArn) throw new Error('expected activityArn to be defined');
    state.activityARN = resp.activityArn;
  }));

  results.push(await runner.runTest('stepfunctions', 'DescribeActivity', async () => {
    const resp = await client.send(new DescribeActivityCommand({
      activityArn: state.activityARN,
    }));
    if (!resp.name) throw new Error('expected activity name to be defined');
  }));

  results.push(await runner.runTest('stepfunctions', 'ListActivities', async () => {
    const resp = await client.send(new ListActivitiesCommand({}));
    if (!resp.activities?.length) throw new Error('expected activities to be non-empty');
  }));

  results.push(await runner.runTest('stepfunctions', 'TagResource', async () => {
    await client.send(new TagResourceCommand({
      resourceArn: state.stateMachineARN,
      tags: [{ key: 'Environment', value: 'test' }],
    }));
  }));

  results.push(await runner.runTest('stepfunctions', 'ListTagsForResource', async () => {
    const resp = await client.send(new ListTagsForResourceCommand({
      resourceArn: state.stateMachineARN,
    }));
    if (!resp.tags?.length) throw new Error('expected tags to be non-empty');
  }));

  results.push(await runner.runTest('stepfunctions', 'UntagResource', async () => {
    await client.send(new UntagResourceCommand({
      resourceArn: state.stateMachineARN,
      tagKeys: ['Environment'],
    }));
  }));

  results.push(await runner.runTest('stepfunctions', 'DeleteActivity', async () => {
    await client.send(new DeleteActivityCommand({
      activityArn: state.activityARN,
    }));
  }));

  results.push(await runner.runTest('stepfunctions', 'DeleteStateMachine', async () => {
    await client.send(new DeleteStateMachineCommand({
      stateMachineArn: state.stateMachineARN,
    }));
  }));
}
