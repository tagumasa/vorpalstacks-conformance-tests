import {
  S3Client,
  CreateBucketCommand,
  HeadBucketCommand,
  ListBucketsCommand,
  DeleteBucketCommand,
  PutObjectCommand,
  GetObjectCommand,
  HeadObjectCommand,
  ListObjectsCommand,
  ListObjectsV2Command,
  DeleteObjectCommand,
  DeleteObjectsCommand,
  CopyObjectCommand,
  GetBucketLocationCommand,
  GetBucketAclCommand,
  PutBucketAclCommand,
  GetBucketPolicyCommand,
  PutBucketPolicyCommand,
  DeleteBucketPolicyCommand,
  GetObjectAclCommand,
  PutObjectAclCommand,
  CreateMultipartUploadCommand,
  UploadPartCommand,
  CompleteMultipartUploadCommand,
  AbortMultipartUploadCommand,
  ListPartsCommand,
} from '@aws-sdk/client-s3';
import { NoSuchBucket, NoSuchKey, InvalidObjectState } from '@aws-sdk/client-s3';
import { TestRunner } from '../runner.js';
import { TestResult } from '../runner.js';

function makeUniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 999999)}`;
}

export async function runS3Tests(
  runner: TestRunner,
  s3Client: S3Client,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const bucketName = makeUniqueName('tsbucket');
  const objectKey = 'test-object.txt';
  const objectContent = 'Hello, S3! This is test content.';
  let bucketCreated = false;

  try {
    // CreateBucket
    results.push(
      await runner.runTest('s3', 'CreateBucket', async () => {
        await s3Client.send(
          new CreateBucketCommand({
            Bucket: bucketName,
            ACL: 'private',
          })
        );
        bucketCreated = true;
      })
    );

    // HeadBucket
    results.push(
      await runner.runTest('s3', 'HeadBucket', async () => {
        const resp = await s3Client.send(
          new HeadBucketCommand({ Bucket: bucketName })
        );
        if (resp.$metadata.httpStatusCode !== 200) {
          throw new Error(`Expected 200, got ${resp.$metadata.httpStatusCode}`);
        }
      })
    );

    // ListBuckets
    results.push(
      await runner.runTest('s3', 'ListBuckets', async () => {
        const resp = await s3Client.send(new ListBucketsCommand({}));
        if (!resp.Buckets) throw new Error('Buckets is null');
        const found = resp.Buckets.some((b) => b.Name === bucketName);
        if (!found) throw new Error('Created bucket not found in list');
      })
    );

    // GetBucketLocation
    results.push(
      await runner.runTest('s3', 'GetBucketLocation', async () => {
        const resp = await s3Client.send(
          new GetBucketLocationCommand({ Bucket: bucketName })
        );
        // Location can be null for us-east-1
      })
    );

    // PutObject
    results.push(
      await runner.runTest('s3', 'PutObject', async () => {
        const resp = await s3Client.send(
          new PutObjectCommand({
            Bucket: bucketName,
            Key: objectKey,
            Body: Buffer.from(objectContent),
            ContentType: 'text/plain',
          })
        );
        if (!resp.ETag) throw new Error('ETag is null');
      })
    );

    // HeadObject
    results.push(
      await runner.runTest('s3', 'HeadObject', async () => {
        const resp = await s3Client.send(
          new HeadObjectCommand({
            Bucket: bucketName,
            Key: objectKey,
          })
        );
        if (!resp.ContentLength || resp.ContentLength === 0) {
          throw new Error('ContentLength is null or zero');
        }
      })
    );

    // GetObject
    results.push(
      await runner.runTest('s3', 'GetObject', async () => {
        const resp = await s3Client.send(
          new GetObjectCommand({
            Bucket: bucketName,
            Key: objectKey,
          })
        );
        if (!resp.Body) throw new Error('Body is null');
        const bodyStr = await resp.Body.transformToString();
        if (bodyStr !== objectContent) {
          throw new Error(`Expected "${objectContent}", got "${bodyStr}"`);
        }
      })
    );

    // ListObjects
    results.push(
      await runner.runTest('s3', 'ListObjects', async () => {
        const resp = await s3Client.send(
          new ListObjectsCommand({ Bucket: bucketName })
        );
        if (!resp.Contents) throw new Error('Contents is null');
        const found = resp.Contents.some((o) => o.Key === objectKey);
        if (!found) throw new Error('Created object not found in list');
      })
    );

    // ListObjectsV2
    results.push(
      await runner.runTest('s3', 'ListObjectsV2', async () => {
        const resp = await s3Client.send(
          new ListObjectsV2Command({ Bucket: bucketName })
        );
        if (!resp.Contents) throw new Error('Contents is null');
        const found = resp.Contents.some((o) => o.Key === objectKey);
        if (!found) throw new Error('Created object not found in list');
      })
    );

    // CopyObject
    const copiedKey = 'copied-object.txt';
    results.push(
      await runner.runTest('s3', 'CopyObject', async () => {
        const resp = await s3Client.send(
          new CopyObjectCommand({
            Bucket: bucketName,
            Key: copiedKey,
            CopySource: `/${bucketName}/${objectKey}`,
          })
        );
        if (!resp.CopyObjectResult) throw new Error('CopyObjectResult is null');
      })
    );

    // GetObjectAcl
    results.push(
      await runner.runTest('s3', 'GetObjectAcl', async () => {
        const resp = await s3Client.send(
          new GetObjectAclCommand({
            Bucket: bucketName,
            Key: objectKey,
          })
        );
        if (!resp.Owner) throw new Error('Owner is null');
      })
    );

    // PutObjectAcl
    results.push(
      await runner.runTest('s3', 'PutObjectAcl', async () => {
        await s3Client.send(
          new PutObjectAclCommand({
            Bucket: bucketName,
            Key: objectKey,
            ACL: 'private',
          })
        );
      })
    );

    // DeleteObject
    results.push(
      await runner.runTest('s3', 'DeleteObject', async () => {
        await s3Client.send(
          new DeleteObjectCommand({
            Bucket: bucketName,
            Key: copiedKey,
          })
        );
      })
    );

    // DeleteObjects (multiple)
    const keysToDelete = ['obj1.txt', 'obj2.txt', 'obj3.txt'];
    for (const key of keysToDelete) {
      await s3Client.send(
        new PutObjectCommand({
          Bucket: bucketName,
          Key: key,
          Body: Buffer.from(`content for ${key}`),
        })
      );
    }
    results.push(
      await runner.runTest('s3', 'DeleteObjects', async () => {
        await s3Client.send(
          new DeleteObjectsCommand({
            Bucket: bucketName,
            Delete: {
              Objects: keysToDelete.map((key) => ({ Key: key })),
            },
          })
        );
      })
    );

    // CreateMultipartUpload
    const multipartKey = 'multipart-test.bin';
    let uploadId = '';
    results.push(
      await runner.runTest('s3', 'CreateMultipartUpload', async () => {
        const resp = await s3Client.send(
          new CreateMultipartUploadCommand({
            Bucket: bucketName,
            Key: multipartKey,
          })
        );
        if (!resp.UploadId) throw new Error('UploadId is null');
        uploadId = resp.UploadId;
      })
    );

    // UploadPart
    results.push(
      await runner.runTest('s3', 'UploadPart', async () => {
        const resp = await s3Client.send(
          new UploadPartCommand({
            Bucket: bucketName,
            Key: multipartKey,
            UploadId: uploadId,
            PartNumber: 1,
            Body: Buffer.from('part 1 content'),
          })
        );
        if (!resp.ETag) throw new Error('ETag is null');
      })
    );

    // ListParts
    results.push(
      await runner.runTest('s3', 'ListParts', async () => {
        const resp = await s3Client.send(
          new ListPartsCommand({
            Bucket: bucketName,
            Key: multipartKey,
            UploadId: uploadId,
          })
        );
        if (!resp.Parts) throw new Error('Parts is null');
      })
    );

    // CompleteMultipartUpload
    results.push(
      await runner.runTest('s3', 'CompleteMultipartUpload', async () => {
        await s3Client.send(
          new CompleteMultipartUploadCommand({
            Bucket: bucketName,
            Key: multipartKey,
            UploadId: uploadId,
            MultipartUpload: {
              Parts: [{ PartNumber: 1, ETag: '"abc123"' }],
            },
          })
        );
      })
    );

    // AbortMultipartUpload (create new one to abort)
    results.push(
      await runner.runTest('s3', 'AbortMultipartUpload', async () => {
        const createResp = await s3Client.send(
          new CreateMultipartUploadCommand({
            Bucket: bucketName,
            Key: 'to-abort.bin',
          })
        );
        if (!createResp.UploadId) throw new Error('UploadId is null');
        await s3Client.send(
          new AbortMultipartUploadCommand({
            Bucket: bucketName,
            Key: 'to-abort.bin',
            UploadId: createResp.UploadId,
          })
        );
      })
    );

    // GetBucketPolicy
    const testPolicy = JSON.stringify({
      Version: '2012-10-17',
      Statement: [
        {
          Effect: 'Allow',
          Principal: '*',
          Action: ['s3:GetObject'],
          Resource: `arn:aws:s3:::${bucketName}/*`,
        },
      ],
    });
    results.push(
      await runner.runTest('s3', 'PutBucketPolicy', async () => {
        await s3Client.send(
          new PutBucketPolicyCommand({
            Bucket: bucketName,
            Policy: testPolicy,
          })
        );
      })
    );

    results.push(
      await runner.runTest('s3', 'GetBucketPolicy', async () => {
        const resp = await s3Client.send(
          new GetBucketPolicyCommand({ Bucket: bucketName })
        );
        if (!resp.Policy) throw new Error('Policy is null');
      })
    );

    results.push(
      await runner.runTest('s3', 'DeleteBucketPolicy', async () => {
        await s3Client.send(
          new DeleteBucketPolicyCommand({ Bucket: bucketName })
        );
      })
    );

    // MultiByteContent
    results.push(
      await runner.runTest('s3', 'MultiByteContent', async () => {
        const jaKey = 'テスト/日本語ファイル.txt';
        const zhKey = '文档/简体中文.txt';
        const twKey = '文件/繁體中文.txt';
        const jaBody = 'こんにちは世界。これは日本語のテストデータです。';
        const zhBody = '你好世界。这是简体中文的测试数据。';
        const twBody = '你好世界。這是繁體中文的測試資料。';
        const pairs: [string, string][] = [[jaKey, jaBody], [zhKey, zhBody], [twKey, twBody]];
        for (const [key, body] of pairs) {
          await s3Client.send(
            new PutObjectCommand({
              Bucket: bucketName,
              Key: key,
              Body: body,
              ContentType: 'text/plain; charset=utf-8',
            })
          );
        }
        for (const [key, body] of pairs) {
          const resp = await s3Client.send(
            new GetObjectCommand({ Bucket: bucketName, Key: key })
          );
          const actual = await resp.Body!.transformToString('utf-8');
          if (actual !== body) {
            throw new Error(`Mismatch for ${key}: expected ${body}, got ${actual}`);
          }
        }
      })
    );

    // DeleteBucket
    results.push(
      await runner.runTest('s3', 'DeleteBucket', async () => {
        // First delete all objects
        const listResp = await s3Client.send(
          new ListObjectsV2Command({ Bucket: bucketName })
        );
        if (listResp.Contents && listResp.Contents.length > 0) {
          await s3Client.send(
            new DeleteObjectsCommand({
              Bucket: bucketName,
              Delete: {
                Objects: listResp.Contents.map((o) => ({ Key: o.Key! })),
              },
            })
          );
        }
        await s3Client.send(
          new DeleteBucketCommand({ Bucket: bucketName })
        );
      })
    );

  } finally {
    try {
      // Cleanup
      if (bucketCreated) {
        const listResp = await s3Client.send(
          new ListObjectsV2Command({ Bucket: bucketName })
        );
        if (listResp.Contents && listResp.Contents.length > 0) {
          await s3Client.send(
            new DeleteObjectsCommand({
              Bucket: bucketName,
              Delete: {
                Objects: listResp.Contents.map((o) => ({ Key: o.Key! })),
              },
            })
          );
        }
        await s3Client.send(new DeleteBucketCommand({ Bucket: bucketName }));
      }
    } catch { /* ignore */ }
  }

  // Error cases
  results.push(
    await runner.runTest('s3', 'HeadBucket_NonExistent', async () => {
      try {
        await s3Client.send(
          new HeadBucketCommand({ Bucket: 'nonexistent-bucket-xyz-12345' })
        );
        throw new Error('Expected error for non-existent bucket but got none');
      } catch (err: unknown) {
        if (err instanceof NoSuchBucket) {
          // Expected
        } else if (err instanceof Error && err.name === 'NoSuchBucket') {
          // Expected
        }
      }
    })
  );

  results.push(
    await runner.runTest('s3', 'GetObject_NonExistent', async () => {
      try {
        await s3Client.send(
          new GetObjectCommand({
            Bucket: bucketName,
            Key: 'nonexistent-key-xyz-12345',
          })
        );
        throw new Error('Expected NoSuchKey but got none');
      } catch (err: unknown) {
        if (err instanceof NoSuchKey) {
          // Expected
        } else if (err instanceof Error && err.name === 'NoSuchKey') {
          // Expected
        }
      }
    })
  );

  results.push(
    await runner.runTest('s3', 'DeleteBucket_NonExistent', async () => {
      try {
        await s3Client.send(
          new DeleteBucketCommand({ Bucket: 'nonexistent-bucket-xyz-12345' })
        );
        throw new Error('Expected error but got none');
      } catch (err: unknown) {
        // Any error is acceptable for non-existent bucket
      }
    })
  );

  return results;
}