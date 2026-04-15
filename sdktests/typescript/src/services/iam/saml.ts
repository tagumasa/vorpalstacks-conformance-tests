import {
  CreateSAMLProviderCommand,
  GetSAMLProviderCommand,
  ListSAMLProvidersCommand,
  UpdateSAMLProviderCommand,
  TagSAMLProviderCommand,
  ListSAMLProviderTagsCommand,
  UntagSAMLProviderCommand,
  DeleteSAMLProviderCommand,
} from '@aws-sdk/client-iam';
import { IAMTestContext } from './context.js';

const SAML_METADATA = `<?xml version="1.0" encoding="UTF-8"?>
<md:EntityDescriptor xmlns:md="urn:oasis:names:tc:SAML:2.0:metadata" entityID="https://idp.example.com">
  <md:IDPSSODescriptor protocolSupportEnumeration="urn:oasis:names:tc:SAML:2.0:protocol">
    <md:SingleSignOnService Binding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect" Location="https://idp.example.com/sso"/>
  </md:IDPSSODescriptor>
</md:EntityDescriptor>`;

export async function runSAMLTests(ctx: IAMTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client } = ctx;
  const results: import('../../runner.js').TestResult[] = [];

  results.push(await runner.runTest('iam', 'CreateSAMLProvider', async () => {
    const resp = await client.send(new CreateSAMLProviderCommand({
      Name: ctx.samlProviderName,
      SAMLMetadataDocument: SAML_METADATA,
      Tags: [{ Key: 'Source', Value: 'test' }],
    }));
    if (!resp.SAMLProviderArn) throw new Error('saml provider arn to be defined');
    ctx.samlProviderArn = resp.SAMLProviderArn;
  }));

  results.push(await runner.runTest('iam', 'GetSAMLProvider', async () => {
    const resp = await client.send(new GetSAMLProviderCommand({ SAMLProviderArn: ctx.samlProviderArn }));
    if (!resp.SAMLMetadataDocument || resp.SAMLMetadataDocument === '') {
      throw new Error('saml metadata document is empty');
    }
  }));

  results.push(await runner.runTest('iam', 'ListSAMLProviders', async () => {
    const resp = await client.send(new ListSAMLProvidersCommand({}));
    if (!resp.SAMLProviderList) throw new Error('saml provider list to be defined');
  }));

  results.push(await runner.runTest('iam', 'UpdateSAMLProvider', async () => {
    const resp = await client.send(new UpdateSAMLProviderCommand({
      SAMLProviderArn: ctx.samlProviderArn,
      SAMLMetadataDocument: SAML_METADATA,
    }));
    if (!resp.SAMLProviderArn) throw new Error('saml provider arn to be defined');
  }));

  results.push(await runner.runTest('iam', 'TagSAMLProvider', async () => {
    await client.send(new TagSAMLProviderCommand({
      SAMLProviderArn: ctx.samlProviderArn,
      Tags: [{ Key: 'Environment', Value: 'test' }],
    }));
  }));

  results.push(await runner.runTest('iam', 'ListSAMLProviderTags', async () => {
    const resp = await client.send(new ListSAMLProviderTagsCommand({ SAMLProviderArn: ctx.samlProviderArn }));
    if (!resp.Tags) throw new Error('tags to be defined');
  }));

  results.push(await runner.runTest('iam', 'UntagSAMLProvider', async () => {
    await client.send(new UntagSAMLProviderCommand({
      SAMLProviderArn: ctx.samlProviderArn,
      TagKeys: ['Environment'],
    }));
  }));

  results.push(await runner.runTest('iam', 'DeleteSAMLProvider', async () => {
    await client.send(new DeleteSAMLProviderCommand({ SAMLProviderArn: ctx.samlProviderArn }));
  }));

  return results;
}
