import {
  CloudWatchLogsClient,
  CreateLogGroupCommand,
  DeleteLogGroupCommand,
  PutSubscriptionFilterCommand,
  DescribeSubscriptionFiltersCommand,
  DeleteSubscriptionFilterCommand,
} from '@aws-sdk/client-cloudwatch-logs';
import { IAMClient, CreateRoleCommand, DeleteRoleCommand } from '@aws-sdk/client-iam';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

const LOGS_TRUST_POLICY = JSON.stringify({
  Version: '2012-10-17',
  Statement: [{ Effect: 'Allow', Principal: { Service: 'logs.amazonaws.com' }, Action: 'sts:AssumeRole' }],
});

export async function runSubscriptionFilterTests(
  runner: TestRunner,
  client: CloudWatchLogsClient,
  iamClient: IAMClient,
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('logs', 'PutSubscriptionFilter_Basic', async () => {
    const sfName = makeUniqueName('SFGroup');
    const roleName = makeUniqueName('test-sub-filter-role');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: sfName }));
      await iamClient.send(new CreateRoleCommand({
        RoleName: roleName,
        AssumeRolePolicyDocument: LOGS_TRUST_POLICY,
      }));
      await client.send(new PutSubscriptionFilterCommand({
        logGroupName: sfName,
        filterName: 'TestSub',
        filterPattern: 'ERROR',
        destinationArn: 'arn:aws:lambda:us-east-1:000000000000:function:test-func',
        roleArn: `arn:aws:iam::000000000000:role/${roleName}`,
        distribution: 'ByLogStream',
      }));
    } finally {
      await safeCleanup(() => iamClient.send(new DeleteRoleCommand({ RoleName: roleName })));
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: sfName })));
    }
  }));

  results.push(await runner.runTest('logs', 'DescribeSubscriptionFilters_Basic', async () => {
    const dsfName = makeUniqueName('DSFGroup');
    const roleName = makeUniqueName('test-desc-sub-role');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: dsfName }));
      await iamClient.send(new CreateRoleCommand({
        RoleName: roleName,
        AssumeRolePolicyDocument: LOGS_TRUST_POLICY,
      }));
      await client.send(new PutSubscriptionFilterCommand({
        logGroupName: dsfName,
        filterName: 'DescSub',
        filterPattern: 'ERROR',
        destinationArn: 'arn:aws:lambda:us-east-1:000000000000:function:test',
        roleArn: `arn:aws:iam::000000000000:role/${roleName}`,
      }));
      const resp = await client.send(new DescribeSubscriptionFiltersCommand({ logGroupName: dsfName }));
      if (resp.subscriptionFilters?.length !== 1) throw new Error(`expected 1 filter, got ${resp.subscriptionFilters?.length}`);
      if (resp.subscriptionFilters[0].filterName !== 'DescSub') throw new Error(`filter name mismatch: got ${resp.subscriptionFilters[0].filterName}`);
      if (resp.subscriptionFilters[0].destinationArn !== 'arn:aws:lambda:us-east-1:000000000000:function:test') throw new Error('destination arn mismatch');
    } finally {
      await safeCleanup(() => iamClient.send(new DeleteRoleCommand({ RoleName: roleName })));
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: dsfName })));
    }
  }));

  results.push(await runner.runTest('logs', 'DeleteSubscriptionFilter_Basic', async () => {
    const delSFName = makeUniqueName('DelSFGroup');
    const roleName = makeUniqueName('test-del-sub-role');
    try {
      await client.send(new CreateLogGroupCommand({ logGroupName: delSFName }));
      await iamClient.send(new CreateRoleCommand({
        RoleName: roleName,
        AssumeRolePolicyDocument: LOGS_TRUST_POLICY,
      }));
      await client.send(new PutSubscriptionFilterCommand({
        logGroupName: delSFName,
        filterName: 'DelSub',
        filterPattern: 'ERROR',
        destinationArn: 'arn:aws:lambda:us-east-1:000000000000:function:test',
        roleArn: `arn:aws:iam::000000000000:role/${roleName}`,
      }));
      await client.send(new DeleteSubscriptionFilterCommand({ logGroupName: delSFName, filterName: 'DelSub' }));
      const resp = await client.send(new DescribeSubscriptionFiltersCommand({ logGroupName: delSFName }));
      if (resp.subscriptionFilters?.length !== 0) throw new Error(`expected 0 filters after delete, got ${resp.subscriptionFilters?.length}`);
    } finally {
      await safeCleanup(() => iamClient.send(new DeleteRoleCommand({ RoleName: roleName })));
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName: delSFName })));
    }
  }));
}
