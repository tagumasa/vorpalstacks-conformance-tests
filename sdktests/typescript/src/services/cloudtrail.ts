import {
  CloudTrailClient,
  ListTrailsCommand,
  CreateTrailCommand,
  GetTrailCommand,
  DescribeTrailsCommand,
  StartLoggingCommand,
  StopLoggingCommand,
  GetTrailStatusCommand,
  UpdateTrailCommand,
  GetEventSelectorsCommand,
  PutEventSelectorsCommand,
  AddTagsCommand,
  ListTagsCommand,
  RemoveTagsCommand,
  LookupEventsCommand,
  DeleteTrailCommand,
} from "@aws-sdk/client-cloudtrail";
import { TestRunner, TestResult } from "../runner";

export async function runCloudTrailTests(
  runner: TestRunner,
  client: CloudTrailClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const trailName = `test-trail-${Date.now()}`;

  results.push(await runner.runTest("cloudtrail", "ListTrails", async () => {
    await client.send(new ListTrailsCommand({}));
  }));

  results.push(await runner.runTest("cloudtrail", "CreateTrail", async () => {
    await client.send(new CreateTrailCommand({
      Name: trailName,
      S3BucketName: "test-bucket",
      IncludeGlobalServiceEvents: true,
      IsMultiRegionTrail: true,
    }));
  }));

  results.push(await runner.runTest("cloudtrail", "GetTrail", async () => {
    await client.send(new GetTrailCommand({ Name: trailName }));
  }));

  results.push(await runner.runTest("cloudtrail", "DescribeTrails", async () => {
    await client.send(new DescribeTrailsCommand({ trailNameList: [trailName] }));
  }));

  results.push(await runner.runTest("cloudtrail", "StartLogging", async () => {
    await client.send(new StartLoggingCommand({ Name: trailName }));
  }));

  results.push(await runner.runTest("cloudtrail", "StopLogging", async () => {
    await client.send(new StopLoggingCommand({ Name: trailName }));
  }));

  results.push(await runner.runTest("cloudtrail", "GetTrailStatus", async () => {
    await client.send(new GetTrailStatusCommand({ Name: trailName }));
  }));

  results.push(await runner.runTest("cloudtrail", "UpdateTrail", async () => {
    await client.send(new UpdateTrailCommand({
      Name: trailName,
      S3BucketName: "updated-bucket",
    }));
  }));

  results.push(await runner.runTest("cloudtrail", "GetEventSelectors", async () => {
    await client.send(new GetEventSelectorsCommand({ TrailName: trailName }));
  }));

  results.push(await runner.runTest("cloudtrail", "PutEventSelectors", async () => {
    await client.send(new PutEventSelectorsCommand({
      TrailName: trailName,
      EventSelectors: [
        {
          ReadWriteType: "All",
          IncludeManagementEvents: true,
        },
      ],
    }));
  }));

  let trailARN = "";
  const getTrailResp = await client.send(new GetTrailCommand({ Name: trailName }));
  if (getTrailResp.Trail?.TrailARN) {
    trailARN = getTrailResp.Trail.TrailARN;
  }
  const resourceID = trailARN || trailName;

  results.push(await runner.runTest("cloudtrail", "AddTags", async () => {
    await client.send(new AddTagsCommand({
      ResourceId: resourceID,
      TagsList: [
        { Key: "Environment", Value: "test" },
        { Key: "Owner", Value: "test-user" },
      ],
    }));
  }));

  results.push(await runner.runTest("cloudtrail", "ListTags", async () => {
    await client.send(new ListTagsCommand({
      ResourceIdList: [resourceID],
    }));
  }));

  results.push(await runner.runTest("cloudtrail", "RemoveTags", async () => {
    await client.send(new RemoveTagsCommand({
      ResourceId: resourceID,
      TagsList: [{ Key: "Environment" }],
    }));
  }));

  results.push(await runner.runTest("cloudtrail", "LookupEvents", async () => {
    await client.send(new LookupEventsCommand({ MaxResults: 10 }));
  }));

  results.push(await runner.runTest("cloudtrail", "DeleteTrail", async () => {
    await client.send(new DeleteTrailCommand({ Name: trailName }));
  }));

  results.push(await runner.runTest("cloudtrail", "GetTrail_NonExistent", async () => {
    try {
      await client.send(new GetTrailCommand({ Name: "nonexistent-trail-xyz" }));
      throw new Error("expected error for non-existent trail");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for non-existent trail") {
        throw err;
      }
    }
  }));

  results.push(await runner.runTest("cloudtrail", "DeleteTrail_NonExistent", async () => {
    try {
      await client.send(new DeleteTrailCommand({ Name: "nonexistent-trail-xyz" }));
      throw new Error("expected error for non-existent trail");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for non-existent trail") {
        throw err;
      }
    }
  }));

  results.push(await runner.runTest("cloudtrail", "StartLogging_NonExistent", async () => {
    try {
      await client.send(new StartLoggingCommand({ Name: "nonexistent-trail-xyz" }));
      throw new Error("expected error for non-existent trail");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for non-existent trail") {
        throw err;
      }
    }
  }));

  results.push(await runner.runTest("cloudtrail", "DescribeTrails_NonExistent", async () => {
    const resp = await client.send(new DescribeTrailsCommand({
      trailNameList: ["nonexistent-trail-xyz"],
    }));
    if (resp.trailList && resp.trailList.length !== 0) {
      throw new Error(`expected empty trail list, got ${resp.trailList.length}`);
    }
  }));

  const verifyTrailName = `verify-trail-${Date.now()}`;
  results.push(await runner.runTest("cloudtrail", "CreateTrail_ContentVerify", async () => {
    const resp = await client.send(new CreateTrailCommand({
      Name: verifyTrailName,
      S3BucketName: "verify-bucket",
      IncludeGlobalServiceEvents: true,
      IsMultiRegionTrail: false,
    }));
    if (resp.Name !== verifyTrailName) {
      throw new Error("trail name mismatch");
    }
    if (resp.S3BucketName !== "verify-bucket") {
      throw new Error("S3 bucket name mismatch");
    }
  }));

  results.push(await runner.runTest("cloudtrail", "UpdateTrail_VerifyChange", async () => {
    await client.send(new UpdateTrailCommand({
      Name: verifyTrailName,
      S3BucketName: "updated-verify-bucket",
    }));
    const getResp = await client.send(new GetTrailCommand({ Name: verifyTrailName }));
    if (getResp.Trail?.S3BucketName !== "updated-verify-bucket") {
      throw new Error(`S3 bucket name not updated, got ${getResp.Trail?.S3BucketName}`);
    }
  }));

  results.push(await runner.runTest("cloudtrail", "PutEventSelectors_VerifyContent", async () => {
    await client.send(new PutEventSelectorsCommand({
      TrailName: verifyTrailName,
      EventSelectors: [
        {
          ReadWriteType: "ReadOnly",
          IncludeManagementEvents: false,
          DataResources: [
            {
              Type: "AWS::S3::Object",
              Values: ["arn:aws:s3:::"],
            },
          ],
        },
      ],
    }));
    const getResp = await client.send(new GetEventSelectorsCommand({ TrailName: verifyTrailName }));
    if (getResp.EventSelectors?.length !== 1) {
      throw new Error(`expected 1 event selector, got ${getResp.EventSelectors?.length}`);
    }
    if (getResp.EventSelectors?.[0].ReadWriteType !== "ReadOnly") {
      throw new Error(`ReadWriteType mismatch, got ${getResp.EventSelectors?.[0].ReadWriteType}`);
    }
  }));

  try {
    await client.send(new DeleteTrailCommand({ Name: verifyTrailName }));
  } catch {
    // ignore cleanup errors
  }

  return results;
}
