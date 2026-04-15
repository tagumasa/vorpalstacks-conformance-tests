import {
  S3Client,
  ListObjectsV2Command,
  DeleteObjectsCommand,
  DeleteBucketCommand,
} from '@aws-sdk/client-s3';

export interface S3TestContext {
  client: S3Client;
  ts: string;
  bucketName: string;
  lockBucket: string;
  mpuBucket: string;
}

export type S3TestSection = (ctx: S3TestContext, runner: import('../../runner.js').TestRunner) => Promise<import('../../runner.js').TestResult[]>;

export function s3BucketName(ts: string, name: string): string {
  return `s3test-${name}-${ts}`;
}

export async function s3CleanupBucket(
  client: S3Client,
  bucket: string,
): Promise<void> {
  try {
    const listResp = await client.send(new ListObjectsV2Command({ Bucket: bucket }));
    if (listResp.Contents && listResp.Contents.length > 0) {
      const objects = listResp.Contents.map((o) => ({ Key: o.Key! }));
      await client.send(new DeleteObjectsCommand({
        Bucket: bucket,
        Delete: { Objects: objects },
      }));
    }
  } catch { /* ignore list/delete errors */ }

  try {
    await client.send(new DeleteBucketCommand({ Bucket: bucket }));
  } catch { /* ignore */ }
}

export async function createS3TestContext(
  endpoint: string,
  region: string,
  credentials: { accessKeyId: string; secretAccessKey: string },
): Promise<S3TestContext> {
  const ts = String(Date.now());
  const bucketName = s3BucketName(ts, 'main');
  const lockBucket = s3BucketName(ts, 'lock');
  const mpuBucket = s3BucketName(ts, 'mpu');

  const client = new S3Client({
    endpoint,
    region,
    credentials,
    forcePathStyle: true,
  });

  return { client, ts, bucketName, lockBucket, mpuBucket };
}
