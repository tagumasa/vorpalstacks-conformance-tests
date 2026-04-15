import type { LambdaClient } from '@aws-sdk/client-lambda';
import type { IAMClient } from '@aws-sdk/client-iam';
import type { CloudWatchLogsClient } from '@aws-sdk/client-cloudwatch-logs';
import type { SFNClient } from '@aws-sdk/client-sfn';
import type { SchedulerClient } from '@aws-sdk/client-scheduler';
import type { SQSClient } from '@aws-sdk/client-sqs';
import type { SNSClient } from '@aws-sdk/client-sns';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';
import { accountId, createIamRole, deleteIamRole, createLambdaFunction, verifyLambdaInvoked, createSqsQueue, createSnsTopic, sleep, lambdaTrustPolicy, sfnTrustPolicy, schedulerTrustPolicy } from './helpers.js';
import { DeleteFunctionCommand } from '@aws-sdk/client-lambda';
import { SubscribeCommand, DeleteTopicCommand } from '@aws-sdk/client-sns';
import { ReceiveMessageCommand, DeleteQueueCommand } from '@aws-sdk/client-sqs';
import { CreateStateMachineCommand, DeleteStateMachineCommand } from '@aws-sdk/client-sfn';
import { CreateScheduleCommand, DeleteScheduleCommand, CreateScheduleGroupCommand, DeleteScheduleGroupCommand } from '@aws-sdk/client-scheduler';

