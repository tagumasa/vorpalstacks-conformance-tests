import {
  CloudTrailClient,
  CreateTrailCommand,
  GetTrailCommand,
  UpdateTrailCommand,
  GetEventSelectorsCommand,
  PutEventSelectorsCommand,
  DeleteTrailCommand,
} from '@aws-sdk/client-cloudtrail';
import { ReadWriteType } from '@aws-sdk/client-cloudtrail';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function registerContentVerify(
  client: CloudTrailClient,
  runner: TestRunner,
  results: TestResult[],
): Promise<void> {
  let verifyTrailName = '';

  results.push(await runner.runTest('cloudtrail', 'CreateTrail_ContentVerify', async () => {
    verifyTrailName = makeUniqueName('verify-trail');
    const resp = await client.send(new CreateTrailCommand({
      Name: verifyTrailName,
      S3BucketName: 'verify-bucket',
      IncludeGlobalServiceEvents: true,
      IsMultiRegionTrail: false,
    }));
    if (resp.Name !== verifyTrailName) throw new Error('trail name mismatch');
    if (resp.S3BucketName !== 'verify-bucket') throw new Error('S3 bucket name mismatch');
  }));

  results.push(await runner.runTest('cloudtrail', 'UpdateTrail_VerifyChange', async () => {
    if (!verifyTrailName) throw new Error('trail name not available');
    await client.send(new UpdateTrailCommand({ Name: verifyTrailName, S3BucketName: 'updated-verify-bucket' }));
    const resp = await client.send(new GetTrailCommand({ Name: verifyTrailName }));
    if (!resp.Trail?.S3BucketName || resp.Trail.S3BucketName !== 'updated-verify-bucket') {
      throw new Error(`S3 bucket name not updated, got ${resp.Trail?.S3BucketName}`);
    }
  }));

  results.push(await runner.runTest('cloudtrail', 'PutEventSelectors_VerifyContent', async () => {
    if (!verifyTrailName) throw new Error('trail name not available');
    await client.send(new PutEventSelectorsCommand({
      TrailName: verifyTrailName,
      EventSelectors: [{
        ReadWriteType: ReadWriteType.ReadOnly,
        IncludeManagementEvents: false,
        DataResources: [{ Type: 'AWS::S3::Object', Values: ['arn:aws:s3:::'] }],
      }],
    }));
    const resp = await client.send(new GetEventSelectorsCommand({ TrailName: verifyTrailName }));
    if (!resp.EventSelectors || resp.EventSelectors.length !== 1) {
      throw new Error(`expected 1 event selector, got ${resp.EventSelectors?.length ?? 0}`);
    }
    if (resp.EventSelectors[0].ReadWriteType !== ReadWriteType.ReadOnly) {
      throw new Error(`ReadWriteType mismatch, got ${resp.EventSelectors[0].ReadWriteType}`);
    }
  }));

  if (verifyTrailName) {
    await safeCleanup(async () => {
      await client.send(new DeleteTrailCommand({ Name: verifyTrailName }));
    });
  }
}
