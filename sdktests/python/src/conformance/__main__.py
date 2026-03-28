import argparse
import asyncio
import sys
from .runner import TestRunner
from .services.lambda_service import run_lambda_tests
from .services.dynamodb_service import run_dynamodb_tests
from .services.sqs_service import run_sqs_tests
from .services.sns_service import run_sns_tests
from .services.iam_service import run_iam_tests
from .services.kms_service import run_kms_tests
from .services.cognito_service import run_cognito_tests
from .services.s3_service import run_s3_tests
from .services.eventbridge_service import run_eventbridge_tests
from .services.stepfunctions_service import run_stepfunctions_tests
from .services.kinesis_service import run_kinesis_tests
from .services.athena_service import run_athena_tests
from .services.secretsmanager_service import run_secretsmanager_tests
from .services.cloudwatchlogs_service import run_cloudwatchlogs_tests
from .services.apigateway_service import run_apigateway_tests
from .services.acm_service import run_acm_tests
from .services.cloudwatch_service import run_cloudwatch_tests
from .services.route53_service import run_route53_tests
from .services.sts_service import run_sts_tests
from .services.cloudfront_service import run_cloudfront_tests
from .services.cloudtrail_service import run_cloudtrail_tests
from .services.sesv2_service import run_sesv2_tests
from .services.ssm_service import run_ssm_tests
from .services.scheduler_service import run_scheduler_tests
from .services.waf_service import run_waf_tests
from .services.timestream_service import run_timestream_tests


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Python SDK conformance tests for VorpalStacks"
    )
    parser.add_argument(
        "-e",
        "--endpoint",
        default="http://localhost:8080",
        help="VorpalStacks endpoint",
    )
    parser.add_argument("-r", "--region", default="us-east-1", help="AWS region")
    parser.add_argument(
        "-s", "--service", default="", help="Comma-separated services or 'all'"
    )
    parser.add_argument(
        "-f",
        "--format",
        default="table",
        choices=["table", "json"],
        help="Output format",
    )
    parser.add_argument("-v", "--verbose", action="store_true", help="Verbose output")

    args = parser.parse_args()

    runner = TestRunner(
        endpoint=args.endpoint, region=args.region, verbose=args.verbose
    )
    service = args.service or "all"

    results = []

    async def run_all():
        if service == "all" or service == "lambda":
            lambda_results = await run_lambda_tests(runner, args.endpoint, args.region)
            results.extend(lambda_results)
        if service == "all" or service == "dynamodb":
            dynamodb_results = await run_dynamodb_tests(
                runner, args.endpoint, args.region
            )
            results.extend(dynamodb_results)
        if service == "all" or service == "sqs":
            sqs_results = await run_sqs_tests(runner, args.endpoint, args.region)
            results.extend(sqs_results)
        if service == "all" or service == "sns":
            sns_results = await run_sns_tests(runner, args.endpoint, args.region)
            results.extend(sns_results)
        if service == "all" or service == "iam":
            iam_results = await run_iam_tests(runner, args.endpoint, args.region)
            results.extend(iam_results)
        if service == "all" or service == "kms":
            kms_results = await run_kms_tests(runner, args.endpoint, args.region)
            results.extend(kms_results)
        if service == "all" or service == "cognito":
            cognito_results = await run_cognito_tests(
                runner, args.endpoint, args.region
            )
            results.extend(cognito_results)
        if service == "all" or service == "s3":
            s3_results = await run_s3_tests(runner, args.endpoint, args.region)
            results.extend(s3_results)
        if service == "all" or service == "eventbridge":
            eb_results = await run_eventbridge_tests(runner, args.endpoint, args.region)
            results.extend(eb_results)
        if service == "all" or service == "sfn":
            sfn_results = await run_stepfunctions_tests(
                runner, args.endpoint, args.region
            )
            results.extend(sfn_results)
        if service == "all" or service == "kinesis":
            kinesis_results = await run_kinesis_tests(
                runner, args.endpoint, args.region
            )
            results.extend(kinesis_results)
        if service == "all" or service == "athena":
            athena_results = await run_athena_tests(runner, args.endpoint, args.region)
            results.extend(athena_results)
        if service == "all" or service == "secretsmanager":
            secrets_results = await run_secretsmanager_tests(
                runner, args.endpoint, args.region
            )
            results.extend(secrets_results)
        if service == "all" or service == "logs":
            logs_results = await run_cloudwatchlogs_tests(
                runner, args.endpoint, args.region
            )
            results.extend(logs_results)
        if service == "all" or service == "apigateway":
            apigateway_results = await run_apigateway_tests(
                runner, args.endpoint, args.region
            )
            results.extend(apigateway_results)
        if service == "all" or service == "acm":
            acm_results = await run_acm_tests(runner, args.endpoint, args.region)
            results.extend(acm_results)
        if service == "all" or service == "cloudwatch":
            cloudwatch_results = await run_cloudwatch_tests(
                runner, args.endpoint, args.region
            )
            results.extend(cloudwatch_results)
        if service == "all" or service == "route53":
            route53_results = await run_route53_tests(
                runner, args.endpoint, args.region
            )
            results.extend(route53_results)
        if service == "all" or service == "sts":
            sts_results = await run_sts_tests(runner, args.endpoint, args.region)
            results.extend(sts_results)
        if service == "all" or service == "cloudfront":
            cloudfront_results = await run_cloudfront_tests(
                runner, args.endpoint, args.region
            )
            results.extend(cloudfront_results)
        if service == "all" or service == "cloudtrail":
            cloudtrail_results = await run_cloudtrail_tests(
                runner, args.endpoint, args.region
            )
            results.extend(cloudtrail_results)
        if service == "all" or service == "sesv2":
            sesv2_results = await run_sesv2_tests(runner, args.endpoint, args.region)
            results.extend(sesv2_results)
        if service == "all" or service == "ssm":
            ssm_results = await run_ssm_tests(runner, args.endpoint, args.region)
            results.extend(ssm_results)
        if service == "all" or service == "scheduler":
            scheduler_results = await run_scheduler_tests(
                runner, args.endpoint, args.region
            )
            results.extend(scheduler_results)
        if service == "all" or service == "waf":
            waf_results = await run_waf_tests(runner, args.endpoint, args.region)
            results.extend(waf_results)
        if service == "all" or service == "timestream":
            timestream_results = await run_timestream_tests(
                runner, args.endpoint, args.region
            )
            results.extend(timestream_results)

    asyncio.run(run_all())
    runner.print_report(results, args.format)

    failed = sum(1 for r in results if r.status == "FAIL")
    sys.exit(1 if failed > 0 else 0)


if __name__ == "__main__":
    main()
