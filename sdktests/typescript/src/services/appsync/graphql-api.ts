import {
  AppSyncClient,
  CreateGraphqlApiCommand,
  GetGraphqlApiCommand,
  ListGraphqlApisCommand,
  UpdateGraphqlApiCommand,
  CreateDataSourceCommand,
  GetDataSourceCommand,
  ListDataSourcesCommand,
  UpdateDataSourceCommand,
  DeleteDataSourceCommand,
  CreateTypeCommand,
  GetTypeCommand,
  ListTypesCommand,
  UpdateTypeCommand,
  DeleteTypeCommand,
  CreateResolverCommand,
  GetResolverCommand,
  ListResolversCommand,
  UpdateResolverCommand,
  DeleteResolverCommand,
  CreateFunctionCommand,
  GetFunctionCommand,
  ListFunctionsCommand,
  UpdateFunctionCommand,
  DeleteFunctionCommand,
  ListResolversByFunctionCommand,
  DeleteGraphqlApiCommand,
} from '@aws-sdk/client-appsync';
import type { TestRunner, TestResult, ServiceContext } from '../../runner.js';
import { makeUniqueName, assertThrows, safeCleanup } from '../../helpers.js';

export interface GraphqlApiState {
  gqlApiId: string;
  gqlApiWithTagsId: string;
  gqlApiOIDCId: string;
}

const postTypeSDL = 'type Post { id: ID! title: String! content: String }';
const queryTypeSDL = 'type Query { getPost(id: ID!): Post }';

