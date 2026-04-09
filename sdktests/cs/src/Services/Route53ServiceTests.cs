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

            results.Add(await runner.RunTestAsync("route53", "DeleteHostedZone", async () =>
            {
                if (hostedZoneId == null)
                    throw new Exception("HostedZoneId is null");
                var resp = await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest
                {
                    Id = hostedZoneId
                });
                if (resp == null)
                    throw new Exception("response is nil");
                hostedZoneId = null;
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
            catch (NoSuchHostedZoneException) { }
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
            catch (NoSuchHostedZoneException) { }
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

        results.Add(await runner.RunTestAsync("route53", "ListHostedZonesByName_WithDNSName", async () =>
        {
            var testDomain = TestRunner.MakeUniqueName("sorttest") + ".com.";
            var hzResp = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
            {
                Name = testDomain,
                CallerReference = Guid.NewGuid().ToString()
            });
            try
            {
                var resp = await route53Client.ListHostedZonesByNameAsync(new ListHostedZonesByNameRequest
                {
                    DNSName = testDomain,
                    MaxItems = "10"
                });
                if (resp.HostedZones == null)
                    throw new Exception("hosted zones list is nil");
                var found = false;
                foreach (var hz in resp.HostedZones)
                {
                    if (hz.Name == testDomain)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    throw new Exception($"created zone {testDomain} not found in ListHostedZonesByName");
            }
            finally
            {
                try { await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest { Id = hzResp.HostedZone.Id }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("route53", "UpdateHostedZoneComment", async () =>
        {
            var ucDomain = TestRunner.MakeUniqueName("updatecomment") + ".com.";
            var createResp = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
            {
                Name = ucDomain,
                CallerReference = Guid.NewGuid().ToString()
            });
            var ucID = createResp.HostedZone.Id;
            try
            {
                var comment = "test comment for zone";
                await route53Client.UpdateHostedZoneCommentAsync(new UpdateHostedZoneCommentRequest
                {
                    Id = ucID,
                    Comment = comment
                });
                var getResp = await route53Client.GetHostedZoneAsync(new GetHostedZoneRequest { Id = ucID });
                if (getResp.HostedZone.Config == null || getResp.HostedZone.Config.Comment != comment)
                    throw new Exception($"comment mismatch: got {getResp.HostedZone.Config?.Comment}, want {comment}");
            }
            finally
            {
                try { await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest { Id = ucID }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("route53", "CreateHealthCheck", async () =>
        {
            var resp = await route53Client.CreateHealthCheckAsync(new CreateHealthCheckRequest
            {
                CallerReference = Guid.NewGuid().ToString(),
                HealthCheckConfig = new HealthCheckConfig
                {
                    Type = HealthCheckType.HTTP,
                    ResourcePath = "/health",
                    FullyQualifiedDomainName = "example.com",
                    RequestInterval = 30,
                    FailureThreshold = 3,
                    MeasureLatency = true,
                    Disabled = false,
                    EnableSNI = true,
                    IPAddress = "192.0.2.1",
                    Port = 443,
                    Inverted = false,
                    InsufficientDataHealthStatus = InsufficientDataHealthStatus.LastKnownStatus,
                }
            });
            try { await route53Client.DeleteHealthCheckAsync(new DeleteHealthCheckRequest { HealthCheckId = resp.HealthCheck.Id }); } catch { }
        }));

        string? healthCheckId = null;
        results.Add(await runner.RunTestAsync("route53", "CreateHealthCheck_GetID", async () =>
        {
            var resp = await route53Client.CreateHealthCheckAsync(new CreateHealthCheckRequest
            {
                CallerReference = Guid.NewGuid().ToString(),
                HealthCheckConfig = new HealthCheckConfig
                {
                    Type = HealthCheckType.TCP,
                    FullyQualifiedDomainName = "hc.example.com",
                    Port = 8080,
                }
            });
            if (resp.HealthCheck == null || string.IsNullOrEmpty(resp.HealthCheck.Id))
                throw new Exception("health check ID is null");
            healthCheckId = resp.HealthCheck.Id;
        }));

        if (healthCheckId != null)
        {
            results.Add(await runner.RunTestAsync("route53", "GetHealthCheck", async () =>
            {
                var resp = await route53Client.GetHealthCheckAsync(new GetHealthCheckRequest
                {
                    HealthCheckId = healthCheckId
                });
                if (resp.HealthCheck == null)
                    throw new Exception("health check is nil");
                if (resp.HealthCheck.HealthCheckConfig == null)
                    throw new Exception("health check config is nil");
                if (resp.HealthCheck.HealthCheckConfig.Type != HealthCheckType.TCP)
                    throw new Exception($"health check type mismatch: got {resp.HealthCheck.HealthCheckConfig.Type}");
            }));

            results.Add(await runner.RunTestAsync("route53", "UpdateHealthCheck", async () =>
            {
                var resp = await route53Client.UpdateHealthCheckAsync(new UpdateHealthCheckRequest
                {
                    HealthCheckId = healthCheckId,
                    ResourcePath = "/updated",
                    FailureThreshold = 5,
                    Disabled = true,
                    Inverted = true,
                    EnableSNI = false,
                    FullyQualifiedDomainName = "updated.example.com",
                });
                if (resp.HealthCheck == null)
                    throw new Exception("health check is nil after update");
            }));

            results.Add(await runner.RunTestAsync("route53", "UpdateHealthCheck_VerifyContent", async () =>
            {
                var resp = await route53Client.GetHealthCheckAsync(new GetHealthCheckRequest
                {
                    HealthCheckId = healthCheckId
                });
                if (resp.HealthCheck.HealthCheckConfig.FailureThreshold != 5)
                    throw new Exception($"failure threshold mismatch: got {resp.HealthCheck.HealthCheckConfig.FailureThreshold}");
                if (resp.HealthCheck.HealthCheckConfig.ResourcePath != "/updated")
                    throw new Exception($"resource path mismatch: got {resp.HealthCheck.HealthCheckConfig.ResourcePath}");
                if (resp.HealthCheck.HealthCheckConfig.Disabled != true)
                    throw new Exception("expected disabled=true");
                if (resp.HealthCheck.HealthCheckConfig.Inverted != true)
                    throw new Exception("expected inverted=true");
            }));

            results.Add(await runner.RunTestAsync("route53", "DeleteHealthCheck", async () =>
            {
                await route53Client.DeleteHealthCheckAsync(new DeleteHealthCheckRequest
                {
                    HealthCheckId = healthCheckId
                });
                healthCheckId = null;
            }));

            results.Add(await runner.RunTestAsync("route53", "GetHealthCheck_NonExistent", async () =>
            {
                try
                {
                    await route53Client.GetHealthCheckAsync(new GetHealthCheckRequest
                    {
                        HealthCheckId = "00000000-0000-0000-0000-000000000000"
                    });
                    throw new Exception("expected error for non-existent health check");
                }
                catch (NoSuchHealthCheckException) { }
            }));

            results.Add(await runner.RunTestAsync("route53", "DeleteHealthCheck_NonExistent", async () =>
            {
                try
                {
                    await route53Client.DeleteHealthCheckAsync(new DeleteHealthCheckRequest
                    {
                        HealthCheckId = "00000000-0000-0000-0000-000000000000"
                    });
                    throw new Exception("expected error for non-existent health check");
                }
                catch (NoSuchHealthCheckException) { }
            }));
        }

        results.Add(await runner.RunTestAsync("route53", "ListHealthChecks", async () =>
        {
            var resp = await route53Client.ListHealthChecksAsync(new ListHealthChecksRequest
            {
                MaxItems = "10"
            });
            if (resp.HealthChecks != null)
            {
                foreach (var hc in resp.HealthChecks)
                {
                    if (string.IsNullOrEmpty(hc.Id))
                        throw new Exception("HealthCheck Id is null");
                }
            }
        }));

        results.Add(await runner.RunTestAsync("route53", "HealthCheckConfig_DefaultPort", async () =>
        {
            var resp = await route53Client.CreateHealthCheckAsync(new CreateHealthCheckRequest
            {
                CallerReference = Guid.NewGuid().ToString(),
                HealthCheckConfig = new HealthCheckConfig
                {
                    Type = HealthCheckType.HTTP,
                    FullyQualifiedDomainName = "porttest.example.com",
                }
            });
            var hcID = resp.HealthCheck.Id;
            try
            {
                var getResp = await route53Client.GetHealthCheckAsync(new GetHealthCheckRequest
                {
                    HealthCheckId = hcID
                });
                if (getResp.HealthCheck.HealthCheckConfig.Port != 80)
                    throw new Exception($"expected default port 80, got {getResp.HealthCheck.HealthCheckConfig.Port}");
            }
            finally
            {
                try { await route53Client.DeleteHealthCheckAsync(new DeleteHealthCheckRequest { HealthCheckId = hcID }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("route53", "AssociateVPCWithHostedZone", async () =>
        {
            var privateDomain = TestRunner.MakeUniqueName("private") + ".com.";
            var createResp = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
            {
                Name = privateDomain,
                CallerReference = Guid.NewGuid().ToString(),
                HostedZoneConfig = new HostedZoneConfig
                {
                    PrivateZone = true,
                    Comment = "private zone for VPC test",
                },
                VPC = new VPC
                {
                    VPCId = "vpc-abcdef01",
                    VPCRegion = "us-east-1",
                }
            });
            var privateZoneID = createResp.HostedZone.Id;
            try
            {
                await route53Client.AssociateVPCWithHostedZoneAsync(new AssociateVPCWithHostedZoneRequest
                {
                    HostedZoneId = privateZoneID,
                    VPC = new VPC
                    {
                        VPCId = "vpc-xyz12345",
                        VPCRegion = "us-east-1",
                    }
                });
                var getResp = await route53Client.GetHostedZoneAsync(new GetHostedZoneRequest
                {
                    Id = privateZoneID
                });
                if (getResp.VPCs == null || getResp.VPCs.Count < 2)
                    throw new Exception($"expected at least 2 VPCs, got {getResp.VPCs?.Count}");
            }
            finally
            {
                try { await route53Client.DisassociateVPCFromHostedZoneAsync(new DisassociateVPCFromHostedZoneRequest { HostedZoneId = privateZoneID, VPC = new VPC { VPCId = "vpc-xyz12345", VPCRegion = "us-east-1" } }); } catch { }
                try { await route53Client.DisassociateVPCFromHostedZoneAsync(new DisassociateVPCFromHostedZoneRequest { HostedZoneId = privateZoneID, VPC = new VPC { VPCId = "vpc-abcdef01", VPCRegion = "us-east-1" } }); } catch { }
                try { await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest { Id = privateZoneID }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("route53", "DisassociateVPCFromHostedZone", async () =>
        {
            var dsDomain = TestRunner.MakeUniqueName("disassoc") + ".com.";
            var createResp = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
            {
                Name = dsDomain,
                CallerReference = Guid.NewGuid().ToString(),
                HostedZoneConfig = new HostedZoneConfig
                {
                    PrivateZone = true,
                },
                VPC = new VPC
                {
                    VPCId = "vpc-disassoc1",
                    VPCRegion = "us-east-1",
                }
            });
            var dsZoneID = createResp.HostedZone.Id;
            try
            {
                await route53Client.AssociateVPCWithHostedZoneAsync(new AssociateVPCWithHostedZoneRequest
                {
                    HostedZoneId = dsZoneID,
                    VPC = new VPC
                    {
                        VPCId = "vpc-disassoc2",
                        VPCRegion = "us-east-1",
                    }
                });
                await route53Client.DisassociateVPCFromHostedZoneAsync(new DisassociateVPCFromHostedZoneRequest
                {
                    HostedZoneId = dsZoneID,
                    VPC = new VPC
                    {
                        VPCId = "vpc-disassoc2",
                        VPCRegion = "us-east-1",
                    }
                });
                var getResp = await route53Client.GetHostedZoneAsync(new GetHostedZoneRequest
                {
                    Id = dsZoneID
                });
                if (getResp.VPCs == null || getResp.VPCs.Count != 1)
                    throw new Exception($"expected 1 VPC after disassociation, got {getResp.VPCs?.Count}");
            }
            finally
            {
                try { await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest { Id = dsZoneID }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("route53", "ChangeTagsForResource", async () =>
        {
            var tagDomain = TestRunner.MakeUniqueName("tags") + ".com.";
            var createResp = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
            {
                Name = tagDomain,
                CallerReference = Guid.NewGuid().ToString()
            });
            var tagZoneID = createResp.HostedZone.Id;
            try
            {
                await route53Client.ChangeTagsForResourceAsync(new ChangeTagsForResourceRequest
                {
                    ResourceType = TagResourceType.Hostedzone,
                    ResourceId = tagZoneID,
                    AddTags = new List<Tag>
                    {
                        new Tag { Key = "Environment", Value = "test" },
                        new Tag { Key = "Owner", Value = "team-a" },
                    }
                });
                var listResp = await route53Client.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceType = TagResourceType.Hostedzone,
                    ResourceId = tagZoneID
                });
                if (listResp.ResourceTagSet == null || listResp.ResourceTagSet.Tags.Count != 2)
                    throw new Exception($"expected 2 tags after add, got {listResp.ResourceTagSet?.Tags?.Count}");
                await route53Client.ChangeTagsForResourceAsync(new ChangeTagsForResourceRequest
                {
                    ResourceType = TagResourceType.Hostedzone,
                    ResourceId = tagZoneID,
                    RemoveTagKeys = new List<string> { "Owner" }
                });
                var listResp2 = await route53Client.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceType = TagResourceType.Hostedzone,
                    ResourceId = tagZoneID
                });
                if (listResp2.ResourceTagSet.Tags.Count != 1)
                    throw new Exception($"expected 1 tag after removal, got {listResp2.ResourceTagSet.Tags.Count}");
                if (listResp2.ResourceTagSet.Tags[0].Key != "Environment")
                    throw new Exception($"expected Environment tag, got {listResp2.ResourceTagSet.Tags[0].Key}");
            }
            finally
            {
                try { await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest { Id = tagZoneID }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("route53", "ListTagsForResource_HealthCheck", async () =>
        {
            var hcResp = await route53Client.CreateHealthCheckAsync(new CreateHealthCheckRequest
            {
                CallerReference = Guid.NewGuid().ToString(),
                HealthCheckConfig = new HealthCheckConfig
                {
                    Type = HealthCheckType.TCP,
                    FullyQualifiedDomainName = "hctag.example.com",
                }
            });
            var hcID = hcResp.HealthCheck.Id;
            try
            {
                await route53Client.ChangeTagsForResourceAsync(new ChangeTagsForResourceRequest
                {
                    ResourceType = TagResourceType.Healthcheck,
                    ResourceId = hcID,
                    AddTags = new List<Tag>
                    {
                        new Tag { Key = "Monitor", Value = "enabled" },
                    }
                });
                var listResp = await route53Client.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceType = TagResourceType.Healthcheck,
                    ResourceId = hcID
                });
                if (listResp.ResourceTagSet.Tags.Count != 1)
                    throw new Exception($"expected 1 tag, got {listResp.ResourceTagSet.Tags.Count}");
            }
            finally
            {
                try { await route53Client.DeleteHealthCheckAsync(new DeleteHealthCheckRequest { HealthCheckId = hcID }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("route53", "ChangeResourceRecordSets_Upsert", async () =>
        {
            var upsertDomain = TestRunner.MakeUniqueName("upsert") + ".com.";
            var createResp = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
            {
                Name = upsertDomain,
                CallerReference = Guid.NewGuid().ToString()
            });
            var upsertZoneID = createResp.HostedZone.Id;
            try
            {
                var recordName = $"upsert.{upsertDomain}";
                await route53Client.ChangeResourceRecordSetsAsync(new ChangeResourceRecordSetsRequest
                {
                    HostedZoneId = upsertZoneID,
                    ChangeBatch = new ChangeBatch
                    {
                        Changes = new List<Change>
                        {
                            new Change
                            {
                                Action = ChangeAction.UPSERT,
                                ResourceRecordSet = new ResourceRecordSet
                                {
                                    Name = recordName,
                                    Type = RRType.A,
                                    TTL = 300,
                                    ResourceRecords = new List<ResourceRecord>
                                    {
                                        new ResourceRecord { Value = "10.0.0.1" }
                                    }
                                }
                            }
                        }
                    }
                });
                await route53Client.ChangeResourceRecordSetsAsync(new ChangeResourceRecordSetsRequest
                {
                    HostedZoneId = upsertZoneID,
                    ChangeBatch = new ChangeBatch
                    {
                        Changes = new List<Change>
                        {
                            new Change
                            {
                                Action = ChangeAction.UPSERT,
                                ResourceRecordSet = new ResourceRecordSet
                                {
                                    Name = recordName,
                                    Type = RRType.A,
                                    TTL = 600,
                                    ResourceRecords = new List<ResourceRecord>
                                    {
                                        new ResourceRecord { Value = "10.0.0.2" }
                                    }
                                }
                            }
                        }
                    }
                });
                var listResp = await route53Client.ListResourceRecordSetsAsync(new ListResourceRecordSetsRequest
                {
                    HostedZoneId = upsertZoneID
                });
                var found = false;
                foreach (var rs in listResp.ResourceRecordSets)
                {
                    if (rs.Name == recordName && rs.Type == RRType.A)
                    {
                        if (rs.TTL != 600)
                            throw new Exception($"TTL mismatch after upsert: got {rs.TTL}, want 600");
                        if (rs.ResourceRecords.Count == 1 && rs.ResourceRecords[0].Value != "10.0.0.2")
                            throw new Exception($"value mismatch after upsert: got {rs.ResourceRecords[0].Value}");
                        found = true;
                        break;
                    }
                }
                if (!found)
                    throw new Exception("upserted record not found");
                try
                {
                    await route53Client.ChangeResourceRecordSetsAsync(new ChangeResourceRecordSetsRequest
                    {
                        HostedZoneId = upsertZoneID,
                        ChangeBatch = new ChangeBatch
                        {
                            Changes = new List<Change>
                            {
                                new Change
                                {
                                    Action = ChangeAction.DELETE,
                                    ResourceRecordSet = new ResourceRecordSet
                                    {
                                        Name = recordName,
                                        Type = RRType.A,
                                        TTL = 600,
                                        ResourceRecords = new List<ResourceRecord>
                                        {
                                            new ResourceRecord { Value = "10.0.0.2" }
                                        }
                                    }
                                }
                            }
                        }
                    });
                }
                catch { }
            }
            finally
            {
                try { await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest { Id = upsertZoneID }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("route53", "CreateHostedZone_PrivateWithComment", async () =>
        {
            var pvtDomain = TestRunner.MakeUniqueName("private-comment") + ".com.";
            var resp = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
            {
                Name = pvtDomain,
                CallerReference = Guid.NewGuid().ToString(),
                HostedZoneConfig = new HostedZoneConfig
                {
                    PrivateZone = true,
                    Comment = "private zone with comment",
                },
                VPC = new VPC
                {
                    VPCId = "vpc-pvttest",
                    VPCRegion = "eu-west-1",
                }
            });
            var pvtID = resp.HostedZone.Id;
            try
            {
                var getResp = await route53Client.GetHostedZoneAsync(new GetHostedZoneRequest { Id = pvtID });
                if (getResp.HostedZone.Config == null)
                    throw new Exception("config is nil");
                if (getResp.HostedZone.Config.PrivateZone != true)
                    throw new Exception("expected PrivateZone=true");
                if (getResp.HostedZone.Config.Comment != "private zone with comment")
                    throw new Exception($"comment mismatch: got {getResp.HostedZone.Config.Comment}");
                if (getResp.VPCs == null || getResp.VPCs.Count != 1)
                    throw new Exception($"expected 1 VPC, got {getResp.VPCs?.Count}");
            }
            finally
            {
                try { await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest { Id = pvtID }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("route53", "DelegationSet_Persisted", async () =>
        {
            var dsDomain = TestRunner.MakeUniqueName("ds-persist") + ".com.";
            var createResp = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
            {
                Name = dsDomain,
                CallerReference = Guid.NewGuid().ToString()
            });
            var dsZoneID = createResp.HostedZone.Id;
            try
            {
                var createDSID = createResp.DelegationSet?.Id;
                if (string.IsNullOrEmpty(createDSID))
                    throw new Exception("delegation set ID is empty in create response");
                var getResp = await route53Client.GetHostedZoneAsync(new GetHostedZoneRequest { Id = dsZoneID });
                var getDSID = getResp.DelegationSet?.Id;
                if (string.IsNullOrEmpty(getDSID))
                    throw new Exception("delegation set ID is empty in get response");
                if (createDSID != getDSID)
                    throw new Exception($"delegation set ID mismatch: create={createDSID}, get={getDSID}");
            }
            finally
            {
                try { await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest { Id = dsZoneID }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("route53", "CreateHostedZone_InvalidName", async () =>
        {
            try
            {
                await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
                {
                    Name = "invalid name with spaces",
                    CallerReference = Guid.NewGuid().ToString()
                });
                throw new Exception("expected error for invalid zone name");
            }
            catch (AmazonRoute53Exception) { }
        }));

        results.Add(await runner.RunTestAsync("route53", "AssociateVPCWithHostedZone_PublicZone", async () =>
        {
            var pubDomain = TestRunner.MakeUniqueName("pub-vpc-test") + ".com.";
            var createResp = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
            {
                Name = pubDomain,
                CallerReference = Guid.NewGuid().ToString()
            });
            var pubZoneID = createResp.HostedZone.Id;
            try
            {
                try
                {
                    await route53Client.AssociateVPCWithHostedZoneAsync(new AssociateVPCWithHostedZoneRequest
                    {
                        HostedZoneId = pubZoneID,
                        VPC = new VPC
                        {
                            VPCId = "vpc-test123",
                            VPCRegion = "us-east-1",
                        }
                    });
                    throw new Exception("expected error when associating VPC with public zone");
                }
                catch (AmazonRoute53Exception) { }
            }
            finally
            {
                try { await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest { Id = pubZoneID }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("route53", "DeleteHostedZone_NotEmpty", async () =>
        {
            var neDomain = TestRunner.MakeUniqueName("notempty") + ".com.";
            var createResp = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
            {
                Name = neDomain,
                CallerReference = Guid.NewGuid().ToString()
            });
            var neZoneID = createResp.HostedZone.Id;
            try
            {
                try
                {
                    await route53Client.ChangeResourceRecordSetsAsync(new ChangeResourceRecordSetsRequest
                    {
                        HostedZoneId = neZoneID,
                        ChangeBatch = new ChangeBatch
                        {
                            Changes = new List<Change>
                            {
                                new Change
                                {
                                    Action = ChangeAction.CREATE,
                                    ResourceRecordSet = new ResourceRecordSet
                                    {
                                        Name = $"keep.{neDomain}",
                                        Type = RRType.A,
                                        TTL = 300,
                                        ResourceRecords = new List<ResourceRecord>
                                        {
                                            new ResourceRecord { Value = "10.0.0.1" }
                                        }
                                    }
                                }
                            }
                        }
                    });
                }
                catch { }

                try
                {
                    await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest
                    {
                        Id = neZoneID
                    });
                    throw new Exception("expected error when deleting non-empty zone");
                }
                catch (HostedZoneNotEmptyException) { }
            }
            finally
            {
                try
                {
                    await route53Client.ChangeResourceRecordSetsAsync(new ChangeResourceRecordSetsRequest
                    {
                        HostedZoneId = neZoneID,
                        ChangeBatch = new ChangeBatch
                        {
                            Changes = new List<Change>
                            {
                                new Change
                                {
                                    Action = ChangeAction.DELETE,
                                    ResourceRecordSet = new ResourceRecordSet
                                    {
                                        Name = $"keep.{neDomain}",
                                        Type = RRType.A,
                                        TTL = 300,
                                        ResourceRecords = new List<ResourceRecord>
                                        {
                                            new ResourceRecord { Value = "10.0.0.1" }
                                        }
                                    }
                                }
                            }
                        }
                    });
                }
                catch { }
                try { await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest { Id = neZoneID }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("route53", "ChangeResourceRecordSets_MultipleTypes", async () =>
        {
            var mtDomain = TestRunner.MakeUniqueName("multitype") + ".com.";
            var createResp = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
            {
                Name = mtDomain,
                CallerReference = Guid.NewGuid().ToString()
            });
            var mtZoneID = createResp.HostedZone.Id;
            try
            {
                await route53Client.ChangeResourceRecordSetsAsync(new ChangeResourceRecordSetsRequest
                {
                    HostedZoneId = mtZoneID,
                    ChangeBatch = new ChangeBatch
                    {
                        Changes = new List<Change>
                        {
                            new Change
                            {
                                Action = ChangeAction.CREATE,
                                ResourceRecordSet = new ResourceRecordSet
                                {
                                    Name = $"www.{mtDomain}",
                                    Type = RRType.CNAME,
                                    TTL = 300,
                                    ResourceRecords = new List<ResourceRecord>
                                    {
                                        new ResourceRecord { Value = "target.example.com" }
                                    }
                                }
                            },
                            new Change
                            {
                                Action = ChangeAction.CREATE,
                                ResourceRecordSet = new ResourceRecordSet
                                {
                                    Name = $"txt.{mtDomain}",
                                    Type = RRType.TXT,
                                    TTL = 300,
                                    ResourceRecords = new List<ResourceRecord>
                                    {
                                        new ResourceRecord { Value = "v=spf1 include:example.com ~all" }
                                    }
                                }
                            }
                        }
                    }
                });
                var listResp = await route53Client.ListResourceRecordSetsAsync(new ListResourceRecordSetsRequest
                {
                    HostedZoneId = mtZoneID
                });
                var foundCNAME = false;
                var foundTXT = false;
                foreach (var rs in listResp.ResourceRecordSets)
                {
                    if (rs.Type == RRType.CNAME && rs.Name.EndsWith(mtDomain))
                        foundCNAME = true;
                    if (rs.Type == RRType.TXT && rs.Name.EndsWith(mtDomain))
                        foundTXT = true;
                }
                if (!foundCNAME)
                    throw new Exception("CNAME record not found");
                if (!foundTXT)
                    throw new Exception("TXT record not found");
            }
            finally
            {
                try
                {
                    await route53Client.ChangeResourceRecordSetsAsync(new ChangeResourceRecordSetsRequest
                    {
                        HostedZoneId = mtZoneID,
                        ChangeBatch = new ChangeBatch
                        {
                            Changes = new List<Change>
                            {
                                new Change
                                {
                                    Action = ChangeAction.DELETE,
                                    ResourceRecordSet = new ResourceRecordSet
                                    {
                                        Name = $"www.{mtDomain}",
                                        Type = RRType.CNAME,
                                        TTL = 300,
                                        ResourceRecords = new List<ResourceRecord>
                                        {
                                            new ResourceRecord { Value = "target.example.com" }
                                        }
                                    }
                                },
                                new Change
                                {
                                    Action = ChangeAction.DELETE,
                                    ResourceRecordSet = new ResourceRecordSet
                                    {
                                        Name = $"txt.{mtDomain}",
                                        Type = RRType.TXT,
                                        TTL = 300,
                                        ResourceRecords = new List<ResourceRecord>
                                        {
                                            new ResourceRecord { Value = "v=spf1 include:example.com ~all" }
                                        }
                                    }
                                }
                            }
                        }
                    });
                }
                catch { }
                try { await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest { Id = mtZoneID }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("route53", "ListHostedZones_Pagination", async () =>
        {
            var pgTs = TestRunner.MakeUniqueName("pzpg");
            var pgZoneIDs = new List<string>();
            for (var i = 0; i < 5; i++)
            {
                var resp = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
                {
                    Name = $"{pgTs}-{i}.example.com.",
                    CallerReference = $"{pgTs}-ref-{i}"
                });
                pgZoneIDs.Add(resp.HostedZone.Id);
            }
            try
            {
                var pageCount = 0;
                var totalCount = 0;
                string? marker = null;
                while (true)
                {
                    var req = new ListHostedZonesRequest { MaxItems = "2" };
                    if (marker != null)
                        req.Marker = marker;
                    var resp = await route53Client.ListHostedZonesAsync(req);
                    pageCount++;
                    totalCount += resp.HostedZones.Count;
                    if (resp.IsTruncated == true && !string.IsNullOrEmpty(resp.NextMarker))
                    {
                        marker = resp.NextMarker;
                    }
                    else
                    {
                        break;
                    }
                }
                if (pageCount < 2)
                    throw new Exception($"expected at least 2 pages, got {pageCount} (total zones: {totalCount})");
                if (totalCount < 5)
                    throw new Exception($"expected at least 5 zones total across pages, got {totalCount}");
            }
            finally
            {
                foreach (var zid in pgZoneIDs)
                {
                    try { await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest { Id = zid }); } catch { }
                }
            }
        }));

        return results;
    }
}
