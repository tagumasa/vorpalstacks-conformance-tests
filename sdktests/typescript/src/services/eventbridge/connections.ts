import {
  EventBridgeClient,
  CreateConnectionCommand,
  DeleteConnectionCommand,
  DescribeConnectionCommand,
  ListConnectionsCommand,
  UpdateConnectionCommand,
} from '@aws-sdk/client-eventbridge';
import type { TestRunner, TestResult } from '../../runner.js';
import type { ServiceContext } from '../../runner.js';
import { makeUniqueName, safeCleanup, assertThrows } from '../../helpers.js';

export async function runConnectionTests(
  runner: TestRunner,
  client: EventBridgeClient,
  ctx: ServiceContext,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('eventbridge', 'CreateConnection', async () => {
    const ccName = makeUniqueName('CcConn');
    try {
      const resp = await client.send(new CreateConnectionCommand({
        Name: ccName, AuthorizationType: 'BASIC',
        AuthParameters: { BasicAuthParameters: { Username: 'testuser', Password: 'testpass' } },
      }));
      if (!resp.ConnectionArn) throw new Error('expected ConnectionArn to be defined');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteConnectionCommand({ Name: ccName })); });
    }
  }));

  results.push(await runner.runTest('eventbridge', 'DescribeConnection', async () => {
    const dcName = makeUniqueName('DcConn');
    try {
      await client.send(new CreateConnectionCommand({
        Name: dcName, AuthorizationType: 'BASIC',
        AuthParameters: { BasicAuthParameters: { Username: 'testuser', Password: 'testpass' } },
      }));
      const resp = await client.send(new DescribeConnectionCommand({ Name: dcName }));
      if (resp.Name !== dcName) throw new Error(`connection name mismatch, got ${resp.Name}`);
      if (!resp.ConnectionArn) throw new Error('expected ConnectionArn to be defined');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteConnectionCommand({ Name: dcName })); });
    }
  }));

  results.push(await runner.runTest('eventbridge', 'DescribeConnection_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new DescribeConnectionCommand({ Name: 'nonexistent-conn-xyz-12345' }));
    }, 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest('eventbridge', 'DeleteConnection_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new DeleteConnectionCommand({ Name: 'nonexistent-conn-xyz-12345' }));
    }, 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest('eventbridge', 'ListConnections', async () => {
    const resp = await client.send(new ListConnectionsCommand({}));
    if (!resp.Connections) throw new Error('expected Connections to be defined');
  }));

  results.push(await runner.runTest('eventbridge', 'UpdateConnection', async () => {
    const ucName = makeUniqueName('UcConn');
    try {
      await client.send(new CreateConnectionCommand({
        Name: ucName, AuthorizationType: 'API_KEY',
        AuthParameters: { ApiKeyAuthParameters: { ApiKeyName: 'key', ApiKeyValue: 'value' } },
      }));
      const resp = await client.send(new UpdateConnectionCommand({ Name: ucName, Description: 'updated connection description' }));
      if (!resp.ConnectionArn) throw new Error('expected ConnectionArn to be defined');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteConnectionCommand({ Name: ucName })); });
    }
  }));

  return results;
}
