import {
  EventBridgeClient,
  CreateEventBusCommand,
  DeleteEventBusCommand,
  DeleteRuleCommand,
  ListEventBusesCommand,
  ListRuleNamesByTargetCommand,
  ListRulesCommand,
  PutEventsCommand,
  PutRuleCommand,
  PutTargetsCommand,
  RemoveTargetsCommand,
  TestEventPatternCommand,
} from '@aws-sdk/client-eventbridge';
import type { TestRunner, TestResult } from '../../runner.js';
import type { ServiceContext } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runOtherTests(
  runner: TestRunner,
  client: EventBridgeClient,
  ctx: ServiceContext,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const region = ctx.region;

  results.push(await runner.runTest('eventbridge', 'TestEventPattern_Match', async () => {
    const pattern = JSON.stringify({ source: ['com.example.custom'] });
    const event = JSON.stringify({ source: 'com.example.custom', 'detail-type': 'TestEvent' });
    const resp = await client.send(new TestEventPatternCommand({ EventPattern: pattern, Event: event }));
    if (!resp.Result) throw new Error('expected pattern to match, got false');
  }));

  results.push(await runner.runTest('eventbridge', 'TestEventPattern_NoMatch', async () => {
    const pattern = JSON.stringify({ source: ['com.example.other'] });
    const event = JSON.stringify({ source: 'com.example.custom', 'detail-type': 'TestEvent' });
    const resp = await client.send(new TestEventPatternCommand({ EventPattern: pattern, Event: event }));
    if (resp.Result) throw new Error('expected pattern not to match, got true');
  }));

  results.push(await runner.runTest('eventbridge', 'ListRuleNamesByTarget', async () => {
    const lrntBus = makeUniqueName('LrntBus');
    const lrntRule = makeUniqueName('LrntRule');
    const lrntTargetID = makeUniqueName('LrntTarget');
    try {
      await client.send(new CreateEventBusCommand({ Name: lrntBus }));
      await client.send(new PutRuleCommand({ Name: lrntRule, EventBusName: lrntBus }));
      const targetARN = `arn:aws:lambda:${region}:000000000000:function:ListRulesFn`;
      await client.send(new PutTargetsCommand({
        Rule: lrntRule, EventBusName: lrntBus, Targets: [{ Id: lrntTargetID, Arn: targetARN }],
      }));
      const resp = await client.send(new ListRuleNamesByTargetCommand({ TargetArn: targetARN, EventBusName: lrntBus }));
      if (!resp.RuleNames) throw new Error('expected RuleNames to be defined');
      const found = resp.RuleNames.includes(lrntRule);
      if (!found) throw new Error(`expected rule ${lrntRule} in list, got ${JSON.stringify(resp.RuleNames)}`);
    } finally {
      await safeCleanup(async () => {
        await client.send(new RemoveTargetsCommand({ Rule: lrntRule, EventBusName: lrntBus, Ids: [lrntTargetID] }));
        await client.send(new DeleteRuleCommand({ Name: lrntRule, EventBusName: lrntBus }));
      });
      await safeCleanup(async () => { await client.send(new DeleteEventBusCommand({ Name: lrntBus })); });
    }
  }));

  results.push(await runner.runTest('eventbridge', 'ListEventBuses_NamePrefix', async () => {
    const lnpBus = makeUniqueName('LnpPrefixBus');
    try {
      await client.send(new CreateEventBusCommand({ Name: lnpBus }));
      const resp = await client.send(new ListEventBusesCommand({ NamePrefix: 'LnpPrefixBus' }));
      if (!resp.EventBuses) throw new Error('expected EventBuses to be defined');
      const found = resp.EventBuses.some((bus) => bus.Name === lnpBus);
      if (!found) throw new Error(`expected bus ${lnpBus} in filtered list`);
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteEventBusCommand({ Name: lnpBus })); });
    }
  }));

  results.push(await runner.runTest('eventbridge', 'PutEvents_MultipleEntries', async () => {
    const event1 = JSON.stringify({ source: 'com.test.multi', 'detail-type': 'Event1', detail: { id: '1' } });
    const event2 = JSON.stringify({ source: 'com.test.multi', 'detail-type': 'Event2', detail: { id: '2' } });
    const resp = await client.send(new PutEventsCommand({
      Entries: [
        { Source: 'com.test.multi', DetailType: 'Event1', Detail: event1 },
        { Source: 'com.test.multi', DetailType: 'Event2', Detail: event2 },
      ],
    }));
    if (resp.FailedEntryCount !== 0) throw new Error(`expected 0 failed entries, got ${resp.FailedEntryCount}`);
    if (!resp.Entries || resp.Entries.length !== 2) throw new Error(`expected 2 entry results, got ${resp.Entries?.length}`);
  }));

  results.push(await runner.runTest('eventbridge', 'ListRules_Pagination', async () => {
    const pgTs = String(Date.now());
    const pgRules: string[] = [];
    try {
      for (const i of [0, 1, 2, 3, 4]) {
        const name = `PagRule-${pgTs}-${i}`;
        await client.send(new PutRuleCommand({ Name: name }));
        pgRules.push(name);
      }
      const allRules: string[] = [];
      let nextToken: string | undefined;
      do {
        const resp = await client.send(new ListRulesCommand({ NextToken: nextToken, Limit: 2 }));
        for (const r of resp.Rules ?? []) {
          if (r.Name?.startsWith(`PagRule-${pgTs}-`)) allRules.push(r.Name);
        }
        nextToken = resp.NextToken;
      } while (nextToken);
      if (allRules.length !== 5) throw new Error(`expected 5 paginated rules, got ${allRules.length}`);
    } finally {
      for (const name of pgRules) {
        await safeCleanup(async () => { await client.send(new DeleteRuleCommand({ Name: name })); });
      }
    }
  }));

  return results;
}
