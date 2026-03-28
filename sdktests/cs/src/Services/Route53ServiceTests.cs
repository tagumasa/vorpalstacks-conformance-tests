using Amazon;
using Amazon.Route53;
using Amazon.Route53.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class Route53ServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonRoute53Client route53Client,
        string region)
    {
        var results = new List<TestResult>();
        var hostedZoneName = TestRunner.MakeUniqueName("testzone") + ".com.";
        var zoneNameDelegation = TestRunner.MakeUniqueName("testzone") + ".com";

        string? hostedZoneId = null;

        try
        {
            results.Add(await runner.RunTestAsync("route53", "CreateHostedZone", async () =>
            {
                var resp = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
                {
                    Name = hostedZoneName,
                    CallerReference = Guid.NewGuid().ToString()
                });
                if (resp.HostedZone == null)
                    throw new Exception("HostedZone is null");
                hostedZoneId = resp.HostedZone.Id;
            }));

            results.Add(await runner.RunTestAsync("route53", "GetHostedZone", async () =>
            {
                if (hostedZoneId == null)
                    throw new Exception("HostedZoneId is null");
                var resp = await route53Client.GetHostedZoneAsync(new GetHostedZoneRequest
                {
                    Id = hostedZoneId
                });
                if (resp.HostedZone == null)
                    throw new Exception("HostedZone is null");
            }));

            results.Add(await runner.RunTestAsync("route53", "ListHostedZones", async () =>
            {
                var resp = await route53Client.ListHostedZonesAsync(new ListHostedZonesRequest());
                if (resp.HostedZones == null)
                    throw new Exception("HostedZones is null");
            }));

            results.Add(await runner.RunTestAsync("route53", "ListHostedZonesByName", async () =>
            {
                var resp = await route53Client.ListHostedZonesByNameAsync(new ListHostedZonesByNameRequest
                {
                    DNSName = hostedZoneName
                });
                if (resp.HostedZones != null)
                {
                    foreach (var z in resp.HostedZones)
                    {
                        if (z.Id == null) throw new Exception("HostedZone Id is null");
                    }
                }
            }));

            results.Add(await runner.RunTestAsync("route53", "ListResourceRecordSets", async () =>
            {
                if (hostedZoneId == null)
                    throw new Exception("HostedZoneId is null");
                var resp = await route53Client.ListResourceRecordSetsAsync(new ListResourceRecordSetsRequest
                {
                    HostedZoneId = hostedZoneId,
                    MaxItems = "10"
                });
                if (resp.ResourceRecordSets == null)
                    throw new Exception("ResourceRecordSets is null");
            }));

            string? changeId = null;
            results.Add(await runner.RunTestAsync("route53", "ChangeResourceRecordSets", async () =>
            {
                if (hostedZoneId == null)
                    throw new Exception("HostedZoneId is null");
                var resp = await route53Client.ChangeResourceRecordSetsAsync(new ChangeResourceRecordSetsRequest
                {
                    HostedZoneId = hostedZoneId,
                    ChangeBatch = new ChangeBatch
                    {
                        Changes = new List<Change>
                        {
                            new Change
                            {
                                Action = ChangeAction.CREATE,
                                ResourceRecordSet = new ResourceRecordSet
                                {
                                    Name = $"test.{hostedZoneName}",
                                    Type = RRType.A,
                                    TTL = 300,
                                    ResourceRecords = new List<ResourceRecord>
                                    {
                                        new ResourceRecord { Value = "192.0.2.1" }
                                    }
                                }
                            }
                        }
                    }
                });
                if (resp == null)
                    throw new Exception("response is nil");
                if (resp.ChangeInfo == null)
                    throw new Exception("change info is nil");
                changeId = resp.ChangeInfo.Id;
            }));

            if (changeId != null)
            {
                results.Add(await runner.RunTestAsync("route53", "GetChange", async () =>
                {
                    var resp = await route53Client.GetChangeAsync(new GetChangeRequest
                    {
                        Id = changeId
                    });
                    if (resp.ChangeInfo == null)
                        throw new Exception("change info is nil");
                }));
            }

            results.Add(await runner.RunTestAsync("route53", "DeleteResourceRecord", async () =>
            {
                if (hostedZoneId == null)
                    throw new Exception("HostedZoneId is null");
                var resp = await route53Client.ChangeResourceRecordSetsAsync(new ChangeResourceRecordSetsRequest
                {
                    HostedZoneId = hostedZoneId,
                    ChangeBatch = new ChangeBatch
                    {
                        Changes = new List<Change>
                        {
                            new Change
                            {
                                Action = ChangeAction.DELETE,
                                ResourceRecordSet = new ResourceRecordSet
                                {
                                    Name = $"test.{hostedZoneName}",
                                    Type = RRType.A,
                                    TTL = 300,
                                    ResourceRecords = new List<ResourceRecord>
                                    {
                                        new ResourceRecord { Value = "192.0.2.1" }
                                    }
                                }
                            }
                        }
                    }
                });
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("route53", "GetDNSSEC", async () =>
            {
                if (hostedZoneId == null)
                    throw new Exception("HostedZoneId is null");
                var resp = await route53Client.GetDNSSECAsync(new GetDNSSECRequest
                {
                    HostedZoneId = hostedZoneId
                });
            }));
        }
        finally
        {
            if (hostedZoneId != null)
            {
                try
                {
                    await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest
                    {
                        Id = hostedZoneId
                    });
                }
                catch { }
            }
        }

        results.Add(await runner.RunTestAsync("route53", "GetHostedZone_NonExistent", async () =>
        {
            try
            {
                await route53Client.GetHostedZoneAsync(new GetHostedZoneRequest
                {
                    Id = "Z00000000000000000000"
                });
                throw new Exception("Expected error but got none");
            }
            catch (NoSuchHostedZoneException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("route53", "DeleteHostedZone_NonExistent", async () =>
        {
            try
            {
                await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest
                {
                    Id = "Z00000000000000000000"
                });
                throw new Exception("Expected error but got none");
            }
            catch (NoSuchHostedZoneException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("route53", "GetChange_NonExistent", async () =>
        {
            try
            {
                await route53Client.GetChangeAsync(new GetChangeRequest
                {
                    Id = "C0000000000000000000000000"
                });
                throw new Exception("expected error for non-existent change");
            }
            catch (NoSuchChangeException) { }
        }));

        results.Add(await runner.RunTestAsync("route53", "ListReusableDelegationSets", async () =>
        {
            var resp = await route53Client.ListReusableDelegationSetsAsync(new ListReusableDelegationSetsRequest
            {
                MaxItems = "10"
            });
            if (resp.DelegationSets != null)
            {
                foreach (var ds in resp.DelegationSets)
                {
                    if (ds.Id == null) throw new Exception("DelegationSet Id is null");
                }
            }
        }));

        results.Add(await runner.RunTestAsync("route53", "CreateHostedZone_ContentVerify", async () =>
        {
            var verifyDomain = TestRunner.MakeUniqueName("verify") + ".com.";
            var createResp = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
            {
                Name = verifyDomain,
                CallerReference = Guid.NewGuid().ToString()
            });
            try
            {
                if (createResp.HostedZone == null || createResp.HostedZone.Name != verifyDomain)
                    throw new Exception($"domain name mismatch: got {createResp.HostedZone?.Name}, want {verifyDomain}");
                var hzId = createResp.HostedZone.Id;
                var getResp = await route53Client.GetHostedZoneAsync(new GetHostedZoneRequest { Id = hzId });
                if (getResp.HostedZone == null || getResp.HostedZone.Name != verifyDomain)
                    throw new Exception("get domain name mismatch");
            }
            finally
            {
                try { await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest { Id = createResp.HostedZone.Id }); } catch { }
            }
        }));

        return results;
    }
}
