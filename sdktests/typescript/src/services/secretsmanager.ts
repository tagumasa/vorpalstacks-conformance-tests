import {
  SecretsManagerClient,
  CreateSecretCommand,
  DescribeSecretCommand,
  GetSecretValueCommand,
  ListSecretsCommand,
  UpdateSecretCommand,
  TagResourceCommand,
  ListSecretVersionIdsCommand,
  DeleteSecretCommand,
} from '@aws-sdk/client-secrets-manager';
import { ResourceNotFoundException, ResourceExistsException } from '@aws-sdk/client-secrets-manager';
import { TestRunner } from '../runner.js';
import { TestResult } from '../runner.js';

function makeUniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 999999)}`;
}

export async function runSecretsManagerTests(
  runner: TestRunner,
  secretsClient: SecretsManagerClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const secretName = makeUniqueName('TestSecret');
  const secretValue = 'my-secret-value';

  try {
    // CreateSecret
    results.push(
      await runner.runTest('secretsmanager', 'CreateSecret', async () => {
        await secretsClient.send(
          new CreateSecretCommand({
            Name: secretName,
            SecretString: secretValue,
          })
        );
      })
    );

    // DescribeSecret
    results.push(
      await runner.runTest('secretsmanager', 'DescribeSecret', async () => {
        const resp = await secretsClient.send(
          new DescribeSecretCommand({ SecretId: secretName })
        );
        if (!resp.Name) throw new Error('secret name is null');
      })
    );

    // GetSecretValue
    results.push(
      await runner.runTest('secretsmanager', 'GetSecretValue', async () => {
        const resp = await secretsClient.send(
          new GetSecretValueCommand({ SecretId: secretName })
        );
        if (!resp.SecretString && !resp.SecretBinary) {
          throw new Error('secret value is null');
        }
      })
    );

    // ListSecrets
    results.push(
      await runner.runTest('secretsmanager', 'ListSecrets', async () => {
        const resp = await secretsClient.send(new ListSecretsCommand({}));
        if (!resp.SecretList) throw new Error('SecretList is null');
      })
    );

    // UpdateSecret
    results.push(
      await runner.runTest('secretsmanager', 'UpdateSecret', async () => {
        await secretsClient.send(
          new UpdateSecretCommand({
            SecretId: secretName,
            SecretString: 'updated-secret-value',
          })
        );
      })
    );

    // TagResource
    results.push(
      await runner.runTest('secretsmanager', 'TagResource', async () => {
        await secretsClient.send(
          new TagResourceCommand({
            SecretId: secretName,
            Tags: [
              { Key: 'Environment', Value: 'test' },
              { Key: 'Project', Value: 'sdk-tests' },
            ],
          })
        );
      })
    );

    // ListSecretVersionIds
    results.push(
      await runner.runTest('secretsmanager', 'ListSecretVersionIds', async () => {
        const resp = await secretsClient.send(
          new ListSecretVersionIdsCommand({ SecretId: secretName })
        );
        if (!resp.Versions) throw new Error('Versions is null');
      })
    );

    // DeleteSecret
    results.push(
      await runner.runTest('secretsmanager', 'DeleteSecret', async () => {
        await secretsClient.send(
          new DeleteSecretCommand({
            SecretId: secretName,
            ForceDeleteWithoutRecovery: true,
          })
        );
      })
    );

  } finally {
    try {
      await secretsClient.send(
        new DeleteSecretCommand({
          SecretId: secretName,
          ForceDeleteWithoutRecovery: true,
        })
      );
    } catch { /* ignore */ }
  }

  // Error cases
  results.push(
    await runner.runTest('secretsmanager', 'GetSecretValue_NonExistent', async () => {
      try {
        await secretsClient.send(
          new GetSecretValueCommand({
            SecretId: 'arn:aws:secretsmanager:us-east-1:000000000000:secret:nonexistent-secret-xyz',
          })
        );
        throw new Error('Expected error but got none');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  results.push(
    await runner.runTest('secretsmanager', 'DescribeSecret_NonExistent', async () => {
      try {
        await secretsClient.send(
          new DescribeSecretCommand({
            SecretId: 'arn:aws:secretsmanager:us-east-1:000000000000:secret:nonexistent-secret-xyz',
          })
        );
        throw new Error('Expected error but got none');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  results.push(
    await runner.runTest('secretsmanager', 'DeleteSecret_NonExistent', async () => {
      try {
        await secretsClient.send(
          new DeleteSecretCommand({
            SecretId: 'arn:aws:secretsmanager:us-east-1:000000000000:secret:nonexistent-xyz',
            ForceDeleteWithoutRecovery: true,
          })
        );
        throw new Error('Expected error but got none');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  const dupName = makeUniqueName('DupSecret');
  results.push(
    await runner.runTest('secretsmanager', 'CreateSecret_Duplicate', async () => {
      try {
        await secretsClient.send(
          new CreateSecretCommand({
            Name: dupName,
            SecretString: 'initial-value',
          })
        );
      } catch {
        // ignore first create error
      }

      try {
        await secretsClient.send(
          new CreateSecretCommand({
            Name: dupName,
            SecretString: 'duplicate-value',
          })
        );
        throw new Error('Expected error but got none');
      } catch (err) {
        if (!(err instanceof ResourceExistsException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceExistsException, got ${name}`);
        }
      } finally {
        try {
          await secretsClient.send(
            new DeleteSecretCommand({
              SecretId: dupName,
              ForceDeleteWithoutRecovery: true,
            })
          );
        } catch { /* ignore */ }
      }
    })
  );

  // GetSecretValue_ContentVerify
  const verifyName = makeUniqueName('VerifySecret');
  results.push(
    await runner.runTest('secretsmanager', 'GetSecretValue_ContentVerify', async () => {
      const verifyValue = 'my-verified-secret-123';
      try {
        await secretsClient.send(
          new CreateSecretCommand({
            Name: verifyName,
            SecretString: verifyValue,
          })
        );

        const resp = await secretsClient.send(
          new GetSecretValueCommand({ SecretId: verifyName })
        );
        if (!resp.SecretString || resp.SecretString !== verifyValue) {
          throw new Error(`secret value mismatch: got ${resp.SecretString}, want ${verifyValue}`);
        }
      } finally {
        try {
          await secretsClient.send(
            new DeleteSecretCommand({
              SecretId: verifyName,
              ForceDeleteWithoutRecovery: true,
            })
          );
        } catch { /* ignore */ }
      }
    })
  );

  // UpdateSecret_ContentVerify
  const updateVerifyName = makeUniqueName('UpdateVerify');
  results.push(
    await runner.runTest('secretsmanager', 'UpdateSecret_ContentVerify', async () => {
      const originalValue = 'original-value';
      const updatedValue = 'updated-secret-value-456';
      try {
        await secretsClient.send(
          new CreateSecretCommand({
            Name: updateVerifyName,
            SecretString: originalValue,
          })
        );

        await secretsClient.send(
          new UpdateSecretCommand({
            SecretId: updateVerifyName,
            SecretString: updatedValue,
          })
        );

        const resp = await secretsClient.send(
          new GetSecretValueCommand({ SecretId: updateVerifyName })
        );
        if (!resp.SecretString || resp.SecretString !== updatedValue) {
          throw new Error(`secret value not updated: got ${resp.SecretString}, want ${updatedValue}`);
        }
      } finally {
        try {
          await secretsClient.send(
            new DeleteSecretCommand({
              SecretId: updateVerifyName,
              ForceDeleteWithoutRecovery: true,
            })
          );
        } catch { /* ignore */ }
      }
    })
  );

  // ListSecrets_ContainsCreated
  const listVerifyName = makeUniqueName('ListVerify');
  results.push(
    await runner.runTest('secretsmanager', 'ListSecrets_ContainsCreated', async () => {
      try {
        await secretsClient.send(
          new CreateSecretCommand({
            Name: listVerifyName,
            SecretString: 'list-test',
          })
        );

        const resp = await secretsClient.send(new ListSecretsCommand({}));
        let found = false;
        for (const s of resp.SecretList || []) {
          if (s.Name === listVerifyName) {
            found = true;
            break;
          }
        }
        if (!found) throw new Error('created secret not found in ListSecrets');
      } finally {
        try {
          await secretsClient.send(
            new DeleteSecretCommand({
              SecretId: listVerifyName,
              ForceDeleteWithoutRecovery: true,
            })
          );
        } catch { /* ignore */ }
      }
    })
  );

  // MultiByteSecret
  results.push(
    await runner.runTest('secretsmanager', 'MultiByteSecret', async () => {
      const pairs: [string, string][] = [
        ['ja', '日本語テストシークレット'],
        ['zh', '简体中文测试机密'],
        ['tw', '繁體中文測試機密'],
      ];
      for (const [label, value] of pairs) {
        const name = makeUniqueName(`MultiByte-${label}`);
        try {
          await secretsClient.send(
            new CreateSecretCommand({ Name: name, SecretString: value })
          );
          const resp = await secretsClient.send(
            new GetSecretValueCommand({ SecretId: name })
          );
          if (resp.SecretString !== value) {
            throw new Error(`Mismatch for ${label}: expected ${value}, got ${resp.SecretString}`);
          }
        } finally {
          try {
            await secretsClient.send(
              new DeleteSecretCommand({ SecretId: name, ForceDeleteWithoutRecovery: true })
            );
          } catch { /* ignore */ }
        }
      }
    })
  );

  return results;
}