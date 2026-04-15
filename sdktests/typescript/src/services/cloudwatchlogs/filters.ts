import {
  CloudWatchLogsClient,
  CreateLogGroupCommand,
  DeleteLogGroupCommand,
  PutRetentionPolicyCommand,
  DeleteRetentionPolicyCommand,
  DescribeLogGroupsCommand,
  PutMetricFilterCommand,
  DescribeMetricFiltersCommand,
  DeleteMetricFilterCommand,
  TestMetricFilterCommand,
} from '@aws-sdk/client-cloudwatch-logs';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runFilterAndRetentionTests(
  runner: TestRunner,
  client: CloudWatchLogsClient,
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('logs', 'DeleteRetentionPolicy_Basic', async () => {
    const drName = makeUniqueName('DelRetGroup');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: drName }));
      await client.send(new PutRetentionPolicyCommand({ logGroupName: drName, retentionInDays: 14 }));
      await client.send(new DeleteRetentionPolicyCommand({ logGroupName: drName }));
      const descResp = await client.send(new DescribeLogGroupsCommand({ logGroupNamePrefix: drName }));
      if (!descResp.logGroups?.length) throw new Error('log group not found');
      const retention = descResp.logGroups[0].retentionInDays;
      if (retention !== undefined && retention !== 0) {
        throw new Error(`expected retention 0 after delete, got ${retention}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: drName })));
    }
  }));

  results.push(await runner.runTest('logs', 'PutMetricFilter_Basic', async () => {
    const mfName = makeUniqueName('MFGroup');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: mfName }));
      await client.send(new PutMetricFilterCommand({
        logGroupName: mfName,
        filterName: 'ErrorFilter',
        filterPattern: '[ip, user, timestamp, request, status_code=*, bytes=*]',
        metricTransformations: [{
          metricName: 'ErrorCount',
          metricNamespace: 'vorpalstacks/test',
          metricValue: '1',
          defaultValue: 0.0,
        }],
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: mfName })));
    }
  }));

  results.push(await runner.runTest('logs', 'DescribeMetricFilters_Basic', async () => {
    const dmfName = makeUniqueName('DMFGroup');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: dmfName }));
      await client.send(new PutMetricFilterCommand({
        logGroupName: dmfName,
        filterName: 'TestFilter',
        filterPattern: 'ERROR',
        metricTransformations: [{
          metricName: 'ErrorCount',
          metricNamespace: 'vorpalstacks/test',
          metricValue: '1',
        }],
      }));
      const resp = await client.send(new DescribeMetricFiltersCommand({ logGroupName: dmfName }));
      if (resp.metricFilters?.length !== 1) throw new Error(`expected 1 filter, got ${resp.metricFilters?.length}`);
      if (resp.metricFilters[0].filterName !== 'TestFilter') throw new Error(`filter name mismatch: got ${resp.metricFilters[0].filterName}`);
      if (resp.metricFilters[0].filterPattern !== 'ERROR') throw new Error(`filter pattern mismatch: got ${resp.metricFilters[0].filterPattern}`);
      if (resp.metricFilters[0].metricTransformations?.length !== 1) throw new Error(`expected 1 transformation, got ${resp.metricFilters[0].metricTransformations?.length}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: dmfName })));
    }
  }));

  results.push(await runner.runTest('logs', 'DescribeMetricFilters_FilterNamePrefix', async () => {
    const fpfName = makeUniqueName('FPFGroup');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: fpfName }));
      await client.send(new PutMetricFilterCommand({
        logGroupName: fpfName, filterName: 'PrefixFilterA', filterPattern: 'ERROR',
        metricTransformations: [{ metricName: 'ErrorA', metricNamespace: 'test', metricValue: '1' }],
      }));
      await client.send(new PutMetricFilterCommand({
        logGroupName: fpfName, filterName: 'PrefixFilterB', filterPattern: 'WARN',
        metricTransformations: [{ metricName: 'WarnB', metricNamespace: 'test', metricValue: '1' }],
      }));
      const resp = await client.send(new DescribeMetricFiltersCommand({
        logGroupName: fpfName,
        filterNamePrefix: 'PrefixFilterA',
      }));
      if (resp.metricFilters?.length !== 1) throw new Error(`expected 1 filter with prefix 'PrefixFilterA', got ${resp.metricFilters?.length}`);
      if (resp.metricFilters[0].filterName !== 'PrefixFilterA') throw new Error(`filter name mismatch: got ${resp.metricFilters[0].filterName}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: fpfName })));
    }
  }));

  results.push(await runner.runTest('logs', 'DeleteMetricFilter_Basic', async () => {
    const dmfDelName = makeUniqueName('DMFDelGroup');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: dmfDelName }));
      await client.send(new PutMetricFilterCommand({
        logGroupName: dmfDelName, filterName: 'TempFilter', filterPattern: 'ERROR',
        metricTransformations: [{ metricName: 'Err', metricNamespace: 'test', metricValue: '1' }],
      }));
      await client.send(new DeleteMetricFilterCommand({ logGroupName: dmfDelName, filterName: 'TempFilter' }));
      const resp = await client.send(new DescribeMetricFiltersCommand({ logGroupName: dmfDelName }));
      const filterCount = resp.metricFilters?.length ?? 0;
      if (filterCount !== 0) throw new Error(`expected 0 filters after delete, got ${filterCount}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: dmfDelName })));
    }
  }));

  results.push(await runner.runTest('logs', 'TestMetricFilter_Basic', async () => {
    const resp = await client.send(new TestMetricFilterCommand({
      filterPattern: 'ERROR',
      logEventMessages: [
        'ERROR something went wrong',
        'INFO all good',
        '[ERROR] critical failure',
      ],
    }));
    if (!resp.matches?.length) throw new Error('expected matches to be defined');
    if (resp.matches.length !== 2) throw new Error(`expected 2 matches, got ${resp.matches.length}`);
    for (const m of resp.matches) {
      if (!m.eventMessage?.includes('ERROR')) {
        throw new Error(`unexpected match: ${m.eventMessage}`);
      }
    }
  }));
}
