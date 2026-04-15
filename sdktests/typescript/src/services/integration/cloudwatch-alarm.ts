import type { LambdaClient } from '@aws-sdk/client-lambda';
import type { IAMClient } from '@aws-sdk/client-iam';
import type { CloudWatchLogsClient } from '@aws-sdk/client-cloudwatch-logs';
import type { CloudWatchClient } from '@aws-sdk/client-cloudwatch';
import type { SFNClient } from '@aws-sdk/client-sfn';
import type { SQSClient } from '@aws-sdk/client-sqs';
import type { SNSClient } from '@aws-sdk/client-sns';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';
import { accountId, createIamRole, deleteIamRole, createLambdaFunction, verifyLambdaInvoked, createSqsQueue, createSnsTopic, sleep, lambdaTrustPolicy, sfnTrustPolicy } from './helpers.js';
import { DeleteFunctionCommand } from '@aws-sdk/client-lambda';
import { SubscribeCommand, DeleteTopicCommand, PublishCommand } from '@aws-sdk/client-sns';
import { ReceiveMessageCommand, DeleteQueueCommand } from '@aws-sdk/client-sqs';
import { PutMetricAlarmCommand, DeleteAlarmsCommand, DescribeAlarmsCommand, SetAlarmStateCommand, PutMetricDataCommand } from '@aws-sdk/client-cloudwatch';
import { CreateStateMachineCommand, DeleteStateMachineCommand, ListExecutionsCommand } from '@aws-sdk/client-sfn';

export async function runCloudWatchAlarmTests(
  lambdaClient: LambdaClient, iamClient: IAMClient, cwlClient: CloudWatchLogsClient,
  cwClient: CloudWatchClient, sfnClient: SFNClient, sqsClient: SQSClient, snsClient: SNSClient,
  runner: TestRunner, results: TestResult[],
): Promise<void> {
  const r = async (testName: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest('integration', testName, fn));

  {
    const topicName = makeUniqueName('integ-alarm-sns');
    const queueName = makeUniqueName('integ-alarm-sns-q');
    const alarmName = makeUniqueName('integ-alarm-sns');
    const topicARN = await createSnsTopic(snsClient, topicName);
    try {
      const { queueUrl, queueArn } = await createSqsQueue(sqsClient, queueName);
      try {
        await snsClient.send(new SubscribeCommand({ TopicArn: topicARN, Protocol: 'sqs', Endpoint: queueArn }));
        await cwClient.send(new PutMetricAlarmCommand({
          AlarmName: alarmName, MetricName: 'CPUUtilization', Namespace: 'AWS/EC2',
          Statistic: 'Average', Period: 1, EvaluationPeriods: 1, Threshold: 0,
          ComparisonOperator: 'GreaterThanThreshold', AlarmActions: [topicARN],
        }));
        try {
          await cwClient.send(new PutMetricDataCommand({
            Namespace: 'AWS/EC2', MetricData: [{ MetricName: 'CPUUtilization', Value: 100 }],
          }));
          await sleep(3000);
          await r('CWAlarm_SNS', async () => {
            const msgs = await sqsClient.send(new ReceiveMessageCommand({ QueueUrl: queueUrl, MaxNumberOfMessages: 5, WaitTimeSeconds: 3 }));
            if (!msgs.Messages || msgs.Messages.length === 0) throw new Error('expected alarm notification in queue, got 0');
          });
        } finally {
          await safeCleanup(() => cwClient.send(new DeleteAlarmsCommand({ AlarmNames: [alarmName] })));
        }
      } finally {
        await safeCleanup(() => sqsClient.send(new DeleteQueueCommand({ QueueUrl: queueUrl })));
      }
    } finally {
      await safeCleanup(() => snsClient.send(new DeleteTopicCommand({ TopicArn: topicARN })));
    }
  }

  {
    const fnName = makeUniqueName('integ-alarm-lambda');
    const roleName = makeUniqueName('integ-alarm-lambda-role');
    const alarmName = makeUniqueName('integ-alarm-lambda');
    await createIamRole(iamClient, roleName, lambdaTrustPolicy);
    try {
      const fnARN = await createLambdaFunction(lambdaClient, iamClient, fnName, roleName, lambdaTrustPolicy);
      try {
        await cwClient.send(new PutMetricAlarmCommand({
          AlarmName: alarmName, MetricName: 'MemoryUtilization', Namespace: 'AWS/EC2',
          Statistic: 'Average', Period: 1, EvaluationPeriods: 1, Threshold: 0,
          ComparisonOperator: 'GreaterThanThreshold', AlarmActions: [fnARN],
        }));
        try {
          await cwClient.send(new PutMetricDataCommand({
            Namespace: 'AWS/EC2', MetricData: [{ MetricName: 'MemoryUtilization', Value: 100 }],
          }));
          await sleep(5000);
          await r('CWAlarm_Lambda', async () => {
            const alarmResp = await cwClient.send(new DescribeAlarmsCommand({ AlarmNames: [alarmName] }));
            if (!alarmResp.MetricAlarms || alarmResp.MetricAlarms.length === 0) throw new Error(`alarm ${alarmName} not found`);
            if (alarmResp.MetricAlarms[0].StateValue !== 'ALARM') throw new Error(`expected alarm state ALARM, got ${alarmResp.MetricAlarms[0].StateValue}`);
            await verifyLambdaInvoked(cwlClient, fnName);
          });
        } finally {
          await safeCleanup(() => cwClient.send(new DeleteAlarmsCommand({ AlarmNames: [alarmName] })));
        }
      } finally {
        await safeCleanup(() => lambdaClient.send(new DeleteFunctionCommand({ FunctionName: fnName })));
      }
    } finally {
      await deleteIamRole(iamClient, roleName);
    }
  }

  {
    const roleName = makeUniqueName('integ-alarm-sfn-role');
    const alarmName = makeUniqueName('integ-alarm-sfn');
    const smName = makeUniqueName('integ-alarm-sfn-sm');
    await createIamRole(iamClient, roleName, sfnTrustPolicy);
    try {
      const smResp = await sfnClient.send(new CreateStateMachineCommand({
        name: smName, roleArn: `arn:aws:iam::${accountId}:role/${roleName}`,
        definition: JSON.stringify({ StartAt: 'Pass', States: { Pass: { Type: 'Pass', End: true } } }),
      }));
      const smARN = smResp.stateMachineArn!;
      try {
        await cwClient.send(new PutMetricAlarmCommand({
          AlarmName: alarmName, MetricName: 'MemoryUtilization', Namespace: 'AWS/EC2',
          Statistic: 'Average', Period: 1, EvaluationPeriods: 1, Threshold: 0,
          ComparisonOperator: 'GreaterThanThreshold', AlarmActions: [smARN],
        }));
        try {
          await cwClient.send(new PutMetricDataCommand({
            Namespace: 'AWS/EC2', MetricData: [{ MetricName: 'DiskSpaceUtilization', Value: 100 }],
          }));
          await sleep(3000);
          await r('CWAlarm_StepFunctions', async () => {
            const resp = await sfnClient.send(new ListExecutionsCommand({ stateMachineArn: smARN }));
            if (!resp.executions || resp.executions.length === 0) throw new Error('expected at least 1 execution from alarm, got 0');
          });
        } finally {
          await safeCleanup(() => cwClient.send(new DeleteAlarmsCommand({ AlarmNames: [alarmName] })));
        }
      } finally {
        await safeCleanup(() => sfnClient.send(new DeleteStateMachineCommand({ stateMachineArn: smARN })));
      }
    } finally {
      await deleteIamRole(iamClient, roleName);
    }
  }
}
