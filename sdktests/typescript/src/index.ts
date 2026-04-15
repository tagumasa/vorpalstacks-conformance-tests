#!/usr/bin/env node

import { Command } from 'commander';
import { TestRunner, ServiceContext, ServiceRegistration } from './runner.js';

import { registerIAM } from './services/iam/index.js';
import { registerDynamoDB } from './services/dynamodb/index.js';
import { registerS3 } from './services/s3/index.js';
import { registerLambda } from './services/lambda/index.js';
import { registerSQS } from './services/sqs/index.js';
import { registerSNS } from './services/sns/index.js';
import { registerKMS } from './services/kms/index.js';
import { registerKinesis } from './services/kinesis/index.js';
import { registerEventBridge } from './services/eventbridge/index.js';
import { registerStepFunctions } from './services/stepfunctions/index.js';
import { registerAPIGateway } from './services/apigateway/index.js';
import { registerACM } from './services/acm/index.js';
import { registerAthena } from './services/athena/index.js';
import { registerCloudFront } from './services/cloudfront/index.js';
import { registerCloudTrail } from './services/cloudtrail/index.js';
import { registerCloudWatch } from './services/cloudwatch/index.js';
import { registerCloudWatchLogs } from './services/cloudwatchlogs/index.js';
import { registerCognito } from './services/cognito/index.js';
import { registerCognitoIdentity } from './services/cognito_identity/index.js';
import { registerRoute53 } from './services/route53/index.js';
import { registerSecretsManager } from './services/secretsmanager/index.js';
import { registerSESv2 } from './services/sesv2/index.js';
import { registerSSM } from './services/ssm/index.js';
import { registerSTS } from './services/sts/index.js';
import { registerScheduler } from './services/scheduler/index.js';
import { registerTimestream } from './services/timestream/index.js';
import { registerWAF } from './services/wafv2/index.js';
import { registerAppSync } from './services/appsync/index.js';
import { registerAppSyncWS } from './services/appsync_ws/index.js';
import { registerNeptune } from './services/neptune/index.js';
import { registerNeptuneData } from './services/neptunedata/index.js';
import { registerNeptuneGraph } from './services/neptunegraph/index.js';
import { registerIntegration } from './services/integration/index.js';

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

const ctx: ServiceContext = {
  endpoint: opts.endpoint,
  region: opts.region,
  credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
  verbose: opts.verbose,
};

const service = opts.service || 'all';

function registerAll(): ServiceRegistration[] {
  return [
    registerIAM(),
    registerDynamoDB(),
    registerS3(),
    registerLambda(),
    registerSQS(),
    registerSNS(),
    registerKMS(),
    registerKinesis(),
    registerEventBridge(),
    registerStepFunctions(),
    registerAPIGateway(),
    registerACM(),
    registerAthena(),
    registerCloudFront(),
    registerCloudTrail(),
    registerCloudWatch(),
    registerCloudWatchLogs(),
    registerCognito(),
    registerCognitoIdentity(),
    registerRoute53(),
    registerSecretsManager(),
    registerSESv2(),
    registerSSM(),
    registerSTS(),
    registerScheduler(),
    registerTimestream(),
    registerWAF(),
    registerAppSync(),
    registerAppSyncWS(),
    registerNeptune(),
    registerNeptuneData(),
    registerNeptuneGraph(),
    registerIntegration(),
  ];
}

async function main() {
  const allRegistrations = registerAll();
  const requested = service === 'all'
    ? allRegistrations.map((r) => r.name)
    : service.split(',').map((s: string) => s.trim());

  const allResults = new Map<string, import('./runner.js').TestResult[]>();

  for (const reg of allRegistrations) {
    if (!requested.includes(reg.name)) continue;
    const cat = `[${reg.category.toUpperCase()}]`.padEnd(14);
    console.log(`\n--- Running ${cat} ${reg.name} tests ---`);
    const svcResults = await reg.run(runner, ctx);
    allResults.set(reg.name, svcResults);
  }

  runner.printReport(allResults, opts.format, allRegistrations);

  const summary = runner.summarize(allResults);
  console.log();
  console.log(`  TOTAL: ${summary.passed} passed, ${summary.failed} failed, ${summary.skipped} skipped`);

  if (summary.failed > 0) {
    process.exit(1);
  }
}

main().catch((err) => {
  console.error('Fatal error:', err);
  process.exit(1);
});
