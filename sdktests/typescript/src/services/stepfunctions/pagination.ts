import {
  SFNClient,
  CreateStateMachineCommand,
  ListStateMachinesCommand,
  DeleteStateMachineCommand,
} from '@aws-sdk/client-sfn';
import type { TestRunner, TestResult } from '../../runner.js';
import { safeCleanup } from '../../helpers.js';

export async function runPaginationTests(
  runner: TestRunner,
  client: SFNClient,
  roleArn: string,
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('stepfunctions', 'ListStateMachines_Pagination', async () => {
    const pgTs = `${Date.now()}`;
    const pgARNs: string[] = [];
    try {
      for (const i of [0, 1, 2, 3, 4]) {
        const name = `PagSM-${pgTs}-${i}`;
        const resp = await client.send(new CreateStateMachineCommand({
          name,
          definition: '{"Comment":"pag test","StartAt":"Done","States":{"Done":{"Type":"Pass","End":true}}}',
          roleArn,
        }));
        if (resp.stateMachineArn) pgARNs.push(resp.stateMachineArn);
      }

      const allNames: string[] = [];
      let nextToken: string | undefined;
      do {
        const resp = await client.send(new ListStateMachinesCommand({
          maxResults: 2,
          nextToken,
        }));
        for (const sm of resp.stateMachines ?? []) {
          if (sm.name?.startsWith(`PagSM-${pgTs}`)) {
            allNames.push(sm.name);
          }
        }
        nextToken = resp.nextToken;
      } while (nextToken);

      if (allNames.length !== 5) throw new Error(`expected 5 paginated state machines, got ${allNames.length}`);
    } finally {
      for (const arn of pgARNs) {
        await safeCleanup(() => client.send(new DeleteStateMachineCommand({ stateMachineArn: arn })));
      }
    }
  }));
}
