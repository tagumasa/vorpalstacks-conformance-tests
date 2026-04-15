import {
  LambdaClient, DeleteFunctionCommand,
} from '@aws-sdk/client-lambda';
import {
  IAMClient,
} from '@aws-sdk/client-iam';
import {
  EventBridgeClient, CreateEventBusCommand, DeleteEventBusCommand,
  PutRuleCommand, DeleteRuleCommand, PutTargetsCommand, RemoveTargetsCommand, PutEventsCommand,
} from '@aws-sdk/client-eventbridge';
import {
  CloudWatchLogsClient,
} from '@aws-sdk/client-cloudwatch-logs';
import {
  SFNClient, CreateStateMachineCommand, DeleteStateMachineCommand, ListExecutionsCommand,
} from '@aws-sdk/client-sfn';
import {
  SQSClient, DeleteQueueCommand, ReceiveMessageCommand,
} from '@aws-sdk/client-sqs';
import {
  SNSClient, DeleteTopicCommand, SubscribeCommand,
} from '@aws-sdk/client-sns';
import {
  KinesisClient, CreateStreamCommand, DeleteStreamCommand, DescribeStreamCommand,
  GetShardIteratorCommand, GetRecordsCommand,
} from '@aws-sdk/client-kinesis';
import { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';
import { accountId, createIamRole, deleteIamRole, createLambdaFunction, verifyLambdaInvoked, createSqsQueue, createSnsTopic, sleep, lambdaTrustPolicy, sfnTrustPolicy } from './helpers.js';

export async function runEventBridgeTests(
  lambdaClient: LambdaClient, iamClient: IAMClient, ebClient: EventBridgeClient,
  cwlClient: CloudWatchLogsClient, sfnClient: SFNClient, sqsClient: SQSClient,
  snsClient: SNSClient, kinesisClient: KinesisClient,
  runner: TestRunner, results: TestResult[], region: string,
): Promise<void> {
  const r = async (testName: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest('integration', testName, fn));

  {
    const fnName = makeUniqueName('integ-eb-lambda');
    const roleName = makeUniqueName('integ-eb-lambda-role');
    const busName = makeUniqueName('integ-eb-bus');
    const ruleName = makeUniqueName('integ-eb-rule');
    await createIamRole(iamClient, roleName, lambdaTrustPolicy);
    try {
      await ebClient.send(new CreateEventBusCommand({ Name: busName }));
      try {
        await ebClient.send(new PutRuleCommand({ Name: ruleName, EventBusName: busName }));
        try {
          const fnARN = await createLambdaFunction(lambdaClient, iamClient, fnName, roleName, lambdaTrustPolicy);
          try {
            await ebClient.send(new PutTargetsCommand({
              Rule: ruleName, EventBusName: busName,
              Targets: [{ Id: 't1', Arn: fnARN }],
            }));
            try {
              await ebClient.send(new PutEventsCommand({
                Entries: [{
                  EventBusName: busName,
                  Source: 'com.integration.test',
                  DetailType: 'IntegrationTest',
                  Detail: JSON.stringify({ test: 'eventbridge-lambda' }),
                }],
              }));
              await sleep(5000);
              await r('EventBridge_Lambda', async () => {
                await verifyLambdaInvoked(cwlClient, fnName);
              });
            } finally {
              await safeCleanup(() => ebClient.send(new RemoveTargetsCommand({ Rule: ruleName, EventBusName: busName, Ids: ['t1'] })));
            }
          } finally {
            await safeCleanup(() => lambdaClient.send(new DeleteFunctionCommand({ FunctionName: fnName })));
          }
        } finally {
          await safeCleanup(() => ebClient.send(new DeleteRuleCommand({ Name: ruleName, EventBusName: busName })));
        }
      } finally {
        await safeCleanup(() => ebClient.send(new DeleteEventBusCommand({ Name: busName })));
      }
    } finally {
      await deleteIamRole(iamClient, roleName);
    }
  }

  {
    const roleName = makeUniqueName('integ-eb-sfn-role');
    const busName = makeUniqueName('integ-eb-sfn-bus');
    const ruleName = makeUniqueName('integ-eb-sfn-rule');
    const smName = makeUniqueName('integ-eb-sfn-sm');
    await createIamRole(iamClient, roleName, sfnTrustPolicy);
    try {
      await ebClient.send(new CreateEventBusCommand({ Name: busName }));
      try {
        await ebClient.send(new PutRuleCommand({ Name: ruleName, EventBusName: busName }));
        try {
          const smResp = await sfnClient.send(new CreateStateMachineCommand({
            name: smName,
            roleArn: `arn:aws:iam::${accountId}:role/${roleName}`,
            definition: JSON.stringify({ StartAt: 'Pass', States: { Pass: { Type: 'Pass', End: true } } }),
          }));
          const smARN = smResp.stateMachineArn!;
          try {
            await ebClient.send(new PutTargetsCommand({
              Rule: ruleName, EventBusName: busName,
              Targets: [{ Id: 't1', Arn: smARN }],
            }));
            try {
              await ebClient.send(new PutEventsCommand({
                Entries: [{
                  EventBusName: busName,
                  Source: 'com.integration.test',
                  DetailType: 'SFNTrigger',
                  Detail: JSON.stringify({ test: 'eb-to-sfn' }),
                }],
              }));
              await sleep(3000);
              await r('EventBridge_StepFunctions', async () => {
                const resp = await sfnClient.send(new ListExecutionsCommand({ stateMachineArn: smARN }));
                if (!resp.executions || resp.executions.length === 0) {
                  throw new Error('expected at least 1 execution, got 0');
                }
              });
            } finally {
              await safeCleanup(() => ebClient.send(new RemoveTargetsCommand({ Rule: ruleName, EventBusName: busName, Ids: ['t1'] })));
            }
          } finally {
            await safeCleanup(() => sfnClient.send(new DeleteStateMachineCommand({ stateMachineArn: smARN })));
          }
        } finally {
          await safeCleanup(() => ebClient.send(new DeleteRuleCommand({ Name: ruleName, EventBusName: busName })));
        }
      } finally {
        await safeCleanup(() => ebClient.send(new DeleteEventBusCommand({ Name: busName })));
      }
    } finally {
      await deleteIamRole(iamClient, roleName);
    }
  }

  {
    const queueName = makeUniqueName('integ-eb-sqs');
    const busName = makeUniqueName('integ-eb-sqs-bus');
    const ruleName = makeUniqueName('integ-eb-sqs-rule');
    const { queueUrl, queueArn } = await createSqsQueue(sqsClient, queueName);
    try {
      await ebClient.send(new CreateEventBusCommand({ Name: busName }));
      try {
        await ebClient.send(new PutRuleCommand({ Name: ruleName, EventBusName: busName }));
        try {
          await ebClient.send(new PutTargetsCommand({
            Rule: ruleName, EventBusName: busName,
            Targets: [{ Id: 't1', Arn: queueArn }],
          }));
          try {
            await ebClient.send(new PutEventsCommand({
              Entries: [{
                EventBusName: busName,
                Source: 'com.integration.test',
                DetailType: 'SQSTest',
                Detail: JSON.stringify({ message: 'eb-to-sqs' }),
              }],
            }));
            await sleep(2000);
            await r('EventBridge_SQS', async () => {
              const msgs = await sqsClient.send(new ReceiveMessageCommand({ QueueUrl: queueUrl, MaxNumberOfMessages: 5, WaitTimeSeconds: 3 }));
              if (!msgs.Messages || msgs.Messages.length === 0) {
                throw new Error('expected message in queue, got 0');
              }
            });
          } finally {
            await safeCleanup(() => ebClient.send(new RemoveTargetsCommand({ Rule: ruleName, EventBusName: busName, Ids: ['t1'] })));
          }
        } finally {
          await safeCleanup(() => ebClient.send(new DeleteRuleCommand({ Name: ruleName, EventBusName: busName })));
        }
      } finally {
        await safeCleanup(() => ebClient.send(new DeleteEventBusCommand({ Name: busName })));
      }
    } finally {
      await safeCleanup(() => sqsClient.send(new DeleteQueueCommand({ QueueUrl: queueUrl })));
    }
  }

  {
    const topicName = makeUniqueName('integ-eb-sns');
    const busName = makeUniqueName('integ-eb-sns-bus');
    const ruleName = makeUniqueName('integ-eb-sns-rule');
    const queueName = makeUniqueName('integ-eb-sns-sqs');
    const topicARN = await createSnsTopic(snsClient, topicName);
    try {
      const { queueUrl, queueArn } = await createSqsQueue(sqsClient, queueName);
      try {
        await snsClient.send(new SubscribeCommand({
          TopicArn: topicARN, Protocol: 'sqs',
          Endpoint: queueArn,
        }));
        await ebClient.send(new CreateEventBusCommand({ Name: busName }));
        try {
          await ebClient.send(new PutRuleCommand({ Name: ruleName, EventBusName: busName }));
          try {
            await ebClient.send(new PutTargetsCommand({
              Rule: ruleName, EventBusName: busName,
              Targets: [{ Id: 't1', Arn: topicARN }],
            }));
            try {
              await ebClient.send(new PutEventsCommand({
                Entries: [{
                  EventBusName: busName,
                  Source: 'com.integration.test',
                  DetailType: 'SNSTest',
                  Detail: JSON.stringify({ message: 'eb-to-sns' }),
                }],
              }));
              await sleep(3000);
              await r('EventBridge_SNS', async () => {
                const msgs = await sqsClient.send(new ReceiveMessageCommand({ QueueUrl: queueUrl, MaxNumberOfMessages: 5, WaitTimeSeconds: 3 }));
                if (!msgs.Messages || msgs.Messages.length === 0) {
                  throw new Error('expected message in queue (via SNS), got 0');
                }
              });
            } finally {
              await safeCleanup(() => ebClient.send(new RemoveTargetsCommand({ Rule: ruleName, EventBusName: busName, Ids: ['t1'] })));
            }
          } finally {
            await safeCleanup(() => ebClient.send(new DeleteRuleCommand({ Name: ruleName, EventBusName: busName })));
          }
        } finally {
          await safeCleanup(() => ebClient.send(new DeleteEventBusCommand({ Name: busName })));
        }
      } finally {
        await safeCleanup(() => sqsClient.send(new DeleteQueueCommand({ QueueUrl: queueUrl })));
      }
    } finally {
      await safeCleanup(() => snsClient.send(new DeleteTopicCommand({ TopicArn: topicARN })));
    }
  }

  {
    const streamName = makeUniqueName('integ-eb-kinesis');
    const busName = makeUniqueName('integ-eb-kin-bus');
    const ruleName = makeUniqueName('integ-eb-kin-rule');
    await kinesisClient.send(new CreateStreamCommand({ StreamName: streamName, ShardCount: 1 }));
    try {
      await sleep(1000);
      const streamDesc = await kinesisClient.send(new DescribeStreamCommand({ StreamName: streamName }));
      if (streamDesc.StreamDescription?.StreamStatus !== 'ACTIVE') {
        for (let i = 0; i < 30; i++) {
          await sleep(1000);
          const d = await kinesisClient.send(new DescribeStreamCommand({ StreamName: streamName }));
          if (d.StreamDescription?.StreamStatus === 'ACTIVE') break;
        }
      }
      const streamARN = `arn:aws:kinesis:${region}:${accountId}:stream/${streamName}`;
      await ebClient.send(new CreateEventBusCommand({ Name: busName }));
      try {
        await ebClient.send(new PutRuleCommand({ Name: ruleName, EventBusName: busName }));
        try {
          await ebClient.send(new PutTargetsCommand({
            Rule: ruleName, EventBusName: busName,
            Targets: [{ Id: 't1', Arn: streamARN }],
          }));
          try {
            await ebClient.send(new PutEventsCommand({
              Entries: [{
                EventBusName: busName,
                Source: 'com.integration.test',
                DetailType: 'KinesisTest',
                Detail: JSON.stringify({ message: 'eb-to-kinesis' }),
              }],
            }));
            await sleep(3000);
            await r('EventBridge_Kinesis', async () => {
              const sd = await kinesisClient.send(new DescribeStreamCommand({ StreamName: streamName }));
              if (!sd.StreamDescription?.Shards || sd.StreamDescription.Shards.length === 0) {
                throw new Error('no shards in stream');
              }
              const shardID = sd.StreamDescription.Shards[0].ShardId!;
              const iterResp = await kinesisClient.send(new GetShardIteratorCommand({
                StreamName: streamName, ShardId: shardID, ShardIteratorType: 'TRIM_HORIZON',
              }));
              const records = await kinesisClient.send(new GetRecordsCommand({ ShardIterator: iterResp.ShardIterator }));
              if (!records.Records || records.Records.length === 0) {
                throw new Error('expected records in kinesis stream, got 0');
              }
            });
          } finally {
            await safeCleanup(() => ebClient.send(new RemoveTargetsCommand({ Rule: ruleName, EventBusName: busName, Ids: ['t1'] })));
          }
        } finally {
          await safeCleanup(() => ebClient.send(new DeleteRuleCommand({ Name: ruleName, EventBusName: busName })));
        }
      } finally {
        await safeCleanup(() => ebClient.send(new DeleteEventBusCommand({ Name: busName })));
      }
    } finally {
      await safeCleanup(() => kinesisClient.send(new DeleteStreamCommand({ StreamName: streamName })));
    }
  }
}
