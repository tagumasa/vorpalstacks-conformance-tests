import {
  Route53Client,
  CreateHostedZoneCommand,
  ListHostedZonesCommand,
  GetHostedZoneCommand,
  ListResourceRecordSetsCommand,
  ChangeResourceRecordSetsCommand,
  GetChangeCommand,
  GetDNSSECCommand,
  DeleteHostedZoneCommand,
  ListReusableDelegationSetsCommand,
  ListHostedZonesByNameCommand,
  UpdateHostedZoneCommentCommand,
} from '@aws-sdk/client-route-53';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup, assertErrorContains } from '../../helpers.js';

export async function runHostedZoneTests(
  runner: TestRunner,
  client: Route53Client,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const domainName = makeUniqueName('example') + '.com.';
  let hostedZoneID = '';

  results.push(await runner.runTest('route53', 'CreateHostedZone', async () => {
    const resp = await client.send(new CreateHostedZoneCommand({
      Name: domainName, CallerReference: makeUniqueName('ref-'),
    }));
    if (!resp.HostedZone) throw new Error('expected HostedZone to be defined');
    hostedZoneID = resp.HostedZone.Id!;
  }));

  results.push(await runner.runTest('route53', 'ListHostedZones', async () => {
    const resp = await client.send(new ListHostedZonesCommand({ MaxItems: 10 }));
    if (!resp.HostedZones) throw new Error('expected HostedZones to be defined');
  }));

  if (hostedZoneID) {
    results.push(await runner.runTest('route53', 'GetHostedZone', async () => {
      const resp = await client.send(new GetHostedZoneCommand({ Id: hostedZoneID }));
      if (!resp.HostedZone) throw new Error('expected HostedZone to be defined');
    }));

    results.push(await runner.runTest('route53', 'ListResourceRecordSets', async () => {
      const resp = await client.send(new ListResourceRecordSetsCommand({ HostedZoneId: hostedZoneID, MaxItems: 10 }));
      if (!resp.ResourceRecordSets) throw new Error('expected ResourceRecordSets to be defined');
    }));

    let changeID = '';
    results.push(await runner.runTest('route53', 'ChangeResourceRecordSets', async () => {
      const resp = await client.send(new ChangeResourceRecordSetsCommand({
        HostedZoneId: hostedZoneID,
        ChangeBatch: { Changes: [{ Action: 'CREATE', ResourceRecordSet: { Name: `test.${domainName}`, Type: 'A', TTL: 300, ResourceRecords: [{ Value: '192.0.2.1' }] } }] },
      }));
      if (!resp.ChangeInfo) throw new Error('expected ChangeInfo to be defined');
      changeID = resp.ChangeInfo.Id!;
    }));

    if (changeID) {
      results.push(await runner.runTest('route53', 'GetChange', async () => {
        const resp = await client.send(new GetChangeCommand({ Id: changeID }));
        if (!resp.ChangeInfo) throw new Error('expected ChangeInfo to be defined');
      }));
    }

    results.push(await runner.runTest('route53', 'DeleteResourceRecord', async () => {
      await client.send(new ChangeResourceRecordSetsCommand({
        HostedZoneId: hostedZoneID,
        ChangeBatch: { Changes: [{ Action: 'DELETE', ResourceRecordSet: { Name: `test.${domainName}`, Type: 'A', TTL: 300, ResourceRecords: [{ Value: '192.0.2.1' }] } }] },
      }));
    }));

    results.push(await runner.runTest('route53', 'GetDNSSEC', async () => {
      await client.send(new GetDNSSECCommand({ HostedZoneId: hostedZoneID }));
    }));

    results.push(await runner.runTest('route53', 'DeleteHostedZone', async () => {
      await client.send(new DeleteHostedZoneCommand({ Id: hostedZoneID }));
    }));
  }

  results.push(await runner.runTest('route53', 'ListReusableDelegationSets', async () => {
    const resp = await client.send(new ListReusableDelegationSetsCommand({ MaxItems: 10 }));
    if (resp.DelegationSets?.length) throw new Error(`expected no delegation sets, got ${resp.DelegationSets.length}`);
  }));

  results.push(await runner.runTest('route53', 'GetHostedZone_NonExistent', async () => {
    try {
      await client.send(new GetHostedZoneCommand({ Id: 'Z00000000000000000000' }));
      throw new Error('expected error');
    } catch (err) { assertErrorContains(err, 'NoSuchHostedZone'); }
  }));

  results.push(await runner.runTest('route53', 'DeleteHostedZone_NonExistent', async () => {
    try {
      await client.send(new DeleteHostedZoneCommand({ Id: 'Z00000000000000000000' }));
      throw new Error('expected error');
    } catch (err) { assertErrorContains(err, 'NoSuchHostedZone'); }
  }));

  results.push(await runner.runTest('route53', 'GetChange_NonExistent', async () => {
    try {
      await client.send(new GetChangeCommand({ Id: 'C0000000000000000000000000' }));
      throw new Error('expected error');
    } catch (err) { assertErrorContains(err, 'NoSuchChange'); }
  }));

  results.push(await runner.runTest('route53', 'CreateHostedZone_ContentVerify', async () => {
    const verifyDomain = makeUniqueName('verify') + '.com.';
    const resp = await client.send(new CreateHostedZoneCommand({ Name: verifyDomain, CallerReference: makeUniqueName('ref-') }));
    const hzID = resp.HostedZone?.Id;
    if (!hzID) throw new Error('expected HostedZone.Id');
    try {
      if (resp.HostedZone?.Name !== verifyDomain) throw new Error('domain name mismatch');
      const getResp = await client.send(new GetHostedZoneCommand({ Id: hzID }));
      if (getResp.HostedZone?.Name !== verifyDomain) throw new Error('get domain name mismatch');
    } finally { await safeCleanup(() => client.send(new DeleteHostedZoneCommand({ Id: hzID }))); }
  }));

  results.push(await runner.runTest('route53', 'ListHostedZonesByName', async () => {
    const resp = await client.send(new ListHostedZonesByNameCommand({ MaxItems: 10 }));
    if (!resp.HostedZones) throw new Error('expected HostedZones to be defined');
  }));

  results.push(await runner.runTest('route53', 'ListHostedZonesByName_WithDNSName', async () => {
    const testDomain = makeUniqueName('sorttest') + '.com.';
    const hzResp = await client.send(new CreateHostedZoneCommand({ Name: testDomain, CallerReference: makeUniqueName('sortref-') }));
    const hzID = hzResp.HostedZone?.Id;
    if (!hzID) throw new Error('expected HostedZone.Id');
    try {
      const resp = await client.send(new ListHostedZonesByNameCommand({ DNSName: testDomain, MaxItems: 10 }));
      if (!resp.HostedZones?.some(hz => hz.Name === testDomain)) throw new Error(`zone ${testDomain} not found`);
    } finally { await safeCleanup(() => client.send(new DeleteHostedZoneCommand({ Id: hzID }))); }
  }));

  results.push(await runner.runTest('route53', 'UpdateHostedZoneComment', async () => {
    const ucDomain = makeUniqueName('updatecomment') + '.com.';
    const createResp = await client.send(new CreateHostedZoneCommand({ Name: ucDomain, CallerReference: makeUniqueName('ucref-') }));
    const ucID = createResp.HostedZone?.Id;
    if (!ucID) throw new Error('expected HostedZone.Id');
    try {
      await client.send(new UpdateHostedZoneCommentCommand({ Id: ucID, Comment: 'test comment for zone' }));
      const getResp = await client.send(new GetHostedZoneCommand({ Id: ucID }));
      if (getResp.HostedZone?.Config?.Comment !== 'test comment for zone') throw new Error('comment mismatch');
    } finally { await safeCleanup(() => client.send(new DeleteHostedZoneCommand({ Id: ucID }))); }
  }));

  results.push(await runner.runTest('route53', 'DelegationSet_Persisted', async () => {
    const dsDomain = makeUniqueName('ds-persist') + '.com.';
    const createResp = await client.send(new CreateHostedZoneCommand({ Name: dsDomain, CallerReference: makeUniqueName('dspersist-') }));
    const dsID = createResp.HostedZone?.Id;
    if (!dsID) throw new Error('expected HostedZone.Id');
    try {
      const createDSID = createResp.DelegationSet?.Id;
      if (!createDSID) throw new Error('delegation set ID missing in create');
      const getResp = await client.send(new GetHostedZoneCommand({ Id: dsID }));
      const getDSID = getResp.DelegationSet?.Id;
      if (!getDSID) throw new Error('delegation set ID missing in get');
      if (createDSID !== getDSID) throw new Error('delegation set ID mismatch');
    } finally { await safeCleanup(() => client.send(new DeleteHostedZoneCommand({ Id: dsID }))); }
  }));

  results.push(await runner.runTest('route53', 'CreateHostedZone_InvalidName', async () => {
    try {
      await client.send(new CreateHostedZoneCommand({ Name: 'invalid name with spaces', CallerReference: makeUniqueName('badref-') }));
      throw new Error('expected error for invalid zone name');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for invalid zone name') throw err;
    }
  }));

  results.push(await runner.runTest('route53', 'DeleteHostedZone_NotEmpty', async () => {
    const neDomain = makeUniqueName('notempty') + '.com.';
    const createResp = await client.send(new CreateHostedZoneCommand({ Name: neDomain, CallerReference: makeUniqueName('neref-') }));
    const neID = createResp.HostedZone?.Id;
    if (!neID) throw new Error('expected HostedZone.Id');
    try {
      await client.send(new ChangeResourceRecordSetsCommand({
        HostedZoneId: neID,
        ChangeBatch: { Changes: [{ Action: 'CREATE', ResourceRecordSet: { Name: `keep.${neDomain}`, Type: 'A', TTL: 300, ResourceRecords: [{ Value: '10.0.0.1' }] } }] },
      }));
      try {
        await client.send(new DeleteHostedZoneCommand({ Id: neID }));
        throw new Error('expected error when deleting non-empty zone');
      } catch (err) {
        if (err instanceof Error && err.message === 'expected error when deleting non-empty zone') throw err;
      }
      await client.send(new ChangeResourceRecordSetsCommand({
        HostedZoneId: neID,
        ChangeBatch: { Changes: [{ Action: 'DELETE', ResourceRecordSet: { Name: `keep.${neDomain}`, Type: 'A', TTL: 300, ResourceRecords: [{ Value: '10.0.0.1' }] } }] },
      }));
    } finally { await safeCleanup(() => client.send(new DeleteHostedZoneCommand({ Id: neID }))); }
  }));

  return results;
}
