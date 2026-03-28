import {
  Route53Client,
  ListHostedZonesCommand,
  CreateHostedZoneCommand,
  GetHostedZoneCommand,
  ListResourceRecordSetsCommand,
  ChangeResourceRecordSetsCommand,
  GetChangeCommand,
  DeleteHostedZoneCommand,
  GetDNSSECCommand,
  ListReusableDelegationSetsCommand,
} from "@aws-sdk/client-route-53";
import { TestRunner, TestResult } from "../runner";

export async function runRoute53Tests(
  runner: TestRunner,
  client: Route53Client,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const domainName = `example-${Date.now()}.com.`;
  let hostedZoneID = "";

  results.push(await runner.runTest("route53", "ListHostedZones", async () => {
    await client.send(new ListHostedZonesCommand({ MaxItems: 10 }));
  }));

  results.push(await runner.runTest("route53", "CreateHostedZone", async () => {
    const resp = await client.send(new CreateHostedZoneCommand({
      Name: domainName,
      CallerReference: `ref-${Date.now()}`,
    }));
    if (resp.HostedZone?.Id) {
      hostedZoneID = resp.HostedZone.Id;
    }
  }));

  if (hostedZoneID) {
    results.push(await runner.runTest("route53", "GetHostedZone", async () => {
      await client.send(new GetHostedZoneCommand({ Id: hostedZoneID }));
    }));

    results.push(await runner.runTest("route53", "ListResourceRecordSets", async () => {
      await client.send(new ListResourceRecordSetsCommand({
        HostedZoneId: hostedZoneID,
        MaxItems: 10,
      }));
    }));

    let changeID = "";
    results.push(await runner.runTest("route53", "ChangeResourceRecordSets_Create", async () => {
      const resp = await client.send(new ChangeResourceRecordSetsCommand({
        HostedZoneId: hostedZoneID,
        ChangeBatch: {
          Changes: [
            {
              Action: "CREATE",
              ResourceRecordSet: {
                Name: `test.${domainName}`,
                Type: "A",
                TTL: 300,
                ResourceRecords: [{ Value: "192.0.2.1" }],
              },
            },
          ],
        },
      }));
      if (resp.ChangeInfo?.Id) {
        changeID = resp.ChangeInfo.Id;
      }
    }));

    if (changeID) {
      results.push(await runner.runTest("route53", "GetChange", async () => {
        await client.send(new GetChangeCommand({ Id: changeID }));
      }));
    }

    results.push(await runner.runTest("route53", "ChangeResourceRecordSets_Delete", async () => {
      await client.send(new ChangeResourceRecordSetsCommand({
        HostedZoneId: hostedZoneID,
        ChangeBatch: {
          Changes: [
            {
              Action: "DELETE",
              ResourceRecordSet: {
                Name: `test.${domainName}`,
                Type: "A",
                TTL: 300,
                ResourceRecords: [{ Value: "192.0.2.1" }],
              },
            },
          ],
        },
      }));
    }));

    results.push(await runner.runTest("route53", "GetDNSSEC", async () => {
      await client.send(new GetDNSSECCommand({ HostedZoneId: hostedZoneID }));
    }));

    results.push(await runner.runTest("route53", "DeleteHostedZone", async () => {
      await client.send(new DeleteHostedZoneCommand({ Id: hostedZoneID }));
    }));
  }

  results.push(await runner.runTest("route53", "ListReusableDelegationSets", async () => {
    await client.send(new ListReusableDelegationSetsCommand({ MaxItems: 10 }));
  }));

  results.push(await runner.runTest("route53", "GetHostedZone_NonExistent", async () => {
    try {
      await client.send(new GetHostedZoneCommand({ Id: "Z00000000000000000000" }));
      throw new Error("expected error for non-existent hosted zone");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for non-existent hosted zone") {
        throw err;
      }
    }
  }));

  results.push(await runner.runTest("route53", "DeleteHostedZone_NonExistent", async () => {
    try {
      await client.send(new DeleteHostedZoneCommand({ Id: "Z00000000000000000000" }));
      throw new Error("expected error for non-existent hosted zone");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for non-existent hosted zone") {
        throw err;
      }
    }
  }));

  results.push(await runner.runTest("route53", "GetChange_NonExistent", async () => {
    try {
      await client.send(new GetChangeCommand({ Id: "C0000000000000000000000000" }));
      throw new Error("expected error for non-existent change");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for non-existent change") {
        throw err;
      }
    }
  }));

  results.push(await runner.runTest("route53", "CreateHostedZone_ContentVerify", async () => {
    const verifyDomain = `verify-${Date.now()}.com.`;
    const verifyRef = `ref-${Date.now()}`;
    const resp = await client.send(new CreateHostedZoneCommand({
      Name: verifyDomain,
      CallerReference: verifyRef,
    }));
    const hzID = resp.HostedZone?.Id;
    if (!hzID) {
      throw new Error("hosted zone id is nil");
    }

    try {
      if (resp.HostedZone?.Name !== verifyDomain) {
        throw new Error(`domain name mismatch: got ${resp.HostedZone?.Name}, want ${verifyDomain}`);
      }

      const getResp = await client.send(new GetHostedZoneCommand({ Id: hzID }));
      if (getResp.HostedZone?.Name !== verifyDomain) {
        throw new Error("get domain name mismatch");
      }
    } finally {
      await client.send(new DeleteHostedZoneCommand({ Id: hzID }));
    }
  }));

  return results;
}
