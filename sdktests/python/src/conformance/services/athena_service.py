import time
import uuid
from botocore.exceptions import ClientError
from ..runner import TestRunner, TestResult


def _make_unique_name(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"


async def run_athena_tests(
    runner: TestRunner,
    endpoint: str,
    region: str,
) -> list[TestResult]:
    results: list[TestResult] = []
    import boto3

    session = boto3.Session(
        aws_access_key_id="test",
        aws_secret_access_key="test",
    )
    athena_client = session.client("athena", endpoint_url=endpoint, region_name=region)

    work_group_name = _make_unique_name("testwg")
    catalog_name = _make_unique_name("testcatalog")
    named_query_name = _make_unique_name("testquery")
    updated_query_name = _make_unique_name("updatedquery")
    old_name_reusable = _make_unique_name("oldnamereuse")

    named_query_id = ""
    reusable_query_id = ""
    query_execution_id = ""

    def _list_work_groups():
        resp = athena_client.list_work_groups(MaxResults=10)
        assert resp.get("WorkGroups") is not None

    results.append(await runner.run_test("athena", "ListWorkGroups", _list_work_groups))

    def _create_work_group():
        athena_client.create_work_group(
            Name=work_group_name,
            Configuration={
                "ResultConfiguration": {"OutputLocation": "s3://test-bucket/athena/"}
            },
        )

    results.append(
        await runner.run_test("athena", "CreateWorkGroup", _create_work_group)
    )

    def _get_work_group():
        resp = athena_client.get_work_group(WorkGroup=work_group_name)
        assert resp.get("WorkGroup") is not None

    results.append(await runner.run_test("athena", "GetWorkGroup", _get_work_group))

    def _list_data_catalogs():
        resp = athena_client.list_data_catalogs(MaxResults=10)
        assert resp.get("DataCatalogsSummary") is not None

    results.append(
        await runner.run_test("athena", "ListDataCatalogs", _list_data_catalogs)
    )

    def _create_data_catalog():
        athena_client.create_data_catalog(
            Name=catalog_name,
            Type="GLUE",
            Description="Test catalog",
        )

    results.append(
        await runner.run_test("athena", "CreateDataCatalog", _create_data_catalog)
    )

    def _get_data_catalog():
        resp = athena_client.get_data_catalog(Name=catalog_name)
        assert resp.get("DataCatalog") is not None

    results.append(await runner.run_test("athena", "GetDataCatalog", _get_data_catalog))

    def _list_databases():
        resp = athena_client.list_databases(CatalogName="AwsDataCatalog")
        assert resp.get("DatabaseList") is not None

    results.append(await runner.run_test("athena", "ListDatabases", _list_databases))

    def _get_database():
        resp = athena_client.get_database(
            CatalogName="AwsDataCatalog", DatabaseName="default"
        )
        assert resp.get("Database") is not None

    results.append(await runner.run_test("athena", "GetDatabase", _get_database))

    def _list_table_metadata():
        athena_client.list_table_metadata(
            CatalogName="AwsDataCatalog", DatabaseName="default"
        )

    results.append(
        await runner.run_test("athena", "ListTableMetadata", _list_table_metadata)
    )

    def _create_named_query():
        nonlocal named_query_id
        resp = athena_client.create_named_query(
            Name=named_query_name,
            Database="default",
            QueryString="SELECT 1",
            Description="Test query",
        )
        assert resp.get("NamedQueryId") is not None
        named_query_id = resp["NamedQueryId"]

    results.append(
        await runner.run_test("athena", "CreateNamedQuery", _create_named_query)
    )

    def _get_named_query():
        resp = athena_client.get_named_query(NamedQueryId=named_query_id)
        assert resp.get("NamedQuery") is not None

    results.append(await runner.run_test("athena", "GetNamedQuery", _get_named_query))

    def _list_named_queries():
        resp = athena_client.list_named_queries(MaxResults=10)
        assert resp.get("NamedQueryIds") is not None

    results.append(
        await runner.run_test("athena", "ListNamedQueries", _list_named_queries)
    )

    def _update_named_query():
        athena_client.update_named_query(
            NamedQueryId=named_query_id,
            Name=updated_query_name,
            Description="Updated test query",
            QueryString="SELECT 2",
        )

    results.append(
        await runner.run_test("athena", "UpdateNamedQuery", _update_named_query)
    )

    def _get_named_query_after_update():
        resp = athena_client.get_named_query(NamedQueryId=named_query_id)
        assert resp.get("NamedQuery") is not None
        nq = resp["NamedQuery"]
        assert nq["Name"] == updated_query_name, (
            f"Expected name {updated_query_name}, got {nq['Name']}"
        )
        assert nq["QueryString"] == "SELECT 2", (
            f"Expected query 'SELECT 2', got {nq['QueryString']}"
        )

    results.append(
        await runner.run_test(
            "athena", "GetNamedQuery_AfterUpdate", _get_named_query_after_update
        )
    )

    def _update_named_query_old_name_reusable():
        nonlocal reusable_query_id
        create_resp = athena_client.create_named_query(
            Name=old_name_reusable,
            Database="default",
            QueryString="SELECT 3",
        )
        assert create_resp.get("NamedQueryId") is not None
        reusable_query_id = create_resp["NamedQueryId"]

        renamed_name = _make_unique_name("renamedquery")
        athena_client.update_named_query(
            NamedQueryId=reusable_query_id,
            Name=renamed_name,
            Description="Renamed",
            QueryString="SELECT 4",
        )

        new_resp = athena_client.create_named_query(
            Name=old_name_reusable,
            Database="default",
            QueryString="SELECT 5",
        )
        assert new_resp.get("NamedQueryId") is not None

    results.append(
        await runner.run_test(
            "athena",
            "UpdateNamedQuery_OldNameReusable",
            _update_named_query_old_name_reusable,
        )
    )

    def _update_named_query_new_name_not_reusable():
        try:
            athena_client.create_named_query(
                Name=updated_query_name,
                Database="default",
                QueryString="SELECT duplicate",
            )
            raise AssertionError(
                "Should have failed with ResourceAlreadyExistsException"
            )
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceAlreadyExistsException"

    results.append(
        await runner.run_test(
            "athena",
            "UpdateNamedQuery_NewNameNotReusable",
            _update_named_query_new_name_not_reusable,
        )
    )

    def _delete_named_query():
        athena_client.delete_named_query(NamedQueryId=named_query_id)

    results.append(
        await runner.run_test("athena", "DeleteNamedQuery", _delete_named_query)
    )

    def _start_query_execution():
        nonlocal query_execution_id
        resp = athena_client.start_query_execution(
            QueryString="SELECT 1",
            QueryExecutionContext={"Database": "default"},
            ResultConfiguration={"OutputLocation": "s3://test-bucket/athena/"},
        )
        assert resp.get("QueryExecutionId") is not None
        query_execution_id = resp["QueryExecutionId"]

    results.append(
        await runner.run_test("athena", "StartQueryExecution", _start_query_execution)
    )

    def _get_query_execution():
        resp = athena_client.get_query_execution(QueryExecutionId=query_execution_id)
        assert resp.get("QueryExecution") is not None

    results.append(
        await runner.run_test("athena", "GetQueryExecution", _get_query_execution)
    )

    def _list_query_executions():
        resp = athena_client.list_query_executions(MaxResults=10)
        assert resp.get("QueryExecutionIds") is not None

    results.append(
        await runner.run_test("athena", "ListQueryExecutions", _list_query_executions)
    )

    def _stop_query_execution():
        get_resp = athena_client.get_query_execution(
            QueryExecutionId=query_execution_id
        )
        state = get_resp.get("QueryExecution", {}).get("Status", {}).get("State")
        if state in ("QUEUED", "RUNNING"):
            athena_client.stop_query_execution(QueryExecutionId=query_execution_id)

    results.append(
        await runner.run_test("athena", "StopQueryExecution", _stop_query_execution)
    )

    def _update_work_group():
        athena_client.update_work_group(
            WorkGroup=work_group_name, Description="Updated work group"
        )

    results.append(
        await runner.run_test("athena", "UpdateWorkGroup", _update_work_group)
    )

    def _delete_work_group():
        athena_client.delete_work_group(
            WorkGroup=work_group_name, RecursiveDeleteOption=True
        )

    results.append(
        await runner.run_test("athena", "DeleteWorkGroup", _delete_work_group)
    )

    def _delete_data_catalog():
        athena_client.delete_data_catalog(Name=catalog_name)

    results.append(
        await runner.run_test("athena", "DeleteDataCatalog", _delete_data_catalog)
    )

    # Error cases
    def _get_work_group_nonexistent():
        try:
            athena_client.get_work_group(WorkGroup="nonexistent_wg_xyz")
            raise AssertionError("Expected error but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "athena", "GetWorkGroup_NonExistent", _get_work_group_nonexistent
        )
    )

    def _delete_work_group_nonexistent():
        try:
            athena_client.delete_work_group(WorkGroup="nonexistent_wg_xyz")
            raise AssertionError("Expected error but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "athena", "DeleteWorkGroup_NonExistent", _delete_work_group_nonexistent
        )
    )

    def _get_named_query_nonexistent():
        try:
            athena_client.get_named_query(
                NamedQueryId="00000000-0000-0000-0000-000000000000"
            )
            raise AssertionError("Expected error but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "athena", "GetNamedQuery_NonExistent", _get_named_query_nonexistent
        )
    )

    def _get_data_catalog_nonexistent():
        try:
            athena_client.get_data_catalog(Name="nonexistent_catalog_xyz")
            raise AssertionError("Expected error but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "athena", "GetDataCatalog_NonExistent", _get_data_catalog_nonexistent
        )
    )

    def _create_work_group_duplicate():
        dup_wg_name = _make_unique_name("dupwg")
        try:
            athena_client.create_work_group(Name=dup_wg_name)
        except Exception:
            pass

        try:
            athena_client.create_work_group(Name=dup_wg_name)
            raise AssertionError("Expected ResourceAlreadyExistsException but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceAlreadyExistsException"
        finally:
            try:
                athena_client.delete_work_group(
                    WorkGroup=dup_wg_name, RecursiveDeleteOption=True
                )
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "athena", "CreateWorkGroup_Duplicate", _create_work_group_duplicate
        )
    )

    return results
