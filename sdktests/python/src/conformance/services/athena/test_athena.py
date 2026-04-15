import pytest
from botocore.exceptions import ClientError

from conformance.conftest import assert_client_error


@pytest.fixture(scope="module")
def work_group(athena_client, unique_name):
    name = unique_name("testwg")
    athena_client.create_work_group(
        Name=name,
        Configuration={
            "ResultConfiguration": {"OutputLocation": "s3://test-bucket/athena/"}
        },
    )
    yield name
    try:
        athena_client.delete_work_group(WorkGroup=name, RecursiveDeleteOption=True)
    except Exception:
        pass


@pytest.fixture(scope="module")
def catalog(athena_client, unique_name):
    name = unique_name("testcatalog")
    athena_client.create_data_catalog(
        Name=name,
        Type="GLUE",
        Description="Test catalog",
    )
    yield name
    try:
        athena_client.delete_data_catalog(Name=name)
    except Exception:
        pass


@pytest.fixture(scope="module")
def named_query_id(athena_client, unique_name):
    name = unique_name("testquery")
    resp = athena_client.create_named_query(
        Name=name,
        Database="default",
        QueryString="SELECT 1",
        Description="Test query",
    )
    qid = resp["NamedQueryId"]
    yield qid
    try:
        athena_client.delete_named_query(NamedQueryId=qid)
    except Exception:
        pass


@pytest.fixture(scope="module")
def updated_query_name(athena_client, named_query_id, unique_name):
    name = unique_name("updatedquery")
    athena_client.update_named_query(
        NamedQueryId=named_query_id,
        Name=name,
        Description="Updated test query",
        QueryString="SELECT 2",
    )
    yield name


@pytest.fixture(scope="module")
def query_execution_id(athena_client):
    resp = athena_client.start_query_execution(
        QueryString="SELECT 1",
        QueryExecutionContext={"Database": "default"},
        ResultConfiguration={"OutputLocation": "s3://test-bucket/athena/"},
    )
    return resp["QueryExecutionId"]


class TestWorkGroups:
    def test_list_work_groups(self, athena_client):
        resp = athena_client.list_work_groups(MaxResults=10)
        assert resp.get("WorkGroups") is not None

    def test_create_work_group(self, work_group):
        assert work_group

    def test_get_work_group(self, athena_client, work_group):
        resp = athena_client.get_work_group(WorkGroup=work_group)
        assert resp.get("WorkGroup") is not None

    def test_update_work_group(self, athena_client, work_group):
        athena_client.update_work_group(
            WorkGroup=work_group, Description="Updated work group"
        )

    def test_delete_work_group(self, athena_client, work_group):
        athena_client.delete_work_group(
            WorkGroup=work_group, RecursiveDeleteOption=True
        )


class TestDataCatalogs:
    def test_list_data_catalogs(self, athena_client):
        resp = athena_client.list_data_catalogs(MaxResults=10)
        assert resp.get("DataCatalogsSummary") is not None

    def test_create_data_catalog(self, catalog):
        assert catalog

    def test_get_data_catalog(self, athena_client, catalog):
        resp = athena_client.get_data_catalog(Name=catalog)
        assert resp.get("DataCatalog") is not None

    def test_delete_data_catalog(self, athena_client, catalog):
        athena_client.delete_data_catalog(Name=catalog)


class TestDatabases:
    def test_list_databases(self, athena_client):
        resp = athena_client.list_databases(CatalogName="AwsDataCatalog")
        assert resp.get("DatabaseList") is not None

    def test_get_database(self, athena_client):
        resp = athena_client.get_database(
            CatalogName="AwsDataCatalog", DatabaseName="default"
        )
        assert resp.get("Database") is not None

    def test_list_table_metadata(self, athena_client):
        athena_client.list_table_metadata(
            CatalogName="AwsDataCatalog", DatabaseName="default"
        )


