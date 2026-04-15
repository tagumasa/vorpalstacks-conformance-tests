import {
  CognitoIdentityProviderClient,
  CreateIdentityProviderCommand,
  DescribeIdentityProviderCommand,
  UpdateIdentityProviderCommand,
  DeleteIdentityProviderCommand,
  ListIdentityProvidersCommand,
  CreateResourceServerCommand,
  DescribeResourceServerCommand,
  UpdateResourceServerCommand,
  DeleteResourceServerCommand,
  ListResourceServersCommand,
  GetGroupCommand,
  TagResourceCommand,
  ListTagsForResourceCommand,
  UntagResourceCommand,
  CreateUserPoolCommand,
  DeleteUserPoolCommand,
} from '@aws-sdk/client-cognito-identity-provider';
import type { TestRunner, TestResult } from '../../runner.js';
import { safeCleanup } from '../../helpers.js';

const SVC = 'cognito';
const uniqueName = (prefix: string) => `${prefix}-${Date.now()}-${Math.floor(Math.random() * 99999)}`;

export async function runIdentityProviderTests(
  runner: TestRunner,
  client: CognitoIdentityProviderClient,
  userPoolId: string,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest(SVC, 'CreateIdentityProvider', async () => {
    const resp = await client.send(new CreateIdentityProviderCommand({
      UserPoolId: userPoolId,
      ProviderName: 'TestProvider',
      ProviderType: 'Facebook',
      ProviderDetails: {
        client_id: 'test-client-id',
        client_secret: 'test-client-secret',
        authorize_scopes: 'public_profile,email',
      },
    }));
    const idp = resp.IdentityProvider;
    if (idp?.ProviderName !== 'TestProvider') throw new Error(`name mismatch: ${idp?.ProviderName}`);
    if (idp?.ProviderType !== 'Facebook') throw new Error(`type mismatch: ${idp?.ProviderType}`);
    if (idp?.UserPoolId !== userPoolId) throw new Error(`pool ID mismatch: ${idp?.UserPoolId}`);
  }));

  results.push(await runner.runTest(SVC, 'ListIdentityProviders', async () => {
    const resp = await client.send(new ListIdentityProvidersCommand({ UserPoolId: userPoolId }));
    if (!resp.Providers?.length) throw new Error('expected at least one identity provider');
  }));

  results.push(await runner.runTest(SVC, 'DescribeIdentityProvider', async () => {
    const resp = await client.send(new DescribeIdentityProviderCommand({ UserPoolId: userPoolId, ProviderName: 'TestProvider' }));
    if (resp.IdentityProvider?.ProviderName !== 'TestProvider') throw new Error(`name mismatch: ${resp.IdentityProvider?.ProviderName}`);
    if (resp.IdentityProvider?.ProviderType !== 'Facebook') throw new Error(`type mismatch: ${resp.IdentityProvider?.ProviderType}`);
  }));

  results.push(await runner.runTest(SVC, 'UpdateIdentityProvider', async () => {
    await client.send(new UpdateIdentityProviderCommand({
      UserPoolId: userPoolId,
      ProviderName: 'TestProvider',
      ProviderDetails: { updated_key: 'updated_value' },
    }));
    const descResp = await client.send(new DescribeIdentityProviderCommand({ UserPoolId: userPoolId, ProviderName: 'TestProvider' }));
    if (descResp.IdentityProvider?.ProviderDetails?.updated_key !== 'updated_value') {
      throw new Error('ProviderDetails not updated');
    }
  }));

  results.push(await runner.runTest(SVC, 'DeleteIdentityProvider', async () => {
    const delProvider = uniqueName('del-provider');
    await client.send(new CreateIdentityProviderCommand({
      UserPoolId: userPoolId,
      ProviderName: delProvider,
      ProviderType: 'Google',
      ProviderDetails: { client_id: 'test' },
    }));
    await client.send(new DeleteIdentityProviderCommand({ UserPoolId: userPoolId, ProviderName: delProvider }));
    let err: unknown;
    try {
      await client.send(new DescribeIdentityProviderCommand({ UserPoolId: userPoolId, ProviderName: delProvider }));
    } catch (e) { err = e; }
    const name = (err as { name?: string })?.name ?? '';
    const msg = err instanceof Error ? err.message : '';
    if (!name.includes('ResourceNotFoundException') && !msg.includes('ResourceNotFoundException')) {
      throw new Error(`expected ResourceNotFoundException, got: ${msg}`);
    }
  }));

  return results;
}

