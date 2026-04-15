import {
  STSClient,
  AssumeRoleCommand,
  AssumeRoleWithSAMLCommand,
  AssumeRoleWithWebIdentityCommand,
} from '@aws-sdk/client-sts';
import type { TestRunner, TestResult } from '../../runner.js';

export async function runAssumeRoleTests(
  runner: TestRunner,
  client: STSClient,
  roleARN: string,
  samlRoleARN: string,
  webIdRoleARN: string,
  samlProviderARN: string,
  dummySAMLAssertion: string,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const svc = 'sts';

  results.push(await runner.runTest(svc, 'AssumeRole', async () => {
    const resp = await client.send(new AssumeRoleCommand({ RoleArn: roleARN, RoleSessionName: 'TestSession' }));
    if (!resp.Credentials) throw new Error('expected Credentials to be defined');
  }));

  results.push(await runner.runTest(svc, 'AssumeRole_NonExistentRole', async () => {
    let caught = false;
    try {
      await client.send(new AssumeRoleCommand({
        RoleArn: 'arn:aws:iam::000000000000:role/NonExistentRole',
        RoleSessionName: 'TestSession',
      }));
    } catch { caught = true; }
    if (!caught) throw new Error('expected error for non-existent role');
  }));

  results.push(await runner.runTest(svc, 'AssumeRole_ContentVerify', async () => {
    const resp = await client.send(new AssumeRoleCommand({ RoleArn: roleARN, RoleSessionName: 'VerifySession' }));
    const creds = resp.Credentials;
    if (!creds) throw new Error('expected Credentials to be defined');
    if (!creds.AccessKeyId) throw new Error('expected AccessKeyId to be defined');
    if (!creds.SecretAccessKey) throw new Error('expected SecretAccessKey to be defined');
    if (!creds.SessionToken) throw new Error('expected SessionToken to be defined');
    if (!creds.Expiration) throw new Error('expected Expiration to be defined');
    const user = resp.AssumedRoleUser;
    if (!user) throw new Error('expected AssumedRoleUser to be defined');
    if (!user.AssumedRoleId) throw new Error('expected AssumedRoleId to be defined');
    if (!user.Arn) throw new Error('expected AssumedRoleUser Arn to be defined');
  }));

  results.push(await runner.runTest(svc, 'AssumeRole_WithSourceIdentity', async () => {
    const resp = await client.send(new AssumeRoleCommand({
      RoleArn: roleARN,
      RoleSessionName: 'SourceIdSession',
      SourceIdentity: 'AdminUser',
    }));
    if (resp.SourceIdentity !== 'AdminUser') {
      throw new Error(`expected SourceIdentity "AdminUser", got "${resp.SourceIdentity}"`);
    }
  }));

  results.push(await runner.runTest(svc, 'AssumeRole_WithPolicy', async () => {
    const inlinePolicy = JSON.stringify({
      Version: '2012-10-17',
      Statement: [{ Effect: 'Allow', Action: 's3:GetObject', Resource: '*' }],
    });
    const resp = await client.send(new AssumeRoleCommand({
      RoleArn: roleARN,
      RoleSessionName: 'PolicySession',
      Policy: inlinePolicy,
    }));
    if (!resp.PackedPolicySize) {
      throw new Error(`expected PackedPolicySize > 0, got ${resp.PackedPolicySize}`);
    }
  }));

  results.push(await runner.runTest(svc, 'AssumeRole_InvalidDuration', async () => {
    let caught = false;
    try {
      await client.send(new AssumeRoleCommand({ RoleArn: roleARN, RoleSessionName: 'DurationSession', DurationSeconds: 100 }));
    } catch { caught = true; }
    if (!caught) throw new Error('expected error for duration < 900');
  }));

  results.push(await runner.runTest(svc, 'AssumeRole_EmptySessionName', async () => {
    let caught = false;
    try {
      await client.send(new AssumeRoleCommand({ RoleArn: roleARN, RoleSessionName: '' }));
    } catch { caught = true; }
    if (!caught) throw new Error('expected error for empty session name');
  }));

  // ========== AssumeRoleWithSAML ==========

  results.push(await runner.runTest(svc, 'AssumeRoleWithSAML_Basic', async () => {
    const resp = await client.send(new AssumeRoleWithSAMLCommand({
      RoleArn: samlRoleARN,
      PrincipalArn: samlProviderARN,
      SAMLAssertion: dummySAMLAssertion,
    }));
    if (!resp.Credentials) throw new Error('expected Credentials to be defined');
  }));

  results.push(await runner.runTest(svc, 'AssumeRoleWithSAML_ContentVerify', async () => {
    const resp = await client.send(new AssumeRoleWithSAMLCommand({
      RoleArn: samlRoleARN,
      PrincipalArn: samlProviderARN,
      SAMLAssertion: dummySAMLAssertion,
    }));
    if (!resp.Credentials?.AccessKeyId) throw new Error('expected Credentials.AccessKeyId to be defined');
    if (!resp.Credentials.Expiration) throw new Error('expected Expiration to be defined');
    if (!resp.AssumedRoleUser?.AssumedRoleId) throw new Error('expected AssumedRoleUser.AssumedRoleId to be defined');
    if (!resp.Subject) throw new Error('expected Subject to be defined');
    if (!resp.SubjectType) throw new Error('expected SubjectType to be defined');
    if (!resp.Issuer) throw new Error('expected Issuer to be defined');
  }));

  results.push(await runner.runTest(svc, 'AssumeRoleWithSAML_WithPolicy', async () => {
    const inlinePolicy = JSON.stringify({
      Version: '2012-10-17',
      Statement: [{ Effect: 'Allow', Action: '*', Resource: '*' }],
    });
    const resp = await client.send(new AssumeRoleWithSAMLCommand({
      RoleArn: samlRoleARN,
      PrincipalArn: samlProviderARN,
      SAMLAssertion: dummySAMLAssertion,
      Policy: inlinePolicy,
    }));
    if (!resp.PackedPolicySize) {
      throw new Error(`expected PackedPolicySize > 0, got ${resp.PackedPolicySize}`);
    }
  }));

  results.push(await runner.runTest(svc, 'AssumeRoleWithSAML_InvalidAssertion', async () => {
    let caught = false;
    try {
      await client.send(new AssumeRoleWithSAMLCommand({
        RoleArn: samlRoleARN,
        PrincipalArn: samlProviderARN,
        SAMLAssertion: '',
      }));
    } catch { caught = true; }
    if (!caught) throw new Error('expected error for empty SAML assertion');
  }));

  results.push(await runner.runTest(svc, 'AssumeRoleWithSAML_NonExistentRole', async () => {
    let caught = false;
    try {
      await client.send(new AssumeRoleWithSAMLCommand({
        RoleArn: 'arn:aws:iam::000000000000:role/NonExistentSAMLRole',
        PrincipalArn: samlProviderARN,
        SAMLAssertion: dummySAMLAssertion,
      }));
    } catch { caught = true; }
    if (!caught) throw new Error('expected error for non-existent role');
  }));

  // ========== AssumeRoleWithWebIdentity ==========

  results.push(await runner.runTest(svc, 'AssumeRoleWithWebIdentity_Basic', async () => {
    const resp = await client.send(new AssumeRoleWithWebIdentityCommand({
      RoleArn: webIdRoleARN,
      RoleSessionName: 'WebIdSession',
      WebIdentityToken: 'dummy-web-identity-token',
      ProviderId: 'example.com',
    }));
    if (!resp.Credentials) throw new Error('expected Credentials to be defined');
  }));

  results.push(await runner.runTest(svc, 'AssumeRoleWithWebIdentity_ContentVerify', async () => {
    const resp = await client.send(new AssumeRoleWithWebIdentityCommand({
      RoleArn: webIdRoleARN,
      RoleSessionName: 'WebIdVerifySession',
      WebIdentityToken: 'dummy-web-identity-token',
      ProviderId: 'example.com',
    }));
    if (!resp.Credentials?.AccessKeyId) throw new Error('expected Credentials.AccessKeyId to be defined');
    if (!resp.Credentials.Expiration) throw new Error('expected Expiration to be defined');
    if (!resp.AssumedRoleUser?.AssumedRoleId) throw new Error('expected AssumedRoleUser.AssumedRoleId to be defined');
    if (!resp.SubjectFromWebIdentityToken) throw new Error('expected SubjectFromWebIdentityToken to be defined');
    if (!resp.Audience) throw new Error('expected Audience to be defined');
  }));

  results.push(await runner.runTest(svc, 'AssumeRoleWithWebIdentity_WithPolicy', async () => {
    const inlinePolicy = JSON.stringify({
      Version: '2012-10-17',
      Statement: [{ Effect: 'Allow', Action: 'dynamodb:Query', Resource: '*' }],
    });
    const resp = await client.send(new AssumeRoleWithWebIdentityCommand({
      RoleArn: webIdRoleARN,
      RoleSessionName: 'WebIdPolicySession',
      WebIdentityToken: 'dummy-web-identity-token',
      ProviderId: 'example.com',
      Policy: inlinePolicy,
    }));
    if (!resp.PackedPolicySize) {
      throw new Error(`expected PackedPolicySize > 0, got ${resp.PackedPolicySize}`);
    }
  }));

  results.push(await runner.runTest(svc, 'AssumeRoleWithWebIdentity_EmptyToken', async () => {
    let caught = false;
    try {
      await client.send(new AssumeRoleWithWebIdentityCommand({
        RoleArn: webIdRoleARN,
        RoleSessionName: 'WebIdSession',
        WebIdentityToken: '',
        ProviderId: 'example.com',
      }));
    } catch { caught = true; }
    if (!caught) throw new Error('expected error for empty web identity token');
  }));

  results.push(await runner.runTest(svc, 'AssumeRoleWithWebIdentity_NonExistentRole', async () => {
    let caught = false;
    try {
      await client.send(new AssumeRoleWithWebIdentityCommand({
        RoleArn: 'arn:aws:iam::000000000000:role/NonExistentWebIdRole',
        RoleSessionName: 'WebIdSession',
        WebIdentityToken: 'dummy-token',
        ProviderId: 'example.com',
      }));
    } catch { caught = true; }
    if (!caught) throw new Error('expected error for non-existent role');
  }));

  return results;
}
