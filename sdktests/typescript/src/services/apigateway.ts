import {
  APIGatewayClient,
  CreateRestApiCommand,
  GetRestApisCommand,
  GetRestApiCommand,
  UpdateRestApiCommand,
  CreateResourceCommand,
  GetResourcesCommand,
  CreateDeploymentCommand,
  GetDeploymentsCommand,
  CreateStageCommand,
  GetStageCommand,
  GetStagesCommand,
  DeleteRestApiCommand,
} from '@aws-sdk/client-api-gateway';
import { NotFoundException } from '@aws-sdk/client-api-gateway';
import { TestRunner } from '../runner.js';
import { TestResult } from '../runner.js';

function makeUniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 999999)}`;
}

export async function runAPIGatewayTests(
  runner: TestRunner,
  apigatewayClient: APIGatewayClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const apiName = makeUniqueName('TestAPI');
  let apiId = '';
  let deploymentId = '';

  // CreateRestApi
  results.push(
    await runner.runTest('apigateway', 'CreateRestApi', async () => {
      const resp = await apigatewayClient.send(
        new CreateRestApiCommand({
          name: apiName,
          description: 'Test API',
        })
      );
      if (resp.id) apiId = resp.id;
    })
  );

  // GetRestApis
  results.push(
    await runner.runTest('apigateway', 'GetRestApis', async () => {
      const resp = await apigatewayClient.send(
        new GetRestApisCommand({ limit: 100 })
      );
      if (!resp.items) throw new Error('items is null');
      for (const item of resp.items) {
        if (item.name === apiName) {
          apiId = item.id || '';
          break;
        }
      }
      if (!apiId) throw new Error('API not found');
    })
  );

  // GetRestApi
  results.push(
    await runner.runTest('apigateway', 'GetRestApi', async () => {
      if (!apiId) throw new Error('API ID not available');
      const resp = await apigatewayClient.send(
        new GetRestApiCommand({ restApiId: apiId })
      );
      if (!resp.name) throw new Error('API name is null');
    })
  );

  // UpdateRestApi
  results.push(
    await runner.runTest('apigateway', 'UpdateRestApi', async () => {
      if (!apiId) throw new Error('API ID not available');
      await apigatewayClient.send(
        new UpdateRestApiCommand({
          restApiId: apiId,
          patchOperations: [
            {
              op: 'replace',
              path: '/description',
              value: 'Updated API',
            },
          ],
        })
      );
    })
  );

  // CreateResource
  results.push(
    await runner.runTest('apigateway', 'CreateResource', async () => {
      if (!apiId) throw new Error('API ID not available');
      const resp = await apigatewayClient.send(
        new CreateResourceCommand({
          restApiId: apiId,
          parentId: apiId,
          pathPart: 'test',
        })
      );
      if (!resp.id) throw new Error('resource ID is null');
    })
  );

  // GetResources
  results.push(
    await runner.runTest('apigateway', 'GetResources', async () => {
      if (!apiId) throw new Error('API ID not available');
      const resp = await apigatewayClient.send(
        new GetResourcesCommand({ restApiId: apiId })
      );
      if (!resp.items) throw new Error('items is null');
    })
  );

  // CreateDeployment
  results.push(
    await runner.runTest('apigateway', 'CreateDeployment', async () => {
      if (!apiId) throw new Error('API ID not available');
      const resp = await apigatewayClient.send(
        new CreateDeploymentCommand({ restApiId: apiId })
      );
      if (resp.id) deploymentId = resp.id;
    })
  );

  // GetDeployments
  results.push(
    await runner.runTest('apigateway', 'GetDeployments', async () => {
      if (!apiId) throw new Error('API ID not available');
      const resp = await apigatewayClient.send(
        new GetDeploymentsCommand({ restApiId: apiId })
      );
      if (!resp.items) throw new Error('items is null');
    })
  );

  // CreateStage
  results.push(
    await runner.runTest('apigateway', 'CreateStage', async () => {
      if (!apiId) throw new Error('API ID not available');
      if (!deploymentId) throw new Error('Deployment ID not available');
      await apigatewayClient.send(
        new CreateStageCommand({
          restApiId: apiId,
          stageName: 'test',
          deploymentId: deploymentId,
        })
      );
    })
  );

  // GetStage
  results.push(
    await runner.runTest('apigateway', 'GetStage', async () => {
      if (!apiId) throw new Error('API ID not available');
      const resp = await apigatewayClient.send(
        new GetStageCommand({
          restApiId: apiId,
          stageName: 'test',
        })
      );
      if (!resp.stageName) throw new Error('stageName is null');
    })
  );

  // GetStages
  results.push(
    await runner.runTest('apigateway', 'GetStages', async () => {
      if (!apiId) throw new Error('API ID not available');
      const resp = await apigatewayClient.send(
        new GetStagesCommand({ restApiId: apiId })
      );
      if (!resp.item) throw new Error('item is null');
    })
  );

  // DeleteRestApi
  results.push(
    await runner.runTest('apigateway', 'DeleteRestApi', async () => {
      if (!apiId) throw new Error('API ID not available');
      await apigatewayClient.send(
        new DeleteRestApiCommand({ restApiId: apiId })
      );
    })
  );

  // Error cases

  // GetRestApi_NonExistent
  results.push(
    await runner.runTest('apigateway', 'GetRestApi_NonExistent', async () => {
      try {
        await apigatewayClient.send(
          new GetRestApiCommand({ restApiId: 'nonexistent_xyz' })
        );
        throw new Error('Expected NotFoundException but got none');
      } catch (err) {
        if (!(err instanceof NotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected NotFoundException, got ${name}`);
        }
      }
    })
  );

  // DeleteRestApi_NonExistent
  results.push(
    await runner.runTest('apigateway', 'DeleteRestApi_NonExistent', async () => {
      try {
        await apigatewayClient.send(
          new DeleteRestApiCommand({ restApiId: 'nonexistent_xyz' })
        );
        throw new Error('Expected NotFoundException but got none');
      } catch (err) {
        if (!(err instanceof NotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected NotFoundException, got ${name}`);
        }
      }
    })
  );

  // GetStage_NonExistent
  const tmpApiName = makeUniqueName('TmpAPI');
  results.push(
    await runner.runTest('apigateway', 'GetStage_NonExistent', async () => {
      let tmpApiId = '';
      try {
        const createResp = await apigatewayClient.send(
          new CreateRestApiCommand({ name: tmpApiName })
        );
        if (createResp.id) tmpApiId = createResp.id;

        await apigatewayClient.send(
          new GetStageCommand({
            restApiId: tmpApiId,
            stageName: 'nonexistent_stage',
          })
        );
        throw new Error('Expected NotFoundException but got none');
      } catch (err) {
        if (!(err instanceof NotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected NotFoundException, got ${name}`);
        }
      } finally {
        if (tmpApiId) {
          try {
            await apigatewayClient.send(new DeleteRestApiCommand({ restApiId: tmpApiId }));
          } catch { /* ignore */ }
        }
      }
    })
  );

  // UpdateRestApi_VerifyUpdate
  const uaApiName = makeUniqueName('UaAPI');
  results.push(
    await runner.runTest('apigateway', 'UpdateRestApi_VerifyUpdate', async () => {
      let uaApiId = '';
      const newDesc = 'updated description v2';
      try {
        const createResp = await apigatewayClient.send(
          new CreateRestApiCommand({
            name: uaApiName,
            description: 'original desc',
          })
        );
        if (createResp.id) uaApiId = createResp.id;

        await apigatewayClient.send(
          new UpdateRestApiCommand({
            restApiId: uaApiId,
            patchOperations: [
              {
                op: 'replace',
                path: '/description',
                value: newDesc,
              },
            ],
          })
        );

        const resp = await apigatewayClient.send(
          new GetRestApiCommand({ restApiId: uaApiId })
        );
        if (resp.description !== newDesc) {
          throw new Error(`description not updated, got ${resp.description}`);
        }
      } finally {
        if (uaApiId) {
          try {
            await apigatewayClient.send(new DeleteRestApiCommand({ restApiId: uaApiId }));
          } catch { /* ignore */ }
        }
      }
    })
  );

  // CreateResource_NestedPath
  const crApiName = makeUniqueName('CrAPI');
  results.push(
    await runner.runTest('apigateway', 'CreateResource_NestedPath', async () => {
      let crApiId = '';
      try {
        const createResp = await apigatewayClient.send(
          new CreateRestApiCommand({ name: crApiName })
        );
        if (createResp.id) crApiId = createResp.id;

        const usersResp = await apigatewayClient.send(
          new CreateResourceCommand({
            restApiId: crApiId,
            parentId: crApiId,
            pathPart: 'users',
          })
        );

        const userIdResp = await apigatewayClient.send(
          new CreateResourceCommand({
            restApiId: crApiId,
            parentId: usersResp.id,
            pathPart: '{userId}',
          })
        );

        if (userIdResp.path !== '/users/{userId}') {
          throw new Error(`nested path mismatch, got ${userIdResp.path}`);
        }

        const resResp = await apigatewayClient.send(
          new GetResourcesCommand({ restApiId: crApiId })
        );

        if ((resResp.items?.length || 0) < 3) {
          throw new Error(`expected at least 3 resources, got ${resResp.items?.length}`);
        }
      } finally {
        if (crApiId) {
          try {
            await apigatewayClient.send(new DeleteRestApiCommand({ restApiId: crApiId }));
          } catch { /* ignore */ }
        }
      }
    })
  );

  // CreateStage_VerifyConfig
  const csApiName = makeUniqueName('CsAPI');
  results.push(
    await runner.runTest('apigateway', 'CreateStage_VerifyConfig', async () => {
      let csApiId = '';
      const stageDesc = 'test stage description';
      let csDeploymentId = '';
      try {
        const createResp = await apigatewayClient.send(
          new CreateRestApiCommand({ name: csApiName })
        );
        if (createResp.id) csApiId = createResp.id;

        const depResp = await apigatewayClient.send(
          new CreateDeploymentCommand({ restApiId: csApiId })
        );
        if (depResp.id) csDeploymentId = depResp.id;

        await apigatewayClient.send(
          new CreateStageCommand({
            restApiId: csApiId,
            stageName: 'v1',
            deploymentId: csDeploymentId,
            description: stageDesc,
          })
        );

        const resp = await apigatewayClient.send(
          new GetStageCommand({
            restApiId: csApiId,
            stageName: 'v1',
          })
        );

        if (resp.description !== stageDesc) {
          throw new Error(`stage description mismatch, got ${resp.description}`);
        }
        if (resp.deploymentId !== csDeploymentId) {
          throw new Error(`deployment ID mismatch, got ${resp.deploymentId}`);
        }
      } finally {
        if (csApiId) {
          try {
            await apigatewayClient.send(new DeleteRestApiCommand({ restApiId: csApiId }));
          } catch { /* ignore */ }
        }
      }
    })
  );

  // GetRestApis_ContainsCreated
  const gaApiName = makeUniqueName('GaAPI');
  results.push(
    await runner.runTest('apigateway', 'GetRestApis_ContainsCreated', async () => {
      let gaApiId = '';
      const gaDesc = 'searchable description';
      try {
        const createResp = await apigatewayClient.send(
          new CreateRestApiCommand({
            name: gaApiName,
            description: gaDesc,
          })
        );
        if (createResp.id) gaApiId = createResp.id;

        const resp = await apigatewayClient.send(
          new GetRestApisCommand({ limit: 500 })
        );

        let found = false;
        for (const item of resp.items || []) {
          if (item.name === gaApiName) {
            found = true;
            if (item.description !== gaDesc) {
              throw new Error(`description mismatch in list, got ${item.description}`);
            }
            break;
          }
        }
        if (!found) throw new Error(`created API ${gaApiName} not found in GetRestApis`);
      } finally {
        if (gaApiId) {
          try {
            await apigatewayClient.send(new DeleteRestApiCommand({ restApiId: gaApiId }));
          } catch { /* ignore */ }
        }
      }
    })
  );

  return results;
}