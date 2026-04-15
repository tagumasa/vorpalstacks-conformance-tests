import {
  AppSyncClient,
  CreateGraphqlApiCommand,
  DeleteGraphqlApiCommand,
  StartSchemaCreationCommand,
  GetSchemaCreationStatusCommand,
  GetIntrospectionSchemaCommand,
  PutGraphqlApiEnvironmentVariablesCommand,
  GetGraphqlApiEnvironmentVariablesCommand,
  CreateApiKeyCommand,
  ListApiKeysCommand,
  UpdateApiKeyCommand,
  DeleteApiKeyCommand,
  CreateApiCacheCommand,
  GetApiCacheCommand,
  UpdateApiCacheCommand,
  FlushApiCacheCommand,
  DeleteApiCacheCommand,
} from '@aws-sdk/client-appsync';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, assertThrows, safeCleanup } from '../../helpers.js';

const queryTypeSDL = 'type Query { getPost(id: ID!): Post }';
const mutationTypeSDL = 'type Mutation { createPost(title: String!, content: String): Post }';
const postTypeSDL = 'type Post { id: ID! title: String! content: String }';

export async function runSchemaAndCacheTests(
  runner: TestRunner,
  client: AppSyncClient,
  gqlApiId: string,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest('appsync', name, fn));

  // ========== Schema ==========

  const schemaGqlName = makeUniqueName('gql-schema');
  let schemaGqlId = '';

  try {
    const schemaResp = await client.send(new CreateGraphqlApiCommand({
      name: schemaGqlName,
      authenticationType: 'API_KEY',
    }));
    schemaGqlId = schemaResp.graphqlApi?.apiId ?? '';

    const fullSchema = `${queryTypeSDL}\n${mutationTypeSDL}\n${postTypeSDL}`;

    await r('StartSchemaCreation', async () => {
      const resp = await client.send(new StartSchemaCreationCommand({
        apiId: schemaGqlId,
        definition: Buffer.from(fullSchema),
      }));
      if (resp.status !== 'PROCESSING' && resp.status !== 'SUCCESS') {
        throw new Error(`unexpected status: ${resp.status}`);
      }
    });

    await r('GetSchemaCreationStatus', async () => {
      const resp = await client.send(new GetSchemaCreationStatusCommand({
        apiId: schemaGqlId,
      }));
      if (!resp.status) throw new Error('expected status to be defined');
    });

    await r('GetIntrospectionSchema', async () => {
      const resp = await client.send(new GetIntrospectionSchemaCommand({
        apiId: schemaGqlId,
        format: 'SDL',
      }));
      if (!resp.schema) throw new Error('expected schema to be defined');
      const schemaStr = Buffer.from(resp.schema).toString('utf-8');
      if (!schemaStr.includes('type Query')) throw new Error('Query type not found in introspection');
      if (!schemaStr.includes('type Mutation')) throw new Error('Mutation type not found in introspection');
      if (!schemaStr.includes('Post')) throw new Error('Post type not found in introspection');
    });
  } finally {
    await safeCleanup(() => client.send(new DeleteGraphqlApiCommand({ apiId: schemaGqlId })) as unknown as Promise<void>);
  }

  // ========== Environment Variables ==========

  const envVarGqlName = makeUniqueName('gql-envvar');
  let envVarGqlId = '';

  try {
    const envResp = await client.send(new CreateGraphqlApiCommand({
      name: envVarGqlName,
      authenticationType: 'API_KEY',
    }));
    envVarGqlId = envResp.graphqlApi?.apiId ?? '';

    await r('PutGraphqlApiEnvironmentVariables', async () => {
      const resp = await client.send(new PutGraphqlApiEnvironmentVariablesCommand({
        apiId: envVarGqlId,
        environmentVariables: {
          ENDPOINT_URL: 'https://example.com',
          TABLE_NAME: 'MyTable',
        },
      }));
      if (!resp.environmentVariables) throw new Error('expected environmentVariables to be defined');
    });

    await r('GetGraphqlApiEnvironmentVariables', async () => {
      const resp = await client.send(new GetGraphqlApiEnvironmentVariablesCommand({
        apiId: envVarGqlId,
      }));
      if (!resp.environmentVariables) throw new Error('expected environmentVariables to be defined');
      if (resp.environmentVariables['ENDPOINT_URL'] !== 'https://example.com') {
        throw new Error('ENDPOINT_URL mismatch');
      }
    });

    await r('PutGraphqlApiEnvironmentVariables_Replace', async () => {
      await client.send(new PutGraphqlApiEnvironmentVariablesCommand({
        apiId: envVarGqlId,
        environmentVariables: { NEW_VAR: 'new-value' },
      }));
      const resp = await client.send(new GetGraphqlApiEnvironmentVariablesCommand({
        apiId: envVarGqlId,
      }));
      if (!resp.environmentVariables) throw new Error('expected environmentVariables to be defined');
      if (resp.environmentVariables['NEW_VAR'] !== 'new-value') {
        throw new Error('NEW_VAR mismatch');
      }
    });
  } finally {
    await safeCleanup(() => client.send(new DeleteGraphqlApiCommand({ apiId: envVarGqlId })) as unknown as Promise<void>);
  }

  // ========== API Key CRUD ==========

  const apiKeyGqlName = makeUniqueName('gql-apikey');
  let apiKeyGqlId = '';
  let createdApiKey = '';

  try {
    const apiKeyResp = await client.send(new CreateGraphqlApiCommand({
      name: apiKeyGqlName,
      authenticationType: 'API_KEY',
    }));
    apiKeyGqlId = apiKeyResp.graphqlApi?.apiId ?? '';

    await r('CreateApiKey', async () => {
      const resp = await client.send(new CreateApiKeyCommand({
        apiId: apiKeyGqlId,
        description: 'Test API key for conformance',
      }));
      if (!resp.apiKey?.id) throw new Error('expected apiKey with id');
      createdApiKey = resp.apiKey.id;
    });

    await r('ListApiKeys', async () => {
      const resp = await client.send(new ListApiKeysCommand({ apiId: apiKeyGqlId }));
      if (!resp.apiKeys?.length) throw new Error('expected apiKeys to be non-empty');
    });

    await r('UpdateApiKey', async () => {
      const resp = await client.send(new UpdateApiKeyCommand({
        apiId: apiKeyGqlId,
        id: createdApiKey,
        description: 'Updated API key description',
      }));
      if (!resp.apiKey) throw new Error('expected apiKey to be defined');
      if (resp.apiKey.description !== 'Updated API key description') throw new Error('description not updated');
    });

    await r('DeleteApiKey', async () => {
      await client.send(new DeleteApiKeyCommand({
        apiId: apiKeyGqlId,
        id: createdApiKey,
      }));
      createdApiKey = '';
    });

    await r('UpdateApiKey_NonExistent', async () => {
      await assertThrows(
        () => client.send(new UpdateApiKeyCommand({
          apiId: apiKeyGqlId,
          id: 'zzz-nonexistent-key-id',
          description: 'nope',
        })),
        'NotFoundException',
      );
    });

    await r('DeleteApiKey_NonExistent', async () => {
      await assertThrows(
        () => client.send(new DeleteApiKeyCommand({
          apiId: apiKeyGqlId,
          id: 'zzz-nonexistent-key-id',
        })),
        'NotFoundException',
      );
    });
  } finally {
    await safeCleanup(() => client.send(new DeleteGraphqlApiCommand({ apiId: apiKeyGqlId })) as unknown as Promise<void>);
  }

  // ========== Additional API Key tests using shared gqlApiId ==========

  await r('CreateApiKey_WithDescription', async () => {
    if (!gqlApiId) throw new Error('no gql api id');
    const resp = await client.send(new CreateApiKeyCommand({
      apiId: gqlApiId,
      description: 'test key with description',
    }));
    if (!resp.apiKey) throw new Error('expected apiKey to be defined');
    if (resp.apiKey.description !== 'test key with description') throw new Error('description not set');
  });

  let pagKey1 = '';
  let pagKey2 = '';
  try {
    const k1 = await client.send(new CreateApiKeyCommand({ apiId: gqlApiId, description: 'pag-key-1' }));
    pagKey1 = k1.apiKey?.id ?? '';
    const k2 = await client.send(new CreateApiKeyCommand({ apiId: gqlApiId, description: 'pag-key-2' }));
    pagKey2 = k2.apiKey?.id ?? '';

    await r('ListApiKeys_WithPagination', async () => {
      const resp = await client.send(new ListApiKeysCommand({ apiId: gqlApiId, maxResults: 1 }));
      if (!resp.apiKeys || resp.apiKeys.length !== 1) throw new Error('expected exactly 1 API key');
      if (!resp.nextToken) throw new Error('expected NextToken to be present');
    });
  } finally {
    await safeCleanup(() => client.send(new DeleteApiKeyCommand({ apiId: gqlApiId, id: pagKey1 })) as unknown as Promise<void>);
    await safeCleanup(() => client.send(new DeleteApiKeyCommand({ apiId: gqlApiId, id: pagKey2 })) as unknown as Promise<void>);
  }

  // ========== Cache CRUD ==========

  const cacheGqlName = makeUniqueName('gql-cache');
  let cacheGqlId = '';

  try {
    const cacheResp = await client.send(new CreateGraphqlApiCommand({
      name: cacheGqlName,
      authenticationType: 'API_KEY',
    }));
    cacheGqlId = cacheResp.graphqlApi?.apiId ?? '';

    await r('CreateApiCache', async () => {
      const resp = await client.send(new CreateApiCacheCommand({
        apiId: cacheGqlId,
        ttl: 300,
        type: 'SMALL',
        apiCachingBehavior: 'FULL_REQUEST_CACHING',
      }));
      if (!resp.apiCache) throw new Error('expected apiCache to be defined');
      if (resp.apiCache.ttl !== 300) throw new Error(`expected Ttl 300, got ${resp.apiCache.ttl}`);
    });

    await r('GetApiCache', async () => {
      const resp = await client.send(new GetApiCacheCommand({ apiId: cacheGqlId }));
      if (!resp.apiCache) throw new Error('expected apiCache to be defined');
    });

    await r('UpdateApiCache', async () => {
      const resp = await client.send(new UpdateApiCacheCommand({
        apiId: cacheGqlId,
        ttl: 600,
        type: 'SMALL',
        apiCachingBehavior: 'FULL_REQUEST_CACHING',
      }));
      if (!resp.apiCache) throw new Error('expected apiCache to be defined');
      if (resp.apiCache.ttl !== 600) throw new Error(`expected Ttl 600, got ${resp.apiCache.ttl}`);
    });

    await r('FlushApiCache', async () => {
      await client.send(new FlushApiCacheCommand({ apiId: cacheGqlId }));
      const verifyResp = await client.send(new GetApiCacheCommand({ apiId: cacheGqlId }));
      if (!verifyResp.apiCache) throw new Error('expected apiCache to be defined after flush');
    });

    await r('DeleteApiCache', async () => {
      await client.send(new DeleteApiCacheCommand({ apiId: cacheGqlId }));
      await assertThrows(
        () => client.send(new GetApiCacheCommand({ apiId: cacheGqlId })),
        'NotFoundException',
      );
    });

    await r('GetApiCache_NonExistent', async () => {
      await assertThrows(
        () => client.send(new GetApiCacheCommand({ apiId: cacheGqlId })),
        'NotFoundException',
      );
    });

    await r('FlushApiCache_NonExistent', async () => {
      await assertThrows(
        () => client.send(new FlushApiCacheCommand({ apiId: cacheGqlId })),
        'NotFoundException',
      );
    });

    await r('DeleteApiCache_NonExistent', async () => {
      await assertThrows(
        () => client.send(new DeleteApiCacheCommand({ apiId: cacheGqlId })),
        'NotFoundException',
      );
    });
  } finally {
    await safeCleanup(() => client.send(new DeleteGraphqlApiCommand({ apiId: cacheGqlId })) as unknown as Promise<void>);
  }

  return results;
}
