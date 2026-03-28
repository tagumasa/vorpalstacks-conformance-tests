#!/usr/bin/env node

import { Command } from 'commander';
import { LambdaClient } from '@aws-sdk/client-lambda';
import { IAMClient } from '@aws-sdk/client-iam';
import { DynamoDBClient } from '@aws-sdk/client-dynamodb';
import { SQSClient } from '@aws-sdk/client-sqs';
import { SNSClient } from '@aws-sdk/client-sns';
import { KMSClient } from '@aws-sdk/client-kms';
import { CognitoIdentityProviderClient } from '@aws-sdk/client-cognito-identity-provider';
import { S3Client } from '@aws-sdk/client-s3';
import { EventBridgeClient } from '@aws-sdk/client-eventbridge';
import { SFNClient } from '@aws-sdk/client-sfn';
import { KinesisClient } from '@aws-sdk/client-kinesis';
import { NodeHttpHandler } from '@aws-sdk/node-http-handler';
import { AthenaClient } from '@aws-sdk/client-athena';
import { SecretsManagerClient } from '@aws-sdk/client-secrets-manager';
import { CloudWatchLogsClient } from '@aws-sdk/client-cloudwatch-logs';
import { APIGatewayClient } from '@aws-sdk/client-api-gateway';
import { ACMClient } from '@aws-sdk/client-acm';
import { CloudWatchClient } from '@aws-sdk/client-cloudwatch';
import { Route53Client } from '@aws-sdk/client-route-53';
import { STSClient } from '@aws-sdk/client-sts';
import { CloudFrontClient } from '@aws-sdk/client-cloudfront';
import { CloudTrailClient } from '@aws-sdk/client-cloudtrail';
import { SESv2Client } from '@aws-sdk/client-sesv2';
import { SSMClient } from '@aws-sdk/client-ssm';
import { SchedulerClient } from '@aws-sdk/client-scheduler';
import { WAFV2Client } from '@aws-sdk/client-wafv2';
import { TimestreamWriteClient } from '@aws-sdk/client-timestream-write';
import { TestRunner } from './runner.js';
import { runLambdaTests } from './services/lambda.js';
import { runDynamoDBTests } from './services/dynamodb.js';
import { runSQSTests } from './services/sqs.js';
import { runSNSTests } from './services/sns.js';
import { runIAMTests } from './services/iam.js';
import { runKMSTests } from './services/kms.js';
import { runCognitoTests } from './services/cognito.js';
import { runS3Tests } from './services/s3.js';
import { runEventBridgeTests } from './services/eventbridge.js';
import { runStepFunctionsTests } from './services/stepfunctions.js';
import { runKinesisTests } from './services/kinesis.js';
import { runAthenaTests } from './services/athena.js';
import { runSecretsManagerTests } from './services/secretsmanager.js';
import { runCloudWatchLogsTests } from './services/cloudwatchlogs.js';
import { runAPIGatewayTests } from './services/apigateway.js';
import { runACMTests } from './services/acm.js';
import { runCloudWatchTests } from './services/cloudwatch.js';
import { runRoute53Tests } from './services/route53.js';
import { runSTSTests } from './services/sts.js';
import { runCloudFrontTests } from './services/cloudfront.js';
import { runCloudTrailTests } from './services/cloudtrail.js';
import { runSESv2Tests } from './services/sesv2.js';
import { runSSMTests } from './services/ssm.js';
import { runSchedulerTests } from './services/scheduler.js';
import { runWAFTests } from './services/waf.js';
import { runTimestreamTests } from './services/timestream.js';

const program = new Command();

program
  .name('sdk-tests-ts')
  .description('TypeScript SDK conformance tests for VorpalStacks')
  .option('-e, --endpoint <url>', 'VorpalStacks endpoint', 'http://localhost:8080')
  .option('-r, --region <region>', 'AWS region', 'us-east-1')
  .option('-s, --service <names>', 'Comma-separated services or "all"')
  .option('-f, --format <format>', 'Output format: table, json', 'table')
  .option('-v, --verbose', 'Verbose output', false);

program.parse();

const opts = program.opts();

const runner = new TestRunner({
  endpoint: opts.endpoint,
  region: opts.region,
  verbose: opts.verbose,
});

const service = opts.service || 'all';

