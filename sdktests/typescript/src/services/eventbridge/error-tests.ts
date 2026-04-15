import {
  EventBridgeClient,
  CreateEventBusCommand,
  DeleteEventBusCommand,
  DeleteRuleCommand,
  DescribeRuleCommand,
  DescribeEventBusCommand,
  DisableRuleCommand,
  EnableRuleCommand,
  PutRuleCommand,
  PutTargetsCommand,
  RemoveTargetsCommand,
  ListTargetsByRuleCommand,
  UpdateEventBusCommand,
  PutEventsCommand,
} from '@aws-sdk/client-eventbridge';
import type { TestRunner, TestResult } from '../../runner.js';
import type { ServiceContext } from '../../runner.js';
import { makeUniqueName, safeCleanup, assertThrows } from '../../helpers.js';

export async function runErrorTests(
  runner: TestRunner,
  client: EventBridgeClient,
  ctx: ServiceContext,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const region = ctx.region;

  results.push(await runner.runTest('eventbridge', 'DescribeEventBus_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new DescribeEventBusCommand({ Name: 'nonexistent-bus-xyz-12345' }));
    }, 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest('eventbridge', 'DeleteEventBus_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new DeleteEventBusCommand({ Name: 'nonexistent-bus-xyz-12345' }));
    }, 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest('eventbridge', 'DescribeRule_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new DescribeRuleCommand({ Name: 'nonexistent-rule-xyz-12345' }));
    }, 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest('eventbridge', 'DeleteRule_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new DeleteRuleCommand({ Name: 'nonexistent-rule-xyz-12345' }));
    }, 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest('eventbridge', 'CreateEventBus_DuplicateName', async () => {
    const dupBus = makeUniqueName('DupBus');
    try {
      await client.send(new CreateEventBusCommand({ Name: dupBus }));
      await assertThrows(async () => {
        await client.send(new CreateEventBusCommand({ Name: dupBus }));
      }, 'ResourceAlreadyExistsException');
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteEventBusCommand({ Name: dupBus })); });
    }
  }));

  results.push(await runner.runTest('eventbridge', 'PutRule_DisableAndVerify', async () => {
    const rdBus = makeUniqueName('RdBus');
    const rdRule = makeUniqueName('RdRule');
    try {
      await client.send(new CreateEventBusCommand({ Name: rdBus }));
      await client.send(new PutRuleCommand({ Name: rdRule, EventBusName: rdBus, Description: 'test rule for disable' }));
      await client.send(new DisableRuleCommand({ Name: rdRule, EventBusName: rdBus }));
      const resp = await client.send(new DescribeRuleCommand({ Name: rdRule, EventBusName: rdBus }));
      if (resp.State !== 'DISABLED') throw new Error(`expected state DISABLED, got ${resp.State}`);
      await client.send(new EnableRuleCommand({ Name: rdRule, EventBusName: rdBus }));
      const resp2 = await client.send(new DescribeRuleCommand({ Name: rdRule, EventBusName: rdBus }));
      if (resp2.State !== 'ENABLED') throw new Error(`expected state ENABLED, got ${resp2.State}`);
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteRuleCommand({ Name: rdRule, EventBusName: rdBus })); });
      await safeCleanup(async () => { await client.send(new DeleteEventBusCommand({ Name: rdBus })); });
    }
  }));

  results.push(await runner.runTest('eventbridge', 'PutRule_WithEventPattern', async () => {
    const epBus = makeUniqueName('EpBus');
    const epRule = makeUniqueName('EpRule');
    try {
      await client.send(new CreateEventBusCommand({ Name: epBus }));
      const pattern = JSON.stringify({ source: ['com.example.test'], 'detail-type': ['OrderCreated'] });
      await client.send(new PutRuleCommand({ Name: epRule, EventBusName: epBus, EventPattern: pattern }));
      const resp = await client.send(new DescribeRuleCommand({ Name: epRule, EventBusName: epBus }));
      if (!resp.EventPattern) throw new Error('expected EventPattern to be defined');
      const gotPattern: Record<string, unknown> = JSON.parse(resp.EventPattern);
      const gotSource = gotPattern['source'];
      if (!Array.isArray(gotSource) || gotSource.length !== 1 || gotSource[0] !== 'com.example.test') {
        throw new Error(`source mismatch in pattern, got ${JSON.stringify(gotSource)}`);
      }
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteRuleCommand({ Name: epRule, EventBusName: epBus })); });
      await safeCleanup(async () => { await client.send(new DeleteEventBusCommand({ Name: epBus })); });
    }
  }));

  results.push(await runner.runTest('eventbridge', 'PutEvents_DefaultBus', async () => {
    const event = JSON.stringify({ source: 'com.test.default', 'detail-type': 'DefaultBusEvent', detail: { key: 'value' } });
    const resp = await client.send(new PutEventsCommand({
      Entries: [{ Source: 'com.test.default', DetailType: 'DefaultBusEvent', Detail: event }],
    }));
    if (resp.FailedEntryCount !== 0) throw new Error(`expected 0 failed entries, got ${resp.FailedEntryCount}`);
    if (!resp.Entries || resp.Entries.length !== 1) throw new Error(`expected 1 entry result, got ${resp.Entries?.length}`);
    if (!resp.Entries[0].EventId) throw new Error('expected non-empty event ID');
  }));

  results.push(await runner.runTest('eventbridge', 'PutTargets_RemoveTargets_Verify', async () => {
    const trBus = makeUniqueName('TrBus');
    const trRule = makeUniqueName('TrRule');
    const trTargetID = makeUniqueName('TrTarget');
    try {
      await client.send(new CreateEventBusCommand({ Name: trBus }));
      await client.send(new PutRuleCommand({ Name: trRule, EventBusName: trBus }));
      const targetARN = `arn:aws:lambda:${region}:000000000000:function:TargetFunc`;
      await client.send(new PutTargetsCommand({
        Rule: trRule, EventBusName: trBus,
        Targets: [{ Id: trTargetID, Arn: targetARN, Input: '{"action": "test"}' }],
      }));
      const listResp = await client.send(new ListTargetsByRuleCommand({ Rule: trRule, EventBusName: trBus }));
      if (!listResp.Targets || listResp.Targets.length !== 1) throw new Error(`expected 1 target, got ${listResp.Targets?.length}`);
      if (listResp.Targets[0].Arn !== targetARN) throw new Error(`target ARN mismatch, got ${listResp.Targets[0].Arn}`);
      if (listResp.Targets[0].Input !== '{"action": "test"}') throw new Error(`target input mismatch, got ${listResp.Targets[0].Input}`);
      await client.send(new RemoveTargetsCommand({ Rule: trRule, EventBusName: trBus, Ids: [trTargetID] }));
      const listResp2 = await client.send(new ListTargetsByRuleCommand({ Rule: trRule, EventBusName: trBus }));
      if (listResp2.Targets && listResp2.Targets.length !== 0) throw new Error(`expected 0 targets after removal, got ${listResp2.Targets.length}`);
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteRuleCommand({ Name: trRule, EventBusName: trBus })); });
      await safeCleanup(async () => { await client.send(new DeleteEventBusCommand({ Name: trBus })); });
    }
  }));

  results.push(await runner.runTest('eventbridge', 'DeleteRule_WithTargetsFails', async () => {
    const dtBus = makeUniqueName('DtBus');
    const dtRule = makeUniqueName('DtRule');
    const dtTarget = makeUniqueName('DtTarget');
    try {
      await client.send(new CreateEventBusCommand({ Name: dtBus }));
      await client.send(new PutRuleCommand({ Name: dtRule, EventBusName: dtBus }));
      await client.send(new PutTargetsCommand({
        Rule: dtRule, EventBusName: dtBus,
        Targets: [{ Id: dtTarget, Arn: `arn:aws:lambda:${region}:000000000000:function:F` }],
      }));
      let err: unknown;
      try { await client.send(new DeleteRuleCommand({ Name: dtRule, EventBusName: dtBus })); } catch (e) { err = e; }
      if (!err) throw new Error('expected error when deleting rule with targets');
    } finally {
      await safeCleanup(async () => {
        await client.send(new RemoveTargetsCommand({ Rule: dtRule, EventBusName: dtBus, Ids: [dtTarget] }));
        await client.send(new DeleteRuleCommand({ Name: dtRule, EventBusName: dtBus }));
      });
      await safeCleanup(async () => { await client.send(new DeleteEventBusCommand({ Name: dtBus })); });
    }
  }));

  results.push(await runner.runTest('eventbridge', 'UpdateEventBus', async () => {
    const ueBus = makeUniqueName('UeBus');
    try {
      await client.send(new CreateEventBusCommand({ Name: ueBus }));
      const resp = await client.send(new UpdateEventBusCommand({ Name: ueBus, Description: 'updated description' }));
      if (resp.Description !== 'updated description') throw new Error(`description mismatch, got ${resp.Description}`);
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteEventBusCommand({ Name: ueBus })); });
    }
  }));

  results.push(await runner.runTest('eventbridge', 'UpdateEventBus_VerifyDescription', async () => {
    const uvBus = makeUniqueName('UvBus');
    try {
      await client.send(new CreateEventBusCommand({ Name: uvBus, Description: 'original' }));
      await client.send(new UpdateEventBusCommand({ Name: uvBus, Description: 'updated' }));
      const desc = await client.send(new DescribeEventBusCommand({ Name: uvBus }));
      if (desc.Description !== 'updated') throw new Error(`description not updated, got ${desc.Description}`);
    } finally {
      await safeCleanup(async () => { await client.send(new DeleteEventBusCommand({ Name: uvBus })); });
    }
  }));

  return results;
}
