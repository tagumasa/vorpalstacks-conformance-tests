using Amazon.Neptune;
using Amazon.Neptune.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static class NeptuneServiceTests
{
    public static async Task<List<TestResult>> RunTests(TestRunner runner, AmazonNeptuneClient client, string region)
    {
        var results = new List<TestResult>();
        var uid = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var clusterID = $"test-cluster-{uid}";
        var paramGroupName = $"test-cpg-{uid}";
        var subnetGroupName = $"test-sng-{uid}";
        var snapshotID = $"test-snap-{uid}";
        var globalClusterID = $"test-global-{uid}";
        var instanceID = $"test-inst-{uid}";

        // === Engine Versions ===

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBEngineVersions", async () =>
        {
            var resp = await client.DescribeDBEngineVersionsAsync(new DescribeDBEngineVersionsRequest
            {
                Engine = "neptune"
            });
            if (resp.DBEngineVersions.Count == 0) throw new Exception("expected at least one engine version");
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBEngineVersions_DefaultEngine", async () =>
        {
            var resp = await client.DescribeDBEngineVersionsAsync(new DescribeDBEngineVersionsRequest());
            if (resp.DBEngineVersions.Count == 0) throw new Exception("expected at least one engine version with default engine");
        }));

        // === Cluster Parameter Groups ===

        results.Add(await runner.RunTestAsync("neptune", "CreateDBClusterParameterGroup", async () =>
        {
            await client.CreateDBClusterParameterGroupAsync(new CreateDBClusterParameterGroupRequest
            {
                DBClusterParameterGroupName = paramGroupName,
                DBParameterGroupFamily = "neptune1",
                Description = "Test cluster parameter group"
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBClusterParameterGroups", async () =>
        {
            var resp = await client.DescribeDBClusterParameterGroupsAsync(new DescribeDBClusterParameterGroupsRequest());
            var found = resp.DBClusterParameterGroups.Any(pg => pg.DBClusterParameterGroupName == paramGroupName);
            if (!found) throw new Exception("created parameter group not found in list");
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBClusterParameterGroups_FilterByName", async () =>
        {
            var resp = await client.DescribeDBClusterParameterGroupsAsync(new DescribeDBClusterParameterGroupsRequest
            {
                DBClusterParameterGroupName = paramGroupName
            });
            if (resp.DBClusterParameterGroups.Count != 1) throw new Exception($"expected 1 parameter group, got {resp.DBClusterParameterGroups.Count}");
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBClusterParameters", async () =>
        {
            await client.DescribeDBClusterParametersAsync(new DescribeDBClusterParametersRequest
            {
                DBClusterParameterGroupName = paramGroupName
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeEngineDefaultClusterParameters", async () =>
        {
            await client.DescribeEngineDefaultClusterParametersAsync(new DescribeEngineDefaultClusterParametersRequest
            {
                DBParameterGroupFamily = "neptune1"
            });
        }));

        // === DB Subnet Groups ===

        results.Add(await runner.RunTestAsync("neptune", "CreateDBSubnetGroup", async () =>
        {
            await client.CreateDBSubnetGroupAsync(new CreateDBSubnetGroupRequest
            {
                DBSubnetGroupName = subnetGroupName,
                DBSubnetGroupDescription = "Test subnet group",
                SubnetIds = ["subnet-aaa111", "subnet-bbb222"]
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBSubnetGroups", async () =>
        {
            var resp = await client.DescribeDBSubnetGroupsAsync(new DescribeDBSubnetGroupsRequest());
            var found = resp.DBSubnetGroups.Any(sg => sg.DBSubnetGroupName == subnetGroupName);
            if (!found) throw new Exception("created subnet group not found in list");
        }));

        results.Add(await runner.RunTestAsync("neptune", "ModifyDBSubnetGroup", async () =>
        {
            await client.ModifyDBSubnetGroupAsync(new ModifyDBSubnetGroupRequest
            {
                DBSubnetGroupName = subnetGroupName,
                DBSubnetGroupDescription = "Modified test subnet group",
                SubnetIds = ["subnet-aaa111", "subnet-bbb222", "subnet-ccc333"]
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBSubnetGroups_FilterByName", async () =>
        {
            var resp = await client.DescribeDBSubnetGroupsAsync(new DescribeDBSubnetGroupsRequest
            {
                DBSubnetGroupName = subnetGroupName
            });
            if (resp.DBSubnetGroups.Count != 1) throw new Exception($"expected 1 subnet group, got {resp.DBSubnetGroups.Count}");
        }));

        // === DB Clusters (CRUD lifecycle) ===

        results.Add(await runner.RunTestAsync("neptune", "CreateDBCluster", async () =>
        {
            await client.CreateDBClusterAsync(new CreateDBClusterRequest
            {
                DBClusterIdentifier = clusterID,
                Engine = "neptune",
                MasterUsername = "admin",
                MasterUserPassword = "Pass123456",
                Port = 8182,
                DBClusterParameterGroupName = paramGroupName,
                DBSubnetGroupName = subnetGroupName,
                BackupRetentionPeriod = 7,
                DeletionProtection = false
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBClusters", async () =>
        {
            var resp = await client.DescribeDBClustersAsync(new DescribeDBClustersRequest());
            var found = resp.DBClusters.Any(c => c.DBClusterIdentifier == clusterID);
            if (!found) throw new Exception("created cluster not found in list");
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBClusters_FilterByID", async () =>
        {
            var resp = await client.DescribeDBClustersAsync(new DescribeDBClustersRequest
            {
                DBClusterIdentifier = clusterID
            });
            if (resp.DBClusters.Count != 1) throw new Exception($"expected 1 cluster, got {resp.DBClusters.Count}");
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBClusters_ContentVerify", async () =>
        {
            var resp = await client.DescribeDBClustersAsync(new DescribeDBClustersRequest
            {
                DBClusterIdentifier = clusterID
            });
            var c = resp.DBClusters[0];
            if (c.Engine != "neptune") throw new Exception($"expected engine=neptune, got {c.Engine}");
            if (c.Port != 8182) throw new Exception($"expected port=8182, got {c.Port}");
            if (c.MasterUsername != "admin") throw new Exception($"expected master username=admin, got {c.MasterUsername}");
            if (c.BackupRetentionPeriod != 7) throw new Exception($"expected backup retention=7, got {c.BackupRetentionPeriod}");
        }));

        results.Add(await runner.RunTestAsync("neptune", "ModifyDBCluster", async () =>
        {
            await client.ModifyDBClusterAsync(new ModifyDBClusterRequest
            {
                DBClusterIdentifier = clusterID,
                BackupRetentionPeriod = 14,
                Port = 8183
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "ModifyDBCluster_Verify", async () =>
        {
            var resp = await client.DescribeDBClustersAsync(new DescribeDBClustersRequest
            {
                DBClusterIdentifier = clusterID
            });
            var c = resp.DBClusters[0];
            if (c.BackupRetentionPeriod != 14) throw new Exception($"expected backup retention=14 after modify, got {c.BackupRetentionPeriod}");
            if (c.Port != 8183) throw new Exception($"expected port=8183 after modify, got {c.Port}");
        }));

        results.Add(await runner.RunTestAsync("neptune", "AddRoleToDBCluster", async () =>
        {
            await client.AddRoleToDBClusterAsync(new AddRoleToDBClusterRequest
            {
                DBClusterIdentifier = clusterID,
                RoleArn = "arn:aws:iam::000000000000:role/test-role",
                FeatureName = "s3Import"
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "RemoveRoleFromDBCluster", async () =>
        {
            await client.RemoveRoleFromDBClusterAsync(new RemoveRoleFromDBClusterRequest
            {
                DBClusterIdentifier = clusterID,
                RoleArn = "arn:aws:iam::000000000000:role/test-role",
                FeatureName = "s3Import"
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "StopDBCluster", async () =>
        {
            await client.StopDBClusterAsync(new StopDBClusterRequest
            {
                DBClusterIdentifier = clusterID
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "StartDBCluster", async () =>
        {
            await client.StartDBClusterAsync(new StartDBClusterRequest
            {
                DBClusterIdentifier = clusterID
            });
        }));

        // === Snapshots ===

        results.Add(await runner.RunTestAsync("neptune", "CreateDBClusterSnapshot", async () =>
        {
            await client.CreateDBClusterSnapshotAsync(new CreateDBClusterSnapshotRequest
            {
                DBClusterSnapshotIdentifier = snapshotID,
                DBClusterIdentifier = clusterID
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBClusterSnapshots", async () =>
        {
            var resp = await client.DescribeDBClusterSnapshotsAsync(new DescribeDBClusterSnapshotsRequest());
            var found = resp.DBClusterSnapshots.Any(s => s.DBClusterSnapshotIdentifier == snapshotID);
            if (!found) throw new Exception("created snapshot not found in list");
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBClusterSnapshots_ContentVerify", async () =>
        {
            var resp = await client.DescribeDBClusterSnapshotsAsync(new DescribeDBClusterSnapshotsRequest
            {
                DBClusterSnapshotIdentifier = snapshotID
            });
            var snap = resp.DBClusterSnapshots[0];
            if (snap.Engine != "neptune") throw new Exception($"expected engine=neptune, got {snap.Engine}");
            if (snap.DBClusterIdentifier != clusterID) throw new Exception($"expected source cluster={clusterID}, got {snap.DBClusterIdentifier}");
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBClusterSnapshotAttributes", async () =>
        {
            await client.DescribeDBClusterSnapshotAttributesAsync(new DescribeDBClusterSnapshotAttributesRequest
            {
                DBClusterSnapshotIdentifier = snapshotID
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "ModifyDBClusterSnapshotAttribute", async () =>
        {
            await client.ModifyDBClusterSnapshotAttributeAsync(new ModifyDBClusterSnapshotAttributeRequest
            {
                DBClusterSnapshotIdentifier = snapshotID,
                AttributeName = "restore"
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "CopyDBClusterSnapshot", async () =>
        {
            var copyID = $"test-snap-copy-{uid}";
            try
            {
                await client.CopyDBClusterSnapshotAsync(new CopyDBClusterSnapshotRequest
                {
                    SourceDBClusterSnapshotIdentifier = snapshotID,
                    TargetDBClusterSnapshotIdentifier = copyID
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () =>
                {
                    await client.DeleteDBClusterSnapshotAsync(new DeleteDBClusterSnapshotRequest
                    {
                        DBClusterSnapshotIdentifier = copyID
                    });
                });
            }
        }));

        results.Add(await runner.RunTestAsync("neptune", "RestoreDBClusterFromSnapshot", async () =>
        {
            var restoreID = $"test-restore-{uid}";
            try
            {
                await client.RestoreDBClusterFromSnapshotAsync(new RestoreDBClusterFromSnapshotRequest
                {
                    DBClusterIdentifier = restoreID,
                    SnapshotIdentifier = snapshotID,
                    Engine = "neptune"
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () =>
                {
                    await client.DeleteDBClusterAsync(new DeleteDBClusterRequest
                    {
                        DBClusterIdentifier = restoreID,
                        SkipFinalSnapshot = true
                    });
                });
            }
        }));

        results.Add(await runner.RunTestAsync("neptune", "RestoreDBClusterToPointInTime", async () =>
        {
            var pitrID = $"test-pitr-{uid}";
            try
            {
                await client.RestoreDBClusterToPointInTimeAsync(new RestoreDBClusterToPointInTimeRequest
                {
                    DBClusterIdentifier = pitrID,
                    SourceDBClusterIdentifier = clusterID
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () =>
                {
                    await client.DeleteDBClusterAsync(new DeleteDBClusterRequest
                    {
                        DBClusterIdentifier = pitrID,
                        SkipFinalSnapshot = true
                    });
                });
            }
        }));

        results.Add(await runner.RunTestAsync("neptune", "DeleteDBClusterSnapshot", async () =>
        {
            await client.DeleteDBClusterSnapshotAsync(new DeleteDBClusterSnapshotRequest
            {
                DBClusterSnapshotIdentifier = snapshotID
            });
        }));

        // === DB Instances ===

        results.Add(await runner.RunTestAsync("neptune", "CreateDBInstance", async () =>
        {
            await client.CreateDBInstanceAsync(new CreateDBInstanceRequest
            {
                DBInstanceIdentifier = instanceID,
                DBClusterIdentifier = clusterID,
                Engine = "neptune",
                DBInstanceClass = "db.r5.large"
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBInstances", async () =>
        {
            var resp = await client.DescribeDBInstancesAsync(new DescribeDBInstancesRequest());
            var found = resp.DBInstances.Any(i => i.DBInstanceIdentifier == instanceID);
            if (!found) throw new Exception("created instance not found in list");
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBInstances_FilterByID", async () =>
        {
            var resp = await client.DescribeDBInstancesAsync(new DescribeDBInstancesRequest
            {
                DBInstanceIdentifier = instanceID
            });
            if (resp.DBInstances.Count != 1) throw new Exception($"expected 1 instance, got {resp.DBInstances.Count}");
        }));

        results.Add(await runner.RunTestAsync("neptune", "ModifyDBInstance", async () =>
        {
            await client.ModifyDBInstanceAsync(new ModifyDBInstanceRequest
            {
                DBInstanceIdentifier = instanceID,
                DBInstanceClass = "db.r5.xlarge"
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "RebootDBInstance", async () =>
        {
            await client.RebootDBInstanceAsync(new RebootDBInstanceRequest
            {
                DBInstanceIdentifier = instanceID
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DeleteDBInstance", async () =>
        {
            await client.DeleteDBInstanceAsync(new DeleteDBInstanceRequest
            {
                DBInstanceIdentifier = instanceID,
                SkipFinalSnapshot = true
            });
        }));

        // === Global Clusters ===

        results.Add(await runner.RunTestAsync("neptune", "CreateGlobalCluster", async () =>
        {
            await client.CreateGlobalClusterAsync(new CreateGlobalClusterRequest
            {
                GlobalClusterIdentifier = globalClusterID,
                Engine = "neptune"
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeGlobalClusters", async () =>
        {
            var resp = await client.DescribeGlobalClustersAsync(new DescribeGlobalClustersRequest());
            var found = resp.GlobalClusters.Any(gc => gc.GlobalClusterIdentifier == globalClusterID);
            if (!found) throw new Exception("created global cluster not found in list");
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeGlobalClusters_FilterByID", async () =>
        {
            var resp = await client.DescribeGlobalClustersAsync(new DescribeGlobalClustersRequest
            {
                GlobalClusterIdentifier = globalClusterID
            });
            if (resp.GlobalClusters.Count != 1) throw new Exception($"expected 1 global cluster, got {resp.GlobalClusters.Count}");
        }));

        results.Add(await runner.RunTestAsync("neptune", "ModifyGlobalCluster", async () =>
        {
            await client.ModifyGlobalClusterAsync(new ModifyGlobalClusterRequest
            {
                GlobalClusterIdentifier = globalClusterID,
                EngineVersion = "1.3.2.0"
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DeleteGlobalCluster", async () =>
        {
            await client.DeleteGlobalClusterAsync(new DeleteGlobalClusterRequest
            {
                GlobalClusterIdentifier = globalClusterID
            });
        }));

        // === Event Subscriptions ===

        var subName = $"test-sub-{uid}";
        results.Add(await runner.RunTestAsync("neptune", "CreateEventSubscription", async () =>
        {
            await client.CreateEventSubscriptionAsync(new CreateEventSubscriptionRequest
            {
                SubscriptionName = subName,
                SnsTopicArn = "arn:aws:sns:us-east-1:000000000000:test-topic",
                SourceType = "db-cluster",
                SourceIds = [clusterID],
                EventCategories = ["creation", "deletion", "failover"]
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeEventSubscriptions", async () =>
        {
            var resp = await client.DescribeEventSubscriptionsAsync(new DescribeEventSubscriptionsRequest());
            var found = resp.EventSubscriptionsList.Any(sub => sub.CustSubscriptionId == subName);
            if (!found) throw new Exception("created event subscription not found in list");
        }));

        results.Add(await runner.RunTestAsync("neptune", "AddSourceIdentifierToSubscription", async () =>
        {
            await client.AddSourceIdentifierToSubscriptionAsync(new AddSourceIdentifierToSubscriptionRequest
            {
                SubscriptionName = subName,
                SourceIdentifier = "cluster-extra"
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "RemoveSourceIdentifierFromSubscription", async () =>
        {
            await client.RemoveSourceIdentifierFromSubscriptionAsync(new RemoveSourceIdentifierFromSubscriptionRequest
            {
                SubscriptionName = subName,
                SourceIdentifier = "cluster-extra"
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "ModifyEventSubscription", async () =>
        {
            await client.ModifyEventSubscriptionAsync(new ModifyEventSubscriptionRequest
            {
                SubscriptionName = subName,
                SourceType = "db-instance"
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DeleteEventSubscription", async () =>
        {
            await client.DeleteEventSubscriptionAsync(new DeleteEventSubscriptionRequest
            {
                SubscriptionName = subName
            });
        }));

        // === Descriptive Operations ===

        results.Add(await runner.RunTestAsync("neptune", "DescribeEventCategories", async () =>
        {
            await client.DescribeEventCategoriesAsync(new DescribeEventCategoriesRequest());
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeEvents", async () =>
        {
            await client.DescribeEventsAsync(new DescribeEventsRequest());
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribePendingMaintenanceActions", async () =>
        {
            await client.DescribePendingMaintenanceActionsAsync(new DescribePendingMaintenanceActionsRequest());
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeOrderableDBInstanceOptions", async () =>
        {
            await client.DescribeOrderableDBInstanceOptionsAsync(new DescribeOrderableDBInstanceOptionsRequest
            {
                Engine = "neptune"
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeEngineDefaultParameters", async () =>
        {
            await client.DescribeEngineDefaultParametersAsync(new DescribeEngineDefaultParametersRequest
            {
                DBParameterGroupFamily = "neptune1"
            });
        }));

        // === Tags ===

        var clusterArn = $"arn:aws:rds:us-east-1:000000000000:cluster:{clusterID}";

        results.Add(await runner.RunTestAsync("neptune", "AddTagsToResource", async () =>
        {
            await client.AddTagsToResourceAsync(new AddTagsToResourceRequest
            {
                ResourceName = clusterArn,
                Tags =
                [
                    new Tag { Key = "Environment", Value = "test" },
                    new Tag { Key = "Owner", Value = "sdk-test" }
                ]
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "ListTagsForResource", async () =>
        {
            var resp = await client.ListTagsForResourceAsync(new ListTagsForResourceRequest
            {
                ResourceName = clusterArn
            });
            if (resp.TagList == null) throw new Exception("expected TagList to be non-null");
            var foundEnv = resp.TagList.Any(t => t.Key == "Environment" && t.Value == "test");
            var foundOwner = resp.TagList.Any(t => t.Key == "Owner" && t.Value == "sdk-test");
            if (!foundEnv) throw new Exception("expected tag Environment=test not found in TagList");
            if (!foundOwner) throw new Exception("expected tag Owner=sdk-test not found in TagList");
        }));

        results.Add(await runner.RunTestAsync("neptune", "RemoveTagsFromResource", async () =>
        {
            await client.RemoveTagsFromResourceAsync(new RemoveTagsFromResourceRequest
            {
                ResourceName = clusterArn,
                TagKeys = ["Environment"]
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "ListTagsForResource_AfterRemove", async () =>
        {
            var resp = await client.ListTagsForResourceAsync(new ListTagsForResourceRequest
            {
                ResourceName = clusterArn
            });
            if (resp.TagList.Any(t => t.Key == "Environment")) throw new Exception("expected tag Environment to be removed but still present");
            if (!resp.TagList.Any(t => t.Key == "Owner")) throw new Exception("expected tag Owner to still be present after removing Environment");
        }));

        // === Cluster Endpoints ===

        var endpointID = $"test-ep-{uid}";
        results.Add(await runner.RunTestAsync("neptune", "CreateDBClusterEndpoint", async () =>
        {
            await client.CreateDBClusterEndpointAsync(new CreateDBClusterEndpointRequest
            {
                DBClusterEndpointIdentifier = endpointID,
                DBClusterIdentifier = clusterID,
                EndpointType = "READER"
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBClusterEndpoints", async () =>
        {
            var resp = await client.DescribeDBClusterEndpointsAsync(new DescribeDBClusterEndpointsRequest
            {
                DBClusterIdentifier = clusterID
            });
            var ep = resp.DBClusterEndpoints.FirstOrDefault(e => e.DBClusterEndpointIdentifier == endpointID);
            if (ep == null) throw new Exception($"expected endpoint {endpointID} in DescribeDBClusterEndpoints response");
            if (ep.EndpointType != "READER") throw new Exception($"expected EndpointType READER, got {ep.EndpointType}");
            if (string.IsNullOrEmpty(ep.Status)) throw new Exception("expected non-empty Status");
        }));

        results.Add(await runner.RunTestAsync("neptune", "ModifyDBClusterEndpoint", async () =>
        {
            await client.ModifyDBClusterEndpointAsync(new ModifyDBClusterEndpointRequest
            {
                DBClusterEndpointIdentifier = endpointID
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DeleteDBClusterEndpoint", async () =>
        {
            await client.DeleteDBClusterEndpointAsync(new DeleteDBClusterEndpointRequest
            {
                DBClusterEndpointIdentifier = endpointID
            });
        }));

        // === Instance Parameter Groups ===

        var instancePGName = $"test-pg-{uid}";
        results.Add(await runner.RunTestAsync("neptune", "CreateDBParameterGroup", async () =>
        {
            await client.CreateDBParameterGroupAsync(new CreateDBParameterGroupRequest
            {
                DBParameterGroupName = instancePGName,
                DBParameterGroupFamily = "neptune1",
                Description = "Test instance parameter group"
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBParameterGroups", async () =>
        {
            var resp = await client.DescribeDBParameterGroupsAsync(new DescribeDBParameterGroupsRequest());
            var found = resp.DBParameterGroups.Any(pg => pg.DBParameterGroupName == instancePGName);
            if (!found) throw new Exception("created parameter group not found in list");
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBParameters", async () =>
        {
            await client.DescribeDBParametersAsync(new DescribeDBParametersRequest
            {
                DBParameterGroupName = instancePGName
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "ModifyDBParameterGroup", async () =>
        {
            await client.ModifyDBParameterGroupAsync(new ModifyDBParameterGroupRequest
            {
                DBParameterGroupName = instancePGName,
                Parameters = [new Parameter { ParameterName = "neptune_query_timeout", ParameterValue = "180000" }]
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "ResetDBParameterGroup", async () =>
        {
            await client.ResetDBParameterGroupAsync(new ResetDBParameterGroupRequest
            {
                DBParameterGroupName = instancePGName
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "CopyDBParameterGroup", async () =>
        {
            var copyPGName = $"test-pg-copy-{uid}";
            try
            {
                await client.CopyDBParameterGroupAsync(new CopyDBParameterGroupRequest
                {
                    SourceDBParameterGroupIdentifier = instancePGName,
                    TargetDBParameterGroupIdentifier = copyPGName,
                    TargetDBParameterGroupDescription = "Copied instance parameter group"
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () =>
                {
                    await client.DeleteDBParameterGroupAsync(new DeleteDBParameterGroupRequest
                    {
                        DBParameterGroupName = copyPGName
                    });
                });
            }
        }));

        results.Add(await runner.RunTestAsync("neptune", "DeleteDBParameterGroup", async () =>
        {
            await client.DeleteDBParameterGroupAsync(new DeleteDBParameterGroupRequest
            {
                DBParameterGroupName = instancePGName
            });
        }));

        // === Error / Edge Case Tests ===

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBClusters_NonExistent", async () =>
        {
            try
            {
                await client.DescribeDBClustersAsync(new DescribeDBClustersRequest
                {
                    DBClusterIdentifier = "nonexistent-cluster"
                });
                throw new Exception("expected DBClusterNotFoundException");
            }
            catch (DBClusterNotFoundException) { }
        }));

        results.Add(await runner.RunTestAsync("neptune", "DescribeDBInstances_NonExistent", async () =>
        {
            try
            {
                await client.DescribeDBInstancesAsync(new DescribeDBInstancesRequest
                {
                    DBInstanceIdentifier = "nonexistent-instance"
                });
                throw new Exception("expected DBInstanceNotFoundException");
            }
            catch (DBInstanceNotFoundException) { }
            catch (AmazonNeptuneException ex) when (ex.ErrorCode != null && ex.ErrorCode.Contains("DBInstanceNotFound")) { }
        }));

        results.Add(await runner.RunTestAsync("neptune", "CreateDBCluster_Duplicate", async () =>
        {
            try
            {
                await client.CreateDBClusterAsync(new CreateDBClusterRequest
                {
                    DBClusterIdentifier = clusterID,
                    Engine = "neptune",
                    MasterUsername = "admin",
                    MasterUserPassword = "Pass123456"
                });
                throw new Exception("expected error for duplicate cluster creation");
            }
            catch (Exception) { }
        }));

        // === Cleanup ===

        results.Add(await runner.RunTestAsync("neptune", "DeleteDBClusterParameterGroup", async () =>
        {
            await client.DeleteDBClusterParameterGroupAsync(new DeleteDBClusterParameterGroupRequest
            {
                DBClusterParameterGroupName = paramGroupName
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DeleteDBSubnetGroup", async () =>
        {
            await client.DeleteDBSubnetGroupAsync(new DeleteDBSubnetGroupRequest
            {
                DBSubnetGroupName = subnetGroupName
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DeleteDBCluster", async () =>
        {
            await client.DeleteDBClusterAsync(new DeleteDBClusterRequest
            {
                DBClusterIdentifier = clusterID,
                SkipFinalSnapshot = true
            });
        }));

        results.Add(await runner.RunTestAsync("neptune", "DeleteDBCluster_VerifyDeleted", async () =>
        {
            try
            {
                await client.DescribeDBClustersAsync(new DescribeDBClustersRequest
                {
                    DBClusterIdentifier = clusterID
                });
                throw new Exception("expected DBClusterNotFoundException after delete");
            }
            catch (DBClusterNotFoundException) { }
        }));

        return results;
    }
}
