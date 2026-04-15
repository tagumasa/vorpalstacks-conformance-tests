import { WAFV2Client } from '@aws-sdk/client-wafv2';
import type { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName } from '../../helpers.js';
import type { WebACLState } from './context.js';
import { runIPSetTests } from './ip-set.js';
import { runRegexPatternSetTests } from './regex-pattern-set.js';
import { runRuleGroupTests } from './rule-group.js';
import { runWebACLTests } from './web-acl.js';
import { runEdgeCaseTests } from './edge-cases.js';

export function registerWAF(): ServiceRegistration {
  return {
    name: 'wafv2',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new WAFV2Client({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });
      const results: TestResult[] = [];

      results.push(...await runIPSetTests(runner, client));

      results.push(...await runRegexPatternSetTests(runner, client));

      results.push(...await runRuleGroupTests(runner, client));

      const aclState: WebACLState = {
        name: makeUniqueName('webacl'),
        id: '',
        arn: '',
        lockToken: '',
      };
      results.push(...await runWebACLTests(runner, client, aclState));

      results.push(...await runEdgeCaseTests(runner, client));

      return results;
    },
  };
}