export async function runResourceServerTests(
  runner: TestRunner,
  client: CognitoIdentityProviderClient,
  userPoolId: string,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const identifier = uniqueName('resource');
  results.push(await runner.runTest(SVC, 'CreateResourceServer', async () => {
    const resp = await client.send(new CreateResourceServerCommand({
      UserPoolId: userPoolId,
      Identifier: identifier,
      Name: 'Test Resource Server',
    }));
    const rs = resp.ResourceServer;
    if (rs?.Identifier !== identifier) throw new Error(`identifier mismatch: ${rs?.Identifier}`);
    if (rs?.Name !== 'Test Resource Server') throw new Error(`name mismatch: ${rs?.Name}`);
    if (rs.Scopes?.length) throw new Error('expected empty scopes');
  }));

  results.push(await runner.runTest(SVC, 'ListResourceServers', async () => {
    const resp = await client.send(new ListResourceServersCommand({ UserPoolId: userPoolId }));
    if (!resp.ResourceServers?.length) throw new Error('expected at least one resource server');
  }));

  results.push(await runner.runTest(SVC, 'DescribeResourceServer', async () => {
    const resp = await client.send(new DescribeResourceServerCommand({ UserPoolId: userPoolId, Identifier: identifier }));
    if (resp.ResourceServer?.Identifier !== identifier) throw new Error(`identifier mismatch`);
    if (resp.ResourceServer?.Name !== 'Test Resource Server') throw new Error(`name mismatch`);
  }));

  results.push(await runner.runTest(SVC, 'UpdateResourceServer', async () => {
    const resp = await client.send(new UpdateResourceServerCommand({
      UserPoolId: userPoolId,
      Identifier: identifier,
      Name: 'Updated Resource Server',
      Scopes: [{ ScopeName: 'read', ScopeDescription: 'Read access' }],
    }));
    if (resp.ResourceServer?.Name !== 'Updated Resource Server') throw new Error(`name not updated: ${resp.ResourceServer?.Name}`);
  }));

  results.push(await runner.runTest(SVC, 'DeleteResourceServer', async () => {
    const delRS = uniqueName('del-rs');
    await client.send(new CreateResourceServerCommand({ UserPoolId: userPoolId, Identifier: delRS, Name: 'Deletable RS' }));
    await client.send(new DeleteResourceServerCommand({ UserPoolId: userPoolId, Identifier: delRS }));
    let err: unknown;
    try {
      await client.send(new DescribeResourceServerCommand({ UserPoolId: userPoolId, Identifier: delRS }));
    } catch (e) { err = e; }
    const name = (err as { name?: string })?.name ?? '';
    const msg = err instanceof Error ? err.message : '';
    if (!name.includes('ResourceNotFoundException') && !msg.includes('ResourceNotFoundException')) {
      throw new Error(`expected ResourceNotFoundException, got: ${msg}`);
    }
  }));

  return results;
}

