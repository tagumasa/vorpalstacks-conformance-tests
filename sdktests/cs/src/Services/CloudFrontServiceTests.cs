using Amazon;
using Amazon.CloudFront;
using Amazon.CloudFront.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class CloudFrontServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonCloudFrontClient cloudFrontClient,
        string region)
    {
        var results = new List<TestResult>();

        results.Add(await runner.RunTestAsync("cloudfront", "ListDistributions", async () =>
        {
            var resp = await cloudFrontClient.ListDistributionsAsync(new ListDistributionsRequest());
            if (resp.DistributionList == null)
                throw new Exception("DistributionList is null");
        }));

        var callerRef = TestRunner.MakeUniqueName("test-cf");
        var originId = TestRunner.MakeUniqueName("test-origin");
        string distId = "";
        string distETag = "";
        results.Add(await runner.RunTestAsync("cloudfront", "CreateDistribution", async () =>
        {
            var resp = await cloudFrontClient.CreateDistributionAsync(new CreateDistributionRequest
            {
                DistributionConfig = new DistributionConfig
                {
                    CallerReference = callerRef,
                    Enabled = true,
                    Comment = "SDK test distribution",
                    DefaultRootObject = "index.html",
                    Origins = new Origins
                    {
                        Items = new List<Origin>
                        {
                            new Origin
                            {
                                Id = originId,
                                DomainName = "example.com",
                                CustomOriginConfig = new CustomOriginConfig
                                {
                                    OriginProtocolPolicy = "http-only",
                                    HTTPPort = 80,
                                    HTTPSPort = 443,
                                }
                            }
                        },
                        Quantity = 1
                    },
                    DefaultCacheBehavior = new DefaultCacheBehavior
                    {
                        TargetOriginId = originId,
                        ViewerProtocolPolicy = "allow-all",
                        AllowedMethods = new AllowedMethods
                        {
                            Items = new List<string> { "HEAD", "GET" },
                            Quantity = 2,
                            CachedMethods = new CachedMethods
                            {
                                Items = new List<string> { "HEAD", "GET" },
                                Quantity = 2
                            }
                        },
                        ForwardedValues = new ForwardedValues
                        {
                            QueryString = false,
                            Cookies = new CookiePreference
                            {
                                Forward = ItemSelection.None
                            }
                        },
                        MinTTL = 0,
                        DefaultTTL = 3600,
                        MaxTTL = 86400
                    },
                    ViewerCertificate = new ViewerCertificate
                    {
                        CloudFrontDefaultCertificate = true
                    },
                    Restrictions = new Restrictions
                    {
                        GeoRestriction = new GeoRestriction
                        {
                            RestrictionType = GeoRestrictionType.None,
                            Quantity = 0
                        }
                    }
                }
            });
            if (resp.Distribution != null)
            {
                distId = resp.Distribution.Id;
                distETag = resp.ETag;
            }
        }));

        if (!string.IsNullOrEmpty(distId))
        {
            results.Add(await runner.RunTestAsync("cloudfront", "GetDistribution", async () =>
            {
                var resp = await cloudFrontClient.GetDistributionAsync(new GetDistributionRequest { Id = distId });
                if (resp.Distribution == null)
                    throw new Exception("Distribution is null");
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "GetDistributionConfig", async () =>
            {
                var resp = await cloudFrontClient.GetDistributionConfigAsync(new GetDistributionConfigRequest { Id = distId });
                if (resp.DistributionConfig == null)
                    throw new Exception("DistributionConfig is null");
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "ListDistributionsAfterCreate", async () =>
            {
                var resp = await cloudFrontClient.ListDistributionsAsync(new ListDistributionsRequest());
                if (resp.DistributionList == null || resp.DistributionList.Quantity < 1)
                    throw new Exception("Expected at least 1 distribution, got 0");
            }));

            string updateETag = "";
            results.Add(await runner.RunTestAsync("cloudfront", "UpdateDistribution", async () =>
            {
                var getResp = await cloudFrontClient.GetDistributionConfigAsync(new GetDistributionConfigRequest { Id = distId });
                getResp.DistributionConfig.Enabled = false;
                var resp = await cloudFrontClient.UpdateDistributionAsync(new UpdateDistributionRequest
                {
                    Id = distId,
                    IfMatch = distETag,
                    DistributionConfig = getResp.DistributionConfig
                });
                if (resp != null)
                    updateETag = resp.ETag;
            }));

            if (!string.IsNullOrEmpty(updateETag))
            {
                results.Add(await runner.RunTestAsync("cloudfront", "DeleteDistribution", async () =>
                {
                    var resp = await cloudFrontClient.DeleteDistributionAsync(new DeleteDistributionRequest
                    {
                        Id = distId,
                        IfMatch = updateETag
                    });
                    if (resp == null)
                        throw new Exception("Response is null");
                }));
            }

            results.Add(await runner.RunTestAsync("cloudfront", "GetDistributionAfterDelete", async () =>
            {
                try
                {
                    await cloudFrontClient.GetDistributionAsync(new GetDistributionRequest { Id = distId });
                    throw new Exception("Expected error for deleted distribution");
                }
                catch (AmazonCloudFrontException)
                {
                }
            }));
        }

         results.Add(await runner.RunTestAsync("cloudfront", "ListDistributionsByWebACLId", async () =>
         {
             var resp = await cloudFrontClient.ListDistributionsByWebACLIdAsync(new ListDistributionsByWebACLIdRequest
             {
                 WebACLId = "12345678-1234-1234-1234-123456789012"
             });
             if (resp.DistributionList != null && resp.DistributionList.Quantity > 0)
             {
                 foreach (var d in resp.DistributionList.Items)
                 {
                     if (d.Id == null) throw new Exception("DistributionSummary Id is null");
                 }
             }
         }));

        results.Add(await runner.RunTestAsync("cloudfront", "ListOriginAccessControls", async () =>
        {
            var resp = await cloudFrontClient.ListOriginAccessControlsAsync(new ListOriginAccessControlsRequest());
        }));

        var oacName = TestRunner.MakeUniqueName("test-oac");
        string oacId = "";
        results.Add(await runner.RunTestAsync("cloudfront", "CreateOriginAccessControl", async () =>
        {
            var resp = await cloudFrontClient.CreateOriginAccessControlAsync(new CreateOriginAccessControlRequest
            {
                OriginAccessControlConfig = new OriginAccessControlConfig
                {
                    Name = oacName,
                    OriginAccessControlOriginType = "s3",
                    SigningBehavior = "never",
                    SigningProtocol = "sigv4"
                }
            });
            if (resp.OriginAccessControl != null)
                oacId = resp.OriginAccessControl.Id;
        }));

        if (!string.IsNullOrEmpty(oacId))
        {
            results.Add(await runner.RunTestAsync("cloudfront", "GetOriginAccessControl", async () =>
            {
                var resp = await cloudFrontClient.GetOriginAccessControlAsync(new GetOriginAccessControlRequest { Id = oacId });
                if (resp.OriginAccessControl == null)
                    throw new Exception("OAC is nil");
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "DeleteOriginAccessControl", async () =>
            {
                await cloudFrontClient.DeleteOriginAccessControlAsync(new DeleteOriginAccessControlRequest { Id = oacId });
                oacId = "";
            }));
        }

        results.Add(await runner.RunTestAsync("cloudfront", "ListKeyGroups", async () =>
        {
            var resp = await cloudFrontClient.ListKeyGroupsAsync(new ListKeyGroupsRequest());
        }));

        results.Add(await runner.RunTestAsync("cloudfront", "ListCachePolicies", async () =>
        {
            var resp = await cloudFrontClient.ListCachePoliciesAsync(new ListCachePoliciesRequest());
        }));

        results.Add(await runner.RunTestAsync("cloudfront", "GetCachePolicy", async () =>
        {
            var resp = await cloudFrontClient.GetCachePolicyAsync(new GetCachePolicyRequest
            {
                Id = "658327ea-f89d-4fab-a63d-7e88639e58f6"
            });
        }));

        results.Add(await runner.RunTestAsync("cloudfront", "ListOriginRequestPolicies", async () =>
        {
            var resp = await cloudFrontClient.ListOriginRequestPoliciesAsync(new ListOriginRequestPoliciesRequest());
        }));

        results.Add(await runner.RunTestAsync("cloudfront", "ListResponseHeadersPolicies", async () =>
        {
            var resp = await cloudFrontClient.ListResponseHeadersPoliciesAsync(new ListResponseHeadersPoliciesRequest());
        }));

        return results;
    }
}
