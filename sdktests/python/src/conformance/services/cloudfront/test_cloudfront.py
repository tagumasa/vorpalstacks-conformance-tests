import time

import pytest


@pytest.fixture(scope="module")
def distribution(cloudfront_client, unique_name):
    caller_ref = f"test-cf-{int(time.time() * 1000)}"
    origin_id = f"test-origin-{int(time.time() * 1000)}"
    resp = cloudfront_client.create_distribution(
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
    dist_id = ""
    dist_etag = ""
    if resp.get("Distribution", {}).get("Id"):
        dist_id = resp["Distribution"]["Id"]
        dist_etag = resp.get("ETag", "")
    yield dist_id, dist_etag
    if dist_id:
        try:
            get_resp = cloudfront_client.get_distribution_config(Id=dist_id)
            config = get_resp.get("DistributionConfig", {})
            config["Enabled"] = False
            update_resp = cloudfront_client.update_distribution(
                Id=dist_id,
                IfMatch=get_resp.get("ETag", ""),
                DistributionConfig=config,
            )
            cloudfront_client.delete_distribution(
                Id=dist_id, IfMatch=update_resp.get("ETag", "")
            )
        except Exception:
            pass


@pytest.fixture(scope="module")
def oac(cloudfront_client):
    oac_name = f"test-oac-{int(time.time() * 1000)}"
    resp = cloudfront_client.create_origin_access_control(
        OriginAccessControlConfig={
            "Name": oac_name,
            "OriginAccessControlOriginType": "s3",
            "SigningBehavior": "never",
            "SigningProtocol": "sigv4",
        },
    )
    oac_id = ""
    if resp.get("OriginAccessControl", {}).get("Id"):
        oac_id = resp["OriginAccessControl"]["Id"]
    yield oac_id
    if oac_id:
        try:
            cloudfront_client.delete_origin_access_control(Id=oac_id)
        except Exception:
            pass


class TestDistributions:
    def test_list_distributions(self, cloudfront_client):
        cloudfront_client.list_distributions(MaxItems="10")

    def test_create_distribution(self, distribution):
        dist_id, _ = distribution
        assert dist_id

    def test_get_distribution(self, cloudfront_client, distribution):
        dist_id, _ = distribution
        if dist_id:
            cloudfront_client.get_distribution(Id=dist_id)

    def test_get_distribution_config(self, cloudfront_client, distribution):
        dist_id, _ = distribution
        if dist_id:
            cloudfront_client.get_distribution_config(Id=dist_id)

    def test_list_distributions_after_create(self, cloudfront_client):
        resp = cloudfront_client.list_distributions(MaxItems="10")
        assert resp.get("DistributionList", {}).get("Quantity", 0) >= 1

    def test_update_distribution(self, cloudfront_client, distribution):
        dist_id, dist_etag = distribution
        if dist_id:
            get_resp = cloudfront_client.get_distribution_config(Id=dist_id)
            config = get_resp.get("DistributionConfig", {})
            config["Enabled"] = False
            cloudfront_client.update_distribution(
                Id=dist_id,
                IfMatch=dist_etag,
                DistributionConfig=config,
            )

    def test_delete_distribution(self, cloudfront_client, distribution):
        dist_id, _ = distribution
        if dist_id:
            get_resp = cloudfront_client.get_distribution_config(Id=dist_id)
            cloudfront_client.delete_distribution(
                Id=dist_id, IfMatch=get_resp.get("ETag", "")
            )

    def test_get_distribution_after_delete(self, cloudfront_client, distribution):
        dist_id, _ = distribution
        if dist_id:
            try:
                cloudfront_client.get_distribution(Id=dist_id)
                raise Exception("expected error for deleted distribution")
            except Exception as e:
                if str(e) == "expected error for deleted distribution":
                    raise

    def test_list_distributions_by_web_acl_id(self, cloudfront_client):
        cloudfront_client.list_distributions_by_web_acl_id(
            WebACLId="12345678-1234-1234-1234-123456789012"
        )


class TestOriginAccessControl:
    def test_list_origin_access_controls(self, cloudfront_client):
        cloudfront_client.list_origin_access_controls(MaxItems="10")

    def test_create_origin_access_control(self, oac):
        assert oac

    def test_get_origin_access_control(self, cloudfront_client, oac):
        if oac:
            cloudfront_client.get_origin_access_control(Id=oac)

    def test_delete_origin_access_control(self, cloudfront_client, oac):
        if oac:
            cloudfront_client.delete_origin_access_control(Id=oac)


class TestPolicies:
    def test_list_key_groups(self, cloudfront_client):
        cloudfront_client.list_key_groups(MaxItems="10")

    def test_list_cache_policies(self, cloudfront_client):
        cloudfront_client.list_cache_policies(MaxItems="10")

    def test_get_cache_policy(self, cloudfront_client):
        cloudfront_client.get_cache_policy(Id="658327ea-f89d-4fab-a63d-7e88639e58f6")

    def test_list_origin_request_policies(self, cloudfront_client):
        cloudfront_client.list_origin_request_policies(MaxItems="10")

    def test_list_response_headers_policies(self, cloudfront_client):
        cloudfront_client.list_response_headers_policies(MaxItems="10")
