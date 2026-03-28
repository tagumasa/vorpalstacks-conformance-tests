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
            try { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = resp.TopicArn }); } catch { }
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
                try { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = resp1.TopicArn }); } catch { }
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
                try { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); } catch { }
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
                try { await snsClient.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = createResp.TopicArn }); } catch { }
            }
        }));

        return results;
    }
}
