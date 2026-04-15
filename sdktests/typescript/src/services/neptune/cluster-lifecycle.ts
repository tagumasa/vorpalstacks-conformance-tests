import {
  NeptuneClient,
  DescribeDBEngineVersionsCommand,
  CreateDBClusterParameterGroupCommand,
  DescribeDBClusterParameterGroupsCommand,
  DescribeDBClusterParametersCommand,
  DescribeEngineDefaultClusterParametersCommand,
  CreateDBSubnetGroupCommand,
  DescribeDBSubnetGroupsCommand,
  ModifyDBSubnetGroupCommand,
  CreateDBClusterCommand,
  DescribeDBClustersCommand,
  ModifyDBClusterCommand,
  AddRoleToDBClusterCommand,
  RemoveRoleFromDBClusterCommand,
  StopDBClusterCommand,
  StartDBClusterCommand,
  CreateDBClusterSnapshotCommand,
  DescribeDBClusterSnapshotsCommand,
  DescribeDBClusterSnapshotAttributesCommand,
  ModifyDBClusterSnapshotAttributeCommand,
  CopyDBClusterSnapshotCommand,
  DeleteDBClusterSnapshotCommand,
  RestoreDBClusterFromSnapshotCommand,
  RestoreDBClusterToPointInTimeCommand,
  DeleteDBClusterCommand,
  CreateDBInstanceCommand,
  DescribeDBInstancesCommand,
  ModifyDBInstanceCommand,
  RebootDBInstanceCommand,
} from '@aws-sdk/client-neptune';
import { TestRunner, TestResult } from '../../runner.js';
import { assertThrows, safeCleanup, makeUniqueName } from '../../helpers.js';
import { NeptuneState } from './context.js';

