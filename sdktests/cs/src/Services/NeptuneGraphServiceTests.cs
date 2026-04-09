using Amazon.NeptuneGraph;
using Amazon.NeptuneGraph.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static class NeptuneGraphServiceTests
{
    public static async Task<List<TestResult>> RunTests(TestRunner runner, AmazonNeptuneGraphClient client, string region)
    {
        var results = new List<TestResult>();
        var uid = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var tsNano = $"{uid}";
        var graphName = $"sdk-graph-{tsNano[^8..]}";
        var snapshotName = $"sdk-snap-{tsNano[^8..]}";

        string? graphId = null;
        string? graphArn = null;

        // === Graph Lifecycle ===

        results.Add(await runner.RunTestAsync("neptunegraph", "CreateGraph", async () =>
        {
            var resp = await client.CreateGraphAsync(new CreateGraphRequest
            {
                GraphName = graphName,
                ProvisionedMemory = 128,
                DeletionProtection = false,
                PublicConnectivity = false,
                Tags = new Dictionary<string, string>
                {
                    { "Environment", "test" },
                    { "Owner", "sdk-test" }
                }
            });
            if (string.IsNullOrEmpty(resp.Id)) throw new Exception("expected non-empty graph ID");
            if (resp.Name != graphName) throw new Exception($"expected {graphName}, got {resp.Name}");
            if (resp.Status != GraphStatus.AVAILABLE) throw new Exception($"expected AVAILABLE, got {resp.Status}");
            graphId = resp.Id;
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "GetGraph", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID from CreateGraph");
            var resp = await client.GetGraphAsync(new GetGraphRequest { GraphIdentifier = graphId });
            if (resp.Id != graphId) throw new Exception($"expected {graphId}, got {resp.Id}");
            if (resp.Name != graphName) throw new Exception($"expected {graphName}, got {resp.Name}");
            if (resp.ProvisionedMemory != 128) throw new Exception($"expected 128, got {resp.ProvisionedMemory}");
            if (string.IsNullOrEmpty(resp.Arn)) throw new Exception("expected non-empty ARN");
            graphArn = resp.Arn;
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "ListGraphs", async () =>
        {
            var resp = await client.ListGraphsAsync(new ListGraphsRequest());
            if (resp.Graphs == null) throw new Exception("expected non-nil Graphs list");
            var found = resp.Graphs.Any(g => g.Id == graphId);
            if (!found) throw new Exception("created graph not found in ListGraphs");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "UpdateGraph", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            await client.UpdateGraphAsync(new UpdateGraphRequest
            {
                GraphIdentifier = graphId,
                ProvisionedMemory = 256,
                DeletionProtection = true
            });
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "UpdateGraph_Verify", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            var resp = await client.GetGraphAsync(new GetGraphRequest { GraphIdentifier = graphId });
            if (resp.ProvisionedMemory != 256) throw new Exception($"expected 256, got {resp.ProvisionedMemory}");
            if (resp.DeletionProtection != true) throw new Exception("expected deletionProtection=true");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "StopGraph", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            await client.StopGraphAsync(new StopGraphRequest { GraphIdentifier = graphId });
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "StopGraph_Verify", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            var resp = await client.GetGraphAsync(new GetGraphRequest { GraphIdentifier = graphId });
            if (resp.Status != GraphStatus.STOPPED) throw new Exception($"expected STOPPED, got {resp.Status}");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "StartGraph", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            await client.StartGraphAsync(new StartGraphRequest { GraphIdentifier = graphId });
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "StartGraph_Verify", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            var resp = await client.GetGraphAsync(new GetGraphRequest { GraphIdentifier = graphId });
            if (resp.Status != GraphStatus.AVAILABLE) throw new Exception($"expected AVAILABLE, got {resp.Status}");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "ResetGraph", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            await client.ResetGraphAsync(new ResetGraphRequest
            {
                GraphIdentifier = graphId,
                SkipSnapshot = true
            });
        }));

        // === Snapshots ===

        string? snapshotId = null;

        results.Add(await runner.RunTestAsync("neptunegraph", "CreateGraphSnapshot", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            var resp = await client.CreateGraphSnapshotAsync(new CreateGraphSnapshotRequest
            {
                GraphIdentifier = graphId,
                SnapshotName = snapshotName,
                Tags = new Dictionary<string, string> { { "Type", "sdk-test" } }
            });
            if (string.IsNullOrEmpty(resp.Id)) throw new Exception("expected non-empty snapshot ID");
            if (resp.Name != snapshotName) throw new Exception($"expected {snapshotName}, got {resp.Name}");
            snapshotId = resp.Id;
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "GetGraphSnapshot", async () =>
        {
            if (snapshotId == null) throw new Exception("no snapshot ID");
            var resp = await client.GetGraphSnapshotAsync(new GetGraphSnapshotRequest { SnapshotIdentifier = snapshotId });
            if (resp.Id != snapshotId) throw new Exception($"expected {snapshotId}, got {resp.Id}");
            if (string.IsNullOrEmpty(resp.Status)) throw new Exception("expected non-empty snapshot status");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "ListGraphSnapshots", async () =>
        {
            var resp = await client.ListGraphSnapshotsAsync(new ListGraphSnapshotsRequest());
            if (resp.GraphSnapshots == null) throw new Exception("expected non-nil GraphSnapshots list");
            var found = resp.GraphSnapshots.Any(s => s.Id == snapshotId);
            if (!found) throw new Exception("created snapshot not found in ListGraphSnapshots");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "ListGraphSnapshots_FilterByGraph", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            var resp = await client.ListGraphSnapshotsAsync(new ListGraphSnapshotsRequest { GraphIdentifier = graphId });
            if (resp.GraphSnapshots == null) throw new Exception("expected non-nil GraphSnapshots list");
            var found = resp.GraphSnapshots.Any(s => s.Id == snapshotId);
            if (!found) throw new Exception("snapshot not found when filtering by graph");
        }));

        // === Private Graph Endpoints ===

        results.Add(await runner.RunTestAsync("neptunegraph", "CreatePrivateGraphEndpoint", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            var resp = await client.CreatePrivateGraphEndpointAsync(new CreatePrivateGraphEndpointRequest
            {
                GraphIdentifier = graphId,
                VpcId = "vpc-test123",
                SubnetIds = ["subnet-aaa111", "subnet-bbb222"]
            });
            if (resp.VpcId != "vpc-test123") throw new Exception($"expected vpc-test123, got {resp.VpcId}");
            if (string.IsNullOrEmpty(resp.Status)) throw new Exception("expected non-empty endpoint status");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "GetPrivateGraphEndpoint", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            var resp = await client.GetPrivateGraphEndpointAsync(new GetPrivateGraphEndpointRequest
            {
                GraphIdentifier = graphId,
                VpcId = "vpc-test123"
            });
            if (resp.VpcId != "vpc-test123") throw new Exception($"expected vpc-test123, got {resp.VpcId}");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "ListPrivateGraphEndpoints", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            var resp = await client.ListPrivateGraphEndpointsAsync(new ListPrivateGraphEndpointsRequest
            {
                GraphIdentifier = graphId
            });
            if (resp.PrivateGraphEndpoints == null || resp.PrivateGraphEndpoints.Count == 0)
                throw new Exception("expected at least one private endpoint");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "DeletePrivateGraphEndpoint", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            await client.DeletePrivateGraphEndpointAsync(new DeletePrivateGraphEndpointRequest
            {
                GraphIdentifier = graphId,
                VpcId = "vpc-test123"
            });
        }));

        // === Import Tasks ===

        string? importTaskId = null;

        results.Add(await runner.RunTestAsync("neptunegraph", "StartImportTask", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            var resp = await client.StartImportTaskAsync(new StartImportTaskRequest
            {
                GraphIdentifier = graphId,
                Source = "s3://test-bucket/import-data/",
                RoleArn = "arn:aws:iam::000000000000:role/NeptuneImportRole",
                Format = Format.CSV
            });
            if (string.IsNullOrEmpty(resp.TaskId)) throw new Exception("expected non-empty import task ID");
            importTaskId = resp.TaskId;
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "GetImportTask", async () =>
        {
            if (importTaskId == null) throw new Exception("no import task ID");
            var resp = await client.GetImportTaskAsync(new GetImportTaskRequest { TaskIdentifier = importTaskId });
            if (resp.TaskId != importTaskId) throw new Exception($"expected {importTaskId}, got {resp.TaskId}");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "ListImportTasks", async () =>
        {
            var resp = await client.ListImportTasksAsync(new ListImportTasksRequest());
            if (resp.Tasks == null) throw new Exception("expected non-nil Tasks list");
            var found = resp.Tasks.Any(t => t.TaskId == importTaskId);
            if (!found) throw new Exception("import task not found in ListImportTasks");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "CancelImportTask", async () =>
        {
            if (importTaskId == null) throw new Exception("no import task ID");
            await client.CancelImportTaskAsync(new CancelImportTaskRequest { TaskIdentifier = importTaskId });
        }));

        // === Export Tasks ===

        string? exportTaskId = null;

        results.Add(await runner.RunTestAsync("neptunegraph", "StartExportTask", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            var resp = await client.StartExportTaskAsync(new StartExportTaskRequest
            {
                GraphIdentifier = graphId,
                Destination = "s3://test-bucket/export-data/",
                KmsKeyIdentifier = "arn:aws:kms:us-east-1:000000000000:key/12345678-1234-1234-1234-123456789012",
                RoleArn = "arn:aws:iam::000000000000:role/NeptuneExportRole",
                Format = ExportFormat.CSV
            });
            if (string.IsNullOrEmpty(resp.TaskId)) throw new Exception("expected non-empty export task ID");
            exportTaskId = resp.TaskId;
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "GetExportTask", async () =>
        {
            if (exportTaskId == null) throw new Exception("no export task ID");
            var resp = await client.GetExportTaskAsync(new GetExportTaskRequest { TaskIdentifier = exportTaskId });
            if (resp.TaskId != exportTaskId) throw new Exception($"expected {exportTaskId}, got {resp.TaskId}");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "ListExportTasks", async () =>
        {
            var resp = await client.ListExportTasksAsync(new ListExportTasksRequest());
            if (resp.Tasks == null) throw new Exception("expected non-nil Tasks list");
            var found = resp.Tasks.Any(t => t.TaskId == exportTaskId);
            if (!found) throw new Exception("export task not found in ListExportTasks");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "ListExportTasks_FilterByGraph", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            var resp = await client.ListExportTasksAsync(new ListExportTasksRequest { GraphIdentifier = graphId });
            if (resp.Tasks == null) throw new Exception("expected non-nil Tasks list");
            var found = resp.Tasks.Any(t => t.TaskId == exportTaskId);
            if (!found) throw new Exception("export task not found when filtering by graph");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "CancelExportTask", async () =>
        {
            if (exportTaskId == null) throw new Exception("no export task ID");
            await client.CancelExportTaskAsync(new CancelExportTaskRequest { TaskIdentifier = exportTaskId });
        }));

        // === Tags ===

        results.Add(await runner.RunTestAsync("neptunegraph", "TagResource", async () =>
        {
            if (graphArn == null) throw new Exception("no graph ARN");
            await client.TagResourceAsync(new TagResourceRequest
            {
                ResourceArn = graphArn,
                Tags = new Dictionary<string, string>
                {
                    { "ExtraTag", "extra-value" },
                    { "CreatedBy", "sdk-tests" }
                }
            });
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "ListTagsForResource", async () =>
        {
            if (graphArn == null) throw new Exception("no graph ARN");
            var resp = await client.ListTagsForResourceAsync(new ListTagsForResourceRequest { ResourceArn = graphArn });
            if (resp.Tags == null) throw new Exception("expected non-nil Tags map");
            if (resp.Tags["Environment"] != "test") throw new Exception("expected Environment=test");
            if (resp.Tags["Owner"] != "sdk-test") throw new Exception("expected Owner=sdk-test");
            if (resp.Tags["ExtraTag"] != "extra-value") throw new Exception("expected ExtraTag=extra-value");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "UntagResource", async () =>
        {
            if (graphArn == null) throw new Exception("no graph ARN");
            await client.UntagResourceAsync(new UntagResourceRequest
            {
                ResourceArn = graphArn,
                TagKeys = ["ExtraTag"]
            });
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "ListTagsForResource_AfterUntag", async () =>
        {
            if (graphArn == null) throw new Exception("no graph ARN");
            var resp = await client.ListTagsForResourceAsync(new ListTagsForResourceRequest { ResourceArn = graphArn });
            if (resp.Tags == null) throw new Exception("expected non-nil Tags map");
            if (resp.Tags.ContainsKey("ExtraTag")) throw new Exception("expected ExtraTag to be removed");
            if (resp.Tags["Environment"] != "test") throw new Exception("expected Environment=test still present");
        }));

        // === Data Plane ===

        results.Add(await runner.RunTestAsync("neptunegraph", "ExecuteQuery_BasicMatch", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            var resp = await client.ExecuteQueryAsync(new ExecuteQueryRequest
            {
                GraphIdentifier = graphId,
                Language = QueryLanguage.OPEN_CYPHER,
                QueryString = "MATCH (n) RETURN n LIMIT 1"
            });
            if (resp == null) throw new Exception("expected payload from ExecuteQuery");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "CancelQuery_NotImplemented", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            try
            {
                await client.CancelQueryAsync(new CancelQueryRequest
                {
                    GraphIdentifier = graphId,
                    QueryId = "q-fake123456"
                });
                throw new Exception("expected exception for CancelQuery (501)");
            }
            catch (AmazonNeptuneGraphException ex) when (ex.ErrorCode == "NotImplementedException")
            {
            }
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "GetGraphSummary", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            var resp = await client.GetGraphSummaryAsync(new GetGraphSummaryRequest
            {
                GraphIdentifier = graphId,
                Mode = GraphSummaryMode.BASIC
            });
            if (resp.GraphSummary == null) throw new Exception("expected non-nil GraphSummary");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "ListQueries", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            var resp = await client.ListQueriesAsync(new ListQueriesRequest
            {
                GraphIdentifier = graphId,
                MaxResults = 10
            });
            if (resp.Queries == null) throw new Exception("expected non-nil Queries list");
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "GetQuery_NotFound", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await client.GetQueryAsync(new GetQueryRequest
                {
                    GraphIdentifier = graphId,
                    QueryId = "q-nonexist00"
                });
            });
        }));

        // === CreateGraphUsingImportTask ===

        results.Add(await runner.RunTestAsync("neptunegraph", "CreateGraphUsingImportTask", async () =>
        {
            var importGraphName = $"sdk-impgraph-{tsNano[^6..]}";
            var resp = await client.CreateGraphUsingImportTaskAsync(new CreateGraphUsingImportTaskRequest
            {
                GraphName = importGraphName,
                Source = "s3://test-bucket/import-data/",
                RoleArn = "arn:aws:iam::000000000000:role/NeptuneImportRole",
                Format = Format.CSV
            });
            if (string.IsNullOrEmpty(resp.TaskId)) throw new Exception("expected non-empty task ID");
            if (string.IsNullOrEmpty(resp.Status)) throw new Exception("expected non-empty status");
            await TestHelpers.SafeCleanupAsync(async () =>
            {
                try { await client.CancelImportTaskAsync(new CancelImportTaskRequest { TaskIdentifier = resp.TaskId }); } catch { }
                if (!string.IsNullOrEmpty(resp.GraphId))
                {
                    try { await client.DeleteGraphAsync(new DeleteGraphRequest { GraphIdentifier = resp.GraphId, SkipSnapshot = true }); } catch { }
                }
            });
        }));

        // === RestoreGraphFromSnapshot ===

        string? restoredGraphId = null;

        results.Add(await runner.RunTestAsync("neptunegraph", "RestoreGraphFromSnapshot", async () =>
        {
            if (snapshotId == null) throw new Exception("no snapshot ID");
            var restoreName = $"sdk-restored-{tsNano[^6..]}";
            var resp = await client.RestoreGraphFromSnapshotAsync(new RestoreGraphFromSnapshotRequest
            {
                SnapshotIdentifier = snapshotId,
                GraphName = restoreName,
                ProvisionedMemory = 128,
                DeletionProtection = false
            });
            if (string.IsNullOrEmpty(resp.Id)) throw new Exception("expected non-empty restored graph ID");
            if (resp.Name != restoreName) throw new Exception($"expected {restoreName}, got {resp.Name}");
            if (resp.SourceSnapshotId != snapshotId) throw new Exception($"expected {snapshotId}, got {resp.SourceSnapshotId}");
            restoredGraphId = resp.Id;
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "RestoreGraphFromSnapshot_Verify", async () =>
        {
            if (restoredGraphId == null) throw new Exception("no restored graph ID");
            var resp = await client.GetGraphAsync(new GetGraphRequest { GraphIdentifier = restoredGraphId });
            if (resp.Status != GraphStatus.AVAILABLE) throw new Exception($"expected AVAILABLE, got {resp.Status}");
            if (resp.SourceSnapshotId != snapshotId) throw new Exception($"expected {snapshotId}, got {resp.SourceSnapshotId}");
        }));

        await TestHelpers.SafeCleanupAsync(async () =>
        {
            if (restoredGraphId != null)
                await client.DeleteGraphAsync(new DeleteGraphRequest { GraphIdentifier = restoredGraphId, SkipSnapshot = true });
        });

        // === Delete Snapshot ===

        results.Add(await runner.RunTestAsync("neptunegraph", "DeleteGraphSnapshot", async () =>
        {
            if (snapshotId == null) throw new Exception("no snapshot ID");
            await client.DeleteGraphSnapshotAsync(new DeleteGraphSnapshotRequest { SnapshotIdentifier = snapshotId });
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "DeleteGraphSnapshot_Verify", async () =>
        {
            if (snapshotId == null) throw new Exception("no snapshot ID");
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await client.GetGraphSnapshotAsync(new GetGraphSnapshotRequest { SnapshotIdentifier = snapshotId });
            });
        }));

        // === Error Cases ===

        results.Add(await runner.RunTestAsync("neptunegraph", "GetGraph_NotFound", async () =>
        {
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await client.GetGraphAsync(new GetGraphRequest { GraphIdentifier = "g-nonexist00" });
            });
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "DeleteGraph_NotFound", async () =>
        {
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await client.DeleteGraphAsync(new DeleteGraphRequest { GraphIdentifier = "g-nonexist00", SkipSnapshot = true });
            });
        }));

        // === Cleanup ===

        results.Add(await runner.RunTestAsync("neptunegraph", "UpdateGraph_DisableDeletionProtection", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            await client.UpdateGraphAsync(new UpdateGraphRequest
            {
                GraphIdentifier = graphId,
                DeletionProtection = false
            });
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "DeleteGraph", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            await client.DeleteGraphAsync(new DeleteGraphRequest { GraphIdentifier = graphId, SkipSnapshot = true });
        }));

        results.Add(await runner.RunTestAsync("neptunegraph", "DeleteGraph_Verify", async () =>
        {
            if (graphId == null) throw new Exception("no graph ID");
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await client.GetGraphAsync(new GetGraphRequest { GraphIdentifier = graphId });
            });
        }));

        return results;
    }
}
