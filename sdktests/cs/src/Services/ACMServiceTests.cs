using Amazon;
using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class ACMServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonCertificateManagerClient acmClient,
        string region)
    {
        var results = new List<TestResult>();

        results.Add(await runner.RunTestAsync("acm", "ListCertificates", async () =>
        {
            var resp = await acmClient.ListCertificatesAsync(new ListCertificatesRequest
            {
                MaxItems = 10
            });
            if (resp.CertificateSummaryList == null)
                throw new Exception("CertificateSummaryList is null");
        }));

        results.Add(await runner.RunTestAsync("acm", "DescribeCertificate", async () =>
        {
            var listResp = await acmClient.ListCertificatesAsync(new ListCertificatesRequest());
            if (listResp.CertificateSummaryList != null && listResp.CertificateSummaryList.Count > 0)
            {
                var arn = listResp.CertificateSummaryList[0].CertificateArn;
                var resp = await acmClient.DescribeCertificateAsync(new DescribeCertificateRequest
                {
                    CertificateArn = arn
                });
                if (resp.Certificate == null)
                    throw new Exception("Certificate is null");
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "RequestCertificate", async () =>
        {
            var domainName = TestRunner.MakeUniqueName("testcert") + ".example.com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domainName,
                ValidationMethod = Amazon.CertificateManager.ValidationMethod.DNS
            });
            if (string.IsNullOrEmpty(resp.CertificateArn))
                throw new Exception("CertificateArn is null");
            await acmClient.DeleteCertificateAsync(new Amazon.CertificateManager.Model.DeleteCertificateRequest
            {
                CertificateArn = resp.CertificateArn
            });
        }));

        results.Add(await runner.RunTestAsync("acm", "DescribeCertificate_ResourceNotFound", async () =>
        {
            try
            {
                await acmClient.DescribeCertificateAsync(new DescribeCertificateRequest
                {
                    CertificateArn = "arn:aws:acm:us-east-1:123456789012:certificate/00000000-0000-0000-0000-000000000000"
                });
                throw new Exception("Expected error but got none");
            }
            catch (ResourceNotFoundException)
            {
            }
        }));

        var testDomain = TestRunner.MakeUniqueName("describe-test") + ".com";
        string testCertArn = "";
        results.Add(await runner.RunTestAsync("acm", "DescribeCertificate_WithCreated", async () =>
        {
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = testDomain,
                ValidationMethod = ValidationMethod.DNS
            });
            testCertArn = resp.CertificateArn;
            if (string.IsNullOrEmpty(testCertArn))
                throw new Exception("certificate ARN is empty");
            var descResp = await acmClient.DescribeCertificateAsync(new DescribeCertificateRequest
            {
                CertificateArn = testCertArn
            });
            if (descResp.Certificate == null)
                throw new Exception("certificate is nil");
        }));

        results.Add(await runner.RunTestAsync("acm", "GetCertificate", async () =>
        {
            if (string.IsNullOrEmpty(testCertArn))
                throw new Exception("no certificate ARN available");
            var resp = await acmClient.GetCertificateAsync(new GetCertificateRequest
            {
                CertificateArn = testCertArn
            });
        }));

        string certArn2 = "";
        results.Add(await runner.RunTestAsync("acm", "RequestCertificate_FullLifecycle", async () =>
        {
            var domain = TestRunner.MakeUniqueName("example") + ".com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.DNS,
                IdempotencyToken = "test-token"
            });
            if (string.IsNullOrEmpty(resp.CertificateArn))
                throw new Exception("certificate ARN is nil");
            certArn2 = resp.CertificateArn;
            var descResp = await acmClient.DescribeCertificateAsync(new DescribeCertificateRequest { CertificateArn = certArn2 });
            if (descResp.Certificate == null)
                throw new Exception("certificate is nil");
            await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = certArn2 });
            certArn2 = "";
        }));

        try
        {
            if (!string.IsNullOrEmpty(testCertArn))
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = testCertArn }); });
            if (!string.IsNullOrEmpty(certArn2))
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = certArn2 }); });
        }
        catch { }

        results.Add(await runner.RunTestAsync("acm", "AddTagsToCertificate", async () =>
        {
            var domain = TestRunner.MakeUniqueName("example2") + ".com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.DNS
            });
            try
            {
                await acmClient.AddTagsToCertificateAsync(new AddTagsToCertificateRequest
                {
                    CertificateArn = resp.CertificateArn,
                    Tags = new List<Tag>
                    {
                        new Tag { Key = "Environment", Value = "test" },
                        new Tag { Key = "Owner", Value = "test-user" }
                    }
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "ListTagsForCertificate", async () =>
        {
            var domain = TestRunner.MakeUniqueName("example3") + ".com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.DNS
            });
            try
            {
                await acmClient.AddTagsToCertificateAsync(new AddTagsToCertificateRequest
                {
                    CertificateArn = resp.CertificateArn,
                    Tags = new List<Tag> { new Tag { Key = "Test", Value = "value" } }
                });
                var tagResp = await acmClient.ListTagsForCertificateAsync(new ListTagsForCertificateRequest
                {
                    CertificateArn = resp.CertificateArn
                });
                if (tagResp.Tags == null)
                    throw new Exception("tags list is nil");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "RemoveTagsFromCertificate", async () =>
        {
            var domain = TestRunner.MakeUniqueName("example4") + ".com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.DNS
            });
            try
            {
                await acmClient.AddTagsToCertificateAsync(new AddTagsToCertificateRequest
                {
                    CertificateArn = resp.CertificateArn,
                    Tags = new List<Tag> { new Tag { Key = "Test", Value = "value" } }
                });
                await acmClient.RemoveTagsFromCertificateAsync(new RemoveTagsFromCertificateRequest
                {
                    CertificateArn = resp.CertificateArn,
                    Tags = new List<Tag> { new Tag { Key = "Test" } }
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "ResendValidationEmail", async () =>
        {
            var domain = TestRunner.MakeUniqueName("example5") + ".com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.EMAIL
            });
            try
            {
                await acmClient.ResendValidationEmailAsync(new ResendValidationEmailRequest
                {
                    CertificateArn = resp.CertificateArn,
                    Domain = domain,
                    ValidationDomain = domain
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "UpdateCertificateOptions", async () =>
        {
            var domain = TestRunner.MakeUniqueName("example6") + ".com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.DNS
            });
            try
            {
                await acmClient.UpdateCertificateOptionsAsync(new UpdateCertificateOptionsRequest
                {
                    CertificateArn = resp.CertificateArn,
                    Options = new CertificateOptions
                    {
                        CertificateTransparencyLoggingPreference = CertificateTransparencyLoggingPreference.ENABLED
                    }
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "ImportCertificate", async () =>
        {
            var certPem = @"-----BEGIN CERTIFICATE-----
MIIBkTCB+wIJAKHHCgVZU1JUMA0GCSqGSIb3DQEBCwUAMBExDzANBgNVBAMMBnRl
c3RjYTAeFw0yNDAxMDEwMDAwMDBaFw0yNTAxMDEwMDAwMDBaMBExDzANBgNVBAMM
BnRlc3RjYTCBnzANBgkqhkiG9w0BAQEFAAOBjQAwgYkCgYEAwK0j6f8C6hJ7u8P
-----END CERTIFICATE-----";
            var privateKeyPem = @"-----BEGIN RSA PRIVATE KEY-----
MIIBOQIBAAJBAKjHCBmV1SlQwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAMCr
-----END RSA PRIVATE KEY-----";
            var resp = await acmClient.ImportCertificateAsync(new ImportCertificateRequest
            {
                Certificate = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(certPem)),
                PrivateKey = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(privateKeyPem))
            });
            if (string.IsNullOrEmpty(resp.CertificateArn))
                throw new Exception("certificate ARN is nil");
            await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp.CertificateArn }); });
        }));

        results.Add(await runner.RunTestAsync("acm", "GetAccountConfiguration", async () =>
        {
            var resp = await acmClient.GetAccountConfigurationAsync(new GetAccountConfigurationRequest());
        }));

        results.Add(await runner.RunTestAsync("acm", "PutAccountConfiguration", async () =>
        {
            await acmClient.PutAccountConfigurationAsync(new PutAccountConfigurationRequest
            {
                IdempotencyToken = "test-token",
                ExpiryEvents = new ExpiryEventsConfiguration
                {
                    DaysBeforeExpiry = 30
                }
            });
        }));

        results.Add(await runner.RunTestAsync("acm", "ExportCertificate", async () =>
        {
            var certPem = @"-----BEGIN CERTIFICATE-----
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
BliGOu4L4o2bCjiPO5V2cv36id6e7FRfJyAmRe/S3M06jJh9HB3/uUTABITkGgee
Sa35wq1cRp86PGHhCGkEg79J8WRQmNrelttmCz/Fs4N5leuwnOlTlgCoEaLt+QSY
1DR2aPlMB0iC7yQ2UMSwdLvdWQ7ted02yYV0Hqgq/QT3wA7vfjI0SG0OUqfaJ5d2
QOl0rfDrYF2ZQNqiUX827TRg9kYRJveMjGxLhFMNVxyZJkQsbGoxJPIMikWULfk2
Xwdo
-----END CERTIFICATE-----";
            var privateKeyPem = @"-----BEGIN RSA PRIVATE KEY-----
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
-----END RSA PRIVATE KEY-----";
            var importResp = await acmClient.ImportCertificateAsync(new ImportCertificateRequest
            {
                Certificate = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(certPem)),
                PrivateKey = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(privateKeyPem))
            });
            try
            {
                var exportResp = await acmClient.ExportCertificateAsync(new ExportCertificateRequest
                {
                    CertificateArn = importResp.CertificateArn,
                    Passphrase = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test-passphrase"))
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = importResp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "GetCertificate_NonExistent", async () =>
        {
            try
            {
                await acmClient.GetCertificateAsync(new GetCertificateRequest
                {
                    CertificateArn = "arn:aws:acm:us-east-1:123456789012:certificate/nonexistent-cert"
                });
                throw new Exception("Expected error but got none");
            }
            catch (ResourceNotFoundException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "GetCertificate_PendingValidation", async () =>
        {
            var domain = TestRunner.MakeUniqueName("pending-get") + ".example.com";
            var reqResp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.DNS
            });
            try
            {
                var descResp = await acmClient.DescribeCertificateAsync(new DescribeCertificateRequest
                {
                    CertificateArn = reqResp.CertificateArn
                });
                if (descResp.Certificate == null)
                    throw new Exception("Certificate is null");
                if (descResp.Certificate.Status != "ISSUED")
                    throw new Exception($"Expected ISSUED status, got {descResp.Certificate.Status}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = reqResp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "DescribeCertificate_NonExistent", async () =>
        {
            try
            {
                await acmClient.DescribeCertificateAsync(new DescribeCertificateRequest
                {
                    CertificateArn = "arn:aws:acm:us-east-1:000000000000:certificate/non-existent"
                });
                throw new Exception("Expected error but got none");
            }
            catch (ResourceNotFoundException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "RequestCertificate_DuplicateDomain", async () =>
        {
            var domain = TestRunner.MakeUniqueName("dup-domain") + ".example.com";
            var resp1 = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.DNS
            });
            var resp2 = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.DNS
            });
            try
            {
                if (string.IsNullOrEmpty(resp1.CertificateArn) || string.IsNullOrEmpty(resp2.CertificateArn))
                    throw new Exception("CertificateArn should not be null");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp1.CertificateArn }); });
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp2.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "RequestCertificate_WithTags", async () =>
        {
            var domain = TestRunner.MakeUniqueName("tagged") + ".example.com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.DNS,
                Tags = new List<Tag>
                {
                    new Tag { Key = "Environment", Value = "production" },
                    new Tag { Key = "Team", Value = "backend" }
                }
            });
            try
            {
                var tagResp = await acmClient.ListTagsForCertificateAsync(new ListTagsForCertificateRequest
                {
                    CertificateArn = resp.CertificateArn
                });
                if (tagResp.Tags == null || tagResp.Tags.Count < 2)
                    throw new Exception("Expected at least 2 tags on certificate");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "RequestCertificate_WithSubjectAlternativeNames", async () =>
        {
            var domain = TestRunner.MakeUniqueName("san") + ".example.com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                SubjectAlternativeNames = new List<string> { "www." + domain, "api." + domain },
                ValidationMethod = ValidationMethod.DNS
            });
            try
            {
                var descResp = await acmClient.DescribeCertificateAsync(new DescribeCertificateRequest
                {
                    CertificateArn = resp.CertificateArn
                });
                if (descResp.Certificate == null)
                    throw new Exception("Certificate is null");
                if (descResp.Certificate.SubjectAlternativeNames == null || descResp.Certificate.SubjectAlternativeNames.Count < 2)
                    throw new Exception("Expected at least 2 subject alternative names");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "UntagResource", async () =>
        {
            var domain = TestRunner.MakeUniqueName("untag") + ".example.com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.DNS
            });
            try
            {
                await acmClient.AddTagsToCertificateAsync(new AddTagsToCertificateRequest
                {
                    CertificateArn = resp.CertificateArn,
                    Tags = new List<Tag>
                    {
                        new Tag { Key = "ToRemove", Value = "value" },
                        new Tag { Key = "ToKeep", Value = "value" }
                    }
                });
                await acmClient.RemoveTagsFromCertificateAsync(new RemoveTagsFromCertificateRequest
                {
                    CertificateArn = resp.CertificateArn,
                    Tags = new List<Tag> { new Tag { Key = "ToRemove" } }
                });
                var tagResp = await acmClient.ListTagsForCertificateAsync(new ListTagsForCertificateRequest
                {
                    CertificateArn = resp.CertificateArn
                });
                if (tagResp.Tags != null)
                {
                    foreach (var tag in tagResp.Tags)
                    {
                        if (tag.Key == "ToRemove")
                            throw new Exception("Tag 'ToRemove' should have been removed");
                    }
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "ListTagsForCertificate_NonExistent", async () =>
        {
            try
            {
                await acmClient.ListTagsForCertificateAsync(new ListTagsForCertificateRequest
                {
                    CertificateArn = "arn:aws:acm:us-east-1:123456789012:certificate/nonexistent-tags"
                });
                throw new Exception("Expected error but got none");
            }
            catch (ResourceNotFoundException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "RenewCertificate", async () =>
        {
            var domain = TestRunner.MakeUniqueName("renew") + ".example.com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.DNS
            });
            try
            {
                await acmClient.RenewCertificateAsync(new RenewCertificateRequest
                {
                    CertificateArn = resp.CertificateArn
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "DeleteCertificate", async () =>
        {
            var domain = TestRunner.MakeUniqueName("delete-test") + ".example.com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.DNS
            });
            var arn = resp.CertificateArn;
            await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest
            {
                CertificateArn = arn
            });
            try
            {
                await acmClient.DescribeCertificateAsync(new DescribeCertificateRequest
                {
                    CertificateArn = arn
                });
                throw new Exception("Expected error when describing deleted certificate");
            }
            catch (ResourceNotFoundException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "DeleteCertificate_NonExistent", async () =>
        {
            try
            {
                await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest
                {
                    CertificateArn = "arn:aws:acm:us-east-1:000000000000:certificate/non-existent"
                });
                throw new Exception("Expected error but got none");
            }
            catch (ResourceNotFoundException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "DescribeCertificate_WithValidation", async () =>
        {
            var domain = TestRunner.MakeUniqueName("validation") + ".example.com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.EMAIL
            });
            try
            {
                var descResp = await acmClient.DescribeCertificateAsync(new DescribeCertificateRequest
                {
                    CertificateArn = resp.CertificateArn
                });
                if (descResp.Certificate == null)
                    throw new Exception("Certificate is null");
                if (descResp.Certificate.DomainValidationOptions == null)
                    throw new Exception("DomainValidationOptions is null");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "DescribeCertificate_StatusTransitions", async () =>
        {
            var domain = TestRunner.MakeUniqueName("status-trans") + ".example.com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.DNS
            });
            try
            {
                var descResp = await acmClient.DescribeCertificateAsync(new DescribeCertificateRequest
                {
                    CertificateArn = resp.CertificateArn
                });
                if (descResp.Certificate == null)
                    throw new Exception("Certificate is null");
                if (string.IsNullOrEmpty(descResp.Certificate.Status))
                    throw new Exception("Status is null or empty");
                if (descResp.Certificate.CreatedAt == null)
                    throw new Exception("CreatedAt is null");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "ListCertificates_FilterByStatus", async () =>
        {
            var domain = TestRunner.MakeUniqueName("filter-status") + ".example.com";
            var reqResp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.DNS
            });
            try
            {
                var resp = await acmClient.ListCertificatesAsync(new ListCertificatesRequest
                {
                    CertificateStatuses = new List<string> { "PENDING_VALIDATION" }
                });
                if (resp.CertificateSummaryList != null)
                {
                    foreach (var cert in resp.CertificateSummaryList)
                    {
                        if (cert.Status != "PENDING_VALIDATION")
                            throw new Exception("Expected PENDING_VALIDATION status");
                    }
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = reqResp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "ListCertificates_Pagination", async () =>
        {
            var resp = await acmClient.ListCertificatesAsync(new ListCertificatesRequest
            {
                MaxItems = 1
            });
            if (resp.CertificateSummaryList == null)
                throw new Exception("CertificateSummaryList is null");
            if (resp.CertificateSummaryList.Count > 1)
                throw new Exception("Expected at most 1 certificate with MaxItems=1");
        }));

        results.Add(await runner.RunTestAsync("acm", "RequestCertificate_WithValidationMethod", async () =>
        {
            var domain = TestRunner.MakeUniqueName("email-val") + ".example.com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.EMAIL
            });
            try
            {
                if (string.IsNullOrEmpty(resp.CertificateArn))
                    throw new Exception("CertificateArn is null");
                var descResp = await acmClient.DescribeCertificateAsync(new DescribeCertificateRequest
                {
                    CertificateArn = resp.CertificateArn
                });
                if (descResp.Certificate == null)
                    throw new Exception("Certificate is null");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "RequestCertificate_WithKeyAlgorithm", async () =>
        {
            var domain = TestRunner.MakeUniqueName("keyalgo") + ".example.com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.DNS,
                KeyAlgorithm = KeyAlgorithm.RSA_2048
            });
            try
            {
                if (string.IsNullOrEmpty(resp.CertificateArn))
                    throw new Exception("CertificateArn is null");
                var descResp = await acmClient.DescribeCertificateAsync(new DescribeCertificateRequest
                {
                    CertificateArn = resp.CertificateArn
                });
                if (descResp.Certificate == null)
                    throw new Exception("Certificate is null");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "TagResource_DeleteCertificate", async () =>
        {
            var domain = TestRunner.MakeUniqueName("tag-delete") + ".example.com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.DNS
            });
            var arn = resp.CertificateArn;
            try
            {
                await acmClient.AddTagsToCertificateAsync(new AddTagsToCertificateRequest
                {
                    CertificateArn = arn,
                    Tags = new List<Tag> { new Tag { Key = "ToDelete", Value = "value" } }
                });
            }
            finally
            {
                await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest
                {
                    CertificateArn = arn
                });
                try
                {
                    await acmClient.DescribeCertificateAsync(new DescribeCertificateRequest
                    {
                        CertificateArn = arn
                    });
                    throw new Exception("Expected error when describing deleted certificate");
                }
                catch (ResourceNotFoundException)
                {
                }
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "Certificate_NotActiveAfterImport", async () =>
        {
            var certPem = @"-----BEGIN CERTIFICATE-----
MIIBkTCB+wIJAKHHCgVZU1JUMA0GCSqGSIb3DQEBCwUAMBExDzANBgNVBAMMBnRl
c3RjYTAeFw0yNDAxMDEwMDAwMDBaFw0yNTAxMDEwMDAwMDBaMBExDzANBgNVBAMM
BnRlc3RjYTCBnzANBgkqhkiG9w0BAQEFAAOBjQAwgYkCgYEAwK0j6f8C6hJ7u8P
-----END CERTIFICATE-----";
            var privateKeyPem = @"-----BEGIN RSA PRIVATE KEY-----
MIIBOQIBAAJBAKjHCBmV1SlQwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAMCr
-----END RSA PRIVATE KEY-----";
            var resp = await acmClient.ImportCertificateAsync(new ImportCertificateRequest
            {
                Certificate = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(certPem)),
                PrivateKey = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(privateKeyPem))
            });
            try
            {
                var descResp = await acmClient.DescribeCertificateAsync(new DescribeCertificateRequest
                {
                    CertificateArn = resp.CertificateArn
                });
                if (descResp.Certificate == null)
                    throw new Exception("Certificate is null");
                if (descResp.Certificate.Type != "IMPORTED")
                    throw new Exception("Expected IMPORTED certificate type");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "RequestCertificate_Exportable", async () =>
        {
            var domain = TestRunner.MakeUniqueName("exportable") + ".example.com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain,
                ValidationMethod = ValidationMethod.DNS
            });
            try
            {
                if (string.IsNullOrEmpty(resp.CertificateArn))
                    throw new Exception("CertificateArn is null");
                var descResp = await acmClient.DescribeCertificateAsync(new DescribeCertificateRequest
                {
                    CertificateArn = resp.CertificateArn
                });
                if (descResp.Certificate == null)
                    throw new Exception("Certificate is null");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp.CertificateArn }); });
            }
        }));

        results.Add(await runner.RunTestAsync("acm", "RequestCertificate_MinimalOptions", async () =>
        {
            var domain = TestRunner.MakeUniqueName("minimal") + ".example.com";
            var resp = await acmClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domain
            });
            try
            {
                if (string.IsNullOrEmpty(resp.CertificateArn))
                    throw new Exception("CertificateArn is null");
                var descResp = await acmClient.DescribeCertificateAsync(new DescribeCertificateRequest
                {
                    CertificateArn = resp.CertificateArn
                });
                if (descResp.Certificate == null)
                    throw new Exception("Certificate is null");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await acmClient.DeleteCertificateAsync(new DeleteCertificateRequest { CertificateArn = resp.CertificateArn }); });
            }
        }));

        return results;
    }
}