export async function runTagTests(
  runner: TestRunner,
  client: CognitoIdentityProviderClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest(SVC, 'TagResource', async () => {
    const poolName = uniqueName('test-pool-tags');
    const newPool = await client.send(new CreateUserPoolCommand({ PoolName: poolName }));
    try {
      const arn = newPool.UserPool!.Arn!;
      await client.send(new TagResourceCommand({ ResourceArn: arn, Tags: { Environment: 'test', Owner: 'test-user' } }));
      const listResp = await client.send(new ListTagsForResourceCommand({ ResourceArn: arn }));
      if (listResp.Tags?.Environment !== 'test') throw new Error('tag Environment not found');
    } finally {
      await safeCleanup(() => client.send(new DeleteUserPoolCommand({ UserPoolId: newPool.UserPool!.Id! })));
    }
  }));

  results.push(await runner.runTest(SVC, 'ListTagsForResource', async () => {
    const poolName = uniqueName('test-pool-listtags');
    const newPool = await client.send(new CreateUserPoolCommand({ PoolName: poolName }));
    try {
      const arn = newPool.UserPool!.Arn!;
      await client.send(new TagResourceCommand({ ResourceArn: arn, Tags: { Test: 'value' } }));
      const resp = await client.send(new ListTagsForResourceCommand({ ResourceArn: arn }));
      if (resp.Tags?.Test !== 'value') throw new Error(`expected Test=value, got ${resp.Tags?.Test}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteUserPoolCommand({ UserPoolId: newPool.UserPool!.Id! })));
    }
  }));

  results.push(await runner.runTest(SVC, 'UntagResource', async () => {
    const poolName = uniqueName('test-pool-untag');
    const newPool = await client.send(new CreateUserPoolCommand({ PoolName: poolName }));
    try {
      const arn = newPool.UserPool!.Arn!;
      await client.send(new TagResourceCommand({ ResourceArn: arn, Tags: { Test: 'value' } }));
      await client.send(new UntagResourceCommand({ ResourceArn: arn, TagKeys: ['Test'] }));
      const listResp = await client.send(new ListTagsForResourceCommand({ ResourceArn: arn }));
      if ('Test' in (listResp.Tags ?? {})) throw new Error('tag Test should have been removed');
    } finally {
      await safeCleanup(() => client.send(new DeleteUserPoolCommand({ UserPoolId: newPool.UserPool!.Id! })));
    }
  }));

  return results;
}

export async function runIdpRsErrorTests(
  runner: TestRunner,
  client: CognitoIdentityProviderClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest(SVC, 'GetGroup_NonExistent', async () => {
    const poolName = uniqueName('ge-pool');
    const createResp = await client.send(new CreateUserPoolCommand({ PoolName: poolName }));
    try {
      let err: unknown;
      try {
        await client.send(new GetGroupCommand({ GroupName: 'nonexistent-group-xyz', UserPoolId: createResp.UserPool!.Id! }));
      } catch (e) { err = e; }
      if (!err) throw new Error('expected error for non-existent group');
    } finally {
      await safeCleanup(() => client.send(new DeleteUserPoolCommand({ UserPoolId: createResp.UserPool!.Id! })));
    }
  }));

  results.push(await runner.runTest(SVC, 'DescribeIdentityProvider_NonExistent', async () => {
    const poolName = uniqueName('dip-pool');
    const createResp = await client.send(new CreateUserPoolCommand({ PoolName: poolName }));
    try {
      let err: unknown;
      try {
        await client.send(new DescribeIdentityProviderCommand({ UserPoolId: createResp.UserPool!.Id!, ProviderName: 'nonexistent-idp-xyz' }));
      } catch (e) { err = e; }
      if (!err) throw new Error('expected error for non-existent identity provider');
    } finally {
      await safeCleanup(() => client.send(new DeleteUserPoolCommand({ UserPoolId: createResp.UserPool!.Id! })));
    }
  }));

  results.push(await runner.runTest(SVC, 'DescribeResourceServer_NonExistent', async () => {
    const poolName = uniqueName('drs-pool');
    const createResp = await client.send(new CreateUserPoolCommand({ PoolName: poolName }));
    try {
      let err: unknown;
      try {
        await client.send(new DescribeResourceServerCommand({ UserPoolId: createResp.UserPool!.Id!, Identifier: 'nonexistent-rs-xyz' }));
      } catch (e) { err = e; }
      if (!err) throw new Error('expected error for non-existent resource server');
    } finally {
      await safeCleanup(() => client.send(new DeleteUserPoolCommand({ UserPoolId: createResp.UserPool!.Id! })));
    }
  }));

  results.push(await runner.runTest(SVC, 'DeleteIdentityProvider_NonExistent', async () => {
    const poolName = uniqueName('dlip-pool');
    const createResp = await client.send(new CreateUserPoolCommand({ PoolName: poolName }));
    try {
      let err: unknown;
      try {
        await client.send(new DeleteIdentityProviderCommand({ UserPoolId: createResp.UserPool!.Id!, ProviderName: 'nonexistent-idp-xyz' }));
      } catch (e) { err = e; }
      if (!err) throw new Error('expected error for non-existent identity provider');
    } finally {
      await safeCleanup(() => client.send(new DeleteUserPoolCommand({ UserPoolId: createResp.UserPool!.Id! })));
    }
  }));

  results.push(await runner.runTest(SVC, 'DeleteResourceServer_NonExistent', async () => {
    const poolName = uniqueName('dlrs-pool');
    const createResp = await client.send(new CreateUserPoolCommand({ PoolName: poolName }));
    try {
      let err: unknown;
      try {
        await client.send(new DeleteResourceServerCommand({ UserPoolId: createResp.UserPool!.Id!, Identifier: 'nonexistent-rs-xyz' }));
      } catch (e) { err = e; }
      if (!err) throw new Error('expected error for non-existent resource server');
    } finally {
      await safeCleanup(() => client.send(new DeleteUserPoolCommand({ UserPoolId: createResp.UserPool!.Id! })));
    }
  }));

  return results;
}
