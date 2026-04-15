import {
  AppSyncClient,
  CreateDomainNameCommand,
  GetDomainNameCommand,
  ListDomainNamesCommand,
  UpdateDomainNameCommand,
  DeleteDomainNameCommand,
  AssociateApiCommand,
  GetApiAssociationCommand,
  DisassociateApiCommand,
  CreateGraphqlApiCommand,
  DeleteGraphqlApiCommand,
  AssociateSourceGraphqlApiCommand,
  GetSourceApiAssociationCommand,
  UpdateSourceApiAssociationCommand,
  ListSourceApiAssociationsCommand,
  StartSchemaMergeCommand,
  DisassociateSourceGraphqlApiCommand,
  AssociateMergedGraphqlApiCommand,
  DisassociateMergedGraphqlApiCommand,
} from '@aws-sdk/client-appsync';
import type { TestRunner, TestResult, ServiceContext } from '../../runner.js';
import { makeUniqueName, assertThrows, safeCleanup } from '../../helpers.js';

export async function runDomainAndAssociationTests(
  runner: TestRunner,
  ctx: ServiceContext,
  client: AppSyncClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest('appsync', name, fn));

  // ========== Domain Name CRUD ==========

  const domainName = makeUniqueName('test-domain');

  await r('CreateDomainName', async () => {
    const resp = await client.send(new CreateDomainNameCommand({
      domainName,
      certificateArn: 'arn:aws:acm:us-east-1:000000000000:certificate/test-cert',
      tags: { Env: 'Test', Service: 'AppSync' },
    }));
    if (!resp.domainNameConfig) throw new Error('expected domainNameConfig to be defined');
  });

  await r('GetDomainName', async () => {
    const resp = await client.send(new GetDomainNameCommand({ domainName }));
    if (!resp.domainNameConfig) throw new Error('expected domainNameConfig to be defined');
  });

  await r('ListDomainNames', async () => {
    const resp = await client.send(new ListDomainNamesCommand({}));
    if (!resp.domainNameConfigs) throw new Error('expected domainNameConfigs to be defined');
  });

  await r('UpdateDomainName', async () => {
    const resp = await client.send(new UpdateDomainNameCommand({
      domainName,
      description: 'Updated domain description',
    }));
    if (!resp.domainNameConfig) throw new Error('expected domainNameConfig to be defined');
  });

  await r('UpdateDomainName_NonExistent', async () => {
    await assertThrows(
      () => client.send(new UpdateDomainNameCommand({
        domainName: 'zzz-nonexistent-domain.local',
        description: 'nope',
      })),
      'NotFoundException',
    );
  });

  // ========== Domain Name Additional Tests ==========

  const forTagDomainName = makeUniqueName('ts-domain');
  let forTagApiId = '';

  try {
    const tagApiResp = await client.send(new CreateGraphqlApiCommand({
      name: makeUniqueName('domain-tag-api'),
      authenticationType: 'API_KEY',
    }));
    forTagApiId = tagApiResp.graphqlApi?.apiId ?? '';

    await r('CreateDomainName_WithTags', async () => {
      const resp = await client.send(new CreateDomainNameCommand({
        domainName: forTagDomainName,
        certificateArn: 'arn:aws:acm:us-east-1:000000000000:certificate/test-cert',
        tags: { Env: 'DomainTest' },
      }));
      if (!resp.domainNameConfig) throw new Error('expected domainNameConfig to be defined');
    });

    await r('GetDomainName_NonExistent', async () => {
      await assertThrows(
        () => client.send(new GetDomainNameCommand({ domainName: 'does-not-exist' })),
        'NotFoundException',
      );
    });

    await r('ListDomainNames_WithPagination', async () => {
      const resp = await client.send(new ListDomainNamesCommand({ maxResults: 1 }));
      if (!resp.domainNameConfigs) throw new Error('expected domainNameConfigs to be defined');
      if (!resp.domainNameConfigs.length) throw new Error('expected at least 1 domain name');
      if (!resp.nextToken) throw new Error('expected NextToken to be present');
    });

    // ========== API Association ==========

    await r('AssociateApi', async () => {
      const resp = await client.send(new AssociateApiCommand({
        domainName: forTagDomainName,
        apiId: forTagApiId,
      }));
      if (!resp.apiAssociation) throw new Error('expected apiAssociation to be defined');
    });

    await r('GetApiAssociation', async () => {
      const resp = await client.send(new GetApiAssociationCommand({
        domainName: forTagDomainName,
      }));
      if (!resp.apiAssociation) throw new Error('expected apiAssociation to be defined');
    });

    await r('DisassociateApi', async () => {
      await client.send(new DisassociateApiCommand({ domainName: forTagDomainName }));
      await assertThrows(
        () => client.send(new GetApiAssociationCommand({ domainName: forTagDomainName })),
        'NotFoundException',
      );
    });

    await r('DisassociateApi_NotAssociated', async () => {
      await assertThrows(
        () => client.send(new DisassociateApiCommand({ domainName: forTagDomainName })),
        'NotFoundException',
      );
    });

    await r('DeleteDomainName_NonExistent', async () => {
      await assertThrows(
        () => client.send(new DeleteDomainNameCommand({ domainName: 'does-not-exist' })),
        'NotFoundException',
      );
    });

    await r('DeleteDomainName_WithTags', async () => {
      await client.send(new DeleteDomainNameCommand({ domainName: forTagDomainName }));
    });
  } finally {
    await safeCleanup(() => client.send(new DeleteGraphqlApiCommand({ apiId: forTagApiId })) as unknown as Promise<void>);
  }

  await r('DeleteDomainName', async () => {
    await client.send(new DeleteDomainNameCommand({ domainName }));
    await assertThrows(
      () => client.send(new GetDomainNameCommand({ domainName })),
      'NotFoundException',
    );
  });

  // ========== Merged API / Source API Associations ==========

  const mergedApiName = makeUniqueName('merged-api');
  let mergedApiId = '';

  const sourceApi1Name = makeUniqueName('source-api-1');
  let sourceApi1Id = '';

  const sourceApi2Name = makeUniqueName('source-api-2');
  let sourceApi2Id = '';

  await r('CreateGraphqlApi_ForMerged', async () => {
    const resp = await client.send(new CreateGraphqlApiCommand({
      name: mergedApiName,
      authenticationType: 'API_KEY',
      apiType: 'MERGED',
    }));
    if (!resp.graphqlApi?.apiId) throw new Error('expected graphqlApi with apiId');
    if (resp.graphqlApi.apiType !== 'MERGED') throw new Error(`expected MERGED api type, got ${resp.graphqlApi.apiType}`);
    mergedApiId = resp.graphqlApi.apiId;
  });

  await r('CreateGraphqlApi_ForSource', async () => {
    const resp = await client.send(new CreateGraphqlApiCommand({
      name: sourceApi2Name,
      authenticationType: 'API_KEY',
    }));
    if (!resp.graphqlApi?.apiId) throw new Error('expected graphqlApi with apiId');
    sourceApi2Id = resp.graphqlApi.apiId;
  });

  try {
    let associationId = '';

    await r('AssociateSourceGraphqlApi', async () => {
      const resp = await client.send(new AssociateSourceGraphqlApiCommand({
        mergedApiIdentifier: mergedApiId,
        sourceApiIdentifier: sourceApi2Id,
      }));
      if (!resp.sourceApiAssociation?.associationId) throw new Error('expected sourceApiAssociation with associationId');
      associationId = resp.sourceApiAssociation.associationId;
    });

    await r('GetSourceApiAssociation', async () => {
      const resp = await client.send(new GetSourceApiAssociationCommand({
        mergedApiIdentifier: mergedApiId,
        associationId,
      }));
      if (!resp.sourceApiAssociation) throw new Error('expected sourceApiAssociation to be defined');
    });

    await r('StartSchemaMerge', async () => {
      await client.send(new StartSchemaMergeCommand({
        mergedApiIdentifier: mergedApiId,
        associationId,
      }));
    });

    await r('DisassociateSourceGraphqlApi', async () => {
      await client.send(new DisassociateSourceGraphqlApiCommand({
        mergedApiIdentifier: mergedApiId,
        associationId,
      }));
    });

    await r('GetSourceApiAssociation_NonExistent', async () => {
      await assertThrows(
        () => client.send(new GetSourceApiAssociationCommand({
          mergedApiIdentifier: 'does-not-exist',
          associationId: 'does-not-exist',
        })),
        'NotFoundException',
      );
    });
  } finally {
    await safeCleanup(() => client.send(new DeleteGraphqlApiCommand({ apiId: mergedApiId })) as unknown as Promise<void>);
    await safeCleanup(() => client.send(new DeleteGraphqlApiCommand({ apiId: sourceApi1Id })) as unknown as Promise<void>);
    await safeCleanup(() => client.send(new DeleteGraphqlApiCommand({ apiId: sourceApi2Id })) as unknown as Promise<void>);
  }

  // ========== Update/Disassociate Merged/Source API ==========

  const updMergedName = makeUniqueName('upd-merged-api');
  let updMergedId = '';
  const updSourceName = makeUniqueName('upd-source-api');
  let updSourceId = '';
  let updAssociationId = '';

  try {
    const updMergedResp = await client.send(new CreateGraphqlApiCommand({
      name: updMergedName,
      authenticationType: 'API_KEY',
      mergedApiExecutionRoleArn: 'arn:aws:iam::000000000000:role/AppSyncMergedRole',
      visibility: 'GLOBAL',
    }));
    if (!updMergedResp.graphqlApi?.apiId) throw new Error('expected merged api with apiId');
    updMergedId = updMergedResp.graphqlApi.apiId;

    const updSourceResp = await client.send(new CreateGraphqlApiCommand({
      name: updSourceName,
      authenticationType: 'API_KEY',
    }));
    if (!updSourceResp.graphqlApi?.apiId) throw new Error('expected source api with apiId');
    updSourceId = updSourceResp.graphqlApi.apiId;

    const assocResp = await client.send(new AssociateSourceGraphqlApiCommand({
      mergedApiIdentifier: updMergedId,
      sourceApiIdentifier: updSourceId,
      description: 'test association for update',
    }));
    if (!assocResp.sourceApiAssociation?.associationId) throw new Error('expected sourceApiAssociation with associationId');
    updAssociationId = assocResp.sourceApiAssociation.associationId;

    await r('UpdateSourceApiAssociation', async () => {
      const resp = await client.send(new UpdateSourceApiAssociationCommand({
        mergedApiIdentifier: updMergedId,
        associationId: updAssociationId,
        description: 'updated association',
      }));
      if (!resp.sourceApiAssociation) throw new Error('expected sourceApiAssociation to be defined');
      if (resp.sourceApiAssociation.description !== 'updated association') throw new Error('description not updated');
    });

    await r('ListSourceApiAssociations', async () => {
      const resp = await client.send(new ListSourceApiAssociationsCommand({
        apiId: updSourceId,
      }));
      if (!resp.sourceApiAssociationSummaries?.length) throw new Error('expected at least 1 association');
    });

    await r('DisassociateSourceGraphqlApi_NonExistent', async () => {
      await assertThrows(
        () => client.send(new DisassociateSourceGraphqlApiCommand({
          mergedApiIdentifier: updMergedId,
          associationId: 'already-deleted',
        })),
        'NotFoundException',
      );
    });

    await client.send(new DisassociateSourceGraphqlApiCommand({
      mergedApiIdentifier: updMergedId,
      associationId: updAssociationId,
    }));

    let mergedAssocId = '';

    await r('AssociateMergedGraphqlApi', async () => {
      const resp = await client.send(new AssociateMergedGraphqlApiCommand({
        sourceApiIdentifier: updSourceId,
        mergedApiIdentifier: updMergedId,
        description: 'merged from source side',
      }));
      if (!resp.sourceApiAssociation?.associationId) throw new Error('expected associationId');
      mergedAssocId = resp.sourceApiAssociation.associationId;
    });

    await r('DisassociateMergedGraphqlApi', async () => {
      const resp = await client.send(new DisassociateMergedGraphqlApiCommand({
        sourceApiIdentifier: updSourceId,
        associationId: mergedAssocId,
      }));
      if (!resp.sourceApiAssociationStatus) throw new Error('expected non-empty status');
    });

    await r('DisassociateMergedGraphqlApi_NonExistent', async () => {
      await assertThrows(
        () => client.send(new DisassociateMergedGraphqlApiCommand({
          sourceApiIdentifier: updSourceId,
          associationId: 'already-deleted',
        })),
        'NotFoundException',
      );
    });
  } finally {
    await safeCleanup(() => client.send(new DeleteGraphqlApiCommand({ apiId: updMergedId })) as unknown as Promise<void>);
    await safeCleanup(() => client.send(new DeleteGraphqlApiCommand({ apiId: updSourceId })) as unknown as Promise<void>);
  }

  return results;
}
