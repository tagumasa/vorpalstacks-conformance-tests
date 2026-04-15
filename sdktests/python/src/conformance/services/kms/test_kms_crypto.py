import pytest


@pytest.fixture(scope="module")
def kms_key(kms_client):
    resp = kms_client.create_key(
        KeyUsage="ENCRYPT_DECRYPT",
        Description="Crypto test key",
    )
    key_id = resp["KeyMetadata"]["KeyId"]
    yield key_id
    try:
        kms_client.schedule_key_deletion(KeyId=key_id, PendingWindowInDays=7)
    except Exception:
        pass


@pytest.fixture(scope="module")
def kms_mac_key(kms_client):
    resp = kms_client.create_key(
        KeyUsage="GENERATE_VERIFY_MAC",
        KeySpec="HMAC_256",
        Description="MAC key for SDK tests",
    )
    key_id = resp["KeyMetadata"]["KeyId"]
    yield key_id
    try:
        kms_client.schedule_key_deletion(KeyId=key_id, PendingWindowInDays=7)
    except Exception:
        pass


class TestEncrypt:
    def test_encrypt(self, kms_client, kms_key):
        resp = kms_client.encrypt(KeyId=kms_key, Plaintext=b"Hello, KMS!")
        assert resp.get("CiphertextBlob")


class TestDecrypt:
    def test_encrypt_decrypt_roundtrip(self, kms_client, kms_key):
        plaintext = b"Hello, KMS!"
        enc_resp = kms_client.encrypt(KeyId=kms_key, Plaintext=plaintext)
        dec_resp = kms_client.decrypt(CiphertextBlob=enc_resp["CiphertextBlob"])
        assert dec_resp["Plaintext"] == plaintext

    def test_encrypt_decrypt_long_data(self, kms_client, kms_key):
        plaintext = b"roundtrip-test-data-12345-" * 100
        enc_resp = kms_client.encrypt(KeyId=kms_key, Plaintext=plaintext)
        dec_resp = kms_client.decrypt(CiphertextBlob=enc_resp["CiphertextBlob"])
        assert dec_resp["Plaintext"] == plaintext

    def test_encrypt_for_decrypt(self, kms_client, kms_key):
        plaintext = b"Hello, KMS!"
        resp = kms_client.encrypt(KeyId=kms_key, Plaintext=plaintext)
        kms_client.decrypt(CiphertextBlob=resp["CiphertextBlob"])


class TestGenerateDataKey:
    def test_generate_data_key(self, kms_client, kms_key):
        resp = kms_client.generate_data_key(KeyId=kms_key, KeySpec="AES_256")
        assert resp.get("CiphertextBlob")
        assert resp.get("Plaintext")
        assert len(resp["Plaintext"]) == 32

    def test_generate_data_key_content_verify(self, kms_client):
        resp = kms_client.create_key(Description="Verify key")
        try:
            key_id = resp["KeyMetadata"]["KeyId"]
            dk_resp = kms_client.generate_data_key(KeyId=key_id, KeySpec="AES_256")
            assert len(dk_resp["Plaintext"]) == 32
            assert len(dk_resp["CiphertextBlob"]) > 0
            assert len(dk_resp["Plaintext"]) != len(dk_resp["CiphertextBlob"])
        finally:
            try:
                kms_client.schedule_key_deletion(
                    KeyId=resp["KeyMetadata"]["KeyId"], PendingWindowInDays=7
                )
            except Exception:
                pass


class TestGenerateDataKeyWithoutPlaintext:
    def test_generate_data_key_without_plaintext(self, kms_client, kms_key):
        resp = kms_client.generate_data_key_without_plaintext(
            KeyId=kms_key, KeySpec="AES_256"
        )
        assert resp.get("CiphertextBlob")
        assert len(resp["CiphertextBlob"]) > 0


class TestGenerateRandom:
    def test_generate_random(self, kms_client):
        resp = kms_client.generate_random(NumberOfBytes=32)
        assert resp.get("Plaintext")
        assert len(resp["Plaintext"]) == 32


class TestGenerateDataKeyPair:
    def test_generate_data_key_pair(self, kms_client, kms_key):
        resp = kms_client.generate_data_key_pair(KeyId=kms_key, KeyPairSpec="RSA_2048")
        assert resp.get("PrivateKeyCiphertextBlob")
        assert resp.get("PublicKey")


class TestReEncrypt:
    def test_re_encrypt(self, kms_client, kms_key):
        plaintext = b"Hello, KMS!"
        enc_resp = kms_client.encrypt(KeyId=kms_key, Plaintext=plaintext)
        resp = kms_client.re_encrypt(
            CiphertextBlob=enc_resp["CiphertextBlob"], DestinationKeyId=kms_key
        )
        assert resp.get("CiphertextBlob")

    def test_re_encrypt_with_different_key(self, kms_client):
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
            assert dec_resp["Plaintext"] == plaintext
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


class TestGenerateMac:
    def test_generate_mac(self, kms_client, kms_mac_key):
        resp = kms_client.generate_mac(
            KeyId=kms_mac_key, Message=b"test message", MacAlgorithm="HMAC_SHA_256"
        )
        assert resp.get("Mac")


class TestVerifyMac:
    def test_verify_mac(self, kms_client, kms_mac_key):
        kms_client.enable_key(KeyId=kms_mac_key)
        mac_resp = kms_client.generate_mac(
            KeyId=kms_mac_key, Message=b"test message", MacAlgorithm="HMAC_SHA_256"
        )
        verify_resp = kms_client.verify_mac(
            KeyId=kms_mac_key,
            Message=b"test message",
            Mac=mac_resp["Mac"],
            MacAlgorithm="HMAC_SHA_256",
        )
        assert verify_resp.get("MacValid") is True
