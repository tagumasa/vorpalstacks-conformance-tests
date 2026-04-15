import {
  CreateRequestValidatorCommand,
  GetRequestValidatorCommand,
  UpdateRequestValidatorCommand,
  GetRequestValidatorsCommand,
  DeleteRequestValidatorCommand,
} from '@aws-sdk/client-api-gateway';
import type { TestResult } from '../../runner.js';
import { ApiGatewayTestContext } from './context.js';

export async function runRequestValidatorTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'CreateRequestValidator', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new CreateRequestValidatorCommand({
      restApiId: ctx.apiID,
      name: 'test-validator',
      validateRequestBody: true,
      validateRequestParameters: true,
    }));
    if (!resp.id) throw new Error('expected validator ID to be defined');
    if (!resp.validateRequestBody) throw new Error('validateRequestBody should be true');
    if (!resp.validateRequestParameters) throw new Error('validateRequestParameters should be true');
    ctx.validatorID = resp.id;
  }));

  results.push(await runner.runTest('apigateway', 'GetRequestValidator', async () => {
    if (!ctx.apiID || !ctx.validatorID) throw new Error('API ID or validator ID not available');
    const resp = await client.send(new GetRequestValidatorCommand({
      restApiId: ctx.apiID,
      requestValidatorId: ctx.validatorID,
    }));
    if (resp.name !== 'test-validator') throw new Error(`name mismatch, got ${resp.name}`);
  }));

  results.push(await runner.runTest('apigateway', 'UpdateRequestValidator', async () => {
    if (!ctx.apiID || !ctx.validatorID) throw new Error('API ID or validator ID not available');
    const resp = await client.send(new UpdateRequestValidatorCommand({
      restApiId: ctx.apiID,
      requestValidatorId: ctx.validatorID,
      patchOperations: [
        { op: 'replace', path: '/name', value: 'updated-validator' },
      ],
    }));
    if (resp.name !== 'updated-validator') throw new Error(`name not updated, got ${resp.name}`);
  }));

  results.push(await runner.runTest('apigateway', 'GetRequestValidators', async () => {
    if (!ctx.apiID) throw new Error('API ID not available');
    const resp = await client.send(new GetRequestValidatorsCommand({
      restApiId: ctx.apiID,
      limit: 100,
    }));
    if (!resp.items || resp.items.length === 0) throw new Error('expected at least 1 validator');
  }));

  results.push(await runner.runTest('apigateway', 'DeleteRequestValidator', async () => {
    if (!ctx.apiID || !ctx.validatorID) throw new Error('API ID or validator ID not available');
    await client.send(new DeleteRequestValidatorCommand({
      restApiId: ctx.apiID,
      requestValidatorId: ctx.validatorID,
    }));
  }));

  return results;
}
