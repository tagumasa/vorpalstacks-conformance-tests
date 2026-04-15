import {
  CreateRestApiCommand,
  DeleteRestApiCommand,
  GetRestApisCommand,
  GetRestApiCommand,
  UpdateRestApiCommand,
  CreateResourceCommand,
  GetResourcesCommand,
  CreateDeploymentCommand,
  GetDeploymentCommand,
  UpdateDeploymentCommand,
  CreateStageCommand,
  GetStageCommand,
  UpdateStageCommand,
  DeleteStageCommand,
  DeleteDeploymentCommand,
  PutMethodCommand,
  GetMethodCommand,
  PutIntegrationCommand,
  GetIntegrationCommand,
  PutIntegrationResponseCommand,
  PutMethodResponseCommand,
  DeleteMethodResponseCommand,
  DeleteIntegrationResponseCommand,
  DeleteIntegrationCommand,
  DeleteMethodCommand,
  DeleteResourceCommand,
  CreateRequestValidatorCommand,
  GetRequestValidatorsCommand,
  CreateModelCommand,
  GetModelCommand,
  UpdateModelCommand,
  GetModelsCommand,
  DeleteModelCommand,
  CreateAuthorizerCommand,
  UpdateAuthorizerCommand,
  GetAuthorizerCommand,
  GetAuthorizersCommand,
  DeleteAuthorizerCommand,
  CreateDomainNameCommand,
  CreateBasePathMappingCommand,
  GetBasePathMappingsCommand,
  DeleteBasePathMappingCommand,
  DeleteDomainNameCommand,
  CreateUsagePlanCommand,
  GetUsagePlanCommand,
  DeleteUsagePlanCommand,
} from '@aws-sdk/client-api-gateway';
import type { TestResult } from '../../runner.js';
import { ApiGatewayTestContext } from './context.js';
import { safeCleanup } from '../../helpers.js';

