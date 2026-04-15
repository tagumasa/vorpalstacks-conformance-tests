import { IAMClient } from '@aws-sdk/client-iam';
import { TestRunner, TestResult } from '../../runner.js';

export interface IAMTestContext {
  client: IAMClient;
  ts: string;
  userName: string;
  groupName: string;
  roleName: string;
  policyName: string;
  profileName: string;
  userInlinePolicyName: string;
  roleInlinePolicyName: string;
  groupInlinePolicyName: string;
  accessKeyId: string;
  policyArn: string;
  accountAlias: string;
  serviceLinkedRoleName: string;
  samlProviderName: string;
  samlProviderArn: string;
  virtualMFADeviceName: string;
  virtualMFASerial: string;
}

export type IAMTestSection = (ctx: IAMTestContext, runner: TestRunner) => Promise<TestResult[]>;

export function createIAMTestContext(endpoint: string, region: string, credentials: { accessKeyId: string; secretAccessKey: string }): IAMTestContext {
  const ts = String(Date.now());
  return {
    client: new IAMClient({ endpoint, region, credentials }),
    ts,
    userName: `TestUser-${ts}`,
    groupName: `TestGroup-${ts}`,
    roleName: `TestRole-${ts}`,
    policyName: `TestPolicy-${ts}`,
    profileName: `TestProfile-${ts}`,
    userInlinePolicyName: `UserPolicy-${ts}`,
    roleInlinePolicyName: `RolePolicy-${ts}`,
    groupInlinePolicyName: `GroupPolicy-${ts}`,
    accessKeyId: '',
    policyArn: '',
    accountAlias: `test-alias-${ts}`,
    serviceLinkedRoleName: '',
    samlProviderName: `TestSAML-${ts}`,
    samlProviderArn: '',
    virtualMFADeviceName: `TestMFA-${ts}`,
    virtualMFASerial: '',
  };
}
