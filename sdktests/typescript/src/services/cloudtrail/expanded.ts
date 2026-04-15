import {
  CloudTrailClient,
  CreateTrailCommand,
  GetTrailCommand,
  DescribeTrailsCommand,
  StartLoggingCommand,
  StopLoggingCommand,
  GetTrailStatusCommand,
  UpdateTrailCommand,
  GetEventSelectorsCommand,
  PutEventSelectorsCommand,
  ListPublicKeysCommand,
  PutInsightSelectorsCommand,
  GetInsightSelectorsCommand,
  PutResourcePolicyCommand,
  GetResourcePolicyCommand,
  DeleteResourcePolicyCommand,
  AddTagsCommand,
  ListTagsCommand,
  RemoveTagsCommand,
  LookupEventsCommand,
  DeleteTrailCommand,
} from '@aws-sdk/client-cloudtrail';
import { ReadWriteType, InsightType } from '@aws-sdk/client-cloudtrail';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup, assertErrorContains } from '../../helpers.js';

export async function registerExpanded(
  client: CloudTrailClient,
  runner: TestRunner,
  results: TestResult[],
): Promise<void> {

  results.push(await runner.runTest('cloudtrail', 'CreateTrail_DefaultFields', async () => {
    const name = makeUniqueName('defaults');
    try {
      const resp = await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'defaults-bucket' }));
      if (resp.IncludeGlobalServiceEvents !== true) throw new Error('IncludeGlobalServiceEvents should default to true');
      if (resp.IsMultiRegionTrail === true) throw new Error('IsMultiRegionTrail should default to false');
      if (resp.LogFileValidationEnabled === true) throw new Error('LogFileValidationEnabled should default to false');
      if (resp.IsOrganizationTrail === true) throw new Error('IsOrganizationTrail should default to false');
      if (!resp.TrailARN) throw new Error('TrailARN should be set');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'CreateTrail_Duplicate', async () => {
    const name = makeUniqueName('dup-trail');
    try {
      await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'dup-bucket' }));
      let err: unknown;
      try { await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'dup-bucket' })); } catch (e) { err = e; }
      assertErrorContains(err, 'TrailAlreadyExists');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'GetTrail_ByARN', async () => {
    const name = makeUniqueName('arn-trail');
    try {
      const cr = await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'arn-bucket' }));
      if (!cr.TrailARN) throw new Error('expected TrailARN to be defined');
      const gr = await client.send(new GetTrailCommand({ Name: cr.TrailARN }));
      if (!gr.Trail || gr.Trail.Name !== name) throw new Error('trail name mismatch after get by ARN');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'DescribeTrails_ByARN', async () => {
    const name = makeUniqueName('desc-arn');
    try {
      const cr = await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'desc-arn-bucket' }));
      if (!cr.TrailARN) throw new Error('expected TrailARN to be defined');
      const resp = await client.send(new DescribeTrailsCommand({ trailNameList: [cr.TrailARN] }));
      if (!resp.trailList || resp.trailList.length !== 1) {
        throw new Error(`expected 1 trail, got ${resp.trailList?.length ?? 0}`);
      }
      if (resp.trailList[0].Name !== name) throw new Error('trail name mismatch');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'DescribeTrails_ListAll', async () => {
    const resp = await client.send(new DescribeTrailsCommand({}));
    if (!resp.trailList) throw new Error('expected trailList to be defined');
  }));

  results.push(await runner.runTest('cloudtrail', 'GetTrailStatus_AfterStart', async () => {
    const name = makeUniqueName('status');
    try {
      await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'status-bucket' }));
      await client.send(new StartLoggingCommand({ Name: name }));
      const s = await client.send(new GetTrailStatusCommand({ Name: name }));
      if (s.IsLogging !== true) throw new Error('expected IsLogging=true after StartLogging');
      if (!s.StartLoggingTime) throw new Error('expected StartLoggingTime to be set');
      if (!s.LatestDeliveryTime) throw new Error('expected LatestDeliveryTime when logging is active');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'GetTrailStatus_AfterStop', async () => {
    const name = makeUniqueName('stopstat');
    try {
      await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'stopstat-bucket' }));
      await client.send(new StartLoggingCommand({ Name: name }));
      await client.send(new StopLoggingCommand({ Name: name }));
      const s = await client.send(new GetTrailStatusCommand({ Name: name }));
      if (s.IsLogging === true) throw new Error('expected IsLogging=false after StopLogging');
      if (!s.StopLoggingTime) throw new Error('expected StopLoggingTime to be set');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'UpdateTrail_EnableLogFileValidation', async () => {
    const name = makeUniqueName('lfv');
    try {
      await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'lfv-bucket' }));
      const resp = await client.send(new UpdateTrailCommand({ Name: name, EnableLogFileValidation: true }));
      if (resp.LogFileValidationEnabled !== true) throw new Error('expected LogFileValidationEnabled=true');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'CreateTrail_WithLogFileValidation', async () => {
    const name = makeUniqueName('lfv-create');
    try {
      const resp = await client.send(new CreateTrailCommand({
        Name: name, S3BucketName: 'lfv-create-bucket',
        EnableLogFileValidation: true, IncludeGlobalServiceEvents: true, IsMultiRegionTrail: true,
      }));
      if (resp.LogFileValidationEnabled !== true) throw new Error('expected LogFileValidationEnabled=true');
      if (resp.IncludeGlobalServiceEvents !== true) throw new Error('expected IncludeGlobalServiceEvents=true');
      if (resp.IsMultiRegionTrail !== true) throw new Error('expected IsMultiRegionTrail=true');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'ListPublicKeys', async () => {
    const resp = await client.send(new ListPublicKeysCommand({}));
    if (!resp.PublicKeyList) throw new Error('expected PublicKeyList to be defined');
    if (resp.PublicKeyList.length === 0) throw new Error('expected at least 1 public key (from previous LogFileValidation trail)');
    const pk = resp.PublicKeyList[0];
    if (!pk.Fingerprint) throw new Error('expected non-empty Fingerprint');
    if (!pk.Value || pk.Value.length === 0) throw new Error('expected non-empty Value (DER bytes)');
    const b64 = Buffer.from(pk.Value).toString('base64');
    try { Buffer.from(b64, 'base64'); } catch { throw new Error('Value should be base64-decodable DER bytes'); }
    if (!pk.ValidityStartTime) throw new Error('expected ValidityStartTime to be set');
    if (!pk.ValidityEndTime) throw new Error('expected ValidityEndTime to be set');
    if (pk.ValidityEndTime < pk.ValidityStartTime) throw new Error('ValidityEndTime should be after ValidityStartTime');
  }));

  results.push(await runner.runTest('cloudtrail', 'ListPublicKeys_TimeFilter', async () => {
    const now = new Date();
    const resp = await client.send(new ListPublicKeysCommand({
      StartTime: new Date(now.getTime() - 3600000),
      EndTime: new Date(now.getTime() + 3600000),
    }));
    if (!resp.PublicKeyList) throw new Error('expected PublicKeyList to be defined');
    if (resp.PublicKeyList.length === 0) throw new Error('expected at least 1 public key in time range');
  }));

  results.push(await runner.runTest('cloudtrail', 'ListPublicKeys_OutsideTimeRange', async () => {
    const ff = new Date(Date.now() + 10 * 365 * 24 * 3600000);
    const resp = await client.send(new ListPublicKeysCommand({
      StartTime: ff,
      EndTime: new Date(ff.getTime() + 3600000),
    }));
    if (!resp.PublicKeyList) throw new Error('expected PublicKeyList to be defined');
    if (resp.PublicKeyList.length !== 0) throw new Error(`expected 0 public keys outside validity range, got ${resp.PublicKeyList.length}`);
  }));

  results.push(await runner.runTest('cloudtrail', 'PutEventSelectors_ExcludeManagementEventSources', async () => {
    const name = makeUniqueName('emes');
    try {
      await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'emes-bucket' }));
      await client.send(new PutEventSelectorsCommand({
        TrailName: name,
        EventSelectors: [{
          ReadWriteType: ReadWriteType.All,
          IncludeManagementEvents: true,
          ExcludeManagementEventSources: ['kms.amazonaws.com'],
        }],
      }));
      const resp = await client.send(new GetEventSelectorsCommand({ TrailName: name }));
      if (!resp.EventSelectors || resp.EventSelectors.length !== 1) {
        throw new Error(`expected 1 selector, got ${resp.EventSelectors?.length ?? 0}`);
      }
      const es = resp.EventSelectors[0];
      if (!es.ExcludeManagementEventSources || es.ExcludeManagementEventSources.length !== 1) {
        throw new Error(`expected 1 ExcludeManagementEventSource, got ${es.ExcludeManagementEventSources?.length ?? 0}`);
      }
      if (es.ExcludeManagementEventSources[0] !== 'kms.amazonaws.com') {
        throw new Error(`expected kms.amazonaws.com, got ${es.ExcludeManagementEventSources[0]}`);
      }
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'GetEventSelectors_DefaultValues', async () => {
    const name = makeUniqueName('def-es');
    try {
      await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'def-es-bucket' }));
      const resp = await client.send(new GetEventSelectorsCommand({ TrailName: name }));
      if (!resp.EventSelectors || resp.EventSelectors.length !== 1) {
        throw new Error(`expected 1 default event selector, got ${resp.EventSelectors?.length ?? 0}`);
      }
      const es = resp.EventSelectors[0];
      if (es.ReadWriteType !== ReadWriteType.All) throw new Error(`expected default ReadWriteType=All, got ${es.ReadWriteType}`);
      if (es.IncludeManagementEvents !== true) throw new Error('expected default IncludeManagementEvents=true');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'PutInsightSelectors', async () => {
    const name = makeUniqueName('insight');
    try {
      await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'insight-bucket' }));
      const resp = await client.send(new PutInsightSelectorsCommand({
        TrailName: name,
        InsightSelectors: [{ InsightType: InsightType.ApiCallRateInsight }],
      }));
      if (!resp.InsightSelectors || resp.InsightSelectors.length !== 1) throw new Error('expected 1 insight selector in response');
      if (resp.InsightSelectors[0].InsightType !== InsightType.ApiCallRateInsight) throw new Error('insight type mismatch');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'GetInsightSelectors', async () => {
    const name = makeUniqueName('get-insight');
    try {
      await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'get-insight-bucket' }));
      await client.send(new PutInsightSelectorsCommand({
        TrailName: name,
        InsightSelectors: [{ InsightType: InsightType.ApiErrorRateInsight }],
      }));
      const resp = await client.send(new GetInsightSelectorsCommand({ TrailName: name }));
      if (!resp.InsightSelectors || resp.InsightSelectors.length !== 1) {
        throw new Error(`expected 1 insight selector, got ${resp.InsightSelectors?.length ?? 0}`);
      }
      if (resp.InsightSelectors[0].InsightType !== InsightType.ApiErrorRateInsight) {
        throw new Error(`expected ApiErrorRateInsight, got ${resp.InsightSelectors[0].InsightType}`);
      }
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'GetInsightSelectors_Empty', async () => {
    const name = makeUniqueName('empty-insight');
    try {
      await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'empty-insight-bucket' }));
      const resp = await client.send(new GetInsightSelectorsCommand({ TrailName: name }));
      if (resp.InsightSelectors && resp.InsightSelectors.length !== 0) {
        throw new Error(`expected 0 insight selectors for new trail, got ${resp.InsightSelectors.length}`);
      }
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'PutResourcePolicy_GetResourcePolicy', async () => {
    const name = makeUniqueName('policy');
    try {
      const cr = await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'policy-bucket' }));
      if (!cr.TrailARN) throw new Error('expected TrailARN to be defined');
      const policyDoc = '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":"*","Action":"cloudtrail:GetTrail","Resource":"*"}]}';
      const pr = await client.send(new PutResourcePolicyCommand({ ResourceArn: cr.TrailARN, ResourcePolicy: policyDoc }));
      if (pr.ResourceArn !== cr.TrailARN) throw new Error('resource ARN mismatch in put response');
      const gr = await client.send(new GetResourcePolicyCommand({ ResourceArn: cr.TrailARN }));
      if (gr.ResourcePolicy !== policyDoc) throw new Error('policy content mismatch');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'GetResourcePolicy_NotFound', async () => {
    const name = makeUniqueName('nopolicy');
    try {
      const cr = await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'nopolicy-bucket' }));
      if (!cr.TrailARN) throw new Error('expected TrailARN to be defined');
      const resp = await client.send(new GetResourcePolicyCommand({ ResourceArn: cr.TrailARN }));
      if (resp.ResourcePolicy) throw new Error(`expected empty policy, got: ${resp.ResourcePolicy}`);
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'DeleteResourcePolicy', async () => {
    const name = makeUniqueName('delpolicy');
    try {
      const cr = await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'delpolicy-bucket' }));
      if (!cr.TrailARN) throw new Error('expected TrailARN to be defined');
      await client.send(new PutResourcePolicyCommand({ ResourceArn: cr.TrailARN, ResourcePolicy: '{"Version":"2012-10-17","Statement":[]}' }));
      await client.send(new DeleteResourcePolicyCommand({ ResourceArn: cr.TrailARN }));
      const resp = await client.send(new GetResourcePolicyCommand({ ResourceArn: cr.TrailARN }));
      if (resp.ResourcePolicy) throw new Error('expected empty policy after delete');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'Tags_Lifecycle', async () => {
    const name = makeUniqueName('tagcycle');
    try {
      const cr = await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'tagcycle-bucket' }));
      if (!cr.TrailARN) throw new Error('expected TrailARN to be defined');
      await client.send(new AddTagsCommand({
        ResourceId: cr.TrailARN,
        TagsList: [
          { Key: 'Key1', Value: 'Value1' },
          { Key: 'Key2', Value: 'Value2' },
          { Key: 'Key3', Value: 'Value3' },
        ],
      }));
      const l1 = await client.send(new ListTagsCommand({ ResourceIdList: [cr.TrailARN] }));
      if (!l1.ResourceTagList?.[0]?.TagsList || l1.ResourceTagList[0].TagsList.length !== 3) {
        throw new Error(`expected 3 tags, got ${l1.ResourceTagList?.[0]?.TagsList?.length ?? 0}`);
      }
      await client.send(new RemoveTagsCommand({ ResourceId: cr.TrailARN, TagsList: [{ Key: 'Key2' }] }));
      const l2 = await client.send(new ListTagsCommand({ ResourceIdList: [cr.TrailARN] }));
      if (!l2.ResourceTagList?.[0]?.TagsList || l2.ResourceTagList[0].TagsList.length !== 2) {
        throw new Error(`expected 2 tags after remove, got ${l2.ResourceTagList?.[0]?.TagsList?.length ?? 0}`);
      }
      await client.send(new AddTagsCommand({ ResourceId: cr.TrailARN, TagsList: [{ Key: 'Key1', Value: 'UpdatedValue1' }] }));
      const l3 = await client.send(new ListTagsCommand({ ResourceIdList: [cr.TrailARN] }));
      const found = l3.ResourceTagList?.[0]?.TagsList?.some((t) => t.Key === 'Key1' && t.Value === 'UpdatedValue1');
      if (!found) throw new Error('expected Key1=UpdatedValue1 after update');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'CreateTrail_WithTags', async () => {
    const name = makeUniqueName('tagtrail');
    try {
      await client.send(new CreateTrailCommand({
        Name: name, S3BucketName: 'tagtrail-bucket',
        TagsList: [{ Key: 'CreatedBy', Value: 'sdk-test' }, { Key: 'Env', Value: 'test' }],
      }));
      const r = await client.send(new GetTrailCommand({ Name: name }));
      if (!r.Trail?.TrailARN) throw new Error('expected TrailARN to be defined');
      const l = await client.send(new ListTagsCommand({ ResourceIdList: [r.Trail.TrailARN] }));
      if (!l.ResourceTagList?.[0]?.TagsList || l.ResourceTagList[0].TagsList.length !== 2) {
        throw new Error(`expected 2 tags, got ${l.ResourceTagList?.[0]?.TagsList?.length ?? 0}`);
      }
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'LookupEvents_WithTimeRange', async () => {
    const now = new Date();
    const resp = await client.send(new LookupEventsCommand({
      StartTime: new Date(now.getTime() - 3600000),
      EndTime: now,
      MaxResults: 5,
    }));
    if (!resp.Events) throw new Error('expected Events to be defined');
  }));

  results.push(await runner.runTest('cloudtrail', 'GetTrailStatus_DefaultIsNotLogging', async () => {
    const name = makeUniqueName('islog');
    try {
      await client.send(new CreateTrailCommand({ Name: name, S3BucketName: 'islog-bucket' }));
      const s = await client.send(new GetTrailStatusCommand({ Name: name }));
      if (s.IsLogging === true) throw new Error('expected IsLogging=false for newly created trail');
      if (s.LatestDeliveryTime) throw new Error('expected undefined LatestDeliveryTime when not logging');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteTrailCommand({ Name: name })); });
    }
  }));
}
