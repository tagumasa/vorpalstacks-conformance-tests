import { ServiceRegistration, TestRunner, ServiceContext, TestResult } from '../../runner.js';
import { DeletePreparedStatementCommand, DeleteWorkGroupCommand, DeleteNamedQueryCommand, DeleteDataCatalogCommand } from '@aws-sdk/client-athena';
import { safeCleanup } from '../../helpers.js';
import { createAthenaTestContext } from './context.js';
import { runWorkGroupTests } from './workgroup.js';
import { runTaggingSetupTests, runTaggingFinallyTests } from './tagging.js';
import { runDataCatalogTests, runDataCatalogFinallyTests } from './datacatalog.js';
import { runMetadataTests, runMetadataFinallyTests } from './metadata.js';
import { runNamedQueryTests, runNamedQueryFinallyTests } from './namedquery.js';
import { runQueryExecutionTests, runQueryExecutionFinallyTests } from './queryexecution.js';
import { runPreparedStatementTests, runPreparedStatementFinallyTests } from './preparedstatement.js';

export function registerAthena(): ServiceRegistration {
  return {
    name: 'athena',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const actx = createAthenaTestContext(runner, ctx);
      const allResults: TestResult[] = [];

      try {
        allResults.push(...await runWorkGroupTests(actx));
        allResults.push(...await runTaggingSetupTests(actx));
        allResults.push(...await runDataCatalogTests(actx));
        allResults.push(...await runMetadataTests(actx));
        allResults.push(...await runNamedQueryTests(actx));
        allResults.push(...await runQueryExecutionTests(actx));
        allResults.push(...await runPreparedStatementTests(actx));
      } finally {
        await safeCleanup(() => actx.client.send(new DeletePreparedStatementCommand({ StatementName: actx.psName, WorkGroup: actx.psWorkGroup })));
        await safeCleanup(() => actx.client.send(new DeleteWorkGroupCommand({ WorkGroup: actx.psWorkGroup, RecursiveDeleteOption: true })));
        await safeCleanup(() => actx.client.send(new DeleteNamedQueryCommand({ NamedQueryId: actx.reusableNqId })));
        await safeCleanup(() => actx.client.send(new DeleteNamedQueryCommand({ NamedQueryId: actx.secondNqId })));
        await safeCleanup(() => actx.client.send(new DeleteNamedQueryCommand({ NamedQueryId: actx.nqId })));
        await safeCleanup(() => actx.client.send(new DeleteDataCatalogCommand({ Name: actx.catalogName })));
        await safeCleanup(() => actx.client.send(new DeleteWorkGroupCommand({ WorkGroup: actx.wgName, RecursiveDeleteOption: true })));

        allResults.push(...await runNamedQueryFinallyTests(actx));
        allResults.push(...await runPreparedStatementFinallyTests(actx));
        allResults.push(...await runMetadataFinallyTests(actx));
        allResults.push(...await runTaggingFinallyTests(actx));
        allResults.push(...await runDataCatalogFinallyTests(actx));
        allResults.push(...await runQueryExecutionFinallyTests(actx));
      }

      return allResults;
    },
  };
}
