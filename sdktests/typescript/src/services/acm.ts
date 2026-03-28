import {
  ACMClient,
  ListCertificatesCommand,
  RequestCertificateCommand,
  DescribeCertificateCommand,
  GetCertificateCommand,
  DeleteCertificateCommand,
  AddTagsToCertificateCommand,
  ListTagsForCertificateCommand,
  RemoveTagsFromCertificateCommand,
  ResendValidationEmailCommand,
  UpdateCertificateOptionsCommand,
  ImportCertificateCommand,
  GetAccountConfigurationCommand,
  PutAccountConfigurationCommand,
  ExportCertificateCommand,
} from '@aws-sdk/client-acm';
import { ResourceNotFoundException } from '@aws-sdk/client-acm';
import { TestRunner } from '../runner.js';
import { TestResult } from '../runner.js';

function makeUniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 999999)}`;
}

export async function runACMTests(
  runner: TestRunner,
  acmClient: ACMClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  // ListCertificates
  results.push(
    await runner.runTest('acm', 'ListCertificates', async () => {
      await acmClient.send(
        new ListCertificatesCommand({ MaxItems: 10 })
      );
    })
  );

  // DescribeCertificate
  const testDomain = makeUniqueName('describe-test') + '.com';
  let testCertArn = '';
  results.push(
    await runner.runTest('acm', 'DescribeCertificate', async () => {
      const resp = await acmClient.send(
        new RequestCertificateCommand({
          DomainName: testDomain,
          ValidationMethod: 'DNS',
        })
      );
      if (resp.CertificateArn) testCertArn = resp.CertificateArn;
      await acmClient.send(
        new DescribeCertificateCommand({ CertificateArn: resp.CertificateArn })
      );
    })
  );

  // GetCertificate
  if (testCertArn) {
    results.push(
      await runner.runTest('acm', 'GetCertificate', async () => {
        await acmClient.send(
          new GetCertificateCommand({ CertificateArn: testCertArn })
        );
      })
    );
  }

  // RequestCertificate
  const domainName = makeUniqueName('example') + '.com';
  let certArn = '';
  results.push(
    await runner.runTest('acm', 'RequestCertificate', async () => {
      const resp = await acmClient.send(
        new RequestCertificateCommand({
          DomainName: domainName,
          ValidationMethod: 'DNS',
          IdempotencyToken: 'test-token',
        })
      );
      if (resp.CertificateArn) certArn = resp.CertificateArn;
    })
  );

  // DescribeCertificateCreated
  if (certArn) {
    results.push(
      await runner.runTest('acm', 'DescribeCertificateCreated', async () => {
        await acmClient.send(
          new DescribeCertificateCommand({ CertificateArn: certArn })
        );
      })
    );

    // DeleteCertificate
    results.push(
      await runner.runTest('acm', 'DeleteCertificate', async () => {
        await acmClient.send(
          new DeleteCertificateCommand({ CertificateArn: certArn })
        );
      })
    );
  }

  // Cleanup testCertArn
  if (testCertArn) {
    try {
      await acmClient.send(new DeleteCertificateCommand({ CertificateArn: testCertArn }));
    } catch { /* ignore */ }
  }

  // AddTagsToCertificate
  const domainName2 = makeUniqueName('example2') + '.com';
  results.push(
    await runner.runTest('acm', 'AddTagsToCertificate', async () => {
      let certArn2 = '';
      try {
        const resp = await acmClient.send(
          new RequestCertificateCommand({
            DomainName: domainName2,
            ValidationMethod: 'DNS',
          })
        );
        if (resp.CertificateArn) certArn2 = resp.CertificateArn;
        await acmClient.send(
          new AddTagsToCertificateCommand({
            CertificateArn: certArn2,
            Tags: [
              { Key: 'Environment', Value: 'test' },
              { Key: 'Owner', Value: 'test-user' },
            ],
          })
        );
      } finally {
        if (certArn2) {
          try {
            await acmClient.send(new DeleteCertificateCommand({ CertificateArn: certArn2 }));
          } catch { /* ignore */ }
        }
      }
    })
  );

  // ListTagsForCertificate
  const domainName3 = makeUniqueName('example3') + '.com';
  results.push(
    await runner.runTest('acm', 'ListTagsForCertificate', async () => {
      let certArn3 = '';
      try {
        const resp = await acmClient.send(
          new RequestCertificateCommand({
            DomainName: domainName3,
            ValidationMethod: 'DNS',
          })
        );
        if (resp.CertificateArn) certArn3 = resp.CertificateArn;
        await acmClient.send(
          new AddTagsToCertificateCommand({
            CertificateArn: certArn3,
            Tags: [{ Key: 'Test', Value: 'value' }],
          })
        );
        await acmClient.send(
          new ListTagsForCertificateCommand({ CertificateArn: certArn3 })
        );
      } finally {
        if (certArn3) {
          try {
            await acmClient.send(new DeleteCertificateCommand({ CertificateArn: certArn3 }));
          } catch { /* ignore */ }
        }
      }
    })
  );

  // RemoveTagsFromCertificate
  const domainName4 = makeUniqueName('example4') + '.com';
  results.push(
    await runner.runTest('acm', 'RemoveTagsFromCertificate', async () => {
      let certArn4 = '';
      try {
        const resp = await acmClient.send(
          new RequestCertificateCommand({
            DomainName: domainName4,
            ValidationMethod: 'DNS',
          })
        );
        if (resp.CertificateArn) certArn4 = resp.CertificateArn;
        await acmClient.send(
          new AddTagsToCertificateCommand({
            CertificateArn: certArn4,
            Tags: [{ Key: 'Test', Value: 'value' }],
          })
        );
        await acmClient.send(
          new RemoveTagsFromCertificateCommand({
            CertificateArn: certArn4,
            Tags: [{ Key: 'Test' }],
          })
        );
      } finally {
        if (certArn4) {
          try {
            await acmClient.send(new DeleteCertificateCommand({ CertificateArn: certArn4 }));
          } catch { /* ignore */ }
        }
      }
    })
  );

  // ResendValidationEmail
  const domainName5 = makeUniqueName('example5') + '.com';
  results.push(
    await runner.runTest('acm', 'ResendValidationEmail', async () => {
      let certArn5 = '';
      try {
        const resp = await acmClient.send(
          new RequestCertificateCommand({
            DomainName: domainName5,
            ValidationMethod: 'EMAIL',
          })
        );
        if (resp.CertificateArn) certArn5 = resp.CertificateArn;
        await acmClient.send(
          new ResendValidationEmailCommand({
            CertificateArn: certArn5,
            Domain: domainName5,
            ValidationDomain: domainName5,
          })
        );
      } finally {
        if (certArn5) {
          try {
            await acmClient.send(new DeleteCertificateCommand({ CertificateArn: certArn5 }));
          } catch { /* ignore */ }
        }
      }
    })
  );

  // UpdateCertificateOptions
  const domainName6 = makeUniqueName('example6') + '.com';
  results.push(
    await runner.runTest('acm', 'UpdateCertificateOptions', async () => {
      let certArn6 = '';
      try {
        const resp = await acmClient.send(
          new RequestCertificateCommand({
            DomainName: domainName6,
            ValidationMethod: 'DNS',
          })
        );
        if (resp.CertificateArn) certArn6 = resp.CertificateArn;
        await acmClient.send(
          new UpdateCertificateOptionsCommand({
            CertificateArn: certArn6,
            Options: {
              CertificateTransparencyLoggingPreference: 'ENABLED',
            },
          })
        );
      } finally {
        if (certArn6) {
          try {
            await acmClient.send(new DeleteCertificateCommand({ CertificateArn: certArn6 }));
          } catch { /* ignore */ }
        }
      }
    })
  );

  // ImportCertificate
  const certPem = `-----BEGIN CERTIFICATE-----
