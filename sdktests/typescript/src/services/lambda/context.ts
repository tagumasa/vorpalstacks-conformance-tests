import { LambdaClient } from '@aws-sdk/client-lambda';
import { IAMClient } from '@aws-sdk/client-iam';
import { createIAMRole, deleteIAMRole } from '../../helpers.js';

/** Lambda実行ロール用の信頼ポリシー（全ファイルで共用） */
export const lambdaTrustPolicy = JSON.stringify({
  Version: '2012-10-17',
  Statement: [
    {
      Effect: 'Allow',
      Principal: { Service: 'lambda.amazonaws.com' },
      Action: 'sts:AssumeRole',
    },
  ],
});

export interface LambdaTestContext {
  client: LambdaClient;
  iamClient: IAMClient;
  ts: string;
  functionName: string;
  roleName: string;
  roleArn: string;
  functionCode: Uint8Array;
}

export type LambdaTestSection = (ctx: LambdaTestContext, runner: import('../../runner.js').TestRunner) => Promise<import('../../runner.js').TestResult[]>;

export async function createLambdaTestContext(
  endpoint: string,
  region: string,
  credentials: { accessKeyId: string; secretAccessKey: string },
): Promise<LambdaTestContext> {
  const ts = String(Date.now());
  const roleName = `TestRole-${ts}`;
  const functionName = `TestFunction-${ts}`;
  const roleArn = `arn:aws:iam::000000000000:role/${roleName}`;

  const iamClient = new IAMClient({ endpoint, region, credentials });
  await createIAMRole(iamClient, roleName, lambdaTrustPolicy);

  const client = new LambdaClient({ endpoint, region, credentials });

  const functionCode = new TextEncoder().encode(
    'exports.handler = async (event) => { return { statusCode: 200, body: "Hello" }; };',
  );

  return { client, iamClient, ts, functionName, roleName, roleArn, functionCode };
}

export async function cleanupLambdaTestContext(ctx: LambdaTestContext): Promise<void> {
  await deleteIAMRole(ctx.iamClient, ctx.roleName);
}
