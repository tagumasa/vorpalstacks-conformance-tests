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

        string taggedDistId = "";
        string taggedDistETag = "";
        var taggedCallerRef = TestRunner.MakeUniqueName("tagged-cf");
        var taggedOriginId = TestRunner.MakeUniqueName("tagged-origin");
        results.Add(await runner.RunTestAsync("cloudfront", "CreateDistributionWithTags", async () =>
        {
            var resp = await cloudFrontClient.CreateDistributionWithTagsAsync(new CreateDistributionWithTagsRequest
            {
                DistributionConfigWithTags = new DistributionConfigWithTags
                {
                    DistributionConfig = new DistributionConfig
                    {
                        CallerReference = taggedCallerRef,
                        Enabled = false,
                        DefaultCacheBehavior = new DefaultCacheBehavior
                        {
                            TargetOriginId = taggedOriginId,
                            ViewerProtocolPolicy = "allow-all",
                            ForwardedValues = new ForwardedValues
                            {
                                QueryString = false,
                                Cookies = new CookiePreference { Forward = ItemSelection.None }
                            },
                            MinTTL = 0,
                            DefaultTTL = 3600,
                            MaxTTL = 86400
                        },
                        Origins = new Origins
                        {
                            Items = new List<Origin>
                            {
                                new Origin
                                {
                                    Id = taggedOriginId,
                                    DomainName = "example.com",
                                    CustomOriginConfig = new CustomOriginConfig
                                    {
                                        OriginProtocolPolicy = "http-only",
                                        HTTPPort = 80,
                                        HTTPSPort = 443
                                    }
                                }
                            },
                            Quantity = 1
                        },
                        ViewerCertificate = new ViewerCertificate { CloudFrontDefaultCertificate = true },
                        Restrictions = new Restrictions
                        {
                            GeoRestriction = new GeoRestriction
                            {
                                RestrictionType = GeoRestrictionType.None,
                                Quantity = 0
                            }
                        }
                    },
                    Tags = new Tags
                    {
                        Items = new List<Tag>
                        {
                            new Tag { Key = "Environment", Value = "test" },
                            new Tag { Key = "Project", Value = "conformance" }
                        }
                    }
                }
            });
            if (resp.Distribution != null)
            {
                taggedDistId = resp.Distribution.Id;
                taggedDistETag = resp.ETag;
            }
            if (string.IsNullOrEmpty(taggedDistId))
                throw new Exception("tagged distribution ID is null");
        }));

        if (!string.IsNullOrEmpty(taggedDistId))
        {
            results.Add(await runner.RunTestAsync("cloudfront", "ListTagsForResource_Distribution", async () =>
            {
                var resp = await cloudFrontClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    Resource = taggedDistId
                });
                if (resp.Tags == null || resp.Tags.Items == null || resp.Tags.Items.Count == 0)
                    throw new Exception("tags are null or empty");
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "TagResource_Distribution", async () =>
            {
                await cloudFrontClient.TagResourceAsync(new TagResourceRequest
                {
                    Resource = taggedDistId,
                    Tags = new Tags
                    {
                        Items = new List<Tag>
                        {
                            new Tag { Key = "ExtraTag", Value = "value1" }
                        }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "ListTagsForResource_AfterTag", async () =>
            {
                var resp = await cloudFrontClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    Resource = taggedDistId
                });
                if (resp.Tags == null || resp.Tags.Items == null || resp.Tags.Items.Count == 0)
                    throw new Exception("tags are null or empty after tag");
                bool found = false;
                foreach (var t in resp.Tags.Items)
                {
                    if (t.Key == "ExtraTag") { found = true; break; }
                }
                if (!found)
                    throw new Exception("ExtraTag not found after tagging");
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "UntagResource_Distribution", async () =>
            {
                await cloudFrontClient.UntagResourceAsync(new UntagResourceRequest
                {
                    Resource = taggedDistId,
                    TagKeys = new TagKeys
                    {
                        Items = new List<string> { "ExtraTag" }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "ListTagsForResource_AfterUntag", async () =>
            {
                var resp = await cloudFrontClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    Resource = taggedDistId
                });
                if (resp.Tags == null || resp.Tags.Items == null || resp.Tags.Items.Count == 0)
                    throw new Exception("tags are null or empty after untag");
                foreach (var t in resp.Tags.Items)
                {
                    if (t.Key == "ExtraTag")
                        throw new Exception("ExtraTag should have been removed");
                }
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "Cleanup_TaggedDistribution", async () =>
            {
                if (string.IsNullOrEmpty(taggedDistId)) return;
                try
                {
                    var configResp = await cloudFrontClient.GetDistributionConfigAsync(new GetDistributionConfigRequest { Id = taggedDistId });
                    await cloudFrontClient.UpdateDistributionAsync(new UpdateDistributionRequest
                    {
                        Id = taggedDistId,
                        IfMatch = taggedDistETag,
                        DistributionConfig = configResp.DistributionConfig
                    });
                    var updateResp = await cloudFrontClient.GetDistributionConfigAsync(new GetDistributionConfigRequest { Id = taggedDistId });
                    var disabledETag = updateResp.ETag;
                    await cloudFrontClient.DeleteDistributionAsync(new DeleteDistributionRequest
                    {
                        Id = taggedDistId,
                        IfMatch = disabledETag
                    });
                }
                catch { }
                taggedDistId = "";
            }));
        }

        string invDistId = "";
        string invDistETag = "";
        var invCallerRef = TestRunner.MakeUniqueName("inv-cf");
        var invOriginId = TestRunner.MakeUniqueName("inv-origin");
        results.Add(await runner.RunTestAsync("cloudfront", "CreateDistribution_ForInvalidation", async () =>
        {
            var resp = await cloudFrontClient.CreateDistributionAsync(new CreateDistributionRequest
            {
                DistributionConfig = new DistributionConfig
                {
                    CallerReference = invCallerRef,
                    Enabled = true,
                    DefaultCacheBehavior = new DefaultCacheBehavior
                    {
                        TargetOriginId = invOriginId,
                        ViewerProtocolPolicy = "allow-all",
                        ForwardedValues = new ForwardedValues
                        {
                            QueryString = false,
                            Cookies = new CookiePreference { Forward = ItemSelection.None }
                        },
                        MinTTL = 0,
                        DefaultTTL = 3600,
                        MaxTTL = 86400
                    },
                    Origins = new Origins
                    {
                        Items = new List<Origin>
                        {
                            new Origin
                            {
                                Id = invOriginId,
                                DomainName = "example.com",
                                CustomOriginConfig = new CustomOriginConfig
                                {
                                    OriginProtocolPolicy = "http-only",
                                    HTTPPort = 80,
                                    HTTPSPort = 443
                                }
                            }
                        },
                        Quantity = 1
                    },
                    ViewerCertificate = new ViewerCertificate { CloudFrontDefaultCertificate = true },
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
                invDistId = resp.Distribution.Id;
                invDistETag = resp.ETag;
            }
            if (string.IsNullOrEmpty(invDistId))
                throw new Exception("invalidation distribution ID is null");
        }));

        if (!string.IsNullOrEmpty(invDistId))
        {
            results.Add(await runner.RunTestAsync("cloudfront", "CreateInvalidation", async () =>
            {
                var resp = await cloudFrontClient.CreateInvalidationAsync(new CreateInvalidationRequest
                {
                    DistributionId = invDistId,
                    InvalidationBatch = new InvalidationBatch
                    {
                        CallerReference = Guid.NewGuid().ToString(),
                        Paths = new Paths
                        {
                            Items = new List<string> { "/test-path" },
                            Quantity = 1
                        }
                    }
                });
                if (resp.Invalidation == null)
                    throw new Exception("invalidation is null");
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "GetInvalidation", async () =>
            {
                var listResp = await cloudFrontClient.ListInvalidationsAsync(new ListInvalidationsRequest
                {
                    DistributionId = invDistId
                });
                if (listResp.InvalidationList == null || listResp.InvalidationList.Quantity == 0)
                    throw new Exception("no invalidations found");
                var id = listResp.InvalidationList.Items[0].Id;
                var resp = await cloudFrontClient.GetInvalidationAsync(new GetInvalidationRequest
                {
                    DistributionId = invDistId,
                    Id = id
                });
                if (resp.Invalidation == null)
                    throw new Exception("invalidation is null");
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "ListInvalidations", async () =>
            {
                var resp = await cloudFrontClient.ListInvalidationsAsync(new ListInvalidationsRequest
                {
                    DistributionId = invDistId
                });
                if (resp.InvalidationList == null)
                    throw new Exception("InvalidationList is null");
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "Cleanup_InvalidationDist", async () =>
            {
                if (string.IsNullOrEmpty(invDistId)) return;
                try
                {
                    var configResp = await cloudFrontClient.GetDistributionConfigAsync(new GetDistributionConfigRequest { Id = invDistId });
                    configResp.DistributionConfig.Enabled = false;
                    var updateResp = await cloudFrontClient.UpdateDistributionAsync(new UpdateDistributionRequest
                    {
                        Id = invDistId,
                        IfMatch = invDistETag,
                        DistributionConfig = configResp.DistributionConfig
                    });
                    var disabledETag = updateResp.ETag;
                    await cloudFrontClient.DeleteDistributionAsync(new DeleteDistributionRequest
                    {
                        Id = invDistId,
                        IfMatch = disabledETag
                    });
                }
                catch { }
                invDistId = "";
            }));
        }

        string oacFullId = "";
        var oacFullName = TestRunner.MakeUniqueName("oac-full");
        results.Add(await runner.RunTestAsync("cloudfront", "CreateOriginAccessControl_Full", async () =>
        {
            var resp = await cloudFrontClient.CreateOriginAccessControlAsync(new CreateOriginAccessControlRequest
            {
                OriginAccessControlConfig = new OriginAccessControlConfig
                {
                    Name = oacFullName,
                    Description = "Full lifecycle OAC",
                    OriginAccessControlOriginType = "s3",
                    SigningBehavior = "always",
                    SigningProtocol = "sigv4"
                }
            });
            if (resp.OriginAccessControl != null)
                oacFullId = resp.OriginAccessControl.Id;
            if (string.IsNullOrEmpty(oacFullId))
                throw new Exception("OAC ID is null");
        }));

        if (!string.IsNullOrEmpty(oacFullId))
        {
            results.Add(await runner.RunTestAsync("cloudfront", "GetOriginAccessControlConfig", async () =>
            {
                var resp = await cloudFrontClient.GetOriginAccessControlConfigAsync(new GetOriginAccessControlConfigRequest
                {
                    Id = oacFullId
                });
                if (resp.OriginAccessControlConfig == null)
                    throw new Exception("OAC config is null");
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "UpdateOriginAccessControl", async () =>
            {
                await cloudFrontClient.UpdateOriginAccessControlAsync(new UpdateOriginAccessControlRequest
                {
                    Id = oacFullId,
                    IfMatch = "*",
                    OriginAccessControlConfig = new OriginAccessControlConfig
                    {
                        Name = oacFullName,
                        Description = "Updated OAC",
                        OriginAccessControlOriginType = "s3",
                        SigningBehavior = "no-override",
                        SigningProtocol = "sigv4"
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "DeleteOriginAccessControl_Full", async () =>
            {
                await cloudFrontClient.DeleteOriginAccessControlAsync(new DeleteOriginAccessControlRequest
                {
                    Id = oacFullId
                });
                oacFullId = "";
            }));
        }

        results.Add(await runner.RunTestAsync("cloudfront", "GetCachePolicy_Managed", async () =>
        {
            var listResp = await cloudFrontClient.ListCachePoliciesAsync(new ListCachePoliciesRequest());
            if (listResp.CachePolicyList == null || listResp.CachePolicyList.Items == null || listResp.CachePolicyList.Items.Count == 0)
                throw new Exception("no managed cache policies found");
            var managedId = listResp.CachePolicyList.Items[0].CachePolicy?.Id;
            if (string.IsNullOrEmpty(managedId))
                throw new Exception("managed cache policy ID is null");
            var resp = await cloudFrontClient.GetCachePolicyAsync(new GetCachePolicyRequest { Id = managedId });
            if (resp.CachePolicy == null)
                throw new Exception("cache policy is null");
        }));

        results.Add(await runner.RunTestAsync("cloudfront", "GetCachePolicyConfig_Managed", async () =>
        {
            var listResp = await cloudFrontClient.ListCachePoliciesAsync(new ListCachePoliciesRequest());
            if (listResp.CachePolicyList == null || listResp.CachePolicyList.Items == null || listResp.CachePolicyList.Items.Count == 0)
                throw new Exception("no managed cache policies found");
            var managedId = listResp.CachePolicyList.Items[0].CachePolicy?.Id;
            if (string.IsNullOrEmpty(managedId))
                throw new Exception("managed cache policy ID is null");
            var resp = await cloudFrontClient.GetCachePolicyConfigAsync(new GetCachePolicyConfigRequest { Id = managedId });
            if (resp.CachePolicyConfig == null)
                throw new Exception("cache policy config is null");
        }));

        string customCachePolicyId = "";
        var customCachePolicyName = TestRunner.MakeUniqueName("custom-cache-policy");
        results.Add(await runner.RunTestAsync("cloudfront", "CreateCachePolicy", async () =>
        {
            var resp = await cloudFrontClient.CreateCachePolicyAsync(new CreateCachePolicyRequest
            {
                CachePolicyConfig = new CachePolicyConfig
                {
                    Name = customCachePolicyName,
                    DefaultTTL = 60,
                    MaxTTL = 86400,
                    MinTTL = 0,
                    ParametersInCacheKeyAndForwardedToOrigin = new ParametersInCacheKeyAndForwardedToOrigin
                    {
                        CookiesConfig = new CachePolicyCookiesConfig { CookieBehavior = "none" },
                        HeadersConfig = new CachePolicyHeadersConfig { HeaderBehavior = "none" },
                        QueryStringsConfig = new CachePolicyQueryStringsConfig { QueryStringBehavior = "none" },
                        EnableAcceptEncodingGzip = true
                    }
                }
            });
            if (resp.CachePolicy != null)
                customCachePolicyId = resp.CachePolicy.Id;
            if (string.IsNullOrEmpty(customCachePolicyId))
                throw new Exception("custom cache policy ID is null");
        }));

        if (!string.IsNullOrEmpty(customCachePolicyId))
        {
            results.Add(await runner.RunTestAsync("cloudfront", "GetCachePolicy_Custom", async () =>
            {
                var resp = await cloudFrontClient.GetCachePolicyAsync(new GetCachePolicyRequest { Id = customCachePolicyId });
                if (resp.CachePolicy == null)
                    throw new Exception("custom cache policy is null");
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "GetCachePolicyConfig_Custom", async () =>
            {
                var resp = await cloudFrontClient.GetCachePolicyConfigAsync(new GetCachePolicyConfigRequest { Id = customCachePolicyId });
                if (resp.CachePolicyConfig == null)
                    throw new Exception("custom cache policy config is null");
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "UpdateCachePolicy", async () =>
            {
                var getResp = await cloudFrontClient.GetCachePolicyConfigAsync(new GetCachePolicyConfigRequest { Id = customCachePolicyId });
                getResp.CachePolicyConfig.DefaultTTL = 120;
                getResp.CachePolicyConfig.Comment = "Updated cache policy";
                await cloudFrontClient.UpdateCachePolicyAsync(new UpdateCachePolicyRequest
                {
                    Id = customCachePolicyId,
                    IfMatch = "*",
                    CachePolicyConfig = getResp.CachePolicyConfig
                });
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "DeleteCachePolicy", async () =>
            {
                await cloudFrontClient.DeleteCachePolicyAsync(new DeleteCachePolicyRequest
                {
                    Id = customCachePolicyId,
                    IfMatch = "*"
                });
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "GetCachePolicy_AfterDelete", async () =>
            {
                try
                {
                    await cloudFrontClient.GetCachePolicyAsync(new GetCachePolicyRequest { Id = customCachePolicyId });
                    throw new Exception("expected error for deleted cache policy");
                }
                catch (NoSuchCachePolicyException)
                {
                }
            }));
        }

        results.Add(await runner.RunTestAsync("cloudfront", "GetOriginRequestPolicy_Managed", async () =>
        {
            var listResp = await cloudFrontClient.ListOriginRequestPoliciesAsync(new ListOriginRequestPoliciesRequest());
            if (listResp.OriginRequestPolicyList == null || listResp.OriginRequestPolicyList.Items == null || listResp.OriginRequestPolicyList.Items.Count == 0)
                throw new Exception("no managed origin request policies found");
            var managedId = listResp.OriginRequestPolicyList.Items[0].OriginRequestPolicy?.Id;
            if (string.IsNullOrEmpty(managedId))
                throw new Exception("managed origin request policy ID is null");
            var resp = await cloudFrontClient.GetOriginRequestPolicyAsync(new GetOriginRequestPolicyRequest { Id = managedId });
            if (resp.OriginRequestPolicy == null)
                throw new Exception("origin request policy is null");
        }));

        string customOrpId = "";
        var customOrpName = TestRunner.MakeUniqueName("custom-orp");
        results.Add(await runner.RunTestAsync("cloudfront", "CreateOriginRequestPolicy", async () =>
        {
            var resp = await cloudFrontClient.CreateOriginRequestPolicyAsync(new CreateOriginRequestPolicyRequest
            {
                OriginRequestPolicyConfig = new OriginRequestPolicyConfig
                {
                    Name = customOrpName,
                    CookiesConfig = new OriginRequestPolicyCookiesConfig { CookieBehavior = "none" },
                    HeadersConfig = new OriginRequestPolicyHeadersConfig { HeaderBehavior = "none" },
                    QueryStringsConfig = new OriginRequestPolicyQueryStringsConfig { QueryStringBehavior = "none" }
                }
            });
            if (resp.OriginRequestPolicy != null)
                customOrpId = resp.OriginRequestPolicy.Id;
            if (string.IsNullOrEmpty(customOrpId))
                throw new Exception("custom ORP ID is null");
        }));

        if (!string.IsNullOrEmpty(customOrpId))
        {
            results.Add(await runner.RunTestAsync("cloudfront", "GetOriginRequestPolicy_Custom", async () =>
            {
                var resp = await cloudFrontClient.GetOriginRequestPolicyAsync(new GetOriginRequestPolicyRequest { Id = customOrpId });
                if (resp.OriginRequestPolicy == null)
                    throw new Exception("custom ORP is null");
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "GetOriginRequestPolicyConfig", async () =>
            {
                var resp = await cloudFrontClient.GetOriginRequestPolicyConfigAsync(new GetOriginRequestPolicyConfigRequest { Id = customOrpId });
                if (resp.OriginRequestPolicyConfig == null)
                    throw new Exception("custom ORP config is null");
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "UpdateOriginRequestPolicy", async () =>
            {
                var getResp = await cloudFrontClient.GetOriginRequestPolicyConfigAsync(new GetOriginRequestPolicyConfigRequest { Id = customOrpId });
                getResp.OriginRequestPolicyConfig.Comment = "Updated ORP";
                await cloudFrontClient.UpdateOriginRequestPolicyAsync(new UpdateOriginRequestPolicyRequest
                {
                    Id = customOrpId,
                    IfMatch = "*",
                    OriginRequestPolicyConfig = getResp.OriginRequestPolicyConfig
                });
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "DeleteOriginRequestPolicy", async () =>
            {
                await cloudFrontClient.DeleteOriginRequestPolicyAsync(new DeleteOriginRequestPolicyRequest
                {
                    Id = customOrpId,
                    IfMatch = "*"
                });
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "GetOriginRequestPolicy_AfterDelete", async () =>
            {
                try
                {
                    await cloudFrontClient.GetOriginRequestPolicyAsync(new GetOriginRequestPolicyRequest { Id = customOrpId });
                    throw new Exception("expected error for deleted origin request policy");
                }
                catch (NoSuchOriginRequestPolicyException)
                {
                }
            }));
        }

        string customRhpId = "";
        var customRhpName = TestRunner.MakeUniqueName("custom-rhp");
        results.Add(await runner.RunTestAsync("cloudfront", "CreateResponseHeadersPolicy", async () =>
        {
            var resp = await cloudFrontClient.CreateResponseHeadersPolicyAsync(new CreateResponseHeadersPolicyRequest
            {
                ResponseHeadersPolicyConfig = new ResponseHeadersPolicyConfig
                {
                    Name = customRhpName
                }
            });
            if (resp.ResponseHeadersPolicy != null)
                customRhpId = resp.ResponseHeadersPolicy.Id;
            if (string.IsNullOrEmpty(customRhpId))
                throw new Exception("custom RHP ID is null");
        }));

        if (!string.IsNullOrEmpty(customRhpId))
        {
            results.Add(await runner.RunTestAsync("cloudfront", "GetResponseHeadersPolicy", async () =>
            {
                var resp = await cloudFrontClient.GetResponseHeadersPolicyAsync(new GetResponseHeadersPolicyRequest { Id = customRhpId });
                if (resp.ResponseHeadersPolicy == null)
                    throw new Exception("custom RHP is null");
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "GetResponseHeadersPolicyConfig", async () =>
            {
                var resp = await cloudFrontClient.GetResponseHeadersPolicyConfigAsync(new GetResponseHeadersPolicyConfigRequest { Id = customRhpId });
                if (resp.ResponseHeadersPolicyConfig == null)
                    throw new Exception("custom RHP config is null");
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "UpdateResponseHeadersPolicy", async () =>
            {
                var getResp = await cloudFrontClient.GetResponseHeadersPolicyConfigAsync(new GetResponseHeadersPolicyConfigRequest { Id = customRhpId });
                getResp.ResponseHeadersPolicyConfig.Comment = "Updated RHP";
                await cloudFrontClient.UpdateResponseHeadersPolicyAsync(new UpdateResponseHeadersPolicyRequest
                {
                    Id = customRhpId,
                    IfMatch = "*",
                    ResponseHeadersPolicyConfig = getResp.ResponseHeadersPolicyConfig
                });
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "DeleteResponseHeadersPolicy", async () =>
            {
                await cloudFrontClient.DeleteResponseHeadersPolicyAsync(new DeleteResponseHeadersPolicyRequest
                {
                    Id = customRhpId,
                    IfMatch = "*"
                });
            }));

            results.Add(await runner.RunTestAsync("cloudfront", "GetResponseHeadersPolicy_AfterDelete", async () =>
            {
                try
                {
                    await cloudFrontClient.GetResponseHeadersPolicyAsync(new GetResponseHeadersPolicyRequest { Id = customRhpId });
                    throw new Exception("expected error for deleted response headers policy");
                }
                catch (NoSuchResponseHeadersPolicyException)
                {
                }
            }));
        }

        results.Add(await runner.RunTestAsync("cloudfront", "ListCachePolicies_Pagination", async () =>
        {
            var resp = await cloudFrontClient.ListCachePoliciesAsync(new ListCachePoliciesRequest { MaxItems = "5" });
            if (resp.CachePolicyList == null)
                throw new Exception("CachePolicyList is null");
            if (!string.IsNullOrEmpty(resp.CachePolicyList.NextMarker))
            {
                var resp2 = await cloudFrontClient.ListCachePoliciesAsync(new ListCachePoliciesRequest
                {
                    Marker = resp.CachePolicyList.NextMarker,
                    MaxItems = "5"
                });
                if (resp2.CachePolicyList == null)
                    throw new Exception("CachePolicyList page 2 is null");
            }
        }));

        return results;
    }
}
