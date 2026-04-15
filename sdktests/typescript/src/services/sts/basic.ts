import {
  STSClient,
  GetCallerIdentityCommand,
  GetSessionTokenCommand,
  DecodeAuthorizationMessageCommand,
  GetAccessKeyInfoCommand,
  GetFederationTokenCommand,
  GetDelegatedAccessTokenCommand,
} from '@aws-sdk/client-sts';
import type { TestRunner, TestResult } from '../../runner.js';

export async function runBasicStsTests(
  runner: TestRunner,
  client: STSClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const svc = 'sts';

  results.push(await runner.runTest(svc, 'GetCallerIdentity', async () => {
    const resp = await client.send(new GetCallerIdentityCommand({}));
    if (!resp.UserId) throw new Error('expected UserId to be defined');
  }));

  results.push(await runner.runTest(svc, 'GetSessionToken', async () => {
    const resp = await client.send(new GetSessionTokenCommand({}));
    if (!resp.Credentials) throw new Error('expected Credentials to be defined');
  }));

  results.push(await runner.runTest(svc, 'GetCallerIdentity_ContentVerify', async () => {
    const resp = await client.send(new GetCallerIdentityCommand({}));
    if (!resp.Account) throw new Error('expected Account to be defined');
    if (!resp.Arn) throw new Error('expected Arn to be defined');
    if (!resp.UserId) throw new Error('expected UserId to be defined');
  }));

  results.push(await runner.runTest(svc, 'GetSessionToken_ContentVerify', async () => {
    const resp = await client.send(new GetSessionTokenCommand({ DurationSeconds: 3600 }));
    const creds = resp.Credentials;
    if (!creds) throw new Error('expected Credentials to be defined');
    if (!creds.AccessKeyId) throw new Error('expected AccessKeyId to be defined');
    if (!creds.SecretAccessKey) throw new Error('expected SecretAccessKey to be defined');
    if (!creds.SessionToken) throw new Error('expected SessionToken to be defined');
    if (!creds.Expiration) throw new Error('expected Expiration to be defined');
  }));

  results.push(await runner.runTest(svc, 'DecodeAuthorizationMessage_Basic', async () => {
    const originalMsg = '{"ErrorCode":"AccessDenied","Message":"Not authorized"}';
    const encoded = btoa(originalMsg);
    const resp = await client.send(new DecodeAuthorizationMessageCommand({ EncodedMessage: encoded }));
    if (resp.DecodedMessage !== originalMsg) {
      throw new Error(`decoded message mismatch, got: ${resp.DecodedMessage}`);
    }
  }));

  results.push(await runner.runTest(svc, 'DecodeAuthorizationMessage_PlainText', async () => {
    const originalMsg = 'Plain text error message';
    const encoded = btoa(originalMsg);
    const resp = await client.send(new DecodeAuthorizationMessageCommand({ EncodedMessage: encoded }));
    if (resp.DecodedMessage !== originalMsg) {
      throw new Error(`decoded message mismatch, got: ${resp.DecodedMessage}`);
    }
  }));

  results.push(await runner.runTest(svc, 'DecodeAuthorizationMessage_InvalidBase64', async () => {
    let caught = false;
    try {
      await client.send(new DecodeAuthorizationMessageCommand({ EncodedMessage: 'not-valid-base64!!!' }));
    } catch { caught = true; }
    if (!caught) throw new Error('expected error for invalid base64');
  }));

  results.push(await runner.runTest(svc, 'DecodeAuthorizationMessage_Empty', async () => {
    let caught = false;
    try {
      await client.send(new DecodeAuthorizationMessageCommand({ EncodedMessage: '' }));
    } catch { caught = true; }
    if (!caught) throw new Error('expected error for empty encoded message');
  }));

  results.push(await runner.runTest(svc, 'GetAccessKeyInfo_AKIAPrefix', async () => {
    const resp = await client.send(new GetAccessKeyInfoCommand({ AccessKeyId: 'AKIAIOSFODNN7EXAMPLE' }));
    if (!resp.Account) throw new Error('expected Account to be defined');
  }));

  results.push(await runner.runTest(svc, 'GetAccessKeyInfo_ASIAPrefix', async () => {
    const resp = await client.send(new GetAccessKeyInfoCommand({ AccessKeyId: 'ASIAIOSFODNN7EXAMPLE' }));
    if (!resp.Account) throw new Error('expected Account to be defined');
  }));

  results.push(await runner.runTest(svc, 'GetAccessKeyInfo_UnknownPrefix', async () => {
    const resp = await client.send(new GetAccessKeyInfoCommand({ AccessKeyId: 'UNKNOWN1234567890' }));
    if (!resp.Account) throw new Error('expected Account to be defined for unknown prefix');
  }));

  results.push(await runner.runTest(svc, 'GetAccessKeyInfo_Invalid', async () => {
    let caught = false;
    try {
      await client.send(new GetAccessKeyInfoCommand({ AccessKeyId: '' }));
    } catch { caught = true; }
    if (!caught) throw new Error('expected error for empty access key');
  }));

  results.push(await runner.runTest(svc, 'GetFederationToken_Basic', async () => {
    const resp = await client.send(new GetFederationTokenCommand({ Name: 'TestFederatedUser' }));
    if (!resp.Credentials) throw new Error('expected Credentials to be defined');
    if (!resp.FederatedUser) throw new Error('expected FederatedUser to be defined');
  }));

  results.push(await runner.runTest(svc, 'GetFederationToken_ContentVerify', async () => {
    const resp = await client.send(new GetFederationTokenCommand({ Name: 'FederatedVerify' }));
    if (!resp.Credentials?.AccessKeyId) throw new Error('expected Credentials.AccessKeyId to be defined');
    if (!resp.Credentials.SecretAccessKey) throw new Error('expected SecretAccessKey to be defined');
    if (!resp.Credentials.SessionToken) throw new Error('expected SessionToken to be defined');
    if (!resp.Credentials.Expiration) throw new Error('expected Expiration to be defined');
    if (!resp.FederatedUser) throw new Error('expected FederatedUser to be defined');
    if (!resp.FederatedUser.FederatedUserId) throw new Error('expected FederatedUserId to be defined');
    if (!resp.FederatedUser.Arn) throw new Error('expected FederatedUser Arn to be defined');
  }));

  results.push(await runner.runTest(svc, 'GetFederationToken_WithPolicy', async () => {
    const inlinePolicy = JSON.stringify({
      Version: '2012-10-17',
      Statement: [{ Effect: 'Allow', Action: 's3:*', Resource: '*' }],
    });
    const resp = await client.send(new GetFederationTokenCommand({ Name: 'FederatedPolicy', Policy: inlinePolicy }));
    if (!resp.PackedPolicySize) {
      throw new Error(`expected PackedPolicySize > 0, got ${resp.PackedPolicySize}`);
    }
  }));

  results.push(await runner.runTest(svc, 'GetFederationToken_InvalidName', async () => {
    let caught = false;
    try {
      await client.send(new GetFederationTokenCommand({ Name: '' }));
    } catch { caught = true; }
    if (!caught) throw new Error('expected error for empty name');
  }));

  results.push(await runner.runTest(svc, 'GetFederationToken_InvalidPolicy', async () => {
    let caught = false;
    try {
      await client.send(new GetFederationTokenCommand({ Name: 'FederatedBadPolicy', Policy: 'not-valid-json' }));
    } catch { caught = true; }
    if (!caught) throw new Error('expected error for malformed policy');
  }));

  results.push(await runner.runTest(svc, 'GetFederationToken_InvalidDuration', async () => {
    let caught = false;
    try {
      await client.send(new GetFederationTokenCommand({ Name: 'FederatedBadDuration', DurationSeconds: 100 }));
    } catch { caught = true; }
    if (!caught) throw new Error('expected error for duration < 900');
  }));

  results.push(await runner.runTest(svc, 'GetDelegatedAccessToken_Basic', async () => {
    const resp = await client.send(new GetDelegatedAccessTokenCommand({ TradeInToken: 'dummy-trade-in-token' }));
    if (!resp.Credentials) throw new Error('expected Credentials to be defined');
  }));

  results.push(await runner.runTest(svc, 'GetDelegatedAccessToken_ContentVerify', async () => {
    const resp = await client.send(new GetDelegatedAccessTokenCommand({ TradeInToken: 'dummy-trade-in-token-verify' }));
    if (!resp.Credentials?.AccessKeyId) throw new Error('expected Credentials.AccessKeyId to be defined');
    if (!resp.Credentials.Expiration) throw new Error('expected Expiration to be defined');
    if (!resp.AssumedPrincipal) throw new Error('expected AssumedPrincipal to be defined');
    if (resp.PackedPolicySize == null) throw new Error('expected PackedPolicySize to be defined');
  }));

  results.push(await runner.runTest(svc, 'GetDelegatedAccessToken_EmptyToken', async () => {
    let caught = false;
    try {
      await client.send(new GetDelegatedAccessTokenCommand({ TradeInToken: '' }));
    } catch { caught = true; }
    if (!caught) throw new Error('expected error for empty trade-in token');
  }));

  results.push(await runner.runTest(svc, 'GetSessionToken_ExtendedDuration', async () => {
    const resp = await client.send(new GetSessionTokenCommand({ DurationSeconds: 86400 }));
    if (!resp.Credentials) throw new Error('expected Credentials to be defined');
  }));

  return results;
}
