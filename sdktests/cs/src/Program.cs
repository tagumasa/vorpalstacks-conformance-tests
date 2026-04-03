using VorpalStacks.SDK.Tests;
using VorpalStacks.SDK.Tests.Services;

var endpoint = "http://localhost:8080";
var region = "us-east-1";
var service = "all";
var format = "table";
var verbose = false;

if (args.Length > 0)
{
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-e":
            case "--endpoint":
                if (i + 1 < args.Length) endpoint = args[++i];
                break;
            case "-r":
            case "--region":
                if (i + 1 < args.Length) region = args[++i];
                break;
            case "-s":
            case "--service":
                if (i + 1 < args.Length) service = args[++i];
                break;
            case "-f":
            case "--format":
                if (i + 1 < args.Length) format = args[++i];
                break;
            case "-v":
            case "--verbose":
                verbose = true;
                break;
            case "-h":
            case "--help":
                Console.WriteLine("Usage: dotnet run -- [-e endpoint] [-r region] [-s service] [-f format] [-v]");
                Console.WriteLine("  -e, --endpoint    VorpalStacks endpoint (default: http://localhost:8080)");
                Console.WriteLine("  -r, --region      AWS region (default: us-east-1)");
                Console.WriteLine("  -s, --service     Service to test or 'all' (default: all)");
                Console.WriteLine("  -f, --format      Output format: table, json (default: table)");
                Console.WriteLine("  -v, --verbose     Verbose output");
                return;
        }
    }
}

var runner = new TestRunner(endpoint, region, verbose);
var results = new List<TestResult>();

Console.WriteLine($"VorpalStacks SDK Tests");
Console.WriteLine($"Endpoint: {endpoint}");
Console.WriteLine($"Region: {region}");
Console.WriteLine($"Service: {service}");
Console.WriteLine();

var credentials = runner.CreateCredentials();

// Lambda
if (service == "all" || service == "lambda")
{
    var lambdaClient = new Amazon.Lambda.AmazonLambdaClient(credentials, runner.CreateLambdaConfig());
    var iamClient = new Amazon.IdentityManagement.AmazonIdentityManagementServiceClient(credentials, runner.CreateIAMConfig());
    results.AddRange(await LambdaServiceTests.RunTests(runner, lambdaClient, iamClient, region));
}

// SQS
if (service == "all" || service == "sqs")
{
    var sqsClient = new Amazon.SQS.AmazonSQSClient(credentials, runner.CreateSQSConfig());
    results.AddRange(await SQSServiceTests.RunTests(runner, sqsClient, region));
}

// SNS
if (service == "all" || service == "sns")
{
    var snsClient = new Amazon.SimpleNotificationService.AmazonSimpleNotificationServiceClient(credentials, runner.CreateSNSConfig());
    results.AddRange(await SNSServiceTests.RunTests(runner, snsClient, region));
}

// DynamoDB
if (service == "all" || service == "dynamodb")
{
    var dynamoClient = new Amazon.DynamoDBv2.AmazonDynamoDBClient(credentials, runner.CreateDynamoDBConfig());
    results.AddRange(await DynamoDBServiceTests.RunTests(runner, dynamoClient, region));
}

// S3
if (service == "all" || service == "s3")
{
    var s3Client = new Amazon.S3.AmazonS3Client(credentials, runner.CreateS3Config());
    results.AddRange(await S3ServiceTests.RunTests(runner, s3Client, region));
}

// IAM
if (service == "all" || service == "iam")
{
    var iamClient = new Amazon.IdentityManagement.AmazonIdentityManagementServiceClient(credentials, runner.CreateIAMConfig());
    results.AddRange(await IAMServiceTests.RunTests(runner, iamClient, region));
}

// KMS
if (service == "all" || service == "kms")
{
    var kmsClient = new Amazon.KeyManagementService.AmazonKeyManagementServiceClient(credentials, runner.CreateKMSConfig());
    results.AddRange(await KMSServiceTests.RunTests(runner, kmsClient, region));
}

// Cognito
if (service == "all" || service == "cognito")
{
    var cognitoClient = new Amazon.CognitoIdentityProvider.AmazonCognitoIdentityProviderClient(credentials, runner.CreateCognitoConfig());
    results.AddRange(await CognitoServiceTests.RunTests(runner, cognitoClient, region));
}

// EventBridge
if (service == "all" || service == "eventbridge")
{
    var eventBridgeClient = new Amazon.EventBridge.AmazonEventBridgeClient(credentials, runner.CreateEventBridgeConfig());
    results.AddRange(await EventBridgeServiceTests.RunTests(runner, eventBridgeClient, region));
}

// StepFunctions
if (service == "all" || service == "sfn" || service == "stepfunctions")
{
    var sfnClient = new Amazon.StepFunctions.AmazonStepFunctionsClient(credentials, runner.CreateStepFunctionsConfig());
    var sfnIamClient = new Amazon.IdentityManagement.AmazonIdentityManagementServiceClient(credentials, runner.CreateIAMConfig());
    results.AddRange(await StepFunctionsServiceTests.RunTests(runner, sfnClient, sfnIamClient, region));
}

