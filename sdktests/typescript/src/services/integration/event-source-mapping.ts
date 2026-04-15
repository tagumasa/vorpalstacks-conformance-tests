import type { LambdaClient } from '@aws-sdk/client-lambda';
import type { IAMClient } from '@aws-sdk/client-iam';
import type { CloudWatchLogsClient } from '@aws-sdk/client-cloudwatch-logs';
import type { CloudWatchClient } from '@aws-sdk/client-cloudwatch';
import type { SQSClient } from '@aws-sdk/client-sqs';
import type { SNSClient } from '@aws-sdk/client-sns';
import type { KinesisClient } from '@aws-sdk/client-kinesis';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';
import { accountId, createIamRole, deleteIamRole, createLambdaFunction, createSqsQueue, createSnsTopic, sleep, lambdaTrustPolicy } from './helpers.js';
import { DeleteFunctionCommand, CreateEventSourceMappingCommand, DeleteEventSourceMappingCommand } from '@aws-sdk/client-lambda';
import { SendMessageCommand, ReceiveMessageCommand, DeleteMessageCommand, DeleteQueueCommand } from '@aws-sdk/client-sqs';
import { CreateStreamCommand, DeleteStreamCommand, DescribeStreamCommand, PutRecordCommand } from '@aws-sdk/client-kinesis';

export async function runEsmTests(
  lambdaClient: LambdaClient, iamClient: IAMClient, cwlClient: CloudWatchLogsClient,
  sqsClient: SQSClient, kinesisClient: KinesisClient,
  runner: TestRunner, results: TestResult[], region: string,
): Promise<void> {
  const r = async (testName: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest('integration', testName, fn));

  {
    const fnName = makeUniqueName('integ-esm-sqs-fn');
    const roleName = makeUniqueName('integ-esm-sqs-role');
    const queueName = makeUniqueName('integ-esm-sqs-q');
    await createIamRole(iamClient, roleName, lambdaTrustPolicy);
    try {
      const fnARN = await createLambdaFunction(lambdaClient, iamClient, fnName, roleName, lambdaTrustPolicy);
      try {
        const { queueUrl, queueArn } = await createSqsQueue(sqsClient, queueName);
        try {
          await lambdaClient.send(new CreateEventSourceMappingCommand({
            FunctionName: fnName, EventSourceArn: queueArn, Enabled: true, BatchSize: 10,
          }));
          try {
            await sqsClient.send(new SendMessageCommand({ QueueUrl: queueUrl, MessageBody: JSON.stringify({ test: 'esm-sqs' }) }));
            await sleep(5000);
            await r('ESM_SQS_Lambda', async () => {
              const msgs = await sqsClient.send(new ReceiveMessageCommand({ QueueUrl: queueUrl, MaxNumberOfMessages: 10, WaitTimeSeconds: 1 }));
              if (msgs.Messages && msgs.Messages.length > 0) {
                for (const m of msgs.Messages) {
                  await sqsClient.send(new DeleteMessageCommand({ QueueUrl: queueUrl, ReceiptHandle: m.ReceiptHandle! }));
                }
                throw new Error(`expected ESM to consume all messages, got ${msgs.Messages.length} remaining`);
              }
            });
          } finally {
          }
        } finally {
          await safeCleanup(() => sqsClient.send(new DeleteQueueCommand({ QueueUrl: queueUrl })));
        }
      } finally {
        await safeCleanup(() => lambdaClient.send(new DeleteFunctionCommand({ FunctionName: fnName })));
      }
    } finally {
      await deleteIamRole(iamClient, roleName);
    }
  }

  {
    const fnName = makeUniqueName('integ-esm-kin-fn');
    const roleName = makeUniqueName('integ-esm-kin-role');
    const streamName = makeUniqueName('integ-esm-kin-s');
    await createIamRole(iamClient, roleName, lambdaTrustPolicy);
    try {
      const fnARN = await createLambdaFunction(lambdaClient, iamClient, fnName, roleName, lambdaTrustPolicy);
      try {
        await kinesisClient.send(new CreateStreamCommand({ StreamName: streamName, ShardCount: 1 }));
        try {
          for (let i = 0; i < 30; i++) {
            const resp = await kinesisClient.send(new DescribeStreamCommand({ StreamName: streamName }));
            if (resp.StreamDescription?.StreamStatus === 'ACTIVE') break;
            await sleep(1000);
          }
          const esmResp = await lambdaClient.send(new CreateEventSourceMappingCommand({
            FunctionName: fnName,
            EventSourceArn: `arn:aws:kinesis:${region}:${accountId}:stream/${streamName}`,
            Enabled: true, BatchSize: 100, StartingPosition: 'LATEST',
          }));
          const esmUUID = esmResp.UUID!;
          try {
            await kinesisClient.send(new PutRecordCommand({
              StreamName: streamName, PartitionKey: 'p1',
              Data: Buffer.from(JSON.stringify({ test: 'esm-kinesis' })),
            }));
            await sleep(8000);
            await r('ESM_Kinesis_Lambda', async () => {
              const { verifyLambdaInvoked } = await import('./helpers.js');
              await verifyLambdaInvoked(cwlClient, fnName);
            });
          } finally {
            await safeCleanup(() => lambdaClient.send(new DeleteEventSourceMappingCommand({ UUID: esmUUID })));
          }
        } finally {
          await safeCleanup(() => kinesisClient.send(new DeleteStreamCommand({ StreamName: streamName })));
        }
      } finally {
        await safeCleanup(() => lambdaClient.send(new DeleteFunctionCommand({ FunctionName: fnName })));
      }
    } finally {
      await deleteIamRole(iamClient, roleName);
    }
  }
}
