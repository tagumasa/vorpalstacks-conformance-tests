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
                try { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); } catch { }
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
                try { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); } catch { }
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
                try { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); } catch { }
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
                try { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); } catch { }
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
                try { await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = createResp.QueueUrl }); } catch { }
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

        return results;
    }
}
