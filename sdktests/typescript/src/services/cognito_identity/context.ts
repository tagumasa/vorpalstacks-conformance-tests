import { CognitoIdentityClient } from '@aws-sdk/client-cognito-identity';
import type { TestRunner, TestResult } from '../../runner.js';

export interface CognitoIdentityTestContext {
  client: CognitoIdentityClient;
  svc: string;
  poolId: string;
  poolArn: string;
  identityId: string;
}

export type CognitoIdentityTestSection = (ctx: CognitoIdentityTestContext, runner: TestRunner) => Promise<TestResult[]>;
