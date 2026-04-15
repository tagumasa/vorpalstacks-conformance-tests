import {
  EventBridgeClient,
  CreateConnectionCommand,
  DeleteConnectionCommand,
  CreateApiDestinationCommand,
  DeleteApiDestinationCommand,
  DescribeApiDestinationCommand,
  ListApiDestinationsCommand,
  UpdateApiDestinationCommand,
} from '@aws-sdk/client-eventbridge';
import type { TestRunner, TestResult } from '../../runner.js';
import type { ServiceContext } from '../../runner.js';
import { makeUniqueName, safeCleanup, assertThrows } from '../../helpers.js';

export async function runApiDestinationTests(
  runner: TestRunner,
  client: EventBridgeClient,
  ctx: ServiceContext,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const region = ctx.region;

  results.push(await runner.runTest('eventbridge', 'CreateApiDestination', async () => {
    const cadName = makeUniqueName('CadDest');
    const connName = makeUniqueName('CadConn');
    try {
      await client.send(new CreateConnectionCommand({
        Name: connName, AuthorizationType: 'BASIC',
        AuthParameters: { BasicAuthParameters: { Username: 'u', Password: 'p' } },
      }));
      const resp = await client.send(new CreateApiDestinationCommand({
        Name: cadName,
        ConnectionArn: `arn:aws:events:${region}:000000000000:connection/${connName}`,
        HttpMethod: 'POST', InvocationEndpoint: 'https://example.com/webhook', Description: 'test api destination',
      }));
      if (!resp.ApiDestinationArn) throw new Error('expected ApiDestinationArn to be defined');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteApiDestinationCommand({ Name: cadName })); });
      await safeCleanup(async () => { await client.send(new DeleteConnectionCommand({ Name: connName })); });
    }
  }));

  results.push(await runner.runTest('eventbridge', 'DescribeApiDestination', async () => {
    const dadName = makeUniqueName('DadDest');
    const dadConn = makeUniqueName('DadConn');
    try {
      await client.send(new CreateConnectionCommand({
        Name: dadConn, AuthorizationType: 'BASIC',
        AuthParameters: { BasicAuthParameters: { Username: 'u', Password: 'p' } },
      }));
      const connARN = `arn:aws:events:${region}:000000000000:connection/${dadConn}`;
      await client.send(new CreateApiDestinationCommand({
        Name: dadName, ConnectionArn: connARN, HttpMethod: 'POST',
        InvocationEndpoint: 'https://example.com/webhook', Description: 'test api destination for describe',
      }));
      const resp = await client.send(new DescribeApiDestinationCommand({ Name: dadName }));
      if (resp.Name !== dadName) throw new Error(`name mismatch, got ${resp.Name}`);
      if (resp.ConnectionArn !== connARN) throw new Error(`connection ARN mismatch, got ${resp.ConnectionArn}`);
      if (resp.HttpMethod !== 'POST') throw new Error(`http method mismatch, got ${resp.HttpMethod}`);
      if (resp.InvocationEndpoint !== 'https://example.com/webhook') throw new Error(`invocation endpoint mismatch, got ${resp.InvocationEndpoint}`);
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteApiDestinationCommand({ Name: dadName })); });
      await safeCleanup(async () => { await client.send(new DeleteConnectionCommand({ Name: dadConn })); });
    }
  }));

  results.push(await runner.runTest('eventbridge', 'DescribeApiDestination_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new DescribeApiDestinationCommand({ Name: 'nonexistent-apidest-xyz-12345' }));
    }, 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest('eventbridge', 'DeleteApiDestination_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new DeleteApiDestinationCommand({ Name: 'nonexistent-apidest-xyz-12345' }));
    }, 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest('eventbridge', 'ListApiDestinations', async () => {
    const resp = await client.send(new ListApiDestinationsCommand({}));
    if (!resp.ApiDestinations) throw new Error('expected ApiDestinations to be defined');
  }));

  results.push(await runner.runTest('eventbridge', 'UpdateApiDestination', async () => {
    const uadName = makeUniqueName('UadDest');
    const uadConn = makeUniqueName('UadConn');
    try {
      await client.send(new CreateConnectionCommand({
        Name: uadConn, AuthorizationType: 'BASIC',
        AuthParameters: { BasicAuthParameters: { Username: 'u', Password: 'p' } },
      }));
      const connARN = `arn:aws:events:${region}:000000000000:connection/${uadConn}`;
      await client.send(new CreateApiDestinationCommand({
        Name: uadName, ConnectionArn: connARN, HttpMethod: 'POST', InvocationEndpoint: 'https://example.com/original',
      }));
      const resp = await client.send(new UpdateApiDestinationCommand({
        Name: uadName, Description: 'updated description', InvocationEndpoint: 'https://example.com/updated',
      }));
      if (!resp.ApiDestinationArn) throw new Error('expected ApiDestinationArn to be defined');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteApiDestinationCommand({ Name: uadName })); });
      await safeCleanup(async () => { await client.send(new DeleteConnectionCommand({ Name: uadConn })); });
    }
  }));

  return results;
}
