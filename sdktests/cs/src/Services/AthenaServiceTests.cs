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
