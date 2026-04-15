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
import { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName } from '../../helpers.js';

export async function runSecretLifecycleTests(
  client: SecretsManagerClient,
  runner: TestRunner,
  results: TestResult[],
): Promise<void> {
  const s = 'secretsmanager';
  const secretName = makeUniqueName('TestSecret');
  const secretValue = 'my-secret-value';

  results.push(await runner.runTest(s, 'CreateSecret', async () => {
    const resp = await client.send(new CreateSecretCommand({
      Name: secretName,
      SecretString: secretValue,
    }));
    if (!resp.ARN) {
      throw new Error('secret ARN to be defined');
    }
  }));

  results.push(await runner.runTest(s, 'DescribeSecret', async () => {
    const resp = await client.send(new DescribeSecretCommand({
      SecretId: secretName,
    }));
    if (!resp.Name) {
      throw new Error('secret name to be defined');
    }
  }));

  results.push(await runner.runTest(s, 'GetSecretValue', async () => {
    const resp = await client.send(new GetSecretValueCommand({
      SecretId: secretName,
    }));
    if (!resp.SecretString && !resp.SecretBinary) {
      throw new Error('secret value to be defined');
    }
  }));

  results.push(await runner.runTest(s, 'ListSecrets', async () => {
    const resp = await client.send(new ListSecretsCommand({}));
    if (!resp.SecretList) {
      throw new Error('secrets list to be defined');
    }
  }));

  results.push(await runner.runTest(s, 'UpdateSecret', async () => {
    const resp = await client.send(new UpdateSecretCommand({
      SecretId: secretName,
      SecretString: 'updated-secret-value',
    }));
    if (!resp.ARN) {
      throw new Error('secret ARN to be defined');
    }
  }));

  results.push(await runner.runTest(s, 'TagResource', async () => {
    const resp = await client.send(new TagResourceCommand({
      SecretId: secretName,
      Tags: [
        { Key: 'Environment', Value: 'test' },
        { Key: 'Project', Value: 'sdk-tests' },
      ],
    }));
    if (!resp) {
      throw new Error('response to be defined');
    }
  }));

  results.push(await runner.runTest(s, 'ListSecretVersionIds', async () => {
    const resp = await client.send(new ListSecretVersionIdsCommand({
      SecretId: secretName,
    }));
    if (!resp.Versions) {
      throw new Error('versions list to be defined');
    }
  }));

  results.push(await runner.runTest(s, 'DeleteSecret', async () => {
    const resp = await client.send(new DeleteSecretCommand({
      SecretId: secretName,
      ForceDeleteWithoutRecovery: true,
    }));
    if (!resp) {
      throw new Error('response to be defined');
    }
  }));
}
