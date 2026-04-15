import {
  CloudTrailClient,
  GetTrailCommand,
  DeleteTrailCommand,
  StartLoggingCommand,
  StopLoggingCommand,
  DescribeTrailsCommand,
  UpdateTrailCommand,
  GetTrailStatusCommand,
  GetEventSelectorsCommand,
  PutEventSelectorsCommand,
  PutInsightSelectorsCommand,
  GetInsightSelectorsCommand,
  AddTagsCommand,
  RemoveTagsCommand,
  ListTagsCommand,
  PutResourcePolicyCommand,
  GetResourcePolicyCommand,
} from '@aws-sdk/client-cloudtrail';
import { ReadWriteType, InsightType } from '@aws-sdk/client-cloudtrail';
import type { TestRunner, TestResult } from '../../runner.js';
import { assertErrorContains } from '../../helpers.js';

export async function registerErrorTests(
  client: CloudTrailClient,
  runner: TestRunner,
  results: TestResult[],
): Promise<void> {

  results.push(await runner.runTest('cloudtrail', 'GetTrail_NonExistent', async () => {
    let err: unknown;
    try { await client.send(new GetTrailCommand({ Name: 'nonexistent-trail-xyz' })); } catch (e) { err = e; }
    assertErrorContains(err, 'TrailNotFoundException');
  }));

  results.push(await runner.runTest('cloudtrail', 'DeleteTrail_NonExistent', async () => {
    let err: unknown;
    try { await client.send(new DeleteTrailCommand({ Name: 'nonexistent-trail-xyz' })); } catch (e) { err = e; }
    assertErrorContains(err, 'TrailNotFoundException');
  }));

  results.push(await runner.runTest('cloudtrail', 'StartLogging_NonExistent', async () => {
    let err: unknown;
    try { await client.send(new StartLoggingCommand({ Name: 'nonexistent-trail-xyz' })); } catch (e) { err = e; }
    assertErrorContains(err, 'TrailNotFoundException');
  }));

  results.push(await runner.runTest('cloudtrail', 'DescribeTrails_NonExistent', async () => {
    const resp = await client.send(new DescribeTrailsCommand({ trailNameList: ['nonexistent-trail-xyz'] }));
    if (!resp.trailList || resp.trailList.length !== 0) {
      throw new Error(`expected empty trail list for non-existent trail, got ${resp.trailList?.length ?? 0}`);
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'StopLogging_NonExistent', async () => {
    let err: unknown;
    try { await client.send(new StopLoggingCommand({ Name: 'nonexistent-stop-xyz' })); } catch (e) { err = e; }
    assertErrorContains(err, 'TrailNotFoundException');
  }));

  results.push(await runner.runTest('cloudtrail', 'GetTrailStatus_NonExistent', async () => {
    let err: unknown;
    try { await client.send(new GetTrailStatusCommand({ Name: 'nonexistent-status-xyz' })); } catch (e) { err = e; }
    assertErrorContains(err, 'TrailNotFoundException');
  }));

  results.push(await runner.runTest('cloudtrail', 'UpdateTrail_NonExistent', async () => {
    let err: unknown;
    try { await client.send(new UpdateTrailCommand({ Name: 'nonexistent-update-xyz', S3BucketName: 'some-bucket' })); } catch (e) { err = e; }
    assertErrorContains(err, 'TrailNotFoundException');
  }));

  results.push(await runner.runTest('cloudtrail', 'GetEventSelectors_NonExistent', async () => {
    let err: unknown;
    try { await client.send(new GetEventSelectorsCommand({ TrailName: 'nonexistent-es-xyz' })); } catch (e) { err = e; }
    assertErrorContains(err, 'TrailNotFoundException');
  }));

  results.push(await runner.runTest('cloudtrail', 'PutEventSelectors_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new PutEventSelectorsCommand({
        TrailName: 'nonexistent-es-xyz',
        EventSelectors: [{ ReadWriteType: ReadWriteType.All }],
      }));
    } catch (e) { err = e; }
    assertErrorContains(err, 'TrailNotFoundException');
  }));

  results.push(await runner.runTest('cloudtrail', 'PutInsightSelectors_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new PutInsightSelectorsCommand({
        TrailName: 'nonexistent-is-xyz',
        InsightSelectors: [{ InsightType: InsightType.ApiCallRateInsight }],
      }));
    } catch (e) { err = e; }
    assertErrorContains(err, 'TrailNotFoundException');
  }));

  results.push(await runner.runTest('cloudtrail', 'GetInsightSelectors_NonExistent', async () => {
    let err: unknown;
    try { await client.send(new GetInsightSelectorsCommand({ TrailName: 'nonexistent-is-xyz' })); } catch (e) { err = e; }
    assertErrorContains(err, 'TrailNotFoundException');
  }));

  results.push(await runner.runTest('cloudtrail', 'AddTags_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new AddTagsCommand({
        ResourceId: 'arn:aws:cloudtrail:us-east-1:123456789012:trail/nonexistent-tag-xyz',
        TagsList: [{ Key: 'K', Value: 'V' }],
      }));
    } catch (e) { err = e; }
    assertErrorContains(err, 'TrailNotFoundException');
  }));

  results.push(await runner.runTest('cloudtrail', 'RemoveTags_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new RemoveTagsCommand({
        ResourceId: 'arn:aws:cloudtrail:us-east-1:123456789012:trail/nonexistent-rm-xyz',
        TagsList: [{ Key: 'K' }],
      }));
    } catch (e) { err = e; }
    assertErrorContains(err, 'TrailNotFoundException');
  }));

  results.push(await runner.runTest('cloudtrail', 'ListTags_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new ListTagsCommand({
        ResourceIdList: ['arn:aws:cloudtrail:us-east-1:123456789012:trail/nonexistent-lt-xyz'],
      }));
    } catch (e) { err = e; }
    assertErrorContains(err, 'TrailNotFoundException');
  }));

  results.push(await runner.runTest('cloudtrail', 'GetResourcePolicy_NonExistentTrail', async () => {
    let err: unknown;
    try {
      await client.send(new GetResourcePolicyCommand({
        ResourceArn: 'arn:aws:cloudtrail:us-east-1:123456789012:trail/nonexistent-grp-xyz',
      }));
    } catch (e) { err = e; }
    assertErrorContains(err, 'TrailNotFoundException');
  }));

  results.push(await runner.runTest('cloudtrail', 'PutResourcePolicy_NonExistentTrail', async () => {
    let err: unknown;
    try {
      await client.send(new PutResourcePolicyCommand({
        ResourceArn: 'arn:aws:cloudtrail:us-east-1:123456789012:trail/nonexistent-policy-trail',
        ResourcePolicy: '{"Version":"2012-10-17"}',
      }));
    } catch (e) { err = e; }
    assertErrorContains(err, 'TrailNotFoundException');
  }));
}
