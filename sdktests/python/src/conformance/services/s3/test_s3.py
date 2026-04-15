import json

import pytest
from botocore.exceptions import ClientError

from conformance.conftest import assert_client_error


@pytest.fixture(scope="module")
def bucket(s3_client, unique_name):
    name = unique_name("pybucket")
    s3_client.create_bucket(Bucket=name)
    yield name
    try:
        list_resp = s3_client.list_objects_v2(Bucket=name)
        if list_resp.get("Contents"):
            s3_client.delete_objects(
                Bucket=name,
                Delete={"Objects": [{"Key": o["Key"]} for o in list_resp["Contents"]]},
            )
        s3_client.delete_bucket(Bucket=name)
    except Exception:
        pass


class TestBucketLifecycle:
    def test_create_bucket(self, bucket):
        assert bucket

    def test_head_bucket(self, s3_client, bucket):
        resp = s3_client.head_bucket(Bucket=bucket)
        assert resp["ResponseMetadata"]["HTTPStatusCode"] == 200

    def test_list_buckets(self, s3_client, bucket):
        resp = s3_client.list_buckets()
        assert resp["Buckets"] is not None
        found = any(b["Name"] == bucket for b in resp["Buckets"])
        assert found

    def test_get_bucket_location(self, s3_client, bucket):
        s3_client.get_bucket_location(Bucket=bucket)


class TestObjectOperations:
    def test_put_object(self, s3_client, bucket):
        resp = s3_client.put_object(
            Bucket=bucket,
            Key="test-object.txt",
            Body="Hello, S3! This is test content.",
            ContentType="text/plain",
        )
        assert resp.get("ETag")

    def test_head_object(self, s3_client, bucket):
        resp = s3_client.head_object(Bucket=bucket, Key="test-object.txt")
        assert resp.get("ContentLength")

    def test_get_object(self, s3_client, bucket):
        resp = s3_client.get_object(Bucket=bucket, Key="test-object.txt")
        assert resp["Body"]
        body_str = resp["Body"].read().decode("utf-8")
        assert body_str == "Hello, S3! This is test content."

    def test_list_objects(self, s3_client, bucket):
        resp = s3_client.list_objects(Bucket=bucket)
        assert resp.get("Contents") is not None
        found = any(o["Key"] == "test-object.txt" for o in resp["Contents"])
        assert found

    def test_list_objects_v2(self, s3_client, bucket):
        resp = s3_client.list_objects_v2(Bucket=bucket)
        assert resp.get("Contents") is not None
        found = any(o["Key"] == "test-object.txt" for o in resp["Contents"])
        assert found

    def test_copy_object(self, s3_client, bucket):
        resp = s3_client.copy_object(
            Bucket=bucket,
            Key="copied-object.txt",
            CopySource=f"/{bucket}/test-object.txt",
        )
        assert resp.get("CopyObjectResult")

    def test_get_object_acl(self, s3_client, bucket):
        resp = s3_client.get_object_acl(Bucket=bucket, Key="test-object.txt")
        assert resp.get("Owner")

    def test_put_object_acl(self, s3_client, bucket):
        s3_client.put_object_acl(Bucket=bucket, Key="test-object.txt", ACL="private")

    def test_delete_object(self, s3_client, bucket):
        s3_client.delete_object(Bucket=bucket, Key="copied-object.txt")

    def test_delete_objects(self, s3_client, bucket):
        keys_to_delete = ["obj1.txt", "obj2.txt", "obj3.txt"]
        for key in keys_to_delete:
            s3_client.put_object(Bucket=bucket, Key=key, Body=f"content for {key}")
        s3_client.delete_objects(
            Bucket=bucket,
            Delete={"Objects": [{"Key": k} for k in keys_to_delete]},
        )


