import json
import time
import uuid
from botocore.exceptions import ClientError
from ..runner import TestRunner, TestResult


def _make_unique_name(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"


async def run_s3_tests(
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
    s3_client = session.client("s3", endpoint_url=endpoint, region_name=region)

    bucket_name = _make_unique_name("pybucket")
    object_key = "test-object.txt"
    object_content = "Hello, S3! This is test content."
    bucket_created = False

    try:

        def _create_bucket():
            nonlocal bucket_created
            s3_client.create_bucket(Bucket=bucket_name)
            bucket_created = True

        results.append(await runner.run_test("s3", "CreateBucket", _create_bucket))

        def _head_bucket():
            resp = s3_client.head_bucket(Bucket=bucket_name)
            assert resp["ResponseMetadata"]["HTTPStatusCode"] == 200

        results.append(await runner.run_test("s3", "HeadBucket", _head_bucket))

        def _list_buckets():
            resp = s3_client.list_buckets()
            assert resp["Buckets"] is not None
            found = any(b["Name"] == bucket_name for b in resp["Buckets"])
            assert found, "Created bucket not found in list"

        results.append(await runner.run_test("s3", "ListBuckets", _list_buckets))

        def _get_bucket_location():
            s3_client.get_bucket_location(Bucket=bucket_name)

        results.append(
            await runner.run_test("s3", "GetBucketLocation", _get_bucket_location)
        )

        def _put_object():
            resp = s3_client.put_object(
                Bucket=bucket_name,
                Key=object_key,
                Body=object_content,
                ContentType="text/plain",
            )
            assert resp.get("ETag"), "ETag is null"

        results.append(await runner.run_test("s3", "PutObject", _put_object))

        def _head_object():
            resp = s3_client.head_object(Bucket=bucket_name, Key=object_key)
            assert resp.get("ContentLength"), "ContentLength is null"

        results.append(await runner.run_test("s3", "HeadObject", _head_object))

        def _get_object():
            resp = s3_client.get_object(Bucket=bucket_name, Key=object_key)
            assert resp["Body"], "Body is null"
            body_str = resp["Body"].read().decode("utf-8")
            assert body_str == object_content

        results.append(await runner.run_test("s3", "GetObject", _get_object))

        def _list_objects():
            resp = s3_client.list_objects(Bucket=bucket_name)
            assert resp.get("Contents") is not None
            found = any(o["Key"] == object_key for o in resp["Contents"])
            assert found, "Created object not found in list"

        results.append(await runner.run_test("s3", "ListObjects", _list_objects))

        def _list_objects_v2():
            resp = s3_client.list_objects_v2(Bucket=bucket_name)
            assert resp.get("Contents") is not None
            found = any(o["Key"] == object_key for o in resp["Contents"])
            assert found, "Created object not found in list"

        results.append(await runner.run_test("s3", "ListObjectsV2", _list_objects_v2))

        def _copy_object():
            copied_key = "copied-object.txt"
            resp = s3_client.copy_object(
                Bucket=bucket_name,
                Key=copied_key,
                CopySource=f"/{bucket_name}/{object_key}",
            )
            assert resp.get("CopyObjectResult"), "CopyObjectResult is null"

        results.append(await runner.run_test("s3", "CopyObject", _copy_object))

        def _get_object_acl():
            resp = s3_client.get_object_acl(Bucket=bucket_name, Key=object_key)
            assert resp.get("Owner"), "Owner is null"

        results.append(await runner.run_test("s3", "GetObjectAcl", _get_object_acl))

        def _put_object_acl():
            s3_client.put_object_acl(Bucket=bucket_name, Key=object_key, ACL="private")

        results.append(await runner.run_test("s3", "PutObjectAcl", _put_object_acl))

        def _delete_object():
            s3_client.delete_object(Bucket=bucket_name, Key="copied-object.txt")

        results.append(await runner.run_test("s3", "DeleteObject", _delete_object))

        def _delete_objects():
            keys_to_delete = ["obj1.txt", "obj2.txt", "obj3.txt"]
            for key in keys_to_delete:
                s3_client.put_object(
                    Bucket=bucket_name, Key=key, Body=f"content for {key}"
                )
            s3_client.delete_objects(
                Bucket=bucket_name,
                Delete={"Objects": [{"Key": k} for k in keys_to_delete]},
            )

        results.append(await runner.run_test("s3", "DeleteObjects", _delete_objects))

        def _create_multipart_upload():
            nonlocal upload_id
            multipart_key = "multipart-test.bin"
            resp = s3_client.create_multipart_upload(
                Bucket=bucket_name, Key=multipart_key
            )
            assert resp.get("UploadId"), "UploadId is null"
            upload_id = resp["UploadId"]

        upload_id = ""

        results.append(
            await runner.run_test(
                "s3", "CreateMultipartUpload", _create_multipart_upload
            )
        )

        def _upload_part():
            resp = s3_client.upload_part(
                Bucket=bucket_name,
                Key="multipart-test.bin",
                UploadId=upload_id,
                PartNumber=1,
                Body=b"part 1 content",
            )
            assert resp.get("ETag"), "ETag is null"

        results.append(await runner.run_test("s3", "UploadPart", _upload_part))

        def _list_parts():
            resp = s3_client.list_parts(
                Bucket=bucket_name, Key="multipart-test.bin", UploadId=upload_id
            )
            assert resp.get("Parts") is not None

        results.append(await runner.run_test("s3", "ListParts", _list_parts))

        def _complete_multipart_upload():
            s3_client.complete_multipart_upload(
                Bucket=bucket_name,
                Key="multipart-test.bin",
                UploadId=upload_id,
                MultipartUpload={"Parts": [{"PartNumber": 1, "ETag": '"abc123"'}]},
            )

        results.append(
            await runner.run_test(
                "s3", "CompleteMultipartUpload", _complete_multipart_upload
            )
        )

        def _abort_multipart_upload():
            create_resp = s3_client.create_multipart_upload(
                Bucket=bucket_name, Key="to-abort.bin"
            )
            assert create_resp.get("UploadId"), "UploadId is null"
            s3_client.abort_multipart_upload(
                Bucket=bucket_name,
                Key="to-abort.bin",
                UploadId=create_resp["UploadId"],
            )

        results.append(
            await runner.run_test("s3", "AbortMultipartUpload", _abort_multipart_upload)
        )

        def _put_bucket_policy():
            test_policy = {
                "Version": "2012-10-17",
                "Statement": [
                    {
                        "Effect": "Allow",
                        "Principal": "*",
                        "Action": ["s3:GetObject"],
                        "Resource": f"arn:aws:s3:::{bucket_name}/*",
                    }
                ],
            }
            s3_client.put_bucket_policy(
                Bucket=bucket_name, Policy=json.dumps(test_policy)
            )

        results.append(
            await runner.run_test("s3", "PutBucketPolicy", _put_bucket_policy)
        )

        def _get_bucket_policy():
            resp = s3_client.get_bucket_policy(Bucket=bucket_name)
            assert resp.get("Policy"), "Policy is null"

        results.append(
            await runner.run_test("s3", "GetBucketPolicy", _get_bucket_policy)
        )

        def _delete_bucket_policy():
            s3_client.delete_bucket_policy(Bucket=bucket_name)

        results.append(
            await runner.run_test("s3", "DeleteBucketPolicy", _delete_bucket_policy)
        )

        def _multi_byte_content():
            ja_key = "テスト/日本語ファイル.txt"
            zh_key = "文档/简体中文.txt"
            tw_key = "文件/繁體中文.txt"
            ja_body = "こんにちは世界。これは日本語のテストデータです。"
            zh_body = "你好世界。这是简体中文的测试数据。"
            tw_body = "你好世界。這是繁體中文的測試資料。"
            for key, body in [(ja_key, ja_body), (zh_key, zh_body), (tw_key, tw_body)]:
                s3_client.put_object(
                    Bucket=bucket_name,
                    Key=key,
                    Body=body,
                    ContentType="text/plain; charset=utf-8",
                )
            for key, body in [(ja_key, ja_body), (zh_key, zh_body), (tw_key, tw_body)]:
                resp = s3_client.get_object(Bucket=bucket_name, Key=key)
                actual = resp["Body"].read().decode("utf-8")
                assert actual == body, (
                    f"Mismatch for {key}: expected {body!r}, got {actual!r}"
                )

        results.append(
            await runner.run_test("s3", "MultiByteContent", _multi_byte_content)
        )

        def _delete_bucket():
            list_resp = s3_client.list_objects_v2(Bucket=bucket_name)
            if list_resp.get("Contents"):
                s3_client.delete_objects(
                    Bucket=bucket_name,
                    Delete={
                        "Objects": [{"Key": o["Key"]} for o in list_resp["Contents"]]
                    },
                )
            s3_client.delete_bucket(Bucket=bucket_name)

        results.append(await runner.run_test("s3", "DeleteBucket", _delete_bucket))

    finally:
        try:
            if bucket_created:
                list_resp = s3_client.list_objects_v2(Bucket=bucket_name)
                if list_resp.get("Contents"):
                    s3_client.delete_objects(
                        Bucket=bucket_name,
                        Delete={
                            "Objects": [
                                {"Key": o["Key"]} for o in list_resp["Contents"]
                            ]
                        },
                    )
                s3_client.delete_bucket(Bucket=bucket_name)
        except Exception:
            pass

    def _head_bucket_nonexistent():
        try:
            s3_client.head_bucket(Bucket="nonexistent-bucket-xyz-12345")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] in ("NoSuchBucket", "404")

    results.append(
        await runner.run_test("s3", "HeadBucket_NonExistent", _head_bucket_nonexistent)
    )

    def _get_object_nonexistent():
        tmp_bucket = f"tmp-{int(time.time() * 1000)}"
        s3_client.create_bucket(Bucket=tmp_bucket)
        try:
            s3_client.get_object(Bucket=tmp_bucket, Key="nonexistent-key-xyz-12345")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "NoSuchKey", (
                f"Expected NoSuchKey, got {e.response['Error']['Code']}"
            )
        finally:
            s3_client.delete_bucket(Bucket=tmp_bucket)

    results.append(
        await runner.run_test("s3", "GetObject_NonExistent", _get_object_nonexistent)
    )

    return results
