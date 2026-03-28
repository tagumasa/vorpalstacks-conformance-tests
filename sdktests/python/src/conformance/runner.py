from dataclasses import dataclass
from typing import Callable, Awaitable, Any


@dataclass
class TestResult:
    service: str
    test_name: str
    status: str
    error: str | None = None
    duration: int = 0


class TestRunner:
    ALL_SERVICES = [
        "acm",
        "apigateway",
        "athena",
        "cloudfront",
        "cloudtrail",
        "cloudwatch",
        "cloudwatchlogs",
        "cognito",
        "dynamodb",
        "eventbridge",
        "iam",
        "kinesis",
        "kms",
        "lambda",
        "route53",
        "s3",
        "scheduler",
        "secretsmanager",
        "sesv2",
        "sns",
        "sqs",
        "ssm",
        "sts",
        "sfn",
        "timestream",
        "waf",
    ]

    def __init__(self, endpoint: str, region: str, verbose: bool = False):
        self.endpoint = endpoint
        self.region = region
        self.verbose = verbose

    async def run_test(
        self,
        service: str,
        test_name: str,
        fn: Callable[[], Any],
    ) -> TestResult:
        import time

        start = time.time()
        try:
            result = fn()
            if hasattr(result, "__await__"):
                import asyncio

                asyncio.get_event_loop().run_until_complete(result)
            duration_ms = int((time.time() - start) * 1000)
            if self.verbose:
                print(f"  [PASS] {service}/{test_name}")
            return TestResult(
                service=service,
                test_name=test_name,
                status="PASS",
                duration=duration_ms,
            )
        except Exception as err:
            duration_ms = int((time.time() - start) * 1000)
            error_msg = str(err)
            if self.verbose:
                print(f"  [FAIL] {service}/{test_name}: {error_msg}")
            return TestResult(
                service=service,
                test_name=test_name,
                status="FAIL",
                error=error_msg,
                duration=duration_ms,
            )

    def get_all_services(self) -> list[str]:
        return list(self.ALL_SERVICES)

    def print_report(self, results: list[TestResult], format: str = "table") -> None:
        if format == "json":
            import json

            output = [
                {
                    "service": r.service,
                    "testName": r.test_name,
                    "status": r.status,
                    "error": r.error,
                    "duration": r.duration,
                }
                for r in results
            ]
            print(json.dumps(output, indent=2))
            return

        print("\n----------------------------------------")
        print("SERVICE          TEST                              STATUS")
        print("----------------------------------------")
        for r in results:
            svc = r.service.ljust(15)
            name = r.test_name[:30].ljust(30)
            status = r.status
            print(f"{svc} {name} {status}")
        print("----------------------------------------")

        passed = sum(1 for r in results if r.status == "PASS")
        failed = sum(1 for r in results if r.status == "FAIL")
        skipped = sum(1 for r in results if r.status == "SKIP")
        print(
            f"\nTotal: {len(results)} | Passed: {passed} | Failed: {failed} | Skipped: {skipped}"
        )