class TestMultipartUpload:
    @pytest.fixture(scope="class")
    def upload_id(self, s3_client, bucket):
        resp = s3_client.create_multipart_upload(
            Bucket=bucket, Key="multipart-test.bin"
        )
        assert resp.get("UploadId")
        return resp["UploadId"]

    def test_create_multipart_upload(self, upload_id):
        assert upload_id

    def test_upload_part(self, s3_client, bucket, upload_id):
        resp = s3_client.upload_part(
            Bucket=bucket,
            Key="multipart-test.bin",
            UploadId=upload_id,
            PartNumber=1,
            Body=b"part 1 content",
        )
        assert resp.get("ETag")

    def test_list_parts(self, s3_client, bucket, upload_id):
        resp = s3_client.list_parts(
            Bucket=bucket, Key="multipart-test.bin", UploadId=upload_id
        )
        assert resp.get("Parts") is not None

    def test_complete_multipart_upload(self, s3_client, bucket, upload_id):
        s3_client.complete_multipart_upload(
            Bucket=bucket,
            Key="multipart-test.bin",
            UploadId=upload_id,
            MultipartUpload={"Parts": [{"PartNumber": 1, "ETag": '"abc123"'}]},
        )

    def test_abort_multipart_upload(self, s3_client, bucket):
        create_resp = s3_client.create_multipart_upload(
            Bucket=bucket, Key="to-abort.bin"
        )
        assert create_resp.get("UploadId")
        s3_client.abort_multipart_upload(
            Bucket=bucket,
            Key="to-abort.bin",
            UploadId=create_resp["UploadId"],
        )


class TestBucketPolicy:
    def test_put_bucket_policy(self, s3_client, bucket):
        test_policy = {
            "Version": "2012-10-17",
            "Statement": [
                {
                    "Effect": "Allow",
                    "Principal": "*",
                    "Action": ["s3:GetObject"],
                    "Resource": f"arn:aws:s3:::{bucket}/*",
                }
            ],
        }
        s3_client.put_bucket_policy(Bucket=bucket, Policy=json.dumps(test_policy))

    def test_get_bucket_policy(self, s3_client, bucket):
        resp = s3_client.get_bucket_policy(Bucket=bucket)
        assert resp.get("Policy")

    def test_delete_bucket_policy(self, s3_client, bucket):
        s3_client.delete_bucket_policy(Bucket=bucket)


class TestMultiByte:
    def test_multi_byte_content(self, s3_client, bucket):
        ja_key = "テスト/日本語ファイル.txt"
        zh_key = "文档/简体中文.txt"
        tw_key = "文件/繁體中文.txt"
        ja_body = "こんにちは世界。これは日本語のテストデータです。"
        zh_body = "你好世界。这是简体中文的测试数据。"
        tw_body = "你好世界。這是繁體中文的測試資料。"
        for key, body in [(ja_key, ja_body), (zh_key, zh_body), (tw_key, tw_body)]:
            s3_client.put_object(
                Bucket=bucket,
                Key=key,
                Body=body,
                ContentType="text/plain; charset=utf-8",
            )
        for key, body in [(ja_key, ja_body), (zh_key, zh_body), (tw_key, tw_body)]:
            resp = s3_client.get_object(Bucket=bucket, Key=key)
            actual = resp["Body"].read().decode("utf-8")
            assert actual == body


class TestErrorCases:
    def test_head_bucket_nonexistent(self, s3_client):
        with pytest.raises(ClientError) as exc_info:
            s3_client.head_bucket(Bucket="nonexistent-bucket-xyz-12345")
        assert_client_error(exc_info, ("NoSuchBucket", "404"))

    def test_get_object_nonexistent(self, s3_client):
        tmp_bucket = "tmp-nonexistent-obj-test"
        s3_client.create_bucket(Bucket=tmp_bucket)
        try:
            with pytest.raises(ClientError) as exc_info:
                s3_client.get_object(Bucket=tmp_bucket, Key="nonexistent-key-xyz-12345")
            assert_client_error(exc_info, "NoSuchKey")
        finally:
            s3_client.delete_bucket(Bucket=tmp_bucket)


class TestDeleteBucket:
    def test_delete_bucket(self, s3_client, bucket):
        list_resp = s3_client.list_objects_v2(Bucket=bucket)
        if list_resp.get("Contents"):
            s3_client.delete_objects(
                Bucket=bucket,
                Delete={"Objects": [{"Key": o["Key"]} for o in list_resp["Contents"]]},
            )
        s3_client.delete_bucket(Bucket=bucket)
