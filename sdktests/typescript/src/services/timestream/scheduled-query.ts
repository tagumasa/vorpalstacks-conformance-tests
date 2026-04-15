import {
  TimestreamWriteClient,
  CreateDatabaseCommand,
  CreateTableCommand,
  WriteRecordsCommand,
  DeleteDatabaseCommand,
  DescribeEndpointsCommand,
} from '@aws-sdk/client-timestream-write';
import {
  TimestreamQueryClient,
  DescribeEndpointsCommand as QueryDescribeEndpointsCommand,
  PrepareQueryCommand,
  QueryCommand,
  CreateScheduledQueryCommand,
  DescribeScheduledQueryCommand,
  ListScheduledQueriesCommand,
  UpdateScheduledQueryCommand,
  ExecuteScheduledQueryCommand,
  DeleteScheduledQueryCommand,
  TagResourceCommand as QueryTagResourceCommand,
  ListTagsForResourceCommand as QueryListTagsForResourceCommand,
  UntagResourceCommand as QueryUntagResourceCommand,
  DescribeAccountSettingsCommand,
  UpdateAccountSettingsCommand,
} from '@aws-sdk/client-timestream-query';
import {
  IAMClient,
  CreateRoleCommand,
  DeleteRoleCommand,
} from '@aws-sdk/client-iam';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, assertThrows, safeCleanup } from '../../helpers.js';

