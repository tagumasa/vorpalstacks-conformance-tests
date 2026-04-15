import {
  PutObjectCommand,
  PutObjectTaggingCommand,
  GetObjectTaggingCommand,
  DeleteObjectTaggingCommand,
  GetObjectAclCommand,
  PutObjectAclCommand,
  PutObjectLegalHoldCommand,
  GetObjectLegalHoldCommand,
  PutObjectRetentionCommand,
  GetObjectRetentionCommand,
  GetObjectAttributesCommand,
  ObjectAttributes,
  ObjectCannedACL,
} from '@aws-sdk/client-s3';
import { S3TestContext, S3TestSection } from './context.js';
import { assertNotNil } from '../../helpers.js';

export const runObjectConfigTests: S3TestSection = async (ctx, runner) => {
  const results = [];
  const { client, bucketName, lockBucket } = ctx;

  // ========== OBJECT TAGGING ==========
  await client.send(new PutObjectCommand({
    Bucket: bucketName,
    Key: 'tagged-obj.txt',
    Body: 'tag me',
  }));

  results.push(await runner.runTest('s3', 'PutObjectTagging', async () => {
    await client.send(new PutObjectTaggingCommand({
      Bucket: bucketName,
      Key: 'tagged-obj.txt',
      Tagging: {
        TagSet: [
          { Key: 'env', Value: 'prod' },
          { Key: 'team', Value: 'backend' },
        ],
      },
    }));
  }));

  results.push(await runner.runTest('s3', 'GetObjectTagging', async () => {
    const resp = await client.send(new GetObjectTaggingCommand({
      Bucket: bucketName,
      Key: 'tagged-obj.txt',
    }));
    if (!resp.TagSet || resp.TagSet.length !== 2) {
      throw new Error(`expected 2 tags, got ${resp.TagSet?.length ?? 0}`);
    }
  }));

  results.push(await runner.runTest('s3', 'DeleteObjectTagging', async () => {
    await client.send(new DeleteObjectTaggingCommand({
      Bucket: bucketName,
      Key: 'tagged-obj.txt',
    }));
  }));

  results.push(await runner.runTest('s3', 'GetObjectTagging_Empty', async () => {
    const resp = await client.send(new GetObjectTaggingCommand({
      Bucket: bucketName,
      Key: 'tagged-obj.txt',
    }));
    if (resp.TagSet && resp.TagSet.length !== 0) {
      throw new Error(`expected 0 tags after delete, got ${resp.TagSet.length}`);
    }
  }));

  // ========== OBJECT ACL ==========
  results.push(await runner.runTest('s3', 'GetObjectAcl', async () => {
    const resp = await client.send(new GetObjectAclCommand({
      Bucket: bucketName,
      Key: 'tagged-obj.txt',
    }));
    assertNotNil(resp.Owner, 'Owner');
  }));

  results.push(await runner.runTest('s3', 'PutObjectAcl', async () => {
    await client.send(new PutObjectAclCommand({
      Bucket: bucketName,
      Key: 'tagged-obj.txt',
      ACL: ObjectCannedACL.private,
    }));
  }));

  // ========== OBJECT LEGAL HOLD ==========
  await client.send(new PutObjectCommand({
    Bucket: lockBucket,
    Key: 'legal-hold-obj.txt',
    Body: 'legal hold test',
  }));

  results.push(await runner.runTest('s3', 'PutObjectLegalHold', async () => {
    await client.send(new PutObjectLegalHoldCommand({
      Bucket: lockBucket,
      Key: 'legal-hold-obj.txt',
      LegalHold: {
        Status: 'ON',
      },
    }));
  }));

  results.push(await runner.runTest('s3', 'GetObjectLegalHold', async () => {
    const resp = await client.send(new GetObjectLegalHoldCommand({
      Bucket: lockBucket,
      Key: 'legal-hold-obj.txt',
    }));
    assertNotNil(resp.LegalHold, 'LegalHold');
    if (resp.LegalHold.Status !== 'ON') {
      throw new Error(`expected ON, got ${resp.LegalHold.Status}`);
    }
  }));

  // ========== OBJECT RETENTION ==========
  await client.send(new PutObjectCommand({
    Bucket: lockBucket,
    Key: 'retention-obj.txt',
    Body: 'retention test',
  }));

  const retainUntil = new Date(Date.now() + 24 * 60 * 60 * 1000);
  results.push(await runner.runTest('s3', 'PutObjectRetention', async () => {
    await client.send(new PutObjectRetentionCommand({
      Bucket: lockBucket,
      Key: 'retention-obj.txt',
      Retention: {
        Mode: 'GOVERNANCE',
        RetainUntilDate: retainUntil,
      },
    }));
  }));

  results.push(await runner.runTest('s3', 'GetObjectRetention', async () => {
    const resp = await client.send(new GetObjectRetentionCommand({
      Bucket: lockBucket,
      Key: 'retention-obj.txt',
    }));
    assertNotNil(resp.Retention, 'Retention');
    if (resp.Retention.Mode !== 'GOVERNANCE') {
      throw new Error(`expected GOVERNANCE, got ${resp.Retention.Mode}`);
    }
  }));

  // ========== GET OBJECT ATTRIBUTES ==========
  await client.send(new PutObjectCommand({
    Bucket: bucketName,
    Key: 'attrs-obj.txt',
    Body: 'object attributes test content',
  }));

  results.push(await runner.runTest('s3', 'GetObjectAttributes', async () => {
    const resp = await client.send(new GetObjectAttributesCommand({
      Bucket: bucketName,
      Key: 'attrs-obj.txt',
      ObjectAttributes: [
        ObjectAttributes.OBJECT_SIZE,
        ObjectAttributes.ETAG,
        ObjectAttributes.STORAGE_CLASS,
      ],
    }));
    if (resp.ObjectSize === undefined || resp.ObjectSize === 0) {
      throw new Error('expected ObjectSize to be non-zero');
    }
    assertNotNil(resp.ETag, 'ETag');
  }));

  return results;
};
