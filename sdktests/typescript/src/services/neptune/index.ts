import {
  NeptuneClient,
  DeleteDBInstanceCommand,
  DeleteDBClusterSnapshotCommand,
  DeleteDBClusterCommand,
  DeleteDBClusterEndpointCommand,
  DeleteDBClusterParameterGroupCommand,
  DeleteDBSubnetGroupCommand,
  DeleteGlobalClusterCommand,
  DeleteEventSubscriptionCommand,
} from '@aws-sdk/client-neptune';
import { ServiceRegistration, ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';
import { NeptuneState } from './context.js';
import { runClusterLifecycleTests } from './cluster-lifecycle.js';
import { runAdvancedAndCleanupTests } from './advanced-and-cleanup.js';

export function registerNeptune(): ServiceRegistration {
  return {
    name: 'neptune',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext): Promise<TestResult[]> => {
      const client = new NeptuneClient({
        endpoint: ctx.endpoint,
        region: ctx.region,
        credentials: ctx.credentials,
      });
      const results: TestResult[] = [];

      const state: NeptuneState = {
        region: ctx.region,
        subnetGroupName: makeUniqueName('nep-subnet'),
        clusterParamGroupName: makeUniqueName('nep-cpg'),
        clusterName: makeUniqueName('nep-cluster'),
        snapshotName: makeUniqueName('nep-snap'),
        instanceName: makeUniqueName('nep-instance'),
        globalClusterId: makeUniqueName('nep-global'),
        endpointName: makeUniqueName('nep-endpoint'),
        clusterArn: '',
        instanceArn: '',
        subnetCreated: false,
        cpgCreated: false,
        clusterCreated: false,
        instanceCreated: false,
        snapshotCreated: false,
        subName: '',
      };

      try {
        await runClusterLifecycleTests(client, runner, results, state);
        await runAdvancedAndCleanupTests(client, runner, results, state);
      } finally {
        await safeCleanup(() => client.send(new DeleteDBInstanceCommand({
          DBInstanceIdentifier: state.instanceName,
          SkipFinalSnapshot: true,
        })));
        await safeCleanup(() => client.send(new DeleteDBClusterSnapshotCommand({
          DBClusterSnapshotIdentifier: state.snapshotName,
        })));
        await safeCleanup(() => client.send(new DeleteDBClusterCommand({
          DBClusterIdentifier: state.clusterName,
          SkipFinalSnapshot: true,
        })));
        await safeCleanup(() => client.send(new DeleteDBClusterEndpointCommand({
          DBClusterEndpointIdentifier: state.endpointName,
        })));
        await safeCleanup(() => client.send(new DeleteDBClusterParameterGroupCommand({
          DBClusterParameterGroupName: state.clusterParamGroupName,
        })));
        await safeCleanup(() => client.send(new DeleteDBSubnetGroupCommand({
          DBSubnetGroupName: state.subnetGroupName,
        })));
        await safeCleanup(() => client.send(new DeleteGlobalClusterCommand({
          GlobalClusterIdentifier: state.globalClusterId,
        })));
        await safeCleanup(() => client.send(new DeleteEventSubscriptionCommand({
          SubscriptionName: state.subName,
        })));
      }

      return results;
    },
  };
}
