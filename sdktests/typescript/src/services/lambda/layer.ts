import {
  PublishLayerVersionCommand,
  GetLayerVersionCommand,
  ListLayersCommand,
  ListLayerVersionsCommand,
  DeleteLayerVersionCommand,
} from '@aws-sdk/client-lambda';
import { LambdaTestContext } from './context.js';

export async function runLayerTests(ctx: LambdaTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, ts } = ctx;
  const results: import('../../runner.js').TestResult[] = [];
  const layerName = `TestLayer-${ts}`;
  const layerZipContent = new TextEncoder().encode('exports.handler = async (event) => { return 1; };');

  results.push(await runner.runTest('lambda', 'PublishLayerVersion', async () => {
    const resp = await client.send(new PublishLayerVersionCommand({
      LayerName: layerName,
      Content: { ZipFile: layerZipContent },
      Description: 'Test layer version',
      CompatibleRuntimes: ['nodejs22.x'],
    }));
    if (!resp.LayerArn) throw new Error('LayerArn to be defined');
    if (resp.Version !== 1) throw new Error(`expected version 1, got ${resp.Version}`);
    if (!resp.Content || !resp.Content.CodeSha256) throw new Error('CodeSha256 to be defined');
  }));

  results.push(await runner.runTest('lambda', 'GetLayerVersion', async () => {
    const resp = await client.send(new GetLayerVersionCommand({
      LayerName: layerName,
      VersionNumber: 1,
    }));
    if (!resp.Content || !resp.Content.CodeSha256) throw new Error('CodeSha256 to be defined');
    if (resp.Version !== 1) throw new Error(`expected version 1, got ${resp.Version}`);
  }));

  results.push(await runner.runTest('lambda', 'ListLayers', async () => {
    const resp = await client.send(new ListLayersCommand({}));
    if (!resp.Layers) throw new Error('layers list to be defined');
    const found = resp.Layers.some((l) => l.LayerName === layerName);
    if (!found) throw new Error(`layer ${layerName} not found in ListLayers`);
  }));

  results.push(await runner.runTest('lambda', 'ListLayerVersions', async () => {
    const resp = await client.send(new ListLayerVersionsCommand({ LayerName: layerName }));
    if (!resp.LayerVersions) throw new Error('layer versions list to be defined');
    if (resp.LayerVersions.length === 0) throw new Error('expected at least 1 layer version');
  }));

  results.push(await runner.runTest('lambda', 'DeleteLayerVersion', async () => {
    await client.send(new DeleteLayerVersionCommand({
      LayerName: layerName,
      VersionNumber: 1,
    }));
  }));

  return results;
}
