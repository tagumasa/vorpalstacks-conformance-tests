import {
  EventBridgeClient,
  PutRuleCommand,
  DescribeRuleCommand,
  ListRulesCommand,
  EnableRuleCommand,
  DisableRuleCommand,
  DeleteRuleCommand,
  PutTargetsCommand,
  ListTargetsByRuleCommand,
  RemoveTargetsCommand,
  PutEventsCommand,
  ListEventBusesCommand,
  DescribeEventBusCommand,
  CreateEventBusCommand,
  DeleteEventBusCommand,
  ListRuleNamesByTargetCommand,
  TagResourceCommand,
  ListTagsForResourceCommand,
  UntagResourceCommand,
} from '@aws-sdk/client-eventbridge';
import { ResourceNotFoundException, ResourceAlreadyExistsException } from '@aws-sdk/client-eventbridge';
import { TestRunner } from '../runner.js';
import { TestResult } from '../runner.js';

function makeUniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 999999)}`;
}

export async function runEventBridgeTests(
  runner: TestRunner,
  ebClient: EventBridgeClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const ruleName = makeUniqueName('TSRule');
  const eventBusName = makeUniqueName('TSEventBus');
  const targetId = makeUniqueName('TSTarget');
  let ruleCreated = false;
  let eventBusCreated = false;

  const sampleEvent = {
    Source: 'com.example.sdk',
    DetailType: 'TestEvent',
    Detail: JSON.stringify({ message: 'Hello from SDK test' }),
  };

  try {
    // CreateEventBus
    results.push(
      await runner.runTest('eventbridge', 'CreateEventBus', async () => {
        const resp = await ebClient.send(
          new CreateEventBusCommand({
            Name: eventBusName,
          })
        );
        if (!resp.EventBusArn) throw new Error('EventBusArn is null');
        eventBusCreated = true;
      })
    );

    // DescribeEventBus
    results.push(
      await runner.runTest('eventbridge', 'DescribeEventBus', async () => {
        const resp = await ebClient.send(
          new DescribeEventBusCommand({ Name: eventBusName })
        );
        if (!resp.Name) throw new Error('event bus name is nil');
      })
    );

    // ListEventBuses
    results.push(
      await runner.runTest('eventbridge', 'ListEventBuses', async () => {
        const resp = await ebClient.send(new ListEventBusesCommand({}));
        if (!resp.EventBuses) throw new Error('EventBuses is null');
      })
    );

    // PutRule
    results.push(
      await runner.runTest('eventbridge', 'PutRule', async () => {
        const resp = await ebClient.send(
          new PutRuleCommand({
            Name: ruleName,
            EventBusName: eventBusName,
          })
        );
        if (!resp) throw new Error('response is nil');
        ruleCreated = true;
      })
    );

    // DescribeRule
    results.push(
      await runner.runTest('eventbridge', 'DescribeRule', async () => {
        const resp = await ebClient.send(
          new DescribeRuleCommand({ Name: ruleName, EventBusName: eventBusName })
        );
        if (!resp.Name) throw new Error('rule name is nil');
      })
    );

    // ListRules
    results.push(
      await runner.runTest('eventbridge', 'ListRules', async () => {
        const resp = await ebClient.send(
          new ListRulesCommand({ EventBusName: eventBusName })
        );
        if (!resp.Rules) throw new Error('Rules is null');
      })
    );

    // PutTargets
    results.push(
      await runner.runTest('eventbridge', 'PutTargets', async () => {
        const resp = await ebClient.send(
          new PutTargetsCommand({
            Rule: ruleName,
            EventBusName: eventBusName,
            Targets: [
              {
                Id: targetId,
                Arn: `arn:aws:lambda:${region}:000000000000:function:TestFunction`,
              },
            ],
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // ListTargetsByRule
    results.push(
      await runner.runTest('eventbridge', 'ListTargetsByRule', async () => {
        const resp = await ebClient.send(
          new ListTargetsByRuleCommand({ Rule: ruleName, EventBusName: eventBusName })
        );
        if (!resp.Targets) throw new Error('Targets is null');
      })
    );

    // PutEvents
    results.push(
      await runner.runTest('eventbridge', 'PutEvents', async () => {
        const event = JSON.stringify({
          source: 'com.example.test',
          'detail-type': 'TestEvent',
          detail: { message: 'test' },
        });
        const resp = await ebClient.send(
          new PutEventsCommand({
            Entries: [{
              Source: 'com.example.test',
              DetailType: 'TestEvent',
              Detail: event,
              EventBusName: eventBusName,
            }],
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // RemoveTargets
    results.push(
      await runner.runTest('eventbridge', 'RemoveTargets', async () => {
        const resp = await ebClient.send(
          new RemoveTargetsCommand({
            Rule: ruleName,
            EventBusName: eventBusName,
            Ids: [targetId],
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // DisableRule
    results.push(
      await runner.runTest('eventbridge', 'DisableRule', async () => {
        const resp = await ebClient.send(
          new DisableRuleCommand({ Name: ruleName, EventBusName: eventBusName })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // EnableRule
    results.push(
      await runner.runTest('eventbridge', 'EnableRule', async () => {
        const resp = await ebClient.send(
          new EnableRuleCommand({ Name: ruleName, EventBusName: eventBusName })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // TagResource
    results.push(
      await runner.runTest('eventbridge', 'TagResource', async () => {
        const ruleARN = `arn:aws:events:${region}:000000000000:rule/${eventBusName}/${ruleName}`;
        const resp = await ebClient.send(
          new TagResourceCommand({
            ResourceARN: ruleARN,
            Tags: [{ Key: 'Environment', Value: 'test' }],
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // ListTagsForResource
    results.push(
      await runner.runTest('eventbridge', 'ListTagsForResource', async () => {
        const ruleARN = `arn:aws:events:${region}:000000000000:rule/${eventBusName}/${ruleName}`;
        const resp = await ebClient.send(
          new ListTagsForResourceCommand({ ResourceARN: ruleARN })
        );
        if (!resp.Tags) throw new Error('tags list is nil');
      })
    );

    // UntagResource
    results.push(
      await runner.runTest('eventbridge', 'UntagResource', async () => {
        const ruleARN = `arn:aws:events:${region}:000000000000:rule/${eventBusName}/${ruleName}`;
        const resp = await ebClient.send(
          new UntagResourceCommand({
            ResourceARN: ruleARN,
            TagKeys: ['Environment'],
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // DeleteRule
    results.push(
      await runner.runTest('eventbridge', 'DeleteRule', async () => {
        const resp = await ebClient.send(
          new DeleteRuleCommand({ Name: ruleName, EventBusName: eventBusName })
        );
        if (!resp) throw new Error('response is nil');
        ruleCreated = false;
      })
    );

    // DeleteEventBus
    results.push(
      await runner.runTest('eventbridge', 'DeleteEventBus', async () => {
        const resp = await ebClient.send(
          new DeleteEventBusCommand({ Name: eventBusName })
        );
        if (!resp) throw new Error('response is nil');
        eventBusCreated = false;
      })
    );

  } finally {
    try {
      if (ruleCreated) {
        await ebClient.send(new RemoveTargetsCommand({ Rule: ruleName, Ids: [targetId] }));
        await ebClient.send(new DeleteRuleCommand({ Name: ruleName }));
      }
    } catch { /* ignore */ }
    try {
      if (eventBusCreated) {
        await ebClient.send(new DeleteEventBusCommand({ Name: eventBusName }));
      }
    } catch { /* ignore */ }
  }

  // === ERROR / EDGE CASE TESTS ===

  results.push(
    await runner.runTest('eventbridge', 'DescribeEventBus_NonExistent', async () => {
      try {
        await ebClient.send(
          new DescribeEventBusCommand({ Name: 'nonexistent-bus-xyz-12345' })
        );
        throw new Error('expected error for non-existent event bus');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  results.push(
    await runner.runTest('eventbridge', 'DeleteEventBus_NonExistent', async () => {
      try {
        await ebClient.send(
          new DeleteEventBusCommand({ Name: 'nonexistent-bus-xyz-12345' })
        );
        throw new Error('expected error for non-existent event bus');
      } catch (err) {
        if (err instanceof Error && err.message.includes('expected error')) throw err;
      }
    })
  );

  results.push(
    await runner.runTest('eventbridge', 'DescribeRule_NonExistent', async () => {
      try {
        await ebClient.send(
          new DescribeRuleCommand({ Name: 'nonexistent-rule-xyz-12345' })
        );
        throw new Error('expected error for non-existent rule');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  results.push(
    await runner.runTest('eventbridge', 'DeleteRule_NonExistent', async () => {
      try {
        await ebClient.send(
          new DeleteRuleCommand({ Name: 'nonexistent-rule-xyz-12345' })
        );
        throw new Error('expected error for non-existent rule');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  results.push(
    await runner.runTest('eventbridge', 'CreateEventBus_DuplicateName', async () => {
      const dupBus = makeUniqueName('TSDupBus');
      try {
        await ebClient.send(new CreateEventBusCommand({ Name: dupBus }));
        try {
          await ebClient.send(new CreateEventBusCommand({ Name: dupBus }));
          throw new Error('expected error for duplicate event bus name');
        } catch (err) {
          if (!(err instanceof ResourceAlreadyExistsException)) {
            const name = err instanceof Error ? err.constructor.name : String(err);
            throw new Error(`Expected ResourceAlreadyExistsException, got ${name}`);
          }
        }
      } finally {
        try { await ebClient.send(new DeleteEventBusCommand({ Name: dupBus })); } catch { /* ignore */ }
      }
    })
  );

  results.push(
    await runner.runTest('eventbridge', 'PutRule_DisableAndVerify', async () => {
      const rdBus = makeUniqueName('TSRdBus');
      const rdRule = makeUniqueName('TSRdRule');
      try {
        await ebClient.send(new CreateEventBusCommand({ Name: rdBus }));
        await ebClient.send(
          new PutRuleCommand({
            Name: rdRule,
            EventBusName: rdBus,
            Description: 'test rule for disable',
          })
        );
        await ebClient.send(new DisableRuleCommand({ Name: rdRule, EventBusName: rdBus }));
        const resp1 = await ebClient.send(
          new DescribeRuleCommand({ Name: rdRule, EventBusName: rdBus })
        );
        if (resp1.State !== 'DISABLED') throw new Error(`expected state DISABLED, got ${resp1.State}`);
        await ebClient.send(new EnableRuleCommand({ Name: rdRule, EventBusName: rdBus }));
        const resp2 = await ebClient.send(
          new DescribeRuleCommand({ Name: rdRule, EventBusName: rdBus })
        );
        if (resp2.State !== 'ENABLED') throw new Error(`expected state ENABLED, got ${resp2.State}`);
      } finally {
        try { await ebClient.send(new DeleteRuleCommand({ Name: rdRule, EventBusName: rdBus })); } catch { /* ignore */ }
        try { await ebClient.send(new DeleteEventBusCommand({ Name: rdBus })); } catch { /* ignore */ }
      }
    })
  );

  results.push(
    await runner.runTest('eventbridge', 'PutRule_WithEventPattern', async () => {
      const epBus = makeUniqueName('TSEpBus');
      const epRule = makeUniqueName('TSEpRule');
      try {
        await ebClient.send(new CreateEventBusCommand({ Name: epBus }));
        const pattern = JSON.stringify({
          source: ['com.example.test'],
          'detail-type': ['OrderCreated'],
        });
        await ebClient.send(
          new PutRuleCommand({
            Name: epRule,
            EventBusName: epBus,
            EventPattern: pattern,
          })
        );
        const resp = await ebClient.send(
          new DescribeRuleCommand({ Name: epRule, EventBusName: epBus })
        );
        if (!resp.EventPattern) throw new Error('event pattern is nil');
        const gotPattern = JSON.parse(resp.EventPattern);
        if (gotPattern.source?.[0] !== 'com.example.test') throw new Error('source mismatch in pattern');
      } finally {
        try { await ebClient.send(new DeleteRuleCommand({ Name: epRule, EventBusName: epBus })); } catch { /* ignore */ }
        try { await ebClient.send(new DeleteEventBusCommand({ Name: epBus })); } catch { /* ignore */ }
      }
    })
  );

  results.push(
    await runner.runTest('eventbridge', 'PutEvents_DefaultBus', async () => {
      const event = JSON.stringify({
        source: 'com.test.default',
        'detail-type': 'DefaultBusEvent',
        detail: { key: 'value' },
      });
      const resp = await ebClient.send(
        new PutEventsCommand({
          Entries: [{
            Source: 'com.test.default',
            DetailType: 'DefaultBusEvent',
            Detail: event,
          }],
        })
      );
      if (resp.FailedEntryCount !== 0) throw new Error(`expected 0 failed entries, got ${resp.FailedEntryCount}`);
      if (!resp.Entries || resp.Entries.length !== 1) throw new Error('expected 1 entry result');
      if (!resp.Entries[0].EventId || resp.Entries[0].EventId === '') throw new Error('expected non-empty event ID');
    })
  );

  results.push(
    await runner.runTest('eventbridge', 'PutTargets_RemoveTargets_Verify', async () => {
      const trBus = makeUniqueName('TSTrBus');
      const trRule = makeUniqueName('TSTrRule');
      const trTargetId = makeUniqueName('TSTrTarget');
      try {
        await ebClient.send(new CreateEventBusCommand({ Name: trBus }));
        await ebClient.send(
          new PutRuleCommand({ Name: trRule, EventBusName: trBus })
        );
        const targetARN = `arn:aws:lambda:${region}:000000000000:function:TargetFunc`;
        await ebClient.send(
          new PutTargetsCommand({
            Rule: trRule,
            EventBusName: trBus,
            Targets: [{
              Id: trTargetId,
              Arn: targetARN,
              Input: '{"action": "test"}',
            }],
          })
        );
        const listResp1 = await ebClient.send(
          new ListTargetsByRuleCommand({ Rule: trRule, EventBusName: trBus })
        );
        if (!listResp1.Targets || listResp1.Targets.length !== 1) throw new Error(`expected 1 target, got ${listResp1.Targets?.length}`);
        await ebClient.send(
          new RemoveTargetsCommand({
            Rule: trRule,
            EventBusName: trBus,
            Ids: [trTargetId],
          })
        );
        const listResp2 = await ebClient.send(
          new ListTargetsByRuleCommand({ Rule: trRule, EventBusName: trBus })
        );
        if (listResp2.Targets && listResp2.Targets.length !== 0) throw new Error(`expected 0 targets after removal, got ${listResp2.Targets.length}`);
      } finally {
        try { await ebClient.send(new DeleteRuleCommand({ Name: trRule, EventBusName: trBus })); } catch { /* ignore */ }
        try { await ebClient.send(new DeleteEventBusCommand({ Name: trBus })); } catch { /* ignore */ }
      }
    })
  );

  results.push(
    await runner.runTest('eventbridge', 'DeleteRule_WithTargetsFails', async () => {
      const dtBus = makeUniqueName('TSDtBus');
      const dtRule = makeUniqueName('TSDtRule');
      const dtTarget = makeUniqueName('TSDtTarget');
      try {
        await ebClient.send(new CreateEventBusCommand({ Name: dtBus }));
        await ebClient.send(
          new PutRuleCommand({ Name: dtRule, EventBusName: dtBus })
        );
        await ebClient.send(
          new PutTargetsCommand({
            Rule: dtRule,
            EventBusName: dtBus,
            Targets: [{
              Id: dtTarget,
              Arn: `arn:aws:lambda:${region}:000000000000:function:F`,
            }],
          })
        );
        try {
          await ebClient.send(
            new DeleteRuleCommand({ Name: dtRule, EventBusName: dtBus })
          );
          throw new Error('expected error when deleting rule with targets');
        } catch (err) {
          if (err instanceof Error && err.message.includes('expected error')) throw err;
        }
      } finally {
        try { await ebClient.send(new RemoveTargetsCommand({ Rule: dtRule, EventBusName: dtBus, Ids: [dtTarget] })); } catch { /* ignore */ }
        try { await ebClient.send(new DeleteRuleCommand({ Name: dtRule, EventBusName: dtBus })); } catch { /* ignore */ }
        try { await ebClient.send(new DeleteEventBusCommand({ Name: dtBus })); } catch { /* ignore */ }
      }
    })
  );

  return results;
}
