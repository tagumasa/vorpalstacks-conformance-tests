import {
  SSMClient,
  PutParameterCommand,
  GetParameterCommand,
  GetParametersCommand,
  GetParametersByPathCommand,
  DescribeParametersCommand,
  DeleteParameterCommand,
  AddTagsToResourceCommand,
  ListTagsForResourceCommand,
  RemoveTagsFromResourceCommand,
} from '@aws-sdk/client-ssm';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, assertThrows, safeCleanup } from '../../helpers.js';

export async function runParameterTests(
  runner: TestRunner,
  client: SSMClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const s = 'ssm';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(s, name, fn));

  const basePath = `/ts-ssm-${Date.now()}`;
  const params: string[] = [];

  try {
    await r('PutParameter', async () => {
      const name = `${basePath}/basic/string-param`;
      await client.send(new PutParameterCommand({ Name: name, Value: 'hello-world', Type: 'String' }));
      params.push(name);
    });

    await r('GetParameter', async () => {
      const resp = await client.send(new GetParameterCommand({ Name: `${basePath}/basic/string-param` }));
      if (!resp.Parameter) throw new Error('expected Parameter to be defined');
      if (resp.Parameter.Value !== 'hello-world') throw new Error('value mismatch');
    });

    const p2 = `${basePath}/batch/p2`;
    const p3 = `${basePath}/batch/p3`;
    await r('GetParameters', async () => {
      await client.send(new PutParameterCommand({ Name: p2, Value: 'val2', Type: 'String' }));
      params.push(p2);
      await client.send(new PutParameterCommand({ Name: p3, Value: 'val3', Type: 'String' }));
      params.push(p3);
      const resp = await client.send(new GetParametersCommand({ Names: [p2, p3] }));
      if (!resp.Parameters || resp.Parameters.length !== 2) {
        throw new Error(`expected 2 parameters, got ${resp.Parameters?.length}`);
      }
    });

    await r('GetParametersByPath', async () => {
      const resp = await client.send(new GetParametersByPathCommand({ Path: `${basePath}/batch` }));
      if (!resp.Parameters?.length) throw new Error('expected parameters under /batch path');
      for (const p of resp.Parameters) {
        if (!p.Name?.startsWith(`${basePath}/batch`)) {
          throw new Error(`parameter ${p.Name} not under expected path`);
        }
      }
    });

    await r('GetParametersByPath_NonRecursive', async () => {
      const base = makeUniqueName('/nr/param');
      const child = `${base}/child`;
      try {
        await client.send(new PutParameterCommand({ Name: base, Value: 'direct', Type: 'String' }));
        await client.send(new PutParameterCommand({ Name: child, Value: 'child', Type: 'String' }));
        const resp = await client.send(new GetParametersByPathCommand({ Path: '/nr', Recursive: false }));
        for (const p of resp.Parameters ?? []) {
          if (p.Name?.includes('/child')) {
            throw new Error(`non-recursive should not return children, got ${p.Name}`);
          }
        }
      } finally {
        await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: base })));
        await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: child })));
      }
    });

    await r('DescribeParameters', async () => {
      const resp = await client.send(new DescribeParametersCommand({}));
      if (!resp.Parameters) throw new Error('expected Parameters to be defined');
    });

    await r('DescribeParameters_Pagination', async () => {
      const pgPrefix = `${basePath}/pg`;
      const pgParams: string[] = [];
      for (const i of [0, 1, 2, 3, 4]) {
        const name = `${pgPrefix}/p${i}`;
        await client.send(new PutParameterCommand({ Name: name, Value: `v${i}`, Type: 'String' }));
        params.push(name);
        pgParams.push(name);
      }

      const allFound: string[] = [];
      let nextToken: string | undefined;
      try {
        do {
          const resp = await client.send(new DescribeParametersCommand({ MaxResults: 2, NextToken: nextToken }));
          for (const p of resp.Parameters ?? []) {
            if (p.Name?.startsWith(pgPrefix)) allFound.push(p.Name);
          }
          nextToken = resp.NextToken;
        } while (nextToken);
      } finally {
        for (const name of pgParams) {
          await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: name })));
        }
      }
      if (allFound.length !== 5) throw new Error(`expected 5 paginated parameters, got ${allFound.length}`);
    });

    await r('PutParameter_Overwrite_IncrementsVersion', async () => {
      const name = `${basePath}/overwrite/param`;
      const resp1 = await client.send(new PutParameterCommand({ Name: name, Value: 'v1', Type: 'String' }));
      params.push(name);
      if (resp1.Version !== 1) throw new Error(`expected version 1, got ${resp1.Version}`);
      const resp2 = await client.send(new PutParameterCommand({ Name: name, Value: 'v2', Type: 'String', Overwrite: true }));
      if (resp2.Version !== 2) throw new Error(`expected version 2, got ${resp2.Version}`);
    });

    await r('PutParameter_GetParameter_Roundtrip', async () => {
      const name = makeUniqueName('/rt/param');
      try {
        await client.send(new PutParameterCommand({ Name: name, Value: 'roundtrip-value-12345', Type: 'String' }));
        const resp = await client.send(new GetParameterCommand({ Name: name }));
        if (!resp.Parameter) throw new Error('expected Parameter to be defined');
        if (resp.Parameter.Value !== 'roundtrip-value-12345') {
          throw new Error(`value mismatch: got ${resp.Parameter.Value}`);
        }
      } finally {
        await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: name })));
      }
    });

    await r('GetParameters_InvalidNames', async () => {
      const validName = '/valid/param-test';
      try {
        await client.send(new PutParameterCommand({ Name: validName, Value: 'valid', Type: 'String' }));
        const resp = await client.send(new GetParametersCommand({ Names: [validName, '/nonexistent/param-xyz'] }));
        if (resp.Parameters?.length !== 1) throw new Error(`expected 1 valid parameter, got ${resp.Parameters?.length}`);
        if (resp.InvalidParameters?.length !== 1) throw new Error(`expected 1 invalid parameter, got ${resp.InvalidParameters?.length}`);
      } finally {
        await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: validName })));
      }
    });

    await r('GetParameter_NonExistent', async () => {
      await assertThrows(
        () => client.send(new GetParameterCommand({ Name: '/nonexistent/param-xyz' })),
        'ParameterNotFound',
      );
    });

    await r('DeleteParameter', async () => {
      const name = `${basePath}/delete/target`;
      await client.send(new PutParameterCommand({ Name: name, Value: 'del-me', Type: 'String' }));
      await client.send(new DeleteParameterCommand({ Name: name }));
      await assertThrows(
        () => client.send(new GetParameterCommand({ Name: name })),
        'ParameterNotFound',
      );
    });

    await r('DeleteParameter_NonExistent', async () => {
      await assertThrows(
        () => client.send(new DeleteParameterCommand({ Name: '/nonexistent/param-xyz' })),
        'ParameterNotFound',
      );
    });

    const tagParam = `${basePath}/tags/tagged-param`;
    await r('AddTagsToResource', async () => {
      await client.send(new PutParameterCommand({ Name: tagParam, Value: 'tagged', Type: 'String' }));
      params.push(tagParam);
      await client.send(new AddTagsToResourceCommand({
        ResourceType: 'Parameter', ResourceId: tagParam,
        Tags: [{ Key: 'Environment', Value: 'Test' }, { Key: 'Team', Value: 'Platform' }],
      }));
    });

    await r('ListTagsForResource', async () => {
      const resp = await client.send(new ListTagsForResourceCommand({ ResourceType: 'Parameter', ResourceId: tagParam }));
      const tags = resp.TagList ?? [];
      const env = tags.find(t => t.Key === 'Environment');
      if (!env || env.Value !== 'Test') throw new Error('Environment tag not found');
      const team = tags.find(t => t.Key === 'Team');
      if (!team || team.Value !== 'Platform') throw new Error('Team tag not found');
    });

    await r('RemoveTagsFromResource', async () => {
      await client.send(new RemoveTagsFromResourceCommand({
        ResourceType: 'Parameter', ResourceId: tagParam, TagKeys: ['Environment'],
      }));
      const resp = await client.send(new ListTagsForResourceCommand({ ResourceType: 'Parameter', ResourceId: tagParam }));
      const env = resp.TagList?.find(t => t.Key === 'Environment');
      if (env) throw new Error('Environment tag should be removed');
      const team = resp.TagList?.find(t => t.Key === 'Team');
      if (!team) throw new Error('Team tag should still exist');
    });

    await r('AddTagsToResource_ListTagsForResource', async () => {
      const name = makeUniqueName('/tag/param');
      try {
        await client.send(new PutParameterCommand({ Name: name, Value: 'tagged', Type: 'String' }));
        await client.send(new AddTagsToResourceCommand({
          ResourceType: 'Parameter', ResourceId: name,
          Tags: [{ Key: 'env', Value: 'test' }, { Key: 'team', Value: 'platform' }],
        }));
        const resp = await client.send(new ListTagsForResourceCommand({ ResourceType: 'Parameter', ResourceId: name }));
        if (resp.TagList?.length !== 2) throw new Error(`expected 2 tags, got ${resp.TagList?.length}`);
        const tagMap: Record<string, string> = {};
        for (const t of resp.TagList) {
          if (t.Key && t.Value) tagMap[t.Key] = t.Value;
        }
        if (tagMap['env'] !== 'test' || tagMap['team'] !== 'platform') throw new Error('tag values mismatch');
      } finally {
        await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: name })));
      }
    });

    await r('DescribeParameters_ContainsCreated', async () => {
      const dpName = makeUniqueName('/dp/param');
      try {
        await client.send(new PutParameterCommand({ Name: dpName, Value: 'desc-test', Type: 'String', Description: 'Test description for search' }));
        const resp = await client.send(new DescribeParametersCommand({ Filters: [{ Key: 'Name', Values: [dpName] }] }));
        if (resp.Parameters?.length !== 1) throw new Error(`expected 1 parameter, got ${resp.Parameters?.length}`);
        if (resp.Parameters[0].Description !== 'Test description for search') throw new Error('description mismatch');
      } finally {
        await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: dpName })));
      }
    });

    await r('DescribeParameters_TypeFilter', async () => {
      const strName = makeUniqueName('/tf/string');
      const slName = makeUniqueName('/tf/stringlist');
      try {
        await client.send(new PutParameterCommand({ Name: strName, Value: 'x', Type: 'String' }));
        await client.send(new PutParameterCommand({ Name: slName, Value: 'a,b,c', Type: 'StringList' }));
        const resp = await client.send(new DescribeParametersCommand({ Filters: [{ Key: 'Type', Values: ['String'] }] }));
        for (const p of resp.Parameters ?? []) {
          if (p.Type !== 'String') throw new Error(`type filter returned non-String type: ${p.Type}`);
        }
      } finally {
        await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: strName })));
        await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: slName })));
      }
    });

  } finally {
    for (const name of params) {
      await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: name })));
    }
  }

  return results;
}
