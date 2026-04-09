using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class SQSServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonSQSClient sqsClient,
        string region)
    {
        var results = new List<TestResult>();
        var queueName = TestRunner.MakeUniqueName("CSQueue");
        var queueUrl = "";

        try
        {
            results.Add(await runner.RunTestAsync("sqs", "CreateQueue", async () =>
            {
                var resp = await sqsClient.CreateQueueAsync(new CreateQueueRequest
                {
                    QueueName = queueName,
                    Attributes = new Dictionary<string, string>
                    {
                        { "DelaySeconds", "0" },
                        { "MaximumMessageSize", "262144" },
                        { "VisibilityTimeout", "30" },
                        { "ReceiveMessageWaitTimeSeconds", "0" }
                    }
                });
                if (string.IsNullOrEmpty(resp.QueueUrl))
                    throw new Exception("QueueUrl is null");
                queueUrl = resp.QueueUrl;
            }));

            results.Add(await runner.RunTestAsync("sqs", "GetQueueUrl", async () =>
            {
                var resp = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
                {
                    QueueName = queueName
                });
                if (string.IsNullOrEmpty(resp.QueueUrl))
                    throw new Exception("QueueUrl is null");
            }));

            results.Add(await runner.RunTestAsync("sqs", "ListQueues", async () =>
            {
                var resp = await sqsClient.ListQueuesAsync(new ListQueuesRequest());
                if (resp.QueueUrls == null)
                    throw new Exception("QueueUrls is null");
            }));

            results.Add(await runner.RunTestAsync("sqs", "SendMessage", async () =>
            {
                var resp = await sqsClient.SendMessageAsync(new SendMessageRequest
                {
                    QueueUrl = queueUrl,
                    MessageBody = "{\"test\":\"hello\",\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}"
                });
                if (string.IsNullOrEmpty(resp.MessageId))
                    throw new Exception("MessageId is null");
            }));

            results.Add(await runner.RunTestAsync("sqs", "ReceiveMessage", async () =>
            {
                var resp = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 1
                });
                if (resp.Messages == null)
                    throw new Exception("Messages is null");
                if (resp.Messages.Count == 0)
                    throw new Exception("No messages received");
            }));

            results.Add(await runner.RunTestAsync("sqs", "DeleteMessage", async () =>
            {
                var receiveResp = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 1
                });
                if (receiveResp.Messages.Count > 0)
                {
                    var receiptHandle = receiveResp.Messages[0].ReceiptHandle;
                    if (string.IsNullOrEmpty(receiptHandle))
                        throw new Exception("ReceiptHandle is null");
                    await sqsClient.DeleteMessageAsync(new DeleteMessageRequest
                    {
                        QueueUrl = queueUrl,
                        ReceiptHandle = receiptHandle
                    });
                }
            }));

            results.Add(await runner.RunTestAsync("sqs", "MultiByteMessage", async () =>
            {
                var bodies = new[] { "日本語テストメッセージ", "简体中文测试消息", "繁體中文測試訊息" };
                foreach (var body in bodies)
                {
                    await sqsClient.SendMessageAsync(new SendMessageRequest
                    {
                        QueueUrl = queueUrl,
                        MessageBody = body
                    });
                }
                var received = new HashSet<string>();
                for (int i = 0; i < 3; i++)
                {
                    var resp = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                    {
                        QueueUrl = queueUrl,
                        MaxNumberOfMessages = 1,
                        WaitTimeSeconds = 2
                    });
                    foreach (var msg in resp.Messages ?? new List<Message>())
                    {
                        received.Add(msg.Body);
                        await sqsClient.DeleteMessageAsync(new DeleteMessageRequest
                        {
                            QueueUrl = queueUrl,
                            ReceiptHandle = msg.ReceiptHandle
                        });
                    }
                }
                if (!received.Contains("日本語テストメッセージ"))
                    throw new Exception("Japanese message not received");
                if (!received.Contains("简体中文测试消息"))
                    throw new Exception("Simplified Chinese message not received");
                if (!received.Contains("繁體中文測試訊息"))
                    throw new Exception("Traditional Chinese message not received");
            }));

            results.Add(await runner.RunTestAsync("sqs", "DeleteQueue", async () =>
            {
                await sqsClient.DeleteQueueAsync(new DeleteQueueRequest
                {
                    QueueUrl = queueUrl
                });
                queueUrl = "";
            }));
        }
        finally
        {
            if (!string.IsNullOrEmpty(queueUrl))
            {
                try
                {
                    await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = queueUrl });
                }
                catch { }
            }
        }

        results.Add(await runner.RunTestAsync("sqs", "GetQueueAttributes", async () =>
        {
            var attrName = TestRunner.MakeUniqueName("AttrQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = attrName });
            try
            {
                var resp = await sqsClient.GetQueueAttributesAsync(new GetQueueAttributesRequest
                {
                    QueueUrl = createResp.QueueUrl
                });
                if (resp.Attributes == null)
                    throw new Exception("Attributes is null");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "SetQueueAttributes", async () =>
        {
            var setName = TestRunner.MakeUniqueName("SetAttrQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = setName });
            try
            {
                await sqsClient.SetQueueAttributesAsync(new SetQueueAttributesRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    Attributes = new Dictionary<string, string>
                    {
                        { "VisibilityTimeout", "30" }
                    }
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "TagQueue", async () =>
        {
            var taggerName = TestRunner.MakeUniqueName("TagQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = taggerName });
            try
            {
                await sqsClient.TagQueueAsync(new TagQueueRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    Tags = new Dictionary<string, string>
                    {
                        { "Environment", "Test" }
                    }
                });
                var listResp = await sqsClient.ListQueueTagsAsync(new ListQueueTagsRequest { QueueUrl = createResp.QueueUrl });
                if (listResp.Tags == null || listResp.Tags.Count == 0)
                    throw new Exception("Tags is nil or empty (expected Environment=Test)");
                await sqsClient.UntagQueueAsync(new UntagQueueRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    TagKeys = new List<string> { "Environment" }
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "PurgeQueue", async () =>
        {
            var purgeName = TestRunner.MakeUniqueName("PurgeQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = purgeName });
            try
            {
                await sqsClient.PurgeQueueAsync(new PurgeQueueRequest
                {
                    QueueUrl = createResp.QueueUrl
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "ChangeMessageVisibility", async () =>
        {
            var cmvName = TestRunner.MakeUniqueName("CMVQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = cmvName });
            try
            {
                await sqsClient.SendMessageAsync(new SendMessageRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    MessageBody = "Test message for visibility"
                });
                var recvResp = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = createResp.QueueUrl
                });
                if (recvResp.Messages.Count == 0)
                    throw new Exception("no messages received");
                var receiptHandle = recvResp.Messages[0].ReceiptHandle;
                if (string.IsNullOrEmpty(receiptHandle))
                    throw new Exception("receipt handle is empty");
                await sqsClient.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    ReceiptHandle = receiptHandle,
                    VisibilityTimeout = 60
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "GetQueueUrl_NonExistent", async () =>
        {
            try
            {
                await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
                {
                    QueueName = "NonExistentQueue_xyz_12345"
                });
                throw new Exception("Expected error but got none");
            }
            catch (AmazonSQSException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "SendMessage_InvalidQueue", async () =>
        {
            try
            {
                await sqsClient.SendMessageAsync(new SendMessageRequest
                {
                    QueueUrl = "https://invalid-queue-url-xyz12345.sqs.region.amazonaws.com/000000000000/NonExistent",
                    MessageBody = "test"
                });
                throw new Exception("Expected error but got none");
            }
            catch (AmazonSQSException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "ReceiveMessage_InvalidQueue", async () =>
        {
            try
            {
                await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = "https://invalid-queue-url-xyz12345.sqs.region.amazonaws.com/000000000000/NonExistent",
                    MaxNumberOfMessages = 1
                });
                throw new Exception("Expected error but got none");
            }
            catch (AmazonSQSException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "SendMessage_ReceiveRoundtrip", async () =>
        {
            var rtName = TestRunner.MakeUniqueName("RTQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = rtName });
            try
            {
                var urlResp = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest { QueueName = rtName });
                var testBody = "roundtrip-test-message-12345";
                var sendResp = await sqsClient.SendMessageAsync(new SendMessageRequest
                {
                    QueueUrl = urlResp.QueueUrl,
                    MessageBody = testBody
                });
                if (string.IsNullOrEmpty(sendResp.MessageId))
                    throw new Exception("message ID is nil or empty");
                var recvResp = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = urlResp.QueueUrl
                });
                if (recvResp.Messages.Count == 0)
                    throw new Exception("no messages received");
                if (recvResp.Messages[0].Body != testBody)
                    throw new Exception($"message body mismatch: got {recvResp.Messages[0].Body}, want {testBody}");
            }
            finally
            {
                try
                {
                    var urlResp = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest { QueueName = rtName });
                    await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = urlResp.QueueUrl });
                }
                catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "ListQueues_ContainsCreated", async () =>
        {
            var lqName = TestRunner.MakeUniqueName("LQTest");
            await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = lqName });
            try
            {
                var resp = await sqsClient.ListQueuesAsync(new ListQueuesRequest());
                if (resp.QueueUrls == null)
                    throw new Exception("queue URLs is nil");
                if (resp.QueueUrls.Count == 0)
                    throw new Exception("expected at least one queue URL");
            }
            finally
            {
                try
                {
                    var urlResp = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest { QueueName = lqName });
                    await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = urlResp.QueueUrl });
                }
                catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "ChangeMessageVisibility_NonExistent", async () =>
        {
            try
            {
                await sqsClient.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest
                {
                    QueueUrl = "https://queue.amazonaws.com/000000000000/nonexistent",
                    ReceiptHandle = "fake-receipt-handle",
                    VisibilityTimeout = 30
                });
                throw new Exception("expected error for non-existent receipt handle");
            }
            catch (AmazonSQSException) { }
        }));

        results.Add(await runner.RunTestAsync("sqs", "CreateQueue_DuplicateName", async () =>
        {
            var dupName = TestRunner.MakeUniqueName("DupQueue");
            await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = dupName });
            try
            {
                await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = dupName });
            }
            catch (AmazonSQSException ex)
            {
                throw new Exception($"duplicate queue name should be idempotent, got: {ex.Message}", ex);
            }
        }));

        var batchQueueName = TestRunner.MakeUniqueName("BatchQueue");

        results.Add(await runner.RunTestAsync("sqs", "SendMessageBatch", async () =>
        {
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = batchQueueName });
            try
            {
                var urlResp = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest { QueueName = batchQueueName });
                var batchResp = await sqsClient.SendMessageBatchAsync(new SendMessageBatchRequest
                {
                    QueueUrl = urlResp.QueueUrl,
                    Entries = new List<SendMessageBatchRequestEntry>
                    {
                        new SendMessageBatchRequestEntry { Id = "msg1", MessageBody = "Batch message 1" },
                        new SendMessageBatchRequestEntry { Id = "msg2", MessageBody = "Batch message 2" }
                    }
                });
                if (batchResp.Successful == null || batchResp.Successful.Count == 0)
                    throw new Exception("SendMessageBatch returned empty Successful entries");
            }
            finally
            {
                try
                {
                    var urlResp = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest { QueueName = batchQueueName });
                    await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = urlResp.QueueUrl });
                }
                catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "DeleteMessageBatch", async () =>
        {
            await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = batchQueueName });
            try
            {
                var urlResp = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest { QueueName = batchQueueName });
                await sqsClient.SendMessageBatchAsync(new SendMessageBatchRequest
                {
                    QueueUrl = urlResp.QueueUrl,
                    Entries = new List<SendMessageBatchRequestEntry>
                    {
                        new SendMessageBatchRequestEntry { Id = "m1", MessageBody = "msg1" },
                        new SendMessageBatchRequestEntry { Id = "m2", MessageBody = "msg2" }
                    }
                });
                var recvResp = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = urlResp.QueueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 2
                });
                if (recvResp.Messages.Count == 0)
                    throw new Exception("no messages received for DeleteMessageBatch test");
                var entries = new List<DeleteMessageBatchRequestEntry>();
                for (int i = 0; i < recvResp.Messages.Count; i++)
                {
                    entries.Add(new DeleteMessageBatchRequestEntry
                    {
                        Id = $"del{i}",
                        ReceiptHandle = recvResp.Messages[i].ReceiptHandle
                    });
                }
                await sqsClient.DeleteMessageBatchAsync(new DeleteMessageBatchRequest
                {
                    QueueUrl = urlResp.QueueUrl,
                    Entries = entries
                });
            }
            finally
            {
                try
                {
                    var urlResp = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest { QueueName = batchQueueName });
                    await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = urlResp.QueueUrl });
                }
                catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "CreateQueue_Fifo", async () =>
        {
            var fifoName = TestRunner.MakeUniqueName("FifoQueue") + ".fifo";
            var resp = await sqsClient.CreateQueueAsync(new CreateQueueRequest
            {
                QueueName = fifoName,
                Attributes = new Dictionary<string, string>
                {
                    { "ContentBasedDeduplication", "true" },
                    { "FifoQueue", "true" }
                }
            });
            if (string.IsNullOrEmpty(resp.QueueUrl))
                throw new Exception("CreateQueue (FIFO) returned null QueueUrl");
            try
            {
                await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = resp.QueueUrl });
            }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("sqs", "GetQueueAttributes_SpecificAttributes", async () =>
        {
            var saName = TestRunner.MakeUniqueName("SAQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest
            {
                QueueName = saName,
                Attributes = new Dictionary<string, string>
                {
                    { "VisibilityTimeout", "45" },
                    { "DelaySeconds", "5" }
                }
            });
            try
            {
                var resp = await sqsClient.GetQueueAttributesAsync(new GetQueueAttributesRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    AttributeNames = new List<string> { "VisibilityTimeout", "DelaySeconds" }
                });
                if (resp.Attributes == null)
                    throw new Exception("attributes is null");
                if (resp.Attributes["VisibilityTimeout"] != "45")
                    throw new Exception($"expected VisibilityTimeout=45, got {resp.Attributes["VisibilityTimeout"]}");
                if (resp.Attributes["DelaySeconds"] != "5")
                    throw new Exception($"expected DelaySeconds=5, got {resp.Attributes["DelaySeconds"]}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "SendMessage_WithDelaySeconds", async () =>
        {
            var dsName = TestRunner.MakeUniqueName("DSQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = dsName });
            try
            {
                var resp = await sqsClient.SendMessageAsync(new SendMessageRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    MessageBody = "delayed message",
                    DelaySeconds = 2
                });
                if (string.IsNullOrEmpty(resp.MessageId))
                    throw new Exception("MessageId is null");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "SendMessage_WithMessageAttributes", async () =>
        {
            var maName = TestRunner.MakeUniqueName("MAQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = maName });
            try
            {
                var resp = await sqsClient.SendMessageAsync(new SendMessageRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    MessageBody = "message with attributes",
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>
                    {
                        { "Attr1", new MessageAttributeValue { DataType = "String", StringValue = "value1" } }
                    }
                });
                if (string.IsNullOrEmpty(resp.MessageId))
                    throw new Exception("MessageId is null");
                var recvResp = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    MessageAttributeNames = new List<string> { "All" }
                });
                if (recvResp.Messages.Count == 0)
                    throw new Exception("no messages received");
                if (!recvResp.Messages[0].MessageAttributes.ContainsKey("Attr1"))
                    throw new Exception("message attribute Attr1 not found");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "ReceiveMessage_MaxNumberOfMessages", async () =>
        {
            var mnName = TestRunner.MakeUniqueName("MNQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = mnName });
            try
            {
                for (int i = 0; i < 5; i++)
                {
                    await sqsClient.SendMessageAsync(new SendMessageRequest
                    {
                        QueueUrl = createResp.QueueUrl,
                        MessageBody = $"msg-{i}"
                    });
                }
                var resp = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    MaxNumberOfMessages = 3
                });
                if (resp.Messages == null)
                    throw new Exception("messages is null");
                if (resp.Messages.Count > 3)
                    throw new Exception($"expected at most 3 messages, got {resp.Messages.Count}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "ReceiveMessage_WaitTimeSeconds", async () =>
        {
            var wtName = TestRunner.MakeUniqueName("WTQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = wtName });
            try
            {
                var resp = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    MaxNumberOfMessages = 1,
                    WaitTimeSeconds = 2
                });
                if (resp.Messages == null)
                    throw new Exception("messages is null");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "ReceiveMessage_VisibilityTimeout", async () =>
        {
            var vtName = TestRunner.MakeUniqueName("VTQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = vtName });
            try
            {
                await sqsClient.SendMessageAsync(new SendMessageRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    MessageBody = "visibility test"
                });
                var resp = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    VisibilityTimeout = 5
                });
                if (resp.Messages == null)
                    throw new Exception("messages is null");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "ListQueues_WithPrefix", async () =>
        {
            var prefix = TestRunner.MakeUniqueName("LQPref");
            await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = prefix + "-1" });
            await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = prefix + "-2" });
            try
            {
                var resp = await sqsClient.ListQueuesAsync(new ListQueuesRequest { QueueNamePrefix = prefix });
                if (resp.QueueUrls == null)
                    throw new Exception("queue URLs is null");
                if (resp.QueueUrls.Count < 2)
                    throw new Exception($"expected at least 2 queues with prefix, got {resp.QueueUrls.Count}");
            }
            finally
            {
                try
                {
                    var urlResp1 = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest { QueueName = prefix + "-1" });
                    await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = urlResp1.QueueUrl });
                }
                catch { }
                try
                {
                    var urlResp2 = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest { QueueName = prefix + "-2" });
                    await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = urlResp2.QueueUrl });
                }
                catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "SetQueueAttributes_MultipleAttrs", async () =>
        {
            var msaName = TestRunner.MakeUniqueName("MSAQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = msaName });
            try
            {
                await sqsClient.SetQueueAttributesAsync(new SetQueueAttributesRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    Attributes = new Dictionary<string, string>
                    {
                        { "VisibilityTimeout", "120" },
                        { "MaximumMessageSize", "1024" },
                        { "DelaySeconds", "10" }
                    }
                });
                var resp = await sqsClient.GetQueueAttributesAsync(new GetQueueAttributesRequest
                {
                    QueueUrl = createResp.QueueUrl
                });
                if (resp.Attributes["VisibilityTimeout"] != "120")
                    throw new Exception($"expected VisibilityTimeout=120, got {resp.Attributes["VisibilityTimeout"]}");
                if (resp.Attributes["MaximumMessageSize"] != "1024")
                    throw new Exception($"expected MaximumMessageSize=1024, got {resp.Attributes["MaximumMessageSize"]}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "ListQueueTags", async () =>
        {
            var ltName = TestRunner.MakeUniqueName("LTQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = ltName });
            try
            {
                await sqsClient.TagQueueAsync(new TagQueueRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    Tags = new Dictionary<string, string>
                    {
                        { "Key1", "Value1" },
                        { "Key2", "Value2" }
                    }
                });
                var resp = await sqsClient.ListQueueTagsAsync(new ListQueueTagsRequest { QueueUrl = createResp.QueueUrl });
                if (resp.Tags == null)
                    throw new Exception("tags is null");
                if (resp.Tags.Count < 2)
                    throw new Exception($"expected at least 2 tags, got {resp.Tags.Count}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "UntagQueue", async () =>
        {
            var utName = TestRunner.MakeUniqueName("UTQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = utName });
            try
            {
                await sqsClient.TagQueueAsync(new TagQueueRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    Tags = new Dictionary<string, string> { { "RemoveMe", "yes" } }
                });
                await sqsClient.UntagQueueAsync(new UntagQueueRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    TagKeys = new List<string> { "RemoveMe" }
                });
                var resp = await sqsClient.ListQueueTagsAsync(new ListQueueTagsRequest { QueueUrl = createResp.QueueUrl });
                if (resp.Tags != null)
                {
                    foreach (var t in resp.Tags)
                    {
                        if (t.Key == "RemoveMe")
                            throw new Exception("RemoveMe tag should have been removed");
                    }
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "ChangeMessageVisibilityBatch", async () =>
        {
            var cmbName = TestRunner.MakeUniqueName("CMBQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = cmbName });
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    await sqsClient.SendMessageAsync(new SendMessageRequest
                    {
                        QueueUrl = createResp.QueueUrl,
                        MessageBody = $"batch-vis-{i}"
                    });
                }
                var recvResp = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    MaxNumberOfMessages = 10
                });
                if (recvResp.Messages.Count == 0)
                    throw new Exception("no messages received");
                var entries = new List<ChangeMessageVisibilityBatchRequestEntry>();
                foreach (var msg in recvResp.Messages)
                {
                    entries.Add(new ChangeMessageVisibilityBatchRequestEntry
                    {
                        Id = msg.MessageId,
                        ReceiptHandle = msg.ReceiptHandle,
                        VisibilityTimeout = 120
                    });
                }
                var resp = await sqsClient.ChangeMessageVisibilityBatchAsync(new ChangeMessageVisibilityBatchRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    Entries = entries
                });
                if (resp.Failed == null || resp.Failed.Count > 0)
                    throw new Exception("ChangeMessageVisibilityBatch had failures");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "ChangeMessageVisibilityBatch_NonExistent", async () =>
        {
            var cmbneName = TestRunner.MakeUniqueName("CMBNEQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = cmbneName });
            try
            {
                var resp = await sqsClient.ChangeMessageVisibilityBatchAsync(new ChangeMessageVisibilityBatchRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    Entries = new List<ChangeMessageVisibilityBatchRequestEntry>
                    {
                        new ChangeMessageVisibilityBatchRequestEntry
                        {
                            Id = "fake-msg-id",
                            ReceiptHandle = "fake-receipt-handle",
                            VisibilityTimeout = 30
                        }
                    }
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "AddPermission", async () =>
        {
            var apName = TestRunner.MakeUniqueName("APQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = apName });
            try
            {
                await sqsClient.AddPermissionAsync(new AddPermissionRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    Label = "TestPerm",
                    AWSAccountIds = new List<string> { "000000000000" },
                    Actions = new List<string> { "SendMessage" }
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.RemovePermissionAsync(new RemovePermissionRequest { QueueUrl = createResp.QueueUrl, Label = "TestPerm" }); });
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "RemovePermission", async () =>
        {
            var rpName = TestRunner.MakeUniqueName("RPQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = rpName });
            try
            {
                await sqsClient.AddPermissionAsync(new AddPermissionRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    Label = "RemoveTestPerm",
                    AWSAccountIds = new List<string> { "000000000000" },
                    Actions = new List<string> { "ReceiveMessage" }
                });
                await sqsClient.RemovePermissionAsync(new RemovePermissionRequest
                {
                    QueueUrl = createResp.QueueUrl,
                    Label = "RemoveTestPerm"
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "ListDeadLetterSourceQueues_Empty", async () =>
        {
            var dlqName = TestRunner.MakeUniqueName("DLQQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = dlqName });
            try
            {
                var resp = await sqsClient.ListDeadLetterSourceQueuesAsync(new ListDeadLetterSourceQueuesRequest
                {
                    QueueUrl = createResp.QueueUrl
                });
                if (resp.QueueUrls == null)
                    throw new Exception("queue URLs is null");
                if (resp.QueueUrls.Count != 0)
                    throw new Exception($"expected 0 dead letter source queues, got {resp.QueueUrls.Count}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "StartMessageMoveTask", async () =>
        {
            var dlqSrcName = TestRunner.MakeUniqueName("DLQSrc");
            var dlqDestName = TestRunner.MakeUniqueName("DLQDest");
            var srcResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = dlqSrcName });
            var destResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = dlqDestName });
            try
            {
                try
                {
                    await sqsClient.StartMessageMoveTaskAsync(new StartMessageMoveTaskRequest
                    {
                        SourceArn = $"arn:aws:sqs:{region}:000000000000:{dlqSrcName}",
                        DestinationArn = $"arn:aws:sqs:{region}:000000000000:{dlqDestName}"
                    });
                }
                catch (AmazonSQSException) { }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = srcResp.QueueUrl }); });
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = destResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "CancelMessageMoveTask", async () =>
        {
            try
            {
                await sqsClient.CancelMessageMoveTaskAsync(new CancelMessageMoveTaskRequest
                {
                    TaskHandle = "nonexistent-task-handle"
                });
            }
            catch (AmazonSQSException) { }
        }));

        results.Add(await runner.RunTestAsync("sqs", "ListMessageMoveTasks", async () =>
        {
            var lmtName = TestRunner.MakeUniqueName("LMTQueue");
            var createResp = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = lmtName });
            try
            {
                try
                {
                    await sqsClient.ListMessageMoveTasksAsync(new ListMessageMoveTasksRequest
                    {
                        SourceArn = $"arn:aws:sqs:{region}:000000000000:{lmtName}"
                    });
                }
                catch (AmazonSQSException) { }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); });
            }
        }));

        results.Add(await runner.RunTestAsync("sqs", "ListQueues_Pagination", async () =>
        {
            var resp = await sqsClient.ListQueuesAsync(new ListQueuesRequest
            {
                MaxResults = 10
            });
            if (resp.QueueUrls == null)
                throw new Exception("queue URLs is null");
            if (!string.IsNullOrEmpty(resp.NextToken))
            {
                var resp2 = await sqsClient.ListQueuesAsync(new ListQueuesRequest
                {
                    MaxResults = 10,
                    NextToken = resp.NextToken
                });
                if (resp2.QueueUrls == null)
                    throw new Exception("queue URLs page 2 is null");
            }
        }));

        return results;
    }
}
