import {
  SSMClient,
  PutParameterCommand,
  GetParameterCommand,
  GetParametersCommand,
  GetParametersByPathCommand,
  DescribeParametersCommand,
  DeleteParameterCommand,
} from "@aws-sdk/client-ssm";
import { TestRunner, TestResult } from "../runner";

export async function runSSMTests(
  runner: TestRunner,
  client: SSMClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const paramName = `/test/param-${Date.now()}`;

  results.push(await runner.runTest("ssm", "PutParameter", async () => {
    await client.send(new PutParameterCommand({
      Name: paramName,
      Value: "test-value",
      Type: "String",
    }));
  }));

  results.push(await runner.runTest("ssm", "GetParameter", async () => {
    await client.send(new GetParameterCommand({
      Name: paramName,
    }));
  }));

  results.push(await runner.runTest("ssm", "GetParameters", async () => {
    await client.send(new GetParametersCommand({
      Names: [paramName],
    }));
  }));

  results.push(await runner.runTest("ssm", "GetParametersByPath", async () => {
    await client.send(new GetParametersByPathCommand({
      Path: "/test",
    }));
  }));

  results.push(await runner.runTest("ssm", "DescribeParameters", async () => {
    await client.send(new DescribeParametersCommand({
      ParameterFilters: [{
        Key: "Path",
        Values: ["/test"],
      }],
    }));
  }));

  results.push(await runner.runTest("ssm", "DeleteParameter", async () => {
    await client.send(new DeleteParameterCommand({
      Name: paramName,
    }));
  }));

  results.push(await runner.runTest("ssm", "GetParameter_NonExistent", async () => {
    try {
      await client.send(new GetParameterCommand({
        Name: "/test/nonexistent-param-xyz",
      }));
      throw new Error("expected error for non-existent parameter");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for non-existent parameter") {
        throw err;
      }
    }
  }));

  results.push(await runner.runTest("ssm", "DeleteParameter_NonExistent", async () => {
    try {
      await client.send(new DeleteParameterCommand({
        Name: "/test/nonexistent-param-xyz",
      }));
      throw new Error("expected error for non-existent parameter");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for non-existent parameter") {
        throw err;
      }
    }
  }));

  results.push(await runner.runTest("ssm", "PutParameter_GetParameter_Roundtrip", async () => {
    const roundtripName = `/test/roundtrip-${Date.now()}`;
    const testValue = "roundtrip-test-value";
    await client.send(new PutParameterCommand({
      Name: roundtripName,
      Value: testValue,
      Type: "String",
    }));
    const getResp = await client.send(new GetParameterCommand({
      Name: roundtripName,
    }));
    if (getResp.Parameter?.Value !== testValue) {
      throw new Error(`value mismatch: expected "${testValue}", got "${getResp.Parameter?.Value}"`);
    }
    await client.send(new DeleteParameterCommand({
      Name: roundtripName,
    }));
  }));

  const invalidName = `/test/invalid-${Date.now()}`;
  results.push(await runner.runTest("ssm", "GetParameters_InvalidNames", async () => {
    const resp = await client.send(new GetParametersCommand({
      Names: [invalidName, paramName],
      WithDecryption: false,
    }));
    if (resp.InvalidParameters && resp.InvalidParameters.length === 0) {
      throw new Error("expected invalid parameters to be reported");
    }
  }));

  results.push(await runner.runTest("ssm", "DescribeParameters_ContainsCreated", async () => {
    const descName = `/test/desc-${Date.now()}`;
    await client.send(new PutParameterCommand({
      Name: descName,
      Value: "describe-test",
      Type: "String",
    }));
    const descResp = await client.send(new DescribeParametersCommand({
      ParameterFilters: [{
        Key: "Name",
        Values: [descName],
      }],
    }));
    const found = descResp.Parameters?.some(p => p.Name === descName);
    if (!found) {
      throw new Error("created parameter not found in describe results");
    }
    await client.send(new DeleteParameterCommand({
      Name: descName,
    }));
  }));

  // MultiByteParameter
  results.push(await runner.runTest("ssm", "MultiByteParameter", async () => {
    const pairs: [string, string][] = [
      ["ja", "日本語テストパラメータ"],
      ["zh", "简体中文测试参数"],
      ["tw", "繁體中文測試參數"],
    ];
    for (const [label, value] of pairs) {
      const name = `/test/multibyte-${label}-${Date.now()}`;
      await client.send(new PutParameterCommand({ Name: name, Value: value, Type: "String" }));
      const resp = await client.send(new GetParameterCommand({ Name: name }));
      if (resp.Parameter?.Value !== value) {
        throw new Error(`Mismatch for ${label}: expected ${value}, got ${resp.Parameter?.Value}`);
      }
      await client.send(new DeleteParameterCommand({ Name: name }));
    }
  }));

  return results;
}