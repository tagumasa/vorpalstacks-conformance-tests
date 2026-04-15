import {
  AppSyncClient,
  CreateApiCommand,
  GetApiCommand,
  ListApisCommand,
  UpdateApiCommand,
  DeleteApiCommand,
  CreateChannelNamespaceCommand,
  GetChannelNamespaceCommand,
  ListChannelNamespacesCommand,
  UpdateChannelNamespaceCommand,
  DeleteChannelNamespaceCommand,
  TagResourceCommand,
  UntagResourceCommand,
  ListTagsForResourceCommand,
} from '@aws-sdk/client-appsync';
import type { TestRunner, TestResult, ServiceContext } from '../../runner.js';
import { makeUniqueName, assertThrows, safeCleanup } from '../../helpers.js';

export interface EventApiState {
  eventApiId: string;
  evtApiForChId: string;
  tracked: Array<() => Promise<void>>;
}

export async function runEventApiTests(
  runner: TestRunner,
  ctx: ServiceContext,
  client: AppSyncClient,
): Promise<{ results: TestResult[]; state: EventApiState }> {
  const results: TestResult[] = [];
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest('appsync', name, fn));

  const tracked: Array<() => Promise<void>> = [];
  const track = (fn: () => Promise<void>) => { tracked.push(fn); };

  const defaultEventConfig = {
    authProviders: [{ authType: 'API_KEY' as const }],
    connectionAuthModes: [{ authType: 'OPENID_CONNECT' as const }],
    defaultPublishAuthModes: [{ authType: 'OPENID_CONNECT' as const }],
    defaultSubscribeAuthModes: [{ authType: 'OPENID_CONNECT' as const }],
  };

  const eventApiName = makeUniqueName('evt-api');
  let eventApiId = '';

  await r('CreateApi', async () => {
    const resp = await client.send(new CreateApiCommand({
      name: eventApiName,
      eventConfig: defaultEventConfig,
    }));
    if (!resp.api?.apiId) throw new Error('expected api with apiId to be defined');
    eventApiId = resp.api.apiId;
  });

  await r('CreateApi_WithTags', async () => {
    const resp = await client.send(new CreateApiCommand({
      name: makeUniqueName('test-api-tags'),
      eventConfig: defaultEventConfig,
      tags: { env: 'test', team: 'platform' },
    }));
    if (!resp.api?.apiId) throw new Error('expected api with apiId to be defined');
    if (resp.api.tags?.env !== 'test' || resp.api.tags?.team !== 'platform') {
      throw new Error('tags not persisted');
    }
  });

  await r('GetApi', async () => {
    const resp = await client.send(new GetApiCommand({ apiId: eventApiId }));
    if (!resp.api) throw new Error('expected api to be defined');
    if (resp.api.name !== eventApiName) throw new Error('name mismatch');
  });

  const eventApiOwner = makeUniqueName('evt-api-owner');
  let eventApiOwnerId = '';
  await r('CreateApi_WithOwnerContact', async () => {
    const resp = await client.send(new CreateApiCommand({
      name: eventApiOwner,
      eventConfig: defaultEventConfig,
      ownerContact: 'test@example.com',
    }));
    if (!resp.api?.apiId) throw new Error('expected api with apiId to be defined');
    eventApiOwnerId = resp.api.apiId;
    track(() => client.send(new DeleteApiCommand({ apiId: eventApiOwnerId })) as unknown as Promise<void>);
  });

  await r('ListApis', async () => {
    const resp = await client.send(new ListApisCommand({}));
    if (!resp.apis?.length) throw new Error('expected apis to be non-empty');
  });

  await r('ListApis_NextTokenFollowUp', async () => {
    const allApiIds: string[] = [];
    let token: string | undefined;
    let iterations = 0;
    do {
      const resp = await client.send(new ListApisCommand({
        maxResults: 1,
        nextToken: token,
      }));
      if (!resp.apis) throw new Error('expected apis to be defined');
      for (const api of resp.apis) {
        if (api.apiId) allApiIds.push(api.apiId);
      }
      token = resp.nextToken;
      iterations++;
    } while (token && iterations < 5);
    if (!allApiIds.length) throw new Error('expected apis collected via pagination');
  });

  await r('UpdateApi', async () => {
    const resp = await client.send(new UpdateApiCommand({
      apiId: eventApiId,
      name: eventApiName + '-updated',
      eventConfig: defaultEventConfig,
    }));
    if (!resp.api) throw new Error('expected api to be defined');
    if (resp.api.name !== eventApiName + '-updated') throw new Error('name not updated');
  });

  await r('UpdateApi_NonExistent', async () => {
    await assertThrows(
      () => client.send(new UpdateApiCommand({
        apiId: 'zzz-nonexistent-api-id',
        name: 'nope',
        eventConfig: defaultEventConfig,
      })),
      'NotFoundException',
    );
  });

  await r('GetApi_NonExistent', async () => {
    await assertThrows(
      () => client.send(new GetApiCommand({ apiId: 'does-not-exist' })),
      'NotFoundException',
    );
  });

  await r('ListApis_WithPagination', async () => {
    const resp = await client.send(new ListApisCommand({ maxResults: 1 }));
    if (!resp.apis || resp.apis.length !== 1) throw new Error('expected exactly 1 API');
    if (!resp.nextToken) throw new Error('expected NextToken to be present');
  });

  // ========== Channel Namespace ==========

  const evtApiForCh = makeUniqueName('evt-ch-ns');
  let evtApiForChId = '';

  try {
    const chCreateResp = await client.send(new CreateApiCommand({
      name: evtApiForCh,
      eventConfig: defaultEventConfig,
    }));
    evtApiForChId = chCreateResp.api?.apiId ?? '';
  } catch {
    evtApiForChId = '';
  }

  const chNsName = makeUniqueName('ch-ns');

  await r('CreateChannelNamespace', async () => {
    if (!evtApiForChId) throw new Error('no event api available');
    const resp = await client.send(new CreateChannelNamespaceCommand({
      apiId: evtApiForChId,
      name: chNsName,
    }));
    if (!resp.channelNamespace) throw new Error('expected channelNamespace to be defined');
  });

  await r('GetChannelNamespace', async () => {
    if (!evtApiForChId) throw new Error('no event api available');
    const resp = await client.send(new GetChannelNamespaceCommand({
      apiId: evtApiForChId,
      name: chNsName,
    }));
    if (resp.channelNamespace?.name !== chNsName) {
      throw new Error(`expected namespace ${chNsName}, got ${resp.channelNamespace?.name}`);
    }
  });

  await r('ListChannelNamespaces', async () => {
    if (!evtApiForChId) throw new Error('no event api available');
    const resp = await client.send(new ListChannelNamespacesCommand({
      apiId: evtApiForChId,
    }));
    if (!resp.channelNamespaces) throw new Error('expected channelNamespaces to be defined');
  });

  await r('UpdateChannelNamespace', async () => {
    if (!evtApiForChId) throw new Error('no event api available');
    const resp = await client.send(new UpdateChannelNamespaceCommand({
      apiId: evtApiForChId,
      name: chNsName,
    }));
    if (!resp.channelNamespace) throw new Error('expected channelNamespace to be defined');
  });

  await r('UpdateChannelNamespace_NonExistent', async () => {
    if (!evtApiForChId) throw new Error('no event api available');
    await assertThrows(
      () => client.send(new UpdateChannelNamespaceCommand({
        apiId: evtApiForChId,
        name: 'zzz-nonexistent-ch-ns',
      })),
      'NotFoundException',
    );
  });

  await r('GetChannelNamespace_NonExistent', async () => {
    if (!evtApiForChId) throw new Error('no event api available');
    await assertThrows(
      () => client.send(new GetChannelNamespaceCommand({
        apiId: evtApiForChId,
        name: 'does-not-exist',
      })),
      'NotFoundException',
    );
  });

  await r('DeleteChannelNamespace', async () => {
    if (!evtApiForChId) throw new Error('no event api available');
    await client.send(new DeleteChannelNamespaceCommand({
      apiId: evtApiForChId,
      name: chNsName,
    }));
  });

  await r('DeleteChannelNamespace_NonExistent', async () => {
    if (!evtApiForChId) throw new Error('no event api available');
    await assertThrows(
      () => client.send(new DeleteChannelNamespaceCommand({
        apiId: evtApiForChId,
        name: 'zzz-nonexistent-ch-ns-del',
      })),
      'NotFoundException',
    );
  });

  track(() => client.send(new DeleteApiCommand({ apiId: evtApiForChId })) as unknown as Promise<void>);

  // ========== Tag Operations on Event APIs ==========

  const tagApiName = makeUniqueName('tag-api');
  let tagApiId = '';
  let tagApiArn = '';

  try {
    const tagResp = await client.send(new CreateApiCommand({
      name: tagApiName,
      eventConfig: defaultEventConfig,
    }));
    tagApiId = tagResp.api?.apiId ?? '';
    tagApiArn = `arn:aws:appsync:${ctx.region}:000000000000:apis/${tagApiId}`;

    await r('ListTagsForResource', async () => {
      const resp = await client.send(new ListTagsForResourceCommand({
        resourceArn: tagApiArn,
      }));
      if (!resp.tags) throw new Error('expected tags to be defined');
    });

    await r('TagResource', async () => {
      await client.send(new TagResourceCommand({
        resourceArn: tagApiArn,
        tags: { Environment: 'Test', Owner: 'Conformance' },
      }));
      const resp = await client.send(new ListTagsForResourceCommand({
        resourceArn: tagApiArn,
      }));
      if (!resp.tags || resp.tags['Environment'] !== 'Test' || resp.tags['Owner'] !== 'Conformance') {
        throw new Error('tags not applied');
      }
    });

    await r('UntagResource', async () => {
      await client.send(new UntagResourceCommand({
        resourceArn: tagApiArn,
        tagKeys: ['Environment'],
      }));
    });

    await r('ListTagsForResource_AfterUntag', async () => {
      const resp = await client.send(new ListTagsForResourceCommand({
        resourceArn: tagApiArn,
      }));
      if (resp.tags?.['Environment']) throw new Error('Environment tag still present');
      if (resp.tags?.['Owner'] !== 'Conformance') throw new Error('Owner tag missing after untag');
    });
  } finally {
    await safeCleanup(() => client.send(new DeleteApiCommand({ apiId: tagApiId })));
  }

  // ========== Additional Tag Tests (forTagApi) ==========

  const forTagApiName = makeUniqueName('tagging-api');
  let forTagApiId = '';
  let forTagApiArn = '';

  await r('CreateApi_ForTagging', async () => {
    const resp = await client.send(new CreateApiCommand({
      name: forTagApiName,
      eventConfig: defaultEventConfig,
      tags: { key1: 'value1' },
    }));
    if (!resp.api?.apiId) throw new Error('expected api with apiId to be defined');
    forTagApiId = resp.api.apiId;
    forTagApiArn = `arn:aws:appsync:${ctx.region}:000000000000:apis/${forTagApiId}`;
    track(() => client.send(new DeleteApiCommand({ apiId: forTagApiId })) as unknown as Promise<void>);
  });

  await r('ListTagsForResource', async () => {
    const resp = await client.send(new ListTagsForResourceCommand({
      resourceArn: forTagApiArn,
    }));
    if (resp.tags?.['key1'] !== 'value1') throw new Error('key1 tag not found');
  });

  await r('TagResource', async () => {
    await client.send(new TagResourceCommand({
      resourceArn: forTagApiArn,
      tags: { key2: 'value2', key3: 'value3' },
    }));
    const resp = await client.send(new ListTagsForResourceCommand({
      resourceArn: forTagApiArn,
    }));
    if (!resp.tags) throw new Error('expected tags to be defined');
    if (Object.keys(resp.tags).length !== 3) throw new Error('expected 3 tags');
  });

  await r('UntagResource', async () => {
    await client.send(new UntagResourceCommand({
      resourceArn: forTagApiArn,
      tagKeys: ['key2'],
    }));
    const resp = await client.send(new ListTagsForResourceCommand({
      resourceArn: forTagApiArn,
    }));
    if (!resp.tags) throw new Error('expected tags to be defined');
    if (resp.tags['key2']) throw new Error('key2 should be removed');
    if (resp.tags['key1'] !== 'value1') throw new Error('key1 should remain');
    if (resp.tags['key3'] !== 'value3') throw new Error('key3 should remain');
  });

  const forTagChNsName = makeUniqueName('tagging-ch-ns');
  let forTagNsArn = '';

  await r('CreateChannelNamespace_ForTagging', async () => {
    if (!evtApiForChId) throw new Error('no event api available');
    const resp = await client.send(new CreateChannelNamespaceCommand({
      apiId: evtApiForChId,
      name: forTagChNsName,
      tags: { nsKey: 'nsValue' },
    }));
    if (!resp.channelNamespace) throw new Error('expected channelNamespace to be defined');
    if (!resp.channelNamespace.channelNamespaceArn) throw new Error('expected channelNamespaceArn');
    forTagNsArn = resp.channelNamespace.channelNamespaceArn;
    track(() => client.send(new DeleteChannelNamespaceCommand({
      apiId: evtApiForChId,
      name: forTagChNsName,
    })) as unknown as Promise<void>);
  });

  await r('TagResource_ChannelNamespace', async () => {
    await client.send(new TagResourceCommand({
      resourceArn: forTagNsArn,
      tags: { added: 'yes' },
    }));
    const resp = await client.send(new ListTagsForResourceCommand({
      resourceArn: forTagNsArn,
    }));
    if (!resp.tags) throw new Error('expected tags to be defined');
    if (resp.tags['nsKey'] !== 'nsValue') throw new Error('nsKey tag not found');
    if (resp.tags['added'] !== 'yes') throw new Error('added tag not found');
  });

  // ========== Delete Event APIs ==========

  await r('DeleteApi', async () => {
    const delResp = await client.send(new CreateApiCommand({
      name: makeUniqueName('del-api'),
      eventConfig: defaultEventConfig,
    }));
    if (!delResp.api?.apiId) throw new Error('expected api with apiId');
    const delApiId = delResp.api.apiId;
    await client.send(new DeleteApiCommand({ apiId: delApiId }));
    await assertThrows(
      () => client.send(new GetApiCommand({ apiId: delApiId })),
      'NotFoundException',
    );
  });

  await r('DeleteApi_NonExistent', async () => {
    await assertThrows(
      () => client.send(new DeleteApiCommand({ apiId: 'already-deleted' })),
      'NotFoundException',
    );
  });

  await r('DeleteApi_WithTags', async () => {
    await client.send(new DeleteApiCommand({ apiId: forTagApiId }));
  });

  await r('DeleteApi_ForTagging', async () => {
    const resp = await client.send(new CreateApiCommand({
      name: makeUniqueName('del-api-tagging'),
      eventConfig: defaultEventConfig,
      tags: { Cleanup: 'true' },
    }));
    if (!resp.api?.apiId) throw new Error('expected api with apiId');
    await client.send(new DeleteApiCommand({ apiId: resp.api.apiId }));
  });

  await r('DeleteApi_WithOwnerContact', async () => {
    const resp = await client.send(new CreateApiCommand({
      name: makeUniqueName('del-api-owner'),
      eventConfig: defaultEventConfig,
      ownerContact: 'owner@test.com',
    }));
    if (!resp.api?.apiId) throw new Error('expected api with apiId');
    await client.send(new DeleteApiCommand({ apiId: resp.api.apiId }));
  });

  await r('DeleteChannelNamespace_ForTagging', async () => {
    if (!evtApiForChId) throw new Error('no event api available');
    await client.send(new DeleteChannelNamespaceCommand({
      apiId: evtApiForChId,
      name: forTagChNsName,
    }));
  });

  for (let i = tracked.length - 1; i >= 0; i--) {
    await safeCleanup(tracked[i]);
  }

  return { results, state: { eventApiId, evtApiForChId, tracked: [] } };
}
