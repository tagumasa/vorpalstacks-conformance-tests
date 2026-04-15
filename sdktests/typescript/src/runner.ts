export interface TestResult {
  service: string;
  testName: string;
  status: 'PASS' | 'FAIL' | 'SKIP';
  error: string;
  durationMs: number;
}

export type TestFunc = () => Promise<void>;

export interface TestRunnerOpts {
  endpoint: string;
  region: string;
  verbose: boolean;
}

export type TestCategory = 'sdk' | 'ws' | 'integration';

export interface ServiceRegistration {
  name: string;
  category: TestCategory;
  run: (runner: TestRunner, ctx: ServiceContext) => Promise<TestResult[]>;
}

export interface ServiceContext {
  endpoint: string;
  region: string;
  credentials: { accessKeyId: string; secretAccessKey: string };
  verbose: boolean;
}

export class TestRunner {
  readonly endpoint: string;
  readonly region: string;
  readonly verbose: boolean;

  constructor(opts: TestRunnerOpts) {
    this.endpoint = opts.endpoint;
    this.region = opts.region;
    this.verbose = opts.verbose;
  }

  async runTest(service: string, testName: string, fn: TestFunc): Promise<TestResult> {
    if (this.verbose) {
      process.stdout.write(`  Running: ${testName}...\n`);
    }

    const start = Date.now();
    try {
      await fn();
      const durationMs = Date.now() - start;
      if (this.verbose) {
        process.stdout.write(`  ✓ ${testName} (${(durationMs / 1000).toFixed(2)}s)\n`);
      }
      return { service, testName, status: 'PASS', error: '', durationMs };
    } catch (err: unknown) {
      const durationMs = Date.now() - start;
      const message = err instanceof Error ? err.message : String(err);
      if (this.verbose) {
        process.stdout.write(`  ✗ ${testName}: ${message}\n`);
      }
      return { service, testName, status: 'FAIL', error: message, durationMs };
    }
  }

  skipTest(service: string, testName: string, reason: string): TestResult {
    if (this.verbose) {
      process.stdout.write(`  - ${testName} (skipped: ${reason})\n`);
    }
    return { service, testName, status: 'SKIP', error: reason, durationMs: 0 };
  }

  printReport(allResults: Map<string, TestResult[]>, format: string, registrations?: ServiceRegistration[]): void {
    if (format === 'json') {
      this.printJSONReport(allResults);
    } else {
      this.printTableReport(allResults, registrations);
    }
  }

  private printTableReport(allResults: Map<string, TestResult[]>, registrations?: ServiceRegistration[]): void {
    const categories: TestCategory[] = ['sdk', 'integration', 'ws'];
    const categoryLabels: Record<TestCategory, string> = {
      sdk: 'SDK TESTS',
      integration: 'INTEGRATION TESTS',
      ws: 'WEBSOCKET TESTS',
    };

    const svcCategory = new Map<string, TestCategory>();
    if (registrations) {
      for (const reg of registrations) {
        svcCategory.set(reg.name, reg.category);
      }
    }

    for (const cat of categories) {
      const catResults = new Map<string, TestResult[]>();
      for (const [svc, results] of allResults) {
        const svcCat = svcCategory.get(svc);
        if (svcCat && svcCat !== cat) continue;
        catResults.set(svc, results);
      }
      if (catResults.size === 0) continue;

      process.stdout.write(`\n========== ${categoryLabels[cat]} ==========\n`);
      for (const [svc, results] of catResults) {
        process.stdout.write(`\n=== ${svc.toUpperCase()} ===\n`);
        for (const r of results) {
          const sym = r.status === 'PASS' ? '✓' : r.status === 'FAIL' ? '✗' : '-';
          let line = `  ${sym} ${r.testName} (${(r.durationMs / 1000).toFixed(2)}s)`;
          if (r.error) line += ` - ${r.error}`;
          process.stdout.write(line + '\n');
        }
      }
    }
  }

  private printJSONReport(allResults: Map<string, TestResult[]>): void {
    const obj: Record<string, unknown> = { results: {} };
    const summary = { passed: 0, failed: 0, skipped: 0 };
    for (const [svc, results] of allResults) {
      (obj.results as Record<string, TestResult[]>)[svc] = results;
      for (const r of results) {
        if (r.status === 'PASS') summary.passed++;
        else if (r.status === 'FAIL') summary.failed++;
        else summary.skipped++;
      }
    }
    obj.summary = summary;
    process.stdout.write(JSON.stringify(obj, null, 2) + '\n');
  }

  summarize(allResults: Map<string, TestResult[]>): { passed: number; failed: number; skipped: number } {
    let passed = 0, failed = 0, skipped = 0;
    for (const results of allResults.values()) {
      for (const r of results) {
        if (r.status === 'PASS') passed++;
        else if (r.status === 'FAIL') failed++;
        else skipped++;
      }
    }
    return { passed, failed, skipped };
  }
}
