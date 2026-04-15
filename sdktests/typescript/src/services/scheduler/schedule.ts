import {
  SchedulerClient,
  CreateScheduleCommand,
  GetScheduleCommand,
  UpdateScheduleCommand,
  DeleteScheduleCommand,
  ListSchedulesCommand,
  TagResourceCommand,
  UntagResourceCommand,
  ListTagsForResourceCommand,
} from '@aws-sdk/client-scheduler';
import { IAMClient } from '@aws-sdk/client-iam';
import type { TestRunner, TestResult, ServiceContext } from '../../runner.js';
import { safeCleanup, createIAMRole, deleteIAMRole } from '../../helpers.js';

const SVC = 'scheduler';
const TRUST_POLICY = JSON.stringify({
  Version: '2012-10-17',
  Statement: [{ Effect: 'Allow', Principal: { Service: 'scheduler.amazonaws.com' }, Action: 'sts:AssumeRole' }],
});

function uniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 99999)}`;
}

export async function runScheduleTests(
  runner: TestRunner,
  ctx: ServiceContext,
): Promise<TestResult[]> {
  const client = new SchedulerClient({
    endpoint: ctx.endpoint,
    region: ctx.region,
    credentials: ctx.credentials,
  });
  const iamClient = new IAMClient({
    endpoint: ctx.endpoint,
    region: ctx.region,
    credentials: ctx.credentials,
  });
  const results: TestResult[] = [];

  const scheduleName = uniqueName('TestSchedule');
  const roleName = uniqueName('TestSchedRole');
  const roleARN = `arn:aws:iam::000000000000:role/${roleName}`;
  const lambdaARN = `arn:aws:lambda:${ctx.region}:000000000000:function:TestFunction`;
  const targetInput = JSON.stringify({ message: 'test message' });

  await createIAMRole(iamClient, roleName, TRUST_POLICY);

  const helperCreateRole = async (rn: string) => { await createIAMRole(iamClient, rn, TRUST_POLICY); };
  const helperDeleteRole = async (rn: string) => { await deleteIAMRole(iamClient, rn); };

  const makeTarget = (roleArn: string, input?: string) => ({
    Arn: lambdaARN,
    RoleArn: roleArn,
    Input: input ?? targetInput,
  });

  const makeFlexibleWindow = () => ({ Mode: 'OFF' as const });

  try {
    results.push(await runner.runTest(SVC, 'CreateSchedule', async () => {
      const resp = await client.send(new CreateScheduleCommand({
        Name: scheduleName,
        ScheduleExpression: 'rate(30 minutes)',
        Target: makeTarget(roleARN),
        FlexibleTimeWindow: makeFlexibleWindow(),
      }));
      if (!resp) throw new Error('expected response');
    }));

    results.push(await runner.runTest(SVC, 'GetSchedule', async () => {
      const resp = await client.send(new GetScheduleCommand({ Name: scheduleName }));
      if (!resp.Name) throw new Error('expected Name to be defined');
    }));

    results.push(await runner.runTest(SVC, 'ListSchedules', async () => {
      const resp = await client.send(new ListSchedulesCommand({}));
      if (!resp.Schedules) throw new Error('expected Schedules to be defined');
    }));

    results.push(await runner.runTest(SVC, 'UpdateSchedule', async () => {
      const resp = await client.send(new UpdateScheduleCommand({
        Name: scheduleName,
        ScheduleExpression: 'rate(60 minutes)',
        Target: makeTarget(roleARN),
        FlexibleTimeWindow: makeFlexibleWindow(),
      }));
      if (!resp) throw new Error('expected response');
    }));

    const scheduleARN = `arn:aws:scheduler:${ctx.region}:000000000000:schedule/${scheduleName}`;
    results.push(await runner.runTest(SVC, 'TagResource', async () => {
      await client.send(new TagResourceCommand({
        ResourceArn: scheduleARN,
        Tags: [{ Key: 'Environment', Value: 'test' }],
      }));
    }));

    results.push(await runner.runTest(SVC, 'ListTagsForResource', async () => {
      const resp = await client.send(new ListTagsForResourceCommand({ ResourceArn: scheduleARN }));
      if (!resp.Tags) throw new Error('expected Tags to be defined');
    }));

    results.push(await runner.runTest(SVC, 'UntagResource', async () => {
      await client.send(new UntagResourceCommand({
        ResourceArn: scheduleARN,
        TagKeys: ['Environment'],
      }));
    }));

    results.push(await runner.runTest(SVC, 'GetSchedule_ContentVerify', async () => {
      const resp = await client.send(new GetScheduleCommand({ Name: scheduleName }));
      if (resp.Name !== scheduleName) throw new Error(`name mismatch: ${resp.Name}`);
      if (resp.ScheduleExpression !== 'rate(60 minutes)') {
        throw new Error(`expression mismatch: ${resp.ScheduleExpression}`);
      }
      if (!resp.Arn) throw new Error('expected Arn to be defined');
      if (!resp.CreationDate) throw new Error('expected CreationDate to be defined');
      if (!resp.LastModificationDate) throw new Error('expected LastModificationDate to be defined');
      if (!resp.Target) throw new Error('expected Target to be defined');
      if (resp.FlexibleTimeWindow?.Mode !== 'OFF') throw new Error('FlexibleTimeWindow mode mismatch');
    }));

    results.push(await runner.runTest(SVC, 'DeleteSchedule', async () => {
      await client.send(new DeleteScheduleCommand({ Name: scheduleName }));
    }));

    results.push(await runner.runTest(SVC, 'GetSchedule_NonExistent', async () => {
      let err: unknown;
      try {
        await client.send(new GetScheduleCommand({ Name: 'nonexistent-schedule-xyz' }));
      } catch (e) { err = e; }
      const name = (err as { name?: string })?.name ?? '';
      const msg = err instanceof Error ? err.message : '';
      if (!name.includes('ResourceNotFoundException') && !msg.includes('ResourceNotFoundException')) {
        throw new Error(`expected ResourceNotFoundException, got: ${msg || name || 'no error'}`);
      }
    }));

    results.push(await runner.runTest(SVC, 'DeleteSchedule_NonExistent', async () => {
      let err: unknown;
      try {
        await client.send(new DeleteScheduleCommand({ Name: 'nonexistent-schedule-xyz' }));
      } catch (e) { err = e; }
      const name = (err as { name?: string })?.name ?? '';
      const msg = err instanceof Error ? err.message : '';
      if (!name.includes('ResourceNotFoundException') && !msg.includes('ResourceNotFoundException')) {
        throw new Error(`expected ResourceNotFoundException, got: ${msg || name || 'no error'}`);
      }
    }));

    results.push(await runner.runTest(SVC, 'CreateSchedule_DuplicateName', async () => {
      const dupName = uniqueName('DupSchedule');
      const dupRole = uniqueName('DupSchedRole');
      await helperCreateRole(dupRole);
      try {
        const dupRoleARN = `arn:aws:iam::000000000000:role/${dupRole}`;
        await client.send(new CreateScheduleCommand({
          Name: dupName,
          ScheduleExpression: 'rate(30 minutes)',
          Target: makeTarget(dupRoleARN),
          FlexibleTimeWindow: makeFlexibleWindow(),
        }));
        try {
          let err: unknown;
          try {
            await client.send(new CreateScheduleCommand({
              Name: dupName,
              ScheduleExpression: 'rate(60 minutes)',
              Target: makeTarget(dupRoleARN),
              FlexibleTimeWindow: makeFlexibleWindow(),
            }));
          } catch (e) { err = e; }
          if (!err) throw new Error('expected error for duplicate schedule name');
        } finally {
          await safeCleanup(() => client.send(new DeleteScheduleCommand({ Name: dupName })));
        }
      } finally {
        await helperDeleteRole(dupRole);
      }
    }));

    results.push(await runner.runTest(SVC, 'UpdateSchedule_VerifyExpression', async () => {
      const updName = uniqueName('UpdSchedule');
      const updRole = uniqueName('UpdSchedRole');
      await helperCreateRole(updRole);
      try {
        const updRoleARN = `arn:aws:iam::000000000000:role/${updRole}`;
        await client.send(new CreateScheduleCommand({
          Name: updName,
          ScheduleExpression: 'rate(30 minutes)',
          Target: makeTarget(updRoleARN),
          FlexibleTimeWindow: makeFlexibleWindow(),
        }));
        try {
          const newExpr = 'rate(60 minutes)';
          await client.send(new UpdateScheduleCommand({
            Name: updName,
            ScheduleExpression: newExpr,
            Target: makeTarget(updRoleARN),
            FlexibleTimeWindow: makeFlexibleWindow(),
          }));
          const getResp = await client.send(new GetScheduleCommand({ Name: updName }));
          if (getResp.ScheduleExpression !== newExpr) {
            throw new Error(`expression not updated, got ${getResp.ScheduleExpression}`);
          }
        } finally {
          await safeCleanup(() => client.send(new DeleteScheduleCommand({ Name: updName })));
        }
      } finally {
        await helperDeleteRole(updRole);
      }
    }));

    results.push(await runner.runTest(SVC, 'UpdateSchedule_NonExistent', async () => {
      let err: unknown;
      try {
        await client.send(new UpdateScheduleCommand({
          Name: 'nonexistent-schedule-xyz',
          ScheduleExpression: 'rate(30 minutes)',
          Target: { Arn: lambdaARN, RoleArn: roleARN },
          FlexibleTimeWindow: makeFlexibleWindow(),
        }));
      } catch (e) { err = e; }
      const name = (err as { name?: string })?.name ?? '';
      const msg = err instanceof Error ? err.message : '';
      if (!name.includes('ResourceNotFoundException') && !msg.includes('ResourceNotFoundException')) {
        throw new Error(`expected ResourceNotFoundException, got: ${msg || name || 'no error'}`);
      }
    }));

    results.push(await runner.runTest(SVC, 'UpdateSchedule_StateToggle', async () => {
      const stateName = uniqueName('StateSchedule');
      const stateRole = uniqueName('StateSchedRole');
      await helperCreateRole(stateRole);
      try {
        const stateRoleARN = `arn:aws:iam::000000000000:role/${stateRole}`;
        await client.send(new CreateScheduleCommand({
          Name: stateName,
          ScheduleExpression: 'rate(30 minutes)',
          Target: { Arn: lambdaARN, RoleArn: stateRoleARN },
          FlexibleTimeWindow: makeFlexibleWindow(),
        }));
        try {
          await client.send(new UpdateScheduleCommand({
            Name: stateName,
            ScheduleExpression: 'rate(30 minutes)',
            Target: { Arn: lambdaARN, RoleArn: stateRoleARN },
            FlexibleTimeWindow: makeFlexibleWindow(),
            State: 'DISABLED',
          }));
          const getResp = await client.send(new GetScheduleCommand({ Name: stateName }));
          if (getResp.State !== 'DISABLED') throw new Error(`expected DISABLED, got ${getResp.State}`);
        } finally {
          await safeCleanup(() => client.send(new DeleteScheduleCommand({ Name: stateName })));
        }
      } finally {
        await helperDeleteRole(stateRole);
      }
    }));

    results.push(await runner.runTest(SVC, 'ListSchedules_NamePrefix', async () => {
      const prefixName = uniqueName('PrefixSched');
      const prefixRole = uniqueName('PrefixSchedRole');
      await helperCreateRole(prefixRole);
      try {
        const prefixRoleARN = `arn:aws:iam::000000000000:role/${prefixRole}`;
        await client.send(new CreateScheduleCommand({
          Name: prefixName,
          ScheduleExpression: 'rate(30 minutes)',
          Target: { Arn: lambdaARN, RoleArn: prefixRoleARN },
          FlexibleTimeWindow: makeFlexibleWindow(),
        }));
        try {
          const prefix = prefixName.slice(0, -8);
          const resp = await client.send(new ListSchedulesCommand({ NamePrefix: prefix }));
          const found = resp.Schedules?.some(s => s.Name === prefixName);
          if (!found) throw new Error(`schedule "${prefixName}" not found with prefix "${prefix}"`);
        } finally {
          await safeCleanup(() => client.send(new DeleteScheduleCommand({ Name: prefixName })));
        }
      } finally {
        await helperDeleteRole(prefixRole);
      }
    }));

    results.push(await runner.runTest(SVC, 'UntagResource_NonExistentKey', async () => {
      const schedARN = `arn:aws:scheduler:${ctx.region}:000000000000:schedule/${scheduleName}`;
      await client.send(new UntagResourceCommand({
        ResourceArn: schedARN,
        TagKeys: ['NonExistentKey'],
      }));
    }));

    results.push(await runner.runTest(SVC, 'CreateSchedule_InvalidExpression', async () => {
      let err: unknown;
      try {
        await client.send(new CreateScheduleCommand({
          Name: uniqueName('InvExprSched'),
          ScheduleExpression: 'not-a-valid-expression',
          Target: { Arn: lambdaARN, RoleArn: roleARN },
          FlexibleTimeWindow: makeFlexibleWindow(),
        }));
      } catch (e) { err = e; }
      if (!err) throw new Error('expected error for invalid schedule expression');
    }));
  } finally {
    await deleteIAMRole(iamClient, roleName);
  }

  return results;
}
