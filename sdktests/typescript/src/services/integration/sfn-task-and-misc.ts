import type { LambdaClient } from '@aws-sdk/client-lambda';
import type { IAMClient } from '@aws-sdk/client-iam';
import type { CloudWatchLogsClient } from '@aws-sdk/client-cloudwatch-logs';
import type { SFNClient } from '@aws-sdk/client-sfn';
import type { SQSClient } from '@aws-sdk/client-sqs';
import type { SNSClient } from '@aws-sdk/client-sns';
import type { S3Client } from '@aws-sdk/client-s3';
import type { KinesisClient } from '@aws-sdk/client-kinesis';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';
import { accountId, createIamRole, deleteIamRole, createLambdaFunction, verifyLambdaInvoked, createSqsQueue, createSnsTopic, sleep, lambdaTrustPolicy, sfnTrustPolicy } from './helpers.js';
import { DeleteFunctionCommand } from '@aws-sdk/client-lambda';
import { SubscribeCommand, DeleteTopicCommand, PublishCommand } from '@aws-sdk/client-sns';
import { SendMessageCommand, ReceiveMessageCommand, DeleteMessageCommand, DeleteQueueCommand } from '@aws-sdk/client-sqs';
import { CreateStateMachineCommand, DeleteStateMachineCommand, StartExecutionCommand, DescribeExecutionCommand, ListExecutionsCommand } from '@aws-sdk/client-sfn';
import { CreateBucketCommand, DeleteBucketCommand, PutObjectCommand, PutBucketNotificationConfigurationCommand } from '@aws-sdk/client-s3';
import { CreateLogGroupCommand, DeleteLogGroupCommand, CreateLogStreamCommand, PutLogEventsCommand, PutSubscriptionFilterCommand } from '@aws-sdk/client-cloudwatch-logs';
import { CreateStreamCommand, DeleteStreamCommand, DescribeStreamCommand, GetShardIteratorCommand, GetRecordsCommand } from '@aws-sdk/client-kinesis';

