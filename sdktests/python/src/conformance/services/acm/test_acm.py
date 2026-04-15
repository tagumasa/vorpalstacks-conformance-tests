import pytest
from botocore.exceptions import ClientError

from conformance.conftest import assert_client_error

CERT_PEM = """-----BEGIN CERTIFICATE-----
MIIBkTCB+wIJAKHHCgVZU1JUMA0GCSqGSIb3DQEBCwUAMBExDzANBgNVBAMMBnRl
c3RjYTAeFw0yNDAxMDEwMDAwMDBaFw0yNTAxMDEwMDAwMDBaMBExDzANBgNVBAMM
BnRlc3RjYTCBnzANBgkqhkiG9w0BAQEFAAOBjQAwgYkCgYEAwK0j6f8C6hJ7u8P
-----END CERTIFICATE-----"""
PRIVATE_KEY_PEM = """-----BEGIN RSA PRIVATE KEY-----
MIIBOQIBAAJBAKjHCBmV1SlQwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAMCr
-----END RSA PRIVATE KEY-----"""
IMPORT_CERT_PEM = """-----BEGIN CERTIFICATE-----
MIICnzCCAYegAwIBAgIBATANBgkqhkiG9w0BAQsFADATMREwDwYDVQQDEwh0ZXN0
Y2VydDAeFw0yNjAzMjUwNzE1MTVaFw0yNzAzMjUwNzE1MTVaMBMxETAPBgNVBAMT
CHRlc3RjZXJ0MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAuQ1frJ4y
NJURfuVZ+ZoXaJnzH9Aca7cAl4kUlZauacQe9GeBiK9MH/gZahS5Nk7uYB3SEFf2
hRFy5O0FOhk89rztdB/iWZn346+RqRHAxBEl1LGRX0HTCaaf/uxl8uj6qraDJrOm
rCaBAU3zBQ+x7xJO0GmYT4y2rsnDdJwnElVIcNW6EcF/e7mN5F8qItLuNvLeZcgI
CEifF1Jxhj6/0LnOB2ywsrvs974lIDfvOs8wbkQJZIOZX7TOkwtUNo9FaBua5a8s
Q03SXxas6nXXBHE7yl/BlJZfneAO8KT1w067ohWpuPjCGfJN6LgXg347nE5IgyFM
gksV2rXM9SdkowIDAQABMA0GCSqGSIb3DQEBCwUAA4IBAQBCHYc/ZkJBo6m8G4I8
3/u2joYJAgo0MpsQiKre1lRuEgvsWHFbyPMBWXQkGdTydV8AIz23YV+rpPDt3/s/
BljGOu4L4o2bCjiPO5V2cv36id6e7FRfJyAmRe/S3M06jJh9HB3/uUTABITkGgee
Sa35wq1cRp86PGHhCGkEg79J8WRQmNrelttmCz/Fs4N5leuwnOlTlgCoEaLt+QSY
1DR2aPlMB0iC7yQ2UMSwdLvdWQ7ted02yYV0Hqgq/QT3wA7vfjI0SG0OUqfaJ5d2
QOl0rfDrYF2ZQNqiUX827TRg9kYRJveMjGxLhFMNVxyZJkQsbGoxJPIMikWULfk2
Xwdo
-----END CERTIFICATE-----"""
IMPORT_PRIVATE_KEY_PEM = """-----BEGIN RSA PRIVATE KEY-----
MIIEpAIBAAKCAQEAuQ1frJ4yNJURfuVZ+ZoXaJnzH9Aca7cAl4kUlZauacQe9GeB
iK9MH/gZahS5Nk7uYB3SEFf2hRFy5O0FOhk89rztdB/iWZn346+RqRHAxBEl1LGR
X0HTCaaf/uxl8uj6qraDJrOmrCaBAU3zBQ+x7xJO0GmYT4y2rsnDdJwnElVIcNW6
EcF/e7mN5F8qItLuNvLeZcgICEifF1Jxhj6/0LnOB2ywsrvs974lIDfvOs8wbkQJ
ZIOZX7TOkwtUNo9FaBua5a8sQ03SXxas6nXXBHE7yl/BlJZfneAO8KT1w067ohWp
uPjCGfJN6LgXg347nE5IgyFMgksV2rXM9SdkowIDAQABAoIBAEseUi2kxBWTQ5hi
6szHT+ROxiIuXTMehPd+lmQI2EEn8zbcQ3lkS38Yu9xTkEGq9dn/kPPAeVpYFG84
hewpLZWtaKjAfqZHuZhr/zGF+t28ZkJ6WFw2QMBEquMVPGdISuT8lK2jtK9iK/EH
HvT5g43cPTEeBE2afdfjIFwYPUYTto2bC1dIsPJ66IH3AUN6uwnYLfIlyomvIxGJ
iwsNZloOEMtjpvf8Q/5JbfioTYwBMGS4SZetPl4CSnASLI44jZPU7hWHDRhlM0OS
U3TzqacbAxNm1tzkzJARxyCd9GatyuLqNSgph0QqW/VXkOH10kCMoGRUmmMc6gWe
40yaH4kCgYEAziBz4/RnEs+WMqs0IwoKQtgU9blXTXNgIs9WS0Eyn3XgigZB9IxB
BIJHbltDSeX5/TO3iyhE0hIeEDukSsuDzMt+O2N2ZOac+4UnRcqczD12XiLLysru
mfep9MUNrIUj+UaMB1ZPyVfxGyfIc8An9RlayBN7jsDZi+Pj3dTSfpsCgYEA5dOP
JVTGgC0ZcK7w+xev2iCDixHMvX4ofm4iKd9eYM3RXKnUulDbTL4GcACjbW82IG5z
0TfEdAF4lNW3C1bCSDhIWM1P3Nc1zPnH65RZju3oSYvToDk/1PXcWTmCcWXA2twq
JE8NRBaHtFjBMqu/5KddcIoohIlTRiC/V/d7zpkCgYEAg88UzIwY7Vp5PWVlLZLa
BOyQWqFuRkSlER1snSrP6FBEiX5+5pZZbTyx2MvbN4IsXdGYaRATEhIrz02UPY/u
dCMcUXXE27jsYZpABs0Nfz0+V+wATWl/Mk3BDJiFqfBplJmcKYTz+FiYATlrYTlb
U8wm1RJATITdmCreJ5hUEkkCgYEAhqlvNnB13qSOQ3g9uuImJ6jlapcDYASLtYjS
e7ZlllMCWUkpXAIEfPLa0sWM/JItJNOTCQOkGFTEUnDmz74GGEriGSYzpTJ0U6YH
fgFueFDtyioj1b21qRJmCeGojMkSNyrJhnzLSRnqacGXchkwVsm59jb9hqrwICcP
9nsMEAECgYB498ktMUMajMgNyKc4bIL92EzScPcTIfn+1a22wd0ZJkiEtTotMwPh
Nw1sf/uZ5JyJwTEr6FU4qBk+zc/M3+4f8VG5ChVMt6mPEVwHAlgscwODj1pxO7nz
Vzw7YxT498cnLJsBFDy+kk9uKMf7cpLCdRF1gRpeIP3K6sFLNF96Gw==
-----END RSA PRIVATE KEY-----"""


