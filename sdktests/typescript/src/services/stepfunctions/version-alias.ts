import {
  SFNClient,
  PublishStateMachineVersionCommand,
  ListStateMachineVersionsCommand,
  DeleteStateMachineVersionCommand,
  CreateStateMachineAliasCommand,
  DescribeStateMachineAliasCommand,
  ListStateMachineAliasesCommand,
  UpdateStateMachineAliasCommand,
  DeleteStateMachineAliasCommand,
} from '@aws-sdk/client-sfn';
import type { TestRunner, TestResult } from '../../runner.js';
import { assertErrorContains, safeCleanup, makeUniqueName } from '../../helpers.js';
import type { VerifySMState } from './definition-tests.js';

export async function runVersionAndAliasTests(
  runner: TestRunner,
  client: SFNClient,
  ctx: { region: string },
  results: TestResult[],
  verify: VerifySMState,
): Promise<{ secondVersionARN: string }> {
  let versionARN = '';
  let secondVersionARN = '';

  results.push(await runner.runTest('stepfunctions', 'PublishStateMachineVersion', async () => {
    if (!verify.arn) throw new Error('state machine ARN not available');
    const resp = await client.send(new PublishStateMachineVersionCommand({
      stateMachineArn: verify.arn,
      description: 'test version 1',
    }));
    if (!resp.stateMachineVersionArn) throw new Error('expected stateMachineVersionArn to be defined');
    versionARN = resp.stateMachineVersionArn;
  }));

  results.push(await runner.runTest('stepfunctions', 'ListStateMachineVersions', async () => {
    if (!verify.arn) throw new Error('state machine ARN not available');
    const resp = await client.send(new ListStateMachineVersionsCommand({
      stateMachineArn: verify.arn,
    }));
    if (!resp.stateMachineVersions?.length) throw new Error('expected at least one version');
  }));

  results.push(await runner.runTest('stepfunctions', 'PublishStateMachineVersion_Second', async () => {
    if (!verify.arn) throw new Error('state machine ARN not available');
    const resp = await client.send(new PublishStateMachineVersionCommand({
      stateMachineArn: verify.arn,
      description: 'test version 2',
    }));
    if (!resp.stateMachineVersionArn) throw new Error('expected stateMachineVersionArn to be defined');
    if (resp.stateMachineVersionArn === versionARN) throw new Error('second version should have different ARN');
    secondVersionARN = resp.stateMachineVersionArn;
  }));

  results.push(await runner.runTest('stepfunctions', 'DeleteStateMachineVersion', async () => {
    if (!versionARN) throw new Error('version ARN not available');
    await client.send(new DeleteStateMachineVersionCommand({ stateMachineVersionArn: versionARN }));
    await client.send(new ListStateMachineVersionsCommand({ stateMachineArn: verify.arn }));
  }));

  results.push(await runner.runTest('stepfunctions', 'DeleteStateMachineVersion_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new DeleteStateMachineVersionCommand({
        stateMachineVersionArn: 'arn:aws:states:us-east-1:000000000000:stateMachine:fake:999',
      }));
    } catch (e) {
      err = e;
    }
    assertErrorContains(err, 'StateMachineVersionNotFound');
  }));

  let aliasARN = '';
  const aliasName = `PROD-${Date.now()}`;

  results.push(await runner.runTest('stepfunctions', 'CreateStateMachineAlias', async () => {
    if (!verify.arn) throw new Error('state machine ARN not available');
    const listResp = await client.send(new ListStateMachineVersionsCommand({
      stateMachineArn: verify.arn,
    }));
    if (!listResp.stateMachineVersions?.length) throw new Error('no versions available for alias');
    const latestVersion = listResp.stateMachineVersions[0].stateMachineVersionArn;

    const resp = await client.send(new CreateStateMachineAliasCommand({
      name: aliasName,
      description: 'production alias',
      routingConfiguration: [{ stateMachineVersionArn: latestVersion!, weight: 100 }],
    }));
    if (!resp.stateMachineAliasArn) throw new Error('expected stateMachineAliasArn to be defined');
    aliasARN = resp.stateMachineAliasArn;
  }));

  results.push(await runner.runTest('stepfunctions', 'DescribeStateMachineAlias', async () => {
    if (!aliasARN) throw new Error('alias ARN not available');
    const resp = await client.send(new DescribeStateMachineAliasCommand({
      stateMachineAliasArn: aliasARN,
    }));
    if (resp.name !== aliasName) throw new Error(`alias name mismatch: got ${resp.name}, want ${aliasName}`);
    if (!resp.creationDate) throw new Error('expected creationDate to be defined');
  }));

  results.push(await runner.runTest('stepfunctions', 'ListStateMachineAliases', async () => {
    if (!verify.arn) throw new Error('state machine ARN not available');
    const resp = await client.send(new ListStateMachineAliasesCommand({
      stateMachineArn: verify.arn,
    }));
    if (!resp.stateMachineAliases?.length) {
      throw new Error(`expected at least one alias, got ${JSON.stringify(resp)}`);
    }
  }));

  results.push(await runner.runTest('stepfunctions', 'UpdateStateMachineAlias', async () => {
    if (!aliasARN) throw new Error('alias ARN not available');
    const resp = await client.send(new UpdateStateMachineAliasCommand({
      stateMachineAliasArn: aliasARN,
      description: 'updated production alias',
    }));
    if (!resp.updateDate) throw new Error('expected updateDate to be defined');
  }));

  results.push(await runner.runTest('stepfunctions', 'DeleteStateMachineAlias', async () => {
    if (!aliasARN) throw new Error('alias ARN not available');
    await client.send(new DeleteStateMachineAliasCommand({ stateMachineAliasArn: aliasARN }));
  }));

  results.push(await runner.runTest('stepfunctions', 'DeleteStateMachineAlias_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new DeleteStateMachineAliasCommand({
        stateMachineAliasArn: 'arn:aws:states:us-east-1:000000000000:stateMachine:fake:NONEXISTENT',
      }));
    } catch (e) {
      err = e;
    }
    assertErrorContains(err, 'StateMachineAliasDoesNotExist');
  }));

  results.push(await runner.runTest('stepfunctions', 'CreateStateMachineAlias_Duplicate', async () => {
    if (!verify.arn) throw new Error('state machine ARN not available');
    const listVerResp = await client.send(new ListStateMachineVersionsCommand({
      stateMachineArn: verify.arn,
    }));
    if (!listVerResp.stateMachineVersions?.length) throw new Error('no versions available');
    const realVersionArn = listVerResp.stateMachineVersions[0].stateMachineVersionArn;

    const dupAliasName = makeUniqueName('DUP');
    try {
      await client.send(new CreateStateMachineAliasCommand({
        name: dupAliasName,
        routingConfiguration: [{ stateMachineVersionArn: realVersionArn!, weight: 100 }],
      }));
    } catch (e) {
      throw new Error(`first create failed: ${e instanceof Error ? e.message : String(e)}`);
    }

    try {
      await client.send(new CreateStateMachineAliasCommand({
        name: dupAliasName,
        routingConfiguration: [{ stateMachineVersionArn: realVersionArn!, weight: 100 }],
      }));
      throw new Error('expected error for duplicate alias');
    } catch (e) {
      if (e instanceof Error && e.message.startsWith('expected error')) throw e;
    } finally {
      await safeCleanup(() => client.send(new DeleteStateMachineAliasCommand({
        stateMachineAliasArn: `arn:aws:states:${ctx.region}:000000000000:stateMachineAlias:${dupAliasName}`,
      })));
    }
  }));

  return { secondVersionARN };
}
