import {
  SNSClient,
  CreateTopicCommand,
  GetTopicAttributesCommand,
  SetTopicAttributesCommand,
  ListTopicsCommand,
  SubscribeCommand,
  ConfirmSubscriptionCommand,
  ListSubscriptionsCommand,
  ListSubscriptionsByTopicCommand,
  GetSubscriptionAttributesCommand,
  PublishCommand,
  PublishBatchCommand,
  UnsubscribeCommand,
  DeleteTopicCommand,
  TagResourceCommand,
  ListTagsForResourceCommand,
  UntagResourceCommand,
} from '@aws-sdk/client-sns';
import { TestRunner } from '../runner.js';
import { TestResult } from '../runner.js';

function makeUniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 999999)}`;
}

export async function runSNSTests(
  runner: TestRunner,
  snsClient: SNSClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const topicName = makeUniqueName('TSTopic');
  const sqsQueueName = makeUniqueName('TSSQSQueue');
  let topicArn = '';
  let subscriptionArn = '';
  let queueUrl = '';

  // Create SQS queue first for subscription
  try {
    const { SQSClient, CreateQueueCommand, GetQueueUrlCommand } = await import('@aws-sdk/client-sqs');
    const sqsClient = new SQSClient({
      endpoint: runner['endpoint'] || 'http://localhost:8080',
      region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });

    const createQueueResp = await sqsClient.send(
      new CreateQueueCommand({
        QueueName: sqsQueueName,
        Attributes: { VisibilityTimeout: '30' },
      })
    );
    queueUrl = createQueueResp.QueueUrl || '';

    const getQueueResp = await sqsClient.send(
      new GetQueueUrlCommand({ QueueName: sqsQueueName })
    );
    if (getQueueResp.QueueUrl) queueUrl = getQueueResp.QueueUrl;
  } catch (err) {
    // SQS might not be available, skip subscription tests
  }

  try {
    // CreateTopic
    results.push(
      await runner.runTest('sns', 'CreateTopic', async () => {
        const resp = await snsClient.send(
          new CreateTopicCommand({
            Name: topicName,
            Attributes: {
              DisplayName: 'Test Topic',
              DeliveryPolicy: JSON.stringify({
                defaultHealthyRetryPolicy: {
                  minDelayTarget: 1,
                  maxDelayTarget: 60,
                  numRetries: 3,
                  numNoDelayRetries: 0,
                },
              }),
            },
            Tags: [
              { Key: 'Environment', Value: 'Test' },
            ],
          })
        );
        if (!resp.TopicArn) throw new Error('TopicArn is null');
        topicArn = resp.TopicArn;
      })
    );

    // GetTopicAttributes
    results.push(
      await runner.runTest('sns', 'GetTopicAttributes', async () => {
        const resp = await snsClient.send(
          new GetTopicAttributesCommand({ TopicArn: topicArn })
        );
        if (!resp.Attributes) throw new Error('Attributes is null');
        if (!resp.Attributes['TopicArn']) throw new Error('TopicArn is missing');
        if (resp.Attributes['DisplayName'] !== 'Test Topic') {
          throw new Error(`Expected DisplayName=Test Topic, got ${resp.Attributes['DisplayName']}`);
        }
      })
    );

    // SetTopicAttributes
    results.push(
      await runner.runTest('sns', 'SetTopicAttributes', async () => {
        await snsClient.send(
          new SetTopicAttributesCommand({
            TopicArn: topicArn,
            AttributeName: 'DisplayName',
            AttributeValue: 'Updated Topic',
          })
        );
        const resp = await snsClient.send(
          new GetTopicAttributesCommand({ TopicArn: topicArn })
        );
        if (resp.Attributes?.['DisplayName'] !== 'Updated Topic') {
          throw new Error(`Expected DisplayName=Updated Topic, got ${resp.Attributes?.['DisplayName']}`);
        }
      })
    );

    // ListTopics
    results.push(
      await runner.runTest('sns', 'ListTopics', async () => {
        const resp = await snsClient.send(new ListTopicsCommand({}));
        if (!resp.Topics) throw new Error('Topics is null');
      })
    );

    // Subscribe (if SQS is available)
    if (queueUrl) {
      results.push(
        await runner.runTest('sns', 'Subscribe', async () => {
          const resp = await snsClient.send(
            new SubscribeCommand({
              TopicArn: topicArn,
              Protocol: 'sqs',
              Endpoint: queueUrl,
            })
          );
          if (!resp.SubscriptionArn) throw new Error('SubscriptionArn is null');
          subscriptionArn = resp.SubscriptionArn;
        })
      );

    // ConfirmSubscription (email protocol requires confirmation)
    results.push(
      await runner.runTest('sns', 'ConfirmSubscription', async () => {
        const subResp = await snsClient.send(
          new SubscribeCommand({
            TopicArn: topicArn,
            Protocol: 'email',
            Endpoint: 'confirm-test@example.com',
          })
        );
        if (!subResp.SubscriptionArn) throw new Error('SubscriptionArn is null');
        const pendingArn = subResp.SubscriptionArn;
        try {
          // email subscriptions are not auto-confirmed, so fetch token via GetSubscriptionAttributes
          const attrResp = await snsClient.send(
            new GetSubscriptionAttributesCommand({ SubscriptionArn: pendingArn })
          );
          const token = attrResp.Attributes?.['Token'];
          if (!token) throw new Error('ConfirmationToken is null for email subscription');
          const confirmResp = await snsClient.send(
            new ConfirmSubscriptionCommand({ TopicArn: topicArn, Token: token })
          );
          if (!confirmResp.SubscriptionArn) throw new Error('ConfirmSubscription SubscriptionArn is null');
        } finally {
          try {
            await snsClient.send(new UnsubscribeCommand({ SubscriptionArn: pendingArn }));
          } catch { /* ignore */ }
        }
      })
    );

      // ListSubscriptionsByTopic
      results.push(
        await runner.runTest('sns', 'ListSubscriptionsByTopic', async () => {
          const resp = await snsClient.send(
            new ListSubscriptionsByTopicCommand({ TopicArn: topicArn })
          );
          if (!resp.Subscriptions) throw new Error('Subscriptions is null');
        })
      );
    }

    // ListSubscriptions
    results.push(
      await runner.runTest('sns', 'ListSubscriptions', async () => {
        const resp = await snsClient.send(new ListSubscriptionsCommand({}));
        if (!resp.Subscriptions) throw new Error('Subscriptions is null');
      })
    );

    // Publish
    let messageId = '';
    results.push(
      await runner.runTest('sns', 'Publish', async () => {
        const resp = await snsClient.send(
          new PublishCommand({
            TopicArn: topicArn,
            Message: JSON.stringify({ test: 'hello', timestamp: Date.now() }),
            Subject: 'Test Message',
            MessageAttributes: {
              AttributeName: {
                DataType: 'String',
                StringValue: 'AttributeValue',
              },
            },
          })
        );
        if (!resp.MessageId) throw new Error('MessageId is null');
        messageId = resp.MessageId;
      })
    );

    // Publish with targetArn
    results.push(
      await runner.runTest('sns', 'Publish_TargetArn', async () => {
        const resp = await snsClient.send(
          new PublishCommand({
            TopicArn: topicArn,
            Message: 'Test message to target',
          })
        );
        if (!resp.MessageId) throw new Error('MessageId is null');
      })
    );

    // PublishBatch
    results.push(
      await runner.runTest('sns', 'PublishBatch', async () => {
        const resp = await snsClient.send(
          new PublishBatchCommand({
            TopicArn: topicArn,
            PublishBatchRequestEntries: [
              { Id: 'msg-1', Message: 'Batch message 1' },
              { Id: 'msg-2', Message: 'Batch message 2' },
              { Id: 'msg-3', Message: 'Batch message 3' },
            ],
          })
        );
        if (!resp.Successful || resp.Successful.length !== 3) {
          throw new Error(`Expected 3 successful, got ${resp.Successful?.length}`);
        }
      })
    );

    // TagResource
    results.push(
      await runner.runTest('sns', 'TagResource', async () => {
        await snsClient.send(
          new TagResourceCommand({
            ResourceArn: topicArn,
            Tags: [{ Key: 'Team', Value: 'Platform' }],
          })
        );
      })
    );

    // ListTagsForResource
    results.push(
      await runner.runTest('sns', 'ListTagsForResource', async () => {
        const resp = await snsClient.send(
          new ListTagsForResourceCommand({ ResourceArn: topicArn })
        );
        if (!resp.Tags) throw new Error('Tags is null');
        const hasTeam = resp.Tags.some((t) => t.Key === 'Team' && t.Value === 'Platform');
        if (!hasTeam) throw new Error('Team tag not found');
      })
    );

    // UntagResource
    results.push(
      await runner.runTest('sns', 'UntagResource', async () => {
        await snsClient.send(
          new UntagResourceCommand({
            ResourceArn: topicArn,
            TagKeys: ['Team'],
          })
        );
      })
    );

    // Unsubscribe (if we have a subscription)
    if (subscriptionArn) {
      results.push(
        await runner.runTest('sns', 'Unsubscribe', async () => {
          await snsClient.send(
            new UnsubscribeCommand({ SubscriptionArn: subscriptionArn })
          );
        })
      );
    }

    // DeleteTopic
    results.push(
      await runner.runTest('sns', 'DeleteTopic', async () => {
        await snsClient.send(
          new DeleteTopicCommand({ TopicArn: topicArn })
        );
      })
    );

  } finally {
    try {
      if (topicArn) {
        await snsClient.send(new DeleteTopicCommand({ TopicArn: topicArn }));
      }
    } catch { /* ignore */ }
    try {
      if (queueUrl) {
        const { SQSClient, DeleteQueueCommand } = await import('@aws-sdk/client-sqs');
        const sqsClient = new SQSClient({
          endpoint: runner['endpoint'] || 'http://localhost:8080',
          region,
          credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
        });
        await sqsClient.send(new DeleteQueueCommand({ QueueUrl: queueUrl }));
      }
    } catch { /* ignore */ }
  }

  // Error cases
  results.push(
    await runner.runTest('sns', 'CreateTopic_Duplicate', async () => {
      const resp = await snsClient.send(
        new CreateTopicCommand({ Name: topicName })
      );
      if (!resp.TopicArn) throw new Error('TopicArn is null');
    })
  );

    results.push(
      await runner.runTest('sns', 'GetTopicAttributes_NonExistent', async () => {
        try {
          await snsClient.send(
            new GetTopicAttributesCommand({
              TopicArn: 'arn:aws:sns:us-east-1:000000000000:NonExistentTopic_xyz_12345'
            })
          );
          throw new Error('Expected error for non-existent topic but got none');
        } catch (err: unknown) {
          if (err instanceof Error && (err.name === 'NotFound' || err.name === 'TopicNotFound' || err.name === 'SNSTopicNotFoundException' || err.name === 'NotFoundException')) {
            // Expected
          } else {
            throw err;
          }
        }
      })
    );

    // MultiBytePublish
    results.push(
      await runner.runTest('sns', 'MultiBytePublish', async () => {
        const jaMsg = '日本語テストメッセージ';
        const zhMsg = '简体中文测试消息';
        const twMsg = '繁體中文測試訊息';
        for (const msg of [jaMsg, zhMsg, twMsg]) {
          const resp = await snsClient.send(
            new PublishCommand({ TopicArn: topicArn, Message: msg })
          );
          if (!resp.MessageId) throw new Error('MessageId is null');
        }
      })
    );

    results.push(
      await runner.runTest('sns', 'Publish_NonExistent', async () => {
        try {
          await snsClient.send(
            new PublishCommand({
              TopicArn: 'arn:aws:sns:us-east-1:000000000000:NonExistentTopic_xyz_12345',
              Message: 'test',
            })
          );
          throw new Error('Expected error for non-existent topic but got none');
        } catch (err: unknown) {
          if (err instanceof Error && (err.name === 'NotFound' || err.name === 'TopicNotFound' || err.name === 'SNSTopicNotFoundException' || err.name === 'NotFoundException')) {
            // Expected
          } else {
            throw err;
          }
        }
      })
    );

    results.push(
      await runner.runTest('sns', 'Subscribe_NonExistent', async () => {
        try {
          await snsClient.send(
            new SubscribeCommand({
              TopicArn: 'arn:aws:sns:us-east-1:000000000000:NonExistentTopic_xyz_12345',
              Protocol: 'sqs',
              Endpoint: 'https://example.sqs.amazonaws.com/000000000000/test',
            })
          );
          throw new Error('Expected error for non-existent topic but got none');
        } catch (err: unknown) {
          if (err instanceof Error && (err.name === 'NotFound' || err.name === 'TopicNotFound' || err.name === 'SNSTopicNotFoundException' || err.name === 'NotFoundException')) {
            // Expected
          } else {
            throw err;
          }
        }
      })
    );

  return results;
}