import {
  EventBridgeClient,
  CreateEventBusCommand,
  DeleteEventBusCommand,
  DescribeEventBusCommand,
  ListEventBusesCommand,
  PutRuleCommand,
  DeleteRuleCommand,
  DescribeRuleCommand,
  ListRulesCommand,
  EnableRuleCommand,
  DisableRuleCommand,
  PutTargetsCommand,
  RemoveTargetsCommand,
  ListTargetsByRuleCommand,
  PutEventsCommand,
  TagResourceCommand,
  UntagResourceCommand,
  ListTagsForResourceCommand,
} from '@aws-sdk/client-eventbridge';
import type { TestRunner, TestResult } from '../../runner.js';
import type { ServiceContext } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runCrudTests(
  runner: TestRunner,
  client: EventBridgeClient,
  ctx: ServiceContext,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const region = ctx.region;

  const busName = makeUniqueName('TestBus');
  const ruleName = makeUniqueName('TestRule');
  const targetID = makeUniqueName('TestTarget');

  try {
    results.push(await runner.runTest('eventbridge', 'CreateEventBus', async () => {
      await client.send(new CreateEventBusCommand({ Name: busName }));
    }));

    results.push(await runner.runTest('eventbridge', 'DescribeEventBus', async () => {
      const resp = await client.send(new DescribeEventBusCommand({ Name: busName }));
      if (!resp.Name) throw new Error('expected Name to be defined');
    }));

    results.push(await runner.runTest('eventbridge', 'ListEventBuses', async () => {
      const resp = await client.send(new ListEventBusesCommand({}));
      if (!resp.EventBuses) throw new Error('expected EventBuses to be defined');
    }));

    results.push(await runner.runTest('eventbridge', 'PutRule', async () => {
      await client.send(new PutRuleCommand({ Name: ruleName, EventBusName: busName }));
    }));

    results.push(await runner.runTest('eventbridge', 'DescribeRule', async () => {
      const resp = await client.send(new DescribeRuleCommand({ Name: ruleName, EventBusName: busName }));
      if (!resp.Name) throw new Error('expected Name to be defined');
    }));

    results.push(await runner.runTest('eventbridge', 'ListRules', async () => {
      const resp = await client.send(new ListRulesCommand({ EventBusName: busName }));
      if (!resp.Rules) throw new Error('expected Rules to be defined');
    }));

    results.push(await runner.runTest('eventbridge', 'PutTargets', async () => {
      await client.send(new PutTargetsCommand({
        Rule: ruleName, EventBusName: busName,
        Targets: [{ Id: targetID, Arn: `arn:aws:lambda:${region}:000000000000:function:TestFunction` }],
      }));
    }));

    results.push(await runner.runTest('eventbridge', 'ListTargetsByRule', async () => {
      const resp = await client.send(new ListTargetsByRuleCommand({ Rule: ruleName, EventBusName: busName }));
      if (!resp.Targets) throw new Error('expected Targets to be defined');
    }));

    results.push(await runner.runTest('eventbridge', 'PutEvents', async () => {
      const event = JSON.stringify({ source: 'com.example.test', 'detail-type': 'TestEvent', detail: { message: 'test' } });
      await client.send(new PutEventsCommand({
        Entries: [{ Source: 'com.example.test', DetailType: 'TestEvent', Detail: event, EventBusName: busName }],
      }));
    }));

    results.push(await runner.runTest('eventbridge', 'RemoveTargets', async () => {
      await client.send(new RemoveTargetsCommand({ Rule: ruleName, EventBusName: busName, Ids: [targetID] }));
    }));

    results.push(await runner.runTest('eventbridge', 'DisableRule', async () => {
      await client.send(new DisableRuleCommand({ Name: ruleName, EventBusName: busName }));
    }));

    results.push(await runner.runTest('eventbridge', 'EnableRule', async () => {
      await client.send(new EnableRuleCommand({ Name: ruleName, EventBusName: busName }));
    }));

    const ruleARN = `arn:aws:events:${region}:000000000000:rule/${busName}/${ruleName}`;

    results.push(await runner.runTest('eventbridge', 'TagResource', async () => {
      await client.send(new TagResourceCommand({ ResourceARN: ruleARN, Tags: [{ Key: 'Environment', Value: 'test' }] }));
    }));

    results.push(await runner.runTest('eventbridge', 'ListTagsForResource', async () => {
      const resp = await client.send(new ListTagsForResourceCommand({ ResourceARN: ruleARN }));
      if (!resp.Tags) throw new Error('expected Tags to be defined');
    }));

    results.push(await runner.runTest('eventbridge', 'UntagResource', async () => {
      await client.send(new UntagResourceCommand({ ResourceARN: ruleARN, TagKeys: ['Environment'] }));
    }));

    results.push(await runner.runTest('eventbridge', 'DeleteRule', async () => {
      await client.send(new DeleteRuleCommand({ Name: ruleName, EventBusName: busName }));
    }));

    results.push(await runner.runTest('eventbridge', 'DeleteEventBus', async () => {
      await client.send(new DeleteEventBusCommand({ Name: busName }));
    }));
  } catch {
    await safeCleanup(async () => { await client.send(new DeleteEventBusCommand({ Name: busName })); });
    throw new Error('CRUD test chain failed');
  }

  return results;
}
