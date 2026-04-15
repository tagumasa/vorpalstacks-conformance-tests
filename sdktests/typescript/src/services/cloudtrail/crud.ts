import {
  CloudTrailClient,
  ListTrailsCommand,
  CreateTrailCommand,
  GetTrailCommand,
  DescribeTrailsCommand,
  StartLoggingCommand,
  StopLoggingCommand,
  GetTrailStatusCommand,
  UpdateTrailCommand,
  GetEventSelectorsCommand,
  PutEventSelectorsCommand,
  AddTagsCommand,
  ListTagsCommand,
  RemoveTagsCommand,
  LookupEventsCommand,
  DeleteTrailCommand,
} from '@aws-sdk/client-cloudtrail';
import { ReadWriteType } from '@aws-sdk/client-cloudtrail';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName } from '../../helpers.js';

export async function registerCRUD(
  client: CloudTrailClient,
  runner: TestRunner,
  results: TestResult[],
): Promise<void> {
  const trailName = makeUniqueName('test-trail');

  try {
    results.push(await runner.runTest('cloudtrail', 'ListTrails', async () => {
      const resp = await client.send(new ListTrailsCommand({}));
      if (!resp.Trails) throw new Error('expected Trails to be defined');
    }));

    results.push(await runner.runTest('cloudtrail', 'CreateTrail', async () => {
      const resp = await client.send(new CreateTrailCommand({
        Name: trailName,
        S3BucketName: 'test-bucket',
        IncludeGlobalServiceEvents: true,
        IsMultiRegionTrail: true,
      }));
      if (!resp.Name) throw new Error('expected Name to be defined');
    }));

    results.push(await runner.runTest('cloudtrail', 'GetTrail', async () => {
      const resp = await client.send(new GetTrailCommand({ Name: trailName }));
      if (!resp.Trail) throw new Error('expected Trail to be defined');
    }));

    results.push(await runner.runTest('cloudtrail', 'DescribeTrails', async () => {
      const resp = await client.send(new DescribeTrailsCommand({ trailNameList: [trailName] }));
      if (!resp.trailList) throw new Error('expected trailList to be defined');
    }));

    results.push(await runner.runTest('cloudtrail', 'StartLogging', async () => {
      await client.send(new StartLoggingCommand({ Name: trailName }));
    }));

    results.push(await runner.runTest('cloudtrail', 'StopLogging', async () => {
      await client.send(new StopLoggingCommand({ Name: trailName }));
    }));

    results.push(await runner.runTest('cloudtrail', 'GetTrailStatus', async () => {
      const resp = await client.send(new GetTrailStatusCommand({ Name: trailName }));
      if (!resp) throw new Error('expected response to be defined');
    }));

    results.push(await runner.runTest('cloudtrail', 'UpdateTrail', async () => {
      const resp = await client.send(new UpdateTrailCommand({ Name: trailName, S3BucketName: 'updated-bucket' }));
      if (!resp) throw new Error('expected response to be defined');
    }));

    results.push(await runner.runTest('cloudtrail', 'GetEventSelectors', async () => {
      const resp = await client.send(new GetEventSelectorsCommand({ TrailName: trailName }));
      if (!resp.EventSelectors) throw new Error('expected EventSelectors to be defined');
    }));

    results.push(await runner.runTest('cloudtrail', 'PutEventSelectors', async () => {
      await client.send(new PutEventSelectorsCommand({
        TrailName: trailName,
        EventSelectors: [{ ReadWriteType: ReadWriteType.All, IncludeManagementEvents: true }],
      }));
    }));

    let trailARN = '';
    const getResp = await client.send(new GetTrailCommand({ Name: trailName }));
    if (getResp.Trail?.TrailARN) trailARN = getResp.Trail.TrailARN;
    const tagResourceId = trailARN || trailName;

    results.push(await runner.runTest('cloudtrail', 'AddTags', async () => {
      await client.send(new AddTagsCommand({
        ResourceId: tagResourceId,
        TagsList: [
          { Key: 'Environment', Value: 'test' },
          { Key: 'Owner', Value: 'test-user' },
        ],
      }));
    }));

    results.push(await runner.runTest('cloudtrail', 'ListTags', async () => {
      const resp = await client.send(new ListTagsCommand({ ResourceIdList: [tagResourceId] }));
      if (!resp.ResourceTagList) throw new Error('expected ResourceTagList to be defined');
    }));

    results.push(await runner.runTest('cloudtrail', 'RemoveTags', async () => {
      await client.send(new RemoveTagsCommand({
        ResourceId: tagResourceId,
        TagsList: [{ Key: 'Environment' }],
      }));
    }));

    results.push(await runner.runTest('cloudtrail', 'LookupEvents', async () => {
      const resp = await client.send(new LookupEventsCommand({ MaxResults: 10 }));
      if (!resp.Events) throw new Error('expected Events to be defined');
    }));

    results.push(await runner.runTest('cloudtrail', 'DeleteTrail', async () => {
      await client.send(new DeleteTrailCommand({ Name: trailName }));
    }));
  } catch {
    await client.send(new DeleteTrailCommand({ Name: trailName })).catch(() => {});
  }
}
