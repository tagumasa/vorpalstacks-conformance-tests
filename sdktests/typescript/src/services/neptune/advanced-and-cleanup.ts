import {
  NeptuneClient,
  CreateGlobalClusterCommand,
  DescribeGlobalClustersCommand,
  ModifyGlobalClusterCommand,
  DeleteGlobalClusterCommand,
  CreateEventSubscriptionCommand,
  DescribeEventSubscriptionsCommand,
  AddSourceIdentifierToSubscriptionCommand,
  RemoveSourceIdentifierFromSubscriptionCommand,
  ModifyEventSubscriptionCommand,
  DeleteEventSubscriptionCommand,
  DescribeEventCategoriesCommand,
  DescribeEventsCommand,
  DescribePendingMaintenanceActionsCommand,
  DescribeOrderableDBInstanceOptionsCommand,
  DescribeEngineDefaultParametersCommand,
  AddTagsToResourceCommand,
  ListTagsForResourceCommand,
  RemoveTagsFromResourceCommand,
  CreateDBClusterEndpointCommand,
  DescribeDBClusterEndpointsCommand,
  ModifyDBClusterEndpointCommand,
  DeleteDBClusterEndpointCommand,
  CreateDBParameterGroupCommand,
  DescribeDBParameterGroupsCommand,
  DescribeDBParametersCommand,
  ModifyDBParameterGroupCommand,
  ResetDBParameterGroupCommand,
  CopyDBParameterGroupCommand,
  DeleteDBParameterGroupCommand,
  DescribeDBClustersCommand,
  DescribeDBInstancesCommand,
  DeleteDBClusterCommand,
  DeleteDBClusterSnapshotCommand,
  DeleteDBInstanceCommand,
  DeleteDBClusterParameterGroupCommand,
  DeleteDBSubnetGroupCommand,
} from '@aws-sdk/client-neptune';
import { TestRunner, TestResult } from '../../runner.js';
import { assertThrows, safeCleanup, makeUniqueName } from '../../helpers.js';
import { NeptuneState } from './context.js';

