import {
  NeptuneGraphClient,
  CreateGraphCommand,
  GetGraphCommand,
  ListGraphsCommand,
  UpdateGraphCommand,
  StopGraphCommand,
  StartGraphCommand,
  ResetGraphCommand,
  CreateGraphSnapshotCommand,
  GetGraphSnapshotCommand,
  ListGraphSnapshotsCommand,
  CreatePrivateGraphEndpointCommand,
  GetPrivateGraphEndpointCommand,
  ListPrivateGraphEndpointsCommand,
  DeletePrivateGraphEndpointCommand,
} from '@aws-sdk/client-neptune-graph';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName } from '../../helpers.js';
import type { NeptuneGraphState } from './context.js';

export async function runGraphCrudTests(
  runner: TestRunner,
  client: NeptuneGraphClient,
  state: NeptuneGraphState,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const svc = 'neptunegraph';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(svc, name, fn));

  const graphName = makeUniqueName('ng');
  const snapshotName = makeUniqueName('ng-snap');

  await r('CreateGraph', async () => {
    const resp = await client.send(new CreateGraphCommand({
      graphName, provisionedMemory: 128, deletionProtection: false, publicConnectivity: false,
      tags: { Environment: 'test', Owner: 'sdk-test' },
    }));
    if (!resp.id || resp.id === '') throw new Error('expected non-empty graph ID');
    if (!resp.name || resp.name !== graphName) throw new Error(`expected graphName=${graphName}, got ${resp.name}`);
    if (resp.status !== 'AVAILABLE') throw new Error(`expected status AVAILABLE, got ${resp.status}`);
    state.graphID = resp.id;
  });

  await r('GetGraph', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    const resp = await client.send(new GetGraphCommand({ graphIdentifier: state.graphID }));
    if (!resp.id || resp.id !== state.graphID) throw new Error(`expected graphId=${state.graphID}, got ${resp.id}`);
    if (!resp.name || resp.name !== graphName) throw new Error(`expected name=${graphName}, got ${resp.name}`);
    if (!resp.provisionedMemory || resp.provisionedMemory !== 128) throw new Error(`expected provisionedMemory=128, got ${resp.provisionedMemory}`);
    if (!resp.arn) throw new Error('expected non-empty ARN');
    state.graphARN = resp.arn;
  });

  await r('ListGraphs', async () => {
    const resp = await client.send(new ListGraphsCommand({}));
    if (!resp.graphs) throw new Error('expected non-nil Graphs list');
    const found = resp.graphs.some((g) => g.id === state.graphID);
    if (!found) throw new Error('created graph not found in ListGraphs');
  });

  await r('UpdateGraph', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    await client.send(new UpdateGraphCommand({ graphIdentifier: state.graphID, provisionedMemory: 256, deletionProtection: true }));
  });

  await r('UpdateGraph_Verify', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    const resp = await client.send(new GetGraphCommand({ graphIdentifier: state.graphID }));
    if (!resp.provisionedMemory || resp.provisionedMemory !== 256) throw new Error(`expected provisionedMemory=256, got ${resp.provisionedMemory}`);
    if (resp.deletionProtection !== true) throw new Error(`expected deletionProtection=true, got ${resp.deletionProtection}`);
  });

  await r('StopGraph', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    await client.send(new StopGraphCommand({ graphIdentifier: state.graphID }));
  });

  await r('StopGraph_Verify', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    const resp = await client.send(new GetGraphCommand({ graphIdentifier: state.graphID }));
    if (resp.status !== 'STOPPED') throw new Error(`expected status STOPPED, got ${resp.status}`);
  });

  await r('StartGraph', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    await client.send(new StartGraphCommand({ graphIdentifier: state.graphID }));
  });

  await r('StartGraph_Verify', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    const resp = await client.send(new GetGraphCommand({ graphIdentifier: state.graphID }));
    if (resp.status !== 'AVAILABLE') throw new Error(`expected status AVAILABLE, got ${resp.status}`);
  });

  await r('ResetGraph', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    await client.send(new ResetGraphCommand({ graphIdentifier: state.graphID, skipSnapshot: true }));
  });

  await r('CreateGraphSnapshot', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    const resp = await client.send(new CreateGraphSnapshotCommand({
      graphIdentifier: state.graphID, snapshotName, tags: { Type: 'sdk-test' },
    }));
    if (!resp.id || resp.id === '') throw new Error('expected non-empty snapshot ID');
    if (!resp.name || resp.name !== snapshotName) throw new Error(`expected snapshotName=${snapshotName}, got ${resp.name}`);
    state.snapshotID = resp.id;
  });

  await r('GetGraphSnapshot', async () => {
    if (!state.snapshotID) throw new Error('no snapshot ID');
    const resp = await client.send(new GetGraphSnapshotCommand({ snapshotIdentifier: state.snapshotID }));
    if (!resp.id || resp.id !== state.snapshotID) throw new Error(`expected snapshotId=${state.snapshotID}, got ${resp.id}`);
    if (!resp.status) throw new Error('expected non-empty snapshot status');
  });

  await r('ListGraphSnapshots', async () => {
    const resp = await client.send(new ListGraphSnapshotsCommand({}));
    if (!resp.graphSnapshots) throw new Error('expected non-nil GraphSnapshots list');
    const found = resp.graphSnapshots.some((s) => s.id === state.snapshotID);
    if (!found) throw new Error('created snapshot not found');
  });

  await r('ListGraphSnapshots_FilterByGraph', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    const resp = await client.send(new ListGraphSnapshotsCommand({ graphIdentifier: state.graphID }));
    if (!resp.graphSnapshots) throw new Error('expected non-nil GraphSnapshots list');
    const found = resp.graphSnapshots.some((s) => s.id === state.snapshotID);
    if (!found) throw new Error('snapshot not found when filtering by graph');
  });

  await r('CreatePrivateGraphEndpoint', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    const resp = await client.send(new CreatePrivateGraphEndpointCommand({
      graphIdentifier: state.graphID, vpcId: 'vpc-test123', subnetIds: ['subnet-aaa111', 'subnet-bbb222'],
    }));
    if (!resp.vpcId || resp.vpcId !== 'vpc-test123') throw new Error(`expected vpcId=vpc-test123, got ${resp.vpcId}`);
    if (!resp.status) throw new Error('expected non-nil endpoint status');
  });

  await r('GetPrivateGraphEndpoint', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    const resp = await client.send(new GetPrivateGraphEndpointCommand({ graphIdentifier: state.graphID, vpcId: 'vpc-test123' }));
    if (!resp.vpcId || resp.vpcId !== 'vpc-test123') throw new Error(`expected vpcId=vpc-test123, got ${resp.vpcId}`);
  });

  await r('ListPrivateGraphEndpoints', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    const resp = await client.send(new ListPrivateGraphEndpointsCommand({ graphIdentifier: state.graphID }));
    if (!resp.privateGraphEndpoints) throw new Error('expected non-nil PrivateGraphEndpoints list');
    if (resp.privateGraphEndpoints.length === 0) throw new Error('expected at least one private endpoint');
  });

  await r('DeletePrivateGraphEndpoint', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    await client.send(new DeletePrivateGraphEndpointCommand({ graphIdentifier: state.graphID, vpcId: 'vpc-test123' }));
  });

  return results;
}