export async function runSchedulerTests(
  lambdaClient: LambdaClient, iamClient: IAMClient, cwlClient: CloudWatchLogsClient,
  sfnClient: SFNClient, sqsClient: SQSClient, snsClient: SNSClient,
  schedulerClient: SchedulerClient,
  runner: TestRunner, results: TestResult[],
): Promise<void> {
  const r = async (testName: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest('integration', testName, fn));

  {
    const fnName = makeUniqueName('integ-sched-lambda');
    const roleName = makeUniqueName('integ-sched-role');
    const lambdaRoleName = makeUniqueName('integ-sched-lambda-fn-role');
    const scheduleName = makeUniqueName('integ-sched-lambda');
    const groupName = makeUniqueName('integ-sched-group');
    await createIamRole(iamClient, roleName, schedulerTrustPolicy);
    try {
      await createIamRole(iamClient, lambdaRoleName, lambdaTrustPolicy);
      try {
        await schedulerClient.send(new CreateScheduleGroupCommand({ Name: groupName }));
        try {
          const fnARN = await createLambdaFunction(lambdaClient, iamClient, fnName, lambdaRoleName, lambdaTrustPolicy);
          try {
            await schedulerClient.send(new CreateScheduleCommand({
              Name: scheduleName, GroupName: groupName, ScheduleExpression: 'rate(1 minute)',
              Target: { Arn: fnARN, RoleArn: `arn:aws:iam::${accountId}:role/${roleName}` },
              FlexibleTimeWindow: { Mode: 'OFF' },
            }));
            try {
              await sleep(8000);
              await r('Scheduler_Lambda', async () => { await verifyLambdaInvoked(cwlClient, fnName); });
            } finally {
              await safeCleanup(() => schedulerClient.send(new DeleteScheduleCommand({ Name: scheduleName, GroupName: groupName })));
            }
          } finally {
            await safeCleanup(() => lambdaClient.send(new DeleteFunctionCommand({ FunctionName: fnName })));
          }
        } finally {
          await safeCleanup(() => schedulerClient.send(new DeleteScheduleGroupCommand({ Name: groupName })));
        }
      } finally { await deleteIamRole(iamClient, lambdaRoleName); }
    } finally { await deleteIamRole(iamClient, roleName); }
  }

  {
    const roleName = makeUniqueName('integ-sched-sqs-role');
    const scheduleName = makeUniqueName('integ-sched-sqs');
    const groupName = makeUniqueName('integ-sched-sqs-group');
    const queueName = makeUniqueName('integ-sched-sqs-q');
    await createIamRole(iamClient, roleName, schedulerTrustPolicy);
    try {
      await schedulerClient.send(new CreateScheduleGroupCommand({ Name: groupName }));
      try {
        const { queueUrl, queueArn } = await createSqsQueue(sqsClient, queueName);
        try {
          await schedulerClient.send(new CreateScheduleCommand({
            Name: scheduleName, GroupName: groupName, ScheduleExpression: 'rate(1 minute)',
            Target: { Arn: queueArn, RoleArn: `arn:aws:iam::${accountId}:role/${roleName}` },
            FlexibleTimeWindow: { Mode: 'OFF' },
          }));
          try {
            await sleep(5000);
            await r('Scheduler_SQS', async () => {
              const msgs = await sqsClient.send(new ReceiveMessageCommand({ QueueUrl: queueUrl, MaxNumberOfMessages: 5, WaitTimeSeconds: 3 }));
              if (!msgs.Messages || msgs.Messages.length === 0) throw new Error('expected message from scheduler in queue, got 0');
            });
          } finally {
            await safeCleanup(() => schedulerClient.send(new DeleteScheduleCommand({ Name: scheduleName, GroupName: groupName })));
          }
        } finally { await safeCleanup(() => sqsClient.send(new DeleteQueueCommand({ QueueUrl: queueUrl }))); }
      } finally { await safeCleanup(() => schedulerClient.send(new DeleteScheduleGroupCommand({ Name: groupName }))); }
    } finally { await deleteIamRole(iamClient, roleName); }
  }

  {
    const roleName = makeUniqueName('integ-sched-sns-role');
    const scheduleName = makeUniqueName('integ-sched-sns');
    const groupName = makeUniqueName('integ-sched-sns-group');
    const topicName = makeUniqueName('integ-sched-sns-t');
    const queueName = makeUniqueName('integ-sched-sns-q');
    await createIamRole(iamClient, roleName, schedulerTrustPolicy);
    try {
      await schedulerClient.send(new CreateScheduleGroupCommand({ Name: groupName }));
      try {
        const topicARN = await createSnsTopic(snsClient, topicName);
        try {
          const { queueUrl, queueArn } = await createSqsQueue(sqsClient, queueName);
          try {
            await snsClient.send(new SubscribeCommand({ TopicArn: topicARN, Protocol: 'sqs', Endpoint: queueArn }));
            await schedulerClient.send(new CreateScheduleCommand({
              Name: scheduleName, GroupName: groupName, ScheduleExpression: 'rate(1 minute)',
              Target: { Arn: topicARN, RoleArn: `arn:aws:iam::${accountId}:role/${roleName}` },
              FlexibleTimeWindow: { Mode: 'OFF' },
            }));
            try {
              await sleep(5000);
              await r('Scheduler_SNS', async () => {
                const msgs = await sqsClient.send(new ReceiveMessageCommand({ QueueUrl: queueUrl, MaxNumberOfMessages: 5, WaitTimeSeconds: 3 }));
                if (!msgs.Messages || msgs.Messages.length === 0) throw new Error('expected message from scheduler (via SNS) in queue, got 0');
              });
            } finally {
              await safeCleanup(() => schedulerClient.send(new DeleteScheduleCommand({ Name: scheduleName, GroupName: groupName })));
            }
          } finally { await safeCleanup(() => sqsClient.send(new DeleteQueueCommand({ QueueUrl: queueUrl }))); }
        } finally { await safeCleanup(() => snsClient.send(new DeleteTopicCommand({ TopicArn: topicARN }))); }
      } finally { await safeCleanup(() => schedulerClient.send(new DeleteScheduleGroupCommand({ Name: groupName }))); }
    } finally { await deleteIamRole(iamClient, roleName); }
  }

  {
    const roleName = makeUniqueName('integ-sched-sfn-role');
    const scheduleName = makeUniqueName('integ-sched-sfn');
    const groupName = makeUniqueName('integ-sched-sfn-group');
    const smName = makeUniqueName('integ-sched-sfn-sm');
    await createIamRole(iamClient, roleName, schedulerTrustPolicy);
    try {
      await createIamRole(iamClient, `${roleName}-sfn`, sfnTrustPolicy);
      try {
        await schedulerClient.send(new CreateScheduleGroupCommand({ Name: groupName }));
        try {
          const smResp = await sfnClient.send(new CreateStateMachineCommand({
            name: smName, roleArn: `arn:aws:iam::${accountId}:role/${roleName}-sfn`,
            definition: JSON.stringify({ StartAt: 'Pass', States: { Pass: { Type: 'Pass', End: true } } }),
          }));
          const smARN = smResp.stateMachineArn!;
          try {
            await schedulerClient.send(new CreateScheduleCommand({
              Name: scheduleName, GroupName: groupName, ScheduleExpression: 'rate(1 minute)',
              Target: { Arn: smARN, RoleArn: `arn:aws:iam::${accountId}:role/${roleName}` },
              FlexibleTimeWindow: { Mode: 'OFF' },
            }));
            try {
              await sleep(5000);
              await r('Scheduler_StepFunctions', async () => {
                const resp = await sfnClient.send(new (await import('@aws-sdk/client-sfn')).ListExecutionsCommand({ stateMachineArn: smARN }));
                if (!resp.executions || resp.executions.length === 0) throw new Error('expected at least 1 execution from scheduler, got 0');
              });
            } finally {
              await safeCleanup(() => schedulerClient.send(new DeleteScheduleCommand({ Name: scheduleName, GroupName: groupName })));
            }
          } finally { await safeCleanup(() => sfnClient.send(new DeleteStateMachineCommand({ stateMachineArn: smARN }))); }
        } finally { await safeCleanup(() => schedulerClient.send(new DeleteScheduleGroupCommand({ Name: groupName }))); }
      } finally { await deleteIamRole(iamClient, `${roleName}-sfn`); }
    } finally { await deleteIamRole(iamClient, roleName); }
  }
}
