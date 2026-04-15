import argparse
import os
import subprocess
import sys


SERVICE_DIRS = {
    "acm": "services/acm",
    "apigateway": "services/apigateway",
    "athena": "services/athena",
    "cloudfront": "services/cloudfront",
    "cloudtrail": "services/cloudtrail",
    "cloudwatch": "services/cloudwatch",
    "cloudwatchlogs": "services/cloudwatchlogs",
    "cognito": "services/cognito",
    "dynamodb": "services/dynamodb",
    "eventbridge": "services/eventbridge",
    "iam": "services/iam",
    "kinesis": "services/kinesis",
    "kms": "services/kms",
    "lambda": "services/lambda",
    "route53": "services/route53",
    "s3": "services/s3",
    "scheduler": "services/scheduler",
    "secretsmanager": "services/secretsmanager",
    "sesv2": "services/sesv2",
    "sns": "services/sns",
    "sqs": "services/sqs",
    "ssm": "services/ssm",
    "sts": "services/sts",
    "sfn": "services/stepfunctions",
    "timestream": "services/timestream",
    "waf": "services/waf",
}


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
    parser.add_argument("-v", "--verbose", action="store_true", help="Verbose output")
    parser.add_argument(
        "-f",
        "--format",
        default="table",
        choices=["table", "json"],
        help="Output format (pytest uses its own, this sets -v/-q)",
    )

    args = parser.parse_args()

    os.environ.setdefault("AWS_ENDPOINT_URL", args.endpoint)
    os.environ.setdefault("AWS_REGION", args.region)

    services_arg = args.service or "all"
    test_dirs = []
    if services_arg == "all":
        test_dirs = list(SERVICE_DIRS.values())
    else:
        for svc in services_arg.split(","):
            svc = svc.strip()
            if svc in SERVICE_DIRS:
                test_dirs.append(SERVICE_DIRS[svc])
            else:
                print(f"Unknown service: {svc}")
                sys.exit(1)

    cmd = [sys.executable, "-m", "pytest"]
    if args.verbose or args.format == "table":
        cmd.append("-v")
    elif args.format == "json":
        cmd.append("-q")
    cmd.extend(test_dirs)

    result = subprocess.run(cmd)
    sys.exit(result.returncode)


if __name__ == "__main__":
    main()