export async function runGraphqlApiTests(
  runner: TestRunner,
  ctx: ServiceContext,
  client: AppSyncClient,
): Promise<{ results: TestResult[]; state: GraphqlApiState }> {
  const results: TestResult[] = [];
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest('appsync', name, fn));

  const gqlApiName = makeUniqueName('gql-api');
  let gqlApiId = '';

  await r('CreateGraphqlApi', async () => {
    const resp = await client.send(new CreateGraphqlApiCommand({
      name: gqlApiName,
      authenticationType: 'API_KEY',
    }));
    if (!resp.graphqlApi?.apiId) throw new Error('expected graphqlApi with apiId');
    gqlApiId = resp.graphqlApi.apiId;
  });

  const gqlApiWithTags = makeUniqueName('gql-api-tags');
  let gqlApiWithTagsId = '';
  await r('CreateGraphqlApi_WithTags', async () => {
    const resp = await client.send(new CreateGraphqlApiCommand({
      name: gqlApiWithTags,
      authenticationType: 'API_KEY',
      tags: { Env: 'Test' },
    }));
    if (!resp.graphqlApi?.apiId) throw new Error('expected graphqlApi with apiId');
    gqlApiWithTagsId = resp.graphqlApi.apiId;
  });

  const gqlApiOIDC = makeUniqueName('gql-api-oidc');
  let gqlApiOIDCId = '';

  await r('GetGraphqlApi', async () => {
    const resp = await client.send(new GetGraphqlApiCommand({ apiId: gqlApiId }));
    if (!resp.graphqlApi) throw new Error('expected graphqlApi to be defined');
    if (resp.graphqlApi.name !== gqlApiName) throw new Error('name mismatch');
  });

  await r('GetGraphqlApi_NonExistent', async () => {
    await assertThrows(
      () => client.send(new GetGraphqlApiCommand({ apiId: 'does-not-exist' })),
      'NotFoundException',
    );
  });

  await r('ListGraphqlApis', async () => {
    const resp = await client.send(new ListGraphqlApisCommand({}));
    if (!resp.graphqlApis?.length) throw new Error('expected graphqlApis to be non-empty');
  });

  await r('ListGraphqlApis_WithPagination', async () => {
    const resp = await client.send(new ListGraphqlApisCommand({ maxResults: 1 }));
    if (!resp.graphqlApis || resp.graphqlApis.length !== 1) throw new Error('expected exactly 1 GraphQL API');
    if (!resp.nextToken) throw new Error('expected NextToken to be present');
  });

  await r('UpdateGraphqlApi', async () => {
    const resp = await client.send(new UpdateGraphqlApiCommand({
      apiId: gqlApiId,
      name: gqlApiName + '-updated',
      authenticationType: 'API_KEY',
    }));
    if (!resp.graphqlApi) throw new Error('expected graphqlApi to be defined');
  });

  await r('UpdateGraphqlApi_NonExistent', async () => {
    await assertThrows(
      () => client.send(new UpdateGraphqlApiCommand({
        apiId: 'zzz-nonexistent-gql-id',
        name: 'nope',
        authenticationType: 'API_KEY',
      })),
      'NotFoundException',
    );
  });

  // ========== DataSource CRUD ==========

  const dsNoneName = makeUniqueName('ds-none');

  await r('CreateDataSource', async () => {
    const resp = await client.send(new CreateDataSourceCommand({
      apiId: gqlApiId,
      name: dsNoneName,
      type: 'NONE',
    }));
    if (!resp.dataSource) throw new Error('expected dataSource to be defined');
    if (resp.dataSource.name !== dsNoneName) throw new Error(`expected name ${dsNoneName}, got ${resp.dataSource.name}`);
    if (resp.dataSource.type !== 'NONE') throw new Error(`expected NONE type, got ${resp.dataSource.type}`);
    if (!resp.dataSource.dataSourceArn) throw new Error('expected dataSourceArn to be defined');
  });

  const dsDescName = makeUniqueName('ds-desc');
  await r('CreateDataSource_WithDescription', async () => {
    const resp = await client.send(new CreateDataSourceCommand({
      apiId: gqlApiId,
      name: dsDescName,
      type: 'NONE',
      description: 'Test data source with description',
    }));
    if (!resp.dataSource) throw new Error('expected dataSource to be defined');
    if (resp.dataSource.description !== 'Test data source with description') throw new Error('description mismatch');
  });

  await r('GetDataSource', async () => {
    const resp = await client.send(new GetDataSourceCommand({
      apiId: gqlApiId,
      name: dsNoneName,
    }));
    if (!resp.dataSource) throw new Error('expected dataSource to be defined');
    if (resp.dataSource.name !== dsNoneName) throw new Error('name mismatch');
  });

  await r('ListDataSources', async () => {
    const resp = await client.send(new ListDataSourcesCommand({ apiId: gqlApiId }));
    if (!resp.dataSources?.length) throw new Error('expected dataSources to be non-empty');
  });

  await r('UpdateDataSource', async () => {
    const resp = await client.send(new UpdateDataSourceCommand({
      apiId: gqlApiId,
      name: dsNoneName,
      type: 'NONE',
      description: 'Updated description',
    }));
    if (!resp.dataSource) throw new Error('expected dataSource to be defined');
    if (resp.dataSource.description !== 'Updated description') throw new Error('description not updated');
  });

  await r('DeleteDataSource', async () => {
    await client.send(new DeleteDataSourceCommand({
      apiId: gqlApiId,
      name: dsNoneName,
    }));
    await assertThrows(
      () => client.send(new GetDataSourceCommand({ apiId: gqlApiId, name: dsNoneName })),
      'NotFoundException',
    );
  });

  await r('GetDataSource_NonExistent', async () => {
    await assertThrows(
      () => client.send(new GetDataSourceCommand({
        apiId: gqlApiId,
        name: 'zzz-nonexistent-ds',
      })),
      'NotFoundException',
    );
  });

  await r('DeleteDataSource_NonExistent', async () => {
    await assertThrows(
      () => client.send(new DeleteDataSourceCommand({
        apiId: gqlApiId,
        name: 'zzz-nonexistent-ds',
      })),
      'NotFoundException',
    );
  });

  // ========== Type CRUD ==========

  const typeName = 'Post';

  await r('CreateType', async () => {
    const resp = await client.send(new CreateTypeCommand({
      apiId: gqlApiId,
      definition: postTypeSDL,
      format: 'SDL',
    }));
    if (!resp.type) throw new Error('expected type to be defined');
  });

  await r('GetType', async () => {
    const resp = await client.send(new GetTypeCommand({
      apiId: gqlApiId,
      typeName,
      format: 'SDL',
    }));
    if (!resp.type) throw new Error('expected type to be defined');
  });

  await r('ListTypes', async () => {
    const resp = await client.send(new ListTypesCommand({
      apiId: gqlApiId,
      format: 'SDL',
    }));
    if (!resp.types?.length) throw new Error('expected types to be non-empty');
  });

  await r('UpdateType', async () => {
    const updatedSDL = 'type Post { id: ID! title: String! content: String author: String }';
    const resp = await client.send(new UpdateTypeCommand({
      apiId: gqlApiId,
      typeName,
      definition: updatedSDL,
      format: 'SDL',
    }));
    if (!resp.type) throw new Error('expected type to be defined');
  });

  await r('DeleteType', async () => {
    await client.send(new DeleteTypeCommand({
      apiId: gqlApiId,
      typeName,
    }));
    await assertThrows(
      () => client.send(new GetTypeCommand({ apiId: gqlApiId, typeName, format: 'SDL' })),
      'NotFoundException',
    );
  });

  await r('GetType_NonExistent', async () => {
    await assertThrows(
      () => client.send(new GetTypeCommand({
        apiId: gqlApiId,
        typeName: 'NonExistentType',
        format: 'SDL',
      })),
      'NotFoundException',
    );
  });

  await r('DeleteType_NonExistent', async () => {
    await assertThrows(
      () => client.send(new DeleteTypeCommand({
        apiId: gqlApiId,
        typeName: 'NonExistentType',
      })),
      'NotFoundException',
    );
  });

  // ========== Resolver CRUD ==========

  const resolverTypeName = 'Query';
  const resolverFieldName = 'getPost';
  const resolverDsName = makeUniqueName('ds-resolver');

  try {
    await client.send(new CreateDataSourceCommand({
      apiId: gqlApiId,
      name: resolverDsName,
      type: 'NONE',
    }));

    await client.send(new CreateTypeCommand({
      apiId: gqlApiId,
      definition: queryTypeSDL,
      format: 'SDL',
    }));

    await r('CreateResolver', async () => {
      const resp = await client.send(new CreateResolverCommand({
        apiId: gqlApiId,
        typeName: resolverTypeName,
        fieldName: resolverFieldName,
        dataSourceName: resolverDsName,
        kind: 'UNIT',
        runtime: { name: 'APPSYNC_JS', runtimeVersion: '1.0.0' },
        code: 'export function request(ctx) { return {} } export function response(ctx) { return ctx.result }',
      }));
      if (!resp.resolver) throw new Error('expected resolver to be defined');
    });

    await r('GetResolver', async () => {
      const resp = await client.send(new GetResolverCommand({
        apiId: gqlApiId,
        typeName: resolverTypeName,
        fieldName: resolverFieldName,
      }));
      if (!resp.resolver) throw new Error('expected resolver to be defined');
    });

    await r('ListResolvers', async () => {
      const resp = await client.send(new ListResolversCommand({
        apiId: gqlApiId,
        typeName: resolverTypeName,
      }));
      if (!resp.resolvers?.length) throw new Error('expected resolvers to be non-empty');
    });

    await r('UpdateResolver', async () => {
      const resp = await client.send(new UpdateResolverCommand({
        apiId: gqlApiId,
        typeName: resolverTypeName,
        fieldName: resolverFieldName,
        kind: 'UNIT',
        runtime: { name: 'APPSYNC_JS', runtimeVersion: '1.0.0' },
        code: 'export function request(ctx) { return { version: "2018-05-29", payload: {} } } export function response(ctx) { return ctx.result }',
      }));
      if (!resp.resolver) throw new Error('expected resolver to be defined');
    });

    await r('DeleteResolver', async () => {
      await client.send(new DeleteResolverCommand({
        apiId: gqlApiId,
        typeName: resolverTypeName,
        fieldName: resolverFieldName,
      }));
      await assertThrows(
        () => client.send(new GetResolverCommand({ apiId: gqlApiId, typeName: resolverTypeName, fieldName: resolverFieldName })),
        'NotFoundException',
      );
    });

    await r('GetResolver_NonExistent', async () => {
      await assertThrows(
        () => client.send(new GetResolverCommand({
          apiId: gqlApiId,
          typeName: 'Query',
          fieldName: 'nonExistentField',
        })),
        'NotFoundException',
      );
    });

    await r('DeleteResolver_NonExistent', async () => {
      await assertThrows(
        () => client.send(new DeleteResolverCommand({
          apiId: gqlApiId,
          typeName: 'Query',
          fieldName: 'nonExistentField',
        })),
        'NotFoundException',
      );
    });
  } finally {
    await safeCleanup(() => client.send(new DeleteDataSourceCommand({ apiId: gqlApiId, name: resolverDsName })));
    await safeCleanup(() => client.send(new DeleteTypeCommand({ apiId: gqlApiId, typeName: 'Query' })));
  }

  // ========== Function CRUD ==========

  const funcDsName = makeUniqueName('ds-func');
  const funcName = makeUniqueName('test-func');
  let functionId = '';

  try {
    await client.send(new CreateDataSourceCommand({
      apiId: gqlApiId,
      name: funcDsName,
      type: 'NONE',
    }));

    await r('CreateFunction', async () => {
      const resp = await client.send(new CreateFunctionCommand({
        apiId: gqlApiId,
        name: funcName,
        dataSourceName: funcDsName,
        runtime: { name: 'APPSYNC_JS', runtimeVersion: '1.0.0' },
        code: 'export function request(ctx) { return {} } export function response(ctx) { return ctx.result }',
      }));
      if (!resp.functionConfiguration) throw new Error('expected functionConfiguration to be defined');
      if (!resp.functionConfiguration.functionId) throw new Error('expected functionId');
      functionId = resp.functionConfiguration.functionId;
    });

    await r('GetFunction', async () => {
      const resp = await client.send(new GetFunctionCommand({
        apiId: gqlApiId,
        functionId,
      }));
      if (!resp.functionConfiguration) throw new Error('expected functionConfiguration to be defined');
    });

    await r('ListFunctions', async () => {
      const resp = await client.send(new ListFunctionsCommand({ apiId: gqlApiId }));
      if (!resp.functions?.length) throw new Error('expected functions to be non-empty');
    });

    await r('UpdateFunction', async () => {
      const resp = await client.send(new UpdateFunctionCommand({
        apiId: gqlApiId,
        functionId,
        name: funcName,
        dataSourceName: funcDsName,
        runtime: { name: 'APPSYNC_JS', runtimeVersion: '1.0.0' },
        code: 'export function request(ctx) { return { version: "2018-05-29" } } export function response(ctx) { return ctx.result }',
      }));
      if (!resp.functionConfiguration) throw new Error('expected functionConfiguration to be defined');
    });

    await r('ListResolversByFunction', async () => {
      const resp = await client.send(new ListResolversByFunctionCommand({
        apiId: gqlApiId,
        functionId,
      }));
      if (!resp.resolvers) throw new Error('expected resolvers to be defined');
    });

    await r('DeleteFunction', async () => {
      await client.send(new DeleteFunctionCommand({
        apiId: gqlApiId,
        functionId,
      }));
      await assertThrows(
        () => client.send(new GetFunctionCommand({ apiId: gqlApiId, functionId })),
        'NotFoundException',
      );
    });

    await r('GetFunction_NonExistent', async () => {
      await assertThrows(
        () => client.send(new GetFunctionCommand({
          apiId: gqlApiId,
          functionId: 'zzz-nonexistent-func',
        })),
        'NotFoundException',
      );
    });

    await r('DeleteFunction_NonExistent', async () => {
      await assertThrows(
        () => client.send(new DeleteFunctionCommand({
          apiId: gqlApiId,
          functionId: 'zzz-nonexistent-func',
        })),
        'NotFoundException',
      );
    });
  } finally {
    await safeCleanup(() => client.send(new DeleteDataSourceCommand({ apiId: gqlApiId, name: funcDsName })));
  }

  // ========== Delete GraphQL APIs ==========

  await r('DeleteGraphqlApi', async () => {
    const resp = await client.send(new CreateGraphqlApiCommand({
      name: makeUniqueName('del-gql-api'),
      authenticationType: 'API_KEY',
    }));
    if (!resp.graphqlApi?.apiId) throw new Error('expected graphqlApi with apiId');
    await client.send(new DeleteGraphqlApiCommand({ apiId: resp.graphqlApi.apiId }));
    await assertThrows(
      () => client.send(new GetGraphqlApiCommand({ apiId: resp.graphqlApi!.apiId! })),
      'NotFoundException',
    );
  });

  await r('DeleteGraphqlApi_WithTags', async () => {
    const resp = await client.send(new CreateGraphqlApiCommand({
      name: makeUniqueName('del-gql-tags'),
      authenticationType: 'API_KEY',
      tags: { DeleteMe: 'yes' },
    }));
    if (!resp.graphqlApi?.apiId) throw new Error('expected graphqlApi with apiId');
    await client.send(new DeleteGraphqlApiCommand({ apiId: resp.graphqlApi.apiId }));
  });

  await r('DeleteGraphqlApi_NonExistent', async () => {
    await assertThrows(
      () => client.send(new DeleteGraphqlApiCommand({ apiId: 'does-not-exist' })),
      'NotFoundException',
    );
  });

  await r('DeleteGraphqlApi_Merged', async () => {
    const resp = await client.send(new CreateGraphqlApiCommand({
      name: makeUniqueName('del-gql-merged'),
      authenticationType: 'API_KEY',
      mergedApiExecutionRoleArn: 'arn:aws:iam::000000000000:role/AppSyncMergedRole',
      visibility: 'GLOBAL',
    }));
    if (!resp.graphqlApi?.apiId) throw new Error('expected graphqlApi with apiId');
    await client.send(new DeleteGraphqlApiCommand({ apiId: resp.graphqlApi.apiId }));
  });

  await r('DeleteGraphqlApi_Source', async () => {
    const resp = await client.send(new CreateGraphqlApiCommand({
      name: makeUniqueName('del-gql-source'),
      authenticationType: 'API_KEY',
    }));
    if (!resp.graphqlApi?.apiId) throw new Error('expected graphqlApi with apiId');
    await client.send(new DeleteGraphqlApiCommand({ apiId: resp.graphqlApi.apiId }));
  });

  // Cleanup shared GraphQL APIs
  await safeCleanup(() => client.send(new DeleteGraphqlApiCommand({ apiId: gqlApiWithTagsId })) as unknown as Promise<void>);
  await safeCleanup(() => client.send(new DeleteGraphqlApiCommand({ apiId: gqlApiOIDCId })) as unknown as Promise<void>);

  return { results, state: { gqlApiId, gqlApiWithTagsId, gqlApiOIDCId } };
}
