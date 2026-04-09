# VorpalStacks Conformance Tests

Conformance tests for the [VorpalStacks](https://github.com/tagumasa/vorpalstacks) mock AWS server. These tests validate that the server correctly implements AWS API behaviour across multiple SDK languages and IaC tools.

## Test Suites

### SDK Tests (`sdktests/`)

| Language | SDK | Tests | Status |
|----------|-----|-------|--------|
| Go | aws-sdk-go-v2 | 2003 | Baseline (source of truth) |
| Python | boto3 | 631 | |
| TypeScript | @aws-sdk/client-* | 629 | |
| C# | AWSSDK.* v4 | 2019 | |

### IaC Tests (`iactests/`)

| Tool | Provider | Services | Status |
|------|----------|----------|--------|
| Terraform | hashicorp/aws ~> 6.0 | 28 | |
| OpenTofu | opentofu/aws ~> 6.0 | 28 | |

## Prerequisites

A running VorpalStacks server instance is required. Start the server before running any tests:

```bash
SIGNATURE_VERIFICATION_ENABLED=false PORT=8080 DATA_PATH=./tmp/testdata TEST_MODE=true ./vorpalstacks
```

Environment variables (defaults shown):

| Variable | Default | Description |
|----------|---------|-------------|
| `AWS_ENDPOINT_URL` | `http://localhost:8080` | VorpalStacks server endpoint |
| `AWS_ACCESS_KEY_ID` | `test` | Access key (ignored in TEST_MODE) |
| `AWS_SECRET_ACCESS_KEY` | `test` | Secret key (ignored in TEST_MODE) |
| `AWS_REGION` | `us-east-1` | Default region |

## Running SDK Tests

### Python

```bash
cd sdktests/python
python3 -m venv venv
source venv/bin/activate
pip install -e .
python3 -m conformance
```

### TypeScript

```bash
cd sdktests/typescript
npm install
npx tsc
npm run test
```

### C#

```bash
cd sdktests/cs/src
dotnet run --project VorpalStacks.SDK.Tests.csproj
```

## Running IaC Tests

### Terraform

```bash
cd iactests/terraform/tests/<service>
terraform init
terraform apply -auto-approve
terraform destroy -auto-approve
```

### OpenTofu

```bash
cd iactests/opentofu/tests/<service>
tofu init
tofu apply -auto-approve
tofu destroy -auto-approve
```

## Project Structure

```
vorpalstacks-conformance-tests/
├── sdktests/
│   ├── python/
│   │   ├── pyproject.toml
│   │   ├── venv/              # Python virtual environment (gitignored)
│   │   └── src/conformance/
│   │       ├── __main__.py    # Entry point
│   │       ├── runner.py      # Server endpoint & AWS credentials
│   │       └── services/      # 26 service test modules
│   ├── typescript/
│   │   ├── package.json
│   │   ├── tsconfig.json
│   │   ├── node_modules/      # npm dependencies (gitignored)
│   │   ├── dist/              # Compiled JS (gitignored)
│   │   └── src/
│   │       ├── index.ts       # Entry point
│   │       ├── runner.ts      # Test runner
│   │       └── services/      # 26 service test modules
│   └── cs/
│       └── src/
│           ├── VorpalStacks.SDK.Tests.csproj
│           ├── Program.cs     # Entry point
│           ├── TestRunner.cs  # Server endpoint & AWS credentials
│           └── Services/      # 26 service test modules
├── iactests/
│   ├── terraform/
│   │   └── tests/             # 28 service modules (hashicorp/aws)
│   └── opentofu/
│       └── tests/             # 28 service modules (opentofu/aws)
├── LICENSE
└── README.md
```

## Services Tested

ACM, API Gateway, Athena, CloudFront, CloudTrail, CloudWatch, CloudWatch Logs, Cognito Identity, Cognito Identity Provider, DynamoDB, EventBridge, IAM, Kinesis, KMS, Lambda, Route 53, S3, Scheduler, Secrets Manager, SESv2, SNS, SQS, SSM, Step Functions, STS, Timestream (Write), WAF, WAFv2

## Conventions

- All tests use unique resource names (timestamp + random/UUID) to allow safe concurrent execution across languages
- Resource prefixes: Python `py-`, TypeScript `ts-`, C# `cs-`, Go (no prefix)
- Each test cleans up its own resources in a `finally` block
- Go SDK tests are the authoritative baseline; all other languages must match or exceed Go coverage
- Tests must never be weakened to hide server bugs — fix the server instead