export async function runSfnTaskAndMiscTests(
  lambdaClient: LambdaClient, iamClient: IAMClient, cwlClient: CloudWatchLogsClient,
  sfnClient: SFNClient, sqsClient: SQSClient, snsClient: SNSClient,
  s3Client: S3Client, kinesisClient: KinesisClient,
  runner: TestRunner, results: TestResult[], region: string, ts: string,
): Promise<void> {
  const r = async (testName: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest('integration', testName, fn));
  const skip = (testName: string, reason: string) =>
    results.push(runner.skipTest('integration', testName, reason));

  skip('ESM_DynamoDBStreams_ToLambda', 'DynamoDB Streams ESM not yet implemented on server');

  {
    const roleName = makeUniqueName('integ-sfn-lambda-role');
    const lambdaRoleName = makeUniqueName('integ-sfn-lambda-fn-role');
    const smName = makeUniqueName('integ-sfn-lambda');
    const fnName = makeUniqueName('integ-sfn-lambda-fn');
    await createIamRole(iamClient, roleName, sfnTrustPolicy);
    try {
      await createIamRole(iamClient, lambdaRoleName, lambdaTrustPolicy);
      try {
        const fnARN = await createLambdaFunction(lambdaClient, iamClient, fnName, lambdaRoleName, lambdaTrustPolicy);
        try {
          const createResp = await sfnClient.send(new CreateStateMachineCommand({
            name: smName, roleArn: `arn:aws:iam::${accountId}:role/${roleName}`,
            definition: JSON.stringify({ StartAt: 'InvokeLambda', States: { InvokeLambda: { Type: 'Task', Resource: fnARN, End: true } } }),
          }));
          const smARN = createResp.stateMachineArn!;
          try {
            const execResp = await sfnClient.send(new StartExecutionCommand({ stateMachineArn: smARN, input: JSON.stringify({ test: 'sfn-task-lambda' }) }));
            await sleep(3000);
            await r('SFN_Task_Lambda', async () => {
              const descResp = await sfnClient.send(new DescribeExecutionCommand({ executionArn: execResp.executionArn! }));
              if (descResp.status !== 'SUCCEEDED') throw new Error(`expected SUCCEEDED, got ${descResp.status}`);
            });
          } finally { await safeCleanup(() => sfnClient.send(new DeleteStateMachineCommand({ stateMachineArn: smARN }))); }
        } finally { await safeCleanup(() => lambdaClient.send(new DeleteFunctionCommand({ FunctionName: fnName }))); }
      } finally { await deleteIamRole(iamClient, lambdaRoleName); }
    } finally { await deleteIamRole(iamClient, roleName); }
  }

  {
    const roleName = makeUniqueName('integ-sfn-sqs-role');
    const smName = makeUniqueName('integ-sfn-sqs');
    const queueName = makeUniqueName('integ-sfn-sqs-q');
    await createIamRole(iamClient, roleName, sfnTrustPolicy);
    try {
      const { queueUrl } = await createSqsQueue(sqsClient, queueName);
      try {
        const createResp = await sfnClient.send(new CreateStateMachineCommand({
          name: smName, roleArn: `arn:aws:iam::${accountId}:role/${roleName}`,
          definition: JSON.stringify({ StartAt: 'SendMsg', States: { SendMsg: { Type: 'Task', Resource: 'arn:aws:states:::sqs:sendMessage', Parameters: { QueueUrl: queueUrl, MessageBody: { test: 'sfn-to-sqs' } }, End: true } } }),
        }));
        const smARN = createResp.stateMachineArn!;
        try {
          const startResp = await sfnClient.send(new StartExecutionCommand({ stateMachineArn: smARN, input: '{}' }));
          await sleep(3000);
          await r('SFN_Task_SQS', async () => {
            const descResp = await sfnClient.send(new DescribeExecutionCommand({ executionArn: startResp.executionArn! }));
            if (descResp.status !== 'SUCCEEDED') throw new Error(`expected SUCCEEDED, got ${descResp.status}`);
            const msgs = await sqsClient.send(new ReceiveMessageCommand({ QueueUrl: queueUrl, MaxNumberOfMessages: 5, WaitTimeSeconds: 3 }));
            if (!msgs.Messages || msgs.Messages.length === 0) throw new Error('expected SQS message from SFN Task, got 0');
          });
        } finally { await safeCleanup(() => sfnClient.send(new DeleteStateMachineCommand({ stateMachineArn: smARN }))); }
      } finally { await safeCleanup(() => sqsClient.send(new DeleteQueueCommand({ QueueUrl: queueUrl }))); }
    } finally { await deleteIamRole(iamClient, roleName); }
  }

  {
    const roleName = makeUniqueName('integ-sfn-sns-role');
    const smName = makeUniqueName('integ-sfn-sns');
    const topicName = makeUniqueName('integ-sfn-sns-t');
    const queueName = makeUniqueName('integ-sfn-sns-q');
    await createIamRole(iamClient, roleName, sfnTrustPolicy);
    try {
      const topicARN = await createSnsTopic(snsClient, topicName);
      try {
        const { queueUrl, queueArn } = await createSqsQueue(sqsClient, queueName);
        try {
          await snsClient.send(new SubscribeCommand({ TopicArn: topicARN, Protocol: 'sqs', Endpoint: queueArn }));
          const createResp = await sfnClient.send(new CreateStateMachineCommand({
            name: smName, roleArn: `arn:aws:iam::${accountId}:role/${roleName}`,
            definition: JSON.stringify({ StartAt: 'Publish', States: { Publish: { Type: 'Task', Resource: 'arn:aws:states:::sns:publish', Parameters: { TopicArn: topicARN, Message: { test: 'sfn-to-sns' } }, End: true } } }),
          }));
          const smARN = createResp.stateMachineArn!;
          try {
            const startResp = await sfnClient.send(new StartExecutionCommand({ stateMachineArn: smARN, input: '{}' }));
            await sleep(3000);
            await r('SFN_Task_SNS', async () => {
              const descResp = await sfnClient.send(new DescribeExecutionCommand({ executionArn: startResp.executionArn! }));
              if (descResp.status !== 'SUCCEEDED') throw new Error(`expected SUCCEEDED, got ${descResp.status}`);
              const msgs = await sqsClient.send(new ReceiveMessageCommand({ QueueUrl: queueUrl, MaxNumberOfMessages: 5, WaitTimeSeconds: 3 }));
              if (!msgs.Messages || msgs.Messages.length === 0) throw new Error('expected SNS message from SFN Task in queue, got 0');
            });
          } finally { await safeCleanup(() => sfnClient.send(new DeleteStateMachineCommand({ stateMachineArn: smARN }))); }
        } finally { await safeCleanup(() => sqsClient.send(new DeleteQueueCommand({ QueueUrl: queueUrl }))); }
      } finally { await safeCleanup(() => snsClient.send(new DeleteTopicCommand({ TopicArn: topicARN }))); }
    } finally { await deleteIamRole(iamClient, roleName); }
  }

  skip('SFN_Task_DynamoDB', 'DynamoDB task integration not yet implemented on server');

  {
    const bucketName = makeUniqueName('integ-s3-lambda').toLowerCase();
    const fnName = makeUniqueName('integ-s3-lambda-fn');
    const roleName = makeUniqueName('integ-s3-lambda-role');
    await createIamRole(iamClient, roleName, lambdaTrustPolicy);
    try {
      const fnARN = await createLambdaFunction(lambdaClient, iamClient, fnName, roleName, lambdaTrustPolicy);
      try {
        await s3Client.send(new CreateBucketCommand({ Bucket: bucketName }));
        try {
          await s3Client.send(new PutBucketNotificationConfigurationCommand({
            Bucket: bucketName, NotificationConfiguration: { LambdaFunctionConfigurations: [{ LambdaFunctionArn: fnARN, Events: ['s3:ObjectCreated:Put'] }] },
          }));
          await s3Client.send(new PutObjectCommand({ Bucket: bucketName, Key: 'test-key.txt', Body: Buffer.from('test-data') }));
          await sleep(5000);
          await r('S3_Notification_Lambda', async () => { await verifyLambdaInvoked(cwlClient, fnName); });
        } finally { await safeCleanup(() => s3Client.send(new DeleteBucketCommand({ Bucket: bucketName }))); }
      } finally { await safeCleanup(() => lambdaClient.send(new DeleteFunctionCommand({ FunctionName: fnName }))); }
    } finally { await deleteIamRole(iamClient, roleName); }
  }

  {
    const bucketName = makeUniqueName('integ-s3-sqs').toLowerCase();
    const queueName = makeUniqueName('integ-s3-sqs-q');
    const { queueUrl, queueArn } = await createSqsQueue(sqsClient, queueName);
    try {
      await s3Client.send(new CreateBucketCommand({ Bucket: bucketName }));
      try {
        await s3Client.send(new PutBucketNotificationConfigurationCommand({
          Bucket: bucketName, NotificationConfiguration: { QueueConfigurations: [{ QueueArn: queueArn, Events: ['s3:ObjectCreated:Put'] }] },
        }));
        await s3Client.send(new PutObjectCommand({ Bucket: bucketName, Key: 'test-key.txt', Body: Buffer.from('test-data') }));
        await sleep(2000);
        await r('S3_Notification_SQS', async () => {
          const msgs = await sqsClient.send(new ReceiveMessageCommand({ QueueUrl: queueUrl, MaxNumberOfMessages: 5, WaitTimeSeconds: 3 }));
          if (!msgs.Messages || msgs.Messages.length === 0) throw new Error('expected S3 notification in queue, got 0');
        });
      } finally { await safeCleanup(() => s3Client.send(new DeleteBucketCommand({ Bucket: bucketName }))); }
    } finally { await safeCleanup(() => sqsClient.send(new DeleteQueueCommand({ QueueUrl: queueUrl }))); }
  }

  {
    const bucketName = makeUniqueName('integ-s3-sns').toLowerCase();
    const topicName = makeUniqueName('integ-s3-sns-t');
    const queueName = makeUniqueName('integ-s3-sns-q');
    const topicARN = await createSnsTopic(snsClient, topicName);
    try {
      const { queueUrl, queueArn } = await createSqsQueue(sqsClient, queueName);
      try {
        await snsClient.send(new SubscribeCommand({ TopicArn: topicARN, Protocol: 'sqs', Endpoint: queueArn }));
        await s3Client.send(new CreateBucketCommand({ Bucket: bucketName }));
        try {
          await s3Client.send(new PutBucketNotificationConfigurationCommand({
            Bucket: bucketName, NotificationConfiguration: { TopicConfigurations: [{ TopicArn: topicARN, Events: ['s3:ObjectCreated:Put'] }] },
          }));
          await s3Client.send(new PutObjectCommand({ Bucket: bucketName, Key: 'test-key.txt', Body: Buffer.from('test-data') }));
          await sleep(3000);
          await r('S3_Notification_SNS', async () => {
            const msgs = await sqsClient.send(new ReceiveMessageCommand({ QueueUrl: queueUrl, MaxNumberOfMessages: 5, WaitTimeSeconds: 3 }));
            if (!msgs.Messages || msgs.Messages.length === 0) throw new Error('expected S3 notification (via SNS) in queue, got 0');
          });
        } finally { await safeCleanup(() => s3Client.send(new DeleteBucketCommand({ Bucket: bucketName }))); }
      } finally { await safeCleanup(() => sqsClient.send(new DeleteQueueCommand({ QueueUrl: queueUrl }))); }
    } finally { await safeCleanup(() => snsClient.send(new DeleteTopicCommand({ TopicArn: topicARN }))); }
  }

  skip('S3_Notification_Kinesis', 'S3 notification to Kinesis not yet implemented on server');

  {
    const fnName = makeUniqueName('integ-cwl-lambda');
    const roleName = makeUniqueName('integ-cwl-role');
    const logGroupName = `/integ/cwl-lambda/${ts}`;
    await createIamRole(iamClient, roleName, lambdaTrustPolicy);
    try {
      const fnARN = await createLambdaFunction(lambdaClient, iamClient, fnName, roleName, lambdaTrustPolicy);
      try {
        await cwlClient.send(new CreateLogGroupCommand({ logGroupName }));
        try {
          await cwlClient.send(new PutSubscriptionFilterCommand({ logGroupName, filterName: 'integ-lambda-sub', filterPattern: '[...]', destinationArn: fnARN }));
          await cwlClient.send(new CreateLogStreamCommand({ logGroupName, logStreamName: 'test-stream' }));
          await cwlClient.send(new PutLogEventsCommand({ logGroupName, logStreamName: 'test-stream', logEvents: [{ message: 'integration test log message', timestamp: Date.now() }] }));
          await sleep(5000);
          await r('CWLogs_Lambda', async () => { await verifyLambdaInvoked(cwlClient, fnName); });
        } finally { await safeCleanup(() => cwlClient.send(new DeleteLogGroupCommand({ logGroupName }))); }
      } finally { await safeCleanup(() => lambdaClient.send(new DeleteFunctionCommand({ FunctionName: fnName }))); }
    } finally { await deleteIamRole(iamClient, roleName); }
  }

  {
    const streamName = makeUniqueName('integ-cwl-kin');
    const logGroupName = `/integ/cwl-kinesis/${ts}`;
    await kinesisClient.send(new CreateStreamCommand({ StreamName: streamName, ShardCount: 1 }));
    try {
      for (let i = 0; i < 30; i++) {
        const resp = await kinesisClient.send(new DescribeStreamCommand({ StreamName: streamName }));
        if (resp.StreamDescription?.StreamStatus === 'ACTIVE') break;
        await sleep(1000);
      }
      const streamARN = `arn:aws:kinesis:${region}:${accountId}:stream/${streamName}`;
      await cwlClient.send(new CreateLogGroupCommand({ logGroupName }));
      try {
        await cwlClient.send(new CreateLogStreamCommand({ logGroupName, logStreamName: 'test-stream' }));
        await cwlClient.send(new PutSubscriptionFilterCommand({ logGroupName, filterName: 'integ-kinesis-sub', filterPattern: '[...]', destinationArn: streamARN }));
        await cwlClient.send(new PutLogEventsCommand({ logGroupName, logStreamName: 'test-stream', logEvents: [{ message: 'kinesis subscription test', timestamp: Date.now() }] }));
        await sleep(3000);
        await r('CWLogs_Kinesis', async () => {
          const streamDesc = await kinesisClient.send(new DescribeStreamCommand({ StreamName: streamName }));
          if (!streamDesc.StreamDescription?.Shards || streamDesc.StreamDescription.Shards.length === 0) throw new Error('no shards in stream');
          const shardID = streamDesc.StreamDescription.Shards[0].ShardId!;
          const iterResp = await kinesisClient.send(new GetShardIteratorCommand({ StreamName: streamName, ShardId: shardID, ShardIteratorType: 'TRIM_HORIZON' }));
          const records = await kinesisClient.send(new GetRecordsCommand({ ShardIterator: iterResp.ShardIterator }));
          if (!records.Records || records.Records.length === 0) throw new Error('expected records from CW Logs subscription, got 0');
          const data = Buffer.from(records.Records[0].Data!).toString('utf-8');
          if (!data.includes('awslogs')) throw new Error(`expected awslogs envelope, got: ${data}`);
        });
      } finally { await safeCleanup(() => cwlClient.send(new DeleteLogGroupCommand({ logGroupName }))); }
    } finally { await safeCleanup(() => kinesisClient.send(new DeleteStreamCommand({ StreamName: streamName }))); }
  }

  {
    const topicName = makeUniqueName('integ-sns-sqs-t');
    const queueName = makeUniqueName('integ-sns-sqs-q');
    const topicARN = await createSnsTopic(snsClient, topicName);
    try {
      const { queueUrl, queueArn } = await createSqsQueue(sqsClient, queueName);
      try {
        await snsClient.send(new SubscribeCommand({ TopicArn: topicARN, Protocol: 'sqs', Endpoint: queueArn }));
        await snsClient.send(new PublishCommand({ TopicArn: topicARN, Message: JSON.stringify({ test: 'sns-to-sqs' }), MessageAttributes: { TestType: { DataType: 'String', StringValue: 'integration' } } }));
        await sleep(2000);
        await r('SNS_SQS', async () => {
          const msgs = await sqsClient.send(new ReceiveMessageCommand({ QueueUrl: queueUrl, MaxNumberOfMessages: 5, WaitTimeSeconds: 3 }));
          if (!msgs.Messages || msgs.Messages.length === 0) throw new Error('expected message from SNS in queue, got 0');
        });
      } finally { await safeCleanup(() => sqsClient.send(new DeleteQueueCommand({ QueueUrl: queueUrl }))); }
    } finally { await safeCleanup(() => snsClient.send(new DeleteTopicCommand({ TopicArn: topicARN }))); }
  }

  {
    const topicName = makeUniqueName('integ-sns-lambda-t');
    const fnName = makeUniqueName('integ-sns-lambda-fn');
    const roleName = makeUniqueName('integ-sns-lambda-role');
    await createIamRole(iamClient, roleName, lambdaTrustPolicy);
    try {
      const fnARN = await createLambdaFunction(lambdaClient, iamClient, fnName, roleName, lambdaTrustPolicy);
      try {
        const topicARN = await createSnsTopic(snsClient, topicName);
        try {
          await snsClient.send(new SubscribeCommand({ TopicArn: topicARN, Protocol: 'lambda', Endpoint: fnARN }));
          await snsClient.send(new PublishCommand({ TopicArn: topicARN, Message: JSON.stringify({ test: 'sns-to-lambda' }) }));
          await sleep(5000);
          await r('SNS_Lambda', async () => { await verifyLambdaInvoked(cwlClient, fnName); });
        } finally { await safeCleanup(() => snsClient.send(new DeleteTopicCommand({ TopicArn: topicARN }))); }
      } finally { await safeCleanup(() => lambdaClient.send(new DeleteFunctionCommand({ FunctionName: fnName }))); }
    } finally { await deleteIamRole(iamClient, roleName); }
  }
}
