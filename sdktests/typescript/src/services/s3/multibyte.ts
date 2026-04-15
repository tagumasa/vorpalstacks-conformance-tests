import {
  PutObjectCommand,
  GetObjectCommand,
  HeadObjectCommand,
  ListObjectsV2Command,
  CopyObjectCommand,
  DeleteObjectsCommand,
  CreateMultipartUploadCommand,
  UploadPartCommand,
  CompleteMultipartUploadCommand,
  PutObjectTaggingCommand,
  GetObjectTaggingCommand,
} from '@aws-sdk/client-s3';
import { S3TestContext, S3TestSection } from './context.js';
import { assertNotNil } from '../../helpers.js';

export const runMultibyteTests: S3TestSection = async (ctx, runner) => {
  const results = [];
  const { client, bucketName } = ctx;

  results.push(await runner.runTest('s3', 'MultiByte_PutGetRoundtrip', async () => {
    const mbCases = [
      { key: 'テスト/日本語ファイル.txt', body: 'こんにちは世界。これは日本語のテストデータです。' },
      { key: '文档/简体中文.txt', body: '你好世界。这是简体中文的测试数据。' },
      { key: '文件/繁體中文.txt', body: '你好世界。這是繁體中文的測試資料。' },
    ];
    for (const tc of mbCases) {
      await client.send(new PutObjectCommand({
        Bucket: bucketName,
        Key: tc.key,
        Body: tc.body,
        ContentType: 'text/plain; charset=utf-8',
      }));
      const resp = await client.send(new GetObjectCommand({
        Bucket: bucketName,
        Key: tc.key,
      }));
      const body = await resp.Body!.transformToString();
      if (body !== tc.body) {
        throw new Error(`body mismatch for "${tc.key}": expected "${tc.body}", got "${body}"`);
      }
    }
  }));

  // NOTE: Go uses non-ASCII metadata values (田中太郎 etc.) but TS SDK v3 cannot
  // send non-ASCII in HTTP headers due to Node.js limitation. This is an open bug:
  //   https://github.com/aws/aws-sdk-js-v3/issues/6596
  // When fixed, restore Go-equivalent non-ASCII metadata values.
  results.push(await runner.runTest('s3', 'MultiByte_MetadataRoundtrip', async () => {
    const mbCases: Array<{ key: string; body: string; meta: Record<string, string> }> = [
      {
        key: 'test/metadata-ascii.txt',
        body: 'metadata test',
        meta: { author: 'test-author@example.com', desc: 'Test: special chars !@#$%^&*()', project: 'project/with/slashes' },
      },
      {
        key: 'path/with spaces/metadata.txt',
        body: 'metadata test 2',
        meta: { 'x-custom-key': 'value-with-dashes', 'key.with.dots': 'ok', 'upper_key': 'upper value' },
      },
    ];
    for (const tc of mbCases) {
      await client.send(new PutObjectCommand({
        Bucket: bucketName,
        Key: tc.key,
        Body: tc.body,
        ContentType: 'text/plain; charset=utf-8',
        Metadata: tc.meta,
      }));
      const resp = await client.send(new HeadObjectCommand({
        Bucket: bucketName,
        Key: tc.key,
      }));
      for (const [k, v] of Object.entries(tc.meta)) {
        const actual = resp.Metadata?.[k];
        if (actual === undefined) {
          throw new Error(`missing metadata key "${k}" for object "${tc.key}"`);
        }
        if (actual !== v) {
          throw new Error(`metadata "${k}" mismatch for "${tc.key}": expected "${v}", got "${actual}"`);
        }
      }
    }
  }));

  results.push(await runner.runTest('s3', 'MultiByte_ListObjectsV2', async () => {
    const mbKeys = [
      'テスト/リスト日本語.txt',
      '文档/列表简体.txt',
      '文件/列表繁體.txt',
      'mixed/混合日本語中文繁體.txt',
    ];
    for (const key of mbKeys) {
      await client.send(new PutObjectCommand({
        Bucket: bucketName,
        Key: key,
        Body: 'list-test-content',
      }));
    }

    const resp = await client.send(new ListObjectsV2Command({
      Bucket: bucketName,
      Prefix: 'テスト/',
    }));
    let found = false;
    if (resp.Contents) {
      for (const obj of resp.Contents) {
        if (obj.Key === 'テスト/リスト日本語.txt') {
          found = true;
          break;
        }
      }
    }
    if (!found) {
      throw new Error('Japanese key not found in ListObjectsV2 results');
    }

    const resp2 = await client.send(new ListObjectsV2Command({
      Bucket: bucketName,
      Prefix: '文档/',
    }));
    let found2 = false;
    if (resp2.Contents) {
      for (const obj of resp2.Contents) {
        if (obj.Key === '文档/列表简体.txt') {
          found2 = true;
          break;
        }
      }
    }
    if (!found2) {
      throw new Error('Simplified Chinese key not found in ListObjectsV2 results');
    }

    const resp3 = await client.send(new ListObjectsV2Command({
      Bucket: bucketName,
      Prefix: '文件/',
    }));
    let found3 = false;
    if (resp3.Contents) {
      for (const obj of resp3.Contents) {
        if (obj.Key === '文件/列表繁體.txt') {
          found3 = true;
          break;
        }
      }
    }
    if (!found3) {
      throw new Error('Traditional Chinese key not found in ListObjectsV2 results');
    }
  }));

  // NOTE: Go uses non-ASCII key (テスト/コピー元.txt) in CopySource but TS SDK v3
  // cannot send non-ASCII in x-amz-copy-source header due to Node.js limitation.
  // This is an open bug: https://github.com/aws/aws-sdk-js-v3/issues/6596
  // When fixed, restore Go-equivalent non-ASCII key names.
  results.push(await runner.runTest('s3', 'MultiByte_CopyObject', async () => {
    const srcKey = 'path/with spaces/copy-source.txt';
    const srcBody = 'copy source content with special chars: !@#$%';
    await client.send(new PutObjectCommand({
      Bucket: bucketName,
      Key: srcKey,
      Body: srcBody,
    }));
    const dstKey = 'path/with spaces/copy-dest.txt';
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
    if (body !== srcBody) {
      throw new Error(`copied body mismatch: expected "${srcBody}", got "${body}"`);
    }
  }));

  results.push(await runner.runTest('s3', 'MultiByte_DeleteObjects', async () => {
    const delKeys = [
      'テスト/削除対象1.txt',
      '文档/删除目标2.txt',
      '文件/刪除目標3.txt',
    ];
    for (const key of delKeys) {
      await client.send(new PutObjectCommand({
        Bucket: bucketName,
        Key: key,
        Body: 'to-delete',
      }));
    }
    const delResp = await client.send(new DeleteObjectsCommand({
      Bucket: bucketName,
      Delete: {
        Objects: delKeys.map((key) => ({ Key: key })),
      },
    }));
    for (const key of delKeys) {
      const found = delResp.Deleted?.some((d) => d.Key === key);
      if (!found) {
        throw new Error(`key "${key}" not found in Deleted results`);
      }
    }
  }));

  results.push(await runner.runTest('s3', 'MultiByte_MultipartUpload', async () => {
    const mpuKey = 'テスト/マルチパート.txt';
    const createResp = await client.send(new CreateMultipartUploadCommand({
      Bucket: bucketName,
      Key: mpuKey,
      ContentType: 'application/octet-stream',
    }));
    assertNotNil(createResp.UploadId, 'UploadId');
    const mpuUploadId = createResp.UploadId;

    const part1Body = 'こんにちは、パート1のデータです。';
    const part2Body = '这是第二部分的数据。這是第二部分的數據。';

    const uploadResp1 = await client.send(new UploadPartCommand({
      Bucket: bucketName,
      Key: mpuKey,
      UploadId: mpuUploadId,
      PartNumber: 1,
      Body: part1Body,
    }));
    assertNotNil(uploadResp1.ETag, 'ETag for part1');

    const uploadResp2 = await client.send(new UploadPartCommand({
      Bucket: bucketName,
      Key: mpuKey,
      UploadId: mpuUploadId,
      PartNumber: 2,
      Body: part2Body,
    }));
    assertNotNil(uploadResp2.ETag, 'ETag for part2');

    await client.send(new CompleteMultipartUploadCommand({
      Bucket: bucketName,
      Key: mpuKey,
      UploadId: mpuUploadId,
      MultipartUpload: {
        Parts: [
          { PartNumber: 1, ETag: uploadResp1.ETag },
          { PartNumber: 2, ETag: uploadResp2.ETag },
        ],
      },
    }));

    const getResp = await client.send(new GetObjectCommand({
      Bucket: bucketName,
      Key: mpuKey,
    }));
    const body = await getResp.Body!.transformToString();
    const expected = part1Body + part2Body;
    if (body !== expected) {
      throw new Error(`multipart body mismatch: expected "${expected}", got "${body}"`);
    }
  }));

  results.push(await runner.runTest('s3', 'MultiByte_ObjectTagging', async () => {
    const tagKey = '文档/标签测试.txt';
    await client.send(new PutObjectCommand({
      Bucket: bucketName,
      Key: tagKey,
      Body: 'tag-test',
    }));
    await client.send(new PutObjectTaggingCommand({
      Bucket: bucketName,
      Key: tagKey,
      Tagging: {
        TagSet: [
          { Key: '環境', Value: 'テスト' },
          { Key: '说明', Value: '简体标签' },
          { Key: '說明', Value: '繁體標籤' },
        ],
      },
    }));
    const tagResp = await client.send(new GetObjectTaggingCommand({
      Bucket: bucketName,
      Key: tagKey,
    }));
    const expectedTags: Record<string, string> = {
      '環境': 'テスト',
      '说明': '简体标签',
      '說明': '繁體標籤',
    };
    for (const [k, v] of Object.entries(expectedTags)) {
      const found = tagResp.TagSet?.some((t) => t.Key === k && t.Value === v);
      if (!found) {
        throw new Error(`tag "${k}"="${v}" not found in response`);
      }
    }
  }));

  results.push(await runner.runTest('s3', 'MultiByte_ContentTypeRoundtrip', async () => {
    const ctCases = [
      { key: 'テスト/contenttype.txt', contentType: 'text/plain; charset=shift_jis' },
      { key: '文档/contenttype.txt', contentType: 'text/html; charset=gb2312' },
      { key: '文件/contenttype.txt', contentType: 'text/html; charset=big5' },
    ];
    for (const tc of ctCases) {
      await client.send(new PutObjectCommand({
        Bucket: bucketName,
        Key: tc.key,
        Body: 'ct-test',
        ContentType: tc.contentType,
      }));
      const resp = await client.send(new HeadObjectCommand({
        Bucket: bucketName,
        Key: tc.key,
      }));
      if (resp.ContentType !== tc.contentType) {
        throw new Error(`ContentType mismatch for "${tc.key}": expected "${tc.contentType}", got ${resp.ContentType}`);
      }
    }
  }));

  return results;
};