export async function runLifecycleTests(ctx: ApiGatewayTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;

  results.push(await runner.runTest('apigateway', 'GetRestApis_ContainsCreated', async () => {
    const gaAPI = `GaAPI-${Date.now()}`;
    const gaDesc = 'searchable description';
    const createResp = await client.send(new CreateRestApiCommand({
      name: gaAPI,
      description: gaDesc,
    }));
    const createdId = createResp.id!;

    try {
      const resp = await client.send(new GetRestApisCommand({ limit: 500 }));
      let found = false;
      for (const item of resp.items ?? []) {
        if (item.name === gaAPI) {
          found = true;
          if (item.description !== gaDesc) throw new Error(`description mismatch in list, got ${item.description}`);
          break;
        }
      }
      if (!found) throw new Error(`created API ${gaAPI} not found in GetRestApis`);
    } finally {
      await safeCleanup(() => client.send(new DeleteRestApiCommand({ restApiId: createdId })));
    }
  }));

  results.push(await runner.runTest('apigateway', 'UpdateRestApi_VerifyUpdate', async () => {
    const uaAPI = `UaAPI-${Date.now()}`;
    const createResp = await client.send(new CreateRestApiCommand({
      name: uaAPI,
      description: 'original desc',
    }));
    const apiId = createResp.id!;

    try {
      const newDesc = 'updated description v2';
      await client.send(new UpdateRestApiCommand({
        restApiId: apiId,
        patchOperations: [{ op: 'replace', path: '/description', value: newDesc }],
      }));

      const resp = await client.send(new GetRestApiCommand({ restApiId: apiId }));
      if (resp.description !== newDesc) throw new Error(`description not updated, got ${resp.description}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteRestApiCommand({ restApiId: apiId })));
    }
  }));

  results.push(await runner.runTest('apigateway', 'CreateResource_NestedPath', async () => {
    const crAPI = `CrAPI-${Date.now()}`;
    const createResp = await client.send(new CreateRestApiCommand({ name: crAPI }));
    const apiId = createResp.id!;

    try {
      const usersResp = await client.send(new CreateResourceCommand({
        restApiId: apiId,
        parentId: apiId,
        pathPart: 'users',
      }));

      const userIdResp = await client.send(new CreateResourceCommand({
        restApiId: apiId,
        parentId: usersResp.id!,
        pathPart: '{userId}',
      }));
      if (userIdResp.path !== '/users/{userId}') throw new Error(`nested path mismatch, got ${userIdResp.path}`);

      const resResp = await client.send(new GetResourcesCommand({ restApiId: apiId }));
      if (!resResp.items || resResp.items.length < 3) {
        throw new Error(`expected at least 3 resources (root, users, {userId}), got ${resResp.items?.length ?? 0}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteRestApiCommand({ restApiId: apiId })));
    }
  }));

  results.push(await runner.runTest('apigateway', 'GetStage_NonExistent', async () => {
    const tmpAPI = `TmpAPI-${Date.now()}`;
    const createResp = await client.send(new CreateRestApiCommand({ name: tmpAPI }));
    const apiId = createResp.id!;

    try {
      let err: unknown;
      try {
        await client.send(new GetStageCommand({
          restApiId: apiId,
          stageName: 'nonexistent_stage',
        }));
      } catch (e) {
        err = e;
      }
      if (!err) throw new Error('expected error for non-existent stage');
      if (typeof err !== 'object' || err === null) throw new Error(`expected error object`);
      const name = (err as { name?: string }).name ?? '';
      const code = (err as { Code?: string }).Code ?? '';
      const message = err instanceof Error ? err.message : String(err);
      if (!name.includes('NotFoundException') && !code.includes('NotFoundException') && !message.includes('NotFoundException')) {
        throw new Error(`expected NotFoundException, got: ${message} (name=${name}, code=${code})`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteRestApiCommand({ restApiId: apiId })));
    }
  }));

  results.push(await runner.runTest('apigateway', 'CreateStage_VerifyConfig', async () => {
    const csAPI = `CsAPI-${Date.now()}`;
    const createResp = await client.send(new CreateRestApiCommand({ name: csAPI }));
    const apiId = createResp.id!;

    try {
      const depResp = await client.send(new CreateDeploymentCommand({ restApiId: apiId }));
      const depId = depResp.id!;

      const stageDesc = 'test stage description';
      await client.send(new CreateStageCommand({
        restApiId: apiId,
        stageName: 'v1',
        deploymentId: depId,
        description: stageDesc,
      }));

      const resp = await client.send(new GetStageCommand({
        restApiId: apiId,
        stageName: 'v1',
      }));
      if (resp.description !== stageDesc) throw new Error(`stage description mismatch, got ${resp.description}`);
      if (resp.deploymentId !== depId) throw new Error(`deployment ID mismatch, got ${resp.deploymentId}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteRestApiCommand({ restApiId: apiId })));
    }
  }));

  results.push(await runner.runTest('apigateway', 'PutMethod_AuthorizationTypes', async () => {
    const pmAPI = `PmAPI-${Date.now()}`;
    const createResp = await client.send(new CreateRestApiCommand({ name: pmAPI }));
    const apiId = createResp.id!;

    try {
      const resResp = await client.send(new CreateResourceCommand({
        restApiId: apiId,
        parentId: apiId,
        pathPart: 'secure',
      }));

      for (const authType of ['NONE', 'AWS_IAM', 'CUSTOM']) {
        await client.send(new PutMethodCommand({
          restApiId: apiId,
          resourceId: resResp.id!,
          httpMethod: 'GET',
          authorizationType: authType,
        }));

        const getResp = await client.send(new GetMethodCommand({
          restApiId: apiId,
          resourceId: resResp.id!,
          httpMethod: 'GET',
        }));
        if (getResp.authorizationType !== authType) {
          throw new Error(`auth type mismatch for ${authType}, got ${getResp.authorizationType}`);
        }
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteRestApiCommand({ restApiId: apiId })));
    }
  }));

  results.push(await runner.runTest('apigateway', 'PutIntegration_Types', async () => {
    const itAPI = `ItAPI-${Date.now()}`;
    const createResp = await client.send(new CreateRestApiCommand({ name: itAPI }));
    const apiId = createResp.id!;

    try {
      const resResp = await client.send(new CreateResourceCommand({
        restApiId: apiId,
        parentId: apiId,
        pathPart: 'inttest',
      }));

      await client.send(new PutMethodCommand({
        restApiId: apiId,
        resourceId: resResp.id!,
        httpMethod: 'POST',
        authorizationType: 'NONE',
      }));

      for (const intType of ['MOCK', 'HTTP', 'HTTP_PROXY', 'AWS_PROXY'] as const) {
        await client.send(new PutIntegrationCommand({
          restApiId: apiId,
          resourceId: resResp.id!,
          httpMethod: 'POST',
          type: intType,
        }));

        const getResp = await client.send(new GetIntegrationCommand({
          restApiId: apiId,
          resourceId: resResp.id!,
          httpMethod: 'POST',
        }));
        if (getResp.type !== intType) {
          throw new Error(`type mismatch, expected ${intType} got ${getResp.type}`);
        }
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteRestApiCommand({ restApiId: apiId })));
    }
  }));

  results.push(await runner.runTest('apigateway', 'MethodWithIntegration_FullLifecycle', async () => {
    const lcAPI = `LcAPI-${Date.now()}`;
    const createResp = await client.send(new CreateRestApiCommand({ name: lcAPI }));
    const apiId = createResp.id!;

    try {
      const resResp = await client.send(new CreateResourceCommand({
        restApiId: apiId,
        parentId: apiId,
        pathPart: 'lifecycle',
      }));

      await client.send(new PutMethodCommand({
        restApiId: apiId,
        resourceId: resResp.id!,
        httpMethod: 'GET',
        authorizationType: 'NONE',
        operationName: 'GetLifecycle',
      }));

      await client.send(new PutIntegrationCommand({
        restApiId: apiId,
        resourceId: resResp.id!,
        httpMethod: 'GET',
        type: 'MOCK',
        integrationHttpMethod: 'POST',
        uri: 'https://httpbin.org/post',
        requestParameters: { 'integration.request.header.X-Custom': "'static'" },
        requestTemplates: { 'application/json': '{"statusCode":200}' },
        passthroughBehavior: 'WHEN_NO_MATCH',
        timeoutInMillis: 3000,
        cacheNamespace: 'lifecycle',
        cacheKeyParameters: ['header.Authorization'],
      }));

      const getIntResp = await client.send(new GetIntegrationCommand({
        restApiId: apiId,
        resourceId: resResp.id!,
        httpMethod: 'GET',
      }));
      if (getIntResp.uri !== 'https://httpbin.org/post') throw new Error(`uri mismatch, got ${getIntResp.uri}`);
      if (getIntResp.timeoutInMillis !== 3000) throw new Error(`timeoutInMillis mismatch, got ${getIntResp.timeoutInMillis}`);

      await client.send(new PutIntegrationResponseCommand({
        restApiId: apiId,
        resourceId: resResp.id!,
        httpMethod: 'GET',
        statusCode: '200',
        responseParameters: { 'method.response.header.Content-Type': 'integration.response.header.Content-Type' },
        responseTemplates: { 'application/json': "$input.json('$')" },
        selectionPattern: '2\\d{2}',
      }));

      await client.send(new PutMethodResponseCommand({
        restApiId: apiId,
        resourceId: resResp.id!,
        httpMethod: 'GET',
        statusCode: '200',
        responseParameters: { 'method.response.header.Content-Type': true },
        responseModels: { 'application/json': 'Empty' },
      }));

      await client.send(new DeleteMethodResponseCommand({
        restApiId: apiId,
        resourceId: resResp.id!,
        httpMethod: 'GET',
        statusCode: '200',
      }));

      await client.send(new DeleteIntegrationResponseCommand({
        restApiId: apiId,
        resourceId: resResp.id!,
        httpMethod: 'GET',
        statusCode: '200',
      }));

      await client.send(new DeleteIntegrationCommand({
        restApiId: apiId,
        resourceId: resResp.id!,
        httpMethod: 'GET',
      }));

      await client.send(new DeleteMethodCommand({
        restApiId: apiId,
        resourceId: resResp.id!,
        httpMethod: 'GET',
      }));

      await client.send(new DeleteResourceCommand({
        restApiId: apiId,
        resourceId: resResp.id!,
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteRestApiCommand({ restApiId: apiId })));
    }
  }));

  results.push(await runner.runTest('apigateway', 'Deployment_Stage_FullLifecycle', async () => {
    const dsAPI = `DsAPI-${Date.now()}`;
    const createResp = await client.send(new CreateRestApiCommand({ name: dsAPI }));
    const apiId = createResp.id!;

    try {
      const depResp = await client.send(new CreateDeploymentCommand({
        restApiId: apiId,
        description: 'v1 deployment',
      }));
      const depId = depResp.id!;

      const getDepResp = await client.send(new GetDeploymentCommand({
        restApiId: apiId,
        deploymentId: depId,
      }));
      if (getDepResp.description !== 'v1 deployment') throw new Error(`deployment description mismatch, got ${getDepResp.description}`);

      await client.send(new UpdateDeploymentCommand({
        restApiId: apiId,
        deploymentId: depId,
        patchOperations: [{ op: 'replace', path: '/description', value: 'v1 updated' }],
      }));

      await client.send(new CreateStageCommand({
        restApiId: apiId,
        stageName: 'production',
        deploymentId: depId,
        description: 'production stage',
      }));

      await client.send(new UpdateStageCommand({
        restApiId: apiId,
        stageName: 'production',
        patchOperations: [
          { op: 'replace', path: '/description', value: 'production updated' },
          { op: 'replace', path: '/variables/env', value: 'prod' },
        ],
      }));

      const stageResp = await client.send(new GetStageCommand({
        restApiId: apiId,
        stageName: 'production',
      }));
      if (!stageResp.variables || stageResp.variables['env'] !== 'prod') {
        throw new Error(`stage variables not set, got ${JSON.stringify(stageResp.variables)}`);
      }

      await client.send(new DeleteStageCommand({ restApiId: apiId, stageName: 'production' }));
      await client.send(new DeleteDeploymentCommand({ restApiId: apiId, deploymentId: depId }));
    } finally {
      await safeCleanup(() => client.send(new DeleteRestApiCommand({ restApiId: apiId })));
    }
  }));

  results.push(await runner.runTest('apigateway', 'RequestValidator_FullLifecycle', async () => {
    const rvAPI = `RvAPI-${Date.now()}`;
    const createResp = await client.send(new CreateRestApiCommand({ name: rvAPI }));
    const apiId = createResp.id!;

    try {
      await client.send(new CreateRequestValidatorCommand({
        restApiId: apiId,
        name: 'body-only',
        validateRequestBody: true,
        validateRequestParameters: false,
      }));

      await client.send(new CreateRequestValidatorCommand({
        restApiId: apiId,
        name: 'params-only',
        validateRequestBody: false,
        validateRequestParameters: true,
      }));

      const rvListResp = await client.send(new GetRequestValidatorsCommand({
        restApiId: apiId,
        limit: 100,
      }));
      if (!rvListResp.items || rvListResp.items.length < 2) {
        throw new Error(`expected at least 2 validators, got ${rvListResp.items?.length ?? 0}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteRestApiCommand({ restApiId: apiId })));
    }
  }));

  results.push(await runner.runTest('apigateway', 'Model_FullLifecycle', async () => {
    const mlAPI = `MlAPI-${Date.now()}`;
    const createResp = await client.send(new CreateRestApiCommand({ name: mlAPI }));
    const apiId = createResp.id!;

    try {
      await client.send(new CreateModelCommand({
        restApiId: apiId,
        name: 'ErrorModel',
        contentType: 'application/json',
        description: 'Error response',
        schema: '{"type":"object","properties":{"message":{"type":"string"}}}',
      }));

      const getResp = await client.send(new GetModelCommand({
        restApiId: apiId,
        modelName: 'ErrorModel',
      }));
      if (getResp.contentType !== 'application/json') throw new Error(`contentType mismatch, got ${getResp.contentType}`);

      await client.send(new UpdateModelCommand({
        restApiId: apiId,
        modelName: 'ErrorModel',
        patchOperations: [{ op: 'replace', path: '/schema', value: '{"type":"object"}' }],
      }));

      const modelsResp = await client.send(new GetModelsCommand({
        restApiId: apiId,
        limit: 100,
      }));
      if (!modelsResp.items || modelsResp.items.length === 0) throw new Error('expected at least 1 model');

      await client.send(new DeleteModelCommand({
        restApiId: apiId,
        modelName: 'ErrorModel',
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteRestApiCommand({ restApiId: apiId })));
    }
  }));

  results.push(await runner.runTest('apigateway', 'Authorizer_FullLifecycle', async () => {
    const auAPI = `AuAPI-${Date.now()}`;
    const createResp = await client.send(new CreateRestApiCommand({ name: auAPI }));
    const apiId = createResp.id!;

    try {
      const authResp = await client.send(new CreateAuthorizerCommand({
        restApiId: apiId,
        name: 'lambda-auth',
        type: 'TOKEN',
        authorizerUri: 'https://example.com/lambda',
        identitySource: 'method.request.header.Auth',
        authorizerCredentials: 'arn:aws:iam::123456789012:role/lambda-auth-role',
        identityValidationExpression: 'Bearer .*',
        authorizerResultTtlInSeconds: 600,
      }));
      if (authResp.authorizerResultTtlInSeconds !== 600) {
        throw new Error(`ttl mismatch, got ${authResp.authorizerResultTtlInSeconds}`);
      }

      await client.send(new UpdateAuthorizerCommand({
        restApiId: apiId,
        authorizerId: authResp.id!,
        patchOperations: [{ op: 'replace', path: '/authorizerResultTtlInSeconds', value: '1200' }],
      }));

      const getAuthResp = await client.send(new GetAuthorizerCommand({
        restApiId: apiId,
        authorizerId: authResp.id!,
      }));
      if (getAuthResp.authorizerResultTtlInSeconds !== 1200) {
        throw new Error(`ttl not updated, got ${getAuthResp.authorizerResultTtlInSeconds}`);
      }

      const authListResp = await client.send(new GetAuthorizersCommand({
        restApiId: apiId,
        limit: 100,
      }));
      if (!authListResp.items || authListResp.items.length === 0) throw new Error('expected at least 1 authorizer');

      await client.send(new DeleteAuthorizerCommand({
        restApiId: apiId,
        authorizerId: authResp.id!,
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteRestApiCommand({ restApiId: apiId })));
    }
  }));

  results.push(await runner.runTest('apigateway', 'DomainName_BasePathMapping_FullLifecycle', async () => {
    const dbAPI = `DbAPI-${Date.now()}`;
    const createResp = await client.send(new CreateRestApiCommand({ name: dbAPI }));
    const apiId = createResp.id!;

    const domain = `lc-${Date.now()}.example.com`;
    const dnResp = await client.send(new CreateDomainNameCommand({
      domainName: domain,
      certificateName: 'lc-cert',
    }));

    try {
      await client.send(new CreateBasePathMappingCommand({
        domainName: domain,
        restApiId: apiId,
        basePath: '(none)',
        stage: 'prod',
      }));

      await client.send(new GetBasePathMappingsCommand({ domainName: domain }));

      await client.send(new DeleteBasePathMappingCommand({
        domainName: domain,
        basePath: '(none)',
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteDomainNameCommand({ domainName: domain })));
      await safeCleanup(() => client.send(new DeleteRestApiCommand({ restApiId: apiId })));
    }
  }));

  results.push(await runner.runTest('apigateway', 'UsagePlan_WithApiStages', async () => {
    const usAPI = `UsAPI-${Date.now()}`;
    const createResp = await client.send(new CreateRestApiCommand({ name: usAPI }));
    const apiId = createResp.id!;

    try {
      const depResp = await client.send(new CreateDeploymentCommand({ restApiId: apiId }));

      await client.send(new CreateStageCommand({
        restApiId: apiId,
        stageName: 'api-stage',
        deploymentId: depResp.id!,
      }));

      const upResp = await client.send(new CreateUsagePlanCommand({
        name: `us-plan-${Date.now()}`,
        apiStages: [{ apiId: apiId, stage: 'api-stage' }],
      }));

      const getResp = await client.send(new GetUsagePlanCommand({ usagePlanId: upResp.id! }));
      if (!getResp.apiStages || getResp.apiStages.length === 0) throw new Error('expected apiStages to be set');

      await safeCleanup(() => client.send(new DeleteUsagePlanCommand({ usagePlanId: upResp.id! })));
    } finally {
      await safeCleanup(() => client.send(new DeleteRestApiCommand({ restApiId: apiId })));
    }
  }));

  results.push(await runner.runTest('apigateway', 'GetRestApis_Pagination', async () => {
    const pgTs = `${Date.now()}`;
    const pgAPIs: string[] = [];

    try {
      for (const i of [0, 1, 2, 3, 4]) {
        const name = `PagAPI-${pgTs}-${i}`;
        const resp = await client.send(new CreateRestApiCommand({
          name: name,
          description: 'pagination test',
        }));
        if (!resp.id) throw new Error('expected API ID to be defined');
        pgAPIs.push(resp.id);
      }

      const allAPIs: string[] = [];
      let position: string | undefined;
      do {
        const resp = await client.send(new GetRestApisCommand({
          limit: 2,
          position: position,
        }));
        for (const item of resp.items ?? []) {
          if (item.name?.startsWith(`PagAPI-${pgTs}-`)) {
            allAPIs.push(item.name);
          }
        }
        position = resp.position;
      } while (position);

      if (allAPIs.length !== 5) throw new Error(`expected 5 paginated rest apis, got ${allAPIs.length}`);
    } finally {
      for (const id of pgAPIs) {
        await safeCleanup(() => client.send(new DeleteRestApiCommand({ restApiId: id })));
      }
    }
  }));

  return results;
}
