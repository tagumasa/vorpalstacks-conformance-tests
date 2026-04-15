import {
  CreateDeploymentCommand,
  GetDeploymentCommand,
  UpdateDeploymentCommand,
  GetDeploymentsCommand,
  DeleteDeploymentCommand,
} from '@aws-sdk/client-api-gateway';
import type { TestResult } from '../../runner.js';
import { ApiGatewayTestContext } from './context.js';

export async function runDeploymentTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'CreateDeployment', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new CreateDeploymentCommand({
      restApiId: ctx.apiID,
      description: 'test deployment',
    }));
    if (!resp.id) throw new Error('expected deployment ID to be defined');
    ctx.deploymentID = resp.id;
  }));

  results.push(await runner.runTest('apigateway', 'GetDeployment', async () => {
    if (!ctx.apiID || !ctx.deploymentID) throw new Error('API ID or deployment ID not available');
    const resp = await client.send(new GetDeploymentCommand({
      restApiId: ctx.apiID,
      deploymentId: ctx.deploymentID,
    }));
    if (resp.id !== ctx.deploymentID) throw new Error(`deployment ID mismatch, got ${resp.id}`);
    if (!resp.createdDate) throw new Error('expected createdDate to be defined');
  }));

  results.push(await runner.runTest('apigateway', 'UpdateDeployment', async () => {
    if (!ctx.apiID || !ctx.deploymentID) throw new Error('API ID or deployment ID not available');
    const resp = await client.send(new UpdateDeploymentCommand({
      restApiId: ctx.apiID,
      deploymentId: ctx.deploymentID,
      patchOperations: [
        { op: 'replace', path: '/description', value: 'updated deployment' },
      ],
    }));
    if (resp.description !== 'updated deployment') throw new Error(`description not updated, got ${resp.description}`);
  }));

  results.push(await runner.runTest('apigateway', 'GetDeployments', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new GetDeploymentsCommand({ restApiId: ctx.apiID }));
    if (!resp.items || resp.items.length === 0) throw new Error('expected at least 1 deployment');
  }));

  return results;
}

export async function runDeleteDeploymentTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'DeleteDeployment', async () => {
    if (!ctx.apiID || !ctx.deploymentID) throw new Error('API ID or deployment ID not available');
    await client.send(new DeleteDeploymentCommand({
      restApiId: ctx.apiID,
      deploymentId: ctx.deploymentID,
    }));
  }));

  return results;
}
