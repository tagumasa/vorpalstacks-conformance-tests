using Amazon.Runtime;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Kinesis;
using Amazon.Kinesis.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Scheduler;
using Amazon.Scheduler.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static class IntegrationTests
{
    private const string IntegSvc = "integration";
    private static readonly HttpClient Http = new();

    private static string IntTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
    private static string IntRoleARN(string roleName) => $"arn:aws:iam::000000000000:role/{roleName}";
    private const string EchoHandlerCode = "exports.handler = async (event) => { return JSON.stringify(event); };";

    private static async Task IAMCreateRoleAsync(string endpoint, string roleName, string trustPolicy)
    {
        var form = new Dictionary<string, string>
        {
            { "Action", "CreateRole" },
            { "Version", "2010-05-08" },
            { "RoleName", roleName },
            { "AssumeRolePolicyDocument", trustPolicy }
        };
        var content = new FormUrlEncodedContent(form);
        var resp = await Http.PostAsync(endpoint, content);
        resp.EnsureSuccessStatusCode();
    }

    private static async Task IAMDeleteRoleAsync(string endpoint, string roleName)
    {
        try
        {
            var form = new Dictionary<string, string>
            {
                { "Action", "DeleteRole" },
                { "Version", "2010-05-08" },
                { "RoleName", roleName }
            };
            var content = new FormUrlEncodedContent(form);
            await Http.PostAsync(endpoint, content);
        }
        catch { }
    }

    private static async Task<string> CreateLambdaAsync(AmazonLambdaClient lambda, string name, string roleName)
    {
        await lambda.CreateFunctionAsync(new CreateFunctionRequest
        {
            FunctionName = name,
            Runtime = Runtime.Nodejs20X,
            Role = IntRoleARN(roleName),
            Handler = "index.handler",
            Code = new FunctionCode { ZipFile = new MemoryStream(Encoding.UTF8.GetBytes(EchoHandlerCode)) }
        });
        return $"arn:aws:lambda:us-east-1:000000000000:function:{name}";
    }

    private static async Task<string> CreateQueueAsync(AmazonSQSClient sqs, string name)
    {
        var resp = await sqs.CreateQueueAsync(new CreateQueueRequest { QueueName = name });
        return resp.QueueUrl;
    }

    private static async Task DeleteQueueAsync(AmazonSQSClient sqs, string queueUrl)
    {
        try { await sqs.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = queueUrl }); } catch { }
    }

    private static async Task<List<Message>> ReceiveMessagesAsync(AmazonSQSClient sqs, string queueUrl, int maxMessages, int waitSeconds)
    {
        var resp = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = maxMessages,
            WaitTimeSeconds = waitSeconds
        });
        return resp.Messages ?? new List<Message>();
    }

    private static async Task<string> CreateTopicAsync(AmazonSimpleNotificationServiceClient sns, string name)
    {
        var resp = await sns.CreateTopicAsync(new CreateTopicRequest { Name = name });
        return resp.TopicArn;
    }

    private static async Task DeleteTopicAsync(AmazonSimpleNotificationServiceClient sns, string topicArn)
    {
        try { await sns.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = topicArn }); } catch { }
    }

    private static async Task VerifyLambdaInvokedAsync(AmazonCloudWatchLogsClient cwl, string fnName)
    {
        var logGroupName = $"/aws/lambda/{fnName}";
        var resp = await cwl.DescribeLogStreamsAsync(new DescribeLogStreamsRequest { LogGroupName = logGroupName });
        if (resp.LogStreams == null || resp.LogStreams.Count == 0)
            throw new Exception($"Lambda {fnName} was not invoked (no CW Logs at {logGroupName})");
    }

    public static async Task<List<TestResult>> RunTests(TestRunner runner, string endpoint, string region, AWSCredentials credentials)
    {
        var results = new List<TestResult>();
        var ts = IntTimestamp();

        var lambdaCfg = runner.CreateLambdaConfig();
        var ebCfg = runner.CreateEventBridgeConfig();
        var cwCfg = runner.CreateCloudWatchConfig();
        var cwlCfg = runner.CreateCloudWatchLogsConfig();
        var sfnCfg = runner.CreateStepFunctionsConfig();
        var schedCfg = runner.CreateSchedulerConfig();
        var snsCfg = runner.CreateSNSConfig();
        var sqsCfg = runner.CreateSQSConfig();
        var kinesisCfg = runner.CreateKinesisConfig();
        var s3Cfg = runner.CreateS3Config();

        using var lambda = new Amazon.Lambda.AmazonLambdaClient(credentials, lambdaCfg);
        using var eb = new Amazon.EventBridge.AmazonEventBridgeClient(credentials, ebCfg);
        using var cw = new Amazon.CloudWatch.AmazonCloudWatchClient(credentials, cwCfg);
        using var cwl = new Amazon.CloudWatchLogs.AmazonCloudWatchLogsClient(credentials, cwlCfg);
        using var sfn = new Amazon.StepFunctions.AmazonStepFunctionsClient(credentials, sfnCfg);
        using var scheduler = new Amazon.Scheduler.AmazonSchedulerClient(credentials, schedCfg);
        using var sns = new Amazon.SimpleNotificationService.AmazonSimpleNotificationServiceClient(credentials, snsCfg);
        using var sqs = new Amazon.SQS.AmazonSQSClient(credentials, sqsCfg);
        using var kinesis = new Amazon.Kinesis.AmazonKinesisClient(credentials, kinesisCfg);
        using var s3 = new Amazon.S3.AmazonS3Client(credentials, s3Cfg);

        results.Add(await runner.RunTestAsync(IntegSvc, "EventBridge_Lambda", async () =>
        {
            var fnName = $"integ-eb-lambda-{ts}";
            var roleName = $"integ-eb-lambda-role-{ts}";
            var busName = $"integ-eb-bus-{ts}";
            var ruleName = $"integ-eb-rule-{ts}";

            await IAMCreateRoleAsync(endpoint, roleName, IamHelpers.LambdaTrustPolicy);
            try
            {
                await eb.CreateEventBusAsync(new CreateEventBusRequest { Name = busName });
                try
                {
                    await eb.PutRuleAsync(new PutRuleRequest { Name = ruleName, EventBusName = busName });
                    try
                    {
                        var fnARN = await CreateLambdaAsync(lambda, fnName, roleName);
                        try
                        {
                            await eb.PutTargetsAsync(new PutTargetsRequest
                            {
                                Rule = ruleName, EventBusName = busName,
                                Targets = [new Amazon.EventBridge.Model.Target { Id = "t1", Arn = fnARN }]
                            });
                            try
                            {
                                var detail = new Dictionary<string, string> { { "test", "eventbridge-lambda" } };
                                await eb.PutEventsAsync(new PutEventsRequest
                                {
                                    Entries = [new PutEventsRequestEntry
                                    {
                                        EventBusName = busName,
                                        Source = "com.integration.test",
                                        DetailType = "IntegrationTest",
                                        Detail = JsonSerializer.Serialize(detail)
                                    }]
                                });
                                await Task.Delay(5000);
                                await VerifyLambdaInvokedAsync(cwl, fnName);
                            }
                            finally
                            {
                                try { await eb.RemoveTargetsAsync(new RemoveTargetsRequest { Rule = ruleName, EventBusName = busName, Ids = ["t1"] }); } catch { }
                            }
                        }
                        finally
                        {
                            try { await lambda.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = fnName }); } catch { }
                        }
                    }
                    finally
                    {
                        try { await eb.DeleteRuleAsync(new DeleteRuleRequest { Name = ruleName, EventBusName = busName }); } catch { }
                    }
                }
                finally
                {
                    try { await eb.DeleteEventBusAsync(new DeleteEventBusRequest { Name = busName }); } catch { }
                }
            }
            finally
            {
                await IAMDeleteRoleAsync(endpoint, roleName);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "EventBridge_StepFunctions", async () =>
        {
            var roleName = $"integ-eb-sfn-role-{ts}";
            var busName = $"integ-eb-sfn-bus-{ts}";
            var ruleName = $"integ-eb-sfn-rule-{ts}";
            var smName = $"integ-eb-sfn-sm-{ts}";

            await IAMCreateRoleAsync(endpoint, roleName, IamHelpers.StatesTrustPolicy);
            try
            {
                await eb.CreateEventBusAsync(new CreateEventBusRequest { Name = busName });
                try
                {
                    await eb.PutRuleAsync(new PutRuleRequest { Name = ruleName, EventBusName = busName });
                    try
                    {
                        var smARN = $"arn:aws:states:us-east-1:000000000000:stateMachine:{smName}";
                        await sfn.CreateStateMachineAsync(new CreateStateMachineRequest
                        {
                            Name = smName,
                            RoleArn = IntRoleARN(roleName),
                            Definition = "{\"StartAt\":\"Pass\",\"States\":{\"Pass\":{\"Type\":\"Pass\",\"End\":true}}}"
                        });
                        try
                        {
                            await eb.PutTargetsAsync(new PutTargetsRequest
                            {
                                Rule = ruleName, EventBusName = busName,
                                Targets = [new Amazon.EventBridge.Model.Target { Id = "t1", Arn = smARN }]
                            });
                            try
                            {
                                await eb.PutEventsAsync(new PutEventsRequest
                                {
                                    Entries = [new PutEventsRequestEntry
                                    {
                                        EventBusName = busName,
                                        Source = "com.integration.test",
                                        DetailType = "SFNTrigger",
                                        Detail = "{\"test\":\"eb-to-sfn\"}"
                                    }]
                                });
                                await Task.Delay(3000);
                                var execResp = await sfn.ListExecutionsAsync(new ListExecutionsRequest { StateMachineArn = smARN });
                                if (execResp.Executions == null || execResp.Executions.Count == 0)
                                    throw new Exception("expected at least 1 execution, got 0");
                            }
                            finally
                            {
                                try { await eb.RemoveTargetsAsync(new RemoveTargetsRequest { Rule = ruleName, EventBusName = busName, Ids = ["t1"] }); } catch { }
                            }
                        }
                        finally
                        {
                            try { await sfn.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = smARN }); } catch { }
                        }
                    }
                    finally
                    {
                        try { await eb.DeleteRuleAsync(new DeleteRuleRequest { Name = ruleName, EventBusName = busName }); } catch { }
                    }
                }
                finally
                {
                    try { await eb.DeleteEventBusAsync(new DeleteEventBusRequest { Name = busName }); } catch { }
                }
            }
            finally
            {
                await IAMDeleteRoleAsync(endpoint, roleName);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "EventBridge_SQS", async () =>
        {
            var queueName = $"integ-eb-sqs-{ts}";
            var busName = $"integ-eb-sqs-bus-{ts}";
            var ruleName = $"integ-eb-sqs-rule-{ts}";

            var queueUrl = await CreateQueueAsync(sqs, queueName);
            var queueARN = $"arn:aws:sqs:us-east-1:000000000000:{queueName}";
            try
            {
                await eb.CreateEventBusAsync(new CreateEventBusRequest { Name = busName });
                try
                {
                    await eb.PutRuleAsync(new PutRuleRequest { Name = ruleName, EventBusName = busName });
                    try
                    {
                        await eb.PutTargetsAsync(new PutTargetsRequest
                        {
                            Rule = ruleName, EventBusName = busName,
                            Targets = [new Amazon.EventBridge.Model.Target { Id = "t1", Arn = queueARN }]
                        });
                        try
                        {
                            await eb.PutEventsAsync(new PutEventsRequest
                            {
                                Entries = [new PutEventsRequestEntry
                                {
                                    EventBusName = busName,
                                    Source = "com.integration.test",
                                    DetailType = "SQSTest",
                                    Detail = "{\"message\":\"eb-to-sqs\"}"
                                }]
                            });
                            await Task.Delay(2000);
                            var msgs = await ReceiveMessagesAsync(sqs, queueUrl, 5, 3);
                            if (msgs.Count == 0)
                                throw new Exception("expected message in queue, got 0");
                        }
                        finally
                        {
                            try { await eb.RemoveTargetsAsync(new RemoveTargetsRequest { Rule = ruleName, EventBusName = busName, Ids = ["t1"] }); } catch { }
                        }
                    }
                    finally
                    {
                        try { await eb.DeleteRuleAsync(new DeleteRuleRequest { Name = ruleName, EventBusName = busName }); } catch { }
                    }
                }
                finally
                {
                    try { await eb.DeleteEventBusAsync(new DeleteEventBusRequest { Name = busName }); } catch { }
                }
            }
            finally
            {
                await DeleteQueueAsync(sqs, queueUrl);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "EventBridge_SNS", async () =>
        {
            var topicName = $"integ-eb-sns-{ts}";
            var busName = $"integ-eb-sns-bus-{ts}";
            var ruleName = $"integ-eb-sns-rule-{ts}";
            var queueName = $"integ-eb-sns-sqs-{ts}";

            var topicARN = await CreateTopicAsync(sns, topicName);
            try
            {
                var queueUrl = await CreateQueueAsync(sqs, queueName);
                try
                {
                    await sns.SubscribeAsync(new SubscribeRequest
                    {
                        TopicArn = topicARN,
                        Protocol = "sqs",
                        Endpoint = $"arn:aws:sqs:us-east-1:000000000000:{queueName}"
                    });
                    await eb.CreateEventBusAsync(new CreateEventBusRequest { Name = busName });
                    try
                    {
                        await eb.PutRuleAsync(new PutRuleRequest { Name = ruleName, EventBusName = busName });
                        try
                        {
                            await eb.PutTargetsAsync(new PutTargetsRequest
                            {
                                Rule = ruleName, EventBusName = busName,
                                Targets = [new Amazon.EventBridge.Model.Target { Id = "t1", Arn = topicARN }]
                            });
                            try
                            {
                                await eb.PutEventsAsync(new PutEventsRequest
                                {
                                    Entries = [new PutEventsRequestEntry
                                    {
                                        EventBusName = busName,
                                        Source = "com.integration.test",
                                        DetailType = "SNSTest",
                                        Detail = "{\"message\":\"eb-to-sns\"}"
                                    }]
                                });
                                await Task.Delay(3000);
                                var msgs = await ReceiveMessagesAsync(sqs, queueUrl, 5, 3);
                                if (msgs.Count == 0)
                                    throw new Exception("expected message in queue (via SNS), got 0");
                            }
                            finally
                            {
                                try { await eb.RemoveTargetsAsync(new RemoveTargetsRequest { Rule = ruleName, EventBusName = busName, Ids = ["t1"] }); } catch { }
                            }
                        }
                        finally
                        {
                            try { await eb.DeleteRuleAsync(new DeleteRuleRequest { Name = ruleName, EventBusName = busName }); } catch { }
                        }
                    }
                    finally
                    {
                        try { await eb.DeleteEventBusAsync(new DeleteEventBusRequest { Name = busName }); } catch { }
                    }
                }
                finally
                {
                    await DeleteQueueAsync(sqs, queueUrl);
                }
            }
            finally
            {
                await DeleteTopicAsync(sns, topicARN);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "EventBridge_Kinesis", async () =>
        {
            var streamName = $"integ-eb-kinesis-{ts}";
            var busName = $"integ-eb-kin-bus-{ts}";
            var ruleName = $"integ-eb-kin-rule-{ts}";

            await kinesis.CreateStreamAsync(new CreateStreamRequest { StreamName = streamName, ShardCount = 1 });
            try
            {
                await Task.Delay(1000);
                var streamARN = $"arn:aws:kinesis:us-east-1:000000000000:stream/{streamName}";
                await eb.CreateEventBusAsync(new CreateEventBusRequest { Name = busName });
                try
                {
                    await eb.PutRuleAsync(new PutRuleRequest { Name = ruleName, EventBusName = busName });
                    try
                    {
                        await eb.PutTargetsAsync(new PutTargetsRequest
                        {
                            Rule = ruleName, EventBusName = busName,
                            Targets = [new Amazon.EventBridge.Model.Target { Id = "t1", Arn = streamARN }]
                        });
                        try
                        {
                            await eb.PutEventsAsync(new PutEventsRequest
                            {
                                Entries = [new PutEventsRequestEntry
                                {
                                    EventBusName = busName,
                                    Source = "com.integration.test",
                                    DetailType = "KinesisTest",
                                    Detail = "{\"message\":\"eb-to-kinesis\"}"
                                }]
                            });
                            await Task.Delay(3000);
                            var descResp = await kinesis.DescribeStreamAsync(new DescribeStreamRequest { StreamName = streamName });
                            if (descResp.StreamDescription.Shards == null || descResp.StreamDescription.Shards.Count == 0)
                                throw new Exception("no shards in stream");
                            var shardID = descResp.StreamDescription.Shards[0].ShardId;
                            var iterResp = await kinesis.GetShardIteratorAsync(new GetShardIteratorRequest
                            {
                                StreamName = streamName,
                                ShardId = shardID,
                                ShardIteratorType = Amazon.Kinesis.ShardIteratorType.TRIM_HORIZON
                            });
                            var recordsResp = await kinesis.GetRecordsAsync(new GetRecordsRequest { ShardIterator = iterResp.ShardIterator });
                            if (recordsResp.Records == null || recordsResp.Records.Count == 0)
                                throw new Exception("expected records in kinesis stream, got 0");
                        }
                        finally
                        {
                            try { await eb.RemoveTargetsAsync(new RemoveTargetsRequest { Rule = ruleName, EventBusName = busName, Ids = ["t1"] }); } catch { }
                        }
                    }
                    finally
                    {
                        try { await eb.DeleteRuleAsync(new DeleteRuleRequest { Name = ruleName, EventBusName = busName }); } catch { }
                    }
                }
                finally
                {
                    try { await eb.DeleteEventBusAsync(new DeleteEventBusRequest { Name = busName }); } catch { }
                }
            }
            finally
            {
                try { await kinesis.DeleteStreamAsync(new DeleteStreamRequest { StreamName = streamName }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "ESM_SQS_Lambda", async () =>
        {
            var fnName = $"integ-esm-sqs-fn-{ts}";
            var roleName = $"integ-esm-sqs-role-{ts}";
            var queueName = $"integ-esm-sqs-q-{ts}";

            await IAMCreateRoleAsync(endpoint, roleName, IamHelpers.LambdaTrustPolicy);
            try
            {
                await CreateLambdaAsync(lambda, fnName, roleName);
                try
                {
                    var queueUrl = await CreateQueueAsync(sqs, queueName);
                    try
                    {
                        await lambda.CreateEventSourceMappingAsync(new CreateEventSourceMappingRequest
                        {
                            FunctionName = fnName,
                            EventSourceArn = $"arn:aws:sqs:us-east-1:000000000000:{queueName}",
                            Enabled = true,
                            BatchSize = 10
                        });
                        await sqs.SendMessageAsync(new SendMessageRequest
                        {
                            QueueUrl = queueUrl,
                            MessageBody = "{\"test\":\"esm-sqs\"}"
                        });
                        await Task.Delay(5000);
                        var msgs = await ReceiveMessagesAsync(sqs, queueUrl, 10, 1);
                        foreach (var m in msgs)
                        {
                            try { await sqs.DeleteMessageAsync(new DeleteMessageRequest { QueueUrl = queueUrl, ReceiptHandle = m.ReceiptHandle }); } catch { }
                        }
                        if (msgs.Count > 0)
                            throw new Exception($"expected ESM to consume all messages, got {msgs.Count} remaining");
                    }
                    finally
                    {
                        await DeleteQueueAsync(sqs, queueUrl);
                    }
                }
                finally
                {
                    try { await lambda.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = fnName }); } catch { }
                }
            }
            finally
            {
                await IAMDeleteRoleAsync(endpoint, roleName);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "ESM_Kinesis_Lambda", async () =>
        {
            var fnName = $"integ-esm-kin-fn-{ts}";
            var roleName = $"integ-esm-kin-role-{ts}";
            var streamName = $"integ-esm-kin-s-{ts}";

            await IAMCreateRoleAsync(endpoint, roleName, IamHelpers.LambdaTrustPolicy);
            try
            {
                await CreateLambdaAsync(lambda, fnName, roleName);
                try
                {
                    await kinesis.CreateStreamAsync(new CreateStreamRequest { StreamName = streamName, ShardCount = 1 });
                    try
                    {
                        await Task.Delay(1000);
                        var esmResp = await lambda.CreateEventSourceMappingAsync(new CreateEventSourceMappingRequest
                        {
                            FunctionName = fnName,
                            EventSourceArn = $"arn:aws:kinesis:us-east-1:000000000000:stream/{streamName}",
                            Enabled = true,
                            BatchSize = 100,
                            StartingPosition = EventSourcePosition.LATEST
                        });
                        try
                        {
                            await kinesis.PutRecordAsync(new PutRecordRequest
                            {
                                StreamName = streamName,
                                PartitionKey = "p1",
                                Data = new MemoryStream(Encoding.UTF8.GetBytes("{\"test\":\"esm-kinesis\"}"))
                            });
                            await Task.Delay(8000);
                            await VerifyLambdaInvokedAsync(cwl, fnName);
                        }
                        finally
                        {
                            try { await lambda.DeleteEventSourceMappingAsync(new DeleteEventSourceMappingRequest { UUID = esmResp.UUID }); } catch { }
                        }
                    }
                    finally
                    {
                        try { await kinesis.DeleteStreamAsync(new DeleteStreamRequest { StreamName = streamName }); } catch { }
                    }
                }
                finally
                {
                    try { await lambda.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = fnName }); } catch { }
                }
            }
            finally
            {
                await IAMDeleteRoleAsync(endpoint, roleName);
            }
        }));

        results.Add(runner.SkipTest(IntegSvc, "ESM_DynamoDBStreams_ToLambda", "DynamoDB Streams ESM not yet implemented on server"));

        results.Add(await runner.RunTestAsync(IntegSvc, "CWAlarm_SNS", async () =>
        {
            var topicName = $"integ-alarm-sns-{ts}";
            var queueName = $"integ-alarm-sns-q-{ts}";
            var alarmName = $"integ-alarm-sns-{ts}";

            var topicARN = await CreateTopicAsync(sns, topicName);
            try
            {
                var queueUrl = await CreateQueueAsync(sqs, queueName);
                try
                {
                    await sns.SubscribeAsync(new SubscribeRequest
                    {
                        TopicArn = topicARN,
                        Protocol = "sqs",
                        Endpoint = $"arn:aws:sqs:us-east-1:000000000000:{queueName}"
                    });
                    await cw.PutMetricAlarmAsync(new PutMetricAlarmRequest
                    {
                        AlarmName = alarmName,
                        MetricName = "CPUUtilization",
                        Namespace = "AWS/EC2",
                        Statistic = Statistic.Average,
                        Period = 1,
                        EvaluationPeriods = 1,
                        Threshold = 0,
                        ComparisonOperator = ComparisonOperator.GreaterThanThreshold,
                        AlarmActions = [topicARN]
                    });
                    try
                    {
                        await cw.PutMetricDataAsync(new PutMetricDataRequest
                        {
                            Namespace = "AWS/EC2",
                            MetricData = [new MetricDatum { MetricName = "CPUUtilization", Value = 100 }]
                        });
                        await Task.Delay(3000);
                        var msgs = await ReceiveMessagesAsync(sqs, queueUrl, 5, 3);
                        if (msgs.Count == 0)
                            throw new Exception("expected alarm notification in queue, got 0");
                    }
                    finally
                    {
                        try { await cw.DeleteAlarmsAsync(new DeleteAlarmsRequest { AlarmNames = [alarmName] }); } catch { }
                    }
                }
                finally
                {
                    await DeleteQueueAsync(sqs, queueUrl);
                }
            }
            finally
            {
                await DeleteTopicAsync(sns, topicARN);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "CWAlarm_Lambda", async () =>
        {
            var fnName = $"integ-alarm-lambda-{ts}";
            var roleName = $"integ-alarm-lambda-role-{ts}";
            var alarmName = $"integ-alarm-lambda-{ts}";

            await IAMCreateRoleAsync(endpoint, roleName, IamHelpers.LambdaTrustPolicy);
            try
            {
                var fnARN = await CreateLambdaAsync(lambda, fnName, roleName);
                try
                {
                    await cw.PutMetricAlarmAsync(new PutMetricAlarmRequest
                    {
                        AlarmName = alarmName,
                        MetricName = "MemoryUtilization",
                        Namespace = "AWS/EC2",
                        Statistic = Statistic.Average,
                        Period = 1,
                        EvaluationPeriods = 1,
                        Threshold = 0,
                        ComparisonOperator = ComparisonOperator.GreaterThanThreshold,
                        AlarmActions = [fnARN]
                    });
                    try
                    {
                        await cw.PutMetricDataAsync(new PutMetricDataRequest
                        {
                            Namespace = "AWS/EC2",
                            MetricData = [new MetricDatum { MetricName = "MemoryUtilization", Value = 100 }]
                        });
                        await Task.Delay(5000);
                        var alarmResp = await cw.DescribeAlarmsAsync(new DescribeAlarmsRequest { AlarmNames = [alarmName] });
                        if (alarmResp.MetricAlarms == null || alarmResp.MetricAlarms.Count == 0)
                            throw new Exception($"alarm {alarmName} not found");
                        if (alarmResp.MetricAlarms[0].StateValue != StateValue.ALARM)
                            throw new Exception($"expected alarm state ALARM, got {alarmResp.MetricAlarms[0].StateValue}");
                        await VerifyLambdaInvokedAsync(cwl, fnName);
                    }
                    finally
                    {
                        try { await cw.DeleteAlarmsAsync(new DeleteAlarmsRequest { AlarmNames = [alarmName] }); } catch { }
                    }
                }
                finally
                {
                    try { await lambda.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = fnName }); } catch { }
                }
            }
            finally
            {
                await IAMDeleteRoleAsync(endpoint, roleName);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "CWAlarm_StepFunctions", async () =>
        {
            var roleName = $"integ-alarm-sfn-role-{ts}";
            var alarmName = $"integ-alarm-sfn-{ts}";
            var smName = $"integ-alarm-sfn-sm-{ts}";

            await IAMCreateRoleAsync(endpoint, roleName, IamHelpers.StatesTrustPolicy);
            try
            {
                var smARN = $"arn:aws:states:us-east-1:000000000000:stateMachine:{smName}";
                await sfn.CreateStateMachineAsync(new CreateStateMachineRequest
                {
                    Name = smName,
                    RoleArn = IntRoleARN(roleName),
                    Definition = "{\"StartAt\":\"Pass\",\"States\":{\"Pass\":{\"Type\":\"Pass\",\"End\":true}}}"
                });
                try
                {
                    await cw.PutMetricAlarmAsync(new PutMetricAlarmRequest
                    {
                        AlarmName = alarmName,
                        MetricName = "MemoryUtilization",
                        Namespace = "AWS/EC2",
                        Statistic = Statistic.Average,
                        Period = 1,
                        EvaluationPeriods = 1,
                        Threshold = 0,
                        ComparisonOperator = ComparisonOperator.GreaterThanThreshold,
                        AlarmActions = [smARN]
                    });
                    try
                    {
                        await cw.PutMetricDataAsync(new PutMetricDataRequest
                        {
                            Namespace = "AWS/EC2",
                            MetricData = [new MetricDatum { MetricName = "DiskSpaceUtilization", Value = 100 }]
                        });
                        await Task.Delay(3000);
                        var execResp = await sfn.ListExecutionsAsync(new ListExecutionsRequest { StateMachineArn = smARN });
                        if (execResp.Executions == null || execResp.Executions.Count == 0)
                            throw new Exception("expected at least 1 execution from alarm, got 0");
                    }
                    finally
                    {
                        try { await cw.DeleteAlarmsAsync(new DeleteAlarmsRequest { AlarmNames = [alarmName] }); } catch { }
                    }
                }
                finally
                {
                    try { await sfn.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = smARN }); } catch { }
                }
            }
            finally
            {
                await IAMDeleteRoleAsync(endpoint, roleName);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "Scheduler_Lambda", async () =>
        {
            var fnName = $"integ-sched-lambda-{ts}";
            var roleName = $"integ-sched-role-{ts}";
            var lambdaRoleName = $"integ-sched-lambda-fn-role-{ts}";
            var scheduleName = $"integ-sched-lambda-{ts}";
            var groupName = $"integ-sched-group-{ts}";

            await IAMCreateRoleAsync(endpoint, roleName, IamHelpers.SchedulerTrustPolicy);
            try
            {
                await IAMCreateRoleAsync(endpoint, lambdaRoleName, IamHelpers.LambdaTrustPolicy);
                try
                {
                    await scheduler.CreateScheduleGroupAsync(new CreateScheduleGroupRequest { Name = groupName });
                    try
                    {
                        var fnARN = await CreateLambdaAsync(lambda, fnName, lambdaRoleName);
                        try
                        {
                            await scheduler.CreateScheduleAsync(new CreateScheduleRequest
                            {
                                Name = scheduleName,
                                GroupName = groupName,
                                ScheduleExpression = "rate(1 minute)",
                                Target = new Amazon.Scheduler.Model.Target { Arn = fnARN, RoleArn = IntRoleARN(roleName) },
                                FlexibleTimeWindow = new FlexibleTimeWindow { Mode = FlexibleTimeWindowMode.OFF }
                            });
                            try
                            {
                                await Task.Delay(8000);
                                await VerifyLambdaInvokedAsync(cwl, fnName);
                            }
                            finally
                            {
                                try { await scheduler.DeleteScheduleAsync(new DeleteScheduleRequest { Name = scheduleName, GroupName = groupName }); } catch { }
                            }
                        }
                        finally
                        {
                            try { await lambda.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = fnName }); } catch { }
                        }
                    }
                    finally
                    {
                        try { await scheduler.DeleteScheduleGroupAsync(new DeleteScheduleGroupRequest { Name = groupName }); } catch { }
                    }
                }
                finally
                {
                    await IAMDeleteRoleAsync(endpoint, lambdaRoleName);
                }
            }
            finally
            {
                await IAMDeleteRoleAsync(endpoint, roleName);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "Scheduler_SQS", async () =>
        {
            var roleName = $"integ-sched-sqs-role-{ts}";
            var scheduleName = $"integ-sched-sqs-{ts}";
            var groupName = $"integ-sched-sqs-group-{ts}";
            var queueName = $"integ-sched-sqs-q-{ts}";

            await IAMCreateRoleAsync(endpoint, roleName, IamHelpers.SchedulerTrustPolicy);
            try
            {
                await scheduler.CreateScheduleGroupAsync(new CreateScheduleGroupRequest { Name = groupName });
                try
                {
                    var queueUrl = await CreateQueueAsync(sqs, queueName);
                    try
                    {
                        var queueARN = $"arn:aws:sqs:us-east-1:000000000000:{queueName}";
                        await scheduler.CreateScheduleAsync(new CreateScheduleRequest
                        {
                            Name = scheduleName,
                            GroupName = groupName,
                            ScheduleExpression = "rate(1 minute)",
                            Target = new Amazon.Scheduler.Model.Target { Arn = queueARN, RoleArn = IntRoleARN(roleName) },
                            FlexibleTimeWindow = new FlexibleTimeWindow { Mode = FlexibleTimeWindowMode.OFF }
                        });
                        try
                        {
                            await Task.Delay(5000);
                            var msgs = await ReceiveMessagesAsync(sqs, queueUrl, 5, 3);
                            if (msgs.Count == 0)
                                throw new Exception("expected message from scheduler in queue, got 0");
                        }
                        finally
                        {
                            try { await scheduler.DeleteScheduleAsync(new DeleteScheduleRequest { Name = scheduleName, GroupName = groupName }); } catch { }
                        }
                    }
                    finally
                    {
                        await DeleteQueueAsync(sqs, queueUrl);
                    }
                }
                finally
                {
                    try { await scheduler.DeleteScheduleGroupAsync(new DeleteScheduleGroupRequest { Name = groupName }); } catch { }
                }
            }
            finally
            {
                await IAMDeleteRoleAsync(endpoint, roleName);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "Scheduler_SNS", async () =>
        {
            var roleName = $"integ-sched-sns-role-{ts}";
            var scheduleName = $"integ-sched-sns-{ts}";
            var groupName = $"integ-sched-sns-group-{ts}";
            var topicName = $"integ-sched-sns-t-{ts}";
            var queueName = $"integ-sched-sns-q-{ts}";

            await IAMCreateRoleAsync(endpoint, roleName, IamHelpers.SchedulerTrustPolicy);
            try
            {
                await scheduler.CreateScheduleGroupAsync(new CreateScheduleGroupRequest { Name = groupName });
                try
                {
                    var topicARN = await CreateTopicAsync(sns, topicName);
                    try
                    {
                        var queueUrl = await CreateQueueAsync(sqs, queueName);
                        try
                        {
                            await sns.SubscribeAsync(new SubscribeRequest
                            {
                                TopicArn = topicARN,
                                Protocol = "sqs",
                                Endpoint = $"arn:aws:sqs:us-east-1:000000000000:{queueName}"
                            });
                            await scheduler.CreateScheduleAsync(new CreateScheduleRequest
                            {
                                Name = scheduleName,
                                GroupName = groupName,
                                ScheduleExpression = "rate(1 minute)",
                                Target = new Amazon.Scheduler.Model.Target { Arn = topicARN, RoleArn = IntRoleARN(roleName) },
                                FlexibleTimeWindow = new FlexibleTimeWindow { Mode = FlexibleTimeWindowMode.OFF }
                            });
                            try
                            {
                                await Task.Delay(5000);
                                var msgs = await ReceiveMessagesAsync(sqs, queueUrl, 5, 3);
                                if (msgs.Count == 0)
                                    throw new Exception("expected message from scheduler (via SNS) in queue, got 0");
                            }
                            finally
                            {
                                try { await scheduler.DeleteScheduleAsync(new DeleteScheduleRequest { Name = scheduleName, GroupName = groupName }); } catch { }
                            }
                        }
                        finally
                        {
                            await DeleteQueueAsync(sqs, queueUrl);
                        }
                    }
                    finally
                    {
                        await DeleteTopicAsync(sns, topicARN);
                    }
                }
                finally
                {
                    try { await scheduler.DeleteScheduleGroupAsync(new DeleteScheduleGroupRequest { Name = groupName }); } catch { }
                }
            }
            finally
            {
                await IAMDeleteRoleAsync(endpoint, roleName);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "Scheduler_StepFunctions", async () =>
        {
            var roleName = $"integ-sched-sfn-role-{ts}";
            var scheduleName = $"integ-sched-sfn-{ts}";
            var groupName = $"integ-sched-sfn-group-{ts}";
            var smName = $"integ-sched-sfn-sm-{ts}";
            var sfnRoleName = $"{roleName}-sfn";

            await IAMCreateRoleAsync(endpoint, roleName, IamHelpers.SchedulerTrustPolicy);
            try
            {
                await IAMCreateRoleAsync(endpoint, sfnRoleName, IamHelpers.StatesTrustPolicy);
                try
                {
                    await scheduler.CreateScheduleGroupAsync(new CreateScheduleGroupRequest { Name = groupName });
                    try
                    {
                        var smARN = $"arn:aws:states:us-east-1:000000000000:stateMachine:{smName}";
                        await sfn.CreateStateMachineAsync(new CreateStateMachineRequest
                        {
                            Name = smName,
                            RoleArn = IntRoleARN(sfnRoleName),
                            Definition = "{\"StartAt\":\"Pass\",\"States\":{\"Pass\":{\"Type\":\"Pass\",\"End\":true}}}"
                        });
                        try
                        {
                            await scheduler.CreateScheduleAsync(new CreateScheduleRequest
                            {
                                Name = scheduleName,
                                GroupName = groupName,
                                ScheduleExpression = "rate(1 minute)",
                                Target = new Amazon.Scheduler.Model.Target { Arn = smARN, RoleArn = IntRoleARN(roleName) },
                                FlexibleTimeWindow = new FlexibleTimeWindow { Mode = FlexibleTimeWindowMode.OFF }
                            });
                            try
                            {
                                await Task.Delay(5000);
                                var execResp = await sfn.ListExecutionsAsync(new ListExecutionsRequest { StateMachineArn = smARN });
                                if (execResp.Executions == null || execResp.Executions.Count == 0)
                                    throw new Exception("expected at least 1 execution from scheduler, got 0");
                            }
                            finally
                            {
                                try { await scheduler.DeleteScheduleAsync(new DeleteScheduleRequest { Name = scheduleName, GroupName = groupName }); } catch { }
                            }
                        }
                        finally
                        {
                            try { await sfn.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = smARN }); } catch { }
                        }
                    }
                    finally
                    {
                        try { await scheduler.DeleteScheduleGroupAsync(new DeleteScheduleGroupRequest { Name = groupName }); } catch { }
                    }
                }
                finally
                {
                    await IAMDeleteRoleAsync(endpoint, sfnRoleName);
                }
            }
            finally
            {
                await IAMDeleteRoleAsync(endpoint, roleName);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "SFN_Task_Lambda", async () =>
        {
            var roleName = $"integ-sfn-lambda-role-{ts}";
            var lambdaRoleName = $"integ-sfn-lambda-fn-role-{ts}";
            var smName = $"integ-sfn-lambda-{ts}";
            var fnName = $"integ-sfn-lambda-fn-{ts}";

            await IAMCreateRoleAsync(endpoint, roleName, IamHelpers.StatesTrustPolicy);
            try
            {
                await IAMCreateRoleAsync(endpoint, lambdaRoleName, IamHelpers.LambdaTrustPolicy);
                try
                {
                    var fnARN = await CreateLambdaAsync(lambda, fnName, lambdaRoleName);
                    try
                    {
                        var definition = $"{{\"StartAt\":\"InvokeLambda\",\"States\":{{\"InvokeLambda\":{{\"Type\":\"Task\",\"Resource\":\"{fnARN}\",\"End\":true}}}}}}";
                        var createResp = await sfn.CreateStateMachineAsync(new CreateStateMachineRequest
                        {
                            Name = smName,
                            RoleArn = IntRoleARN(roleName),
                            Definition = definition
                        });
                        try
                        {
                            var execResp = await sfn.StartExecutionAsync(new StartExecutionRequest
                            {
                                StateMachineArn = createResp.StateMachineArn,
                                Input = "{\"test\":\"sfn-task-lambda\"}"
                            });
                            await Task.Delay(3000);
                            var descResp = await sfn.DescribeExecutionAsync(new DescribeExecutionRequest { ExecutionArn = execResp.ExecutionArn });
                            if (descResp.Status != "SUCCEEDED")
                                throw new Exception($"expected SUCCEEDED, got {descResp.Status}");
                        }
                        finally
                        {
                            try { await sfn.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = createResp.StateMachineArn }); } catch { }
                        }
                    }
                    finally
                    {
                        try { await lambda.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = fnName }); } catch { }
                    }
                }
                finally
                {
                    await IAMDeleteRoleAsync(endpoint, lambdaRoleName);
                }
            }
            finally
            {
                await IAMDeleteRoleAsync(endpoint, roleName);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "SFN_Task_SQS", async () =>
        {
            var roleName = $"integ-sfn-sqs-role-{ts}";
            var smName = $"integ-sfn-sqs-{ts}";
            var queueName = $"integ-sfn-sqs-q-{ts}";

            await IAMCreateRoleAsync(endpoint, roleName, IamHelpers.StatesTrustPolicy);
            try
            {
                var queueUrl = await CreateQueueAsync(sqs, queueName);
                try
                {
                    var definition = $"{{\"StartAt\":\"SendMsg\",\"States\":{{\"SendMsg\":{{\"Type\":\"Task\",\"Resource\":\"arn:aws:states:::sqs:sendMessage\",\"Parameters\":{{\"QueueUrl\":\"{queueUrl}\",\"MessageBody\":{{\"test\":\"sfn-to-sqs\"}}}},\"End\":true}}}}}}";
                    var createResp = await sfn.CreateStateMachineAsync(new CreateStateMachineRequest
                    {
                        Name = smName,
                        RoleArn = IntRoleARN(roleName),
                        Definition = definition
                    });
                    try
                    {
                        var startResp = await sfn.StartExecutionAsync(new StartExecutionRequest
                        {
                            StateMachineArn = createResp.StateMachineArn,
                            Input = "{}"
                        });
                        await Task.Delay(3000);
                        var descResp = await sfn.DescribeExecutionAsync(new DescribeExecutionRequest { ExecutionArn = startResp.ExecutionArn });
                        if (descResp.Status != "SUCCEEDED")
                            throw new Exception($"expected SUCCEEDED, got {descResp.Status}");
                        var msgs = await ReceiveMessagesAsync(sqs, queueUrl, 5, 3);
                        if (msgs.Count == 0)
                            throw new Exception("expected SQS message from SFN Task, got 0");
                    }
                    finally
                    {
                        try { await sfn.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = createResp.StateMachineArn }); } catch { }
                    }
                }
                finally
                {
                    await DeleteQueueAsync(sqs, queueUrl);
                }
            }
            finally
            {
                await IAMDeleteRoleAsync(endpoint, roleName);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "SFN_Task_SNS", async () =>
        {
            var roleName = $"integ-sfn-sns-role-{ts}";
            var smName = $"integ-sfn-sns-{ts}";
            var topicName = $"integ-sfn-sns-t-{ts}";
            var queueName = $"integ-sfn-sns-q-{ts}";

            await IAMCreateRoleAsync(endpoint, roleName, IamHelpers.StatesTrustPolicy);
            try
            {
                var topicARN = await CreateTopicAsync(sns, topicName);
                try
                {
                    var queueUrl = await CreateQueueAsync(sqs, queueName);
                    try
                    {
                        await sns.SubscribeAsync(new SubscribeRequest
                        {
                            TopicArn = topicARN,
                            Protocol = "sqs",
                            Endpoint = $"arn:aws:sqs:us-east-1:000000000000:{queueName}"
                        });
                        var definition = $"{{\"StartAt\":\"Publish\",\"States\":{{\"Publish\":{{\"Type\":\"Task\",\"Resource\":\"arn:aws:states:::sns:publish\",\"Parameters\":{{\"TopicArn\":\"{topicARN}\",\"Message\":{{\"test\":\"sfn-to-sns\"}}}},\"End\":true}}}}}}";
                        var createResp = await sfn.CreateStateMachineAsync(new CreateStateMachineRequest
                        {
                            Name = smName,
                            RoleArn = IntRoleARN(roleName),
                            Definition = definition
                        });
                        try
                        {
                            var startResp = await sfn.StartExecutionAsync(new StartExecutionRequest
                            {
                                StateMachineArn = createResp.StateMachineArn,
                                Input = "{}"
                            });
                            await Task.Delay(3000);
                            var descResp = await sfn.DescribeExecutionAsync(new DescribeExecutionRequest { ExecutionArn = startResp.ExecutionArn });
                            if (descResp.Status != "SUCCEEDED")
                                throw new Exception($"expected SUCCEEDED, got {descResp.Status}");
                            var msgs = await ReceiveMessagesAsync(sqs, queueUrl, 5, 3);
                            if (msgs.Count == 0)
                                throw new Exception("expected SNS message from SFN Task in queue, got 0");
                        }
                        finally
                        {
                            try { await sfn.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = createResp.StateMachineArn }); } catch { }
                        }
                    }
                    finally
                    {
                        await DeleteQueueAsync(sqs, queueUrl);
                    }
                }
                finally
                {
                    await DeleteTopicAsync(sns, topicARN);
                }
            }
            finally
            {
                await IAMDeleteRoleAsync(endpoint, roleName);
            }
        }));

        results.Add(runner.SkipTest(IntegSvc, "SFN_Task_DynamoDB", "DynamoDB task integration not yet implemented on server"));

        results.Add(await runner.RunTestAsync(IntegSvc, "S3_Notification_Lambda", async () =>
        {
            var bucketName = $"integ-s3-lambda-{ts.ToLower()}";
            var fnName = $"integ-s3-lambda-fn-{ts}";
            var roleName = $"integ-s3-lambda-role-{ts}";

            await IAMCreateRoleAsync(endpoint, roleName, IamHelpers.LambdaTrustPolicy);
            try
            {
                var fnARN = await CreateLambdaAsync(lambda, fnName, roleName);
                try
                {
                    await s3.PutBucketAsync(new PutBucketRequest { BucketName = bucketName });
                    try
                    {
                        await s3.PutBucketNotificationAsync(new PutBucketNotificationRequest
                        {
                            BucketName = bucketName,
                            LambdaFunctionConfigurations = [new LambdaFunctionConfiguration
                            {
                                Id = "1",
                                FunctionArn = fnARN,
                                Events = [Amazon.S3.EventType.ObjectCreatedPut]
                            }]
                        });
                        await s3.PutObjectAsync(new PutObjectRequest { BucketName = bucketName, Key = "test-key.txt", ContentBody = "test-data" });
                        await Task.Delay(5000);
                        await VerifyLambdaInvokedAsync(cwl, fnName);
                    }
                    finally
                    {
                        try { await s3.DeleteBucketAsync(new DeleteBucketRequest { BucketName = bucketName }); } catch { }
                    }
                }
                finally
                {
                    try { await lambda.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = fnName }); } catch { }
                }
            }
            finally
            {
                await IAMDeleteRoleAsync(endpoint, roleName);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "S3_Notification_SQS", async () =>
        {
            var bucketName = $"integ-s3-sqs-{ts.ToLower()}";
            var queueName = $"integ-s3-sqs-q-{ts}";

            var queueUrl = await CreateQueueAsync(sqs, queueName);
            try
            {
                var queueARN = $"arn:aws:sqs:us-east-1:000000000000:{queueName}";
                await s3.PutBucketAsync(new PutBucketRequest { BucketName = bucketName });
                try
                {
                    await s3.PutBucketNotificationAsync(new PutBucketNotificationRequest
                    {
                        BucketName = bucketName,
                        QueueConfigurations = [new QueueConfiguration
                        {
                            Id = "1",
                            Queue = queueARN,
                            Events = [Amazon.S3.EventType.ObjectCreatedPut]
                        }]
                    });
                    await s3.PutObjectAsync(new PutObjectRequest { BucketName = bucketName, Key = "test-key.txt", ContentBody = "test-data" });
                    await Task.Delay(2000);
                    var msgs = await ReceiveMessagesAsync(sqs, queueUrl, 5, 3);
                    if (msgs.Count == 0)
                        throw new Exception("expected S3 notification in queue, got 0");
                }
                finally
                {
                    try { await s3.DeleteBucketAsync(new DeleteBucketRequest { BucketName = bucketName }); } catch { }
                }
            }
            finally
            {
                await DeleteQueueAsync(sqs, queueUrl);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "S3_Notification_SNS", async () =>
        {
            var bucketName = $"integ-s3-sns-{ts.ToLower()}";
            var topicName = $"integ-s3-sns-t-{ts}";
            var queueName = $"integ-s3-sns-q-{ts}";

            var topicARN = await CreateTopicAsync(sns, topicName);
            try
            {
                var queueUrl = await CreateQueueAsync(sqs, queueName);
                try
                {
                    await sns.SubscribeAsync(new SubscribeRequest
                    {
                        TopicArn = topicARN,
                        Protocol = "sqs",
                        Endpoint = $"arn:aws:sqs:us-east-1:000000000000:{queueName}"
                    });
                    await s3.PutBucketAsync(new PutBucketRequest { BucketName = bucketName });
                    try
                    {
                        await s3.PutBucketNotificationAsync(new PutBucketNotificationRequest
                        {
                            BucketName = bucketName,
                            TopicConfigurations = [new TopicConfiguration
                            {
                                Id = "1",
                                Topic = topicARN,
                                Events = [Amazon.S3.EventType.ObjectCreatedPut]
                            }]
                        });
                        await s3.PutObjectAsync(new PutObjectRequest { BucketName = bucketName, Key = "test-key.txt", ContentBody = "test-data" });
                        await Task.Delay(3000);
                        var msgs = await ReceiveMessagesAsync(sqs, queueUrl, 5, 3);
                        if (msgs.Count == 0)
                            throw new Exception("expected S3 notification (via SNS) in queue, got 0");
                    }
                    finally
                    {
                        try { await s3.DeleteBucketAsync(new DeleteBucketRequest { BucketName = bucketName }); } catch { }
                    }
                }
                finally
                {
                    await DeleteQueueAsync(sqs, queueUrl);
                }
            }
            finally
            {
                await DeleteTopicAsync(sns, topicARN);
            }
        }));

        results.Add(runner.SkipTest(IntegSvc, "S3_Notification_Kinesis", "S3 notification to Kinesis not yet implemented on server"));

        results.Add(await runner.RunTestAsync(IntegSvc, "CWLogs_Lambda", async () =>
        {
            var fnName = $"integ-cwl-lambda-{ts}";
            var roleName = $"integ-cwl-role-{ts}";
            var logGroupName = $"/integ/cwl-lambda/{ts}";

            await IAMCreateRoleAsync(endpoint, roleName, IamHelpers.LambdaTrustPolicy);
            try
            {
                var fnARN = await CreateLambdaAsync(lambda, fnName, roleName);
                try
                {
                    await cwl.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = logGroupName });
                    try
                    {
                        await cwl.PutSubscriptionFilterAsync(new PutSubscriptionFilterRequest
                        {
                            LogGroupName = logGroupName,
                            FilterName = "integ-lambda-sub",
                            FilterPattern = "[...]",
                            DestinationArn = fnARN
                        });
                        await cwl.CreateLogStreamAsync(new CreateLogStreamRequest
                        {
                            LogGroupName = logGroupName,
                            LogStreamName = "test-stream"
                        });
                        await cwl.PutLogEventsAsync(new PutLogEventsRequest
                        {
                            LogGroupName = logGroupName,
                            LogStreamName = "test-stream",
                            LogEvents = [new InputLogEvent { Message = "integration test log message", Timestamp = DateTimeOffset.UtcNow.UtcDateTime }]
                        });
                        await Task.Delay(5000);
                        await VerifyLambdaInvokedAsync(cwl, fnName);
                    }
                    finally
                    {
                        try { await cwl.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = logGroupName }); } catch { }
                    }
                }
                finally
                {
                    try { await lambda.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = fnName }); } catch { }
                }
            }
            finally
            {
                await IAMDeleteRoleAsync(endpoint, roleName);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "CWLogs_Kinesis", async () =>
        {
            var streamName = $"integ-cwl-kin-{ts}";
            var logGroupName = $"/integ/cwl-kinesis/{ts}";

            await kinesis.CreateStreamAsync(new CreateStreamRequest { StreamName = streamName, ShardCount = 1 });
            try
            {
                await Task.Delay(1000);
                var streamARN = $"arn:aws:kinesis:us-east-1:000000000000:stream/{streamName}";
                await cwl.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = logGroupName });
                try
                {
                    await cwl.CreateLogStreamAsync(new CreateLogStreamRequest
                    {
                        LogGroupName = logGroupName,
                        LogStreamName = "test-stream"
                    });
                    await cwl.PutSubscriptionFilterAsync(new PutSubscriptionFilterRequest
                    {
                        LogGroupName = logGroupName,
                        FilterName = "integ-kinesis-sub",
                        FilterPattern = "[...]",
                        DestinationArn = streamARN
                    });
                    await cwl.PutLogEventsAsync(new PutLogEventsRequest
                    {
                        LogGroupName = logGroupName,
                        LogStreamName = "test-stream",
                        LogEvents = [new InputLogEvent { Message = "kinesis subscription test", Timestamp = DateTimeOffset.UtcNow.UtcDateTime }]
                    });
                    await Task.Delay(3000);
                    var descResp = await kinesis.DescribeStreamAsync(new DescribeStreamRequest { StreamName = streamName });
                    if (descResp.StreamDescription.Shards == null || descResp.StreamDescription.Shards.Count == 0)
                        throw new Exception("no shards in stream");
                    var shardID = descResp.StreamDescription.Shards[0].ShardId;
                    var iterResp = await kinesis.GetShardIteratorAsync(new GetShardIteratorRequest
                    {
                        StreamName = streamName,
                        ShardId = shardID,
                        ShardIteratorType = Amazon.Kinesis.ShardIteratorType.TRIM_HORIZON
                    });
                    var recordsResp = await kinesis.GetRecordsAsync(new GetRecordsRequest { ShardIterator = iterResp.ShardIterator });
                    if (recordsResp.Records == null || recordsResp.Records.Count == 0)
                        throw new Exception("expected records from CW Logs subscription, got 0");
                    using var reader = new StreamReader(recordsResp.Records[0].Data);
                    var data = reader.ReadToEnd();
                    if (!data.Contains("awslogs"))
                        throw new Exception($"expected awslogs envelope, got: {data}");
                }
                finally
                {
                    try { await cwl.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = logGroupName }); } catch { }
                }
            }
            finally
            {
                try { await kinesis.DeleteStreamAsync(new DeleteStreamRequest { StreamName = streamName }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "SNS_SQS", async () =>
        {
            var topicName = $"integ-sns-sqs-t-{ts}";
            var queueName = $"integ-sns-sqs-q-{ts}";

            var topicARN = await CreateTopicAsync(sns, topicName);
            try
            {
                var queueUrl = await CreateQueueAsync(sqs, queueName);
                try
                {
                    await sns.SubscribeAsync(new SubscribeRequest
                    {
                        TopicArn = topicARN,
                        Protocol = "sqs",
                        Endpoint = $"arn:aws:sqs:us-east-1:000000000000:{queueName}"
                    });
                    await sns.PublishAsync(new PublishRequest
                    {
                        TopicArn = topicARN,
                        Message = "{\"test\":\"sns-to-sqs\"}",
                        MessageAttributes = new Dictionary<string, Amazon.SimpleNotificationService.Model.MessageAttributeValue>
                        {
                            { "TestType", new Amazon.SimpleNotificationService.Model.MessageAttributeValue { DataType = "String", StringValue = "integration" } }
                        }
                    });
                    await Task.Delay(2000);
                    var msgs = await ReceiveMessagesAsync(sqs, queueUrl, 5, 3);
                    if (msgs.Count == 0)
                        throw new Exception("expected message from SNS in queue, got 0");
                }
                finally
                {
                    await DeleteQueueAsync(sqs, queueUrl);
                }
            }
            finally
            {
                await DeleteTopicAsync(sns, topicARN);
            }
        }));

        results.Add(await runner.RunTestAsync(IntegSvc, "SNS_Lambda", async () =>
        {
            var topicName = $"integ-sns-lambda-t-{ts}";
            var fnName = $"integ-sns-lambda-fn-{ts}";
            var roleName = $"integ-sns-lambda-role-{ts}";

            await IAMCreateRoleAsync(endpoint, roleName, IamHelpers.LambdaTrustPolicy);
            try
            {
                var fnARN = await CreateLambdaAsync(lambda, fnName, roleName);
                try
                {
                    var topicARN = await CreateTopicAsync(sns, topicName);
                    try
                    {
                        await sns.SubscribeAsync(new SubscribeRequest
                        {
                            TopicArn = topicARN,
                            Protocol = "lambda",
                            Endpoint = fnARN
                        });
                        await sns.PublishAsync(new PublishRequest
                        {
                            TopicArn = topicARN,
                            Message = "{\"test\":\"sns-to-lambda\"}"
                        });
                        await Task.Delay(5000);
                        await VerifyLambdaInvokedAsync(cwl, fnName);
                    }
                    finally
                    {
                        await DeleteTopicAsync(sns, topicARN);
                    }
                }
                finally
                {
                    try { await lambda.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = fnName }); } catch { }
                }
            }
            finally
            {
                await IAMDeleteRoleAsync(endpoint, roleName);
            }
        }));

        return results;
    }
}
