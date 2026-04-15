import {
  CognitoIdentityProviderClient,
  CreateUserPoolCommand,
  DescribeUserPoolCommand,
  UpdateUserPoolCommand,
  DeleteUserPoolCommand,
  ListUserPoolsCommand,
  CreateUserPoolClientCommand,
  DescribeUserPoolClientCommand,
  UpdateUserPoolClientCommand,
  DeleteUserPoolClientCommand,
  ListUserPoolClientsCommand,
  CreateUserPoolDomainCommand,
  DescribeUserPoolDomainCommand,
  UpdateUserPoolDomainCommand,
  DeleteUserPoolDomainCommand,
  SetUserPoolMfaConfigCommand,
  GetUserPoolMfaConfigCommand,
  GetCSVHeaderCommand,
  DescribeRiskConfigurationCommand,
} from '@aws-sdk/client-cognito-identity-provider';
import type { TestRunner, TestResult } from '../../runner.js';
import { safeCleanup } from '../../helpers.js';

const SVC = 'cognito';
const uniqueName = (prefix: string) => `${prefix}-${Date.now()}-${Math.floor(Math.random() * 99999)}`;

export async function runPoolTests(
  runner: TestRunner,
  client: CognitoIdentityProviderClient,
): Promise<{ results: TestResult[]; userPoolId: string; clientId: string }> {
  const results: TestResult[] = [];
  let userPoolId = '';
  let clientId = '';

  const poolName = uniqueName('test-pool');

  results.push(await runner.runTest(SVC, 'CreateUserPool', async () => {
    const resp = await client.send(new CreateUserPoolCommand({
      PoolName: poolName,
      Policies: {
        PasswordPolicy: {
          MinimumLength: 8,
          RequireUppercase: true,
          RequireLowercase: true,
          RequireNumbers: true,
          RequireSymbols: false,
        },
      },
    }));
    const pool = resp.UserPool;
    if (!pool?.Id) throw new Error('expected UserPool.Id to be defined');
    if (pool.Name !== poolName) throw new Error(`name mismatch: ${pool.Name}`);
    if (!pool.Arn) throw new Error('expected Arn to be defined');
    userPoolId = pool.Id;
  }));

  if (userPoolId) {
    results.push(await runner.runTest(SVC, 'DescribeUserPool', async () => {
      const resp = await client.send(new DescribeUserPoolCommand({ UserPoolId: userPoolId }));
      const pool = resp.UserPool;
      if (!pool?.Id) throw new Error('expected UserPool.Id to be defined');
      if (pool.Name !== poolName) throw new Error(`name mismatch: ${pool.Name}`);
      if (!pool.Arn) throw new Error('expected Arn to be defined');
    }));

    const clientName = uniqueName('test-client');
    results.push(await runner.runTest(SVC, 'CreateUserPoolClient', async () => {
      const resp = await client.send(new CreateUserPoolClientCommand({
        UserPoolId: userPoolId,
        ClientName: clientName,
      }));
      const uc = resp.UserPoolClient;
      if (!uc?.ClientId) throw new Error('expected ClientId to be defined');
      if (uc.ClientName !== clientName) throw new Error(`name mismatch: ${uc.ClientName}`);
      clientId = uc.ClientId;
    }));

    if (clientId) {
      results.push(await runner.runTest(SVC, 'DescribeUserPoolClient', async () => {
        const resp = await client.send(new DescribeUserPoolClientCommand({
          ClientId: clientId,
          UserPoolId: userPoolId,
        }));
        const uc = resp.UserPoolClient;
        if (!uc?.ClientId) throw new Error('expected ClientId to be defined');
        if (uc.ClientName !== clientName) throw new Error(`name mismatch: ${uc.ClientName}`);
        if (uc.UserPoolId !== userPoolId) throw new Error(`pool ID mismatch: ${uc.UserPoolId}`);
      }));

      results.push(await runner.runTest(SVC, 'UpdateUserPoolClient', async () => {
        const resp = await client.send(new UpdateUserPoolClientCommand({
          ClientId: clientId,
          UserPoolId: userPoolId,
          ClientName: 'updated-client',
        }));
        if (resp.UserPoolClient?.ClientName !== 'updated-client') {
          throw new Error(`name not updated: ${resp.UserPoolClient?.ClientName}`);
        }
      }));
    }

    const domainName = uniqueName('test-domain');
    results.push(await runner.runTest(SVC, 'CreateUserPoolDomain', async () => {
      const resp = await client.send(new CreateUserPoolDomainCommand({
        Domain: domainName,
        UserPoolId: userPoolId,
      }));
      if (!resp.CloudFrontDomain) throw new Error('expected CloudFrontDomain to be defined');
    }));

    results.push(await runner.runTest(SVC, 'DescribeUserPoolDomain', async () => {
      const resp = await client.send(new DescribeUserPoolDomainCommand({ Domain: domainName }));
      const desc = resp.DomainDescription;
      if (!desc) throw new Error('expected DomainDescription to be defined');
      if (desc.UserPoolId !== userPoolId) throw new Error(`pool ID mismatch: ${desc.UserPoolId}`);
      if (desc.Domain !== domainName) throw new Error(`domain mismatch: ${desc.Domain}`);
    }));

    results.push(await runner.runTest(SVC, 'ListUserPoolClients', async () => {
      const resp = await client.send(new ListUserPoolClientsCommand({
        UserPoolId: userPoolId,
        MaxResults: 10,
      }));
      if (!resp.UserPoolClients?.length) throw new Error('expected at least one client');
    }));

    results.push(await runner.runTest(SVC, 'ListUserPools', async () => {
      const resp = await client.send(new ListUserPoolsCommand({ MaxResults: 10 }));
      if (!resp.UserPools?.length) throw new Error('expected at least one pool');
    }));

    results.push(await runner.runTest(SVC, 'UpdateUserPool', async () => {
      await client.send(new UpdateUserPoolCommand({
        UserPoolId: userPoolId,
        Policies: {
          PasswordPolicy: {
            MinimumLength: 10,
            RequireUppercase: true,
            RequireLowercase: true,
            RequireNumbers: true,
            RequireSymbols: true,
          },
        },
      }));
    }));

    results.push(await runner.runTest(SVC, 'SetUserPoolMfaConfig', async () => {
      await client.send(new SetUserPoolMfaConfigCommand({
        UserPoolId: userPoolId,
        MfaConfiguration: 'ON',
      }));
      const mfaResp = await client.send(new GetUserPoolMfaConfigCommand({ UserPoolId: userPoolId }));
      if (mfaResp.MfaConfiguration !== 'ON') throw new Error(`expected ON, got ${mfaResp.MfaConfiguration}`);
      await client.send(new SetUserPoolMfaConfigCommand({
        UserPoolId: userPoolId,
        MfaConfiguration: 'OFF',
      }));
    }));

    results.push(await runner.runTest(SVC, 'GetUserPoolMfaConfig', async () => {
      const resp = await client.send(new GetUserPoolMfaConfigCommand({ UserPoolId: userPoolId }));
      if (!resp.MfaConfiguration && !resp.SoftwareTokenMfaConfiguration && !resp.SmsMfaConfiguration && !resp.EmailMfaConfiguration) {
        throw new Error('expected at least one MFA config field');
      }
    }));

    results.push(await runner.runTest(SVC, 'GetCSVHeader', async () => {
      const resp = await client.send(new GetCSVHeaderCommand({ UserPoolId: userPoolId }));
      if (!resp.CSVHeader?.length) throw new Error('expected non-empty CSV header');
    }));

    results.push(await runner.runTest(SVC, 'DescribeRiskConfiguration', async () => {
      const resp = await client.send(new DescribeRiskConfigurationCommand({ UserPoolId: userPoolId }));
      if (!resp.RiskConfiguration) throw new Error('expected RiskConfiguration to be defined');
    }));

    results.push(await runner.runTest(SVC, 'UpdateUserPoolDomain', async () => {
      const udDomain = uniqueName('ud-domain');
      await client.send(new CreateUserPoolDomainCommand({ Domain: udDomain, UserPoolId: userPoolId }));
      try {
        const resp = await client.send(new UpdateUserPoolDomainCommand({ Domain: udDomain, UserPoolId: userPoolId }));
        if (!resp.CloudFrontDomain) throw new Error('expected CloudFrontDomain to be defined');
      } finally {
        await safeCleanup(() => client.send(new DeleteUserPoolDomainCommand({ Domain: udDomain, UserPoolId: userPoolId })));
      }
    }));

    results.push(await runner.runTest(SVC, 'DeleteUserPoolDomain', async () => {
      await client.send(new DeleteUserPoolDomainCommand({ Domain: domainName, UserPoolId: userPoolId }));
    }));

    if (clientId) {
      results.push(await runner.runTest(SVC, 'DeleteUserPoolClient', async () => {
        await client.send(new DeleteUserPoolClientCommand({ ClientId: clientId, UserPoolId: userPoolId }));
      }));
    }
  }

  return { results, userPoolId, clientId };
}

