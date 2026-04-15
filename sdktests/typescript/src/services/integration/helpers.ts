import {
  LambdaClient,
  CreateFunctionCommand,
  DeleteFunctionCommand,
  GetFunctionConfigurationCommand,
} from '@aws-sdk/client-lambda';
import {
  IAMClient,
  CreateRoleCommand,
  DeleteRoleCommand,
} from '@aws-sdk/client-iam';
import {
  CloudWatchLogsClient,
  DescribeLogStreamsCommand,
} from '@aws-sdk/client-cloudwatch-logs';
import {
  SQSClient,
  CreateQueueCommand,
  GetQueueAttributesCommand,
} from '@aws-sdk/client-sqs';
import {
  SNSClient,
  CreateTopicCommand,
} from '@aws-sdk/client-sns';
import { safeCleanup } from '../../helpers.js';

export const sleep = (ms: number) => new Promise(r => setTimeout(r, ms));

export const lambdaTrustPolicy = JSON.stringify({
  Version: '2012-10-17',
  Statement: [{ Effect: 'Allow', Principal: { Service: 'lambda.amazonaws.com' }, Action: 'sts:AssumeRole' }],
});

export const sfnTrustPolicy = JSON.stringify({
  Version: '2012-10-17',
  Statement: [{ Effect: 'Allow', Principal: { Service: 'states.amazonaws.com' }, Action: 'sts:AssumeRole' }],
});

export const schedulerTrustPolicy = JSON.stringify({
  Version: '2012-10-17',
  Statement: [{ Effect: 'Allow', Principal: { Service: 'scheduler.amazonaws.com' }, Action: 'sts:AssumeRole' }],
});

export const handlerCode = Buffer.from('exports.handler = async (event) => { return JSON.stringify(event); };');

export const accountId = '000000000000';

export async function createIamRole(iamClient: IAMClient, roleName: string, trustPolicy: string): Promise<string> {
  try {
    await iamClient.send(new CreateRoleCommand({ RoleName: roleName, AssumeRolePolicyDocument: trustPolicy }));
  } catch { /* may already exist */ }
  return `arn:aws:iam::${accountId}:role/${roleName}`;
}

export async function deleteIamRole(iamClient: IAMClient, roleName: string): Promise<void> {
  await safeCleanup(() => iamClient.send(new DeleteRoleCommand({ RoleName: roleName })));
}

export async function createLambdaFunction(
  lambdaClient: LambdaClient,
  iamClient: IAMClient,
  funcName: string,
  roleName: string,
  trustPolicy: string,
): Promise<string> {
  const roleArn = await createIamRole(iamClient, roleName, trustPolicy);
  const resp = await lambdaClient.send(new CreateFunctionCommand({
    FunctionName: funcName,
    Runtime: 'nodejs22.x',
    Role: roleArn,
    Handler: 'index.handler',
    Code: { ZipFile: handlerCode },
  }));
  return resp.FunctionArn || `arn:aws:lambda:${''}:000000000000:function:${funcName}`;
}

export async function waitForLambdaActive(
  lambdaClient: LambdaClient,
  funcName: string,
  maxAttempts = 30,
): Promise<void> {
  for (const _ of Array(maxAttempts)) {
    try {
      const resp = await lambdaClient.send(new GetFunctionConfigurationCommand({ FunctionName: funcName }));
      if (resp.State === 'Active') return;
    } catch { /* not ready */ }
    await sleep(1000);
  }
  throw new Error(`lambda ${funcName} did not become active`);
}

export async function verifyLambdaInvoked(
  cwlClient: CloudWatchLogsClient,
  funcName: string,
  maxAttempts = 20,
): Promise<void> {
  const logGroupName = `/aws/lambda/${funcName}`;
  for (const _ of Array(maxAttempts)) {
    await sleep(2000);
    try {
      const resp = await cwlClient.send(new DescribeLogStreamsCommand({ logGroupName, limit: 10 }));
      if (resp.logStreams && resp.logStreams.length > 0) return;
    } catch { /* log group may not exist yet */ }
  }
  throw new Error(`lambda ${funcName} was not invoked (no log streams found)`);
}

export async function createSqsQueue(sqsClient: SQSClient, queueName: string): Promise<{ queueUrl: string; queueArn: string }> {
  const resp = await sqsClient.send(new CreateQueueCommand({ QueueName: queueName }));
  const queueUrl = resp.QueueUrl!;
  const attrs = await sqsClient.send(new GetQueueAttributesCommand({ QueueUrl: queueUrl, AttributeNames: ['QueueArn'] }));
  const queueArn = attrs.Attributes?.['QueueArn'] || '';
  return { queueUrl, queueArn };
}

export async function createSnsTopic(snsClient: SNSClient, topicName: string): Promise<string> {
  const resp = await snsClient.send(new CreateTopicCommand({ Name: topicName }));
  return resp.TopicArn!;
}
