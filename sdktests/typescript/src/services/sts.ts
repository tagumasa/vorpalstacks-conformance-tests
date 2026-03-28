import {
  STSClient,
  GetCallerIdentityCommand,
  GetSessionTokenCommand,
  AssumeRoleCommand,
} from "@aws-sdk/client-sts";
import {
  IAMClient,
  CreateRoleCommand,
  DeleteRoleCommand,
} from "@aws-sdk/client-iam";
import { TestRunner, TestResult } from "../runner";

export async function runSTSTests(
  runner: TestRunner,
  stsClient: STSClient,
  iamClient: IAMClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const roleName = `TestRole-${Date.now() % 1000000}`;
  const trustPolicy = JSON.stringify({
    Version: "2012-10-17",
    Statement: [{
      Effect: "Allow",
      Principal: { AWS: "arn:aws:iam::000000000000:root" },
      Action: "sts:AssumeRole",
    }],
  });

  try {
    await iamClient.send(new CreateRoleCommand({
      RoleName: roleName,
      AssumeRolePolicyDocument: trustPolicy,
    }));
  } catch (err: unknown) {
    // Role may already exist, continue anyway
  }

  results.push(await runner.runTest("sts", "GetCallerIdentity", async () => {
    const resp = await stsClient.send(new GetCallerIdentityCommand({}));
    if (!resp.UserId) {
      throw new Error("user ID is nil");
    }
  }));

  results.push(await runner.runTest("sts", "GetSessionToken", async () => {
    const resp = await stsClient.send(new GetSessionTokenCommand({}));
    if (!resp.Credentials) {
      throw new Error("credentials is nil");
    }
  }));

  results.push(await runner.runTest("sts", "AssumeRole", async () => {
    const roleARN = `arn:aws:iam::000000000000:role/${roleName}`;
    await stsClient.send(new AssumeRoleCommand({
      RoleArn: roleARN,
      RoleSessionName: "TestSession",
    }));
  }));

  results.push(await runner.runTest("sts", "AssumeRole_NonExistentRole", async () => {
    try {
      await stsClient.send(new AssumeRoleCommand({
        RoleArn: "arn:aws:iam::000000000000:role/NonExistentRole",
        RoleSessionName: "TestSession",
      }));
      throw new Error("expected error for non-existent role");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for non-existent role") {
        throw err;
      }
    }
  }));

  results.push(await runner.runTest("sts", "GetCallerIdentity_ContentVerify", async () => {
    const resp = await stsClient.send(new GetCallerIdentityCommand({}));
    if (!resp.Account || resp.Account === "") {
      throw new Error("account is nil or empty");
    }
    if (!resp.Arn || resp.Arn === "") {
      throw new Error("ARN is nil or empty");
    }
    if (!resp.UserId || resp.UserId === "") {
      throw new Error("user ID is nil or empty");
    }
  }));

  results.push(await runner.runTest("sts", "GetSessionToken_ContentVerify", async () => {
    const resp = await stsClient.send(new GetSessionTokenCommand({
      DurationSeconds: 3600,
    }));
    if (!resp.Credentials) {
      throw new Error("credentials is nil");
    }
    if (!resp.Credentials.AccessKeyId || resp.Credentials.AccessKeyId === "") {
      throw new Error("access key ID is nil or empty");
    }
    if (!resp.Credentials.SecretAccessKey || resp.Credentials.SecretAccessKey === "") {
      throw new Error("secret access key is nil or empty");
    }
    if (!resp.Credentials.SessionToken || resp.Credentials.SessionToken === "") {
      throw new Error("session token is nil or empty");
    }
    if (!resp.Credentials.Expiration) {
      throw new Error("expiration is zero");
    }
  }));

  try {
    await iamClient.send(new DeleteRoleCommand({ RoleName: roleName }));
  } catch {
    // ignore cleanup errors
  }

  return results;
}
