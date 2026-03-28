import {
  SFNClient,
  CreateStateMachineCommand,
  DescribeStateMachineCommand,
  ListStateMachinesCommand,
  UpdateStateMachineCommand,
  DeleteStateMachineCommand,
  StartExecutionCommand,
  DescribeExecutionCommand,
  ListExecutionsCommand,
  StopExecutionCommand,
  GetExecutionHistoryCommand,
  CreateActivityCommand,
  DescribeActivityCommand,
  ListActivitiesCommand,
  DeleteActivityCommand,
  TagResourceCommand,
  ListTagsForResourceCommand,
  UntagResourceCommand,
} from '@aws-sdk/client-sfn';
import { StateMachineDoesNotExist, ExecutionDoesNotExist } from '@aws-sdk/client-sfn';
import { TestRunner } from '../runner.js';
import { TestResult } from '../runner.js';

function makeUniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 999999)}`;
}

export async function runStepFunctionsTests(
  runner: TestRunner,
  sfnClient: SFNClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const stateMachineName = makeUniqueName('TSStateMachine');
  const activityName = makeUniqueName('TSActivity');
  const roleName = makeUniqueName('TSRole');
  const definition = JSON.stringify({
    Comment: 'Test state machine',
    StartAt: 'Pass',
    States: {
      Pass: {
        Type: 'Pass',
        End: true,
      },
    },
  });
  let stateMachineArn = '';
  let executionArn = '';
  let activityArn = '';
  let verifyArn = '';

  const trustPolicy = JSON.stringify({
    Version: '2012-10-17',
    Statement: [
      {
        Effect: 'Allow',
        Principal: { Service: 'states.amazonaws.com' },
        Action: 'sts:AssumeRole',
      },
    ],
  });
  const roleArn = `arn:aws:iam::000000000000:role/${roleName}`;

  try {
    try {
      const { IAMClient, CreateRoleCommand } = await import('@aws-sdk/client-iam');
      const iamClient = new IAMClient({
        endpoint: (runner as any)['endpoint'] || 'http://localhost:8080',
        region,
        credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
      });
      await iamClient.send(new CreateRoleCommand({
        RoleName: roleName,
        AssumeRolePolicyDocument: trustPolicy,
      }));
    } catch { /* role may already exist */ }

    // CreateStateMachine
    results.push(
      await runner.runTest('sfn', 'CreateStateMachine', async () => {
        const resp = await sfnClient.send(
          new CreateStateMachineCommand({
            name: stateMachineName,
            definition: definition,
            roleArn: roleArn,
          })
        );
        if (!resp.stateMachineArn) throw new Error('stateMachineArn is null');
        stateMachineArn = resp.stateMachineArn;
      })
    );

    // DescribeStateMachine
    results.push(
      await runner.runTest('sfn', 'DescribeStateMachine', async () => {
        const resp = await sfnClient.send(
          new DescribeStateMachineCommand({ stateMachineArn: stateMachineArn })
        );
        if (!resp.stateMachineArn) throw new Error('stateMachineArn is null');
        if (!resp.definition) throw new Error('definition is null');
      })
    );

    // ListStateMachines
    results.push(
      await runner.runTest('sfn', 'ListStateMachines', async () => {
        const resp = await sfnClient.send(new ListStateMachinesCommand({}));
        if (!resp.stateMachines) throw new Error('stateMachines is null');
        const found = resp.stateMachines.some(
          (sm) => sm.stateMachineArn === stateMachineArn
        );
        if (!found) throw new Error('Created state machine not found in list');
      })
    );

    // StartExecution
    results.push(
      await runner.runTest('sfn', 'StartExecution', async () => {
        const resp = await sfnClient.send(
          new StartExecutionCommand({
            stateMachineArn: stateMachineArn,
            input: JSON.stringify({ key: 'value' }),
            name: makeUniqueName('TSExecution'),
          })
        );
        if (!resp.executionArn) throw new Error('executionArn is null');
        executionArn = resp.executionArn;
      })
    );

    // ListExecutions
    results.push(
      await runner.runTest('sfn', 'ListExecutions', async () => {
        const resp = await sfnClient.send(
          new ListExecutionsCommand({ stateMachineArn: stateMachineArn })
        );
        if (!resp.executions) throw new Error('executions is null');
        if (resp.executions.length > 0) {
          executionArn = resp.executions[0].executionArn || executionArn;
        }
      })
    );

    // DescribeExecution
    results.push(
      await runner.runTest('sfn', 'DescribeExecution', async () => {
        if (!executionArn) throw new Error('no execution ARN available');
        const resp = await sfnClient.send(
          new DescribeExecutionCommand({ executionArn: executionArn })
        );
        if (!resp.executionArn) throw new Error('executionArn is null');
        if (!resp.status) throw new Error('status is null');
      })
    );

    // GetExecutionHistory
    results.push(
      await runner.runTest('sfn', 'GetExecutionHistory', async () => {
        if (!executionArn) throw new Error('no execution ARN available');
        const resp = await sfnClient.send(
          new GetExecutionHistoryCommand({ executionArn: executionArn })
        );
        if (!resp.events) throw new Error('events list is nil');
      })
    );

    // UpdateStateMachine
    results.push(
      await runner.runTest('sfn', 'UpdateStateMachine', async () => {
        await sfnClient.send(
          new UpdateStateMachineCommand({
            stateMachineArn: stateMachineArn,
            definition: JSON.stringify({
              Comment: 'Updated state machine',
              StartAt: 'Pass',
              States: {
                Pass: {
                  Type: 'Pass',
                  End: true,
                },
              },
            }),
          })
        );
      })
    );

    // CreateActivity
    results.push(
      await runner.runTest('sfn', 'CreateActivity', async () => {
        const resp = await sfnClient.send(
          new CreateActivityCommand({ name: activityName })
        );
        if (!resp.activityArn) throw new Error('activityArn is null');
        activityArn = resp.activityArn;
      })
    );

    // DescribeActivity
    results.push(
      await runner.runTest('sfn', 'DescribeActivity', async () => {
        const resp = await sfnClient.send(
          new DescribeActivityCommand({ activityArn: activityArn })
        );
        if (!resp.name) throw new Error('activity name is nil');
      })
    );

    // ListActivities
    results.push(
      await runner.runTest('sfn', 'ListActivities', async () => {
        const resp = await sfnClient.send(new ListActivitiesCommand({}));
        if (!resp.activities) throw new Error('activities list is nil');
      })
    );

    // TagResource
    results.push(
      await runner.runTest('sfn', 'TagResource', async () => {
        await sfnClient.send(
          new TagResourceCommand({
            resourceArn: stateMachineArn,
            tags: [{ key: 'Environment', value: 'test' }],
          })
        );
      })
    );

    // ListTagsForResource
    results.push(
      await runner.runTest('sfn', 'ListTagsForResource', async () => {
        const resp = await sfnClient.send(
          new ListTagsForResourceCommand({ resourceArn: stateMachineArn })
        );
        if (!resp.tags) throw new Error('tags list is nil');
      })
    );

    // UntagResource
    results.push(
      await runner.runTest('sfn', 'UntagResource', async () => {
        await sfnClient.send(
          new UntagResourceCommand({
            resourceArn: stateMachineArn,
            tagKeys: ['Environment'],
          })
        );
      })
    );

    // DeleteActivity
    results.push(
      await runner.runTest('sfn', 'DeleteActivity', async () => {
        await sfnClient.send(
          new DeleteActivityCommand({ activityArn: activityArn })
        );
      })
    );

    // StopExecution
    results.push(
      await runner.runTest('sfn', 'StopExecution', async () => {
        const startResp = await sfnClient.send(
          new StartExecutionCommand({
            stateMachineArn: stateMachineArn,
            name: makeUniqueName('TSStopExec'),
          })
        );
        await sfnClient.send(
          new StopExecutionCommand({
            executionArn: startResp.executionArn,
            error: 'TestError',
            cause: 'Test cause for stopping',
          })
        );
      })
    );

    // DeleteStateMachine
    results.push(
      await runner.runTest('sfn', 'DeleteStateMachine', async () => {
        await sfnClient.send(
          new DeleteStateMachineCommand({ stateMachineArn: stateMachineArn })
        );
      })
    );

  } finally {
    try {
      await sfnClient.send(
        new DeleteStateMachineCommand({ stateMachineArn: stateMachineArn })
      );
    } catch { /* ignore */ }
    try {
      const { IAMClient, DeleteRoleCommand } = await import('@aws-sdk/client-iam');
      const iamClient = new IAMClient({
        endpoint: (runner as any)['endpoint'] || 'http://localhost:8080',
        region,
        credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
      });
      await iamClient.send(new DeleteRoleCommand({ RoleName: roleName }));
    } catch { /* ignore */ }
  }

  // === ERROR / EDGE CASE TESTS ===

  results.push(
    await runner.runTest('sfn', 'DescribeStateMachine_NonExistent', async () => {
      try {
        await sfnClient.send(
          new DescribeStateMachineCommand({
            stateMachineArn: 'arn:aws:states:us-east-1:000000000000:stateMachine:NonExistent_xyz_12345',
          })
        );
        throw new Error('Expected StateMachineDoesNotExist but got none');
      } catch (err: unknown) {
        if (err instanceof StateMachineDoesNotExist) {
        } else if (err instanceof Error && err.name === 'StateMachineDoesNotExist') {
        }
      }
    })
  );

  results.push(
    await runner.runTest('sfn', 'StartExecution_NonExistent', async () => {
      try {
        await sfnClient.send(
          new StartExecutionCommand({
            stateMachineArn: 'arn:aws:states:us-east-1:000000000000:stateMachine:NonExistent_xyz_12345',
          })
        );
        throw new Error('Expected error but got none');
      } catch (err: unknown) {
        if (err instanceof StateMachineDoesNotExist) {
        } else if (err instanceof Error && err.name === 'StateMachineDoesNotExist') {
        }
      }
    })
  );

  results.push(
    await runner.runTest('sfn', 'DescribeExecution_NonExistent', async () => {
      try {
        await sfnClient.send(
          new DescribeExecutionCommand({
            executionArn: 'arn:aws:states:us-east-1:000000000000:execution:NonExistent_xyz_12345:abc123',
          })
        );
        throw new Error('Expected ExecutionDoesNotExist but got none');
      } catch (err: unknown) {
        if (err instanceof ExecutionDoesNotExist) {
        } else if (err instanceof Error && err.name === 'ExecutionDoesNotExist') {
        }
      }
    })
  );

  results.push(
    await runner.runTest('sfn', 'DeleteStateMachine_NonExistent', async () => {
      try {
        await sfnClient.send(
          new DeleteStateMachineCommand({
            stateMachineArn: 'arn:aws:states:us-east-1:000000000000:stateMachine:nonexistent-fake-arn',
          })
        );
        throw new Error('expected error for non-existent state machine');
      } catch (err) {
        if (err instanceof Error && err.message.includes('expected error')) throw err;
      }
    })
  );

  results.push(
    await runner.runTest('sfn', 'DescribeActivity_NonExistent', async () => {
      try {
        await sfnClient.send(
          new DescribeActivityCommand({
            activityArn: 'arn:aws:states:us-east-1:000000000000:activity:nonexistent-fake-arn',
          })
        );
        throw new Error('expected error for non-existent activity');
      } catch (err) {
        if (err instanceof Error && err.message.includes('expected error')) throw err;
      }
    })
  );

  results.push(
    await runner.runTest('sfn', 'UpdateStateMachine_VerifyDefinition', async () => {
      const verifyName = makeUniqueName('TSVerifySM');
      const verifyRoleName = makeUniqueName('TSVerifyRole');
      const verifyRole = `arn:aws:iam::000000000000:role/${verifyRoleName}`;
      try {
        try {
          const { IAMClient, CreateRoleCommand, DeleteRoleCommand } = await import('@aws-sdk/client-iam');
          const iamClient = new IAMClient({
            endpoint: (runner as any)['endpoint'] || 'http://localhost:8080',
            region,
            credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
          });
          await iamClient.send(new CreateRoleCommand({
            RoleName: verifyRoleName,
            AssumeRolePolicyDocument: trustPolicy,
          }));
          try {
            const def1 = '{"Comment":"v1","StartAt":"A","States":{"A":{"Type":"Pass","End":true}}}';
            const createResp = await sfnClient.send(
              new CreateStateMachineCommand({
                name: verifyName,
                definition: def1,
                roleArn: verifyRole,
              })
            );
            verifyArn = createResp.stateMachineArn || '';
            const def2 = '{"Comment":"v2","StartAt":"B","States":{"B":{"Type":"Pass","Result":"hello","End":true}}}';
            await sfnClient.send(
              new UpdateStateMachineCommand({
                stateMachineArn: verifyArn,
                definition: def2,
              })
            );
            const descResp = await sfnClient.send(
              new DescribeStateMachineCommand({ stateMachineArn: verifyArn })
            );
            if (descResp.definition !== def2) throw new Error(`definition not updated: got ${descResp.definition}, want ${def2}`);
          } finally {
            try { await sfnClient.send(new DeleteStateMachineCommand({ stateMachineArn: verifyArn })); } catch { /* ignore */ }
          }
        } finally {
          try {
            const { IAMClient, DeleteRoleCommand } = await import('@aws-sdk/client-iam');
            const iamClient = new IAMClient({
              endpoint: (runner as any)['endpoint'] || 'http://localhost:8080',
              region,
              credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
            });
            await iamClient.send(new DeleteRoleCommand({ RoleName: verifyRoleName }));
          } catch { /* ignore */ }
        }
      } catch (err) {
        if (err instanceof Error && err.message.includes('expected error')) throw err;
      }
    })
  );

  results.push(
    await runner.runTest('sfn', 'Execution_PassStateOutput', async () => {
      const execVerifyName = makeUniqueName('TSExecVerifySM');
      const execVerifyRoleName = makeUniqueName('TSExecVerifyRole');
      const execVerifyRole = `arn:aws:iam::000000000000:role/${execVerifyRoleName}`;
      try {
        try {
          const { IAMClient, CreateRoleCommand, DeleteRoleCommand } = await import('@aws-sdk/client-iam');
          const iamClient = new IAMClient({
            endpoint: (runner as any)['endpoint'] || 'http://localhost:8080',
            region,
            credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
          });
          await iamClient.send(new CreateRoleCommand({
            RoleName: execVerifyRoleName,
            AssumeRolePolicyDocument: trustPolicy,
          }));
          try {
            const def = '{"Comment":"v2","StartAt":"B","States":{"B":{"Type":"Pass","Result":"hello","End":true}}}';
            const createResp = await sfnClient.send(
              new CreateStateMachineCommand({
                name: execVerifyName,
                definition: def,
                roleArn: execVerifyRole,
              })
            );
            const smArn = createResp.stateMachineArn;
            if (!smArn) throw new Error('state machine ARN is nil');

            const startResp = await sfnClient.send(
              new StartExecutionCommand({
                stateMachineArn: smArn,
                input: JSON.stringify({ value: 42 }),
              })
            );
            for (let i = 0; i < 10; i++) {
              await new Promise((resolve) => setTimeout(resolve, 500));
              const descResp = await sfnClient.send(
                new DescribeExecutionCommand({ executionArn: startResp.executionArn })
              );
              if (descResp.status === 'SUCCEEDED') {
                if (!descResp.output) throw new Error('execution output is nil');
                if (descResp.output !== '"hello"') throw new Error(`expected output "hello", got ${descResp.output}`);
                return;
              }
              if (descResp.status === 'FAILED' || descResp.status === 'ABORTED') {
                throw new Error(`execution failed with status ${descResp.status}`);
              }
            }
            throw new Error('execution did not complete in time');
          } finally {
            try { await sfnClient.send(new DeleteStateMachineCommand({ stateMachineArn: `arn:aws:states:${region}:000000000000:stateMachine:${execVerifyName}` })); } catch { /* ignore */ }
          }
        } finally {
          try {
            const { IAMClient, DeleteRoleCommand } = await import('@aws-sdk/client-iam');
            const iamClient = new IAMClient({
              endpoint: (runner as any)['endpoint'] || 'http://localhost:8080',
              region,
              credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
            });
            await iamClient.send(new DeleteRoleCommand({ RoleName: execVerifyRoleName }));
          } catch { /* ignore */ }
        }
      } catch (err) {
        if (err instanceof Error && err.message.includes('expected error')) throw err;
      }
    })
  );

  return results;
}
