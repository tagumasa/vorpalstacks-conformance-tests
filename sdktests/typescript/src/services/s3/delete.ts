import {
  CreateBucketCommand,
  PutObjectCommand,
  ListObjectsV2Command,
  DeleteBucketCommand,
} from '@aws-sdk/client-s3';
import { S3TestContext, S3TestSection, s3BucketName, s3CleanupBucket } from './context.js';

export const runDeleteTests: S3TestSection = async (ctx, runner) => {
  const results = [];
  const { client, ts, bucketName, lockBucket, mpuBucket } = ctx;

  results.push(await runner.runTest('s3', 'ListObjectsV2_Pagination', async () => {
    const pagBucket = s3BucketName(ts, 'pag');
    await client.send(new CreateBucketCommand({ Bucket: pagBucket }));

    try {
      const pagKeys: string[] = [];
      for (const i of [0, 1, 2, 3, 4]) {
        const key = `pag/object-${i}.txt`;
        await client.send(new PutObjectCommand({
          Bucket: pagBucket,
          Key: key,
          Body: 'pagination test data',
        }));
        pagKeys.push(key);
      }

      const allKeys: string[] = [];
      let continuationToken: string | undefined;
      let pageCount = 0;
      while (true) {
        const resp = await client.send(new ListObjectsV2Command({
          Bucket: pagBucket,
          Prefix: 'pag/',
          MaxKeys: 2,
          ContinuationToken: continuationToken,
        }));
        pageCount++;
        if (resp.Contents) {
          for (const obj of resp.Contents) {
            allKeys.push(obj.Key!);
          }
        }
        if (resp.IsTruncated) {
          continuationToken = resp.NextContinuationToken;
        } else {
          break;
        }
      }

      if (allKeys.length !== 5) {
        throw new Error(`expected 5 paginated objects, got ${allKeys.length}`);
      }
      if (pageCount < 2) {
        throw new Error(`expected at least 2 pages, got ${pageCount}`);
      }
    } finally {
      await s3CleanupBucket(client, pagBucket);
    }
  }));

  results.push(await runner.runTest('s3', 'DeleteBucket', async () => {
    await s3CleanupBucket(client, bucketName);
    await s3CleanupBucket(client, lockBucket);
    await s3CleanupBucket(client, mpuBucket);
  }));

  return results;
};
