import {
  Route53Client,
  CreateHostedZoneCommand,
  GetHostedZoneCommand,
  ChangeResourceRecordSetsCommand,
  ListResourceRecordSetsCommand,
  AssociateVPCWithHostedZoneCommand,
  DisassociateVPCFromHostedZoneCommand,
  ChangeTagsForResourceCommand,
  ListTagsForResourceCommand,
  CreateHealthCheckCommand,
  DeleteHealthCheckCommand,
  DeleteHostedZoneCommand,
  ListHostedZonesCommand,
} from '@aws-sdk/client-route-53';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runAdvancedTests(
  runner: TestRunner,
  client: Route53Client,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('route53', 'AssociateVPCWithHostedZone', async () => {
    const privateDomain = makeUniqueName('private') + '.com.';
    const createResp = await client.send(new CreateHostedZoneCommand({
      Name: privateDomain,
      CallerReference: makeUniqueName('privref-'),
      HostedZoneConfig: { PrivateZone: true, Comment: 'private zone for VPC test' },
      VPC: { VPCId: 'vpc-abcdef01', VPCRegion: 'us-east-1' },
    }));
    const pvtID = createResp.HostedZone?.Id;
    if (!pvtID) throw new Error('expected HostedZone.Id');
    try {
      await client.send(new AssociateVPCWithHostedZoneCommand({
        HostedZoneId: pvtID,
        VPC: { VPCId: 'vpc-xyz12345', VPCRegion: 'us-east-1' },
      }));
      const getResp = await client.send(new GetHostedZoneCommand({ Id: pvtID }));
      if ((getResp.VPCs?.length ?? 0) < 2) {
        throw new Error(`expected at least 2 VPCs, got ${getResp.VPCs?.length}`);
      }
      await client.send(new DisassociateVPCFromHostedZoneCommand({
        HostedZoneId: pvtID,
        VPC: { VPCId: 'vpc-xyz12345', VPCRegion: 'us-east-1' },
      }));
      await client.send(new DisassociateVPCFromHostedZoneCommand({
        HostedZoneId: pvtID,
        VPC: { VPCId: 'vpc-abcdef01', VPCRegion: 'us-east-1' },
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteHostedZoneCommand({ Id: pvtID })));
    }
  }));

  results.push(await runner.runTest('route53', 'DisassociateVPCFromHostedZone', async () => {
    const dsDomain = makeUniqueName('disassoc') + '.com.';
    const createResp = await client.send(new CreateHostedZoneCommand({
      Name: dsDomain,
      CallerReference: makeUniqueName('dsref-'),
      HostedZoneConfig: { PrivateZone: true },
      VPC: { VPCId: 'vpc-disassoc1', VPCRegion: 'us-east-1' },
    }));
    const dsID = createResp.HostedZone?.Id;
    if (!dsID) throw new Error('expected HostedZone.Id');
    try {
      await client.send(new AssociateVPCWithHostedZoneCommand({
        HostedZoneId: dsID,
        VPC: { VPCId: 'vpc-disassoc2', VPCRegion: 'us-east-1' },
      }));
      await client.send(new DisassociateVPCFromHostedZoneCommand({
        HostedZoneId: dsID,
        VPC: { VPCId: 'vpc-disassoc2', VPCRegion: 'us-east-1' },
      }));
      const getResp = await client.send(new GetHostedZoneCommand({ Id: dsID }));
      if (getResp.VPCs?.length !== 1) {
        throw new Error(`expected 1 VPC after disassociation, got ${getResp.VPCs?.length}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteHostedZoneCommand({ Id: dsID })));
    }
  }));

  results.push(await runner.runTest('route53', 'ChangeTagsForResource', async () => {
    const tagDomain = makeUniqueName('tags') + '.com.';
    const createResp = await client.send(new CreateHostedZoneCommand({
      Name: tagDomain,
      CallerReference: makeUniqueName('tagref-'),
    }));
    const tagID = createResp.HostedZone?.Id;
    if (!tagID) throw new Error('expected HostedZone.Id');
    try {
      await client.send(new ChangeTagsForResourceCommand({
        ResourceType: 'hostedzone',
        ResourceId: tagID,
        AddTags: [{ Key: 'Environment', Value: 'test' }, { Key: 'Owner', Value: 'team-a' }],
      }));
      const listResp = await client.send(new ListTagsForResourceCommand({
        ResourceType: 'hostedzone',
        ResourceId: tagID,
      }));
      if (listResp.ResourceTagSet?.Tags?.length !== 2) {
        throw new Error(`expected 2 tags after add, got ${listResp.ResourceTagSet?.Tags?.length}`);
      }
      await client.send(new ChangeTagsForResourceCommand({
        ResourceType: 'hostedzone',
        ResourceId: tagID,
        RemoveTagKeys: ['Owner'],
      }));
      const listResp2 = await client.send(new ListTagsForResourceCommand({
        ResourceType: 'hostedzone',
        ResourceId: tagID,
      }));
      const remaining = listResp2.ResourceTagSet?.Tags ?? [];
      if (remaining.length !== 1 || remaining[0].Key !== 'Environment') {
        throw new Error('expected 1 Environment tag after removal');
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteHostedZoneCommand({ Id: tagID })));
    }
  }));

  results.push(await runner.runTest('route53', 'ListTagsForResource_HealthCheck', async () => {
    const hcResp = await client.send(new CreateHealthCheckCommand({
      CallerReference: makeUniqueName('hctagref-'),
      HealthCheckConfig: { Type: 'TCP', FullyQualifiedDomainName: 'hctag.example.com' },
    }));
    const hcID = hcResp.HealthCheck?.Id;
    if (!hcID) throw new Error('expected HealthCheck.Id');
    try {
      await client.send(new ChangeTagsForResourceCommand({
        ResourceType: 'healthcheck',
        ResourceId: hcID,
        AddTags: [{ Key: 'Monitor', Value: 'enabled' }],
      }));
      const listResp = await client.send(new ListTagsForResourceCommand({
        ResourceType: 'healthcheck',
        ResourceId: hcID,
      }));
      if (listResp.ResourceTagSet?.Tags?.length !== 1) {
        throw new Error(`expected 1 tag, got ${listResp.ResourceTagSet?.Tags?.length}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteHealthCheckCommand({ HealthCheckId: hcID })));
    }
  }));

  results.push(await runner.runTest('route53', 'ChangeResourceRecordSets_Upsert', async () => {
    const upsertDomain = makeUniqueName('upsert') + '.com.';
    const createResp = await client.send(new CreateHostedZoneCommand({
      Name: upsertDomain,
      CallerReference: makeUniqueName('upsertref-'),
    }));
    const upID = createResp.HostedZone?.Id;
    if (!upID) throw new Error('expected HostedZone.Id');
    try {
      const recordName = `upsert.${upsertDomain}`;
      await client.send(new ChangeResourceRecordSetsCommand({
        HostedZoneId: upID,
        ChangeBatch: {
          Changes: [{
            Action: 'UPSERT',
            ResourceRecordSet: { Name: recordName, Type: 'A', TTL: 300, ResourceRecords: [{ Value: '10.0.0.1' }] },
          }],
        },
      }));
      await client.send(new ChangeResourceRecordSetsCommand({
        HostedZoneId: upID,
        ChangeBatch: {
          Changes: [{
            Action: 'UPSERT',
            ResourceRecordSet: { Name: recordName, Type: 'A', TTL: 600, ResourceRecords: [{ Value: '10.0.0.2' }] },
          }],
        },
      }));
      const listResp = await client.send(new ListResourceRecordSetsCommand({ HostedZoneId: upID }));
      const record = listResp.ResourceRecordSets?.find(rs => rs.Name === recordName && rs.Type === 'A');
      if (!record) throw new Error('upserted record not found');
      if (record.TTL !== 600) throw new Error(`TTL mismatch after upsert: got ${record.TTL}, want 600`);
      if (record.ResourceRecords?.[0]?.Value !== '10.0.0.2') {
        throw new Error(`value mismatch after upsert: got ${record.ResourceRecords?.[0]?.Value}`);
      }
      await client.send(new ChangeResourceRecordSetsCommand({
        HostedZoneId: upID,
        ChangeBatch: {
          Changes: [{
            Action: 'DELETE',
            ResourceRecordSet: { Name: recordName, Type: 'A', TTL: 600, ResourceRecords: [{ Value: '10.0.0.2' }] },
          }],
        },
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteHostedZoneCommand({ Id: upID })));
    }
  }));

  results.push(await runner.runTest('route53', 'CreateHostedZone_PrivateWithComment', async () => {
    const pvtDomain = makeUniqueName('private-comment') + '.com.';
    const resp = await client.send(new CreateHostedZoneCommand({
      Name: pvtDomain,
      CallerReference: makeUniqueName('pvtref-'),
      HostedZoneConfig: { PrivateZone: true, Comment: 'private zone with comment' },
      VPC: { VPCId: 'vpc-pvttest', VPCRegion: 'eu-west-1' },
    }));
    const pvtID = resp.HostedZone?.Id;
    if (!pvtID) throw new Error('expected HostedZone.Id');
    try {
      const getResp = await client.send(new GetHostedZoneCommand({ Id: pvtID }));
      const config = getResp.HostedZone?.Config;
      if (!config) throw new Error('expected Config to be defined');
      if (config.PrivateZone !== true) throw new Error('expected PrivateZone=true');
      if (config.Comment !== 'private zone with comment') {
        throw new Error(`comment mismatch: got ${config.Comment}`);
      }
      if (getResp.VPCs?.length !== 1) throw new Error(`expected 1 VPC, got ${getResp.VPCs?.length}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteHostedZoneCommand({ Id: pvtID })));
    }
  }));

  results.push(await runner.runTest('route53', 'AssociateVPCWithHostedZone_PublicZone', async () => {
    const pubDomain = makeUniqueName('pub-vpc-test') + '.com.';
    const createResp = await client.send(new CreateHostedZoneCommand({
      Name: pubDomain,
      CallerReference: makeUniqueName('pubvpc-'),
    }));
    const pubID = createResp.HostedZone?.Id;
    if (!pubID) throw new Error('expected HostedZone.Id');
    try {
      try {
        await client.send(new AssociateVPCWithHostedZoneCommand({
          HostedZoneId: pubID,
          VPC: { VPCId: 'vpc-test123', VPCRegion: 'us-east-1' },
        }));
        throw new Error('expected error when associating VPC with public zone');
      } catch (err) {
        if (err instanceof Error && err.message === 'expected error when associating VPC with public zone') throw err;
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteHostedZoneCommand({ Id: pubID })));
    }
  }));

  results.push(await runner.runTest('route53', 'ChangeResourceRecordSets_MultipleTypes', async () => {
    const mtDomain = makeUniqueName('multitype') + '.com.';
    const createResp = await client.send(new CreateHostedZoneCommand({
      Name: mtDomain,
      CallerReference: makeUniqueName('mtref-'),
    }));
    const mtID = createResp.HostedZone?.Id;
    if (!mtID) throw new Error('expected HostedZone.Id');
    try {
      await client.send(new ChangeResourceRecordSetsCommand({
        HostedZoneId: mtID,
        ChangeBatch: {
          Changes: [
            { Action: 'CREATE', ResourceRecordSet: { Name: `www.${mtDomain}`, Type: 'CNAME', TTL: 300, ResourceRecords: [{ Value: 'target.example.com' }] } },
            { Action: 'CREATE', ResourceRecordSet: { Name: `txt.${mtDomain}`, Type: 'TXT', TTL: 300, ResourceRecords: [{ Value: 'v=spf1 include:example.com ~all' }] } },
          ],
        },
      }));
      const listResp = await client.send(new ListResourceRecordSetsCommand({ HostedZoneId: mtID }));
      const foundCNAME = listResp.ResourceRecordSets?.some(rs => rs.Type === 'CNAME' && rs.Name?.endsWith(mtDomain));
      const foundTXT = listResp.ResourceRecordSets?.some(rs => rs.Type === 'TXT' && rs.Name?.endsWith(mtDomain));
      if (!foundCNAME) throw new Error('CNAME record not found');
      if (!foundTXT) throw new Error('TXT record not found');
      await client.send(new ChangeResourceRecordSetsCommand({
        HostedZoneId: mtID,
        ChangeBatch: {
          Changes: [
            { Action: 'DELETE', ResourceRecordSet: { Name: `www.${mtDomain}`, Type: 'CNAME', TTL: 300, ResourceRecords: [{ Value: 'target.example.com' }] } },
            { Action: 'DELETE', ResourceRecordSet: { Name: `txt.${mtDomain}`, Type: 'TXT', TTL: 300, ResourceRecords: [{ Value: 'v=spf1 include:example.com ~all' }] } },
          ],
        },
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteHostedZoneCommand({ Id: mtID })));
    }
  }));

  results.push(await runner.runTest('route53', 'ListHostedZones_Pagination', async () => {
    const pgTs = makeUniqueName('pzpg');
    const pgZoneIDs: string[] = [];
    try {
      for (const suffix of ['0', '1', '2', '3', '4']) {
        const resp = await client.send(new CreateHostedZoneCommand({
          Name: `${pgTs}-${suffix}.example.com.`,
          CallerReference: `${pgTs}-ref-${suffix}`,
        }));
        const id = resp.HostedZone?.Id;
        if (!id) throw new Error('expected HostedZone.Id to be defined');
        pgZoneIDs.push(id);
      }
      let pageCount = 0;
      let totalCount = 0;
      let marker: string | undefined;
      do {
        const resp = await client.send(new ListHostedZonesCommand({ Marker: marker, MaxItems: 2 }));
        pageCount++;
        totalCount += resp.HostedZones?.length ?? 0;
        marker = resp.IsTruncated === true ? resp.NextMarker : undefined;
      } while (marker);
      if (pageCount < 2) throw new Error(`expected at least 2 pages, got ${pageCount} (total zones: ${totalCount})`);
      if (totalCount < 5) throw new Error(`expected at least 5 zones total across pages, got ${totalCount}`);
    } finally {
      for (const zid of pgZoneIDs) {
        await safeCleanup(() => client.send(new DeleteHostedZoneCommand({ Id: zid })));
      }
    }
  }));

  return results;
}