MIIBkTCB+wIJAKHHCgVZU1JUMA0GCSqGSIb3DQEBCwUAMBExDzANBgNVBAMMBnRl
c3RjYTAeFw0yNDAxMDEwMDAwMDBaFw0yNTAxMDEwMDAwMDBaMBExDzANBgNVBAMM
BnRlc3RjYTCBnzANBgkqhkiG9w0BAQEFAAOBjQAwgYkCgYEAwK0j6f8C6hJ7u8P
-----END CERTIFICATE-----`;
  const privateKeyPem = `-----BEGIN RSA PRIVATE KEY-----
MIIBOQIBAAJBAKjHCBmV1SlQwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAMCr
-----END RSA PRIVATE KEY-----`;
  results.push(
    await runner.runTest('acm', 'ImportCertificate', async () => {
      await acmClient.send(
        new ImportCertificateCommand({
          Certificate: new TextEncoder().encode(certPem),
          PrivateKey: new TextEncoder().encode(privateKeyPem),
        })
      );
    })
  );

  // GetAccountConfiguration
  results.push(
    await runner.runTest('acm', 'GetAccountConfiguration', async () => {
      await acmClient.send(new GetAccountConfigurationCommand({}));
    })
  );

  // PutAccountConfiguration
  results.push(
    await runner.runTest('acm', 'PutAccountConfiguration', async () => {
      await acmClient.send(
        new PutAccountConfigurationCommand({
          IdempotencyToken: 'test-token',
          ExpiryEvents: {
            DaysBeforeExpiry: 30,
          },
        })
      );
    })
  );

  // ExportCertificate
  const importCertPem = `-----BEGIN CERTIFICATE-----
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
1DR2aPlMB0iC7yQ2UMSwdLvdWQ7ted02yYV0Hqgq/QT3wA7vfjI0SG0OUqfaJ5d
QOl0rfDrYF2ZQNqiUX827TRg9kYRJveMjGxLhFMNVxyZJkQsbGoxJPIMikWULfk2
Xwdo
-----END CERTIFICATE-----`;
  const importPrivateKeyPem = `-----BEGIN RSA PRIVATE KEY-----
