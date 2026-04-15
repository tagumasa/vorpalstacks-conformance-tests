import {
  HeadBucketCommand,
  GetObjectCommand,
  HeadObjectCommand,
  DeleteObjectCommand,
  DeleteBucketCommand,
  PutObjectCommand,
  CreateBucketCommand,
} from '@aws-sdk/client-s3';
import { S3TestContext, S3TestSection, s3BucketName, s3CleanupBucket } from './context.js';
import { assertErrorContains } from '../../helpers.js';

export const runErrorTests: S3TestSection = async (ctx, runner) => {
  const results = [];
  const { client, bucketName, ts } = ctx;

  results.push(await runner.runTest('s3', 'HeadBucket_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new HeadBucketCommand({
        Bucket: 'nonexistent-bucket-xyz-12345',
      }));
    } catch (e) {
      err = e;
    }
    if (err === undefined) {
      throw new Error('expected error for non-existent bucket');
    }
    const name = (err as { name?: string }).name ?? '';
    if (!name.includes('NotFound') && !name.includes('NoSuchBucket')) {
      const message = err instanceof Error ? err.message : String(err);
      throw new Error(`expected NotFound, got: ${name}: ${message}`);
    }
  }));

  results.push(await runner.runTest('s3', 'GetObject_NonExistentKey', async () => {
    let err: unknown;
    try {
      await client.send(new GetObjectCommand({
        Bucket: bucketName,
        Key: 'nonexistent-key.txt',
      }));
    } catch (e) {
      err = e;
    }
    if (err === undefined) {
      throw new Error('expected error for non-existent key');
    }
    const name = (err as { name?: string }).name ?? '';
    if (!name.includes('NoSuchKey')) {
      const message = err instanceof Error ? err.message : String(err);
      throw new Error(`expected NoSuchKey, got: ${name}: ${message}`);
    }
  }));

  results.push(await runner.runTest('s3', 'HeadObject_NonExistentKey', async () => {
    let err: unknown;
    try {
      await client.send(new HeadObjectCommand({
        Bucket: bucketName,
        Key: 'nonexistent-key.txt',
      }));
    } catch (e) {
      err = e;
    }
    if (err === undefined) {
      throw new Error('expected error for non-existent key');
    }
    const name = (err as { name?: string }).name ?? '';
    if (!name.includes('NotFound') && !name.includes('NoSuchKey') && !name.includes('404')) {
      const message = err instanceof Error ? err.message : String(err);
      throw new Error(`expected NotFound (404), got: ${name}: ${message}`);
    }
  }));

  results.push(await runner.runTest('s3', 'DeleteObject_NonExistentKey', async () => {
    await client.send(new DeleteObjectCommand({
      Bucket: bucketName,
      Key: 'nonexistent-delete-key.txt',
    }));
  }));

  results.push(await runner.runTest('s3', 'DeleteBucket_NotEmpty', async () => {
    const neBucket = s3BucketName(ts, 'notempty');
    await client.send(new CreateBucketCommand({ Bucket: neBucket }));
    try {
      await client.send(new PutObjectCommand({
        Bucket: neBucket,
        Key: 'keep-me.txt',
        Body: 'data',
      }));

      let err: unknown;
      try {
        await client.send(new DeleteBucketCommand({ Bucket: neBucket }));
      } catch (e) {
        err = e;
      }
      assertErrorContains(err, 'BucketNotEmpty');
    } finally {
      await s3CleanupBucket(client, neBucket);
    }
  }));

  return results;
};
