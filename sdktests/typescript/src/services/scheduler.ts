import {
  SchedulerClient,
  CreateScheduleCommand,
  GetScheduleCommand,
  ListSchedulesCommand,
  UpdateScheduleCommand,
  TagResourceCommand,
  ListTagsForResourceCommand,
  UntagResourceCommand,
  DeleteScheduleCommand,
} from "@aws-sdk/client-scheduler";
import { IAMClient } from "@aws-sdk/client-iam";
import { STSClient } from "@aws-sdk/client-sts";
import { TestRunner, TestResult } from "../runner";

async function createSchedulerRole(iamClient: IAMClient, stsClient: STSClient, region: string): Promise<string> {
  const accountId = "123456789012";
  const roleName = `scheduler-test-role-${Date.now()}`;
  const trustPolicy = {
    Version: "2012-10-17",
    Statement: [{
      Effect: "Allow",
      Principal: { Service: "scheduler.amazonaws.com" },
      Action: "sts:AssumeRole",
    }],
  };
  await iamClient.send(new (await import("@aws-sdk/client-iam")).CreateRoleCommand({
    RoleName: roleName,
    AssumeRolePolicyDocument: JSON.stringify(trustPolicy),
  }));
  const roleArn = `arn:aws:iam::${accountId}:role/${roleName}`;
  return roleArn;
}

async function deleteSchedulerRole(iamClient: IAMClient, roleName: string): Promise<void> {
  try {
    await iamClient.send(new (await import("@aws-sdk/client-iam")).DeleteRoleCommand({
      RoleName: roleName,
    }));
  } catch {
    // ignore cleanup errors
  }
}

