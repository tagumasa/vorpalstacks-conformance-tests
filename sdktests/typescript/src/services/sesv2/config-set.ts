import {
  SESv2Client,
  CreateConfigurationSetCommand,
  GetConfigurationSetCommand,
  ListConfigurationSetsCommand,
  DeleteConfigurationSetCommand,
  CreateConfigurationSetEventDestinationCommand,
  GetConfigurationSetEventDestinationsCommand,
  UpdateConfigurationSetEventDestinationCommand,
  DeleteConfigurationSetEventDestinationCommand,
  PutConfigurationSetSendingOptionsCommand,
  PutConfigurationSetReputationOptionsCommand,
  PutConfigurationSetDeliveryOptionsCommand,
  PutConfigurationSetSuppressionOptionsCommand,
  PutConfigurationSetTrackingOptionsCommand,
  PutConfigurationSetVdmOptionsCommand,
  PutConfigurationSetArchivingOptionsCommand,
  TagResourceCommand,
  UntagResourceCommand,
  ListTagsForResourceCommand,
} from '@aws-sdk/client-sesv2';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';
import type { Sesv2State } from './context.js';

export async function runConfigSetTests(
  runner: TestRunner,
  client: SESv2Client,
  state: Sesv2State,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const configSetName = makeUniqueName('ts-sesv2-cs');

  results.push(await runner.runTest('sesv2', 'CreateConfigurationSet', async () => {
    await client.send(new CreateConfigurationSetCommand({ ConfigurationSetName: configSetName }));
  }));

  results.push(await runner.runTest('sesv2', 'GetConfigurationSet', async () => {
    const resp = await client.send(new GetConfigurationSetCommand({ ConfigurationSetName: configSetName }));
    if (!resp.ConfigurationSetName) throw new Error('ConfigurationSetName is null');
  }));

  results.push(await runner.runTest('sesv2', 'ListConfigurationSets', async () => {
    const resp = await client.send(new ListConfigurationSetsCommand({}));
    if (resp.ConfigurationSets === undefined) throw new Error('ConfigurationSets is undefined');
  }));

  results.push(await runner.runTest('sesv2', 'CreateConfigurationSetEventDestination', async () => {
    await client.send(new CreateConfigurationSetEventDestinationCommand({
      ConfigurationSetName: configSetName,
      EventDestinationName: 'test-event-dest',
      EventDestination: {
        Enabled: true,
        MatchingEventTypes: ['SEND', 'DELIVERY'],
        SnsDestination: { TopicArn: `arn:aws:sns:${state.region}:000000000000:test-topic` },
      },
    }));
  }));

  results.push(await runner.runTest('sesv2', 'UpdateConfigurationSetEventDestination', async () => {
    await client.send(new UpdateConfigurationSetEventDestinationCommand({
      ConfigurationSetName: configSetName,
      EventDestinationName: 'test-event-dest',
      EventDestination: {
        Enabled: false,
        MatchingEventTypes: ['SEND'],
        SnsDestination: { TopicArn: `arn:aws:sns:${state.region}:000000000000:updated-topic` },
      },
    }));
  }));

  results.push(await runner.runTest('sesv2', 'DeleteConfigurationSetEventDestination', async () => {
    await client.send(new DeleteConfigurationSetEventDestinationCommand({
      ConfigurationSetName: configSetName,
      EventDestinationName: 'test-event-dest',
    }));
  }));

  results.push(await runner.runTest('sesv2', 'DeleteConfigurationSet', async () => {
    await client.send(new DeleteConfigurationSetCommand({ ConfigurationSetName: configSetName }));
  }));

  results.push(await runner.runTest('sesv2', 'ListConfigurationSets_Pagination', async () => {
    const pgPrefix = makeUniqueName('ts-sesv2-pg-cs');
    const created: string[] = [];
    try {
      for (const i of ['0', '1', '2', '3', '4']) {
        const name = `${pgPrefix}-${i}`;
        await client.send(new CreateConfigurationSetCommand({ ConfigurationSetName: name }));
        created.push(name);
      }
      const allCS: string[] = [];
      let nextToken: string | undefined;
      do {
        const resp = await client.send(new ListConfigurationSetsCommand({ PageSize: 2, NextToken: nextToken }));
        if (resp.ConfigurationSets) allCS.push(...resp.ConfigurationSets);
        nextToken = resp.NextToken;
      } while (nextToken);
      if (allCS.length < 5) throw new Error(`Expected >=5, got ${allCS.length}`);
    } finally {
      for (const name of created) {
        await safeCleanup(() => client.send(new DeleteConfigurationSetCommand({ ConfigurationSetName: name })));
      }
    }
  }));

  const putCsName = makeUniqueName('ts-sesv2-put-cs');
  try {
    await client.send(new CreateConfigurationSetCommand({ ConfigurationSetName: putCsName }));

    results.push(await runner.runTest('sesv2', 'PutConfigurationSetSendingOptions', async () => {
      await client.send(new PutConfigurationSetSendingOptionsCommand({
        ConfigurationSetName: putCsName, SendingEnabled: true,
      }));
    }));

    results.push(await runner.runTest('sesv2', 'PutConfigurationSetReputationOptions', async () => {
      await client.send(new PutConfigurationSetReputationOptionsCommand({
        ConfigurationSetName: putCsName, ReputationMetricsEnabled: true,
      }));
    }));

    results.push(await runner.runTest('sesv2', 'PutConfigurationSetDeliveryOptions', async () => {
      await client.send(new PutConfigurationSetDeliveryOptionsCommand({
        ConfigurationSetName: putCsName, MaxDeliverySeconds: 30,
      }));
    }));

    results.push(await runner.runTest('sesv2', 'PutConfigurationSetSuppressionOptions', async () => {
      await client.send(new PutConfigurationSetSuppressionOptionsCommand({
        ConfigurationSetName: putCsName, SuppressedReasons: ['BOUNCE', 'COMPLAINT'],
      }));
    }));

    results.push(await runner.runTest('sesv2', 'PutConfigurationSetTrackingOptions', async () => {
      await client.send(new PutConfigurationSetTrackingOptionsCommand({
        ConfigurationSetName: putCsName, HttpsPolicy: 'REQUIRE',
      }));
    }));

    results.push(await runner.runTest('sesv2', 'PutConfigurationSetVdmOptions', async () => {
      await client.send(new PutConfigurationSetVdmOptionsCommand({
        ConfigurationSetName: putCsName,
        VdmOptions: { DashboardOptions: { EngagementMetrics: 'ENABLED' } },
      }));
    }));

    results.push(await runner.runTest('sesv2', 'PutConfigurationSetArchivingOptions', async () => {
      await client.send(new PutConfigurationSetArchivingOptionsCommand({
        ConfigurationSetName: putCsName,
        ArchiveArn: 'arn:aws:mailmanager:us-east-1:000000000000:archive/test',
      }));
    }));

    results.push(await runner.runTest('sesv2', 'GetConfigurationSetEventDestinations', async () => {
      const resp = await client.send(new GetConfigurationSetEventDestinationsCommand({
        ConfigurationSetName: putCsName,
      }));
      if (resp.EventDestinations === undefined) throw new Error('event destinations is undefined');
    }));

    const tagCsName = makeUniqueName('ts-sesv2-tag-cs');
    await client.send(new CreateConfigurationSetCommand({ ConfigurationSetName: tagCsName }));
    try {
      const tagCsArn = `arn:aws:ses:${state.region}:000000000000:configuration-set/${tagCsName}`;

      results.push(await runner.runTest('sesv2', 'TagResource_ConfigSet', async () => {
        await client.send(new TagResourceCommand({
          ResourceArn: tagCsArn, Tags: [{ Key: 'Environment', Value: 'test' }],
        }));
      }));

      results.push(await runner.runTest('sesv2', 'ListTagsForResource_ConfigSet', async () => {
        const resp = await client.send(new ListTagsForResourceCommand({ ResourceArn: tagCsArn }));
        if (resp.Tags === undefined) throw new Error('tags is undefined');
      }));

      results.push(await runner.runTest('sesv2', 'UntagResource_ConfigSet', async () => {
        await client.send(new UntagResourceCommand({
          ResourceArn: tagCsArn, TagKeys: ['Environment'],
        }));
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteConfigurationSetCommand({ ConfigurationSetName: tagCsName })));
    }
  } finally {
    await safeCleanup(() => client.send(new DeleteConfigurationSetCommand({ ConfigurationSetName: putCsName })));
  }

  return results;
}
