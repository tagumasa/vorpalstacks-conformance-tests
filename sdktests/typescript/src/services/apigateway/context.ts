import { APIGatewayClient } from '@aws-sdk/client-api-gateway';
import { TestRunner } from '../../runner.js';

export interface ApiGatewayTestContext {
  client: APIGatewayClient;
  runner: TestRunner;
  region: string;
  apiName: string;
  apiID: string;
  resourceID: string;
  deploymentID: string;
  validatorID: string;
  authorizerID: string;
  apiKeyID: string;
  apiKeyValue: string;
  usagePlanID: string;
  domainName: string;
}

export function createApiGatewayTestContext(
  endpoint: string,
  region: string,
): ApiGatewayTestContext {
  const apiName = `TestAPI-${Date.now()}`;
  return {
    client: new APIGatewayClient({
      endpoint,
      region,
      credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
    }),
    runner: new TestRunner({ endpoint, region, verbose: false }),
    region,
    apiName,
    apiID: '',
    resourceID: '',
    deploymentID: '',
    validatorID: '',
    authorizerID: '',
    apiKeyID: '',
    apiKeyValue: '',
    usagePlanID: '',
    domainName: '',
  };
}
