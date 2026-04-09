using Amazon;
using Amazon.Athena;
using Amazon.Athena.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class AthenaServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonAthenaClient athenaClient,
        string region)
    {
        var results = new List<TestResult>();
        var workGroupName = TestRunner.MakeUniqueName("CSWorkGroup");
        var customCatalogName = TestRunner.MakeUniqueName("CSCatalog");
        var namedQueryName = TestRunner.MakeUniqueName("CSNamedQuery");
        var updatedQueryName = TestRunner.MakeUniqueName("CSUpdatedQuery");
        var oldNameReusable = TestRunner.MakeUniqueName("CSOldNameReuse");
        var renamedName = TestRunner.MakeUniqueName("CSRenamedQuery");
        var dupWGName = TestRunner.MakeUniqueName("CSDupWG");

        string? namedQueryId = null;
        string? reusableQueryId = null;
        string? queryExecutionId = null;

        try
        {
            results.Add(await runner.RunTestAsync("athena", "ListWorkGroups", async () =>
            {
                var resp = await athenaClient.ListWorkGroupsAsync(new ListWorkGroupsRequest { MaxResults = 10 });
                if (resp.WorkGroups == null)
                    throw new Exception("WorkGroups is null");
            }));

            results.Add(await runner.RunTestAsync("athena", "CreateWorkGroup", async () =>
            {
                var resp = await athenaClient.CreateWorkGroupAsync(new CreateWorkGroupRequest
                {
                    Name = workGroupName,
                    Configuration = new WorkGroupConfiguration
                    {
                        ResultConfiguration = new ResultConfiguration
                        {
                            OutputLocation = "s3://test-bucket/athena/"
                        }
                    }
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("athena", "GetWorkGroup", async () =>
            {
                var resp = await athenaClient.GetWorkGroupAsync(new GetWorkGroupRequest
                {
                    WorkGroup = workGroupName
                });
                if (resp.WorkGroup == null)
                    throw new Exception("work group is null");
            }));

            results.Add(await runner.RunTestAsync("athena", "ListDataCatalogs", async () =>
            {
                var resp = await athenaClient.ListDataCatalogsAsync(new ListDataCatalogsRequest { MaxResults = 10 });
                if (resp.DataCatalogsSummary == null)
                    throw new Exception("data catalogs summary list is null");
            }));

            results.Add(await runner.RunTestAsync("athena", "CreateDataCatalog", async () =>
            {
                var resp = await athenaClient.CreateDataCatalogAsync(new CreateDataCatalogRequest
                {
                    Name = customCatalogName,
                    Type = DataCatalogType.GLUE,
                    Description = "Test catalog for GetDataCatalog"
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("athena", "GetDataCatalog", async () =>
            {
                var resp = await athenaClient.GetDataCatalogAsync(new GetDataCatalogRequest
                {
                    Name = customCatalogName
                });
                if (resp.DataCatalog == null)
                    throw new Exception("data catalog is nil");
            }));

            results.Add(await runner.RunTestAsync("athena", "ListDatabases", async () =>
            {
                var resp = await athenaClient.ListDatabasesAsync(new ListDatabasesRequest
                {
                    CatalogName = "AwsDataCatalog"
                });
                if (resp.DatabaseList == null)
                    throw new Exception("database list is nil");
            }));

            results.Add(await runner.RunTestAsync("athena", "GetDatabase", async () =>
            {
                var resp = await athenaClient.GetDatabaseAsync(new GetDatabaseRequest
                {
                    CatalogName = "AwsDataCatalog",
                    DatabaseName = "default"
                });
                if (resp.Database == null)
                    throw new Exception("database is nil");
            }));

            results.Add(await runner.RunTestAsync("athena", "ListTableMetadata", async () =>
            {
                var resp = await athenaClient.ListTableMetadataAsync(new ListTableMetadataRequest
                {
                    CatalogName = "AwsDataCatalog",
                    DatabaseName = "default"
                });
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("athena", "CreateNamedQuery", async () =>
            {
                var resp = await athenaClient.CreateNamedQueryAsync(new CreateNamedQueryRequest
                {
                    Name = namedQueryName,
                    Database = "default",
                    QueryString = "SELECT 1",
                    Description = "Test query"
                });
                if (resp == null)
                    throw new Exception("response is null");
                namedQueryId = resp.NamedQueryId;
                if (namedQueryId == null)
                    throw new Exception("NamedQueryId is null");
            }));

            results.Add(await runner.RunTestAsync("athena", "GetNamedQuery", async () =>
            {
                var resp = await athenaClient.GetNamedQueryAsync(new GetNamedQueryRequest
                {
                    NamedQueryId = namedQueryId
                });
                if (resp.NamedQuery == null)
                    throw new Exception("named query is nil");
            }));

            results.Add(await runner.RunTestAsync("athena", "ListNamedQueries", async () =>
            {
                var resp = await athenaClient.ListNamedQueriesAsync(new ListNamedQueriesRequest { MaxResults = 10 });
                if (resp.NamedQueryIds == null)
                    throw new Exception("named query IDs list is nil");
            }));

            results.Add(await runner.RunTestAsync("athena", "UpdateNamedQuery", async () =>
            {
                var resp = await athenaClient.UpdateNamedQueryAsync(new UpdateNamedQueryRequest
                {
                    NamedQueryId = namedQueryId,
                    Name = updatedQueryName,
                    Description = "Updated test query",
                    QueryString = "SELECT 2"
                });
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("athena", "GetNamedQuery_AfterUpdate", async () =>
            {
                var resp = await athenaClient.GetNamedQueryAsync(new GetNamedQueryRequest
                {
                    NamedQueryId = namedQueryId
                });
                var nq = resp.NamedQuery;
                if (nq.Name != updatedQueryName)
                    throw new Exception($"expected name '{updatedQueryName}', got '{nq.Name}'");
                if (nq.QueryString != "SELECT 2")
                    throw new Exception($"expected query 'SELECT 2', got '{nq.QueryString}'");
            }));

            results.Add(await runner.RunTestAsync("athena", "UpdateNamedQuery_OldNameReusable", async () =>
            {
                var createResp = await athenaClient.CreateNamedQueryAsync(new CreateNamedQueryRequest
                {
                    Name = oldNameReusable,
                    Database = "default",
                    QueryString = "SELECT 3"
                });
                reusableQueryId = createResp.NamedQueryId;

                await athenaClient.UpdateNamedQueryAsync(new UpdateNamedQueryRequest
                {
                    NamedQueryId = reusableQueryId,
                    Name = renamedName,
                    Description = "Renamed",
                    QueryString = "SELECT 4"
                });

                try
                {
                    await athenaClient.CreateNamedQueryAsync(new CreateNamedQueryRequest
                    {
                        Name = oldNameReusable,
                        Database = "default",
                        QueryString = "SELECT 5"
                    });
                }
                catch (Exception ex)
                {
                    throw new Exception($"creating query with old name should succeed after rename: {ex.Message}");
                }
            }));

            results.Add(await runner.RunTestAsync("athena", "UpdateNamedQuery_NewNameNotReusable", async () =>
            {
                try
                {
                    await athenaClient.CreateNamedQueryAsync(new CreateNamedQueryRequest
                    {
                        Name = updatedQueryName,
                        Database = "default",
                        QueryString = "SELECT duplicate"
                    });
                    throw new Exception("expected error for duplicate name (ResourceAlreadyExistsException)");
                }
                catch (Exception ex) when (ex is not Exception { Message: "expected error for duplicate name (ResourceAlreadyExistsException)" })
                {
                }
            }));

            results.Add(await runner.RunTestAsync("athena", "DeleteNamedQuery", async () =>
            {
                var resp = await athenaClient.DeleteNamedQueryAsync(new DeleteNamedQueryRequest
                {
                    NamedQueryId = namedQueryId
                });
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("athena", "StartQueryExecution", async () =>
            {
                try
                {
                    var resp = await athenaClient.StartQueryExecutionAsync(new StartQueryExecutionRequest
                    {
                        QueryString = "SELECT 1",
                        QueryExecutionContext = new QueryExecutionContext { Database = "default" },
                        ResultConfiguration = new ResultConfiguration { OutputLocation = "s3://test-bucket/athena/" }
                    });
                    queryExecutionId = resp.QueryExecutionId;
                }
                catch (InvalidRequestException)
                {
                }
            }));

            results.Add(await runner.RunTestAsync("athena", "GetQueryExecution", async () =>
            {
                if (queryExecutionId == null)
                    return;
                var resp = await athenaClient.GetQueryExecutionAsync(new GetQueryExecutionRequest
                {
                    QueryExecutionId = queryExecutionId
                });
                if (resp.QueryExecution == null)
                    throw new Exception("query execution is nil");
            }));

            results.Add(await runner.RunTestAsync("athena", "ListQueryExecutions", async () =>
            {
                var resp = await athenaClient.ListQueryExecutionsAsync(new ListQueryExecutionsRequest { MaxResults = 10 });
                if (resp.QueryExecutionIds == null)
                    throw new Exception("query execution IDs list is nil");
            }));

            results.Add(await runner.RunTestAsync("athena", "StopQueryExecution", async () =>
            {
                if (queryExecutionId == null)
                    return;
                var getResp = await athenaClient.GetQueryExecutionAsync(new GetQueryExecutionRequest
                {
                    QueryExecutionId = queryExecutionId
                });
                var state = getResp.QueryExecution.Status.State;
                if (state == QueryExecutionState.QUEUED || state == QueryExecutionState.RUNNING)
                {
                    await athenaClient.StopQueryExecutionAsync(new StopQueryExecutionRequest
                    {
                        QueryExecutionId = queryExecutionId
                    });
                }
            }));

            results.Add(await runner.RunTestAsync("athena", "UpdateWorkGroup", async () =>
            {
                var resp = await athenaClient.UpdateWorkGroupAsync(new UpdateWorkGroupRequest
                {
                    WorkGroup = workGroupName,
                    Description = "Updated work group"
                });
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("athena", "DeleteWorkGroup", async () =>
            {
                var resp = await athenaClient.DeleteWorkGroupAsync(new DeleteWorkGroupRequest
                {
                    WorkGroup = workGroupName,
                    RecursiveDeleteOption = true
                });
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("athena", "DeleteDataCatalog", async () =>
            {
                var resp = await athenaClient.DeleteDataCatalogAsync(new DeleteDataCatalogRequest
                {
                    Name = customCatalogName
                });
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("athena", "GetWorkGroup_NonExistent", async () =>
            {
                try
                {
                    await athenaClient.GetWorkGroupAsync(new GetWorkGroupRequest
                    {
                        WorkGroup = "nonexistent_wg_xyz"
                    });
                    throw new Exception("expected error for non-existent work group");
                }
                catch (Exception ex) when (ex is not Exception { Message: "expected error for non-existent work group" })
                {
                }
            }));

            results.Add(await runner.RunTestAsync("athena", "DeleteWorkGroup_NonExistent", async () =>
            {
                try
                {
                    await athenaClient.DeleteWorkGroupAsync(new DeleteWorkGroupRequest
                    {
                        WorkGroup = "nonexistent_wg_xyz"
                    });
                    throw new Exception("expected error for non-existent work group");
                }
                catch (Exception ex) when (ex is not Exception { Message: "expected error for non-existent work group" })
                {
                }
            }));

            results.Add(await runner.RunTestAsync("athena", "GetNamedQuery_NonExistent", async () =>
            {
                try
                {
                    await athenaClient.GetNamedQueryAsync(new GetNamedQueryRequest
                    {
                        NamedQueryId = "00000000-0000-0000-0000-000000000000"
                    });
                    throw new Exception("expected error for non-existent named query");
                }
                catch (Exception ex) when (ex is not Exception { Message: "expected error for non-existent named query" })
                {
                }
            }));

            results.Add(await runner.RunTestAsync("athena", "GetDataCatalog_NonExistent", async () =>
            {
                try
                {
                    await athenaClient.GetDataCatalogAsync(new GetDataCatalogRequest
                    {
                        Name = "nonexistent_catalog_xyz"
                    });
                    throw new Exception("expected error for non-existent catalog");
                }
                catch (Exception ex) when (ex is not Exception { Message: "expected error for non-existent catalog" })
                {
                }
            }));

            results.Add(await runner.RunTestAsync("athena", "CreateWorkGroup_Duplicate", async () =>
            {
                try
                {
                    var createResp = await athenaClient.CreateWorkGroupAsync(new CreateWorkGroupRequest
                    {
                        Name = dupWGName
                    });
                    if (createResp == null)
                        throw new Exception("first create returned null");
                }
                catch (Exception ex)
                {
                    throw new Exception($"first create: {ex.Message}");
                }

                try
                {
                    var dupResp = await athenaClient.CreateWorkGroupAsync(new CreateWorkGroupRequest
                    {
                        Name = dupWGName
                    });
                    if (dupResp == null)
                        throw new Exception("idempotent create returned null");
                }
                catch (Exception ex) when (ex is not Exception { Message: "first create:" })
                {
                }

                try
                {
                    await athenaClient.DeleteWorkGroupAsync(new DeleteWorkGroupRequest
                    {
                        WorkGroup = dupWGName
                    });
                }
                catch { }
            }));

            results.Add(await runner.RunTestAsync("athena", "GetQueryExecution_NonExistent", async () =>
            {
                try
                {
                    await athenaClient.GetQueryExecutionAsync(new GetQueryExecutionRequest
                    {
                        QueryExecutionId = "NonExistentQueryId_xyz_12345"
                    });
                    throw new Exception("Expected error but got none");
                }
                catch (Exception ex) when (ex is not Exception { Message: "Expected error but got none" })
                {
                }
            }));

            var tagWgName = TestRunner.MakeUniqueName("TagWG");
            try
            {
                await athenaClient.CreateWorkGroupAsync(new CreateWorkGroupRequest { Name = tagWgName });

                var tagWgArn = $"arn:aws:athena:{region}:000000000000:workgroup/{tagWgName}";

                results.Add(await runner.RunTestAsync("athena", "TagResource_CreateWG", async () =>
                {
                    await athenaClient.TagResourceAsync(new TagResourceRequest
                    {
                        ResourceARN = tagWgArn,
                        Tags = new List<Tag> { new Tag { Key = "Environment", Value = "test" } }
                    });
                }));

                results.Add(await runner.RunTestAsync("athena", "TagResource", async () =>
                {
                    await athenaClient.TagResourceAsync(new TagResourceRequest
                    {
                        ResourceARN = tagWgArn,
                        Tags = new List<Tag> { new Tag { Key = "Team", Value = "conformance" } }
                    });
                }));

                results.Add(await runner.RunTestAsync("athena", "ListTagsForResource", async () =>
                {
                    var resp = await athenaClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                    {
                        ResourceARN = tagWgArn
                    });
                    if (resp.Tags == null)
                        throw new Exception("tags list is null");
                }));

                results.Add(await runner.RunTestAsync("athena", "UntagResource", async () =>
                {
                    await athenaClient.UntagResourceAsync(new UntagResourceRequest
                    {
                        ResourceARN = tagWgArn,
                        TagKeys = new List<string> { "Team" }
                    });
                }));

                results.Add(await runner.RunTestAsync("athena", "ListTagsForResource_AfterUntag", async () =>
                {
                    var resp = await athenaClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                    {
                        ResourceARN = tagWgArn
                    });
                    if (resp.Tags != null)
                    {
                        foreach (var t in resp.Tags)
                        {
                            if (t.Key == "Team")
                                throw new Exception("Team tag should have been removed");
                        }
                    }
                }));

                results.Add(await runner.RunTestAsync("athena", "DeleteWorkGroup_TagCleanup", async () =>
                {
                    await athenaClient.DeleteWorkGroupAsync(new DeleteWorkGroupRequest
                    {
                        WorkGroup = tagWgName
                    });
                }));
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await athenaClient.DeleteWorkGroupAsync(new DeleteWorkGroupRequest { WorkGroup = tagWgName }); });
            }

            var tagCatalogName = TestRunner.MakeUniqueName("TagCatalog");
            try
            {
                await athenaClient.CreateDataCatalogAsync(new CreateDataCatalogRequest
                {
                    Name = tagCatalogName,
                    Type = DataCatalogType.GLUE
                });

                results.Add(await runner.RunTestAsync("athena", "TagResource_DataCatalog", async () =>
                {
                    var catalogArn = $"arn:aws:athena:{region}:000000000000:datacatalog/{tagCatalogName}";
                    await athenaClient.TagResourceAsync(new TagResourceRequest
                    {
                        ResourceARN = catalogArn,
                        Tags = new List<Tag> { new Tag { Key = "Env", Value = "test" } }
                    });
                }));

                results.Add(await runner.RunTestAsync("athena", "DeleteDataCatalog_TagCleanup", async () =>
                {
                    await athenaClient.DeleteDataCatalogAsync(new DeleteDataCatalogRequest { Name = tagCatalogName });
                }));
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await athenaClient.DeleteDataCatalogAsync(new DeleteDataCatalogRequest { Name = tagCatalogName }); });
            }

            var psWgName = TestRunner.MakeUniqueName("PSWG");
            var psQueryName = "test_prepared_stmt";
            string? psWgArn = null;
            try
            {
                await athenaClient.CreateWorkGroupAsync(new CreateWorkGroupRequest { Name = psWgName });

                results.Add(await runner.RunTestAsync("athena", "PreparedStatement_CreateWG", async () =>
                {
                    var wgResp = await athenaClient.GetWorkGroupAsync(new GetWorkGroupRequest { WorkGroup = psWgName });
                    psWgArn = $"arn:aws:athena:{region}:000000000000:workgroup/{psWgName}";
                    if (wgResp.WorkGroup == null)
                        throw new Exception("work group is null");
                }));

                results.Add(await runner.RunTestAsync("athena", "CreatePreparedStatement", async () =>
                {
                    var resp = await athenaClient.CreatePreparedStatementAsync(new CreatePreparedStatementRequest
                    {
                        WorkGroup = psWgName,
                        StatementName = psQueryName,
                        QueryStatement = "SELECT * FROM default.test WHERE col = :var1"
                    });
                    if (resp == null)
                        throw new Exception("response is null");
                }));

                results.Add(await runner.RunTestAsync("athena", "CreatePreparedStatement_Duplicate", async () =>
                {
                    try
                    {
                        await athenaClient.CreatePreparedStatementAsync(new CreatePreparedStatementRequest
                        {
                            WorkGroup = psWgName,
                            StatementName = psQueryName,
                            QueryStatement = "SELECT 1"
                        });
                        throw new Exception("expected error for duplicate prepared statement");
                    }
                    catch (InvalidRequestException) { }
                }));

                results.Add(await runner.RunTestAsync("athena", "GetPreparedStatement", async () =>
                {
                    var resp = await athenaClient.GetPreparedStatementAsync(new GetPreparedStatementRequest
                    {
                        WorkGroup = psWgName,
                        StatementName = psQueryName
                    });
                    if (resp.PreparedStatement == null)
                        throw new Exception("prepared statement is null");
                }));

                results.Add(await runner.RunTestAsync("athena", "ListPreparedStatements", async () =>
                {
                    var resp = await athenaClient.ListPreparedStatementsAsync(new ListPreparedStatementsRequest
                    {
                        WorkGroup = psWgName
                    });
                    if (resp.PreparedStatements == null)
                        throw new Exception("prepared statements list is null");
                }));

                results.Add(await runner.RunTestAsync("athena", "UpdatePreparedStatement", async () =>
                {
                    await athenaClient.UpdatePreparedStatementAsync(new UpdatePreparedStatementRequest
                    {
                        WorkGroup = psWgName,
                        StatementName = psQueryName,
                        QueryStatement = "SELECT * FROM default.test WHERE col2 = :var2"
                    });
                }));

                results.Add(await runner.RunTestAsync("athena", "GetPreparedStatement_AfterUpdate", async () =>
                {
                    var resp = await athenaClient.GetPreparedStatementAsync(new GetPreparedStatementRequest
                    {
                        WorkGroup = psWgName,
                        StatementName = psQueryName
                    });
                    if (resp.PreparedStatement.QueryStatement != "SELECT * FROM default.test WHERE col2 = :var2")
                        throw new Exception("prepared statement not updated");
                }));

                var psSecondName = "test_prepared_stmt_2";
                results.Add(await runner.RunTestAsync("athena", "CreatePreparedStatement_Second", async () =>
                {
                    await athenaClient.CreatePreparedStatementAsync(new CreatePreparedStatementRequest
                    {
                        WorkGroup = psWgName,
                        StatementName = psSecondName,
                        QueryStatement = "SELECT 42"
                    });
                }));

                results.Add(await runner.RunTestAsync("athena", "BatchGetPreparedStatement", async () =>
                {
                    var resp = await athenaClient.BatchGetPreparedStatementAsync(new BatchGetPreparedStatementRequest
                    {
                        PreparedStatementNames = new List<string> { psQueryName, psSecondName },
                        WorkGroup = psWgName
                    });
                    if (resp.PreparedStatements == null)
                        throw new Exception("prepared statements batch result is null");
                }));

                results.Add(await runner.RunTestAsync("athena", "DeletePreparedStatement", async () =>
                {
                    await athenaClient.DeletePreparedStatementAsync(new DeletePreparedStatementRequest
                    {
                        WorkGroup = psWgName,
                        StatementName = psQueryName
                    });
                }));

                results.Add(await runner.RunTestAsync("athena", "GetPreparedStatement_NonExistent", async () =>
                {
                    try
                    {
                        await athenaClient.GetPreparedStatementAsync(new GetPreparedStatementRequest
                        {
                            WorkGroup = psWgName,
                            StatementName = "nonexistent_ps_xyz"
                        });
                        throw new Exception("expected error for non-existent prepared statement");
                    }
                    catch (InvalidRequestException) { }
                }));

                results.Add(await runner.RunTestAsync("athena", "DeletePreparedStatement_NonExistent", async () =>
                {
                    try
                    {
                        await athenaClient.DeletePreparedStatementAsync(new DeletePreparedStatementRequest
                        {
                            WorkGroup = psWgName,
                            StatementName = "nonexistent_ps_xyz"
                        });
                        throw new Exception("expected error for non-existent prepared statement");
                    }
                    catch (InvalidRequestException) { }
                }));

                results.Add(await runner.RunTestAsync("athena", "DeleteWorkGroup_PSCleanup", async () =>
                {
                    await athenaClient.DeleteWorkGroupAsync(new DeleteWorkGroupRequest
                    {
                        WorkGroup = psWgName,
                        RecursiveDeleteOption = true
                    });
                }));
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await athenaClient.DeleteWorkGroupAsync(new DeleteWorkGroupRequest { WorkGroup = psWgName, RecursiveDeleteOption = true }); });
            }

            string? batchNqId1 = null;
            string? batchNqId2 = null;
            var batchNq1 = TestRunner.MakeUniqueName("BatchNQ1");
            var batchNq2 = TestRunner.MakeUniqueName("BatchNQ2");
            try
            {
                var createResp1 = await athenaClient.CreateNamedQueryAsync(new CreateNamedQueryRequest
                {
                    Name = batchNq1,
                    Database = "default",
                    QueryString = "SELECT 100"
                });
                batchNqId1 = createResp1.NamedQueryId;
                var createResp2 = await athenaClient.CreateNamedQueryAsync(new CreateNamedQueryRequest
                {
                    Name = batchNq2,
                    Database = "default",
                    QueryString = "SELECT 200"
                });
                batchNqId2 = createResp2.NamedQueryId;

                results.Add(await runner.RunTestAsync("athena", "BatchGetNamedQuery_Setup", async () =>
                {
                    if (batchNqId1 == null || batchNqId2 == null)
                        throw new Exception("batch named query IDs are null");
                }));

                results.Add(await runner.RunTestAsync("athena", "BatchGetNamedQuery", async () =>
                {
                    var resp = await athenaClient.BatchGetNamedQueryAsync(new BatchGetNamedQueryRequest
                    {
                        NamedQueryIds = new List<string> { batchNqId1!, batchNqId2! }
                    });
                    if (resp.NamedQueries == null)
                        throw new Exception("named queries batch result is null");
                    if (resp.NamedQueries.Count < 2)
                        throw new Exception($"expected at least 2 named queries, got {resp.NamedQueries.Count}");
                }));
            }
            finally
            {
                if (batchNqId1 != null)
                    await TestHelpers.SafeCleanupAsync(async () => { await athenaClient.DeleteNamedQueryAsync(new DeleteNamedQueryRequest { NamedQueryId = batchNqId1 }); });
                if (batchNqId2 != null)
                    await TestHelpers.SafeCleanupAsync(async () => { await athenaClient.DeleteNamedQueryAsync(new DeleteNamedQueryRequest { NamedQueryId = batchNqId2 }); });
            }

            string? qrExecId = null;
            results.Add(await runner.RunTestAsync("athena", "GetQueryResults_StartQuery", async () =>
            {
                try
                {
                    var startResp = await athenaClient.StartQueryExecutionAsync(new StartQueryExecutionRequest
                    {
                        QueryString = "SELECT 1",
                        QueryExecutionContext = new QueryExecutionContext { Database = "default" },
                        ResultConfiguration = new ResultConfiguration { OutputLocation = "s3://test-bucket/athena/" }
                    });
                    qrExecId = startResp.QueryExecutionId;
                }
                catch (InvalidRequestException) { }
            }));

            results.Add(await runner.RunTestAsync("athena", "GetQueryResults_WaitForCompletion", async () =>
            {
                if (qrExecId == null) return;
                for (int i = 0; i < 20; i++)
                {
                    var execResp = await athenaClient.GetQueryExecutionAsync(new GetQueryExecutionRequest { QueryExecutionId = qrExecId });
                    var state = execResp.QueryExecution.Status.State;
                    if (state == QueryExecutionState.SUCCEEDED || state == QueryExecutionState.FAILED || state == QueryExecutionState.CANCELLED)
                        break;
                    await Task.Delay(1000);
                }
            }));

            results.Add(await runner.RunTestAsync("athena", "GetQueryResults", async () =>
            {
                if (qrExecId == null) return;
                try
                {
                    var resp = await athenaClient.GetQueryResultsAsync(new GetQueryResultsRequest
                    {
                        QueryExecutionId = qrExecId
                    });
                    if (resp.ResultSet == null)
                        throw new Exception("result set is null");
                }
                catch (InvalidRequestException) { }
            }));

            results.Add(await runner.RunTestAsync("athena", "GetQueryRuntimeStatistics", async () =>
            {
                if (qrExecId == null) return;
                try
                {
                    var resp = await athenaClient.GetQueryRuntimeStatisticsAsync(new GetQueryRuntimeStatisticsRequest
                    {
                        QueryExecutionId = qrExecId
                    });
                }
                catch (InvalidRequestException) { }
            }));

            var udcCatalogName = TestRunner.MakeUniqueName("UDCCatalog");
            try
            {
                await athenaClient.CreateDataCatalogAsync(new CreateDataCatalogRequest
                {
                    Name = udcCatalogName,
                    Type = DataCatalogType.GLUE,
                    Description = "Original description"
                });

                results.Add(await runner.RunTestAsync("athena", "UpdateDataCatalog_Setup", async () =>
                {
                    var resp = await athenaClient.GetDataCatalogAsync(new GetDataCatalogRequest { Name = udcCatalogName });
                    if (resp.DataCatalog == null)
                        throw new Exception("catalog is null");
                }));

                results.Add(await runner.RunTestAsync("athena", "UpdateDataCatalog", async () =>
                {
                    await athenaClient.UpdateDataCatalogAsync(new UpdateDataCatalogRequest
                    {
                        Name = udcCatalogName,
                        Type = DataCatalogType.GLUE,
                        Description = "Updated description"
                    });
                }));

                results.Add(await runner.RunTestAsync("athena", "UpdateDataCatalog_Verify", async () =>
                {
                    var resp = await athenaClient.GetDataCatalogAsync(new GetDataCatalogRequest { Name = udcCatalogName });
                    if (resp.DataCatalog.Description != "Updated description")
                        throw new Exception($"expected 'Updated description', got '{resp.DataCatalog.Description}'");
                }));

                results.Add(await runner.RunTestAsync("athena", "DeleteDataCatalog_UDCCleanup", async () =>
                {
                    await athenaClient.DeleteDataCatalogAsync(new DeleteDataCatalogRequest { Name = udcCatalogName });
                }));
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await athenaClient.DeleteDataCatalogAsync(new DeleteDataCatalogRequest { Name = udcCatalogName }); });
            }

            results.Add(await runner.RunTestAsync("athena", "ListEngineVersions", async () =>
            {
                var resp = await athenaClient.ListEngineVersionsAsync(new ListEngineVersionsRequest());
                if (resp.EngineVersions == null)
                    throw new Exception("engine versions list is null");
            }));

            string? bqeExecId1 = null;
            string? bqeExecId2 = null;
            try
            {
                try
                {
                    var startResp1 = await athenaClient.StartQueryExecutionAsync(new StartQueryExecutionRequest
                    {
                        QueryString = "SELECT 10",
                        QueryExecutionContext = new QueryExecutionContext { Database = "default" },
                        ResultConfiguration = new ResultConfiguration { OutputLocation = "s3://test-bucket/athena/" }
                    });
                    bqeExecId1 = startResp1.QueryExecutionId;
                    var startResp2 = await athenaClient.StartQueryExecutionAsync(new StartQueryExecutionRequest
                    {
                        QueryString = "SELECT 20",
                        QueryExecutionContext = new QueryExecutionContext { Database = "default" },
                        ResultConfiguration = new ResultConfiguration { OutputLocation = "s3://test-bucket/athena/" }
                    });
                    bqeExecId2 = startResp2.QueryExecutionId;
                }
                catch (InvalidRequestException) { }

                results.Add(await runner.RunTestAsync("athena", "BatchGetQueryExecution_Setup", async () =>
                {
                    if (bqeExecId1 == null || bqeExecId2 == null)
                        throw new Exception("batch query execution IDs are null");
                }));

                results.Add(await runner.RunTestAsync("athena", "BatchGetQueryExecution", async () =>
                {
                    if (bqeExecId1 == null || bqeExecId2 == null) return;
                    var ids = new List<string> { bqeExecId1, bqeExecId2 };
                    var resp = await athenaClient.BatchGetQueryExecutionAsync(new BatchGetQueryExecutionRequest
                    {
                        QueryExecutionIds = ids
                    });
                    if (resp.QueryExecutions == null)
                        throw new Exception("query executions batch result is null");
                }));
            }
            catch { }

            results.Add(await runner.RunTestAsync("athena", "GetTableMetadata_NonExistent", async () =>
            {
                try
                {
                    await athenaClient.GetTableMetadataAsync(new GetTableMetadataRequest
                    {
                        CatalogName = "AwsDataCatalog",
                        DatabaseName = "default",
                        TableName = "nonexistent_table_xyz_12345"
                    });
                    throw new Exception("expected error for non-existent table metadata");
                }
                catch (MetadataException) { }
            }));

            results.Add(await runner.RunTestAsync("athena", "ListWorkGroups_Pagination", async () =>
            {
                var resp = await athenaClient.ListWorkGroupsAsync(new ListWorkGroupsRequest { MaxResults = 5 });
                if (resp.WorkGroups == null)
                    throw new Exception("work groups list is null");
                if (!string.IsNullOrEmpty(resp.NextToken))
                {
                    var resp2 = await athenaClient.ListWorkGroupsAsync(new ListWorkGroupsRequest
                    {
                        MaxResults = 5,
                        NextToken = resp.NextToken
                    });
                    if (resp2.WorkGroups == null)
                        throw new Exception("work groups list page 2 is null");
                }
            }));
        }
        finally
        {
            try
            {
                if (reusableQueryId != null)
                    await athenaClient.DeleteNamedQueryAsync(new DeleteNamedQueryRequest { NamedQueryId = reusableQueryId });
            }
            catch { }

            try
            {
                if (namedQueryId != null)
                    await athenaClient.DeleteNamedQueryAsync(new DeleteNamedQueryRequest { NamedQueryId = namedQueryId });
            }
            catch { }

            try
            {
                await athenaClient.DeleteWorkGroupAsync(new DeleteWorkGroupRequest { WorkGroup = workGroupName, RecursiveDeleteOption = true });
            }
            catch { }

            try
            {
                await athenaClient.DeleteDataCatalogAsync(new DeleteDataCatalogRequest { Name = customCatalogName });
            }
            catch { }

            try
            {
                await athenaClient.DeleteWorkGroupAsync(new DeleteWorkGroupRequest { WorkGroup = dupWGName, RecursiveDeleteOption = true });
            }
            catch { }
        }

        return results;
    }
}
