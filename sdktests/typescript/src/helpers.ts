import { IAMClient, CreateRoleCommand, DeleteRoleCommand } from '@aws-sdk/client-iam';

export function makeUniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 999999)}`;
}

export async function assertThrows(
  fn: () => Promise<unknown>,
  expectedName: string,
): Promise<void> {
  let err: unknown;
  try {
    await fn();
  } catch (e) {
    err = e;
  }
  if (err === undefined) {
    throw new Error(`Expected ${expectedName} but no error was thrown`);
  }
  if (err instanceof Error && err.name === expectedName) return;
  const actual = err instanceof Error ? `${err.constructor.name} (${err.name})` : String(err);
  throw new Error(`Expected ${expectedName}, got ${actual}`);
}

export function assertErrorContains(err: unknown, expectedType: string): void {
  if (err === undefined || err === null) {
    throw new Error(`expected error containing "${expectedType}", got nil`);
  }
  const message = err instanceof Error ? err.message : String(err);
  const code = (err as { Code?: string }).Code ?? '';
  const name = (err as { name?: string }).name ?? '';
  if (!message.includes(expectedType) && !code.includes(expectedType) && !name.includes(expectedType)) {
    throw new Error(`expected error containing "${expectedType}", got: ${message} (name=${name}, code=${code})`);
  }
}

export function assertNoError(err: unknown, context: string): void {
  if (err !== undefined && err !== null) {
    const message = err instanceof Error ? err.message : String(err);
    throw new Error(`${context}: ${message}`);
  }
}

export function assertNotNil<T>(value: T | null | undefined, name: string): asserts value is T {
  if (value === null || value === undefined) {
    throw new Error(`${name} is null`);
  }
}

export async function safeCleanup(fn: () => Promise<unknown>): Promise<void> {
  try {
    await fn();
  } catch { /* intentionally ignored */ }
}

const defaultTrustPolicy = JSON.stringify({
  Version: '2012-10-17',
  Statement: [
    {
      Effect: 'Allow',
      Principal: { Service: 'states.amazonaws.com' },
      Action: 'sts:AssumeRole',
    },
  ],
});

export async function createIAMRole(
  iamClient: IAMClient,
  roleName: string,
  trustPolicy?: string,
): Promise<void> {
  try {
    await iamClient.send(new CreateRoleCommand({
      RoleName: roleName,
      AssumeRolePolicyDocument: trustPolicy ?? defaultTrustPolicy,
    }));
  } catch { /* may already exist */ }
}

export async function deleteIAMRole(
  iamClient: IAMClient,
  roleName: string,
): Promise<void> {
  await safeCleanup(async () => {
    await iamClient.send(new DeleteRoleCommand({ RoleName: roleName }));
  });
}
