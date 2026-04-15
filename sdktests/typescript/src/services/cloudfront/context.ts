import { CloudFrontClient } from '@aws-sdk/client-cloudfront';
import { ServiceContext, TestRunner, TestResult } from '../../runner.js';

export interface CloudFrontTestContext {
  client: CloudFrontClient;
  svc: string;
}

export type CloudFrontTestSection = (ctx: CloudFrontTestContext, runner: TestRunner) => Promise<TestResult[]>;

export function createCloudFrontTestContext(serviceCtx: ServiceContext): CloudFrontTestContext {
  const client = new CloudFrontClient({
    endpoint: serviceCtx.endpoint,
    region: serviceCtx.region,
    credentials: serviceCtx.credentials,
  });

  return { client, svc: 'cloudfront' };
}
