import {
  PutObjectCommand,
  GetObjectCommand,
  HeadObjectCommand,
  ListObjectsV2Command,
  DeleteObjectCommand,
  CopyObjectCommand,
  CreateBucketCommand,
} from '@aws-sdk/client-s3';
import { S3TestContext, S3TestSection, s3BucketName, s3CleanupBucket } from './context.js';
import { assertNotNil } from '../../helpers.js';

export const runObjectDataTests: S3TestSection = async (ctx, runner) => {
  const results = [];
  const { client, ts, bucketName } = ctx;

  results.push(await runner.runTest('s3', 'PutObject', async () => {
    const resp = await client.send(new PutObjectCommand({
      Bucket: bucketName,
      Key: 'test.txt',
      Body: 'Hello, World!',
    }));
    assertNotNil(resp.ETag, 'ETag');
  }));

  results.push(await runner.runTest('s3', 'GetObject', async () => {
    const resp = await client.send(new GetObjectCommand({
      Bucket: bucketName,
      Key: 'test.txt',
    }));
    assertNotNil(resp.Body, 'Body');
    resp.Body.transformToByteArray();
  }));

  results.push(await runner.runTest('s3', 'HeadObject', async () => {
    const resp = await client.send(new HeadObjectCommand({
      Bucket: bucketName,
      Key: 'test.txt',
    }));
    assertNotNil(resp.ContentLength, 'ContentLength');
    assertNotNil(resp.ETag, 'ETag');
  }));

  results.push(await runner.runTest('s3', 'ListObjectsV2', async () => {
    const resp = await client.send(new ListObjectsV2Command({ Bucket: bucketName }));
    assertNotNil(resp.Contents, 'Contents');
  }));

  results.push(await runner.runTest('s3', 'DeleteObject', async () => {
    await client.send(new DeleteObjectCommand({
      Bucket: bucketName,
      Key: 'test.txt',
    }));
  }));

  results.push(await runner.runTest('s3', 'ListObjectsAfterDelete', async () => {
    const resp = await client.send(new ListObjectsV2Command({ Bucket: bucketName }));
    if (resp.Contents && resp.Contents.length > 0) {
      const keys = resp.Contents.map((o) => o.Key).join(', ');
      throw new Error(`bucket ${bucketName} not empty after delete, objects: ${keys}`);
    }
  }));

  results.push(await runner.runTest('s3', 'CopyObject', async () => {
    const srcKey = 'copy-source.txt';
    const dstKey = 'copy-dest.txt';
    const content = 'copy me';

    await client.send(new PutObjectCommand({
      Bucket: bucketName,
      Key: srcKey,
      Body: content,
    }));

    await client.send(new CopyObjectCommand({
      Bucket: bucketName,
      Key: dstKey,
      CopySource: `${bucketName}/${srcKey}`,
    }));

    const resp = await client.send(new GetObjectCommand({
      Bucket: bucketName,
      Key: dstKey,
    }));
    const body = await resp.Body!.transformToString();
    if (body !== content) {
      throw new Error(`copy content mismatch: got "${body}", want "${content}"`);
    }
  }));

  results.push(await runner.runTest('s3', 'PutObject_GetObject_ContentVerification', async () => {
    const content = 'Hello, S3 content verification! Japanese test';
    const key = 'verify-content.txt';
    await client.send(new PutObjectCommand({
      Bucket: bucketName,
      Key: key,
      Body: content,
      ContentType: 'text/plain; charset=utf-8',
      ContentLength: Buffer.byteLength(content),
    }));

    const resp = await client.send(new GetObjectCommand({
      Bucket: bucketName,
      Key: key,
    }));
    const body = await resp.Body!.transformToString();
    if (body !== content) {
      throw new Error(`content mismatch: got "${body}", want "${content}"`);
    }

    if (resp.ContentLength !== undefined && resp.ContentLength !== Buffer.byteLength(content)) {
      throw new Error(`ContentLength mismatch: got ${resp.ContentLength}, want ${Buffer.byteLength(content)}`);
    }
  }));

  results.push(await runner.runTest('s3', 'PutObject_Overwrite', async () => {
    const key = 'overwrite-test.txt';
    const content1 = 'Original content';
    const content2 = 'Updated content';

    await client.send(new PutObjectCommand({
      Bucket: bucketName,
      Key: key,
      Body: content1,
    }));

    await client.send(new PutObjectCommand({
      Bucket: bucketName,
      Key: key,
      Body: content2,
    }));

    const resp = await client.send(new GetObjectCommand({
      Bucket: bucketName,
      Key: key,
    }));
    const body = await resp.Body!.transformToString();
    if (body !== content2) {
      throw new Error(`after overwrite expected "${content2}", got "${body}"`);
    }
  }));

  results.push(await runner.runTest('s3', 'HeadObject_VerifyMetadata', async () => {
    const key = 'metadata-test.txt';
    const content = 'metadata check';
    await client.send(new PutObjectCommand({
      Bucket: bucketName,
      Key: key,
      Body: content,
      ContentType: 'application/json',
      ContentLength: Buffer.byteLength(content),
      Metadata: {
        'custom-key': 'custom-value',
      },
    }));

    const resp = await client.send(new HeadObjectCommand({
      Bucket: bucketName,
      Key: key,
    }));
    if (resp.ContentType !== 'application/json') {
      throw new Error(`ContentType mismatch, got ${resp.ContentType}`);
    }
    if (resp.ContentLength !== undefined && resp.ContentLength !== Buffer.byteLength(content)) {
      throw new Error(`ContentLength mismatch, got ${resp.ContentLength}`);
    }
  }));

  results.push(await runner.runTest('s3', 'ListObjectsV2_MultipleObjects', async () => {
    const lobBucket = s3BucketName(ts, 'list');
    await client.send(new CreateBucketCommand({ Bucket: lobBucket }));
    try {
      for (const i of [0, 1, 2, 3, 4]) {
        const key = `obj${i}.txt`;
        await client.send(new PutObjectCommand({
          Bucket: lobBucket,
          Key: key,
          Body: `content ${i}`,
        }));
      }

      const resp = await client.send(new ListObjectsV2Command({ Bucket: lobBucket }));
      if (!resp.Contents || resp.Contents.length !== 5) {
        throw new Error(`expected 5 contents, got ${resp.Contents?.length ?? 0}`);
      }
      for (const obj of resp.Contents) {
        if (obj.Key === undefined || obj.Key === null) {
          throw new Error('expected key to be defined');
        }
        if (obj.Size === undefined || obj.Size === 0) {
          throw new Error(`expected non-zero size for ${obj.Key}`);
        }
      }
    } finally {
      await s3CleanupBucket(client, lobBucket);
    }
  }));

  return results;
};
