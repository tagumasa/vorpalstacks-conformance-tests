import {
  LambdaClient,
} from '@aws-sdk/client-lambda';
import {
  IAMClient,
} from '@aws-sdk/client-iam';
import {
  EventBridgeClient,
} from '@aws-sdk/client-eventbridge';
import {
  CloudWatchClient,
} from '@aws-sdk/client-cloudwatch';
import {
  CloudWatchLogsClient,
} from '@aws-sdk/client-cloudwatch-logs';
import {
  SFNClient,
} from '@aws-sdk/client-sfn';
import {
  SchedulerClient,
} from '@aws-sdk/client-scheduler';
import {
  SNSClient,
} from '@aws-sdk/client-sns';
import {
  SQSClient,
} from '@aws-sdk/client-sqs';
import {
  KinesisClient,
} from '@aws-sdk/client-kinesis';
import {
  S3Client,
} from '@aws-sdk/client-s3';
import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { runEventBridgeTests } from './eventbridge-tests.js';
import { runEsmTests } from './event-source-mapping.js';
import { runCloudWatchAlarmTests } from './cloudwatch-alarm.js';
import { runSchedulerTests } from './scheduler.js';
import { runSfnTaskAndMiscTests } from './sfn-task-and-misc.js';

export function registerIntegration(): ServiceRegistration {
  return {
    name: 'integration',
    category: 'integration',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const mkClient = <T>(ClientClass: new (cfg: any) => T, forcePathStyle = false): T =>
        new ClientClass({ endpoint: ctx.endpoint, region: ctx.region, credentials: ctx.credentials, forcePathStyle });

      const lambdaClient = mkClient(LambdaClient);
      const iamClient = mkClient(IAMClient);
      const ebClient = mkClient(EventBridgeClient);
      const cwClient = mkClient(CloudWatchClient);
      const cwlClient = mkClient(CloudWatchLogsClient);
      const sfnClient = mkClient(SFNClient);
      const schedulerClient = mkClient(SchedulerClient);
      const snsClient = mkClient(SNSClient);
      const sqsClient = mkClient(SQSClient);
      const kinesisClient = mkClient(KinesisClient);
      const s3Client = mkClient(S3Client, true);

      const results: TestResult[] = [];
      const region = ctx.region;
      const ts = String(Date.now());

      await runEventBridgeTests(lambdaClient, iamClient, ebClient, cwlClient, sfnClient, sqsClient, snsClient, kinesisClient, runner, results, region);
      await runEsmTests(lambdaClient, iamClient, cwlClient, sqsClient, kinesisClient, runner, results, region);
      await runCloudWatchAlarmTests(lambdaClient, iamClient, cwlClient, cwClient, sfnClient, sqsClient, snsClient, runner, results);
      await runSchedulerTests(lambdaClient, iamClient, cwlClient, sfnClient, sqsClient, snsClient, schedulerClient, runner, results);
      await runSfnTaskAndMiscTests(lambdaClient, iamClient, cwlClient, sfnClient, sqsClient, snsClient, s3Client, kinesisClient, runner, results, region, ts);

      return results;
    },
  };
}
