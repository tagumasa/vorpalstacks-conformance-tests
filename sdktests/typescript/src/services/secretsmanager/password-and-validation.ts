import {
  SecretsManagerClient,
  GetRandomPasswordCommand,
  ValidateResourcePolicyCommand,
} from '@aws-sdk/client-secrets-manager';
import type { TestRunner, TestResult } from '../../runner.js';

export async function runPasswordAndPolicyValidationTests(
  client: SecretsManagerClient,
  runner: TestRunner,
  results: TestResult[],
): Promise<void> {
  const s = 'secretsmanager';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(s, name, fn));

  await r('GetRandomPassword_Basic', async () => {
    const resp = await client.send(new GetRandomPasswordCommand({}));
    if (!resp.RandomPassword || resp.RandomPassword.length !== 32) {
      throw new Error(`expected default password length 32, got ${resp.RandomPassword?.length}`);
    }
  });

  await r('GetRandomPassword_CustomLength', async () => {
    const resp = await client.send(new GetRandomPasswordCommand({ PasswordLength: 16 }));
    if (!resp.RandomPassword || resp.RandomPassword.length !== 16) {
      throw new Error(`expected password length 16, got ${resp.RandomPassword?.length}`);
    }
  });

  await r('GetRandomPassword_ExcludeCharacters', async () => {
    const resp = await client.send(new GetRandomPasswordCommand({
      PasswordLength: 50,
      ExcludeCharacters: 'abcdefABCDEF0123456789',
      ExcludePunctuation: true,
      IncludeSpace: false,
    }));
    if (!resp.RandomPassword) throw new Error('RandomPassword to be defined');
    for (const c of resp.RandomPassword) {
      if (c >= 'a' && c <= 'f') throw new Error(`found excluded lowercase char: ${c}`);
      if (c >= 'A' && c <= 'F') throw new Error(`found excluded uppercase char: ${c}`);
      if (c >= '0' && c <= '5') throw new Error(`found excluded digit: ${c}`);
    }
  });

  await r('GetRandomPassword_RequireEachIncludedType', async () => {
    const resp = await client.send(new GetRandomPasswordCommand({
      PasswordLength: 20,
      RequireEachIncludedType: true,
    }));
    if (!resp.RandomPassword) throw new Error('RandomPassword to be defined');
    let hasLower = false, hasUpper = false, hasDigit = false, hasPunct = false;
    for (const c of resp.RandomPassword) {
      if (c >= 'a' && c <= 'z') hasLower = true;
      if (c >= 'A' && c <= 'Z') hasUpper = true;
      if (c >= '0' && c <= '9') hasDigit = true;
      if ((c >= '!' && c <= '/') || (c >= ':' && c <= '@') || (c >= '[' && c <= '`') || (c >= '{' && c <= '~')) hasPunct = true;
    }
    if (!hasLower || !hasUpper || !hasDigit || !hasPunct) {
      throw new Error(`missing required types: lower=${hasLower} upper=${hasUpper} digit=${hasDigit} punct=${hasPunct}`);
    }
  });

  await r('ValidateResourcePolicy_Valid', async () => {
    const policy = '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":"*","Action":"*","Resource":"*"}]}';
    const resp = await client.send(new ValidateResourcePolicyCommand({ ResourcePolicy: policy }));
    if (!resp.PolicyValidationPassed) throw new Error('expected policy validation to pass');
  });

  await r('ValidateResourcePolicy_Invalid', async () => {
    const resp = await client.send(new ValidateResourcePolicyCommand({ ResourcePolicy: 'not valid json {' }));
    if (resp.PolicyValidationPassed) throw new Error('expected policy validation to fail for invalid JSON');
  });
}