export async function runAdvancedAndCleanupTests(
  client: NeptuneClient,
  runner: TestRunner,
  results: TestResult[],
  state: NeptuneState,
): Promise<void> {
  const s = 'neptune';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(s, name, fn));

  const snsTopicArn = `arn:aws:sns:${state.region}:000000000000:test-topic`;

  await r('CreateGlobalCluster', async () => {
    const resp = await client.send(new CreateGlobalClusterCommand({
      GlobalClusterIdentifier: state.globalClusterId,
      Engine: 'neptune',
    }));
    if (!resp.GlobalCluster) throw new Error('GlobalCluster is null');
    if (!resp.GlobalCluster.GlobalClusterArn) throw new Error('GlobalClusterArn is null');
  });

  await r('DescribeGlobalClusters', async () => {
    const resp = await client.send(new DescribeGlobalClustersCommand({}));
    if (!resp.GlobalClusters || resp.GlobalClusters.length === 0) {
      throw new Error('GlobalClusters is empty');
    }
  });

  await r('DescribeGlobalClusters_FilterByID', async () => {
    const resp = await client.send(new DescribeGlobalClustersCommand({
      GlobalClusterIdentifier: state.globalClusterId,
    }));
    if (!resp.GlobalClusters || resp.GlobalClusters.length === 0) {
      throw new Error('GlobalClusters is empty for filter');
    }
    if (resp.GlobalClusters[0].GlobalClusterIdentifier !== state.globalClusterId) {
      throw new Error('GlobalClusterIdentifier mismatch');
    }
  });

  await r('ModifyGlobalCluster', async () => {
    const resp = await client.send(new ModifyGlobalClusterCommand({
      GlobalClusterIdentifier: state.globalClusterId,
      EngineVersion: '1.3.2.0',
    }));
    if (!resp.GlobalCluster) throw new Error('GlobalCluster is null');
  });

  await r('DeleteGlobalCluster', async () => {
    const resp = await client.send(new DeleteGlobalClusterCommand({
      GlobalClusterIdentifier: state.globalClusterId,
    }));
    if (!resp.GlobalCluster) throw new Error('GlobalCluster is null');
  });

  state.subName = makeUniqueName('nep-eventsub');
  await r('CreateEventSubscription', async () => {
    const resp = await client.send(new CreateEventSubscriptionCommand({
      SubscriptionName: state.subName,
      SnsTopicArn: snsTopicArn,
      SourceType: 'db-cluster',
      EventCategories: ['failover', 'failure'],
    }));
    if (!resp.EventSubscription) throw new Error('EventSubscription is null');
  });

  await r('DescribeEventSubscriptions', async () => {
    const resp = await client.send(new DescribeEventSubscriptionsCommand({}));
    if (!resp.EventSubscriptionsList || resp.EventSubscriptionsList.length === 0) {
      throw new Error('EventSubscriptionsList is empty');
    }
  });

  await r('AddSourceIdentifierToSubscription', async () => {
    const resp = await client.send(new AddSourceIdentifierToSubscriptionCommand({
      SubscriptionName: state.subName,
      SourceIdentifier: state.clusterName,
    }));
    if (!resp.EventSubscription) throw new Error('EventSubscription is null');
  });

  await r('RemoveSourceIdentifierFromSubscription', async () => {
    const resp = await client.send(new RemoveSourceIdentifierFromSubscriptionCommand({
      SubscriptionName: state.subName,
      SourceIdentifier: state.clusterName,
    }));
    if (!resp.EventSubscription) throw new Error('EventSubscription is null');
  });

  await r('ModifyEventSubscription', async () => {
    const resp = await client.send(new ModifyEventSubscriptionCommand({
      SubscriptionName: state.subName,
      SnsTopicArn: snsTopicArn,
      Enabled: true,
    }));
    if (!resp.EventSubscription) throw new Error('EventSubscription is null');
  });

  await r('DeleteEventSubscription', async () => {
    const resp = await client.send(new DeleteEventSubscriptionCommand({
      SubscriptionName: state.subName,
    }));
    if (!resp.EventSubscription) throw new Error('EventSubscription is null');
  });

  await r('DescribeEventCategories', async () => {
    const resp = await client.send(new DescribeEventCategoriesCommand({}));
    if (!resp.EventCategoriesMapList || resp.EventCategoriesMapList.length === 0) {
      throw new Error('EventCategoriesMapList is empty');
    }
  });

  await r('DescribeEvents', async () => {
    const resp = await client.send(new DescribeEventsCommand({}));
    if (!resp.Events) throw new Error('Events is null');
  });

  await r('DescribePendingMaintenanceActions', async () => {
    const resp = await client.send(new DescribePendingMaintenanceActionsCommand({}));
    if (!resp.PendingMaintenanceActions) throw new Error('PendingMaintenanceActions is null');
  });

  await r('DescribeOrderableDBInstanceOptions', async () => {
    const resp = await client.send(new DescribeOrderableDBInstanceOptionsCommand({
      Engine: 'neptune',
    }));
    if (!resp.OrderableDBInstanceOptions || resp.OrderableDBInstanceOptions.length === 0) {
      throw new Error('OrderableDBInstanceOptions is empty');
    }
  });

  await r('DescribeEngineDefaultParameters', async () => {
    const resp = await client.send(new DescribeEngineDefaultParametersCommand({
      DBParameterGroupFamily: 'neptune1',
    }));
    if (!resp.EngineDefaults) throw new Error('EngineDefaults is null');
    if (!resp.EngineDefaults.Parameters) throw new Error('Parameters is null');
  });

  await r('AddTagsToResource', async () => {
    await client.send(new AddTagsToResourceCommand({
      ResourceName: state.clusterArn,
      Tags: [
        { Key: 'Environment', Value: 'Test' },
        { Key: 'Owner', Value: 'Conformance' },
      ],
    }));
  });

  await r('ListTagsForResource', async () => {
    const resp = await client.send(new ListTagsForResourceCommand({
      ResourceName: state.clusterArn,
    }));
    if (!resp.TagList) throw new Error('TagList is null');
    const hasEnv = resp.TagList.some(t => t.Key === 'Environment' && t.Value === 'Test');
    if (!hasEnv) throw new Error('Environment tag not found');
  });

  await r('RemoveTagsFromResource', async () => {
    await client.send(new RemoveTagsFromResourceCommand({
      ResourceName: state.clusterArn,
      TagKeys: ['Owner'],
    }));
  });

  await r('ListTagsForResource_AfterRemove', async () => {
    const resp = await client.send(new ListTagsForResourceCommand({
      ResourceName: state.clusterArn,
    }));
    if (resp.TagList) {
      const hasOwner = resp.TagList.some(t => t.Key === 'Owner');
      if (hasOwner) throw new Error('Owner tag should be removed');
    }
  });

  await r('CreateDBClusterEndpoint', async () => {
    const resp = await client.send(new CreateDBClusterEndpointCommand({
      DBClusterEndpointIdentifier: state.endpointName,
      DBClusterIdentifier: state.clusterName,
      EndpointType: 'READER',
    }));
    if (!resp.DBClusterEndpointArn) throw new Error('DBClusterEndpointArn is null');
    if (!(resp as any).Endpoint) throw new Error('Endpoint is null');
  });

  await r('DescribeDBClusterEndpoints', async () => {
    const resp = await client.send(new DescribeDBClusterEndpointsCommand({
      DBClusterIdentifier: state.clusterName,
    }));
    if (!resp.DBClusterEndpoints || resp.DBClusterEndpoints.length === 0) {
      throw new Error('DBClusterEndpoints is empty');
    }
    const found = resp.DBClusterEndpoints.some(
      e => e.DBClusterEndpointIdentifier === state.endpointName,
    );
    if (!found) throw new Error('Created endpoint not found');
  });

  await r('ModifyDBClusterEndpoint', async () => {
    const resp = await client.send(new ModifyDBClusterEndpointCommand({
      DBClusterEndpointIdentifier: state.endpointName,
      EndpointType: 'ANY',
    }));
    if (!resp.DBClusterEndpointArn) throw new Error('DBClusterEndpointArn is null');
  });

  await r('DeleteDBClusterEndpoint', async () => {
    const resp = await client.send(new DeleteDBClusterEndpointCommand({
      DBClusterEndpointIdentifier: state.endpointName,
    }));
    if (!resp.DBClusterEndpointArn) throw new Error('DBClusterEndpointArn is null');
  });

  const dbParamGroupName = makeUniqueName('nep-pg');
  await r('CreateDBParameterGroup', async () => {
    await client.send(new CreateDBParameterGroupCommand({
      DBParameterGroupName: dbParamGroupName,
      DBParameterGroupFamily: 'neptune1',
      Description: 'TS Neptune conformance test param group',
    }));
  });

  await r('DescribeDBParameterGroups', async () => {
    const resp = await client.send(new DescribeDBParameterGroupsCommand({}));
    if (!resp.DBParameterGroups || resp.DBParameterGroups.length === 0) {
      throw new Error('DBParameterGroups is empty');
    }
    const found = resp.DBParameterGroups.some(
      g => g.DBParameterGroupName === dbParamGroupName,
    );
    if (!found) throw new Error('Created param group not found');
  });

  await r('DescribeDBParameters', async () => {
    const resp = await client.send(new DescribeDBParametersCommand({
      DBParameterGroupName: dbParamGroupName,
    }));
    if (!resp.Parameters) throw new Error('Parameters is null');
  });

  await r('ModifyDBParameterGroup', async () => {
    await client.send(new ModifyDBParameterGroupCommand({
      DBParameterGroupName: dbParamGroupName,
      Parameters: [{
        ParameterName: 'neptune_query_timeout',
        ParameterValue: '120000',
        ApplyMethod: 'immediate',
      }],
    }));
  });

  await r('ResetDBParameterGroup', async () => {
    await client.send(new ResetDBParameterGroupCommand({
      DBParameterGroupName: dbParamGroupName,
      ResetAllParameters: true,
    }));
  });

  await r('CopyDBParameterGroup', async () => {
    const copiedPgName = makeUniqueName('nep-pg-copy');
    try {
      const resp = await client.send(new CopyDBParameterGroupCommand({
        SourceDBParameterGroupIdentifier: dbParamGroupName,
        TargetDBParameterGroupIdentifier: copiedPgName,
        TargetDBParameterGroupDescription: 'Copied TS Neptune param group',
      }));
      if (!resp.DBParameterGroup) throw new Error('DBParameterGroup is null');
    } finally {
      await safeCleanup(() => client.send(new DeleteDBParameterGroupCommand({
        DBParameterGroupName: copiedPgName,
      })));
    }
  });

  await r('DeleteDBParameterGroup', async () => {
    await client.send(new DeleteDBParameterGroupCommand({
      DBParameterGroupName: dbParamGroupName,
    }));
  });

  await r('DescribeDBClusters_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new DescribeDBClustersCommand({
        DBClusterIdentifier: 'nonexistent-cluster-xyz-12345',
      }));
    }, 'DBClusterNotFoundFault');
  });

  await r('DescribeDBInstances_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new DescribeDBInstancesCommand({
        DBInstanceIdentifier: 'nonexistent-instance-xyz-12345',
      }));
    }, 'DBInstanceNotFoundFault');
  });

  await r('DeleteDBInstance', async () => {
    const resp = await client.send(new DeleteDBInstanceCommand({
      DBInstanceIdentifier: state.instanceName,
      SkipFinalSnapshot: true,
    }));
    if (!resp.DBInstance) throw new Error('DBInstance is null');
    state.instanceCreated = false;
  });

  await r('DeleteDBClusterSnapshot', async () => {
    const resp = await client.send(new DeleteDBClusterSnapshotCommand({
      DBClusterSnapshotIdentifier: state.snapshotName,
    }));
    if (!resp.DBClusterSnapshot) throw new Error('DBClusterSnapshot is null');
    state.snapshotCreated = false;
  });

  await r('DeleteDBCluster', async () => {
    const resp = await client.send(new DeleteDBClusterCommand({
      DBClusterIdentifier: state.clusterName,
      SkipFinalSnapshot: true,
    }));
    if (!resp.DBCluster) throw new Error('DBCluster is null');
    state.clusterCreated = false;
  });

  await r('DeleteDBCluster_VerifyDeleted', async () => {
    await assertThrows(async () => {
      await client.send(new DescribeDBClustersCommand({
        DBClusterIdentifier: state.clusterName,
      }));
    }, 'DBClusterNotFoundFault');
  });

  await r('DeleteDBClusterParameterGroup', async () => {
    await client.send(new DeleteDBClusterParameterGroupCommand({
      DBClusterParameterGroupName: state.clusterParamGroupName,
    }));
    state.cpgCreated = false;
  });

  await r('DeleteDBSubnetGroup', async () => {
    await client.send(new DeleteDBSubnetGroupCommand({
      DBSubnetGroupName: state.subnetGroupName,
    }));
    state.subnetCreated = false;
  });
}