export async function runSchedulerTests(
  runner: TestRunner,
  client: SchedulerClient,
  iamClient: IAMClient,
  stsClient: STSClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const scheduleName = `test-schedule-${Date.now()}`;
  const targetArn = `arn:aws:lambda:us-east-1:123456789012:function:test-function`;

  let roleArn = "";
  try {
    const accountId = "123456789012";
    const roleName = `scheduler-test-role-${Date.now()}`;
    const trustPolicy = {
      Version: "2012-10-17",
      Statement: [{
        Effect: "Allow",
        Principal: { Service: "scheduler.amazonaws.com" },
        Action: "sts:AssumeRole",
      }],
    };
    await iamClient.send(new (await import("@aws-sdk/client-iam")).CreateRoleCommand({
      RoleName: roleName,
      AssumeRolePolicyDocument: JSON.stringify(trustPolicy),
    }));
    roleArn = `arn:aws:iam::${accountId}:role/${roleName}`;

    results.push(await runner.runTest("scheduler", "CreateSchedule", async () => {
      await client.send(new CreateScheduleCommand({
        Name: scheduleName,
        ScheduleExpression: "rate(1 day)",
        FlexibleTimeWindow: {
          Mode: "OFF",
        },
        Target: {
          Arn: roleArn,
          RoleArn: roleArn,
        },
      }));
    }));

    results.push(await runner.runTest("scheduler", "GetSchedule", async () => {
      await client.send(new GetScheduleCommand({
        Name: scheduleName,
      }));
    }));

    results.push(await runner.runTest("scheduler", "ListSchedules", async () => {
      await client.send(new ListSchedulesCommand({}));
    }));

    results.push(await runner.runTest("scheduler", "UpdateSchedule", async () => {
      await client.send(new UpdateScheduleCommand({
        Name: scheduleName,
        ScheduleExpression: "rate(2 days)",
        FlexibleTimeWindow: {
          Mode: "OFF",
        },
        Target: {
          Arn: roleArn,
          RoleArn: roleArn,
        },
      }));
    }));

    let scheduleArn = "";
    const getSchedResp = await client.send(new GetScheduleCommand({ Name: scheduleName }));
    if (getSchedResp.Arn) {
      scheduleArn = getSchedResp.Arn;
    }

    results.push(await runner.runTest("scheduler", "TagResource", async () => {
      await client.send(new TagResourceCommand({
        ResourceArn: scheduleArn || `arn:aws:scheduler:us-east-1:123456789012:schedule/default/${scheduleName}`,
        Tags: [
          { Key: "Environment", Value: "test" },
          { Key: "Owner", Value: "test-user" },
        ],
      }));
    }));

    results.push(await runner.runTest("scheduler", "ListTagsForResource", async () => {
      await client.send(new ListTagsForResourceCommand({
        ResourceArn: scheduleArn || `arn:aws:scheduler:us-east-1:123456789012:schedule/default/${scheduleName}`,
      }));
    }));

    results.push(await runner.runTest("scheduler", "UntagResource", async () => {
      await client.send(new UntagResourceCommand({
        ResourceArn: scheduleArn || `arn:aws:scheduler:us-east-1:123456789012:schedule/default/${scheduleName}`,
        TagKeys: ["Environment"],
      }));
    }));

    results.push(await runner.runTest("scheduler", "DeleteSchedule", async () => {
      await client.send(new DeleteScheduleCommand({
        Name: scheduleName,
      }));
    }));

    results.push(await runner.runTest("scheduler", "GetSchedule_NonExistent", async () => {
      try {
        await client.send(new GetScheduleCommand({
          Name: "nonexistent-schedule-xyz",
        }));
        throw new Error("expected error for non-existent schedule");
      } catch (err: unknown) {
        if (err instanceof Error && err.message === "expected error for non-existent schedule") {
          throw err;
        }
      }
    }));

    results.push(await runner.runTest("scheduler", "DeleteSchedule_NonExistent", async () => {
      try {
        await client.send(new DeleteScheduleCommand({
          Name: "nonexistent-schedule-xyz",
        }));
        throw new Error("expected error for non-existent schedule");
      } catch (err: unknown) {
        if (err instanceof Error && err.message === "expected error for non-existent schedule") {
          throw err;
        }
      }
    }));

    const dupName = `dup-schedule-${Date.now()}`;
    results.push(await runner.runTest("scheduler", "CreateSchedule_DuplicateName", async () => {
await client.send(new CreateScheduleCommand({
          Name: dupName,
          ScheduleExpression: "rate(1 day)",
          FlexibleTimeWindow: {
            Mode: "OFF",
          },
          Target: {
            Arn: roleArn,
            RoleArn: roleArn,
          },
        }));
        try {
          await client.send(new CreateScheduleCommand({
            Name: dupName,
            ScheduleExpression: "rate(1 day)",
            FlexibleTimeWindow: {
              Mode: "OFF",
            },
            Target: {
              Arn: roleArn,
              RoleArn: roleArn,
            },
          }));
        throw new Error("expected error for duplicate schedule name");
      } catch (err: unknown) {
        if (err instanceof Error && err.message === "expected error for duplicate schedule name") {
          throw err;
        }
      } finally {
        try {
          await client.send(new DeleteScheduleCommand({ Name: dupName }));
        } catch {
          // ignore cleanup
        }
      }
    }));

    const verifyName = `verify-schedule-${Date.now()}`;
    results.push(await runner.runTest("scheduler", "UpdateSchedule_VerifyExpression", async () => {
      await client.send(new CreateScheduleCommand({
        Name: verifyName,
        ScheduleExpression: "rate(1 day)",
        FlexibleTimeWindow: {
          Mode: "OFF",
        },
        Target: {
          Arn: roleArn,
          RoleArn: roleArn,
        },
      }));
      await client.send(new UpdateScheduleCommand({
        Name: verifyName,
        ScheduleExpression: "rate(3 days)",
        FlexibleTimeWindow: {
          Mode: "OFF",
        },
        Target: {
          Arn: roleArn,
          RoleArn: roleArn,
        },
      }));
      const getResp = await client.send(new GetScheduleCommand({ Name: verifyName }));
      if (!getResp.ScheduleExpression?.includes("3 days")) {
        throw new Error(`schedule expression not updated, got ${getResp.ScheduleExpression}`);
      }
      await client.send(new DeleteScheduleCommand({ Name: verifyName }));
    }));

  } finally {
    const roleName = `scheduler-test-role-${Date.now()}`;
    await deleteSchedulerRole(iamClient, roleName);
  }

  return results;
}