class TestNamedQueries:
    def test_create_named_query(self, named_query_id):
        assert named_query_id

    def test_get_named_query(self, athena_client, named_query_id):
        resp = athena_client.get_named_query(NamedQueryId=named_query_id)
        assert resp.get("NamedQuery") is not None

    def test_list_named_queries(self, athena_client):
        resp = athena_client.list_named_queries(MaxResults=10)
        assert resp.get("NamedQueryIds") is not None

    def test_update_named_query(self, updated_query_name):
        assert updated_query_name

    def test_get_named_query_after_update(
        self, athena_client, named_query_id, updated_query_name
    ):
        resp = athena_client.get_named_query(NamedQueryId=named_query_id)
        assert resp.get("NamedQuery") is not None
        nq = resp["NamedQuery"]
        assert nq["Name"] == updated_query_name
        assert nq["QueryString"] == "SELECT 2"

    def test_update_named_query_old_name_reusable(self, athena_client, unique_name):
        old_name = unique_name("oldnamereuse")
        create_resp = athena_client.create_named_query(
            Name=old_name,
            Database="default",
            QueryString="SELECT 3",
        )
        assert create_resp.get("NamedQueryId") is not None
        reusable_query_id = create_resp["NamedQueryId"]

        renamed_name = unique_name("renamedquery")
        athena_client.update_named_query(
            NamedQueryId=reusable_query_id,
            Name=renamed_name,
            Description="Renamed",
            QueryString="SELECT 4",
        )

        new_resp = athena_client.create_named_query(
            Name=old_name,
            Database="default",
            QueryString="SELECT 5",
        )
        assert new_resp.get("NamedQueryId") is not None

    def test_update_named_query_new_name_not_reusable(
        self, athena_client, updated_query_name
    ):
        with pytest.raises(ClientError) as exc_info:
            athena_client.create_named_query(
                Name=updated_query_name,
                Database="default",
                QueryString="SELECT duplicate",
            )
        assert_client_error(exc_info, "ResourceAlreadyExistsException")

    def test_delete_named_query(self, athena_client, named_query_id):
        athena_client.delete_named_query(NamedQueryId=named_query_id)


class TestQueryExecution:
    def test_start_query_execution(self, query_execution_id):
        assert query_execution_id

    def test_get_query_execution(self, athena_client, query_execution_id):
        resp = athena_client.get_query_execution(QueryExecutionId=query_execution_id)
        assert resp.get("QueryExecution") is not None

    def test_list_query_executions(self, athena_client):
        resp = athena_client.list_query_executions(MaxResults=10)
        assert resp.get("QueryExecutionIds") is not None

    def test_stop_query_execution(self, athena_client, query_execution_id):
        get_resp = athena_client.get_query_execution(
            QueryExecutionId=query_execution_id
        )
        state = get_resp.get("QueryExecution", {}).get("Status", {}).get("State")
        if state in ("QUEUED", "RUNNING"):
            athena_client.stop_query_execution(QueryExecutionId=query_execution_id)


class TestErrorCases:
    def test_get_work_group_nonexistent(self, athena_client):
        with pytest.raises(ClientError) as exc_info:
            athena_client.get_work_group(WorkGroup="nonexistent_wg_xyz")
        assert_client_error(exc_info, "ResourceNotFoundException")

    def test_delete_work_group_nonexistent(self, athena_client):
        with pytest.raises(ClientError) as exc_info:
            athena_client.delete_work_group(WorkGroup="nonexistent_wg_xyz")
        assert_client_error(exc_info, "ResourceNotFoundException")

    def test_get_named_query_nonexistent(self, athena_client):
        with pytest.raises(ClientError) as exc_info:
            athena_client.get_named_query(
                NamedQueryId="00000000-0000-0000-0000-000000000000"
            )
        assert_client_error(exc_info, "ResourceNotFoundException")

    def test_get_data_catalog_nonexistent(self, athena_client):
        with pytest.raises(ClientError) as exc_info:
            athena_client.get_data_catalog(Name="nonexistent_catalog_xyz")
        assert_client_error(exc_info, "ResourceNotFoundException")

    def test_create_work_group_duplicate(self, athena_client, unique_name):
        dup_wg_name = unique_name("dupwg")
        try:
            athena_client.create_work_group(Name=dup_wg_name)
        except Exception:
            pass
        try:
            with pytest.raises(ClientError) as exc_info:
                athena_client.create_work_group(Name=dup_wg_name)
            assert_client_error(exc_info, "ResourceAlreadyExistsException")
        finally:
            try:
                athena_client.delete_work_group(
                    WorkGroup=dup_wg_name, RecursiveDeleteOption=True
                )
            except Exception:
                pass
