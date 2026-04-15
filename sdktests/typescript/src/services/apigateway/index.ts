import { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { createApiGatewayTestContext } from './context.js';
import { runRestApiTests, runDeleteRestApiTests } from './rest-api.js';
import { runResourceTests, runDeleteResourceTests } from './resource.js';
import { runMethodTests, runDeleteMethodTests } from './method.js';
import { runIntegrationTests, runDeleteIntegrationTests } from './integration.js';
import { runDeploymentTests, runDeleteDeploymentTests } from './deployment.js';
import { runStageTests, runDeleteStageTests } from './stage.js';
import { runRequestValidatorTests } from './request-validator.js';
import { runModelTests } from './model.js';
import { runAuthorizerTests } from './authorizer.js';
import { runTestInvokeTests } from './test-invoke.js';
import { runApiKeyTests } from './api-key.js';
import { runUsagePlanTests } from './usage-plan.js';
import { runDomainNameTests } from './domain-name.js';
import { runTagTests } from './tags.js';
import { runLifecycleTests } from './lifecycle.js';
import { runErrorTests } from './errors.js';
import { runMultibyteTests } from './multibyte.js';

export function registerAPIGateway(): ServiceRegistration {
  return {
    name: 'apigateway',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const agCtx = createApiGatewayTestContext(ctx.endpoint, ctx.region);
      agCtx.runner = runner;
      const allResults: TestResult[] = [];

      allResults.push(...await runRestApiTests(agCtx));
      allResults.push(...await runResourceTests(agCtx));
      allResults.push(...await runMethodTests(agCtx));
      allResults.push(...await runIntegrationTests(agCtx));
      allResults.push(...await runDeleteIntegrationTests(agCtx));
      allResults.push(...await runDeleteMethodTests(agCtx));
      allResults.push(...await runDeleteResourceTests(agCtx));
      allResults.push(...await runDeploymentTests(agCtx));
      allResults.push(...await runStageTests(agCtx));
      allResults.push(...await runDeleteStageTests(agCtx));
      allResults.push(...await runDeleteDeploymentTests(agCtx));
      allResults.push(...await runRequestValidatorTests(agCtx));
      allResults.push(...await runModelTests(agCtx));
      allResults.push(...await runAuthorizerTests(agCtx));
      allResults.push(...await runTestInvokeTests(agCtx));
      allResults.push(...await runApiKeyTests(agCtx));
      allResults.push(...await runUsagePlanTests(agCtx));
      allResults.push(...await runDomainNameTests(agCtx));
      allResults.push(...await runTagTests(agCtx));
      allResults.push(...await runDeleteRestApiTests(agCtx));

      allResults.push(...await runErrorTests(agCtx));
      allResults.push(...await runLifecycleTests(agCtx));
      allResults.push(...await runMultibyteTests(agCtx.runner, agCtx.client));

      return allResults;
    },
  };
}
