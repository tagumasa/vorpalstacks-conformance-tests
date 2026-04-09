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

            results.Add(await runner.RunTestAsync("s3", "ListBuckets", async () =>
            {
                var response = await s3Client.ListBucketsAsync(new ListBucketsRequest());
                if (response.Buckets == null)
                    throw new Exception("Buckets list is null");
                var found = response.Buckets.Any(b => b.BucketName == bucketName);
                if (!found)
                    throw new Exception("Created bucket not found in list");
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

            results.Add(await runner.RunTestAsync("s3", "HeadBucket", async () =>
            {
                await s3Client.HeadBucketAsync(new HeadBucketRequest
                {
                    BucketName = bucketName
                });
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketLocation", async () =>
            {
                var resp = await s3Client.GetBucketLocationAsync(new GetBucketLocationRequest
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

            results.Add(await runner.RunTestAsync("s3", "PutObject", async () =>
            {
                var response = await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = "test.txt",
                    ContentBody = "Hello, World!",
                    ContentType = "text/plain"
                });
                if (string.IsNullOrEmpty(response.ETag))
                    throw new Exception("ETag is null");
            }));

            results.Add(await runner.RunTestAsync("s3", "GetObject", async () =>
            {
                var response = await s3Client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = "test.txt"
                });
                if (response.ResponseStream == null)
                    throw new Exception("ResponseStream is null");
                using var reader = new StreamReader(response.ResponseStream);
                var bodyStr = await reader.ReadToEndAsync();
                if (bodyStr != "Hello, World!")
                    throw new Exception($"Content mismatch. Expected: Hello, World!, Got: {bodyStr}");
            }));

            results.Add(await runner.RunTestAsync("s3", "HeadObject", async () =>
            {
                var response = await s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = bucketName,
                    Key = "test.txt"
                });
                if (response.ContentLength == 0)
                    throw new Exception("ContentLength is zero");
                if (string.IsNullOrEmpty(response.ETag))
                    throw new Exception("ETag is null");
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
                    Key = "test.txt"
                });
            }));

            results.Add(await runner.RunTestAsync("s3", "ListObjectsAfterDelete", async () =>
            {
                var resp = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    Prefix = "test.txt"
                });
                if (resp.S3Objects != null && resp.S3Objects.Count > 0)
                    throw new Exception("Object still exists after delete");
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

            results.Add(await runner.RunTestAsync("s3", "PutObject_GetObject_ContentVerification", async () =>
            {
                var content = "Hello, S3 content verification!";
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

            results.Add(await runner.RunTestAsync("s3", "MultiByte_PutGetRoundtrip", async () =>
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

            results.Add(await runner.RunTestAsync("s3", "MultiByte_MetadataRoundtrip", async () =>
            {
                var key = "テスト/metadata-日本語.txt";
                var content = "メタデータテスト";
                var putReq = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = key,
                    ContentBody = content,
                    ContentType = "text/plain; charset=utf-8"
                };
                putReq.Metadata.Add("author", "tanaka-taro");
                putReq.Metadata.Add("desc", "japanese-metadata-test");
                putReq.Metadata.Add("project", "multibyte-key-project");
                await s3Client.PutObjectAsync(putReq);
                var resp = await s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = bucketName, Key = key
                });
                if (resp.Metadata == null)
                    throw new Exception("Metadata is null");
                if (resp.Metadata["author"] != "tanaka-taro")
                    throw new Exception($"Expected author=tanaka-taro, got {resp.Metadata["author"]}");
                if (resp.Metadata["desc"] != "japanese-metadata-test")
                    throw new Exception($"Expected desc=japanese-metadata-test, got {resp.Metadata["desc"]}");
            }));

            results.Add(await runner.RunTestAsync("s3", "MultiByte_ListObjectsV2", async () =>
            {
                var prefix = "日本語/";
                for (int i = 0; i < 3; i++)
                {
                    await s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = bucketName,
                        Key = $"{prefix}ファイル{i}.txt",
                        ContentBody = $"コンテンツ{i}"
                    });
                }
                var resp = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    Prefix = prefix
                });
                if (resp.S3Objects == null || resp.S3Objects.Count < 3)
                    throw new Exception($"Expected at least 3 multibyte objects, got {resp.S3Objects?.Count ?? 0}");
            }));

            results.Add(await runner.RunTestAsync("s3", "MultiByte_CopyObject", async () =>
            {
                var srcKey = "コピー元/データ.txt";
                var dstKey = "コピー先/データ.txt";
                var content = "コピーテストデータ";
                await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucketName, Key = srcKey, ContentBody = content, ContentType = "text/plain; charset=utf-8"
                });
                await s3Client.CopyObjectAsync(new CopyObjectRequest
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
                    throw new Exception($"Multibyte copy content mismatch: got {body}");
            }));

            results.Add(await runner.RunTestAsync("s3", "MultiByte_DeleteObjects", async () =>
            {
                var prefix = "削除/";
                var keys = new[] { "ファイルA.txt", "ファイルB.txt", "ファイルC.txt" };
                foreach (var k in keys)
                {
                    await s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = bucketName,
                        Key = $"{prefix}{k}",
                        ContentBody = "削除テスト"
                    });
                }
                var deleteReq = new DeleteObjectsRequest
                {
                    BucketName = bucketName,
                    Objects = keys.Select(k => new KeyVersion { Key = $"{prefix}{k}" }).ToList()
                };
                var delResp = await s3Client.DeleteObjectsAsync(deleteReq);
                if (delResp.DeletedObjects == null || delResp.DeletedObjects.Count < 3)
                    throw new Exception($"Expected 3 deleted, got {delResp.DeletedObjects?.Count ?? 0}");
            }));

            results.Add(await runner.RunTestAsync("s3", "MultiByte_MultipartUpload", async () =>
            {
                var key = "マルチパート/テスト.bin";
                var content = "マルチパートアップロードテストデータ";
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                var initResp = await s3Client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
                {
                    BucketName = bucketName,
                    Key = key,
                    ContentType = "application/octet-stream"
                });
                var uploadId = initResp.UploadId;
                try
                {
                    var partResp = await s3Client.UploadPartAsync(new UploadPartRequest
                    {
                        BucketName = bucketName,
                        Key = key,
                        UploadId = uploadId,
                        PartNumber = 1,
                        InputStream = new MemoryStream(bytes)
                    });
                    await s3Client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
                    {
                        BucketName = bucketName,
                        Key = key,
                        UploadId = uploadId,
                        PartETags = new List<PartETag> { new PartETag(1, partResp.ETag ?? "") }
                    });
                    var getResp = await s3Client.GetObjectAsync(new GetObjectRequest
                    {
                        BucketName = bucketName, Key = key
                    });
                    using var reader = new StreamReader(getResp.ResponseStream!);
                    var body = await reader.ReadToEndAsync();
                    if (body != content)
                        throw new Exception($"Multipart multibyte content mismatch: got {body}");
                }
                catch
                {
                    try { await s3Client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest { BucketName = bucketName, Key = key, UploadId = uploadId }); } catch { }
                    throw;
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "MultiByte_ObjectTagging", async () =>
            {
                var key = "タグ/テスト.txt";
                await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucketName, Key = key, ContentBody = "タグテスト", ContentType = "text/plain; charset=utf-8"
                });
                await s3Client.PutObjectTaggingAsync(new PutObjectTaggingRequest
                {
                    BucketName = bucketName,
                    Key = key,
                    Tagging = new Tagging { TagSet = new List<Tag> { new Tag { Key = "環境", Value = "テスト" } } }
                });
                var tagResp = await s3Client.GetObjectTaggingAsync(new GetObjectTaggingRequest
                {
                    BucketName = bucketName,
                    Key = key
                });
                if (tagResp.Tagging == null || tagResp.Tagging.Count == 0)
                    throw new Exception("Multibyte tag not found");
                var found = tagResp.Tagging.Any(t => t.Key == "環境" && t.Value == "テスト");
                if (!found)
                    throw new Exception("Expected multibyte tag not found");
                await s3Client.DeleteObjectTaggingAsync(new DeleteObjectTaggingRequest
                {
                    BucketName = bucketName,
                    Key = key
                });
            }));

            results.Add(await runner.RunTestAsync("s3", "MultiByte_ContentTypeRoundtrip", async () =>
            {
                var key = "コンテンツタイプ/テスト.json";
                var content = "{\"メッセージ\": \"こんにちは\"}";
                await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = key,
                    ContentBody = content,
                    ContentType = "application/json; charset=utf-8"
                });
                var resp = await s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = bucketName, Key = key
                });
                if (resp.ContentType != "application/json; charset=utf-8")
                    throw new Exception($"Multibyte ContentType mismatch, got {resp.ContentType}");
                var getResp = await s3Client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = bucketName, Key = key
                });
                using var reader = new StreamReader(getResp.ResponseStream!);
                var body = await reader.ReadToEndAsync();
                if (body != content)
                    throw new Exception("Multibyte content-type roundtrip body mismatch");
            }));

            results.Add(await runner.RunTestAsync("s3", "PutBucketAcl", async () =>
            {
                var b = TestRunner.MakeUniqueName("acl-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutACLAsync(new PutACLRequest { BucketName = b, CannedACL = S3CannedACL.Private });
                    var resp = await s3Client.GetACLAsync(new GetACLRequest { BucketName = b });
                    if (resp == null)
                        throw new Exception("GetBucketAcl response is null");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketAcl", async () =>
            {
                var b = TestRunner.MakeUniqueName("acl-get-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutACLAsync(new PutACLRequest { BucketName = b, CannedACL = S3CannedACL.PublicRead });
                    var resp = await s3Client.GetACLAsync(new GetACLRequest { BucketName = b });
                    if (resp == null)
                        throw new Exception("GetBucketAcl response is null");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "PutBucketPolicy", async () =>
            {
                var b = TestRunner.MakeUniqueName("policy-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    var policy = "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":\"*\",\"Action\":\"s3:GetObject\",\"Resource\":\"arn:aws:s3:::*\"}]}";
                    await s3Client.PutBucketPolicyAsync(new PutBucketPolicyRequest { BucketName = b, Policy = policy });
                    var resp = await s3Client.GetBucketPolicyAsync(new GetBucketPolicyRequest { BucketName = b });
                    if (string.IsNullOrEmpty(resp.Policy))
                        throw new Exception("Policy is null or empty");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketPolicy", async () =>
            {
                var b = TestRunner.MakeUniqueName("policy-get-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    var policy = "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":\"*\",\"Action\":\"s3:GetObject\",\"Resource\":\"arn:aws:s3:::*\"}]}";
                    await s3Client.PutBucketPolicyAsync(new PutBucketPolicyRequest { BucketName = b, Policy = policy });
                    var resp = await s3Client.GetBucketPolicyAsync(new GetBucketPolicyRequest { BucketName = b });
                    if (string.IsNullOrEmpty(resp.Policy))
                        throw new Exception("Policy is null or empty");
                    if (!resp.Policy.Contains("Allow"))
                        throw new Exception("Policy content unexpected");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "DeleteBucketPolicy", async () =>
            {
                var b = TestRunner.MakeUniqueName("policy-del-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    var policy = "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":\"*\",\"Action\":\"s3:GetObject\",\"Resource\":\"arn:aws:s3:::*\"}]}";
                    await s3Client.PutBucketPolicyAsync(new PutBucketPolicyRequest { BucketName = b, Policy = policy });
                    await s3Client.DeleteBucketPolicyAsync(new DeleteBucketPolicyRequest { BucketName = b });
                    var policyResp = await s3Client.GetBucketPolicyAsync(new GetBucketPolicyRequest { BucketName = b });
                    if (policyResp.Policy != null && policyResp.Policy.Contains("<Error>"))
                        return;
                    if (!string.IsNullOrEmpty(policyResp?.Policy))
                        throw new Exception($"Policy should not exist after delete, got: {policyResp.Policy}");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketPolicy_NotFound", async () =>
            {
                var b = TestRunner.MakeUniqueName("policy-nf-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    var policyResp = await s3Client.GetBucketPolicyAsync(new GetBucketPolicyRequest { BucketName = b });
                    if (policyResp.Policy != null && policyResp.Policy.Contains("<Error>"))
                        return;
                    if (!string.IsNullOrEmpty(policyResp?.Policy))
                        throw new Exception($"Expected policy not found, got: {policyResp.Policy}");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "PutBucketCors", async () =>
            {
                var b = TestRunner.MakeUniqueName("cors-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutCORSConfigurationAsync(new PutCORSConfigurationRequest
                    {
                        BucketName = b,
                        Configuration = new CORSConfiguration
                        {
                            Rules = new List<CORSRule> { new CORSRule { AllowedMethods = new List<string> { "GET" }, AllowedOrigins = new List<string> { "*" } } }
                        }
                    });
                    var resp = await s3Client.GetCORSConfigurationAsync(new GetCORSConfigurationRequest { BucketName = b });
                    if (resp.Configuration == null || resp.Configuration.Rules == null || resp.Configuration.Rules.Count == 0)
                        throw new Exception("CORS configuration is null or empty");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "DeleteBucketCors", async () =>
            {
                var b = TestRunner.MakeUniqueName("cors-del-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutCORSConfigurationAsync(new PutCORSConfigurationRequest
                    {
                        BucketName = b,
                        Configuration = new CORSConfiguration
                        {
                            Rules = new List<CORSRule> { new CORSRule { AllowedMethods = new List<string> { "GET" }, AllowedOrigins = new List<string> { "*" } } }
                        }
                    });
                    await s3Client.DeleteCORSConfigurationAsync(new DeleteCORSConfigurationRequest { BucketName = b });
                    try
                    {
                        var corsResp = await s3Client.GetCORSConfigurationAsync(new GetCORSConfigurationRequest { BucketName = b });
                        if (corsResp?.Configuration != null)
                            throw new Exception("Expected CORS not found");
                    }
                    catch (AmazonS3Exception ex) when ((int)ex.StatusCode == 404) { }
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketCors_NotFound", async () =>
            {
                var b = TestRunner.MakeUniqueName("cors-nf-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    try
                    {
                        await s3Client.GetCORSConfigurationAsync(new GetCORSConfigurationRequest { BucketName = b });
                        throw new Exception("Expected CORS not found");
                    }
                    catch (AmazonS3Exception ex) when ((int)ex.StatusCode == 404) { }
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "PutBucketEncryption", async () =>
            {
                var b = TestRunner.MakeUniqueName("enc-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketEncryptionAsync(new PutBucketEncryptionRequest
                    {
                        BucketName = b,
                        ServerSideEncryptionConfiguration = new ServerSideEncryptionConfiguration
                        {
                            ServerSideEncryptionRules = new List<ServerSideEncryptionRule>
                            {
                                new ServerSideEncryptionRule
                                {
                                    ServerSideEncryptionByDefault = new ServerSideEncryptionByDefault
                                    {
                                        ServerSideEncryptionAlgorithm = ServerSideEncryptionMethod.AES256
                                    }
                                }
                            }
                        }
                    });
                    var resp = await s3Client.GetBucketEncryptionAsync(new GetBucketEncryptionRequest { BucketName = b });
                    if (resp == null || resp.ServerSideEncryptionConfiguration == null)
                        throw new Exception("Encryption configuration is null");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketEncryption", async () =>
            {
                var b = TestRunner.MakeUniqueName("enc-get-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketEncryptionAsync(new PutBucketEncryptionRequest
                    {
                        BucketName = b,
                        ServerSideEncryptionConfiguration = new ServerSideEncryptionConfiguration
                        {
                            ServerSideEncryptionRules = new List<ServerSideEncryptionRule>
                            {
                                new ServerSideEncryptionRule
                                {
                                    ServerSideEncryptionByDefault = new ServerSideEncryptionByDefault
                                    {
                                        ServerSideEncryptionAlgorithm = ServerSideEncryptionMethod.AES256
                                    }
                                }
                            }
                        }
                    });
                    var resp = await s3Client.GetBucketEncryptionAsync(new GetBucketEncryptionRequest { BucketName = b });
                    if (resp?.ServerSideEncryptionConfiguration?.ServerSideEncryptionRules == null || resp.ServerSideEncryptionConfiguration.ServerSideEncryptionRules.Count == 0)
                        throw new Exception("Encryption rules empty");
                    var rule = resp.ServerSideEncryptionConfiguration.ServerSideEncryptionRules[0];
                    if (rule.ServerSideEncryptionByDefault?.ServerSideEncryptionAlgorithm != ServerSideEncryptionMethod.AES256)
                        throw new Exception($"Expected AES256, got {rule.ServerSideEncryptionByDefault?.ServerSideEncryptionAlgorithm}");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "DeleteBucketEncryption", async () =>
            {
                var b = TestRunner.MakeUniqueName("enc-del-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketEncryptionAsync(new PutBucketEncryptionRequest
                    {
                        BucketName = b,
                        ServerSideEncryptionConfiguration = new ServerSideEncryptionConfiguration
                        {
                            ServerSideEncryptionRules = new List<ServerSideEncryptionRule>
                            {
                                new ServerSideEncryptionRule
                                {
                                    ServerSideEncryptionByDefault = new ServerSideEncryptionByDefault
                                    {
                                        ServerSideEncryptionAlgorithm = ServerSideEncryptionMethod.AES256
                                    }
                                }
                            }
                        }
                    });
                    await s3Client.DeleteBucketEncryptionAsync(new DeleteBucketEncryptionRequest { BucketName = b });
                    try
                    {
                        var encResp = await s3Client.GetBucketEncryptionAsync(new GetBucketEncryptionRequest { BucketName = b });
                        if (encResp?.ServerSideEncryptionConfiguration != null)
                            throw new Exception("Expected encryption not found");
                    }
                    catch (AmazonS3Exception ex) when ((int)ex.StatusCode == 404) { }
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketEncryption_NotFound", async () =>
            {
                var b = TestRunner.MakeUniqueName("enc-nf-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    var encResp = await s3Client.GetBucketEncryptionAsync(new GetBucketEncryptionRequest { BucketName = b });
                    if (encResp?.ServerSideEncryptionConfiguration == null)
                        return;
                    throw new Exception("Expected encryption not found");
                }
                catch (AmazonS3Exception ex) when ((int)ex.StatusCode == 404) { }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "PutBucketVersioning_Enabled", async () =>
            {
                var b = TestRunner.MakeUniqueName("ver-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketVersioningAsync(new PutBucketVersioningRequest
                    {
                        BucketName = b,
                        VersioningConfig = new S3BucketVersioningConfig { Status = VersionStatus.Enabled }
                    });
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketVersioning_Enabled", async () =>
            {
                var b = TestRunner.MakeUniqueName("ver-get-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketVersioningAsync(new PutBucketVersioningRequest
                    {
                        BucketName = b,
                        VersioningConfig = new S3BucketVersioningConfig { Status = VersionStatus.Enabled }
                    });
                    var resp = await s3Client.GetBucketVersioningAsync(new GetBucketVersioningRequest { BucketName = b });
                    if (resp.VersioningConfig.Status != VersionStatus.Enabled)
                        throw new Exception($"Expected Enabled, got {resp.VersioningConfig.Status}");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "PutBucketVersioning_Suspended", async () =>
            {
                var b = TestRunner.MakeUniqueName("ver-sus-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketVersioningAsync(new PutBucketVersioningRequest
                    {
                        BucketName = b,
                        VersioningConfig = new S3BucketVersioningConfig { Status = VersionStatus.Enabled }
                    });
                    await s3Client.PutBucketVersioningAsync(new PutBucketVersioningRequest
                    {
                        BucketName = b,
                        VersioningConfig = new S3BucketVersioningConfig { Status = VersionStatus.Suspended }
                    });
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketVersioning_Suspended", async () =>
            {
                var b = TestRunner.MakeUniqueName("ver-sus-get-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketVersioningAsync(new PutBucketVersioningRequest
                    {
                        BucketName = b,
                        VersioningConfig = new S3BucketVersioningConfig { Status = VersionStatus.Enabled }
                    });
                    await s3Client.PutBucketVersioningAsync(new PutBucketVersioningRequest
                    {
                        BucketName = b,
                        VersioningConfig = new S3BucketVersioningConfig { Status = VersionStatus.Suspended }
                    });
                    var resp = await s3Client.GetBucketVersioningAsync(new GetBucketVersioningRequest { BucketName = b });
                    if (resp.VersioningConfig.Status != VersionStatus.Suspended)
                        throw new Exception($"Expected Suspended, got {resp.VersioningConfig.Status}");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "PutBucketLifecycleConfiguration", async () =>
            {
                var b = TestRunner.MakeUniqueName("lc-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutLifecycleConfigurationAsync(new PutLifecycleConfigurationRequest
                    {
                        BucketName = b,
                        Configuration = new LifecycleConfiguration
                        {
                            Rules = new List<LifecycleRule>
                            {
                                new LifecycleRule
                                {
                                    Id = "test-rule",
                                    Status = LifecycleRuleStatus.Enabled,
                                    Filter = new LifecycleFilter { Prefix = "logs/" },
                                    Expiration = new LifecycleRuleExpiration { Days = 7 }
                                }
                            }
                        }
                    });
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketLifecycleConfiguration", async () =>
            {
                var b = TestRunner.MakeUniqueName("lc-get-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutLifecycleConfigurationAsync(new PutLifecycleConfigurationRequest
                    {
                        BucketName = b,
                        Configuration = new LifecycleConfiguration
                        {
                            Rules = new List<LifecycleRule>
                            {
                                new LifecycleRule
                                {
                                    Id = "test-rule",
                                    Status = LifecycleRuleStatus.Enabled,
                                    Filter = new LifecycleFilter { Prefix = "logs/" },
                                    Expiration = new LifecycleRuleExpiration { Days = 7 }
                                }
                            }
                        }
                    });
                    var resp = await s3Client.GetLifecycleConfigurationAsync(new GetLifecycleConfigurationRequest { BucketName = b });
                    if (resp.Configuration == null || resp.Configuration.Rules == null || resp.Configuration.Rules.Count == 0)
                        throw new Exception("Lifecycle configuration is empty");
                    if (resp.Configuration.Rules[0].Id != "test-rule")
                        throw new Exception($"Expected rule id test-rule, got {resp.Configuration.Rules[0].Id}");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "DeleteBucketLifecycleConfiguration", async () =>
            {
                var b = TestRunner.MakeUniqueName("lc-del-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutLifecycleConfigurationAsync(new PutLifecycleConfigurationRequest
                    {
                        BucketName = b,
                        Configuration = new LifecycleConfiguration
                        {
                            Rules = new List<LifecycleRule>
                            {
                                new LifecycleRule
                                {
                                    Id = "test-rule",
                                    Status = LifecycleRuleStatus.Enabled,
                                    Filter = new LifecycleFilter { Prefix = "logs/" },
                                    Expiration = new LifecycleRuleExpiration { Days = 7 }
                                }
                            }
                        }
                    });
                    await s3Client.DeleteLifecycleConfigurationAsync(new DeleteLifecycleConfigurationRequest { BucketName = b });
                    var lcResp = await s3Client.GetLifecycleConfigurationAsync(new GetLifecycleConfigurationRequest { BucketName = b });
                    if (lcResp.Configuration == null || lcResp.Configuration.Rules == null || lcResp.Configuration.Rules.Count == 0)
                        return;
                    throw new Exception($"Expected lifecycle not found after delete, got {lcResp.Configuration.Rules.Count} rules");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketLifecycleConfiguration_NotFound", async () =>
            {
                var b = TestRunner.MakeUniqueName("lc-nf-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    var lcResp = await s3Client.GetLifecycleConfigurationAsync(new GetLifecycleConfigurationRequest { BucketName = b });
                    if (lcResp.Configuration == null || lcResp.Configuration.Rules == null || lcResp.Configuration.Rules.Count == 0)
                        return;
                    throw new Exception($"Expected lifecycle not found, got {lcResp.Configuration.Rules.Count} rules");
                }
                catch (AmazonS3Exception ex) when ((int)ex.StatusCode == 404) { }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "PutBucketWebsite", async () =>
            {
                var b = TestRunner.MakeUniqueName("web-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketWebsiteAsync(new PutBucketWebsiteRequest
                    {
                        BucketName = b,
                        WebsiteConfiguration = new WebsiteConfiguration
                        {
                            IndexDocumentSuffix = "index.html",
                            ErrorDocument = "error.html"
                        }
                    });
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketWebsite", async () =>
            {
                var b = TestRunner.MakeUniqueName("web-get-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketWebsiteAsync(new PutBucketWebsiteRequest
                    {
                        BucketName = b,
                        WebsiteConfiguration = new WebsiteConfiguration
                        {
                            IndexDocumentSuffix = "index.html",
                            ErrorDocument = "error.html"
                        }
                    });
                    var resp = await s3Client.GetBucketWebsiteAsync(new GetBucketWebsiteRequest { BucketName = b });
                    if (resp.WebsiteConfiguration == null)
                        throw new Exception("Website configuration is null");
                    if (resp.WebsiteConfiguration.IndexDocumentSuffix != "index.html")
                        throw new Exception($"Expected index.html, got {resp.WebsiteConfiguration.IndexDocumentSuffix}");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "DeleteBucketWebsite", async () =>
            {
                var b = TestRunner.MakeUniqueName("web-del-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketWebsiteAsync(new PutBucketWebsiteRequest
                    {
                        BucketName = b,
                        WebsiteConfiguration = new WebsiteConfiguration
                        {
                            IndexDocumentSuffix = "index.html"
                        }
                    });
                    await s3Client.DeleteBucketWebsiteAsync(new DeleteBucketWebsiteRequest { BucketName = b });
                    var webResp = await s3Client.GetBucketWebsiteAsync(new GetBucketWebsiteRequest { BucketName = b });
                    if (webResp.WebsiteConfiguration == null ||
                        string.IsNullOrEmpty(webResp.WebsiteConfiguration.IndexDocumentSuffix))
                        return;
                    throw new Exception($"Expected website not found after delete, got IndexDoc={webResp.WebsiteConfiguration.IndexDocumentSuffix}");
                }
                catch (AmazonS3Exception ex) when ((int)ex.StatusCode == 404) { }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketWebsite_NotFound", async () =>
            {
                var b = TestRunner.MakeUniqueName("web-nf-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    var webResp = await s3Client.GetBucketWebsiteAsync(new GetBucketWebsiteRequest { BucketName = b });
                    if (webResp.WebsiteConfiguration == null ||
                        string.IsNullOrEmpty(webResp.WebsiteConfiguration.IndexDocumentSuffix))
                        return;
                    throw new Exception($"Expected website not found, got IndexDoc={webResp.WebsiteConfiguration.IndexDocumentSuffix}");
                }
                catch (AmazonS3Exception ex) when ((int)ex.StatusCode == 404) { }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketWebsite_NotFound", async () =>
            {
                var b = TestRunner.MakeUniqueName("web-nf-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    var webResp = await s3Client.GetBucketWebsiteAsync(new GetBucketWebsiteRequest { BucketName = b });
                    if (webResp.WebsiteConfiguration == null ||
                        string.IsNullOrEmpty(webResp.WebsiteConfiguration.IndexDocumentSuffix))
                        return;
                    throw new Exception($"Expected website not found, got IndexDoc={webResp.WebsiteConfiguration.IndexDocumentSuffix}");
                }
                catch (AmazonS3Exception ex) when ((int)ex.StatusCode == 404) { }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
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

            results.Add(await runner.RunTestAsync("s3", "PutObjectLockConfiguration", async () =>
            {
                var b = TestRunner.MakeUniqueName("lock-bucket");
                try
                {
                    await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b, ObjectLockEnabledForBucket = true });
                }
                catch
                {
                    await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                }
                try
                {
                    await s3Client.PutObjectLockConfigurationAsync(new PutObjectLockConfigurationRequest
                    {
                        BucketName = b,
                        ObjectLockConfiguration = new ObjectLockConfiguration { ObjectLockEnabled = ObjectLockEnabled.Enabled }
                    });
                    var resp = await s3Client.GetObjectLockConfigurationAsync(new GetObjectLockConfigurationRequest { BucketName = b });
                    if (resp == null || resp.ObjectLockConfiguration == null)
                        throw new Exception("ObjectLockConfiguration is null");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetObjectLockConfiguration", async () =>
            {
                var b = TestRunner.MakeUniqueName("lock-get-bucket");
                try
                {
                    await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b, ObjectLockEnabledForBucket = true });
                }
                catch
                {
                    await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                }
                try
                {
                    await s3Client.PutObjectLockConfigurationAsync(new PutObjectLockConfigurationRequest
                    {
                        BucketName = b,
                        ObjectLockConfiguration = new ObjectLockConfiguration { ObjectLockEnabled = ObjectLockEnabled.Enabled }
                    });
                    var resp = await s3Client.GetObjectLockConfigurationAsync(new GetObjectLockConfigurationRequest { BucketName = b });
                    if (resp?.ObjectLockConfiguration == null)
                        throw new Exception("ObjectLockConfiguration is null");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "PutBucketNotificationConfiguration", async () =>
            {
                var b = TestRunner.MakeUniqueName("notif-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketNotificationAsync(new PutBucketNotificationRequest
                    {
                        BucketName = b,
                        TopicConfigurations = new List<TopicConfiguration>
                        {
                            new TopicConfiguration
                            {
                                Events = new List<EventType> { EventType.ObjectCreatedAll },
                                Topic = "arn:aws:sns:us-east-1:000000000000:test-topic"
                            }
                        }
                    });
                    var resp = await s3Client.GetBucketNotificationAsync(new GetBucketNotificationRequest { BucketName = b });
                    if (resp == null)
                        throw new Exception("NotificationConfiguration is null");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketNotificationConfiguration", async () =>
            {
                var b = TestRunner.MakeUniqueName("notif-get-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketNotificationAsync(new PutBucketNotificationRequest
                    {
                        BucketName = b,
                        TopicConfigurations = new List<TopicConfiguration>
                        {
                            new TopicConfiguration
                            {
                                Events = new List<EventType> { EventType.ObjectCreatedAll },
                                Topic = "arn:aws:sns:us-east-1:000000000000:test-topic"
                            }
                        }
                    });
                    var resp = await s3Client.GetBucketNotificationAsync(new GetBucketNotificationRequest { BucketName = b });
                    if (resp == null)
                        throw new Exception("NotificationConfiguration is null");
                    if (resp.TopicConfigurations == null || resp.TopicConfigurations.Count == 0)
                        throw new Exception("TopicConfigurations is empty");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "PutBucketLogging", async () =>
            {
                var b = TestRunner.MakeUniqueName("log-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketLoggingAsync(new PutBucketLoggingRequest
                    {
                        BucketName = b,
                        LoggingConfig = new S3BucketLoggingConfig
                        {
                            TargetBucketName = b,
                            TargetPrefix = "log-"
                        }
                    });
                    var resp = await s3Client.GetBucketLoggingAsync(new GetBucketLoggingRequest { BucketName = b });
                    if (resp == null)
                        throw new Exception("BucketLoggingConfig is null");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketLogging", async () =>
            {
                var b = TestRunner.MakeUniqueName("log-get-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketLoggingAsync(new PutBucketLoggingRequest
                    {
                        BucketName = b,
                        LoggingConfig = new S3BucketLoggingConfig
                        {
                            TargetBucketName = b,
                            TargetPrefix = "log-"
                        }
                    });
                    var resp = await s3Client.GetBucketLoggingAsync(new GetBucketLoggingRequest { BucketName = b });
                    if (resp?.BucketLoggingConfig == null)
                        throw new Exception("BucketLoggingConfig is null");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "PutPublicAccessBlock", async () =>
            {
                var b = TestRunner.MakeUniqueName("pab-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutPublicAccessBlockAsync(new PutPublicAccessBlockRequest
                    {
                        BucketName = b,
                        PublicAccessBlockConfiguration = new PublicAccessBlockConfiguration
                        {
                            BlockPublicAcls = true,
                            IgnorePublicAcls = true,
                            BlockPublicPolicy = true,
                            RestrictPublicBuckets = true
                        }
                    });
                    var resp = await s3Client.GetPublicAccessBlockAsync(new GetPublicAccessBlockRequest { BucketName = b });
                    if (resp == null || resp.PublicAccessBlockConfiguration == null)
                        throw new Exception("PublicAccessBlockConfiguration is null");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetPublicAccessBlock", async () =>
            {
                var b = TestRunner.MakeUniqueName("pab-get-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutPublicAccessBlockAsync(new PutPublicAccessBlockRequest
                    {
                        BucketName = b,
                        PublicAccessBlockConfiguration = new PublicAccessBlockConfiguration
                        {
                            BlockPublicAcls = true,
                            IgnorePublicAcls = true,
                            BlockPublicPolicy = true,
                            RestrictPublicBuckets = true
                        }
                    });
                    var resp = await s3Client.GetPublicAccessBlockAsync(new GetPublicAccessBlockRequest { BucketName = b });
                    if (resp?.PublicAccessBlockConfiguration == null)
                        throw new Exception("PublicAccessBlockConfiguration is null");
                    if (resp.PublicAccessBlockConfiguration.BlockPublicAcls != true)
                        throw new Exception("BlockPublicAcls should be true");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "DeletePublicAccessBlock", async () =>
            {
                var b = TestRunner.MakeUniqueName("pab-del-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutPublicAccessBlockAsync(new PutPublicAccessBlockRequest
                    {
                        BucketName = b,
                        PublicAccessBlockConfiguration = new PublicAccessBlockConfiguration
                        {
                            BlockPublicAcls = true,
                            IgnorePublicAcls = true,
                            BlockPublicPolicy = true,
                            RestrictPublicBuckets = true
                        }
                    });
                    await s3Client.DeletePublicAccessBlockAsync(new DeletePublicAccessBlockRequest { BucketName = b });
                    try
                    {
                        await s3Client.GetPublicAccessBlockAsync(new GetPublicAccessBlockRequest { BucketName = b });
                        throw new Exception("Expected public access block not found");
                    }
                    catch (AmazonS3Exception ex) when ((int)ex.StatusCode == 404) { }
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetPublicAccessBlock_NotFound", async () =>
            {
                var b = TestRunner.MakeUniqueName("pab-nf-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    try
                    {
                        await s3Client.GetPublicAccessBlockAsync(new GetPublicAccessBlockRequest { BucketName = b });
                        throw new Exception("Expected public access block not found");
                    }
                    catch (AmazonS3Exception ex) when ((int)ex.StatusCode == 404) { }
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "PutBucketOwnershipControls", async () =>
            {
                var b = TestRunner.MakeUniqueName("own-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketOwnershipControlsAsync(new PutBucketOwnershipControlsRequest
                    {
                        BucketName = b,
                        OwnershipControls = new OwnershipControls
                        {
                            Rules = new List<OwnershipControlsRule> { new OwnershipControlsRule { ObjectOwnership = ObjectOwnership.BucketOwnerPreferred } }
                        }
                    });
                    var resp = await s3Client.GetBucketOwnershipControlsAsync(new GetBucketOwnershipControlsRequest { BucketName = b });
                    if (resp == null || resp.OwnershipControls == null)
                        throw new Exception("OwnershipControls is null");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketOwnershipControls", async () =>
            {
                var b = TestRunner.MakeUniqueName("own-get-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketOwnershipControlsAsync(new PutBucketOwnershipControlsRequest
                    {
                        BucketName = b,
                        OwnershipControls = new OwnershipControls
                        {
                            Rules = new List<OwnershipControlsRule> { new OwnershipControlsRule { ObjectOwnership = ObjectOwnership.BucketOwnerPreferred } }
                        }
                    });
                    var resp = await s3Client.GetBucketOwnershipControlsAsync(new GetBucketOwnershipControlsRequest { BucketName = b });
                    if (resp?.OwnershipControls == null || resp.OwnershipControls.Rules == null || resp.OwnershipControls.Rules.Count == 0)
                        throw new Exception("OwnershipControls rules empty");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "DeleteBucketOwnershipControls", async () =>
            {
                var b = TestRunner.MakeUniqueName("own-del-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketOwnershipControlsAsync(new PutBucketOwnershipControlsRequest
                    {
                        BucketName = b,
                        OwnershipControls = new OwnershipControls
                        {
                            Rules = new List<OwnershipControlsRule> { new OwnershipControlsRule { ObjectOwnership = ObjectOwnership.BucketOwnerPreferred } }
                        }
                    });
                    await s3Client.DeleteBucketOwnershipControlsAsync(new DeleteBucketOwnershipControlsRequest { BucketName = b });
                    try
                    {
                        await s3Client.GetBucketOwnershipControlsAsync(new GetBucketOwnershipControlsRequest { BucketName = b });
                        throw new Exception("Expected ownership controls not found");
                    }
                    catch (AmazonS3Exception ex) when ((int)ex.StatusCode == 404) { }
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketOwnershipControls_NotFound", async () =>
            {
                var b = TestRunner.MakeUniqueName("own-nf-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    try
                    {
                        await s3Client.GetBucketOwnershipControlsAsync(new GetBucketOwnershipControlsRequest { BucketName = b });
                        throw new Exception("Expected ownership controls not found");
                    }
                    catch (AmazonS3Exception ex) when ((int)ex.StatusCode == 404) { }
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "PutBucketRequestPayment", async () =>
            {
                var b = TestRunner.MakeUniqueName("pay-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketRequestPaymentAsync(new PutBucketRequestPaymentRequest
                    {
                        BucketName = b,
                        RequestPaymentConfiguration = new RequestPaymentConfiguration { Payer = "Requester" }
                    });
                    var resp = await s3Client.GetBucketRequestPaymentAsync(new GetBucketRequestPaymentRequest { BucketName = b });
                    if (resp == null || resp.Payer != "Requester")
                        throw new Exception($"Expected Requester, got {resp?.Payer}");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketRequestPayment", async () =>
            {
                var b = TestRunner.MakeUniqueName("pay-get-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    var resp = await s3Client.GetBucketRequestPaymentAsync(new GetBucketRequestPaymentRequest { BucketName = b });
                    if (resp == null)
                        throw new Exception("RequestPayment response is null");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "PutBucketAccelerateConfiguration", async () =>
            {
                var b = TestRunner.MakeUniqueName("accel-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketAccelerateConfigurationAsync(new PutBucketAccelerateConfigurationRequest
                    {
                        BucketName = b,
                        AccelerateConfiguration = new AccelerateConfiguration { Status = BucketAccelerateStatus.Enabled }
                    });
                    var resp = await s3Client.GetBucketAccelerateConfigurationAsync(new GetBucketAccelerateConfigurationRequest { BucketName = b });
                    if (resp == null)
                        throw new Exception("AccelerateConfiguration response is null");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetBucketAccelerateConfiguration", async () =>
            {
                var b = TestRunner.MakeUniqueName("accel-get-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketAccelerateConfigurationAsync(new PutBucketAccelerateConfigurationRequest
                    {
                        BucketName = b,
                        AccelerateConfiguration = new AccelerateConfiguration { Status = BucketAccelerateStatus.Enabled }
                    });
                    var resp = await s3Client.GetBucketAccelerateConfigurationAsync(new GetBucketAccelerateConfigurationRequest { BucketName = b });
                    if (resp?.Status != BucketAccelerateStatus.Enabled)
                        throw new Exception($"Expected Enabled, got {resp?.Status}");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "PutObjectTagging", async () =>
            {
                var k = "tagging-test.txt";
                await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucketName, Key = k, ContentBody = "tag test"
                });
                await s3Client.PutObjectTaggingAsync(new PutObjectTaggingRequest
                {
                    BucketName = bucketName,
                    Key = k,
                    Tagging = new Tagging { TagSet = new List<Tag> { new Tag { Key = "env", Value = "test" } } }
                });
            }));

            results.Add(await runner.RunTestAsync("s3", "GetObjectTagging", async () =>
            {
                var k = "tagging-test.txt";
                var resp = await s3Client.GetObjectTaggingAsync(new GetObjectTaggingRequest
                {
                    BucketName = bucketName,
                    Key = k
                });
                if (resp.Tagging == null || resp.Tagging.Count == 0)
                    throw new Exception("Tagging is empty");
                var found = resp.Tagging.Any(t => t.Key == "env" && t.Value == "test");
                if (!found)
                    throw new Exception("Expected tag env=test not found");
            }));

            results.Add(await runner.RunTestAsync("s3", "DeleteObjectTagging", async () =>
            {
                var k = "tagging-test.txt";
                await s3Client.DeleteObjectTaggingAsync(new DeleteObjectTaggingRequest
                {
                    BucketName = bucketName,
                    Key = k
                });
            }));

            results.Add(await runner.RunTestAsync("s3", "GetObjectTagging_Empty", async () =>
            {
                var k = "tagging-test.txt";
                var resp = await s3Client.GetObjectTaggingAsync(new GetObjectTaggingRequest
                {
                    BucketName = bucketName,
                    Key = k
                });
                if (resp.Tagging != null && resp.Tagging.Count > 0)
                    throw new Exception($"Expected empty tags, got {resp.Tagging.Count} tags");
            }));

            results.Add(await runner.RunTestAsync("s3", "GetObjectAcl", async () =>
            {
                var k = "acl-obj-test.txt";
                await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucketName, Key = k, ContentBody = "acl test"
                });
                var resp = await s3Client.GetACLAsync(new GetACLRequest
                {
                    BucketName = bucketName,
                    Key = k
                });
                if (resp == null)
                    throw new Exception("GetObjectAcl response is null");
            }));

            results.Add(await runner.RunTestAsync("s3", "PutObjectAcl", async () =>
            {
                var k = "acl-obj-test.txt";
                await s3Client.PutACLAsync(new PutACLRequest
                {
                    BucketName = bucketName,
                    Key = k,
                    CannedACL = S3CannedACL.PublicRead
                });
                var resp = await s3Client.GetACLAsync(new GetACLRequest
                {
                    BucketName = bucketName,
                    Key = k
                });
                if (resp == null)
                    throw new Exception("GetObjectAcl after PutObjectAcl is null");
            }));

            results.Add(await runner.RunTestAsync("s3", "PutObjectLegalHold", async () =>
            {
                var b = TestRunner.MakeUniqueName("legal-bucket");
                try
                {
                    await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b, ObjectLockEnabledForBucket = true });
                }
                catch
                {
                    await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                }
                try
                {
                    await s3Client.PutObjectLockConfigurationAsync(new PutObjectLockConfigurationRequest
                    {
                        BucketName = b,
                        ObjectLockConfiguration = new ObjectLockConfiguration { ObjectLockEnabled = ObjectLockEnabled.Enabled }
                    });
                    var k = "legal-hold.txt";
                    await s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = b, Key = k, ContentBody = "legal hold test"
                    });
                    await s3Client.PutObjectLegalHoldAsync(new PutObjectLegalHoldRequest
                    {
                        BucketName = b,
                        Key = k,
                        LegalHold = new ObjectLockLegalHold { Status = ObjectLockLegalHoldStatus.On }
                    });
                    var resp = await s3Client.GetObjectLegalHoldAsync(new GetObjectLegalHoldRequest
                    {
                        BucketName = b,
                        Key = k
                    });
                    if (resp?.LegalHold?.Status != ObjectLockLegalHoldStatus.On)
                        throw new Exception($"Expected On, got {resp?.LegalHold?.Status}");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetObjectLegalHold", async () =>
            {
                var b = TestRunner.MakeUniqueName("legal-get-bucket");
                try
                {
                    await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b, ObjectLockEnabledForBucket = true });
                }
                catch
                {
                    await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                }
                try
                {
                    await s3Client.PutObjectLockConfigurationAsync(new PutObjectLockConfigurationRequest
                    {
                        BucketName = b,
                        ObjectLockConfiguration = new ObjectLockConfiguration { ObjectLockEnabled = ObjectLockEnabled.Enabled }
                    });
                    var k = "legal-get.txt";
                    await s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = b, Key = k, ContentBody = "legal hold get test"
                    });
                    await s3Client.PutObjectLegalHoldAsync(new PutObjectLegalHoldRequest
                    {
                        BucketName = b,
                        Key = k,
                        LegalHold = new ObjectLockLegalHold { Status = ObjectLockLegalHoldStatus.On }
                    });
                    var resp = await s3Client.GetObjectLegalHoldAsync(new GetObjectLegalHoldRequest
                    {
                        BucketName = b,
                        Key = k
                    });
                    if (resp?.LegalHold == null)
                        throw new Exception("LegalHold is null");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "PutObjectRetention", async () =>
            {
                var b = TestRunner.MakeUniqueName("retention-bucket");
                try
                {
                    await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b, ObjectLockEnabledForBucket = true });
                }
                catch
                {
                    await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                }
                try
                {
                    await s3Client.PutObjectLockConfigurationAsync(new PutObjectLockConfigurationRequest
                    {
                        BucketName = b,
                        ObjectLockConfiguration = new ObjectLockConfiguration { ObjectLockEnabled = ObjectLockEnabled.Enabled }
                    });
                    var k = "retention.txt";
                    await s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = b, Key = k, ContentBody = "retention test"
                    });
                    await s3Client.PutObjectRetentionAsync(new PutObjectRetentionRequest
                    {
                        BucketName = b,
                        Key = k,
                        Retention = new ObjectLockRetention { Mode = ObjectLockRetentionMode.Governance, RetainUntilDate = DateTime.UtcNow.AddDays(1) }
                    });
                    var resp = await s3Client.GetObjectRetentionAsync(new GetObjectRetentionRequest
                    {
                        BucketName = b,
                        Key = k
                    });
                    if (resp?.Retention == null)
                        throw new Exception("Retention is null");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetObjectRetention", async () =>
            {
                var b = TestRunner.MakeUniqueName("retention-get-bucket");
                try
                {
                    await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b, ObjectLockEnabledForBucket = true });
                }
                catch
                {
                    await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                }
                try
                {
                    await s3Client.PutObjectLockConfigurationAsync(new PutObjectLockConfigurationRequest
                    {
                        BucketName = b,
                        ObjectLockConfiguration = new ObjectLockConfiguration { ObjectLockEnabled = ObjectLockEnabled.Enabled }
                    });
                    var k = "retention-get.txt";
                    await s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = b, Key = k, ContentBody = "retention get test"
                    });
                    await s3Client.PutObjectRetentionAsync(new PutObjectRetentionRequest
                    {
                        BucketName = b,
                        Key = k,
                        Retention = new ObjectLockRetention { Mode = ObjectLockRetentionMode.Governance, RetainUntilDate = DateTime.UtcNow.AddDays(1) }
                    });
                    var resp = await s3Client.GetObjectRetentionAsync(new GetObjectRetentionRequest
                    {
                        BucketName = b,
                        Key = k
                    });
                    if (resp?.Retention?.Mode != ObjectLockRetentionMode.Governance)
                        throw new Exception($"Expected Governance, got {resp?.Retention?.Mode}");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "GetObjectAttributes", async () =>
            {
                var k = "attrs-test.txt";
                var content = "attributes test content";
                await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucketName, Key = k, ContentBody = content
                });
                var resp = await s3Client.GetObjectAttributesAsync(new GetObjectAttributesRequest
                {
                    BucketName = bucketName,
                    Key = k,
                    ObjectAttributes = new List<ObjectAttributes> { ObjectAttributes.ETag, ObjectAttributes.StorageClass, ObjectAttributes.ObjectSize }
                });
                if (resp == null)
                    throw new Exception("GetObjectAttributes response is null");
            }));

            results.Add(await runner.RunTestAsync("s3", "CreateMultipartUpload", async () =>
            {
                var k = "multipart-test.bin";
                var initResp = await s3Client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
                {
                    BucketName = bucketName,
                    Key = k
                });
                if (string.IsNullOrEmpty(initResp.UploadId))
                    throw new Exception("UploadId is null or empty");
                try { await s3Client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest { BucketName = bucketName, Key = k, UploadId = initResp.UploadId }); } catch { }
            }));

            results.Add(await runner.RunTestAsync("s3", "CreateMultipartUpload_Initiate", async () =>
            {
                var k = "multipart-initiate.bin";
                var initResp = await s3Client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
                {
                    BucketName = bucketName,
                    Key = k
                });
                if (string.IsNullOrEmpty(initResp.UploadId))
                    throw new Exception("UploadId is null or empty");
                try { await s3Client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest { BucketName = bucketName, Key = k, UploadId = initResp.UploadId }); } catch { }
            }));

            results.Add(await runner.RunTestAsync("s3", "UploadPart", async () =>
            {
                var k = "multipart-upload-part.bin";
                var content = "upload part test data here";
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                var initResp = await s3Client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
                {
                    BucketName = bucketName,
                    Key = k
                });
                var uploadId = initResp.UploadId;
                try
                {
                    var partResp = await s3Client.UploadPartAsync(new UploadPartRequest
                    {
                        BucketName = bucketName,
                        Key = k,
                        UploadId = uploadId,
                        PartNumber = 1,
                        InputStream = new MemoryStream(bytes)
                    });
                    if (string.IsNullOrEmpty(partResp.ETag))
                        throw new Exception("Part ETag is null or empty");
                }
                finally
                {
                    try { await s3Client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest { BucketName = bucketName, Key = k, UploadId = uploadId }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "UploadPart_Part2", async () =>
            {
                var k = "multipart-upload-part2.bin";
                var content1 = "part one content";
                var content2 = "part two content";
                var initResp = await s3Client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
                {
                    BucketName = bucketName,
                    Key = k
                });
                var uploadId = initResp.UploadId;
                try
                {
                    var part1Resp = await s3Client.UploadPartAsync(new UploadPartRequest
                    {
                        BucketName = bucketName, Key = k, UploadId = uploadId, PartNumber = 1,
                        InputStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content1))
                    });
                    var part2Resp = await s3Client.UploadPartAsync(new UploadPartRequest
                    {
                        BucketName = bucketName, Key = k, UploadId = uploadId, PartNumber = 2,
                        InputStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content2))
                    });
                    if (string.IsNullOrEmpty(part2Resp.ETag))
                        throw new Exception("Part 2 ETag is null or empty");
                }
                finally
                {
                    try { await s3Client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest { BucketName = bucketName, Key = k, UploadId = uploadId }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "ListParts", async () =>
            {
                var k = "multipart-list-parts.bin";
                var content1 = "part one content";
                var content2 = "part two content";
                var initResp = await s3Client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
                {
                    BucketName = bucketName,
                    Key = k
                });
                var uploadId = initResp.UploadId;
                try
                {
                    await s3Client.UploadPartAsync(new UploadPartRequest
                    {
                        BucketName = bucketName, Key = k, UploadId = uploadId, PartNumber = 1,
                        InputStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content1))
                    });
                    await s3Client.UploadPartAsync(new UploadPartRequest
                    {
                        BucketName = bucketName, Key = k, UploadId = uploadId, PartNumber = 2,
                        InputStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content2))
                    });
                    var listResp = await s3Client.ListPartsAsync(new ListPartsRequest
                    {
                        BucketName = bucketName, Key = k, UploadId = uploadId
                    });
                    if (listResp.Parts == null || listResp.Parts.Count != 2)
                        throw new Exception($"Expected 2 parts, got {listResp.Parts?.Count ?? 0}");
                }
                finally
                {
                    try { await s3Client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest { BucketName = bucketName, Key = k, UploadId = uploadId }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "CompleteMultipartUpload", async () =>
            {
                var k = "multipart-complete.bin";
                var content = "complete multipart test data";
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                var initResp = await s3Client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
                {
                    BucketName = bucketName,
                    Key = k
                });
                var uploadId = initResp.UploadId;
                try
                {
                    var partResp = await s3Client.UploadPartAsync(new UploadPartRequest
                    {
                        BucketName = bucketName,
                        Key = k,
                        UploadId = uploadId,
                        PartNumber = 1,
                        InputStream = new MemoryStream(bytes)
                    });
                    await s3Client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
                    {
                        BucketName = bucketName,
                        Key = k,
                        UploadId = uploadId,
                        PartETags = new List<PartETag> { new PartETag(1, partResp.ETag ?? "") }
                    });
                }
                catch
                {
                    try { await s3Client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest { BucketName = bucketName, Key = k, UploadId = uploadId }); } catch { }
                    throw;
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "MultipartUpload_GetObject", async () =>
            {
                var k = "multipart-get-verify.bin";
                var content = "multipart get object verification data";
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                var initResp = await s3Client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
                {
                    BucketName = bucketName,
                    Key = k
                });
                var uploadId = initResp.UploadId;
                try
                {
                    var partResp = await s3Client.UploadPartAsync(new UploadPartRequest
                    {
                        BucketName = bucketName,
                        Key = k,
                        UploadId = uploadId,
                        PartNumber = 1,
                        InputStream = new MemoryStream(bytes)
                    });
                    await s3Client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
                    {
                        BucketName = bucketName,
                        Key = k,
                        UploadId = uploadId,
                        PartETags = new List<PartETag> { new PartETag(1, partResp.ETag ?? "") }
                    });
                    var getResp = await s3Client.GetObjectAsync(new GetObjectRequest
                    {
                        BucketName = bucketName, Key = k
                    });
                    using var reader = new StreamReader(getResp.ResponseStream!);
                    var body = await reader.ReadToEndAsync();
                    if (body != content)
                        throw new Exception($"Multipart content mismatch: got {body}");
                }
                catch
                {
                    try { await s3Client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest { BucketName = bucketName, Key = k, UploadId = uploadId }); } catch { }
                    throw;
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "AbortMultipartUpload", async () =>
            {
                var k = "multipart-abort.bin";
                var content = "abort multipart test data";
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                var initResp = await s3Client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
                {
                    BucketName = bucketName,
                    Key = k
                });
                var uploadId = initResp.UploadId;
                await s3Client.UploadPartAsync(new UploadPartRequest
                {
                    BucketName = bucketName,
                    Key = k,
                    UploadId = uploadId,
                    PartNumber = 1,
                    InputStream = new MemoryStream(bytes)
                });
                await s3Client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
                {
                    BucketName = bucketName,
                    Key = k,
                    UploadId = uploadId
                });
            }));

            results.Add(await runner.RunTestAsync("s3", "ListMultipartUploads", async () =>
            {
                var k = "multipart-list-uploads.bin";
                var content = "list multipart uploads test";
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                var initResp = await s3Client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
                {
                    BucketName = bucketName,
                    Key = k
                });
                var uploadId = initResp.UploadId;
                try
                {
                    await s3Client.UploadPartAsync(new UploadPartRequest
                    {
                        BucketName = bucketName,
                        Key = k,
                        UploadId = uploadId,
                        PartNumber = 1,
                        InputStream = new MemoryStream(bytes)
                    });
                    var listResp = await s3Client.ListMultipartUploadsAsync(new ListMultipartUploadsRequest { BucketName = bucketName });
                    if (listResp.MultipartUploads == null || listResp.MultipartUploads.Count == 0)
                        throw new Exception("No multipart uploads listed");
                    var found = listResp.MultipartUploads.Any(u => u.UploadId == uploadId);
                    if (!found)
                        throw new Exception("Initiated upload not found in list");
                }
                finally
                {
                    try { await s3Client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest { BucketName = bucketName, Key = k, UploadId = uploadId }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "ListObjectVersions", async () =>
            {
                var b = TestRunner.MakeUniqueName("versions-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutBucketVersioningAsync(new PutBucketVersioningRequest
                    {
                        BucketName = b,
                        VersioningConfig = new S3BucketVersioningConfig { Status = VersionStatus.Enabled }
                    });
                    await s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = b, Key = "versioned.txt", ContentBody = "v1"
                    });
                    await s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = b, Key = "versioned.txt", ContentBody = "v2"
                    });
                    var resp = await s3Client.ListVersionsAsync(new ListVersionsRequest { BucketName = b });
                    if (resp.Versions == null || resp.Versions.Count == 0)
                        throw new Exception("No versions listed");
                }
                finally
                {
                    try
                    {
                        var listResp = await s3Client.ListVersionsAsync(new ListVersionsRequest { BucketName = b });
                        if (listResp.Versions != null)
                        {
                            foreach (var v in listResp.Versions)
                            {
                                try { await s3Client.DeleteObjectAsync(b, v.Key, v.VersionId); } catch { }
                            }
                        }
                        await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b });
                    }
                    catch { }
                }
            }));

            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = "select-data.csv",
                ContentBody = "name,age\nAlice,30\nBob,25\n"
            });

            results.Add(await runner.RunTestAsync("s3", "SelectObjectContent", async () =>
            {
                var resp = await s3Client.SelectObjectContentAsync(new SelectObjectContentRequest
                {
                    BucketName = bucketName,
                    Key = "select-data.csv",
                    Expression = "SELECT s.name FROM s3object s WHERE s.age = '30'",
                    ExpressionType = "SQL",
                    InputSerialization = new InputSerialization
                    {
                        CSV = new CSVInput
                        {
                            FileHeaderInfo = FileHeaderInfo.Use
                        }
                    },
                    OutputSerialization = new OutputSerialization
                    {
                        CSV = new CSVOutput()
                    }
                });
                if (resp.Payload == null)
                    throw new Exception("Payload (event stream) is null");
                var recordData = new List<string>();
                foreach (var ev in resp.Payload)
                {
                    if (ev is RecordsEvent rec)
                    {
                        using var sr = new StreamReader(rec.Payload);
                        recordData.Add(await sr.ReadToEndAsync());
                    }
                }
                if (recordData.Count == 0)
                    throw new Exception("Expected at least one record result");
            }));

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

            results.Add(await runner.RunTestAsync("s3", "DeleteObject_NonExistentKey", async () =>
            {
                await s3Client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = "nonexistent-delete-key.txt"
                });
            }));

            results.Add(await runner.RunTestAsync("s3", "DeleteBucket_NotEmpty", async () =>
            {
                var b = TestRunner.MakeUniqueName("notempty-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    await s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = b, Key = "keep.txt", ContentBody = "keep"
                    });
                    try
                    {
                        await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b });
                        throw new Exception("Expected error deleting non-empty bucket");
                    }
                    catch (AmazonS3Exception) { }
                }
                finally
                {
                    try { await s3Client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = b, Key = "keep.txt" }); } catch { }
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("s3", "ListObjectsV2_Pagination", async () =>
            {
                var b = TestRunner.MakeUniqueName("pag-bucket");
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = b });
                try
                {
                    for (int i = 0; i < 10; i++)
                    {
                        await s3Client.PutObjectAsync(new PutObjectRequest
                        {
                            BucketName = b,
                            Key = $"page/obj{i}.txt",
                            ContentBody = $"content {i}"
                        });
                    }
                    var allKeys = new List<string>();
                    string? continuationToken = null;
                    do
                    {
                        var resp = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
                        {
                            BucketName = b,
                            Prefix = "page/",
                            MaxKeys = 3,
                            ContinuationToken = continuationToken
                        });
                        if (resp.S3Objects != null)
                        {
                            foreach (var obj in resp.S3Objects)
                                allKeys.Add(obj.Key);
                        }
                        continuationToken = resp.IsTruncated == true ? resp.NextContinuationToken : null;
                    } while (continuationToken != null);

                    if (allKeys.Count != 10)
                        throw new Exception($"Expected 10 keys via pagination, got {allKeys.Count}");
                }
                finally
                {
                    try { await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = b }); } catch { }
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

        return results;
    }
}