MIIEpAIBAAKCAQEAuQ1frJ4yNJURfuVZ+ZoXaJnzH9Aca7cAl4kUlZauacQe9GeB
iK9MH/gZahS5Nk7uYB3SEFf2hRFy5O0FOhk89rztdB/iWZn346+RqRHAxBEl1LGR
X0HTCaaf/uxl8uj6qraDJrOmrCaBAU3zBQ+x7xJO0GmYT4y2rsnDdJwnElVIcNW6
EcF/e7mN5F8qItLuNvLeZcgICEifF1Jxhj6/0LnOB2ywsrvs974lIDfvOs8wbkQJ
ZIOZX7TOkwtUNo9FaBua5a8sQ03SXxas6nXXBHE7yl/BlJZfneAO8KT1w067ohWp
uPjCGfJN6LgXg347nE5IgyFMgksV2rXM9SdkowIDAQABAoIBAEseUi2kxBWTQ5hi
6szHT+ROxiIuXTMehPd+lmQI2EEn8zbcQ3lkS38Yu9xTkEGq9dn/kPPAeVpYFG84
hewsLZWtaKjAfqZHuZhr/zGF+t28ZkJ6WFw2QMBEquMVPGdISuT8lK2jtK9iK/EH
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
-----END RSA PRIVATE KEY-----`;
  results.push(
    await runner.runTest('acm', 'ExportCertificate', async () => {
      const importResp = await acmClient.send(
        new ImportCertificateCommand({
          Certificate: new TextEncoder().encode(importCertPem),
          PrivateKey: new TextEncoder().encode(importPrivateKeyPem),
        })
      );
      if (importResp.CertificateArn) {
        try {
          await acmClient.send(
            new ExportCertificateCommand({
              CertificateArn: importResp.CertificateArn,
              Passphrase: new TextEncoder().encode('test-passphrase'),
            })
          );
        } finally {
          try {
            await acmClient.send(
              new DeleteCertificateCommand({ CertificateArn: importResp.CertificateArn })
            );
          } catch { /* ignore */ }
        }
      }
    })
  );

  // Error cases
  results.push(
    await runner.runTest('acm', 'DescribeCertificate_NonExistent', async () => {
      try {
        await acmClient.send(
          new DescribeCertificateCommand({
            CertificateArn: 'arn:aws:acm:us-east-1:000000000000:certificate/00000000-0000-0000-0000-000000000000',
          })
        );
        throw new Error('Expected ResourceNotFoundException but got none');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  results.push(
    await runner.runTest('acm', 'DeleteCertificate_NonExistent', async () => {
      try {
        await acmClient.send(
          new DeleteCertificateCommand({
            CertificateArn: 'arn:aws:acm:us-east-1:000000000000:certificate/00000000-0000-0000-0000-000000000000',
          })
        );
        throw new Error('Expected ResourceNotFoundException but got none');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  return results;
}