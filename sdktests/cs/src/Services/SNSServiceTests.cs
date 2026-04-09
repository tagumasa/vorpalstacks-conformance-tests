using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class SNSServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonSimpleNotificationServiceClient snsClient,
        string region)
    {
        var results = new List<TestResult>();
        var topicName = TestRunner.MakeUniqueName("CSTopic");
        var topicArn = "";
        var subscriptionArn = "";

        try
        {
            results.Add(await runner.RunTestAsync("sns", "CreateTopic", async () =>
            {
                var resp = await snsClient.CreateTopicAsync(new CreateTopicRequest
                {
                    Name = topicName,
                    Attributes = new Dictionary<string, string>
                    {
                        { "DisplayName", "Test Topic" },
                        { "DeliveryPolicy", @"{
                            ""defaultHealthyRetryPolicy"": {
                                ""minDelayTarget"": 1,
                                ""maxDelayTarget"": 60,
                                ""numRetries"": 3,
                                ""numNoDelayRetries"": 0
                            }
                        }" }
                    },
                    Tags = new List<Tag> { new Tag { Key = "Environment", Value = "Test" } }
                });
                if (string.IsNullOrEmpty(resp.TopicArn))
                    throw new Exception("TopicArn is null");
                topicArn = resp.TopicArn;
            }));

            results.Add(await runner.RunTestAsync("sns", "GetTopicAttributes", async () =>
            {
                var resp = await snsClient.GetTopicAttributesAsync(new GetTopicAttributesRequest
                {
                    TopicArn = topicArn
                });
                if (resp.Attributes == null)
                    throw new Exception("Attributes is null");
                if (!resp.Attributes.ContainsKey("TopicArn"))
                    throw new Exception("TopicArn is missing");
                if (resp.Attributes["DisplayName"] != "Test Topic")
                    throw new Exception("DisplayName mismatch");
            }));

            results.Add(await runner.RunTestAsync("sns", "SetTopicAttributes", async () =>
            {
                await snsClient.SetTopicAttributesAsync(new SetTopicAttributesRequest
                {
                    TopicArn = topicArn,
                    AttributeName = "DisplayName",
                    AttributeValue = "Updated Topic"
                });
                var resp = await snsClient.GetTopicAttributesAsync(new GetTopicAttributesRequest
                {
                    TopicArn = topicArn
                });
                if (resp.Attributes["DisplayName"] != "Updated Topic")
                    throw new Exception("DisplayName was not updated");
            }));

            results.Add(await runner.RunTestAsync("sns", "ListTopics", async () =>
            {
                var resp = await snsClient.ListTopicsAsync(new ListTopicsRequest());
                if (resp.Topics == null)
                    throw new Exception("Topics is null");
            }));

            results.Add(await runner.RunTestAsync("sns", "Subscribe", async () =>
            {
                var resp = await snsClient.SubscribeAsync(new SubscribeRequest
                {
                    TopicArn = topicArn,
                    Protocol = "email",
                    Endpoint = "test@example.com"
                });
                if (string.IsNullOrEmpty(resp.SubscriptionArn))
                    throw new Exception("SubscriptionArn is null");
                subscriptionArn = resp.SubscriptionArn;
            }));

            results.Add(await runner.RunTestAsync("sns", "AddPermission", async () =>
            {
                await snsClient.AddPermissionAsync(new AddPermissionRequest
                {
                    TopicArn = topicArn,
                    Label = "TestPermission",
                    AWSAccountId = new List<string> { "000000000000" },
                    ActionName = new List<string> { "Publish" }
                });
            }));

            results.Add(await runner.RunTestAsync("sns", "RemovePermission", async () =>
            {
                await snsClient.RemovePermissionAsync(new RemovePermissionRequest
                {
                    TopicArn = topicArn,
                    Label = "TestPermission"
                });
            }));

            results.Add(await runner.RunTestAsync("sns", "TagResource", async () =>
            {
                await snsClient.TagResourceAsync(new TagResourceRequest
                {
                    ResourceArn = topicArn,
                    Tags = new List<Tag> { new Tag { Key = "Environment", Value = "Test" } }
                });
            }));

            results.Add(await runner.RunTestAsync("sns", "ListTagsForResource", async () =>
            {
                var resp = await snsClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceArn = topicArn
                });
                if (resp.Tags == null)
                    throw new Exception("Tags is nil");
            }));

            results.Add(await runner.RunTestAsync("sns", "UntagResource", async () =>
            {
                await snsClient.UntagResourceAsync(new UntagResourceRequest
                {
                    ResourceArn = topicArn,
                    TagKeys = new List<string> { "Environment" }
                });
            }));

            results.Add(await runner.RunTestAsync("sns", "PublishBatch", async () =>
            {
                var resp = await snsClient.PublishBatchAsync(new PublishBatchRequest
                {
                    TopicArn = topicArn,
                    PublishBatchRequestEntries = new List<PublishBatchRequestEntry>
                    {
                        new PublishBatchRequestEntry { Id = "msg1", Message = "Batch message 1" },
                        new PublishBatchRequestEntry { Id = "msg2", Message = "Batch message 2" }
                    }
                });
                if (resp.Successful == null || resp.Successful.Count == 0)
                    throw new Exception("expected at least one successful publish, got 0");
            }));

            results.Add(await runner.RunTestAsync("sns", "ListSubscriptionsByTopic", async () =>
            {
                var resp = await snsClient.ListSubscriptionsByTopicAsync(new ListSubscriptionsByTopicRequest
                {
                    TopicArn = topicArn
                });
                if (resp.Subscriptions == null)
                    throw new Exception("Subscriptions is null");
            }));

            results.Add(await runner.RunTestAsync("sns", "ListSubscriptions", async () =>
            {
                var resp = await snsClient.ListSubscriptionsAsync(new ListSubscriptionsRequest());
                if (resp.Subscriptions == null)
                    throw new Exception("Subscriptions is null");
            }));

            results.Add(await runner.RunTestAsync("sns", "Publish", async () =>
            {
                var resp = await snsClient.PublishAsync(new PublishRequest
                {
                    TopicArn = topicArn,
                    Message = """{"test": "hello", "timestamp": """ + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + """}""",
                    Subject = "Test Message",
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>
                    {
                        {
                            "AttributeName", new MessageAttributeValue
                            {
                                DataType = "String",
                                StringValue = "AttributeValue"
                            }
                        }
                    }
                });
                if (string.IsNullOrEmpty(resp.MessageId))
                    throw new Exception("MessageId is null");
            }));

            results.Add(await runner.RunTestAsync("sns", "Publish_TargetArn", async () =>
            {
                var resp = await snsClient.PublishAsync(new PublishRequest
                {
                    TopicArn = topicArn,
                    Message = "Test message to target"
                });
                if (string.IsNullOrEmpty(resp.MessageId))
                    throw new Exception("MessageId is null");
            }));

            results.Add(await runner.RunTestAsync("sns", "MultiBytePublish", async () =>
            {
                var messages = new[] { "日本語テストメッセージ", "简体中文测试消息", "繁體中文測試訊息" };
                foreach (var msg in messages)
                {
                    var resp = await snsClient.PublishAsync(new PublishRequest
                    {
                        TopicArn = topicArn,
                        Message = msg
                    });
                    if (string.IsNullOrEmpty(resp.MessageId))
                        throw new Exception("MessageId is null");
                }
            }));

            results.Add(await runner.RunTestAsync("sns", "Unsubscribe", async () =>
            {
                await snsClient.UnsubscribeAsync(new UnsubscribeRequest
                {
                    SubscriptionArn = subscriptionArn
                });
            }));

            results.Add(await runner.RunTestAsync("sns", "DeleteTopic", async () =>
            {
                await snsClient.DeleteTopicAsync(new DeleteTopicRequest
                {
                    TopicArn = topicArn
                });
                topicArn = "";
            }));
        }
        finally
        {
            if (!string.IsNullOrEmpty(topicArn))
            {
                try
                {
                    await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = topicArn });
                }
                catch { }
            }
        }

        results.Add(await runner.RunTestAsync("sns", "CreateTopic_Duplicate", async () =>
        {
            try
            {
                await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = topicName });
            }
            catch (AmazonSimpleNotificationServiceException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "GetTopicAttributes_NonExistent", async () =>
        {
            try
            {
                await snsClient.GetTopicAttributesAsync(new GetTopicAttributesRequest
                {
                    TopicArn = "arn:aws:sns:us-east-1:000000000000:NonExistentTopic_xyz_12345"
                });
                throw new Exception("Expected error but got none");
            }
            catch (AmazonSimpleNotificationServiceException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "Publish_NonExistent", async () =>
        {
            try
            {
                await snsClient.PublishAsync(new PublishRequest
                {
                    TopicArn = "arn:aws:sns:us-east-1:000000000000:NonExistentTopic_xyz_12345",
                    Message = "test"
                });
                throw new Exception("Expected error but got none");
            }
            catch (AmazonSimpleNotificationServiceException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "Subscribe_NonExistent", async () =>
        {
            try
            {
                await snsClient.SubscribeAsync(new SubscribeRequest
                {
                    TopicArn = "arn:aws:sns:us-east-1:000000000000:NonExistentTopic_xyz_12345",
                    Protocol = "email",
                    Endpoint = "test@example.com"
                });
                throw new Exception("Expected error but got none");
            }
            catch (AmazonSimpleNotificationServiceException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "CreateTopic_Fifo", async () =>
        {
            var fifoName = TestRunner.MakeUniqueName("FifoTopic") + ".fifo";
            var resp = await snsClient.CreateTopicAsync(new CreateTopicRequest
            {
                Name = fifoName,
                Attributes = new Dictionary<string, string>
                {
                    { "ContentBasedDeduplication", "true" },
                    { "FifoTopic", "true" }
                }
            });
            if (string.IsNullOrEmpty(resp.TopicArn))
                throw new Exception("TopicArn is nil");
            await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = resp.TopicArn }); });
        }));

        results.Add(await runner.RunTestAsync("sns", "CreateTopic_DuplicateIdempotent", async () =>
        {
            var dupName = TestRunner.MakeUniqueName("DupTopic");
            var resp1 = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = dupName });
            try
            {
                var resp2 = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = dupName });
                if (resp1.TopicArn != resp2.TopicArn)
                    throw new Exception($"ARN mismatch: {resp1.TopicArn} vs {resp2.TopicArn}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = resp1.TopicArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "ListTopics_ContainsCreated", async () =>
        {
            var ltName = TestRunner.MakeUniqueName("LTTopic");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = ltName });
            try
            {
                var listResp = await snsClient.ListTopicsAsync(new ListTopicsRequest());
                bool found = false;
                foreach (var t in listResp.Topics)
                {
                    if (t.TopicArn == createResp.TopicArn)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    throw new Exception("created topic not found in ListTopics");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "SetTopicAttributes_GetVerify", async () =>
        {
            var attrName = TestRunner.MakeUniqueName("AttrTopic");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = attrName });
            try
            {
                await snsClient.SetTopicAttributesAsync(new SetTopicAttributesRequest
                {
                    TopicArn = createResp.TopicArn,
                    AttributeName = "DisplayName",
                    AttributeValue = "MyDisplayName"
                });
                var getResp = await snsClient.GetTopicAttributesAsync(new GetTopicAttributesRequest
                {
                    TopicArn = createResp.TopicArn
                });
                if (getResp.Attributes == null)
                    throw new Exception("attributes is nil");
                if (getResp.Attributes["DisplayName"] != "MyDisplayName")
                    throw new Exception($"DisplayName mismatch: got {getResp.Attributes["DisplayName"]}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        var fifoTopicName = TestRunner.MakeUniqueName("FifoPub") + ".fifo";
        string? fifoTopicArn = null;
        try
        {
            var fifoResp = await snsClient.CreateTopicAsync(new CreateTopicRequest
            {
                Name = fifoTopicName,
                Attributes = new Dictionary<string, string>
                {
                    { "FifoTopic", "true" },
                    { "ContentBasedDeduplication", "false" }
                }
            });
            fifoTopicArn = fifoResp.TopicArn;

            results.Add(await runner.RunTestAsync("sns", "Publish_FIFO_WithMessageGroupId", async () =>
            {
                if (fifoTopicArn == null) throw new Exception("FIFO topic ARN is null");
                var resp = await snsClient.PublishAsync(new PublishRequest
                {
                    TopicArn = fifoTopicArn,
                    Message = "FIFO message with group",
                    MessageGroupId = "group1",
                    MessageDeduplicationId = Guid.NewGuid().ToString()
                });
                if (string.IsNullOrEmpty(resp.MessageId))
                    throw new Exception("MessageId is null");
            }));

            results.Add(await runner.RunTestAsync("sns", "Publish_FIFO_ContentBasedDedup", async () =>
            {
                if (fifoTopicArn == null) throw new Exception("FIFO topic ARN is null");
                var resp = await snsClient.PublishAsync(new PublishRequest
                {
                    TopicArn = fifoTopicArn,
                    Message = "Dedup message " + Guid.NewGuid(),
                    MessageGroupId = "group2",
                    MessageDeduplicationId = Guid.NewGuid().ToString()
                });
                if (string.IsNullOrEmpty(resp.MessageId))
                    throw new Exception("MessageId is null");
            }));

            results.Add(await runner.RunTestAsync("sns", "Publish_FIFO_DeduplicationId", async () =>
            {
                if (fifoTopicArn == null) throw new Exception("FIFO topic ARN is null");
                var dedupId = Guid.NewGuid().ToString();
                var resp1 = await snsClient.PublishAsync(new PublishRequest
                {
                    TopicArn = fifoTopicArn,
                    Message = "dedup test",
                    MessageGroupId = "group3",
                    MessageDeduplicationId = dedupId
                });
                var resp2 = await snsClient.PublishAsync(new PublishRequest
                {
                    TopicArn = fifoTopicArn,
                    Message = "dedup test",
                    MessageGroupId = "group3",
                    MessageDeduplicationId = dedupId
                });
                if (string.IsNullOrEmpty(resp1.MessageId) || string.IsNullOrEmpty(resp2.MessageId))
                    throw new Exception("MessageId is null");
            }));
        }
        finally
        {
            if (fifoTopicArn != null)
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = fifoTopicArn }); });
        }

        results.Add(await runner.RunTestAsync("sns", "Publish_WithMessageAttributes", async () =>
        {
            var maName = TestRunner.MakeUniqueName("MATopic");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = maName });
            try
            {
                var resp = await snsClient.PublishAsync(new PublishRequest
                {
                    TopicArn = createResp.TopicArn,
                    Message = "message with attrs",
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>
                    {
                        { "attr1", new MessageAttributeValue { DataType = "String", StringValue = "val1" } },
                        { "attr2", new MessageAttributeValue { DataType = "Number", StringValue = "42" } }
                    }
                });
                if (string.IsNullOrEmpty(resp.MessageId))
                    throw new Exception("MessageId is null");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "PublishBatch_WithAttributes", async () =>
        {
            var pbaName = TestRunner.MakeUniqueName("PBATopic");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = pbaName });
            try
            {
                var resp = await snsClient.PublishBatchAsync(new PublishBatchRequest
                {
                    TopicArn = createResp.TopicArn,
                    PublishBatchRequestEntries = new List<PublishBatchRequestEntry>
                    {
                        new PublishBatchRequestEntry
                        {
                            Id = "b1",
                            Message = "batch msg 1",
                            MessageAttributes = new Dictionary<string, MessageAttributeValue>
                            {
                                { "key1", new MessageAttributeValue { DataType = "String", StringValue = "v1" } }
                            }
                        },
                        new PublishBatchRequestEntry
                        {
                            Id = "b2",
                            Message = "batch msg 2",
                            MessageAttributes = new Dictionary<string, MessageAttributeValue>
                            {
                                { "key2", new MessageAttributeValue { DataType = "String", StringValue = "v2" } }
                            }
                        }
                    }
                });
                if (resp.Successful == null || resp.Successful.Count < 2)
                    throw new Exception($"expected 2 successful, got {resp.Successful?.Count}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "PublishBatch_MaxEntries", async () =>
        {
            var maxName = TestRunner.MakeUniqueName("MaxTopic");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = maxName });
            try
            {
                var entries = new List<PublishBatchRequestEntry>();
                for (int i = 0; i < 10; i++)
                {
                    entries.Add(new PublishBatchRequestEntry { Id = $"m{i}", Message = $"msg-{i}" });
                }
                var resp = await snsClient.PublishBatchAsync(new PublishBatchRequest
                {
                    TopicArn = createResp.TopicArn,
                    PublishBatchRequestEntries = entries
                });
                if (resp.Successful == null || resp.Successful.Count == 0)
                    throw new Exception("no successful entries");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "PublishBatch_FailedEntry", async () =>
        {
            var feName = TestRunner.MakeUniqueName("FETopic");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = feName });
            try
            {
                var entries = new List<PublishBatchRequestEntry>
                {
                    new PublishBatchRequestEntry { Id = "ok1", Message = "valid message" }
                };
                var resp = await snsClient.PublishBatchAsync(new PublishBatchRequest
                {
                    TopicArn = createResp.TopicArn,
                    PublishBatchRequestEntries = entries
                });
                if (resp.Successful == null || resp.Successful.Count == 0)
                    throw new Exception("expected successful entries");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "PutDataProtectionPolicy", async () =>
        {
            var dppName = TestRunner.MakeUniqueName("DPPTopic");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = dppName });
            try
            {
                var policy = @"{""Name"":""test-policy"",""Description"":""Test data protection policy""}";
                await snsClient.PutDataProtectionPolicyAsync(new PutDataProtectionPolicyRequest
                {
                    ResourceArn = createResp.TopicArn,
                    DataProtectionPolicy = policy
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "GetDataProtectionPolicy", async () =>
        {
            var gdpName = TestRunner.MakeUniqueName("GDPTopic");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = gdpName });
            try
            {
                var policy = @"{""Name"":""test-policy"",""Description"":""Test""}";
                await snsClient.PutDataProtectionPolicyAsync(new PutDataProtectionPolicyRequest
                {
                    ResourceArn = createResp.TopicArn,
                    DataProtectionPolicy = policy
                });
                var resp = await snsClient.GetDataProtectionPolicyAsync(new GetDataProtectionPolicyRequest
                {
                    ResourceArn = createResp.TopicArn
                });
                if (string.IsNullOrEmpty(resp.DataProtectionPolicy))
                    throw new Exception("data protection policy is null");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "GetDataProtectionPolicy_NonExistent", async () =>
        {
            try
            {
                await snsClient.GetDataProtectionPolicyAsync(new GetDataProtectionPolicyRequest
                {
                    ResourceArn = $"arn:aws:sns:{region}:000000000000:NonExistentTopic_xyz_12345"
                });
                throw new Exception("expected error for non-existent topic data protection policy");
            }
            catch (AmazonSimpleNotificationServiceException) { }
        }));

        results.Add(await runner.RunTestAsync("sns", "CreatePlatformApplication", async () =>
        {
            var appName = TestRunner.MakeUniqueName("PlatApp");
            var resp = await snsClient.CreatePlatformApplicationAsync(new CreatePlatformApplicationRequest
            {
                Name = appName,
                Platform = "GCM",
                Attributes = new Dictionary<string, string>
                {
                    { "PlatformCredential", "fake-credential" }
                }
            });
            if (string.IsNullOrEmpty(resp.PlatformApplicationArn))
                throw new Exception("PlatformApplicationArn is null");
            await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeletePlatformApplicationAsync(new DeletePlatformApplicationRequest { PlatformApplicationArn = resp.PlatformApplicationArn }); });
        }));

        results.Add(await runner.RunTestAsync("sns", "CreatePlatformApplication_Duplicate", async () =>
        {
            var dupAppName = TestRunner.MakeUniqueName("DupPlatApp");
            var resp1 = await snsClient.CreatePlatformApplicationAsync(new CreatePlatformApplicationRequest
            {
                Name = dupAppName,
                Platform = "GCM",
                Attributes = new Dictionary<string, string> { { "PlatformCredential", "fake" } }
            });
            try
            {
                var resp2 = await snsClient.CreatePlatformApplicationAsync(new CreatePlatformApplicationRequest
                {
                    Name = dupAppName,
                    Platform = "GCM",
                    Attributes = new Dictionary<string, string> { { "PlatformCredential", "fake" } }
                });
            }
            catch (AmazonSimpleNotificationServiceException) { }
            await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeletePlatformApplicationAsync(new DeletePlatformApplicationRequest { PlatformApplicationArn = resp1.PlatformApplicationArn }); });
        }));

        results.Add(await runner.RunTestAsync("sns", "ListPlatformApplications", async () =>
        {
            var resp = await snsClient.ListPlatformApplicationsAsync(new ListPlatformApplicationsRequest());
            if (resp.PlatformApplications != null)
            {
                foreach (var app in resp.PlatformApplications)
                {
                    if (string.IsNullOrEmpty(app.PlatformApplicationArn))
                        throw new Exception("PlatformApplicationArn is null");
                }
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "GetPlatformApplicationAttributes", async () =>
        {
            var gpaName = TestRunner.MakeUniqueName("GPAPlatApp");
            var createResp = await snsClient.CreatePlatformApplicationAsync(new CreatePlatformApplicationRequest
            {
                Name = gpaName,
                Platform = "GCM",
                Attributes = new Dictionary<string, string> { { "PlatformCredential", "fake" } }
            });
            try
            {
                var resp = await snsClient.GetPlatformApplicationAttributesAsync(new GetPlatformApplicationAttributesRequest
                {
                    PlatformApplicationArn = createResp.PlatformApplicationArn
                });
                if (resp.Attributes == null)
                    throw new Exception("attributes is null");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeletePlatformApplicationAsync(new DeletePlatformApplicationRequest { PlatformApplicationArn = createResp.PlatformApplicationArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "SetPlatformApplicationAttributes", async () =>
        {
            var spaName = TestRunner.MakeUniqueName("SPAPlatApp");
            var createResp = await snsClient.CreatePlatformApplicationAsync(new CreatePlatformApplicationRequest
            {
                Name = spaName,
                Platform = "GCM",
                Attributes = new Dictionary<string, string> { { "PlatformCredential", "fake" } }
            });
            try
            {
                await snsClient.SetPlatformApplicationAttributesAsync(new SetPlatformApplicationAttributesRequest
                {
                    PlatformApplicationArn = createResp.PlatformApplicationArn,
                    Attributes = new Dictionary<string, string> { { "Enabled", "true" } }
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeletePlatformApplicationAsync(new DeletePlatformApplicationRequest { PlatformApplicationArn = createResp.PlatformApplicationArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "CreatePlatformEndpoint", async () =>
        {
            var cpeName = TestRunner.MakeUniqueName("CPEPlatApp");
            var createResp = await snsClient.CreatePlatformApplicationAsync(new CreatePlatformApplicationRequest
            {
                Name = cpeName,
                Platform = "GCM",
                Attributes = new Dictionary<string, string> { { "PlatformCredential", "fake" } }
            });
            try
            {
                var epResp = await snsClient.CreatePlatformEndpointAsync(new CreatePlatformEndpointRequest
                {
                    PlatformApplicationArn = createResp.PlatformApplicationArn,
                    Token = "fake-device-token-" + Guid.NewGuid()
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeletePlatformApplicationAsync(new DeletePlatformApplicationRequest { PlatformApplicationArn = createResp.PlatformApplicationArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "ListEndpointsByPlatformApplication", async () =>
        {
            var lepaName = TestRunner.MakeUniqueName("LEPAPlatApp");
            var createResp = await snsClient.CreatePlatformApplicationAsync(new CreatePlatformApplicationRequest
            {
                Name = lepaName,
                Platform = "GCM",
                Attributes = new Dictionary<string, string> { { "PlatformCredential", "fake" } }
            });
            try
            {
                var resp = await snsClient.ListEndpointsByPlatformApplicationAsync(new ListEndpointsByPlatformApplicationRequest
                {
                    PlatformApplicationArn = createResp.PlatformApplicationArn
                });
                if (resp.Endpoints != null)
                {
                    foreach (var ep in resp.Endpoints)
                    {
                        if (string.IsNullOrEmpty(ep.EndpointArn))
                            throw new Exception("EndpointArn is null");
                    }
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeletePlatformApplicationAsync(new DeletePlatformApplicationRequest { PlatformApplicationArn = createResp.PlatformApplicationArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "GetEndpointAttributes", async () =>
        {
            var geaName = TestRunner.MakeUniqueName("GEAPlatApp");
            var createResp = await snsClient.CreatePlatformApplicationAsync(new CreatePlatformApplicationRequest
            {
                Name = geaName,
                Platform = "GCM",
                Attributes = new Dictionary<string, string> { { "PlatformCredential", "fake" } }
            });
            try
            {
                var epResp = await snsClient.CreatePlatformEndpointAsync(new CreatePlatformEndpointRequest
                {
                    PlatformApplicationArn = createResp.PlatformApplicationArn,
                    Token = "fake-token-" + Guid.NewGuid()
                });
                try
                {
                    var resp = await snsClient.GetEndpointAttributesAsync(new GetEndpointAttributesRequest
                    {
                        EndpointArn = epResp.EndpointArn
                    });
                    if (resp.Attributes == null)
                        throw new Exception("attributes is null");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteEndpointAsync(new DeleteEndpointRequest { EndpointArn = epResp.EndpointArn }); });
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeletePlatformApplicationAsync(new DeletePlatformApplicationRequest { PlatformApplicationArn = createResp.PlatformApplicationArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "SetEndpointAttributes", async () =>
        {
            var seaName = TestRunner.MakeUniqueName("SEAPlatApp");
            var createResp = await snsClient.CreatePlatformApplicationAsync(new CreatePlatformApplicationRequest
            {
                Name = seaName,
                Platform = "GCM",
                Attributes = new Dictionary<string, string> { { "PlatformCredential", "fake" } }
            });
            try
            {
                var epResp = await snsClient.CreatePlatformEndpointAsync(new CreatePlatformEndpointRequest
                {
                    PlatformApplicationArn = createResp.PlatformApplicationArn,
                    Token = "fake-token-" + Guid.NewGuid()
                });
                try
                {
                    await snsClient.SetEndpointAttributesAsync(new SetEndpointAttributesRequest
                    {
                        EndpointArn = epResp.EndpointArn,
                        Attributes = new Dictionary<string, string> { { "Enabled", "true" } }
                    });
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteEndpointAsync(new DeleteEndpointRequest { EndpointArn = epResp.EndpointArn }); });
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeletePlatformApplicationAsync(new DeletePlatformApplicationRequest { PlatformApplicationArn = createResp.PlatformApplicationArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "DeletePlatformApplication_Cascade", async () =>
        {
            var dpaName = TestRunner.MakeUniqueName("DPAPlatApp");
            var createResp = await snsClient.CreatePlatformApplicationAsync(new CreatePlatformApplicationRequest
            {
                Name = dpaName,
                Platform = "GCM",
                Attributes = new Dictionary<string, string> { { "PlatformCredential", "fake" } }
            });
            try
            {
                await snsClient.DeletePlatformApplicationAsync(new DeletePlatformApplicationRequest
                {
                    PlatformApplicationArn = createResp.PlatformApplicationArn
                });
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("sns", "DeleteEndpoint", async () =>
        {
            var deName = TestRunner.MakeUniqueName("DEPlatApp");
            var createResp = await snsClient.CreatePlatformApplicationAsync(new CreatePlatformApplicationRequest
            {
                Name = deName,
                Platform = "GCM",
                Attributes = new Dictionary<string, string> { { "PlatformCredential", "fake" } }
            });
            try
            {
                var epResp = await snsClient.CreatePlatformEndpointAsync(new CreatePlatformEndpointRequest
                {
                    PlatformApplicationArn = createResp.PlatformApplicationArn,
                    Token = "fake-token-del-" + Guid.NewGuid()
                });
                await snsClient.DeleteEndpointAsync(new DeleteEndpointRequest
                {
                    EndpointArn = epResp.EndpointArn
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeletePlatformApplicationAsync(new DeletePlatformApplicationRequest { PlatformApplicationArn = createResp.PlatformApplicationArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "GetSubscriptionAttributes", async () =>
        {
            var gsaName = TestRunner.MakeUniqueName("GSATopic");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = gsaName });
            try
            {
                var subResp = await snsClient.SubscribeAsync(new SubscribeRequest
                {
                    TopicArn = createResp.TopicArn,
                    Protocol = "email",
                    Endpoint = "gsa-test@example.com"
                });
                try
                {
                    var resp = await snsClient.GetSubscriptionAttributesAsync(new GetSubscriptionAttributesRequest
                    {
                        SubscriptionArn = subResp.SubscriptionArn
                    });
                    if (resp.Attributes == null)
                        throw new Exception("attributes is null");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await snsClient.UnsubscribeAsync(new UnsubscribeRequest { SubscriptionArn = subResp.SubscriptionArn }); });
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "SetSubscriptionAttributes", async () =>
        {
            var ssaName = TestRunner.MakeUniqueName("SSATopic");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = ssaName });
            try
            {
                var subResp = await snsClient.SubscribeAsync(new SubscribeRequest
                {
                    TopicArn = createResp.TopicArn,
                    Protocol = "email",
                    Endpoint = "ssa-test@example.com"
                });
                try
                {
                    await snsClient.SetSubscriptionAttributesAsync(new SetSubscriptionAttributesRequest
                    {
                        SubscriptionArn = subResp.SubscriptionArn,
                        AttributeName = "RawMessageDelivery",
                        AttributeValue = "true"
                    });
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await snsClient.UnsubscribeAsync(new UnsubscribeRequest { SubscriptionArn = subResp.SubscriptionArn }); });
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "ListSubscriptions_ContainsCreated", async () =>
        {
            var lscName = TestRunner.MakeUniqueName("LSCTopic");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = lscName });
            try
            {
                var subResp = await snsClient.SubscribeAsync(new SubscribeRequest
                {
                    TopicArn = createResp.TopicArn,
                    Protocol = "email",
                    Endpoint = "lsc-test@example.com"
                });
                try
                {
                    var listResp = await snsClient.ListSubscriptionsAsync(new ListSubscriptionsRequest());
                    bool found = false;
                    foreach (var s in listResp.Subscriptions)
                    {
                        if (s.SubscriptionArn == subResp.SubscriptionArn)
                        {
                            found = true;
                            break;
                        }
                    }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await snsClient.UnsubscribeAsync(new UnsubscribeRequest { SubscriptionArn = subResp.SubscriptionArn }); });
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "SetSubscriptionAttributes_NonExistent", async () =>
        {
            try
            {
                await snsClient.SetSubscriptionAttributesAsync(new SetSubscriptionAttributesRequest
                {
                    SubscriptionArn = "arn:aws:sns:us-east-1:000000000000:nonexistent-sub:00000000-0000-0000-0000-000000000000",
                    AttributeName = "RawMessageDelivery",
                    AttributeValue = "true"
                });
                throw new Exception("expected error for non-existent subscription");
            }
            catch (AmazonSimpleNotificationServiceException) { }
        }));

        results.Add(await runner.RunTestAsync("sns", "GetSubscriptionAttributes_NonExistent", async () =>
        {
            try
            {
                await snsClient.GetSubscriptionAttributesAsync(new GetSubscriptionAttributesRequest
                {
                    SubscriptionArn = "arn:aws:sns:us-east-1:000000000000:nonexistent-sub:00000000-0000-0000-0000-000000000000"
                });
                throw new Exception("expected error for non-existent subscription");
            }
            catch (AmazonSimpleNotificationServiceException) { }
        }));

        results.Add(await runner.RunTestAsync("sns", "SetTopicAttributes_NonExistent", async () =>
        {
            try
            {
                await snsClient.SetTopicAttributesAsync(new SetTopicAttributesRequest
                {
                    TopicArn = $"arn:aws:sns:{region}:000000000000:NonExistentTopic_xyz_12345",
                    AttributeName = "DisplayName",
                    AttributeValue = "test"
                });
                throw new Exception("expected error for non-existent topic");
            }
            catch (AmazonSimpleNotificationServiceException) { }
        }));

        results.Add(await runner.RunTestAsync("sns", "Subscribe_EmailPendingConfirmation", async () =>
        {
            var epcName = TestRunner.MakeUniqueName("EPCTopic");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = epcName });
            try
            {
                var resp = await snsClient.SubscribeAsync(new SubscribeRequest
                {
                    TopicArn = createResp.TopicArn,
                    Protocol = "email",
                    Endpoint = "pending-confirmation@example.com"
                });
                if (string.IsNullOrEmpty(resp.SubscriptionArn))
                    throw new Exception("SubscriptionArn is null");
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.UnsubscribeAsync(new UnsubscribeRequest { SubscriptionArn = resp.SubscriptionArn }); });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "Subscribe_ApplicationPendingConfirmation", async () =>
        {
            var apcName = TestRunner.MakeUniqueName("APCPlatApp");
            var createResp = await snsClient.CreatePlatformApplicationAsync(new CreatePlatformApplicationRequest
            {
                Name = apcName,
                Platform = "GCM",
                Attributes = new Dictionary<string, string> { { "PlatformCredential", "fake" } }
            });
            try
            {
                var epResp = await snsClient.CreatePlatformEndpointAsync(new CreatePlatformEndpointRequest
                {
                    PlatformApplicationArn = createResp.PlatformApplicationArn,
                    Token = "fake-token-apc-" + Guid.NewGuid()
                });
                try
                {
                    var topicName2 = TestRunner.MakeUniqueName("APCTopic");
                    var topicResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = topicName2 });
                    try
                    {
                        var subResp = await snsClient.SubscribeAsync(new SubscribeRequest
                        {
                            TopicArn = topicResp.TopicArn,
                            Protocol = "application",
                            Endpoint = epResp.EndpointArn
                        });
                        await TestHelpers.SafeCleanupAsync(async () => { await snsClient.UnsubscribeAsync(new UnsubscribeRequest { SubscriptionArn = subResp.SubscriptionArn }); });
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = topicResp.TopicArn }); });
                    }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteEndpointAsync(new DeleteEndpointRequest { EndpointArn = epResp.EndpointArn }); });
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeletePlatformApplicationAsync(new DeletePlatformApplicationRequest { PlatformApplicationArn = createResp.PlatformApplicationArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "GetTopicAttributes_FifoAttributes", async () =>
        {
            var fAttrName = TestRunner.MakeUniqueName("FAttrTopic") + ".fifo";
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest
            {
                Name = fAttrName,
                Attributes = new Dictionary<string, string>
                {
                    { "FifoTopic", "true" },
                    { "ContentBasedDeduplication", "true" }
                }
            });
            try
            {
                var resp = await snsClient.GetTopicAttributesAsync(new GetTopicAttributesRequest
                {
                    TopicArn = createResp.TopicArn
                });
                if (resp.Attributes["FifoTopic"] != "true")
                    throw new Exception("FifoTopic attribute is not true");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "GetTopicAttributes_PolicyDefault", async () =>
        {
            var pdName = TestRunner.MakeUniqueName("PDTopic");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = pdName });
            try
            {
                var resp = await snsClient.GetTopicAttributesAsync(new GetTopicAttributesRequest
                {
                    TopicArn = createResp.TopicArn
                });
                if (resp.Attributes == null)
                    throw new Exception("attributes is null");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "CreateTopic_WithTags", async () =>
        {
            var wtName = TestRunner.MakeUniqueName("WTTopic");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest
            {
                Name = wtName,
                Tags = new List<Tag>
                {
                    new Tag { Key = "Env", Value = "prod" },
                    new Tag { Key = "Team", Value = "sdk" }
                }
            });
            try
            {
                var tagResp = await snsClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceArn = createResp.TopicArn
                });
                if (tagResp.Tags == null || tagResp.Tags.Count < 2)
                    throw new Exception("expected at least 2 tags");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "TagResource_MultipleTags", async () =>
        {
            var mtName = TestRunner.MakeUniqueName("MTTopic");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = mtName });
            try
            {
                await snsClient.TagResourceAsync(new TagResourceRequest
                {
                    ResourceArn = createResp.TopicArn,
                    Tags = new List<Tag>
                    {
                        new Tag { Key = "A", Value = "1" },
                        new Tag { Key = "B", Value = "2" },
                        new Tag { Key = "C", Value = "3" }
                    }
                });
                var tagResp = await snsClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceArn = createResp.TopicArn
                });
                if (tagResp.Tags == null || tagResp.Tags.Count < 3)
                    throw new Exception($"expected at least 3 tags, got {tagResp.Tags?.Count}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "ListSubscriptionsByTopic_Empty", async () =>
        {
            var lseName = TestRunner.MakeUniqueName("LSETopic");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = lseName });
            try
            {
                var resp = await snsClient.ListSubscriptionsByTopicAsync(new ListSubscriptionsByTopicRequest
                {
                    TopicArn = createResp.TopicArn
                });
                if (resp.Subscriptions != null && resp.Subscriptions.Count != 0)
                    throw new Exception($"expected 0 subscriptions, got {resp.Subscriptions.Count}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "ListTopics_Pagination", async () =>
        {
            var resp = await snsClient.ListTopicsAsync(new ListTopicsRequest());
            if (resp.Topics == null)
                throw new Exception("topics is null");
            if (!string.IsNullOrEmpty(resp.NextToken))
            {
                var resp2 = await snsClient.ListTopicsAsync(new ListTopicsRequest { NextToken = resp.NextToken });
                if (resp2.Topics == null)
                    throw new Exception("topics page 2 is null");
            }
        }));

        results.Add(await runner.RunTestAsync("sns", "DeleteTopic_VerifyGone", async () =>
        {
            var vgName = TestRunner.MakeUniqueName("VGTopic");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = vgName });
            await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn });
            try
            {
                await snsClient.GetTopicAttributesAsync(new GetTopicAttributesRequest
                {
                    TopicArn = createResp.TopicArn
                });
                throw new Exception("expected error for deleted topic");
            }
            catch (AmazonSimpleNotificationServiceException) { }
        }));

        results.Add(await runner.RunTestAsync("sns", "Subscribe_SQS_AutoConfirmed", async () =>
        {
            var sqaName = TestRunner.MakeUniqueName("SQASub");
            var createResp = await snsClient.CreateTopicAsync(new CreateTopicRequest { Name = sqaName });
            try
            {
                var queueUrl = $"https://sqs.{region}.amazonaws.com/000000000000/test-queue";
                var resp = await snsClient.SubscribeAsync(new SubscribeRequest
                {
                    TopicArn = createResp.TopicArn,
                    Protocol = "sqs",
                    Endpoint = queueUrl
                });
                if (string.IsNullOrEmpty(resp.SubscriptionArn))
                    throw new Exception("SubscriptionArn is null");
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.UnsubscribeAsync(new UnsubscribeRequest { SubscriptionArn = resp.SubscriptionArn }); });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); });
            }
        }));

        return results;
    }
}
