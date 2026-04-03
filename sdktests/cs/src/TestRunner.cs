using System.Diagnostics;
using Amazon;
using Amazon.S3;
using Amazon.Lambda;
using Amazon.DynamoDBv2;
using Amazon.IdentityManagement;
using Amazon.SQS;
using Amazon.SimpleNotificationService;
using Amazon.KeyManagementService;
using Amazon.CognitoIdentityProvider;
using Amazon.EventBridge;
using Amazon.StepFunctions;
using Amazon.Kinesis;
using Amazon.Athena;
using Amazon.SecretsManager;
using Amazon.CloudWatchLogs;
using Amazon.APIGateway;
using Amazon.CertificateManager;
using Amazon.CloudWatch;
using Amazon.Route53;
using Amazon.SecurityToken;
using Amazon.CloudFront;
using Amazon.CloudTrail;
using Amazon.SimpleEmail;
using Amazon.SimpleEmailV2;
using Amazon.SimpleSystemsManagement;
using Amazon.Scheduler;
using Amazon.WAF;
using Amazon.WAFV2;
using Amazon.TimestreamWrite;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests;

public class TestRunner
{
    private readonly string _endpoint;
    private readonly string _region;
    private readonly bool _verbose;

    public static readonly string[] AllServices =
    {
        "acm", "apigateway", "athena", "cloudfront", "cloudtrail",
        "cloudwatch", "cloudwatchlogs", "cognito", "dynamodb", "eventbridge",
        "iam", "kinesis", "kms", "lambda", "route53", "s3",
        "scheduler", "secretsmanager", "sesv2", "sns", "sqs",
        "ssm", "sts", "sfn", "timestream", "waf"
    };

    public TestRunner(string endpoint, string region, bool verbose)
    {
        _endpoint = endpoint;
        _region = region;
        _verbose = verbose;
    }

    public string Endpoint => _endpoint;
    public string Region => _region;

    public AWSCredentials CreateCredentials()
    {
        return new BasicAWSCredentials("test", "test");
    }

    public AmazonS3Config CreateS3Config() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true,
        ForcePathStyle = true,
        AuthenticationServiceName = "s3"
    };

    public AmazonLambdaConfig CreateLambdaConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonDynamoDBConfig CreateDynamoDBConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonIdentityManagementServiceConfig CreateIAMConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonSQSConfig CreateSQSConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonSimpleNotificationServiceConfig CreateSNSConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonKeyManagementServiceConfig CreateKMSConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonCognitoIdentityProviderConfig CreateCognitoConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonEventBridgeConfig CreateEventBridgeConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonStepFunctionsConfig CreateStepFunctionsConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonKinesisConfig CreateKinesisConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonAthenaConfig CreateAthenaConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonSecretsManagerConfig CreateSecretsManagerConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonCloudWatchLogsConfig CreateCloudWatchLogsConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonAPIGatewayConfig CreateAPIGatewayConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonCertificateManagerConfig CreateACMConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonCloudWatchConfig CreateCloudWatchConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonRoute53Config CreateRoute53Config() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonSecurityTokenServiceConfig CreateSTSConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonCloudFrontConfig CreateCloudFrontConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonCloudTrailConfig CreateCloudTrailConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonSimpleEmailServiceV2Config CreateSESv2Config() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonSimpleSystemsManagementConfig CreateSSMConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonSchedulerConfig CreateSchedulerConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonWAFConfig CreateWAFConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonWAFV2Config CreateWAFv2Config() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public AmazonTimestreamWriteConfig CreateTimestreamWriteConfig() => new()
    {
        ServiceURL = _endpoint,
        UseHttp = true
    };

    public TestResult RunTest(string service, string testName, Action testFunc)
    {
        var sw = Stopwatch.StartNew();
        var result = new TestResult
        {
            Service = service,
            TestName = testName,
            Status = "PASS",
            DurationMs = 0
        };

        try
        {
            testFunc();
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            if (_verbose)
            {
                Console.WriteLine($"  [PASS] {service}/{testName}");
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Status = "FAIL";
            result.Error = ex.Message;
            if (_verbose)
            {
                Console.WriteLine($"  [FAIL] {service}/{testName}: {ex.Message}");
            }
        }

        return result;
    }

    public async Task<TestResult> RunTestAsync(string service, string testName, Func<Task> testFunc)
    {
        var sw = Stopwatch.StartNew();
        var result = new TestResult
        {
            Service = service,
            TestName = testName,
            Status = "PASS",
            DurationMs = 0
        };

        try
        {
            await testFunc();
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            if (_verbose)
            {
                Console.WriteLine($"  [PASS] {service}/{testName}");
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Status = "FAIL";
            result.Error = ex.Message;
            if (_verbose)
            {
                Console.WriteLine($"  [FAIL] {service}/{testName}: {ex.Message}");
            }
        }

        return result;
    }

    public void PrintReport(List<TestResult> results, string format)
    {
        if (format == "json")
        {
            Console.WriteLine("[");
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var comma = i < results.Count - 1 ? "," : "";
                Console.WriteLine($"  {{");
                Console.WriteLine($"    \"service\": \"{r.Service}\",");
                Console.WriteLine($"    \"testName\": \"{r.TestName}\",");
                Console.WriteLine($"    \"status\": \"{r.Status}\",");
                Console.WriteLine($"    \"error\": {(r.Error != null ? $"\"{r.Error}\"" : "null")},");
                Console.WriteLine($"    \"duration\": {r.DurationMs}");
                Console.WriteLine($"  }}{comma}");
            }
            Console.WriteLine("]");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("----------------------------------------");
        Console.WriteLine("SERVICE          TEST                              STATUS");
        Console.WriteLine("----------------------------------------");
        foreach (var r in results)
        {
            var svc = r.Service.PadRight(15);
            var name = r.TestName.Length > 30 ? r.TestName[..30] : r.TestName.PadRight(30);
            Console.WriteLine($"{svc} {name} {r.Status}");
        }
        Console.WriteLine("----------------------------------------");

        var passed = results.Count(r => r.Status == "PASS");
        var failed = results.Count(r => r.Status == "FAIL");
        var skipped = results.Count(r => r.Status == "SKIP");
        Console.WriteLine();
        Console.WriteLine($"Total: {results.Count} | Passed: {passed} | Failed: {failed} | Skipped: {skipped}");
    }

    public static string MakeUniqueName(string prefix)
    {
        return $"{prefix}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Random.Shared.Next(100000, 999999)}";
    }
}
