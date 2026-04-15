import pytest


@pytest.fixture(scope="module")
def tagged_table(dynamodb_client, region, unique_name):
    table_name = unique_name("PyTagTable")
    dynamodb_client.create_table(
        TableName=table_name,
        AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "S"}],
        KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
        BillingMode="PAY_PER_REQUEST",
    )
    table_arn = f"arn:aws:dynamodb:{region}:000000000000:table/{table_name}"
    yield {"name": table_name, "arn": table_arn}
    try:
        dynamodb_client.delete_table(TableName=table_name)
    except Exception:
        pass


class TestTagResource:
    def test_tag_resource(self, dynamodb_client, tagged_table):
        dynamodb_client.tag_resource(
            ResourceArn=tagged_table["arn"],
            Tags=[{"Key": "Environment", "Value": "Test"}],
        )


class TestListTagsOfResource:
    def test_list_tags_of_resource(self, dynamodb_client, tagged_table):
        dynamodb_client.tag_resource(
            ResourceArn=tagged_table["arn"],
            Tags=[{"Key": "ToList", "Value": "Test"}],
        )
        resp = dynamodb_client.list_tags_of_resource(ResourceArn=tagged_table["arn"])
        assert len(resp.get("Tags", [])) > 0


class TestUntagResource:
    def test_untag_resource(self, dynamodb_client, tagged_table):
        dynamodb_client.tag_resource(
            ResourceArn=tagged_table["arn"],
            Tags=[{"Key": "ToRemove", "Value": "Test"}],
        )
        dynamodb_client.untag_resource(
            ResourceArn=tagged_table["arn"], TagKeys=["ToRemove"]
        )
