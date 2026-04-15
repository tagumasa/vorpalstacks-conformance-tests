import {
  CreateStageCommand,
  GetStageCommand,
  GetStagesCommand,
  UpdateStageCommand,
  DeleteStageCommand,
} from '@aws-sdk/client-api-gateway';
import type { TestResult } from '../../runner.js';
import { ApiGatewayTestContext } from './context.js';

export async function runStageTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'CreateStage', async () => {
    if (!ctx.apiID || !ctx.deploymentID) throw new Error('API ID or deployment ID not available');
    const resp = await client.send(new CreateStageCommand({
      restApiId: ctx.apiID,
      stageName: 'test',
      deploymentId: ctx.deploymentID,
      description: 'test stage',
    }));
    if (!resp) throw new Error('expected response to be defined');
  }));

  results.push(await runner.runTest('apigateway', 'GetStage', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new GetStageCommand({
      restApiId: ctx.apiID,
      stageName: 'test',
    }));
    if (resp.stageName !== 'test') throw new Error(`stage name mismatch, got ${resp.stageName}`);
  }));

  results.push(await runner.runTest('apigateway', 'GetStages', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new GetStagesCommand({ restApiId: ctx.apiID }));
    if (!resp.item || resp.item.length === 0) throw new Error('expected at least 1 stage');
  }));

  results.push(await runner.runTest('apigateway', 'UpdateStage', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new UpdateStageCommand({
      restApiId: ctx.apiID,
      stageName: 'test',
      patchOperations: [
        { op: 'replace', path: '/description', value: 'updated stage' },
      ],
    }));
    if (resp.description !== 'updated stage') throw new Error(`description not updated, got ${resp.description}`);
  }));

  return results;
}

export async function runDeleteStageTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'DeleteStage', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    await client.send(new DeleteStageCommand({
      restApiId: ctx.apiID,
      stageName: 'test',
    }));
  }));

  return results;
}