async function main() {
  const results = [];

  if (service === 'all' || service === 'lambda') {
    const lambdaClient = new LambdaClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const iamClient = new IAMClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const lambdaResults = await runLambdaTests(runner, lambdaClient, iamClient, opts.region);
    results.push(...lambdaResults);
  }

  if (service === 'all' || service === 'dynamodb') {
    const dynamodbClient = new DynamoDBClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const dynamodbResults = await runDynamoDBTests(runner, dynamodbClient, opts.region);
    results.push(...dynamodbResults);
  }

  if (service === 'all' || service === 'sqs') {
    const sqsClient = new SQSClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const sqsResults = await runSQSTests(runner, sqsClient, opts.region);
    results.push(...sqsResults);
  }

  if (service === 'all' || service === 'sns') {
    const snsClient = new SNSClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const snsResults = await runSNSTests(runner, snsClient, opts.region);
    results.push(...snsResults);
  }

  if (service === 'all' || service === 'iam') {
    const iamClient = new IAMClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const iamResults = await runIAMTests(runner, iamClient, opts.region);
    results.push(...iamResults);
  }

  if (service === 'all' || service === 'kms') {
    const kmsClient = new KMSClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const kmsResults = await runKMSTests(runner, kmsClient, opts.region);
    results.push(...kmsResults);
  }

  if (service === 'all' || service === 'cognito') {
    const cognitoClient = new CognitoIdentityProviderClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const cognitoResults = await runCognitoTests(runner, cognitoClient, opts.region);
    results.push(...cognitoResults);
  }

  if (service === 'all' || service === 's3') {
    const s3Client = new S3Client({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
      forcePathStyle: true,
    });
    const s3Results = await runS3Tests(runner, s3Client, opts.region);
    results.push(...s3Results);
  }

  if (service === 'all' || service === 'eventbridge') {
    const ebClient = new EventBridgeClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const ebResults = await runEventBridgeTests(runner, ebClient, opts.region);
    results.push(...ebResults);
  }

  if (service === 'all' || service === 'sfn') {
    const sfnClient = new SFNClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const sfnResults = await runStepFunctionsTests(runner, sfnClient, opts.region);
    results.push(...sfnResults);
  }

  if (service === 'all' || service === 'kinesis') {
    const kinesisClient = new KinesisClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
      requestHandler: new NodeHttpHandler(),
    });
    const kinesisResults = await runKinesisTests(runner, kinesisClient, opts.region);
    results.push(...kinesisResults);
  }

  if (service === 'all' || service === 'athena') {
    const athenaClient = new AthenaClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const athenaResults = await runAthenaTests(runner, athenaClient, opts.region);
    results.push(...athenaResults);
  }

  if (service === 'all' || service === 'secretsmanager') {
    const secretsClient = new SecretsManagerClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const secretsResults = await runSecretsManagerTests(runner, secretsClient, opts.region);
    results.push(...secretsResults);
  }

  if (service === 'all' || service === 'logs') {
    const logsClient = new CloudWatchLogsClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const logsResults = await runCloudWatchLogsTests(runner, logsClient, opts.region);
    results.push(...logsResults);
  }

  if (service === 'all' || service === 'apigateway') {
    const apigatewayClient = new APIGatewayClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const apigatewayResults = await runAPIGatewayTests(runner, apigatewayClient, opts.region);
    results.push(...apigatewayResults);
  }

  if (service === 'all' || service === 'acm') {
    const acmClient = new ACMClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const acmResults = await runACMTests(runner, acmClient, opts.region);
    results.push(...acmResults);
  }

  if (service === 'all' || service === 'cloudwatch') {
    const cloudwatchClient = new CloudWatchClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const cloudwatchResults = await runCloudWatchTests(runner, cloudwatchClient, opts.region);
    results.push(...cloudwatchResults);
  }

  if (service === 'all' || service === 'route53') {
    const route53Client = new Route53Client({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const route53Results = await runRoute53Tests(runner, route53Client, opts.region);
    results.push(...route53Results);
  }

  if (service === 'all' || service === 'sts') {
    const stsClient = new STSClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const iamClient = new IAMClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const stsResults = await runSTSTests(runner, stsClient, iamClient, opts.region);
    results.push(...stsResults);
  }

  if (service === 'all' || service === 'cloudfront') {
    const cloudfrontClient = new CloudFrontClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const cloudfrontResults = await runCloudFrontTests(runner, cloudfrontClient, opts.region);
    results.push(...cloudfrontResults);
  }

  if (service === 'all' || service === 'cloudtrail') {
    const cloudtrailClient = new CloudTrailClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const cloudtrailResults = await runCloudTrailTests(runner, cloudtrailClient, opts.region);
    results.push(...cloudtrailResults);
  }

  if (service === 'all' || service === 'sesv2') {
    const sesv2Client = new SESv2Client({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const sesv2Results = await runSESv2Tests(runner, sesv2Client, opts.region);
    results.push(...sesv2Results);
  }

  if (service === 'all' || service === 'ssm') {
    const ssmClient = new SSMClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const ssmResults = await runSSMTests(runner, ssmClient, opts.region);
    results.push(...ssmResults);
  }

  if (service === 'all' || service === 'scheduler') {
    const schedulerClient = new SchedulerClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const iamClient = new IAMClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const stsClient = new STSClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const schedulerResults = await runSchedulerTests(runner, schedulerClient, iamClient, stsClient, opts.region);
    results.push(...schedulerResults);
  }

  if (service === 'all' || service === 'waf') {
    const wafClient = new WAFV2Client({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const wafResults = await runWAFTests(runner, wafClient, opts.region);
    results.push(...wafResults);
  }

  if (service === 'all' || service === 'timestream') {
    const timestreamClient = new TimestreamWriteClient({
      endpoint: opts.endpoint,
      region: opts.region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    });
    const timestreamResults = await runTimestreamTests(runner, timestreamClient, opts.region);
    results.push(...timestreamResults);
  }

  runner.printReport(results, opts.format);

  const failed = results.filter((r) => r.status === 'FAIL').length;
  process.exit(failed > 0 ? 1 : 0);
}

main().catch((err) => {
  console.error('Fatal error:', err);
  process.exit(1);
});