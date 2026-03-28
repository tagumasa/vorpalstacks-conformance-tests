using Amazon.S3;
using Amazon.S3.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static class S3ServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonS3Client s3Client,
        string region)
    {
        var results = new List<TestResult>();
        var bucketName = TestRunner.MakeUniqueName("csbucket");
        var objectKey = "test-object.txt";
        var objectContent = "Hello, S3! This is test content.";
        var bucketCreated = false;

        try
        {
            results.Add(await runner.RunTestAsync("s3", "CreateBucket", async () =>
            {
                var response = await s3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = bucketName,
                    UseClientRegion = true
                });
                bucketCreated = true;
                if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
                    throw new Exception($"CreateBucket failed with status: {response.HttpStatusCode}");
            }));

            results.Add(await runner.RunTestAsync("s3", "HeadBucket", async () =>
            {
                await s3Client.HeadBucketAsync(new HeadBucketRequest
                {
                    BucketName = bucketName
                });
            }));

            results.Add(await runner.RunTestAsync("s3", "ListBuckets", async () =>
            {
                var response = await s3Client.ListBucketsAsync(new ListBucketsRequest());
                if (response.Buckets == null)
                    throw new Exception("Buckets list is null");
                var found = response.Buckets.Any(b => b.BucketName == bucketName);
                if (!found)
                    throw new Exception("Created bucket not found in list");
            }));

            results.Add(await runner.RunTestAsync("s3", "PutObject", async () =>
            {
                var response = await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey,
                    ContentBody = objectContent,
                    ContentType = "text/plain"
                });
                if (string.IsNullOrEmpty(response.ETag))
                    throw new Exception("ETag is null");
            }));

            results.Add(await runner.RunTestAsync("s3", "HeadObject", async () =>
            {
                var response = await s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = bucketName,
                    Key = objectKey
                });
                if (response.ContentLength == 0)
                    throw new Exception("ContentLength is zero");
                if (string.IsNullOrEmpty(response.ETag))
                    throw new Exception("ETag is null");
            }));

            results.Add(await runner.RunTestAsync("s3", "GetObject", async () =>
            {
                var response = await s3Client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey
                });
                if (response.ResponseStream == null)
                    throw new Exception("ResponseStream is null");
                using var reader = new StreamReader(response.ResponseStream);
                var bodyStr = await reader.ReadToEndAsync();
                if (bodyStr != objectContent)
                    throw new Exception($"Content mismatch. Expected: {objectContent}, Got: {bodyStr}");
            }));

            results.Add(await runner.RunTestAsync("s3", "GetObject_NonExistentKey", async () =>
            {
                try
                {
                    await s3Client.GetObjectAsync(new GetObjectRequest
                    {
                        BucketName = bucketName,
                        Key = "nonexistent-key.txt"
                    });
                    throw new Exception("Expected error but got none");
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { }
            }));

            results.Add(await runner.RunTestAsync("s3", "HeadObject_NonExistentKey", async () =>
            {
                try
                {
                    await s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                    {
                        BucketName = bucketName,
                        Key = "nonexistent-key.txt"
                    });
                    throw new Exception("Expected error but got none");
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { }
            }));

            results.Add(await runner.RunTestAsync("s3", "PutObject_GetObject_ContentVerification", async () =>
            {
                var content = "Hello, S3 content verification! Japanese test";
                var key = "verify-content.txt";
                await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = key,
                    ContentBody = content,
                    ContentType = "text/plain; charset=utf-8"
                });
                var resp = await s3Client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = key
                });
                using var reader = new StreamReader(resp.ResponseStream!);
                var body = await reader.ReadToEndAsync();
                if (body != content)
                    throw new Exception($"Content mismatch: got {body}");
            }));

            results.Add(await runner.RunTestAsync("s3", "PutObject_Overwrite", async () =>
            {
                var key = "overwrite-test.txt";
                var content1 = "Original content";
                var content2 = "Updated content";
                await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucketName, Key = key, ContentBody = content1
                });
                await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucketName, Key = key, ContentBody = content2
                });
                var resp = await s3Client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = bucketName, Key = key
                });
                using var reader = new StreamReader(resp.ResponseStream!);
                var body = await reader.ReadToEndAsync();
                if (body != content2)
                    throw new Exception($"After overwrite expected {content2}, got {body}");
            }));

            results.Add(await runner.RunTestAsync("s3", "HeadObject_VerifyMetadata", async () =>
            {
                var key = "metadata-test.txt";
                var content = "metadata check";
                var putReq = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = key,
                    ContentBody = content,
                    ContentType = "application/json"
                };
                putReq.Metadata.Add("custom-key", "custom-value");
                await s3Client.PutObjectAsync(putReq);
                var resp = await s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = bucketName, Key = key
                });
                if (resp.ContentType != "application/json")
                    throw new Exception($"ContentType mismatch, got {resp.ContentType}");
            }));

            results.Add(await runner.RunTestAsync("s3", "ListObjectsV2", async () =>
            {
                var resp = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = bucketName
                });
                if (resp.S3Objects == null)
                    throw new Exception("S3Objects is null");
            }));

            results.Add(await runner.RunTestAsync("s3", "DeleteObject", async () =>
            {
                await s3Client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey
                });
            }));

            results.Add(await runner.RunTestAsync("s3", "ListObjectsV2_AfterDelete", async () =>
            {
                var resp = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    Prefix = objectKey
                });
                if (resp.S3Objects != null && resp.S3Objects.Count > 0)
                    throw new Exception("Object still exists after delete");
            }));

            results.Add(await runner.RunTestAsync("s3", "DeleteObject_NonExistentKey", async () =>
            {
                await s3Client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = "nonexistent-delete-key.txt"
                });
            }));

            results.Add(await runner.RunTestAsync("s3", "CopyObject", async () =>
            {
                var srcKey = "copy-source.txt";
                var dstKey = "copy-dest.txt";
                var content = "copy me";
                await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucketName, Key = srcKey, ContentBody = content
                });
                var copyResp = await s3Client.CopyObjectAsync(new CopyObjectRequest
                {
                    DestinationBucket = bucketName,
                    DestinationKey = dstKey,
                    SourceBucket = bucketName,
                    SourceKey = srcKey
                });
                var getResp = await s3Client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = bucketName, Key = dstKey
                });
                using var reader = new StreamReader(getResp.ResponseStream!);
                var body = await reader.ReadToEndAsync();
                if (body != content)
                    throw new Exception($"Copy content mismatch: got {body}");
            }));

            results.Add(await runner.RunTestAsync("s3", "PutBucketTagging", async () =>
            {
                await s3Client.PutBucketTaggingAsync(new PutBucketTaggingRequest
                {
                    BucketName = bucketName,
                    TagSet = new List<Tag> { new Tag { Key = "Environment", Value = "Test" } }
                });
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketTagging", async () =>
            {
                var resp = await s3Client.GetBucketTaggingAsync(new GetBucketTaggingRequest
                {
                    BucketName = bucketName
                });
                if (resp.TagSet == null || resp.TagSet.Count == 0)
                    throw new Exception("TagSet is nil or empty");
                var found = resp.TagSet.Any(t => t.Key == "Environment" && t.Value == "Test");
                if (!found)
                    throw new Exception("Expected tag Environment=Test not found");
            }));

            results.Add(await runner.RunTestAsync("s3", "DeleteBucketTagging", async () =>
            {
                await s3Client.DeleteBucketTaggingAsync(new DeleteBucketTaggingRequest
                {
                    BucketName = bucketName
                });
            }));

            results.Add(await runner.RunTestAsync("s3", "CreateBucket_DuplicateName", async () =>
            {
                try
                {
                    await s3Client.PutBucketAsync(new PutBucketRequest
                    {
                        BucketName = bucketName
                    });
                    throw new Exception("Expected error for duplicate bucket name");
                }
                catch (AmazonS3Exception) { }
            }));

            results.Add(await runner.RunTestAsync("s3", "ListObjectsV2_MultipleObjects", async () =>
            {
                var lobBucket = TestRunner.MakeUniqueName("lob-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = lobBucket });
                try
                {
                    for (int i = 0; i < 5; i++)
                    {
                        await s3Client.PutObjectAsync(new PutObjectRequest
                        {
                            BucketName = lobBucket,
                            Key = $"obj{i}.txt",
                            ContentBody = $"content {i}"
                        });
                    }
                    var resp = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
                    {
                        BucketName = lobBucket
                    });
                    if (resp.S3Objects.Count != 5)
                        throw new Exception($"Expected 5 objects, got {resp.S3Objects.Count}");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = lobBucket }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "ListBuckets_SortedByName", async () =>
            {
                var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var sortBuckets = new[] { $"z-sort-{ts}", $"a-sort-{ts}", $"m-sort-{ts}" };
                foreach (var b in sortBuckets)
                    await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    var resp = await s3Client.ListBucketsAsync(new ListBucketsRequest());
                    for (int i = 1; i < resp.Buckets.Count; i++)
                    {
                        if (string.Compare(resp.Buckets[i].BucketName, resp.Buckets[i - 1].BucketName) < 0)
                            throw new Exception($"Buckets not sorted: {resp.Buckets[i - 1].BucketName} before {resp.Buckets[i].BucketName}");
                    }
                }
                finally
                {
                    foreach (var b in sortBuckets)
                        try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "MultiByteContent", async () =>
            {
                var pairs = new (string Key, string Body)[]
                {
                    ("テスト/日本語ファイル.txt", "こんにちは世界。これは日本語のテストデータです。"),
                    ("文档/简体中文.txt", "你好世界。这是简体中文的测试数据。"),
                    ("文件/繁體中文.txt", "你好世界。這是繁體中文的測試資料。"),
                };
                foreach (var (key, body) in pairs)
                {
                    await s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = bucketName,
                        Key = key,
                        ContentBody = body,
                        ContentType = "text/plain; charset=utf-8"
                    });
                }
                foreach (var (key, body) in pairs)
                {
                    var resp = await s3Client.GetObjectAsync(new GetObjectRequest
                    {
                        BucketName = bucketName,
                        Key = key
                    });
                    using var reader = new StreamReader(resp.ResponseStream!);
                    var actual = await reader.ReadToEndAsync();
                    if (actual != body)
                        throw new Exception($"Mismatch for {key}: expected {body}, got {actual}");
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "DeleteBucket", async () =>
            {
                try
                {
                    var listResp = await s3Client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucketName });
                    if (listResp.S3Objects != null)
                    {
                        foreach (var obj in listResp.S3Objects)
                        {
                            try { await s3Client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucketName, Key = obj.Key }); } catch { }
                        }
                    }
                }
                catch { }
                await s3Client.DeleteBucketAsync(new DeleteBucketRequest
                {
                    BucketName = bucketName
                });
                bucketCreated = false;
            }));
        }
        finally
        {
            if (bucketCreated)
            {
                try
                {
                    await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = bucketName });
                }
                catch { }
            }
        }

        results.Add(await runner.RunTestAsync("s3", "HeadBucket_NonExistent", async () =>
        {
            try
            {
                await s3Client.HeadBucketAsync(new HeadBucketRequest
                {
                    BucketName = "nonexistent-bucket-xyz-12345"
                });
                throw new Exception("Expected 404 Not Found but got success");
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { }
        }));

        return results;
    }
}
