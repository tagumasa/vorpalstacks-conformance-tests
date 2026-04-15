import {
  CreateBucketCommand,
  ListBucketsCommand,
  HeadBucketCommand,
  GetBucketLocationCommand,
} from '@aws-sdk/client-s3';
import { S3TestContext, S3TestSection, s3BucketName, s3CleanupBucket } from './context.js';
import { assertNotNil, assertErrorContains } from '../../helpers.js';

export const runBucketCoreTests: S3TestSection = async (ctx, runner) => {
  const results = [];
  const { client, ts, bucketName } = ctx;

  results.push(await runner.runTest('s3', 'CreateBucket', async () => {
    const resp = await client.send(new CreateBucketCommand({ Bucket: bucketName }));
    assertNotNil(resp.Location, 'Location');
  }));

  results.push(await runner.runTest('s3', 'ListBuckets', async () => {
    const resp = await client.send(new ListBucketsCommand({}));
    assertNotNil(resp.Buckets, 'Buckets');
  }));

  const sortBuckets = [
    s3BucketName(ts, 'sort-z'),
    s3BucketName(ts, 'sort-a'),
    s3BucketName(ts, 'sort-m'),
  ];
  results.push(await runner.runTest('s3', 'ListBuckets_SortedByName', async () => {
    for (const b of sortBuckets) {
      await client.send(new CreateBucketCommand({ Bucket: b }));
    }
    try {
      const resp = await client.send(new ListBucketsCommand({}));
      const buckets = resp.Buckets!;
      for (let i = 1; i < buckets.length; i++) {
        if (buckets[i].Name! < buckets[i - 1].Name!) {
          throw new Error(`buckets not sorted: ${buckets[i - 1].Name} before ${buckets[i].Name}`);
        }
      }
    } finally {
      for (const b of sortBuckets) {
        await s3CleanupBucket(client, b);
      }
    }
  }));

  results.push(await runner.runTest('s3', 'HeadBucket', async () => {
    const resp = await client.send(new HeadBucketCommand({ Bucket: bucketName }));
    assertNotNil(resp.BucketRegion, 'BucketRegion');
  }));

  results.push(await runner.runTest('s3', 'GetBucketLocation', async () => {
    const resp = await client.send(new GetBucketLocationCommand({ Bucket: bucketName }));
    const lc = resp.LocationConstraint as string | undefined;
    if (lc !== undefined && lc !== null && lc !== '' && lc !== 'us-east-1') {
      throw new Error(`unexpected LocationConstraint: ${lc}`);
    }
  }));

  results.push(await runner.runTest('s3', 'CreateBucket_DuplicateName', async () => {
    let err: unknown;
    try {
      await client.send(new CreateBucketCommand({ Bucket: bucketName }));
    } catch (e) {
      err = e;
    }
    if (err === undefined) {
      throw new Error('expected error for duplicate bucket name');
    }
    const message = err instanceof Error ? err.message : String(err);
    const code = (err as { Code?: string }).Code ?? '';
    const name = (err as { name?: string }).name ?? '';
    if (
      !name.includes('BucketAlreadyOwnedByYou') &&
      !name.includes('BucketAlreadyExists') &&
      !code.includes('BucketAlreadyOwnedByYou') &&
      !code.includes('BucketAlreadyExists') &&
      !message.includes('BucketAlreadyOwnedByYou') &&
      !message.includes('BucketAlreadyExists')
    ) {
      throw new Error(`expected BucketAlreadyOwnedByYou or BucketAlreadyExists, got: ${message}`);
    }
  }));

  return results;
};
