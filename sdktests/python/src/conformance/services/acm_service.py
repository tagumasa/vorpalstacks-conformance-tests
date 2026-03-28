import time
import uuid
from botocore.exceptions import ClientError
from ..runner import TestRunner, TestResult


def _make_unique_name(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"


async def run_acm_tests(
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
    acm_client = session.client("acm", endpoint_url=endpoint, region_name=region)

    def _list_certificates():
        acm_client.list_certificates(MaxItems=10)

    results.append(await runner.run_test("acm", "ListCertificates", _list_certificates))

    test_domain = _make_unique_name("describe-test") + ".com"
    test_cert_arn = ""

    def _describe_certificate():
        nonlocal test_cert_arn
        resp = acm_client.request_certificate(
            DomainName=test_domain, ValidationMethod="DNS"
        )
        test_cert_arn = resp.get("CertificateArn", "")
        acm_client.describe_certificate(CertificateArn=test_cert_arn)

    results.append(
        await runner.run_test("acm", "DescribeCertificate", _describe_certificate)
    )

    if test_cert_arn:

        def _get_certificate():
            acm_client.get_certificate(CertificateArn=test_cert_arn)

        results.append(await runner.run_test("acm", "GetCertificate", _get_certificate))

    domain_name = _make_unique_name("example") + ".com"
    cert_arn = ""

    def _request_certificate():
        nonlocal cert_arn
        resp = acm_client.request_certificate(
            DomainName=domain_name,
            ValidationMethod="DNS",
            IdempotencyToken="test-token",
        )
        cert_arn = resp.get("CertificateArn", "")

    results.append(
        await runner.run_test("acm", "RequestCertificate", _request_certificate)
    )

    if cert_arn:

        def _describe_certificate_created():
            acm_client.describe_certificate(CertificateArn=cert_arn)

        results.append(
            await runner.run_test(
                "acm", "DescribeCertificateCreated", _describe_certificate_created
            )
        )

        def _delete_certificate():
            acm_client.delete_certificate(CertificateArn=cert_arn)

        results.append(
            await runner.run_test("acm", "DeleteCertificate", _delete_certificate)
        )

    if test_cert_arn:
        try:
            acm_client.delete_certificate(CertificateArn=test_cert_arn)
        except Exception:
            pass

    domain_name2 = _make_unique_name("example2") + ".com"

    def _add_tags_to_certificate():
        cert_arn2 = ""
        try:
            resp = acm_client.request_certificate(
                DomainName=domain_name2, ValidationMethod="DNS"
            )
            cert_arn2 = resp.get("CertificateArn", "")
            acm_client.add_tags_to_certificate(
                CertificateArn=cert_arn2,
                Tags=[
                    {"Key": "Environment", "Value": "test"},
                    {"Key": "Owner", "Value": "test-user"},
                ],
            )
        finally:
            if cert_arn2:
                try:
                    acm_client.delete_certificate(CertificateArn=cert_arn2)
                except Exception:
                    pass

    results.append(
        await runner.run_test("acm", "AddTagsToCertificate", _add_tags_to_certificate)
    )

    domain_name3 = _make_unique_name("example3") + ".com"

    def _list_tags_for_certificate():
        cert_arn3 = ""
        try:
            resp = acm_client.request_certificate(
                DomainName=domain_name3, ValidationMethod="DNS"
            )
            cert_arn3 = resp.get("CertificateArn", "")
            acm_client.add_tags_to_certificate(
                CertificateArn=cert_arn3, Tags=[{"Key": "Test", "Value": "value"}]
            )
            acm_client.list_tags_for_certificate(CertificateArn=cert_arn3)
        finally:
            if cert_arn3:
                try:
                    acm_client.delete_certificate(CertificateArn=cert_arn3)
                except Exception:
                    pass

    results.append(
        await runner.run_test(
            "acm", "ListTagsForCertificate", _list_tags_for_certificate
        )
    )

    domain_name4 = _make_unique_name("example4") + ".com"

    def _remove_tags_from_certificate():
        cert_arn4 = ""
        try:
            resp = acm_client.request_certificate(
                DomainName=domain_name4, ValidationMethod="DNS"
            )
            cert_arn4 = resp.get("CertificateArn", "")
            acm_client.add_tags_to_certificate(
                CertificateArn=cert_arn4, Tags=[{"Key": "Test", "Value": "value"}]
            )
            acm_client.remove_tags_from_certificate(
                CertificateArn=cert_arn4, Tags=[{"Key": "Test"}]
            )
        finally:
            if cert_arn4:
                try:
                    acm_client.delete_certificate(CertificateArn=cert_arn4)
                except Exception:
                    pass

    results.append(
        await runner.run_test(
            "acm", "RemoveTagsFromCertificate", _remove_tags_from_certificate
        )
    )

    domain_name5 = _make_unique_name("example5") + ".com"

    def _resend_validation_email():
        cert_arn5 = ""
        try:
            resp = acm_client.request_certificate(
                DomainName=domain_name5, ValidationMethod="EMAIL"
            )
            cert_arn5 = resp.get("CertificateArn", "")
            acm_client.resend_validation_email(
                CertificateArn=cert_arn5,
                Domain=domain_name5,
                ValidationDomain=domain_name5,
            )
        finally:
            if cert_arn5:
                try:
                    acm_client.delete_certificate(CertificateArn=cert_arn5)
                except Exception:
                    pass

    results.append(
        await runner.run_test("acm", "ResendValidationEmail", _resend_validation_email)
    )

    domain_name6 = _make_unique_name("example6") + ".com"

    def _update_certificate_options():
        cert_arn6 = ""
        try:
            resp = acm_client.request_certificate(
                DomainName=domain_name6, ValidationMethod="DNS"
            )
            cert_arn6 = resp.get("CertificateArn", "")
            acm_client.update_certificate_options(
                CertificateArn=cert_arn6,
                Options={"CertificateTransparencyLoggingPreference": "ENABLED"},
            )
        finally:
            if cert_arn6:
                try:
                    acm_client.delete_certificate(CertificateArn=cert_arn6)
                except Exception:
                    pass

    results.append(
        await runner.run_test(
            "acm", "UpdateCertificateOptions", _update_certificate_options
        )
    )

    cert_pem = """-----BEGIN CERTIFICATE-----
MIIBkTCB+wIJAKHHCgVZU1JUMA0GCSqGSIb3DQEBCwUAMBExDzANBgNVBAMMBnRl
c3RjYTAeFw0yNDAxMDEwMDAwMDBaFw0yNTAxMDEwMDAwMDBaMBExDzANBgNVBAMM
BnRlc3RjYTCBnzANBgkqhkiG9w0BAQEFAAOBjQAwgYkCgYEAwK0j6f8C6hJ7u8P
-----END CERTIFICATE-----"""
    private_key_pem = """-----BEGIN RSA PRIVATE KEY-----
MIIBOQIBAAJBAKjHCBmV1SlQwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAMCr
-----END RSA PRIVATE KEY-----"""

    def _import_certificate():
        acm_client.import_certificate(
            Certificate=cert_pem.encode(), PrivateKey=private_key_pem.encode()
        )

    results.append(
        await runner.run_test("acm", "ImportCertificate", _import_certificate)
    )

    def _get_account_configuration():
        acm_client.get_account_configuration()

    results.append(
        await runner.run_test(
            "acm", "GetAccountConfiguration", _get_account_configuration
        )
    )

    def _put_account_configuration():
        acm_client.put_account_configuration(
            IdempotencyToken="test-token", ExpiryEvents={"DaysBeforeExpiry": 30}
        )

    results.append(
        await runner.run_test(
            "acm", "PutAccountConfiguration", _put_account_configuration
        )
    )

    import_cert_pem = """-----BEGIN CERTIFICATE-----
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
    import_private_key_pem = """-----BEGIN RSA PRIVATE KEY-----
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

    def _export_certificate():
        import_resp = acm_client.import_certificate(
            Certificate=import_cert_pem.encode(),
            PrivateKey=import_private_key_pem.encode(),
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

    results.append(
        await runner.run_test("acm", "ExportCertificate", _export_certificate)
    )

    def _describe_certificate_nonexistent():
        try:
            acm_client.describe_certificate(
                CertificateArn="arn:aws:acm:us-east-1:000000000000:certificate/00000000-0000-0000-0000-000000000000"
            )
            raise AssertionError("Expected ResourceNotFoundException but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "acm", "DescribeCertificate_NonExistent", _describe_certificate_nonexistent
        )
    )

    def _delete_certificate_nonexistent():
        try:
            acm_client.delete_certificate(
                CertificateArn="arn:aws:acm:us-east-1:000000000000:certificate/00000000-0000-0000-0000-000000000000"
            )
            raise AssertionError("Expected ResourceNotFoundException but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "acm", "DeleteCertificate_NonExistent", _delete_certificate_nonexistent
        )
    )

    return results
