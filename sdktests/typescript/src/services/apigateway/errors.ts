import {
  GetRestApiCommand,
  DeleteRestApiCommand,
} from '@aws-sdk/client-api-gateway';
import type { TestResult } from '../../runner.js';
import { ApiGatewayTestContext } from './context.js';
import { assertErrorContains } from '../../helpers.js';

export async function runErrorTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'GetRestApi_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new GetRestApiCommand({ restApiId: 'nonexistent_xyz' }));
    } catch (e) {
      err = e;
    }
    if (!err) throw new Error('expected error for non-existent API');
    assertErrorContains(err, 'NotFoundException');
  }));

  results.push(await runner.runTest('apigateway', 'DeleteRestApi_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new DeleteRestApiCommand({ restApiId: 'nonexistent_xyz' }));
    } catch (e) {
      err = e;
    }
    if (!err) throw new Error('expected error for non-existent API');
    assertErrorContains(err, 'NotFoundException');
  }));

  return results;
}