export async function runClusterLifecycleTests(
  client: NeptuneClient,
  runner: TestRunner,
  results: TestResult[],
  state: NeptuneState,
): Promise<void> {
  const s = 'neptune';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(s, name, fn));

  await r('DescribeDBEngineVersions', async () => {
    const resp = await client.send(new DescribeDBEngineVersionsCommand({}));
    if (!resp.DBEngineVersions || resp.DBEngineVersions.length === 0) {
      throw new Error('DBEngineVersions is empty');
    }
  });

  await r('DescribeDBEngineVersions_DefaultEngine', async () => {
    const resp = await client.send(new DescribeDBEngineVersionsCommand({
      Engine: 'neptune',
    }));
    if (!resp.DBEngineVersions || resp.DBEngineVersions.length === 0) {
      throw new Error('No engine versions found for neptune');
    }
    const hasEngine = resp.DBEngineVersions.some(v => v.Engine === 'neptune');
    if (!hasEngine) throw new Error('Expected neptune engine version');
  });

  await r('CreateDBClusterParameterGroup', async () => {
    await client.send(new CreateDBClusterParameterGroupCommand({
      DBClusterParameterGroupName: state.clusterParamGroupName,
      DBParameterGroupFamily: 'neptune1',
      Description: 'TS Neptune conformance test cluster param group',
    }));
    state.cpgCreated = true;
  });

  await r('DescribeDBClusterParameterGroups', async () => {
    const resp = await client.send(new DescribeDBClusterParameterGroupsCommand({}));
    if (!resp.DBClusterParameterGroups || resp.DBClusterParameterGroups.length === 0) {
      throw new Error('DBClusterParameterGroups is empty');
    }
    const found = resp.DBClusterParameterGroups.some(
      g => g.DBClusterParameterGroupName === state.clusterParamGroupName,
    );
    if (!found) throw new Error('Created param group not found');
  });

  await r('DescribeDBClusterParameterGroups_FilterByName', async () => {
    const resp = await client.send(new DescribeDBClusterParameterGroupsCommand({
      DBClusterParameterGroupName: state.clusterParamGroupName,
    }));
    if (!resp.DBClusterParameterGroups || resp.DBClusterParameterGroups.length !== 1) {
      throw new Error(`expected 1 parameter group, got ${resp.DBClusterParameterGroups?.length}`);
    }
  });

  await r('DescribeDBClusterParameters', async () => {
    const resp = await client.send(new DescribeDBClusterParametersCommand({
      DBClusterParameterGroupName: state.clusterParamGroupName,
    }));
    if (!resp.Parameters) throw new Error('Parameters is null');
  });

  await r('DescribeEngineDefaultClusterParameters', async () => {
    const resp = await client.send(new DescribeEngineDefaultClusterParametersCommand({
      DBParameterGroupFamily: 'neptune1',
    }));
    if (!resp.EngineDefaults) throw new Error('EngineDefaults is null');
    if (!resp.EngineDefaults.Parameters) throw new Error('Parameters is null');
  });

  await r('CreateDBSubnetGroup', async () => {
    await client.send(new CreateDBSubnetGroupCommand({
      DBSubnetGroupName: state.subnetGroupName,
      DBSubnetGroupDescription: 'TS Neptune conformance test subnet group',
      SubnetIds: ['subnet-aaa111', 'subnet-bbb222'],
    }));
    state.subnetCreated = true;
  });

  await r('DescribeDBSubnetGroups', async () => {
    const resp = await client.send(new DescribeDBSubnetGroupsCommand({}));
    if (!resp.DBSubnetGroups || resp.DBSubnetGroups.length === 0) {
      throw new Error('DBSubnetGroups is empty');
    }
    const found = resp.DBSubnetGroups.some(
      g => g.DBSubnetGroupName === state.subnetGroupName,
    );
    if (!found) throw new Error('Created subnet group not found');
  });

  await r('DescribeDBSubnetGroups_FilterByName', async () => {
    const resp = await client.send(new DescribeDBSubnetGroupsCommand({
      DBSubnetGroupName: state.subnetGroupName,
    }));
    if (!resp.DBSubnetGroups || resp.DBSubnetGroups.length !== 1) {
      throw new Error(`expected 1 subnet group, got ${resp.DBSubnetGroups?.length}`);
    }
  });

  await r('ModifyDBSubnetGroup', async () => {
    const resp = await client.send(new ModifyDBSubnetGroupCommand({
      DBSubnetGroupName: state.subnetGroupName,
      DBSubnetGroupDescription: 'Modified TS Neptune subnet group',
      SubnetIds: ['subnet-aaa111', 'subnet-bbb222'],
    }));
    if (!resp.DBSubnetGroup) throw new Error('DBSubnetGroup is null');
  });

  await r('CreateDBCluster', async () => {
    const resp = await client.send(new CreateDBClusterCommand({
      DBClusterIdentifier: state.clusterName,
      Engine: 'neptune',
      DBSubnetGroupName: state.subnetGroupName,
      DBClusterParameterGroupName: state.clusterParamGroupName,
      MasterUsername: 'admin',
      MasterUserPassword: 'TestPass123!',
    }));
    if (!resp.DBCluster) throw new Error('DBCluster is null');
    if (!resp.DBCluster.DBClusterArn) throw new Error('DBClusterArn is null');
    state.clusterArn = resp.DBCluster.DBClusterArn;
    state.clusterCreated = true;
  });

  await r('DescribeDBClusters', async () => {
    const resp = await client.send(new DescribeDBClustersCommand({}));
    if (!resp.DBClusters || resp.DBClusters.length === 0) {
      throw new Error('DBClusters is empty');
    }
  });

  await r('DescribeDBClusters_FilterByID', async () => {
    const resp = await client.send(new DescribeDBClustersCommand({
      DBClusterIdentifier: state.clusterName,
    }));
    if (!resp.DBClusters || resp.DBClusters.length === 0) {
      throw new Error('DBClusters is empty for filter');
    }
    if (resp.DBClusters[0].DBClusterIdentifier !== state.clusterName) {
      throw new Error('Cluster identifier mismatch');
    }
  });

  await r('ModifyDBCluster', async () => {
    const resp = await client.send(new ModifyDBClusterCommand({
      DBClusterIdentifier: state.clusterName,
      ApplyImmediately: true,
      MasterUserPassword: 'NewPass456!',
    }));
    if (!resp.DBCluster) throw new Error('DBCluster is null');
  });

  await r('ModifyDBCluster_Verify', async () => {
    const resp = await client.send(new DescribeDBClustersCommand({
      DBClusterIdentifier: state.clusterName,
    }));
    if (!resp.DBClusters || resp.DBClusters.length === 0) {
      throw new Error('DBClusters is empty');
    }
    if (resp.DBClusters[0].Engine !== 'neptune') {
      throw new Error('Engine should be neptune');
    }
  });

  await r('DescribeDBClusters_ContentVerify', async () => {
    const resp = await client.send(new DescribeDBClustersCommand({
      DBClusterIdentifier: state.clusterName,
    }));
    if (!resp.DBClusters || resp.DBClusters.length === 0) {
      throw new Error('DBClusters is empty');
    }
    const c = resp.DBClusters[0];
    if (c.Engine !== 'neptune') {
      throw new Error(`expected engine=neptune, got ${c.Engine}`);
    }
    if (!c.Port || c.Port <= 0) {
      throw new Error(`expected port > 0, got ${c.Port}`);
    }
    if (c.MasterUsername !== 'admin') {
      throw new Error(`expected master username=admin, got ${c.MasterUsername}`);
    }
    if (!c.BackupRetentionPeriod || c.BackupRetentionPeriod < 1) {
      throw new Error(`expected backup retention >= 1, got ${c.BackupRetentionPeriod}`);
    }
  });

  await r('AddRoleToDBCluster', async () => {
    await client.send(new AddRoleToDBClusterCommand({
      DBClusterIdentifier: state.clusterName,
      RoleArn: 'arn:aws:iam::000000000000:role/NeptuneTestRole',
    }));
  });

  await r('RemoveRoleFromDBCluster', async () => {
    await client.send(new RemoveRoleFromDBClusterCommand({
      DBClusterIdentifier: state.clusterName,
      RoleArn: 'arn:aws:iam::000000000000:role/NeptuneTestRole',
    }));
  });

  await r('StopDBCluster', async () => {
    const resp = await client.send(new StopDBClusterCommand({
      DBClusterIdentifier: state.clusterName,
    }));
    if (!resp.DBCluster) throw new Error('DBCluster is null');
  });

  await r('StartDBCluster', async () => {
    const resp = await client.send(new StartDBClusterCommand({
      DBClusterIdentifier: state.clusterName,
    }));
    if (!resp.DBCluster) throw new Error('DBCluster is null');
  });

  await r('CreateDBCluster_Duplicate', async () => {
    await assertThrows(async () => {
      await client.send(new CreateDBClusterCommand({
        DBClusterIdentifier: state.clusterName,
        Engine: 'neptune',
        DBSubnetGroupName: state.subnetGroupName,
        MasterUsername: 'admin',
        MasterUserPassword: 'TestPass123!',
      }));
    }, 'DBClusterAlreadyExistsFault');
  });

  await r('CreateDBClusterSnapshot', async () => {
    const resp = await client.send(new CreateDBClusterSnapshotCommand({
      DBClusterSnapshotIdentifier: state.snapshotName,
      DBClusterIdentifier: state.clusterName,
    }));
    if (!resp.DBClusterSnapshot) throw new Error('DBClusterSnapshot is null');
    if (!resp.DBClusterSnapshot.DBClusterSnapshotArn) {
      throw new Error('DBClusterSnapshotArn is null');
    }
    state.snapshotCreated = true;
  });

  await r('DescribeDBClusterSnapshots', async () => {
    const resp = await client.send(new DescribeDBClusterSnapshotsCommand({}));
    if (!resp.DBClusterSnapshots || resp.DBClusterSnapshots.length === 0) {
      throw new Error('DBClusterSnapshots is empty');
    }
  });

  await r('DescribeDBClusterSnapshots_ContentVerify', async () => {
    const resp = await client.send(new DescribeDBClusterSnapshotsCommand({
      DBClusterSnapshotIdentifier: state.snapshotName,
    }));
    if (!resp.DBClusterSnapshots || resp.DBClusterSnapshots.length === 0) {
      throw new Error('DBClusterSnapshots is empty');
    }
    const snap = resp.DBClusterSnapshots[0];
    if (snap.Engine !== 'neptune') {
      throw new Error(`expected engine=neptune, got ${snap.Engine}`);
    }
    if (snap.DBClusterIdentifier !== state.clusterName) {
      throw new Error(`expected source cluster=${state.clusterName}, got ${snap.DBClusterIdentifier}`);
    }
  });

  await r('DescribeDBClusterSnapshotAttributes', async () => {
    const resp = await client.send(new DescribeDBClusterSnapshotAttributesCommand({
      DBClusterSnapshotIdentifier: state.snapshotName,
    }));
    if (!resp.DBClusterSnapshotAttributesResult) {
      throw new Error('DBClusterSnapshotAttributesResult is null');
    }
    if (!resp.DBClusterSnapshotAttributesResult.DBClusterSnapshotIdentifier) {
      throw new Error('DBClusterSnapshotIdentifier is null in attributes');
    }
  });

  await r('ModifyDBClusterSnapshotAttribute', async () => {
    const resp = await client.send(new ModifyDBClusterSnapshotAttributeCommand({
      DBClusterSnapshotIdentifier: state.snapshotName,
      AttributeName: 'restore',
      ValuesToAdd: ['000000000000'],
    }));
    if (!resp.DBClusterSnapshotAttributesResult) {
      throw new Error('DBClusterSnapshotAttributesResult is null');
    }
  });

  await r('CopyDBClusterSnapshot', async () => {
    const copiedSnapshotName = makeUniqueName('nep-snap-copy');
    try {
      const resp = await client.send(new CopyDBClusterSnapshotCommand({
        SourceDBClusterSnapshotIdentifier: state.snapshotName,
        TargetDBClusterSnapshotIdentifier: copiedSnapshotName,
      }));
      if (!resp.DBClusterSnapshot) throw new Error('DBClusterSnapshot is null');
    } finally {
      await safeCleanup(() => client.send(new DeleteDBClusterSnapshotCommand({
        DBClusterSnapshotIdentifier: copiedSnapshotName,
      })));
    }
  });

  await r('RestoreDBClusterFromSnapshot', async () => {
    const restoredClusterName = makeUniqueName('nep-restore');
    try {
      const resp = await client.send(new RestoreDBClusterFromSnapshotCommand({
        DBClusterIdentifier: restoredClusterName,
        SnapshotIdentifier: state.snapshotName,
        Engine: 'neptune',
        DBSubnetGroupName: state.subnetGroupName,
      }));
      if (!resp.DBCluster) throw new Error('DBCluster is null');
    } finally {
      await safeCleanup(() => client.send(new DeleteDBClusterCommand({
        DBClusterIdentifier: restoredClusterName,
        SkipFinalSnapshot: true,
      })));
    }
  });

  await r('RestoreDBClusterToPointInTime', async () => {
    const pitrClusterName = makeUniqueName('nep-pitr');
    try {
      const resp = await client.send(new RestoreDBClusterToPointInTimeCommand({
        DBClusterIdentifier: pitrClusterName,
        SourceDBClusterIdentifier: state.clusterName,
        RestoreToTime: new Date(Date.now() - 60000),
        DBSubnetGroupName: state.subnetGroupName,
      }));
      if (!resp.DBCluster) throw new Error('DBCluster is null');
    } finally {
      await safeCleanup(() => client.send(new DeleteDBClusterCommand({
        DBClusterIdentifier: pitrClusterName,
        SkipFinalSnapshot: true,
      })));
    }
  });

  await r('CreateDBInstance', async () => {
    const resp = await client.send(new CreateDBInstanceCommand({
      DBInstanceIdentifier: state.instanceName,
      DBInstanceClass: 'db.t3.medium',
      Engine: 'neptune',
      DBClusterIdentifier: state.clusterName,
      DBSubnetGroupName: state.subnetGroupName,
    }));
    if (!resp.DBInstance) throw new Error('DBInstance is null');
    if (!resp.DBInstance.DBInstanceArn) throw new Error('DBInstanceArn is null');
    state.instanceArn = resp.DBInstance.DBInstanceArn;
    state.instanceCreated = true;
  });

  await r('DescribeDBInstances', async () => {
    const resp = await client.send(new DescribeDBInstancesCommand({}));
    if (!resp.DBInstances || resp.DBInstances.length === 0) {
      throw new Error('DBInstances is empty');
    }
  });

  await r('DescribeDBInstances_FilterByID', async () => {
    const resp = await client.send(new DescribeDBInstancesCommand({
      DBInstanceIdentifier: state.instanceName,
    }));
    if (!resp.DBInstances || resp.DBInstances.length === 0) {
      throw new Error('DBInstances is empty for filter');
    }
    if (resp.DBInstances[0].DBInstanceIdentifier !== state.instanceName) {
      throw new Error('Instance identifier mismatch');
    }
  });

  await r('ModifyDBInstance', async () => {
    const resp = await client.send(new ModifyDBInstanceCommand({
      DBInstanceIdentifier: state.instanceName,
      ApplyImmediately: true,
      AllocatedStorage: 20,
    }));
    if (!resp.DBInstance) throw new Error('DBInstance is null');
  });

  await r('RebootDBInstance', async () => {
    const resp = await client.send(new RebootDBInstanceCommand({
      DBInstanceIdentifier: state.instanceName,
    }));
    if (!resp.DBInstance) throw new Error('DBInstance is null');
  });
}
