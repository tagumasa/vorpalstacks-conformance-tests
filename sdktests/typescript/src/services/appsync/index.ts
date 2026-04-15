import { AppSyncClient, DeleteGraphqlApiCommand } from '@aws-sdk/client-appsync';
import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { safeCleanup } from '../../helpers.js';
import { runEventApiTests } from './event-api.js';
import { runGraphqlApiTests } from './graphql-api.js';
import { runSchemaAndCacheTests } from './schema.js';
import { runDomainAndAssociationTests } from './domain-association.js';
import { runMultibyteTests } from './multibyte.js';

export function registerAppSync(): ServiceRegistration {
  return {
    name: 'appsync',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new AppSyncClient({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });

      const allResults: TestResult[] = [];

      const eventApiResult = await runEventApiTests(runner, ctx, client);
      allResults.push(...eventApiResult.results);

      const graphqlApiResult = await runGraphqlApiTests(runner, ctx, client);
      allResults.push(...graphqlApiResult.results);

      try {
        const schemaResults = await runSchemaAndCacheTests(runner, client, graphqlApiResult.state.gqlApiId);
        allResults.push(...schemaResults);
      } finally {
        await safeCleanup(() => client.send(new DeleteGraphqlApiCommand({ apiId: graphqlApiResult.state.gqlApiId })) as unknown as Promise<void>);
      }

      const domainResults = await runDomainAndAssociationTests(runner, ctx, client);
      allResults.push(...domainResults);

      allResults.push(...await runMultibyteTests(runner, client, graphqlApiResult.state.gqlApiId));

      return allResults;
    },
  };
}
