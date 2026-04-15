import {
  CloudWatchLogsClient,
  PutDestinationCommand,
  DescribeDestinationsCommand,
  PutDestinationPolicyCommand,
  DeleteDestinationCommand,
} from '@aws-sdk/client-cloudwatch-logs';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runDestinationTests(
  runner: TestRunner,
  client: CloudWatchLogsClient,
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('logs', 'PutDestination_Basic', async () => {
    const destName = makeUniqueName('TestDest');
    try {
      await client.send(new PutDestinationCommand({
        destinationName: destName,
        roleArn: 'arn:aws:iam::000000000000:role/dest-role',
        targetArn: 'arn:aws:kinesis:us-east-1:000000000000:stream/test-stream',
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteDestinationCommand({ destinationName: destName })));
    }
  }));

  results.push(await runner.runTest('logs', 'DescribeDestinations_Basic', async () => {
    const ddName = makeUniqueName('DescDest');
    try {
      await client.send(new PutDestinationCommand({
        destinationName: ddName,
        roleArn: 'arn:aws:iam::000000000000:role/dd-role',
        targetArn: 'arn:aws:kinesis:us-east-1:000000000000:stream/dd-stream',
      }));
      const resp = await client.send(new DescribeDestinationsCommand({ DestinationNamePrefix: ddName }));
      if (resp.destinations?.length !== 1) throw new Error(`expected 1 destination, got ${resp.destinations?.length}`);
      if (resp.destinations[0].destinationName !== ddName) throw new Error(`name mismatch: got ${resp.destinations[0].destinationName}`);
      if (!resp.destinations[0].arn) throw new Error('ARN is empty');
    } finally {
      await safeCleanup(() => client.send(new DeleteDestinationCommand({ destinationName: ddName })));
    }
  }));

  results.push(await runner.runTest('logs', 'PutDestinationPolicy_Basic', async () => {
    const pdpName = makeUniqueName('PdpDest');
    try {
      await client.send(new PutDestinationCommand({
        destinationName: pdpName,
        roleArn: 'arn:aws:iam::000000000000:role/pdp-role',
        targetArn: 'arn:aws:kinesis:us-east-1:000000000000:stream/pdp-stream',
      }));
      const policy = '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":"*","Action":"logs:PutSubscriptionFilter"}]}';
      await client.send(new PutDestinationPolicyCommand({ destinationName: pdpName, accessPolicy: policy }));
      const resp = await client.send(new DescribeDestinationsCommand({ DestinationNamePrefix: pdpName }));
      if (!resp.destinations?.length) throw new Error('destination not found');
      if (resp.destinations[0].accessPolicy !== policy) throw new Error('access policy mismatch');
    } finally {
      await safeCleanup(() => client.send(new DeleteDestinationCommand({ destinationName: pdpName })));
    }
  }));

  results.push(await runner.runTest('logs', 'PutDestination_UpdateInPlace', async () => {
    const udpName = makeUniqueName('UdpDest');
    try {
      await client.send(new PutDestinationCommand({
        destinationName: udpName,
        roleArn: 'arn:aws:iam::000000000000:role/original-role',
        targetArn: 'arn:aws:kinesis:us-east-1:000000000000:stream/original',
      }));
      await client.send(new PutDestinationCommand({
        destinationName: udpName,
        roleArn: 'arn:aws:iam::000000000000:role/updated-role',
        targetArn: 'arn:aws:kinesis:us-east-1:000000000000:stream/updated',
      }));
      const resp = await client.send(new DescribeDestinationsCommand({ DestinationNamePrefix: udpName }));
      if (!resp.destinations?.length) throw new Error('destination not found');
      if (resp.destinations[0].roleArn !== 'arn:aws:iam::000000000000:role/updated-role') throw new Error(`roleArn not updated: got ${resp.destinations[0].roleArn}`);
      if (resp.destinations[0].targetArn !== 'arn:aws:kinesis:us-east-1:000000000000:stream/updated') throw new Error(`targetArn not updated: got ${resp.destinations[0].targetArn}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteDestinationCommand({ destinationName: udpName })));
    }
  }));

  results.push(await runner.runTest('logs', 'DeleteDestination_Basic', async () => {
    const ddelName = makeUniqueName('DelDest');
    await client.send(new PutDestinationCommand({
      destinationName: ddelName,
      roleArn: 'arn:aws:iam::000000000000:role/ddel-role',
      targetArn: 'arn:aws:kinesis:us-east-1:000000000000:stream/ddel-stream',
    }));
    await client.send(new DeleteDestinationCommand({ destinationName: ddelName }));
    const resp = await client.send(new DescribeDestinationsCommand({ DestinationNamePrefix: ddelName }));
    if (resp.destinations?.length !== 0) throw new Error(`expected 0 destinations after delete, got ${resp.destinations?.length}`);
  }));
}