@pytest.fixture(scope="module")
def describe_cert_arn(acm_client, unique_name):
    domain = unique_name("describe-test") + ".com"
    resp = acm_client.request_certificate(DomainName=domain, ValidationMethod="DNS")
    arn = resp.get("CertificateArn", "")
    yield arn
    if arn:
        try:
            acm_client.delete_certificate(CertificateArn=arn)
        except Exception:
            pass


@pytest.fixture(scope="module")
def cert_arn(acm_client, unique_name):
    domain = unique_name("example") + ".com"
    resp = acm_client.request_certificate(
        DomainName=domain,
        ValidationMethod="DNS",
        IdempotencyToken="test-token",
    )
    arn = resp.get("CertificateArn", "")
    yield arn
    if arn:
        try:
            acm_client.delete_certificate(CertificateArn=arn)
        except Exception:
            pass


class TestCertificateListing:
    def test_list_certificates(self, acm_client):
        acm_client.list_certificates(MaxItems=10)


class TestCertificateDescribe:
    def test_describe_certificate(self, acm_client, describe_cert_arn):
        acm_client.describe_certificate(CertificateArn=describe_cert_arn)

    def test_get_certificate(self, acm_client, describe_cert_arn):
        acm_client.get_certificate(CertificateArn=describe_cert_arn)


class TestCertificateRequest:
    def test_request_certificate(self, cert_arn):
        assert cert_arn

    def test_describe_certificate_created(self, acm_client, cert_arn):
        acm_client.describe_certificate(CertificateArn=cert_arn)

    def test_delete_certificate(self, acm_client, cert_arn):
        acm_client.delete_certificate(CertificateArn=cert_arn)


