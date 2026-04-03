import json
import time
import uuid
from botocore.exceptions import ClientError
from ..runner import TestRunner, TestResult


def _make_unique_name(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"


async def run_kms_tests(
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
    kms_client = session.client("kms", endpoint_url=endpoint, region_name=region)

    key_alias = "alias/" + _make_unique_name("PyKey")
    key_id = ""
    key_arn = ""
    grant_id = ""
    grant_token = ""
    mac_key_id = ""
    ciphertext_blob = None

    try:

        def _create_key():
            nonlocal key_id, key_arn
            resp = kms_client.create_key(
                KeyUsage="ENCRYPT_DECRYPT",
                Description="Test key for SDK tests",
                Tags=[{"TagKey": "Environment", "TagValue": "Test"}],
            )
            assert resp["KeyMetadata"], "KeyMetadata is null"
            assert resp["KeyMetadata"]["KeyId"], "KeyId is null"
            key_id = resp["KeyMetadata"]["KeyId"]
            key_arn = resp["KeyMetadata"].get("Arn", "")

        results.append(await runner.run_test("kms", "CreateKey", _create_key))

        def _describe_key():
            resp = kms_client.describe_key(KeyId=key_id)
            assert resp["KeyMetadata"], "KeyMetadata is null"
            assert resp["KeyMetadata"]["KeyId"], "KeyId is null"

        results.append(await runner.run_test("kms", "DescribeKey", _describe_key))

        def _list_keys():
            resp = kms_client.list_keys()
            assert resp["Keys"] is not None
            found = any(k["KeyId"] == key_id for k in resp["Keys"])
            assert found, "Created key not found in list"

        results.append(await runner.run_test("kms", "ListKeys", _list_keys))

        def _update_key_description():
            kms_client.update_key_description(
                KeyId=key_id, Description="Updated test key description"
            )

        results.append(
            await runner.run_test(
                "kms", "UpdateKeyDescription", _update_key_description
            )
        )

        def _get_key_policy():
            resp = kms_client.get_key_policy(KeyId=key_id, PolicyName="default")
            assert resp.get("Policy"), "Policy is null"

        results.append(await runner.run_test("kms", "GetKeyPolicy", _get_key_policy))

        def _create_grant():
            nonlocal grant_id, grant_token
            resp = kms_client.create_grant(
                KeyId=key_id,
                GranteePrincipal="arn:aws:iam::000000000000:user/test",
                Operations=["Encrypt", "Decrypt", "GenerateDataKey"],
            )
            assert resp["GrantId"], "GrantId is null"
            grant_id = resp["GrantId"]
            if resp.get("GrantToken"):
                grant_token = resp["GrantToken"]

        results.append(await runner.run_test("kms", "CreateGrant", _create_grant))

        def _list_grants():
            resp = kms_client.list_grants(KeyId=key_id)
            assert resp["Grants"] is not None
            found = any(g["GrantId"] == grant_id for g in resp["Grants"])
            assert found, "Created grant not found in list"

        results.append(await runner.run_test("kms", "ListGrants", _list_grants))

        def _list_retirable_grants():
            resp = kms_client.list_retirable_grants(
                RetiringPrincipal="arn:aws:iam::000000000000:user/TestUser"
            )
            assert resp["Grants"] is not None

        results.append(
            await runner.run_test("kms", "ListRetirableGrants", _list_retirable_grants)
        )

        def _enable_key():
            kms_client.enable_key(KeyId=key_id)

        results.append(await runner.run_test("kms", "EnableKey", _enable_key))

        def _encrypt():
            plaintext = b"Hello, KMS!"
            resp = kms_client.encrypt(KeyId=key_id, Plaintext=plaintext)
            assert resp.get("CiphertextBlob"), "CiphertextBlob is null"

        results.append(await runner.run_test("kms", "Encrypt", _encrypt))

        def _encrypt_for_decrypt():
            nonlocal ciphertext_blob
            plaintext = b"Hello, KMS!"
            resp = kms_client.encrypt(KeyId=key_id, Plaintext=plaintext)
            ciphertext_blob = resp["CiphertextBlob"]

        results.append(
            await runner.run_test("kms", "Encrypt_ForDecrypt", _encrypt_for_decrypt)
        )

        def _decrypt():
            assert ciphertext_blob is not None, "ciphertext not available"
            kms_client.decrypt(CiphertextBlob=ciphertext_blob)

        results.append(await runner.run_test("kms", "Decrypt", _decrypt))

        def _generate_data_key():
            resp = kms_client.generate_data_key(KeyId=key_id, KeySpec="AES_256")
            assert resp.get("CiphertextBlob"), "CiphertextBlob is null"
            assert resp.get("Plaintext"), "Plaintext is null"
            assert len(resp["Plaintext"]) == 32, (
                f"expected 32-byte plaintext, got {len(resp['Plaintext'])}"
            )

        results.append(
            await runner.run_test("kms", "GenerateDataKey", _generate_data_key)
        )

        def _generate_data_key_without_plaintext():
            resp = kms_client.generate_data_key_without_plaintext(
                KeyId=key_id, KeySpec="AES_256"
            )
            assert resp.get("CiphertextBlob"), "CiphertextBlob is null"
            assert len(resp["CiphertextBlob"]) > 0, "CiphertextBlob is empty"

        results.append(
            await runner.run_test(
                "kms",
                "GenerateDataKeyWithoutPlaintext",
                _generate_data_key_without_plaintext,
            )
        )

        def _generate_random():
            resp = kms_client.generate_random(NumberOfBytes=32)
            assert resp.get("Plaintext"), "Plaintext is null"
            assert len(resp["Plaintext"]) == 32, (
                f"expected 32 bytes, got {len(resp['Plaintext'])}"
            )

        results.append(await runner.run_test("kms", "GenerateRandom", _generate_random))

        def _generate_data_key_pair():
            resp = kms_client.generate_data_key_pair(
                KeyId=key_id, KeyPairSpec="RSA_2048"
            )
            assert resp.get("PrivateKeyCiphertextBlob"), (
                "PrivateKeyCiphertextBlob is null"
            )
            assert resp.get("PublicKey"), "PublicKey is null"

        results.append(
            await runner.run_test("kms", "GenerateDataKeyPair", _generate_data_key_pair)
        )

        def _generate_mac():
            nonlocal mac_key_id
            mac_resp = kms_client.create_key(
                KeyUsage="GENERATE_VERIFY_MAC",
                KeySpec="HMAC_256",
                Description="MAC key for SDK tests",
            )
            mac_key_id = mac_resp["KeyMetadata"]["KeyId"]
            resp = kms_client.generate_mac(
                KeyId=mac_key_id, Message=b"test message", MacAlgorithm="HMAC_SHA_256"
            )
            assert resp.get("Mac"), "Mac is null"

        results.append(await runner.run_test("kms", "GenerateMac", _generate_mac))

        def _verify_mac():
            kms_client.enable_key(KeyId=mac_key_id)
            mac_resp = kms_client.generate_mac(
                KeyId=mac_key_id, Message=b"test message", MacAlgorithm="HMAC_SHA_256"
            )
            verify_resp = kms_client.verify_mac(
                KeyId=mac_key_id,
                Message=b"test message",
                Mac=mac_resp["Mac"],
                MacAlgorithm="HMAC_SHA_256",
            )
            assert verify_resp.get("MacValid") is True

        results.append(await runner.run_test("kms", "VerifyMac", _verify_mac))

        def _re_encrypt():
            assert ciphertext_blob is not None, "ciphertext not available"
            resp = kms_client.re_encrypt(
                CiphertextBlob=ciphertext_blob, DestinationKeyId=key_id
            )
            assert resp.get("CiphertextBlob"), "Re-encrypted CiphertextBlob is null"

        results.append(await runner.run_test("kms", "ReEncrypt", _re_encrypt))

        def _put_key_policy():
            policy = json.dumps(
                {
                    "Version": "2012-10-17",
                    "Statement": [
                        {
                            "Effect": "Allow",
                            "Principal": {"AWS": "*"},
                            "Action": "kms:*",
                            "Resource": "*",
                        }
                    ],
                }
            )
            kms_client.put_key_policy(KeyId=key_id, PolicyName="default", Policy=policy)

        results.append(await runner.run_test("kms", "PutKeyPolicy", _put_key_policy))

        def _list_key_policies():
            resp = kms_client.list_key_policies(KeyId=key_id)
            assert resp.get("PolicyNames") is not None, "PolicyNames is null"

        results.append(
            await runner.run_test("kms", "ListKeyPolicies", _list_key_policies)
        )

        def _tag_resource():
            kms_client.tag_resource(
                KeyId=key_id,
                Tags=[
                    {"TagKey": "Environment", "TagValue": "test"},
                    {"TagKey": "Project", "TagValue": "sdk-tests"},
                ],
            )

        results.append(await runner.run_test("kms", "TagResource", _tag_resource))

        def _list_resource_tags():
            resp = kms_client.list_resource_tags(KeyId=key_id)
            assert resp.get("Tags") is not None, "Tags is null"

        results.append(
            await runner.run_test("kms", "ListResourceTags", _list_resource_tags)
        )

        def _untag_resource():
            kms_client.untag_resource(KeyId=key_id, TagKeys=["Environment"])

        results.append(await runner.run_test("kms", "UntagResource", _untag_resource))

        def _create_alias():
            kms_client.create_alias(AliasName=key_alias, TargetKeyId=key_id)

        results.append(await runner.run_test("kms", "CreateAlias", _create_alias))

        def _list_aliases():
            resp = kms_client.list_aliases()
            assert resp.get("Aliases") is not None, "Aliases is null"

        results.append(await runner.run_test("kms", "ListAliases", _list_aliases))

        def _update_alias():
            nonlocal key_alias
            new_alias = _make_unique_name("PyKeyUpdated")
            kms_client.update_alias(AliasName=key_alias, TargetKeyId=key_id)
            list_resp = kms_client.list_aliases()
            found = any(a["AliasName"] == key_alias for a in list_resp["Aliases"])
            assert found, f"alias {key_alias} not found after update"

        results.append(await runner.run_test("kms", "UpdateAlias", _update_alias))

        def _enable_key_rotation():
            kms_client.enable_key_rotation(KeyId=key_id)

        results.append(
            await runner.run_test("kms", "EnableKeyRotation", _enable_key_rotation)
        )

        def _get_key_rotation_status():
            kms_client.get_key_rotation_status(KeyId=key_id)

        results.append(
            await runner.run_test(
                "kms", "GetKeyRotationStatus", _get_key_rotation_status
            )
        )

        def _disable_key_rotation():
            kms_client.disable_key_rotation(KeyId=key_id)

        results.append(
            await runner.run_test("kms", "DisableKeyRotation", _disable_key_rotation)
        )

        def _disable_key():
            kms_client.disable_key(KeyId=key_id)

        results.append(await runner.run_test("kms", "DisableKey", _disable_key))

        def _enable_key_after_disable():
            kms_client.enable_key(KeyId=key_id)

        results.append(
            await runner.run_test(
                "kms", "EnableKey_AfterDisable", _enable_key_after_disable
            )
        )

        def _schedule_key_deletion():
            resp = kms_client.schedule_key_deletion(KeyId=key_id, PendingWindowInDays=7)
            assert resp.get("DeletionDate"), "DeletionDate is null"

        results.append(
            await runner.run_test("kms", "ScheduleKeyDeletion", _schedule_key_deletion)
        )

        def _cancel_key_deletion():
            resp = kms_client.cancel_key_deletion(KeyId=key_id)
            assert resp.get("KeyId"), "KeyId in response is null"

        results.append(
            await runner.run_test("kms", "CancelKeyDeletion", _cancel_key_deletion)
        )

        def _delete_alias():
            kms_client.delete_alias(AliasName=key_alias)

        results.append(await runner.run_test("kms", "DeleteAlias", _delete_alias))

        def _retire_grant():
            retire_grant_resp = kms_client.create_grant(
                KeyId=key_id,
                GranteePrincipal="arn:aws:iam::000000000000:user/test",
                Operations=["Encrypt", "Decrypt"],
            )
            kms_client.retire_grant(GrantToken=retire_grant_resp["GrantToken"])

        results.append(await runner.run_test("kms", "RetireGrant", _retire_grant))

        def _revoke_grant():
            kms_client.revoke_grant(KeyId=key_id, GrantId=grant_id)

        results.append(await runner.run_test("kms", "RevokeGrant", _revoke_grant))

    finally:
        try:
            kms_client.schedule_key_deletion(KeyId=key_id, PendingWindowInDays=7)
        except Exception:
            pass
        try:
            kms_client.schedule_key_deletion(KeyId=mac_key_id, PendingWindowInDays=7)
        except Exception:
            pass

    def _describe_key_nonexistent():
        try:
            kms_client.describe_key(KeyId="12345678-1234-1234-1234-123456789012")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] in (
                "NotFoundException",
                "DependencyTimeoutException",
            )

    results.append(
        await runner.run_test(
            "kms", "DescribeKey_NonExistent", _describe_key_nonexistent
        )
    )

    def _encrypt_nonexistent():
        try:
            kms_client.encrypt(
                KeyId="12345678-1234-1234-1234-123456789012",
                Plaintext=b"test",
            )
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] in (
                "NotFoundException",
                "DependencyTimeoutException",
            )

    results.append(
        await runner.run_test("kms", "Encrypt_NonExistent", _encrypt_nonexistent)
    )

    def _decrypt_nonexistent():
        try:
            kms_client.decrypt(CiphertextBlob=b"invalid ciphertext")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] in (
                "InvalidCiphertextException",
                "NotFoundException",
            )

    results.append(
        await runner.run_test("kms", "Decrypt_NonExistent", _decrypt_nonexistent)
    )

    def _encrypt_decrypt_roundtrip():
        rt_resp = kms_client.create_key(Description="Roundtrip test key")
        try:
            rt_key_id = rt_resp["KeyMetadata"]["KeyId"]
            plaintext = b"roundtrip-test-data-12345"
            enc_resp = kms_client.encrypt(KeyId=rt_key_id, Plaintext=plaintext)
            dec_resp = kms_client.decrypt(CiphertextBlob=enc_resp["CiphertextBlob"])
            assert dec_resp["Plaintext"] == plaintext, "plaintext mismatch"
        finally:
            try:
                kms_client.schedule_key_deletion(
                    KeyId=rt_resp["KeyMetadata"]["KeyId"], PendingWindowInDays=7
                )
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "kms", "Encrypt_DecryptRoundtrip", _encrypt_decrypt_roundtrip
        )
    )

    def _generate_data_key_content_verify():
        v_resp = kms_client.create_key(Description="Verify key")
        try:
            resp = kms_client.generate_data_key(
                KeyId=v_resp["KeyMetadata"]["KeyId"], KeySpec="AES_256"
            )
            assert len(resp["Plaintext"]) == 32, (
                f"expected 32-byte plaintext, got {len(resp['Plaintext'])}"
            )
            assert len(resp["CiphertextBlob"]) > 0, "CiphertextBlob is empty"
            assert len(resp["Plaintext"]) != len(resp["CiphertextBlob"]), (
                "plaintext and ciphertext should have different lengths"
            )
        finally:
            try:
                kms_client.schedule_key_deletion(
                    KeyId=v_resp["KeyMetadata"]["KeyId"], PendingWindowInDays=7
                )
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "kms", "GenerateDataKey_ContentVerify", _generate_data_key_content_verify
        )
    )

    def _create_alias_duplicate():
        dup_resp = kms_client.create_key(Description="Dup alias test")
        try:
            dup_alias = "alias/" + _make_unique_name("DupAlias")
            kms_client.create_alias(
                AliasName=dup_alias, TargetKeyId=dup_resp["KeyMetadata"]["KeyId"]
            )
            try:
                try:
                    kms_client.create_alias(
                        AliasName=dup_alias,
                        TargetKeyId=dup_resp["KeyMetadata"]["KeyId"],
                    )
                    raise AssertionError("Expected error for duplicate alias")
                except ClientError as e:
                    assert e.response["Error"]["Code"] == "AlreadyExistsException", (
                        f"expected AlreadyExistsException, got {e.response['Error']['Code']}"
                    )
            finally:
                try:
                    kms_client.delete_alias(AliasName=dup_alias)
                except Exception:
                    pass
        finally:
            try:
                kms_client.schedule_key_deletion(
                    KeyId=dup_resp["KeyMetadata"]["KeyId"], PendingWindowInDays=7
                )
            except Exception:
                pass

    results.append(
        await runner.run_test("kms", "CreateAlias_Duplicate", _create_alias_duplicate)
    )

    def _encrypt_disabled_key():
        dis_resp = kms_client.create_key(Description="Disable test")
        try:
            dis_key_id = dis_resp["KeyMetadata"]["KeyId"]
            kms_client.disable_key(KeyId=dis_key_id)
            try:
                try:
                    kms_client.encrypt(KeyId=dis_key_id, Plaintext=b"should fail")
                    raise AssertionError(
                        "Expected error when encrypting with disabled key"
                    )
                except ClientError as e:
                    assert e.response["Error"]["Code"] == "DisabledException", (
                        f"expected DisabledException, got {e.response['Error']['Code']}"
                    )
            finally:
                try:
                    kms_client.enable_key(KeyId=dis_key_id)
                except Exception:
                    pass
        finally:
            try:
                kms_client.schedule_key_deletion(
                    KeyId=dis_resp["KeyMetadata"]["KeyId"], PendingWindowInDays=7
                )
            except Exception:
                pass

    results.append(
        await runner.run_test("kms", "Encrypt_DisabledKey", _encrypt_disabled_key)
    )

    def _schedule_key_deletion_invalid_window():
        inv_resp = kms_client.create_key(Description="Invalid window test")
        try:
            try:
                kms_client.schedule_key_deletion(
                    KeyId=inv_resp["KeyMetadata"]["KeyId"], PendingWindowInDays=3
                )
                raise AssertionError(
                    "Expected error for invalid pending window (3 days, min is 7)"
                )
            except ClientError:
                pass
        finally:
            try:
                kms_client.schedule_key_deletion(
                    KeyId=inv_resp["KeyMetadata"]["KeyId"], PendingWindowInDays=7
                )
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "kms",
            "ScheduleKeyDeletion_InvalidWindow",
            _schedule_key_deletion_invalid_window,
        )
    )

    def _list_aliases_contains_created():
        la_resp = kms_client.create_key(Description="List alias test")
        try:
            la_alias = "alias/" + _make_unique_name("LaAlias")
            kms_client.create_alias(
                AliasName=la_alias, TargetKeyId=la_resp["KeyMetadata"]["KeyId"]
            )
            try:
                list_resp = kms_client.list_aliases()
                found = any(a["AliasName"] == la_alias for a in list_resp["Aliases"])
                assert found, f"Created alias {la_alias} not found in ListAliases"
            finally:
                try:
                    kms_client.delete_alias(AliasName=la_alias)
                except Exception:
                    pass
        finally:
            try:
                kms_client.schedule_key_deletion(
                    KeyId=la_resp["KeyMetadata"]["KeyId"], PendingWindowInDays=7
                )
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "kms", "ListAliases_ContainsCreated", _list_aliases_contains_created
        )
    )

    def _get_key_policy_content_verify():
        gp_resp = kms_client.create_key(Description="Policy verify")
        try:
            policy = json.dumps(
                {
                    "Version": "2012-10-17",
                    "Statement": [
                        {
                            "Effect": "Allow",
                            "Principal": {"AWS": "*"},
                            "Action": "kms:*",
                            "Resource": "*",
                        }
                    ],
                }
            )
            kms_client.put_key_policy(
                KeyId=gp_resp["KeyMetadata"]["KeyId"],
                PolicyName="default",
                Policy=policy,
            )
            get_resp = kms_client.get_key_policy(
                KeyId=gp_resp["KeyMetadata"]["KeyId"], PolicyName="default"
            )
            assert get_resp["Policy"] == policy, "policy content mismatch"
        finally:
            try:
                kms_client.schedule_key_deletion(
                    KeyId=gp_resp["KeyMetadata"]["KeyId"], PendingWindowInDays=7
                )
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "kms", "GetKeyPolicy_ContentVerify", _get_key_policy_content_verify
        )
    )

    def _re_encrypt_with_different_key():
        re1 = kms_client.create_key(Description="ReEncrypt source")
        re2 = kms_client.create_key(Description="ReEncrypt dest")
        try:
            plaintext = b"re-encrypt-test"
            enc_resp = kms_client.encrypt(
                KeyId=re1["KeyMetadata"]["KeyId"], Plaintext=plaintext
            )
            re_resp = kms_client.re_encrypt(
                CiphertextBlob=enc_resp["CiphertextBlob"],
                DestinationKeyId=re2["KeyMetadata"]["KeyId"],
            )
            dec_resp = kms_client.decrypt(
                CiphertextBlob=re_resp["CiphertextBlob"],
                KeyId=re2["KeyMetadata"]["KeyId"],
            )
            assert dec_resp["Plaintext"] == plaintext, (
                "plaintext mismatch after re-encrypt"
            )
        finally:
            try:
                kms_client.schedule_key_deletion(
                    KeyId=re1["KeyMetadata"]["KeyId"], PendingWindowInDays=7
                )
            except Exception:
                pass
            try:
                kms_client.schedule_key_deletion(
                    KeyId=re2["KeyMetadata"]["KeyId"], PendingWindowInDays=7
                )
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "kms", "ReEncrypt_WithDifferentKey", _re_encrypt_with_different_key
        )
    )

    return results
