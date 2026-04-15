import {
  CreateBucketCommand,
  CreateMultipartUploadCommand,
  UploadPartCommand,
  ListPartsCommand,
  CompleteMultipartUploadCommand,
  GetObjectCommand,
  AbortMultipartUploadCommand,
  ListMultipartUploadsCommand,
  PutBucketVersioningCommand,
  ListObjectVersionsCommand,
  PutObjectCommand,
} from '@aws-sdk/client-s3';
import { S3TestContext, S3TestSection, s3BucketName, s3CleanupBucket } from './context.js';
import { assertNotNil } from '../../helpers.js';

export const runMultipartTests: S3TestSection = async (ctx, runner) => {
  const results = [];
  const { client, ts, mpuBucket } = ctx;

  results.push(await runner.runTest('s3', 'CreateMultipartUpload', async () => {
    await client.send(new CreateBucketCommand({ Bucket: mpuBucket }));
  }));

  let uploadId: string;
  results.push(await runner.runTest('s3', 'CreateMultipartUpload_Initiate', async () => {
    const resp = await client.send(new CreateMultipartUploadCommand({
      Bucket: mpuBucket,
      Key: 'multipart-obj.txt',
    }));
    assertNotNil(resp.UploadId, 'UploadId');
    uploadId = resp.UploadId;
  }));

  let part1ETag: string;
  results.push(await runner.runTest('s3', 'UploadPart', async () => {
    const resp = await client.send(new UploadPartCommand({
      Bucket: mpuBucket,
      Key: 'multipart-obj.txt',
      PartNumber: 1,
      UploadId: uploadId,
      Body: 'part one content',
    }));
    assertNotNil(resp.ETag, 'ETag');
    part1ETag = resp.ETag;
  }));

  let part2ETag: string;
  results.push(await runner.runTest('s3', 'UploadPart_Part2', async () => {
    const resp = await client.send(new UploadPartCommand({
      Bucket: mpuBucket,
      Key: 'multipart-obj.txt',
      PartNumber: 2,
      UploadId: uploadId,
      Body: 'part two content',
    }));
    assertNotNil(resp.ETag, 'ETag');
    part2ETag = resp.ETag;
  }));

  results.push(await runner.runTest('s3', 'ListParts', async () => {
    const resp = await client.send(new ListPartsCommand({
      Bucket: mpuBucket,
      Key: 'multipart-obj.txt',
      UploadId: uploadId,
    }));
    if (!resp.Parts || resp.Parts.length !== 2) {
      throw new Error(`expected 2 parts, got ${resp.Parts?.length ?? 0}`);
    }
  }));

  results.push(await runner.runTest('s3', 'CompleteMultipartUpload', async () => {
    const resp = await client.send(new CompleteMultipartUploadCommand({
      Bucket: mpuBucket,
      Key: 'multipart-obj.txt',
      UploadId: uploadId,
      MultipartUpload: {
        Parts: [
          { PartNumber: 1, ETag: part1ETag },
          { PartNumber: 2, ETag: part2ETag },
        ],
      },
    }));
    assertNotNil(resp.Location, 'Location');
    assertNotNil(resp.ETag, 'ETag');
  }));

  results.push(await runner.runTest('s3', 'MultipartUpload_GetObject', async () => {
    const resp = await client.send(new GetObjectCommand({
      Bucket: mpuBucket,
      Key: 'multipart-obj.txt',
    }));
    const body = await resp.Body!.transformToString();
    const expected = 'part one contentpart two content';
    if (body !== expected) {
      throw new Error(`content mismatch: got "${body}", want "${expected}"`);
    }
  }));

  // ========== ABORT MULTIPART UPLOAD ==========
  const abortBucket = s3BucketName(ts, 'abort');
  results.push(await runner.runTest('s3', 'AbortMultipartUpload', async () => {
    await client.send(new CreateBucketCommand({ Bucket: abortBucket }));
    try {
      const createResp = await client.send(new CreateMultipartUploadCommand({
        Bucket: abortBucket,
        Key: 'abort-obj.txt',
      }));
      assertNotNil(createResp.UploadId, 'UploadId');

      await client.send(new UploadPartCommand({
        Bucket: abortBucket,
        Key: 'abort-obj.txt',
        PartNumber: 1,
        UploadId: createResp.UploadId,
        Body: 'abort data',
      }));

      await client.send(new AbortMultipartUploadCommand({
        Bucket: abortBucket,
        Key: 'abort-obj.txt',
        UploadId: createResp.UploadId,
      }));
    } finally {
      await s3CleanupBucket(client, abortBucket);
    }
  }));

  // ========== LIST MULTIPART UPLOADS ==========
  const lmpuBucket = s3BucketName(ts, 'listmpu');
  results.push(await runner.runTest('s3', 'ListMultipartUploads', async () => {
    await client.send(new CreateBucketCommand({ Bucket: lmpuBucket }));
    try {
      await client.send(new CreateMultipartUploadCommand({
        Bucket: lmpuBucket,
        Key: 'listed-mpu-obj.txt',
      }));

      const resp = await client.send(new ListMultipartUploadsCommand({ Bucket: lmpuBucket }));
      if (!resp.Uploads || resp.Uploads.length === 0) {
        throw new Error('expected at least one upload');
      }
    } finally {
      await s3CleanupBucket(client, lmpuBucket);
    }
  }));

  // ========== LIST OBJECT VERSIONS ==========
  const verBucket = s3BucketName(ts, 'versions');
  results.push(await runner.runTest('s3', 'ListObjectVersions', async () => {
    await client.send(new CreateBucketCommand({ Bucket: verBucket }));
    try {
      await client.send(new PutBucketVersioningCommand({
        Bucket: verBucket,
        VersioningConfiguration: { Status: 'Enabled' },
      }));

      for (const i of [0, 1, 2]) {
        await client.send(new PutObjectCommand({
          Bucket: verBucket,
          Key: 'versioned-obj.txt',
          Body: `version ${i}`,
        }));
      }

      const resp = await client.send(new ListObjectVersionsCommand({ Bucket: verBucket }));
      if (!resp.Versions || resp.Versions.length === 0) {
        throw new Error('expected at least one version');
      }
    } finally {
      await s3CleanupBucket(client, verBucket);
    }
  }));

  return results;
};