class TestCertificateTags:
    def test_add_tags_to_certificate(self, acm_client, unique_name):
        domain = unique_name("example2") + ".com"
        arn = ""
        try:
            resp = acm_client.request_certificate(
                DomainName=domain, ValidationMethod="DNS"
            )
            arn = resp.get("CertificateArn", "")
            acm_client.add_tags_to_certificate(
                CertificateArn=arn,
                Tags=[
                    {"Key": "Environment", "Value": "test"},
                    {"Key": "Owner", "Value": "test-user"},
                ],
            )
        finally:
            if arn:
                try:
                    acm_client.delete_certificate(CertificateArn=arn)
                except Exception:
                    pass

    def test_list_tags_for_certificate(self, acm_client, unique_name):
        domain = unique_name("example3") + ".com"
        arn = ""
        try:
            resp = acm_client.request_certificate(
                DomainName=domain, ValidationMethod="DNS"
            )
            arn = resp.get("CertificateArn", "")
            acm_client.add_tags_to_certificate(
                CertificateArn=arn, Tags=[{"Key": "Test", "Value": "value"}]
            )
            acm_client.list_tags_for_certificate(CertificateArn=arn)
        finally:
            if arn:
                try:
                    acm_client.delete_certificate(CertificateArn=arn)
                except Exception:
                    pass

    def test_remove_tags_from_certificate(self, acm_client, unique_name):
        domain = unique_name("example4") + ".com"
        arn = ""
        try:
            resp = acm_client.request_certificate(
                DomainName=domain, ValidationMethod="DNS"
            )
            arn = resp.get("CertificateArn", "")
            acm_client.add_tags_to_certificate(
                CertificateArn=arn, Tags=[{"Key": "Test", "Value": "value"}]
            )
            acm_client.remove_tags_from_certificate(
                CertificateArn=arn, Tags=[{"Key": "Test"}]
            )
        finally:
            if arn:
                try:
                    acm_client.delete_certificate(CertificateArn=arn)
                except Exception:
                    pass


class TestCertificateOperations:
    def test_resend_validation_email(self, acm_client, unique_name):
        domain = unique_name("example5") + ".com"
        arn = ""
        try:
            resp = acm_client.request_certificate(
                DomainName=domain, ValidationMethod="EMAIL"
            )
            arn = resp.get("CertificateArn", "")
            acm_client.resend_validation_email(
                CertificateArn=arn,
                Domain=domain,
                ValidationDomain=domain,
            )
        finally:
            if arn:
                try:
                    acm_client.delete_certificate(CertificateArn=arn)
                except Exception:
                    pass

    def test_update_certificate_options(self, acm_client, unique_name):
        domain = unique_name("example6") + ".com"
        arn = ""
        try:
            resp = acm_client.request_certificate(
                DomainName=domain, ValidationMethod="DNS"
            )
            arn = resp.get("CertificateArn", "")
            acm_client.update_certificate_options(
                CertificateArn=arn,
                Options={"CertificateTransparencyLoggingPreference": "ENABLED"},
            )
        finally:
            if arn:
                try:
                    acm_client.delete_certificate(CertificateArn=arn)
                except Exception:
                    pass


class TestCertificateImport:
    def test_import_certificate(self, acm_client):
        acm_client.import_certificate(
            Certificate=CERT_PEM.encode(), PrivateKey=PRIVATE_KEY_PEM.encode()
        )

    def test_export_certificate(self, acm_client):
        import_resp = acm_client.import_certificate(
            Certificate=IMPORT_CERT_PEM.encode(),
            PrivateKey=IMPORT_PRIVATE_KEY_PEM.encode(),
        )
        cert_arn_imported = import_resp.get("CertificateArn", "")
        if cert_arn_imported:
            try:
                acm_client.export_certificate(
                    CertificateArn=cert_arn_imported, Passphrase=b"test-passphrase"
                )
            finally:
                try:
                    acm_client.delete_certificate(CertificateArn=cert_arn_imported)
                except Exception:
                    pass


class TestAccountConfiguration:
    def test_get_account_configuration(self, acm_client):
        acm_client.get_account_configuration()

    def test_put_account_configuration(self, acm_client):
        acm_client.put_account_configuration(
            IdempotencyToken="test-token", ExpiryEvents={"DaysBeforeExpiry": 30}
        )


class TestErrorCases:
    def test_describe_certificate_nonexistent(self, acm_client):
        with pytest.raises(ClientError) as exc_info:
            acm_client.describe_certificate(
                CertificateArn="arn:aws:acm:us-east-1:000000000000:certificate/00000000-0000-0000-0000-000000000000"
            )
        assert_client_error(exc_info, "ResourceNotFoundException")

    def test_delete_certificate_nonexistent(self, acm_client):
        with pytest.raises(ClientError) as exc_info:
            acm_client.delete_certificate(
                CertificateArn="arn:aws:acm:us-east-1:000000000000:certificate/00000000-0000-0000-0000-000000000000"
            )
        assert_client_error(exc_info, "ResourceNotFoundException")