export async function runPoolErrorTests(
  runner: TestRunner,
  client: CognitoIdentityProviderClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest(SVC, 'DescribeUserPool_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new DescribeUserPoolCommand({ UserPoolId: 'us-east-1_nonexistentpool' }));
    } catch (e) { err = e; }
    if (!err) throw new Error('expected error for non-existent user pool');
  }));

  results.push(await runner.runTest(SVC, 'DeleteUserPool_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new DeleteUserPoolCommand({ UserPoolId: 'us-east-1_nonexistentpool' }));
    } catch (e) { err = e; }
    if (!err) throw new Error('expected error for non-existent user pool');
  }));

  results.push(await runner.runTest(SVC, 'CreateUserPool_DuplicateName', async () => {
    const dupName = uniqueName('dup-pool');
    const resp1 = await client.send(new CreateUserPoolCommand({ PoolName: dupName }));
    try {
      const resp2 = await client.send(new CreateUserPoolCommand({ PoolName: dupName }));
      if (resp2.UserPool?.Id === resp1.UserPool?.Id) throw new Error('duplicate pool should have different ID');
      await safeCleanup(() => client.send(new DeleteUserPoolCommand({ UserPoolId: resp2.UserPool!.Id! })));
    } finally {
      await safeCleanup(() => client.send(new DeleteUserPoolCommand({ UserPoolId: resp1.UserPool!.Id! })));
    }
  }));

  results.push(await runner.runTest(SVC, 'ListUserPools_Pagination', async () => {
    const ts = Date.now();
    const poolIds: string[] = [];
    for (let i = 0; i < 5; i++) {
      const createResp = await client.send(new CreateUserPoolCommand({ PoolName: `PagPool-${ts}-${i}` }));
      if (createResp.UserPool?.Id) poolIds.push(createResp.UserPool.Id);
    }
    try {
      let pages = 0;
      let nextToken: string | undefined;
      do {
        const resp = await client.send(new ListUserPoolsCommand({ MaxResults: 2, NextToken: nextToken }));
        pages++;
        nextToken = resp.NextToken;
      } while (nextToken);
      if (pages < 2) throw new Error(`expected at least 2 pages, got ${pages}`);
    } finally {
      for (const pid of poolIds) {
        await safeCleanup(() => client.send(new DeleteUserPoolCommand({ UserPoolId: pid })));
      }
    }
  }));

  return results;
}

export async function deletePoolAndCleanup(
  client: CognitoIdentityProviderClient,
  userPoolId: string,
): Promise<void> {
  if (userPoolId) {
    await safeCleanup(() => client.send(new DeleteUserPoolCommand({ UserPoolId: userPoolId })));
  }
}
