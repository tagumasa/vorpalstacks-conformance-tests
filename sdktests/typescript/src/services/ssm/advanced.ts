import {
  SSMClient,
  PutParameterCommand,
  GetParameterCommand,
  GetParameterHistoryCommand,
  LabelParameterVersionCommand,
  DeleteParameterCommand,
  DeleteParametersCommand,
  AddTagsToResourceCommand,
  ListTagsForResourceCommand,
  RemoveTagsFromResourceCommand,
} from '@aws-sdk/client-ssm';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, assertThrows, safeCleanup } from '../../helpers.js';

export async function runAdvancedParameterTests(
  runner: TestRunner,
  client: SSMClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const s = 'ssm';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(s, name, fn));

  await r('AddTagsToResource_Merge', async () => {
    const name = makeUniqueName('/tag/merge/param');
    try {
      await client.send(new PutParameterCommand({ Name: name, Value: 'tagged', Type: 'String' }));
      await client.send(new AddTagsToResourceCommand({ ResourceType: 'Parameter', ResourceId: name, Tags: [{ Key: 'a', Value: '1' }] }));
      await client.send(new AddTagsToResourceCommand({ ResourceType: 'Parameter', ResourceId: name, Tags: [{ Key: 'b', Value: '2' }, { Key: 'a', Value: 'updated' }] }));
      const resp = await client.send(new ListTagsForResourceCommand({ ResourceType: 'Parameter', ResourceId: name }));
      if (!resp.TagList) throw new Error('expected TagList to be defined');
      const tagMap: Record<string, string> = {};
      for (const t of resp.TagList) { if (t.Key) tagMap[t.Key] = t.Value ?? ''; }
      if (Object.keys(tagMap).length !== 2) throw new Error(`expected 2 tags, got ${Object.keys(tagMap).length}`);
      if (tagMap['a'] !== 'updated') throw new Error(`expected a=updated, got ${tagMap['a']}`);
      if (tagMap['b'] !== '2') throw new Error(`expected b=2, got ${tagMap['b']}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: name })));
    }
  });

  await r('ListTagsForResource_Empty', async () => {
    const name = makeUniqueName('/tag/empty/param');
    try {
      await client.send(new PutParameterCommand({ Name: name, Value: 'notags', Type: 'String' }));
      const resp = await client.send(new ListTagsForResourceCommand({ ResourceType: 'Parameter', ResourceId: name }));
      if (resp.TagList && resp.TagList.length !== 0) throw new Error(`expected 0 tags, got ${resp.TagList.length}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: name })));
    }
  });

  await r('AddTagsToResource_NonExistent', async () => {
    await assertThrows(
      () => client.send(new AddTagsToResourceCommand({ ResourceType: 'Parameter', ResourceId: '/nonexistent/param-xyz', Tags: [{ Key: 'k', Value: 'v' }] })),
      'ParameterNotFound',
    );
  });

  await r('RemoveTagsFromResource_NonExistent', async () => {
    await assertThrows(
      () => client.send(new RemoveTagsFromResourceCommand({ ResourceType: 'Parameter', ResourceId: '/nonexistent/param-xyz', TagKeys: ['k'] })),
      'ParameterNotFound',
    );
  });

  await r('PutParameter_Duplicate_NoOverwrite', async () => {
    const name = makeUniqueName('/ver/dup');
    try {
      await client.send(new PutParameterCommand({ Name: name, Value: 'first', Type: 'String' }));
      try {
        await client.send(new PutParameterCommand({ Name: name, Value: 'second', Type: 'String' }));
        throw new Error('expected error when creating duplicate without Overwrite');
      } catch (err) {
        if (err instanceof Error && err.message === 'expected error when creating duplicate without Overwrite') throw err;
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: name })));
    }
  });

  await r('GetParameterHistory_TwoVersions', async () => {
    const name = makeUniqueName('/ver/hist');
    try {
      await client.send(new PutParameterCommand({ Name: name, Value: 'v1', Type: 'String' }));
      await client.send(new PutParameterCommand({ Name: name, Value: 'v2', Type: 'String', Overwrite: true }));
      const resp = await client.send(new GetParameterHistoryCommand({ Name: name }));
      if (resp.Parameters?.length !== 2) throw new Error(`expected 2 history entries, got ${resp.Parameters?.length}`);
      if (resp.Parameters[0].Version !== 2) throw new Error(`expected first entry version 2, got ${resp.Parameters[0].Version}`);
      if (resp.Parameters[1].Version !== 1) throw new Error(`expected second entry version 1, got ${resp.Parameters[1].Version}`);
      if (resp.Parameters[0].Value !== 'v2') throw new Error(`expected v2 value, got ${resp.Parameters[0].Value}`);
      if (resp.Parameters[1].Value !== 'v1') throw new Error(`expected v1 value, got ${resp.Parameters[1].Value}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: name })));
    }
  });

  await r('GetParameterHistory_ContainsLabels', async () => {
    const name = makeUniqueName('/ver/histlabel');
    try {
      await client.send(new PutParameterCommand({ Name: name, Value: 'v1', Type: 'String' }));
      await client.send(new LabelParameterVersionCommand({ Name: name, ParameterVersion: 1, Labels: ['golden'] }));
      const resp = await client.send(new GetParameterHistoryCommand({ Name: name }));
      if (resp.Parameters?.length !== 1) throw new Error(`expected 1 history entry, got ${resp.Parameters?.length}`);
      const labels = resp.Parameters[0].Labels;
      if (labels?.length !== 1 || labels[0] !== 'golden') {
        throw new Error(`expected label 'golden', got ${JSON.stringify(labels)}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: name })));
    }
  });

  await r('LabelParameterVersion_GetByLabel', async () => {
    const name = makeUniqueName('/ver/getlabel');
    try {
      await client.send(new PutParameterCommand({ Name: name, Value: 'original', Type: 'String' }));
      await client.send(new LabelParameterVersionCommand({ Name: name, ParameterVersion: 1, Labels: ['mylabel'] }));
      const selector = `${name}:mylabel`;
      const resp = await client.send(new GetParameterCommand({ Name: selector }));
      if (!resp.Parameter) throw new Error('expected Parameter to be defined');
      if (resp.Parameter.Value !== 'original') throw new Error(`value mismatch: got ${resp.Parameter.Value}`);
      if (resp.Parameter.Selector !== selector) throw new Error(`selector mismatch`);
    } finally {
      await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: name })));
    }
  });

  await r('LabelParameterVersion_MovesLabel', async () => {
    const name = makeUniqueName('/ver/movelabel');
    try {
      await client.send(new PutParameterCommand({ Name: name, Value: 'v1', Type: 'String' }));
      await client.send(new PutParameterCommand({ Name: name, Value: 'v2', Type: 'String', Overwrite: true }));
      await client.send(new LabelParameterVersionCommand({ Name: name, ParameterVersion: 1, Labels: ['latest'] }));
      await client.send(new LabelParameterVersionCommand({ Name: name, ParameterVersion: 2, Labels: ['latest'] }));
      const hist = await client.send(new GetParameterHistoryCommand({ Name: name }));
      for (const p of hist.Parameters ?? []) {
        if (p.Version === 1 && p.Labels?.includes('latest')) {
          throw new Error('v1 should not have latest label after move');
        }
        if (p.Version === 2 && !p.Labels?.includes('latest')) {
          throw new Error('v2 should have latest label');
        }
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: name })));
    }
  });

  await r('DeleteParameters_Success', async () => {
    const n1 = makeUniqueName('/batch/del1');
    const n2 = makeUniqueName('/batch/del2');
    await client.send(new PutParameterCommand({ Name: n1, Value: 'batch', Type: 'String' }));
    await client.send(new PutParameterCommand({ Name: n2, Value: 'batch', Type: 'String' }));
    const resp = await client.send(new DeleteParametersCommand({ Names: [n1, n2] }));
    if (resp.DeletedParameters?.length !== 2) throw new Error(`expected 2 deleted, got ${resp.DeletedParameters?.length}`);
  });

  await r('DeleteParameters_MixedValidInvalid', async () => {
    const n1 = makeUniqueName('/batch/mixed');
    try {
      await client.send(new PutParameterCommand({ Name: n1, Value: 'batch', Type: 'String' }));
      const resp = await client.send(new DeleteParametersCommand({ Names: [n1, '/nonexistent/batch-xyz'] }));
      if (resp.DeletedParameters?.length !== 1) throw new Error(`expected 1 deleted, got ${resp.DeletedParameters?.length}`);
      if (resp.InvalidParameters?.length !== 1) throw new Error(`expected 1 invalid, got ${resp.InvalidParameters?.length}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: n1 })));
    }
  });

  await r('GetParameter_WithVersionSelector', async () => {
    const name = makeUniqueName('/ver/sel');
    try {
      await client.send(new PutParameterCommand({ Name: name, Value: 'version-one', Type: 'String' }));
      await client.send(new PutParameterCommand({ Name: name, Value: 'version-two', Type: 'String', Overwrite: true }));
      const resp = await client.send(new GetParameterCommand({ Name: `${name}:1` }));
      if (!resp.Parameter) throw new Error('expected Parameter to be defined');
      if (resp.Parameter.Value !== 'version-one') throw new Error(`expected 'version-one', got ${resp.Parameter.Value}`);
      if (resp.Parameter.Version !== 1) throw new Error(`expected version 1, got ${resp.Parameter.Version}`);
      if (!resp.Parameter.ARN) throw new Error('ARN should not be empty for version selector');
    } finally {
      await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: name })));
    }
  });

  await r('PutParameter_ReturnsVersionAndTier', async () => {
    const name = makeUniqueName('/resp/param');
    try {
      const resp = await client.send(new PutParameterCommand({ Name: name, Value: 'check', Type: 'String', Tier: 'Standard' }));
      if (resp.Version !== 1) throw new Error(`expected version 1, got ${resp.Version}`);
      if (resp.Tier !== 'Standard') throw new Error(`expected Standard tier, got ${resp.Tier}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: name })));
    }
  });

  return results;
}
