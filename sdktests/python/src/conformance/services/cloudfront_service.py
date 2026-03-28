import time
from ..runner import TestRunner, TestResult


async def run_cloudfront_tests(
    runner: TestRunner,
    endpoint: str,
    region: str,
) -> list[TestResult]:
    results: list[TestResult] = []
    import boto3

    session = boto3.Session(
        aws_access_key_id="test",
        aws_secret_access_key="test",
    )
    client = session.client("cloudfront", endpoint_url=endpoint, region_name=region)

    caller_ref = f"test-cf-{int(time.time() * 1000)}"
    origin_id = f"test-origin-{int(time.time() * 1000)}"
    dist_id = ""
    dist_etag = ""

    def _list_distributions():
        client.list_distributions(MaxItems="10")

    results.append(
        await runner.run_test("cloudfront", "ListDistributions", _list_distributions)
    )

    def _create_distribution():
        nonlocal dist_id, dist_etag
        resp = client.create_distribution(
            DistributionConfig={
                "CallerReference": caller_ref,
                "Enabled": True,
                "Comment": "SDK test distribution",
                "DefaultRootObject": "index.html",
                "Origins": {
                    "Quantity": 1,
                    "Items": [
                        {
                            "Id": origin_id,
                            "DomainName": "example.com",
                            "CustomOriginConfig": {
                                "HTTPPort": 80,
                                "HTTPSPort": 443,
                                "OriginProtocolPolicy": "http-only",
                                "OriginReadTimeout": 30,
                                "OriginKeepaliveTimeout": 5,
                                "OriginSslProtocols": {
                                    "Quantity": 1,
                                    "Items": ["TLSv1.2"],
                                },
                            },
                        },
                    ],
                },
                "DefaultCacheBehavior": {
                    "TargetOriginId": origin_id,
                    "ViewerProtocolPolicy": "allow-all",
                    "AllowedMethods": {
                        "Quantity": 2,
                        "Items": ["HEAD", "GET"],
                        "CachedMethods": {
                            "Quantity": 2,
                            "Items": ["HEAD", "GET"],
                        },
                    },
                    "ForwardedValues": {
                        "QueryString": False,
                        "Cookies": {
                            "Forward": "none",
                        },
                    },
                    "MinTTL": 0,
                    "DefaultTTL": 3600,
                    "MaxTTL": 86400,
                },
                "ViewerCertificate": {
                    "CloudFrontDefaultCertificate": True,
                },
                "Restrictions": {
                    "GeoRestriction": {
                        "RestrictionType": "none",
                        "Quantity": 0,
                    },
                },
            },
        )
        if resp.get("Distribution", {}).get("Id"):
            dist_id = resp["Distribution"]["Id"]
            dist_etag = resp.get("ETag", "")

    results.append(
        await runner.run_test("cloudfront", "CreateDistribution", _create_distribution)
    )

    if dist_id:

        def _get_distribution():
            client.get_distribution(Id=dist_id)

        results.append(
            await runner.run_test("cloudfront", "GetDistribution", _get_distribution)
        )

        def _get_distribution_config():
            client.get_distribution_config(Id=dist_id)

        results.append(
            await runner.run_test(
                "cloudfront", "GetDistributionConfig", _get_distribution_config
            )
        )

        def _list_distributions_after_create():
            resp = client.list_distributions(MaxItems="10")
            if not resp.get("DistributionList", {}).get("Quantity", 0) >= 1:
                raise Exception("expected at least 1 distribution, got 0")

        results.append(
            await runner.run_test(
                "cloudfront",
                "ListDistributionsAfterCreate",
                _list_distributions_after_create,
            )
        )

        update_etag = ""

        def _update_distribution():
            nonlocal update_etag
            get_resp = client.get_distribution_config(Id=dist_id)
            config = get_resp.get("DistributionConfig", {})
            config["Enabled"] = False
            resp = client.update_distribution(
                Id=dist_id,
                IfMatch=dist_etag,
                DistributionConfig=config,
            )
            update_etag = resp.get("ETag", "")

        results.append(
            await runner.run_test(
                "cloudfront", "UpdateDistribution", _update_distribution
            )
        )

        if update_etag:

            def _delete_distribution():
                client.delete_distribution(Id=dist_id, IfMatch=update_etag)

            results.append(
                await runner.run_test(
                    "cloudfront", "DeleteDistribution", _delete_distribution
                )
            )

        def _get_distribution_after_delete():
            try:
                client.get_distribution(Id=dist_id)
                raise Exception("expected error for deleted distribution")
            except Exception as e:
                if str(e) == "expected error for deleted distribution":
                    raise

        results.append(
            await runner.run_test(
                "cloudfront",
                "GetDistributionAfterDelete",
                _get_distribution_after_delete,
            )
        )

    def _list_distributions_by_web_acl_id():
        client.list_distributions_by_web_acl_id(
            WebACLId="12345678-1234-1234-1234-123456789012"
        )

    results.append(
        await runner.run_test(
            "cloudfront",
            "ListDistributionsByWebACLId",
            _list_distributions_by_web_acl_id,
        )
    )

    def _list_origin_access_controls():
        client.list_origin_access_controls(MaxItems="10")

    results.append(
        await runner.run_test(
            "cloudfront", "ListOriginAccessControls", _list_origin_access_controls
        )
    )

    oac_name = f"test-oac-{int(time.time() * 1000)}"
    oac_id = ""

    def _create_origin_access_control():
        nonlocal oac_id
        resp = client.create_origin_access_control(
            OriginAccessControlConfig={
                "Name": oac_name,
                "OriginAccessControlOriginType": "s3",
                "SigningBehavior": "never",
                "SigningProtocol": "sigv4",
            },
        )
        if resp.get("OriginAccessControl", {}).get("Id"):
            oac_id = resp["OriginAccessControl"]["Id"]

    results.append(
        await runner.run_test(
            "cloudfront", "CreateOriginAccessControl", _create_origin_access_control
        )
    )

    if oac_id:

        def _get_origin_access_control():
            client.get_origin_access_control(Id=oac_id)

        results.append(
            await runner.run_test(
                "cloudfront", "GetOriginAccessControl", _get_origin_access_control
            )
        )

        def _delete_origin_access_control():
            client.delete_origin_access_control(Id=oac_id)

        results.append(
            await runner.run_test(
                "cloudfront", "DeleteOriginAccessControl", _delete_origin_access_control
            )
        )

    def _list_key_groups():
        client.list_key_groups(MaxItems="10")

    results.append(
        await runner.run_test("cloudfront", "ListKeyGroups", _list_key_groups)
    )

    def _list_cache_policies():
        client.list_cache_policies(MaxItems="10")

    results.append(
        await runner.run_test("cloudfront", "ListCachePolicies", _list_cache_policies)
    )

    def _get_cache_policy():
        client.get_cache_policy(Id="658327ea-f89d-4fab-a63d-7e88639e58f6")

    results.append(
        await runner.run_test("cloudfront", "GetCachePolicy", _get_cache_policy)
    )

    def _list_origin_request_policies():
        client.list_origin_request_policies(MaxItems="10")

    results.append(
        await runner.run_test(
            "cloudfront", "ListOriginRequestPolicies", _list_origin_request_policies
        )
    )

    def _list_response_headers_policies():
        client.list_response_headers_policies(MaxItems="10")

    results.append(
        await runner.run_test(
            "cloudfront", "ListResponseHeadersPolicies", _list_response_headers_policies
        )
    )

    return results
