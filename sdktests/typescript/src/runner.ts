export interface TestResult {
  service: string;
  testName: string;
  status: 'PASS' | 'FAIL' | 'SKIP';
  error?: string;
  duration: number;
}

export interface RunnerOptions {
  endpoint: string;
  region: string;
  verbose: boolean;
}

const ALL_SERVICES = [
  'acm', 'apigateway', 'athena', 'cloudfront', 'cloudtrail',
  'cloudwatch', 'cloudwatchlogs', 'cognito', 'dynamodb', 'eventbridge',
  'iam', 'kinesis', 'kms', 'lambda', 'route53', 's3',
  'scheduler', 'secretsmanager', 'sesv2', 'sns', 'sqs',
  'ssm', 'sts', 'sfn', 'timestream', 'waf',
];

export class TestRunner {
  private endpoint: string;
  private region: string;
  private verbose: boolean;

  constructor(options: RunnerOptions) {
    this.endpoint = options.endpoint;
    this.region = options.region;
    this.verbose = options.verbose;
  }

  async runTest(
    service: string,
    testName: string,
    fn: () => Promise<void>
  ): Promise<TestResult> {
    const start = Date.now();
    try {
      await fn();
      const duration = Date.now() - start;
      if (this.verbose) {
        console.log(`  [PASS] ${service}/${testName}`);
      }
      return { service, testName, status: 'PASS', duration };
    } catch (err) {
      const duration = Date.now() - start;
      const error = err instanceof Error ? err.message : String(err);
      if (this.verbose) {
        console.log(`  [FAIL] ${service}/${testName}: ${error}`);
      }
      return { service, testName, status: 'FAIL', error, duration };
    }
  }

  getAllServices(): string[] {
    return [...ALL_SERVICES];
  }

  printReport(results: TestResult[], format: 'table' | 'json'): void {
    if (format === 'json') {
      console.log(JSON.stringify(results, null, 2));
      return;
    }

    console.log('\n----------------------------------------');
    console.log('SERVICE          TEST                              STATUS');
    console.log('----------------------------------------');
    for (const r of results) {
      const svc = r.service.padEnd(15);
      const name = r.testName.substring(0, 30).padEnd(30);
      const status = r.status;
      console.log(`${svc} ${name} ${status}`);
    }
    console.log('----------------------------------------');

    const passed = results.filter((r) => r.status === 'PASS').length;
    const failed = results.filter((r) => r.status === 'FAIL').length;
    const skipped = results.filter((r) => r.status === 'SKIP').length;
    console.log(`\nTotal: ${results.length} | Passed: ${passed} | Failed: ${failed} | Skipped: ${skipped}`);
  }
}