export async function runScheduledQueryTests(
  runner: TestRunner,
  writeClient: TimestreamWriteClient,
  queryClient: TimestreamQueryClient,
  iamClient: IAMClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const s = 'timestream';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(s, name, fn));

  await r('DescribeEndpoints_Query', async () => {
    const resp = await queryClient.send(new QueryDescribeEndpointsCommand({}));
    if (!resp.Endpoints || resp.Endpoints.length === 0) throw new Error('Endpoints is empty');
  });

  await r('DescribeEndpoints_Write', async () => {
    await writeClient.send(new DescribeEndpointsCommand({}));
  });

  await r('PrepareQuery', async () => {
    const resp = await queryClient.send(new PrepareQueryCommand({ QueryString: 'SELECT 1' }));
    if (!resp.QueryString || resp.QueryString !== 'SELECT 1') throw new Error('QueryString mismatch');
  });

  const sqName = makeUniqueName('sq-test');
  const sqDb = makeUniqueName('sq-db');
  const sqTbl = makeUniqueName('sq-tbl');
  const sqRoleName = makeUniqueName('ts-sq-role');
  let sqArn = '';
  let sqSetup = false;

  try {
    await r('ScheduledQuery_Setup', async () => {
      await iamClient.send(new CreateRoleCommand({
        RoleName: sqRoleName,
        AssumeRolePolicyDocument: JSON.stringify({
          Version: '2012-10-17',
          Statement: [{ Effect: 'Allow', Principal: { Service: 'timestream.amazonaws.com' }, Action: 'sts:AssumeRole' }],
        }),
      }));
      await writeClient.send(new CreateDatabaseCommand({ DatabaseName: sqDb }));
      await writeClient.send(new CreateTableCommand({ DatabaseName: sqDb, TableName: sqTbl }));
      sqSetup = true;
    });

    await r('CreateScheduledQuery', async () => {
      const resp = await queryClient.send(new CreateScheduledQueryCommand({
        Name: sqName,
        QueryString: `SELECT * FROM "${sqDb}"."${sqTbl}"`,
        ScheduleConfiguration: { ScheduleExpression: 'cron(0 0 * * ? *)' },
        NotificationConfiguration: { SnsConfiguration: { TopicArn: 'arn:aws:sns:us-east-1:000000000000:test-topic' } },
        ErrorReportConfiguration: { S3Configuration: { BucketName: 'error-report-bucket' } },
        ScheduledQueryExecutionRoleArn: `arn:aws:iam::000000000000:role/${sqRoleName}`,
        Tags: [{ Key: 'env', Value: 'scheduled-test' }],
      }));
      if (!resp.Arn) throw new Error('Arn is null');
      sqArn = resp.Arn;
    });

    await r('DescribeScheduledQuery', async () => {
      const resp = await queryClient.send(new DescribeScheduledQueryCommand({ ScheduledQueryArn: sqArn }));
      if (!resp.ScheduledQuery) throw new Error('ScheduledQuery is null');
      if (!resp.ScheduledQuery.Name || resp.ScheduledQuery.Name !== sqName)
        throw new Error(`expected Name=${sqName}, got ${resp.ScheduledQuery.Name}`);
      if (!resp.ScheduledQuery.CreationTime) throw new Error('CreationTime is null');
    });

    await r('ListScheduledQueries', async () => {
      const resp = await queryClient.send(new ListScheduledQueriesCommand({}));
      if (!resp.ScheduledQueries || resp.ScheduledQueries.length === 0)
        throw new Error('expected at least 1 scheduled query');
      const found = resp.ScheduledQueries.some((sq: any) => sq.Name === sqName);
      if (!found) throw new Error(`scheduled query ${sqName} not found in list`);
    });

    await r('UpdateScheduledQuery', async () => {
      await queryClient.send(new UpdateScheduledQueryCommand({ ScheduledQueryArn: sqArn, State: 'DISABLED' }));
    });

    await r('UpdateScheduledQuery_Verify', async () => {
      const resp = await queryClient.send(new DescribeScheduledQueryCommand({ ScheduledQueryArn: sqArn }));
      if (!resp.ScheduledQuery) throw new Error('ScheduledQuery is null');
      if (resp.ScheduledQuery.State !== 'DISABLED')
        throw new Error(`expected state DISABLED, got ${resp.ScheduledQuery.State}`);
    });

    await r('ExecuteScheduledQuery', async () => {
      await queryClient.send(new ExecuteScheduledQueryCommand({ ScheduledQueryArn: sqArn, InvocationTime: new Date() }));
    });

    await r('TagResource_ScheduledQuery', async () => {
      await queryClient.send(new QueryTagResourceCommand({ ResourceARN: sqArn, Tags: [{ Key: 'extra', Value: 'tag' }] }));
    });

    await r('ListTagsForResource_ScheduledQuery', async () => {
      const resp = await queryClient.send(new QueryListTagsForResourceCommand({ ResourceARN: sqArn }));
      if (!resp.Tags || resp.Tags.length < 2) throw new Error(`expected at least 2 tags, got ${resp.Tags?.length ?? 0}`);
    });

    await r('UntagResource_ScheduledQuery', async () => {
      await queryClient.send(new QueryUntagResourceCommand({ ResourceARN: sqArn, TagKeys: ['extra'] }));
    });

    await r('DeleteScheduledQuery', async () => {
      await queryClient.send(new DeleteScheduledQueryCommand({ ScheduledQueryArn: sqArn }));
      sqArn = '';
    });

    await r('DescribeScheduledQuery_NonExistent', async () => {
      await assertThrows(async () => {
        await queryClient.send(new DescribeScheduledQueryCommand({
          ScheduledQueryArn: 'arn:aws:timestream:us-east-1:000000000000:scheduled-query/nonexistent',
        }));
      }, 'ResourceNotFoundException');
    });

    const dupSqName = makeUniqueName('dup-sq');
    await r('CreateScheduledQuery_Duplicate', async () => {
      let dupArn = '';
      try {
        const resp = await queryClient.send(new CreateScheduledQueryCommand({
          Name: dupSqName, QueryString: 'SELECT 1',
          ScheduleConfiguration: { ScheduleExpression: 'cron(0 0 * * ? *)' },
          NotificationConfiguration: { SnsConfiguration: { TopicArn: 'arn:aws:sns:us-east-1:000000000000:test-topic' } },
          ErrorReportConfiguration: { S3Configuration: { BucketName: 'error-report-bucket' } },
          ScheduledQueryExecutionRoleArn: `arn:aws:iam::000000000000:role/${sqRoleName}`,
        }));
        if (!resp.Arn) throw new Error('first create: Arn is null');
        dupArn = resp.Arn;
        try {
          await queryClient.send(new CreateScheduledQueryCommand({
            Name: dupSqName, QueryString: 'SELECT 1',
            ScheduleConfiguration: { ScheduleExpression: 'cron(0 0 * * ? *)' },
            NotificationConfiguration: { SnsConfiguration: { TopicArn: 'arn:aws:sns:us-east-1:000000000000:test-topic' } },
            ErrorReportConfiguration: { S3Configuration: { BucketName: 'error-report-bucket' } },
            ScheduledQueryExecutionRoleArn: `arn:aws:iam::000000000000:role/${sqRoleName}`,
          }));
          throw new Error('expected error for duplicate scheduled query');
        } catch (err: any) {
          if (err.message === 'expected error for duplicate scheduled query') throw err;
        }
      } finally {
        if (dupArn) await safeCleanup(() => queryClient.send(new DeleteScheduledQueryCommand({ ScheduledQueryArn: dupArn })));
      }
    });

    await r('DescribeAccountSettings', async () => {
      const resp = await queryClient.send(new DescribeAccountSettingsCommand({}));
      if (resp.MaxQueryTCU === undefined || resp.MaxQueryTCU === null)
        throw new Error('expected MaxQueryTCU to be set');
    });

    await r('UpdateAccountSettings', async () => {
      await queryClient.send(new UpdateAccountSettingsCommand({ MaxQueryTCU: 8, QueryPricingModel: 'COMPUTE_UNITS' }));
    });

    await r('DescribeAccountSettings_AfterUpdate', async () => {
      const resp = await queryClient.send(new DescribeAccountSettingsCommand({}));
      if (!resp.MaxQueryTCU || resp.MaxQueryTCU !== 8)
        throw new Error(`expected MaxQueryTCU=8, got ${resp.MaxQueryTCU}`);
    });

    await r('ScheduledQuery_Cleanup', async () => {
      if (sqSetup) {
        await safeCleanup(() => writeClient.send(new DeleteDatabaseCommand({ DatabaseName: sqDb })));
        sqSetup = false;
      }
      await safeCleanup(() => iamClient.send(new DeleteRoleCommand({ RoleName: sqRoleName })));
    });

    await r('WriteRecords_GetRecords_Roundtrip', async () => {
      const rtDb = makeUniqueName('rt-db');
      const rtTbl = makeUniqueName('rt-tbl');
      await writeClient.send(new CreateDatabaseCommand({ DatabaseName: rtDb }));
      try {
        await writeClient.send(new CreateTableCommand({ DatabaseName: rtDb, TableName: rtTbl }));
        const measureValue = `verify-${Date.now()}`;
        await writeClient.send(new WriteRecordsCommand({
          DatabaseName: rtDb, TableName: rtTbl,
          Records: [{ MeasureName: 'cpu_utilization', MeasureValue: measureValue, MeasureValueType: 'DOUBLE', Time: String(Date.now()), TimeUnit: 'MILLISECONDS' }],
        }));
        const queryResp = await queryClient.send(new QueryCommand({ QueryString: `SELECT * FROM "${rtDb}"."${rtTbl}"` }));
        if (!queryResp.Rows || queryResp.Rows.length === 0) throw new Error('query returned zero rows, expected at least 1');
      } finally {
        await safeCleanup(() => writeClient.send(new DeleteDatabaseCommand({ DatabaseName: rtDb })));
      }
    });
  } finally {
    if (sqArn) await safeCleanup(() => queryClient.send(new DeleteScheduledQueryCommand({ ScheduledQueryArn: sqArn })));
    if (sqSetup) await safeCleanup(() => writeClient.send(new DeleteDatabaseCommand({ DatabaseName: sqDb })));
    await safeCleanup(() => iamClient.send(new DeleteRoleCommand({ RoleName: sqRoleName })));
  }

  return results;
}