// Kinesis
if (service == "all" || service == "kinesis")
{
    var kinesisClient = new Amazon.Kinesis.AmazonKinesisClient(credentials, runner.CreateKinesisConfig());
    results.AddRange(await KinesisServiceTests.RunTests(runner, kinesisClient, region));
}

// Athena
if (service == "all" || service == "athena")
{
    var athenaClient = new Amazon.Athena.AmazonAthenaClient(credentials, runner.CreateAthenaConfig());
    results.AddRange(await AthenaServiceTests.RunTests(runner, athenaClient, region));
}

// SecretsManager
if (service == "all" || service == "secretsmanager")
{
    var secretsClient = new Amazon.SecretsManager.AmazonSecretsManagerClient(credentials, runner.CreateSecretsManagerConfig());
    results.AddRange(await SecretsManagerServiceTests.RunTests(runner, secretsClient, region));
}

// CloudWatch Logs
if (service == "all" || service == "cloudwatchlogs")
{
    var cwLogsClient = new Amazon.CloudWatchLogs.AmazonCloudWatchLogsClient(credentials, runner.CreateCloudWatchLogsConfig());
    results.AddRange(await CloudWatchLogsServiceTests.RunTests(runner, cwLogsClient, region));
}

// API Gateway
if (service == "all" || service == "apigateway")
{
    var apigatewayClient = new Amazon.APIGateway.AmazonAPIGatewayClient(credentials, runner.CreateAPIGatewayConfig());
    results.AddRange(await APIGatewayServiceTests.RunTests(runner, apigatewayClient, region));
}

// ACM
if (service == "all" || service == "acm")
{
    var acmClient = new Amazon.CertificateManager.AmazonCertificateManagerClient(credentials, runner.CreateACMConfig());
    results.AddRange(await ACMServiceTests.RunTests(runner, acmClient, region));
}

// CloudWatch
if (service == "all" || service == "cloudwatch")
{
    var cwClient = new Amazon.CloudWatch.AmazonCloudWatchClient(credentials, runner.CreateCloudWatchConfig());
    results.AddRange(await CloudWatchServiceTests.RunTests(runner, cwClient, region));
}

// Route53
if (service == "all" || service == "route53")
{
    var route53Client = new Amazon.Route53.AmazonRoute53Client(credentials, runner.CreateRoute53Config());
    results.AddRange(await Route53ServiceTests.RunTests(runner, route53Client, region));
}

// STS
if (service == "all" || service == "sts")
{
    var stsClient = new Amazon.SecurityToken.AmazonSecurityTokenServiceClient(credentials, runner.CreateSTSConfig());
    var stsIamClient = new Amazon.IdentityManagement.AmazonIdentityManagementServiceClient(credentials, runner.CreateIAMConfig());
    results.AddRange(await STSServiceTests.RunTests(runner, stsClient, stsIamClient, region));
}

// CloudFront
if (service == "all" || service == "cloudfront")
{
    var cfClient = new Amazon.CloudFront.AmazonCloudFrontClient(credentials, runner.CreateCloudFrontConfig());
    results.AddRange(await CloudFrontServiceTests.RunTests(runner, cfClient, region));
}

// CloudTrail
if (service == "all" || service == "cloudtrail")
{
    var ctClient = new Amazon.CloudTrail.AmazonCloudTrailClient(credentials, runner.CreateCloudTrailConfig());
    results.AddRange(await CloudTrailServiceTests.RunTests(runner, ctClient, region));
}

// SESv2
if (service == "all" || service == "sesv2" || service == "ses")
{
    var sesv2Client = new Amazon.SimpleEmailV2.AmazonSimpleEmailServiceV2Client(credentials, runner.CreateSESv2Config());
    results.AddRange(await SESv2ServiceTests.RunTests(runner, sesv2Client, region));
}

// SSM
if (service == "all" || service == "ssm")
{
    var ssmClient = new Amazon.SimpleSystemsManagement.AmazonSimpleSystemsManagementClient(credentials, runner.CreateSSMConfig());
    results.AddRange(await SSMServiceTests.RunTests(runner, ssmClient, region));
}

// Scheduler
if (service == "all" || service == "scheduler")
{
    var schedulerClient = new Amazon.Scheduler.AmazonSchedulerClient(credentials, runner.CreateSchedulerConfig());
    var schedIamClient = new Amazon.IdentityManagement.AmazonIdentityManagementServiceClient(credentials, runner.CreateIAMConfig());
    results.AddRange(await SchedulerServiceTests.RunTests(runner, schedulerClient, schedIamClient, region));
}

// WAF
if (service == "all" || service == "waf")
{
    var wafClient = new Amazon.WAFV2.AmazonWAFV2Client(credentials, runner.CreateWAFv2Config());
    results.AddRange(await WAFServiceTests.RunTests(runner, wafClient, region));
}

// Timestream
if (service == "all" || service == "timestream")
{
    var timestreamClient = new Amazon.TimestreamWrite.AmazonTimestreamWriteClient(credentials, runner.CreateTimestreamWriteConfig());
    results.AddRange(await TimestreamServiceTests.RunTests(runner, timestreamClient, region));
}

runner.PrintReport(results, format);
Environment.Exit(results.Any(r => r.Status == "FAIL") ? 1 : 0);
