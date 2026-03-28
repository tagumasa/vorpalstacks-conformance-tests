import {
  CloudFrontClient,
  ListDistributionsCommand,
  CreateDistributionCommand,
  GetDistributionCommand,
  GetDistributionConfigCommand,
  UpdateDistributionCommand,
  DeleteDistributionCommand,
  ListDistributionsByWebACLIdCommand,
  ListOriginAccessControlsCommand,
  CreateOriginAccessControlCommand,
  GetOriginAccessControlCommand,
  DeleteOriginAccessControlCommand,
  ListKeyGroupsCommand,
  ListCachePoliciesCommand,
  GetCachePolicyCommand,
  ListOriginRequestPoliciesCommand,
  ListResponseHeadersPoliciesCommand,
} from "@aws-sdk/client-cloudfront";
import { TestRunner, TestResult } from "../runner";

export async function runCloudFrontTests(
  runner: TestRunner,
  client: CloudFrontClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const callerRef = `test-cf-${Date.now()}`;
  const originID = `test-origin-${Date.now()}`;
  let distID = "";
  let distETag = "";

  results.push(await runner.runTest("cloudfront", "ListDistributions", async () => {
    await client.send(new ListDistributionsCommand({ MaxItems: 10 }));
  }));

  results.push(await runner.runTest("cloudfront", "CreateDistribution", async () => {
    const resp = await client.send(new CreateDistributionCommand({
      DistributionConfig: {
        CallerReference: callerRef,
        Enabled: true,
        Comment: "SDK test distribution",
        DefaultRootObject: "index.html",
        Origins: {
          Quantity: 1,
          Items: [
            {
              Id: originID,
              DomainName: "example.com",
              CustomOriginConfig: {
                HTTPPort: 80,
                HTTPSPort: 443,
                OriginProtocolPolicy: "http-only",
                OriginReadTimeout: 30,
                OriginKeepaliveTimeout: 5,
                OriginSslProtocols: {
                  Quantity: 1,
                  Items: ["TLSv1.2"],
                },
              },
            },
          ],
        },
        DefaultCacheBehavior: {
          TargetOriginId: originID,
          ViewerProtocolPolicy: "allow-all",
          AllowedMethods: {
            Quantity: 2,
            Items: ["HEAD", "GET"],
            CachedMethods: {
              Quantity: 2,
              Items: ["HEAD", "GET"],
            },
          },
          ForwardedValues: {
            QueryString: false,
            Cookies: {
              Forward: "none",
            },
          },
          MinTTL: 0,
          DefaultTTL: 3600,
          MaxTTL: 86400,
        },
        ViewerCertificate: {
          CloudFrontDefaultCertificate: true,
        },
        Restrictions: {
          GeoRestriction: {
            RestrictionType: "none",
            Quantity: 0,
          },
        },
      },
    }));
    if (resp.Distribution?.Id) {
      distID = resp.Distribution.Id;
      distETag = resp.ETag || "";
    }
  }));

  if (distID) {
    results.push(await runner.runTest("cloudfront", "GetDistribution", async () => {
      await client.send(new GetDistributionCommand({ Id: distID }));
    }));

    results.push(await runner.runTest("cloudfront", "GetDistributionConfig", async () => {
      await client.send(new GetDistributionConfigCommand({ Id: distID }));
    }));

    results.push(await runner.runTest("cloudfront", "ListDistributionsAfterCreate", async () => {
      const resp = await client.send(new ListDistributionsCommand({ MaxItems: 10 }));
      if (!resp.DistributionList?.Quantity || resp.DistributionList.Quantity < 1) {
        throw new Error("expected at least 1 distribution, got 0");
      }
    }));

    let updateETag = "";
    results.push(await runner.runTest("cloudfront", "UpdateDistribution", async () => {
      const getResp = await client.send(new GetDistributionConfigCommand({ Id: distID }));
      if (getResp.DistributionConfig) {
        getResp.DistributionConfig.Enabled = false;
      }
      const resp = await client.send(new UpdateDistributionCommand({
        Id: distID,
        IfMatch: distETag,
        DistributionConfig: getResp.DistributionConfig,
      }));
      updateETag = resp.ETag || "";
    }));

    if (updateETag) {
      results.push(await runner.runTest("cloudfront", "DeleteDistribution", async () => {
        await client.send(new DeleteDistributionCommand({
          Id: distID,
          IfMatch: updateETag,
        }));
      }));
    }

    results.push(await runner.runTest("cloudfront", "GetDistributionAfterDelete", async () => {
      try {
        await client.send(new GetDistributionCommand({ Id: distID }));
        throw new Error("expected error for deleted distribution");
      } catch (err: unknown) {
        if (err instanceof Error && err.message === "expected error for deleted distribution") {
          throw err;
        }
      }
    }));
  }

  results.push(await runner.runTest("cloudfront", "ListDistributionsByWebACLId", async () => {
    await client.send(new ListDistributionsByWebACLIdCommand({
      WebACLId: "12345678-1234-1234-1234-123456789012",
    }));
  }));

  results.push(await runner.runTest("cloudfront", "ListOriginAccessControls", async () => {
    await client.send(new ListOriginAccessControlsCommand({ MaxItems: 10 }));
  }));

  const oacName = `test-oac-${Date.now()}`;
  let oacID = "";
  results.push(await runner.runTest("cloudfront", "CreateOriginAccessControl", async () => {
    const resp = await client.send(new CreateOriginAccessControlCommand({
      OriginAccessControlConfig: {
        Name: oacName,
        OriginAccessControlOriginType: "s3",
        SigningBehavior: "never",
        SigningProtocol: "sigv4",
      },
    }));
    if (resp.OriginAccessControl?.Id) {
      oacID = resp.OriginAccessControl.Id;
    }
  }));

  if (oacID) {
    results.push(await runner.runTest("cloudfront", "GetOriginAccessControl", async () => {
      await client.send(new GetOriginAccessControlCommand({ Id: oacID }));
    }));

    results.push(await runner.runTest("cloudfront", "DeleteOriginAccessControl", async () => {
      await client.send(new DeleteOriginAccessControlCommand({ Id: oacID }));
    }));
  }

  results.push(await runner.runTest("cloudfront", "ListKeyGroups", async () => {
    await client.send(new ListKeyGroupsCommand({ MaxItems: 10 }));
  }));

  results.push(await runner.runTest("cloudfront", "ListCachePolicies", async () => {
    await client.send(new ListCachePoliciesCommand({ MaxItems: 10 }));
  }));

  results.push(await runner.runTest("cloudfront", "GetCachePolicy", async () => {
    await client.send(new GetCachePolicyCommand({
      Id: "658327ea-f89d-4fab-a63d-7e88639e58f6",
    }));
  }));

  results.push(await runner.runTest("cloudfront", "ListOriginRequestPolicies", async () => {
    await client.send(new ListOriginRequestPoliciesCommand({ MaxItems: 10 }));
  }));

  results.push(await runner.runTest("cloudfront", "ListResponseHeadersPolicies", async () => {
    await client.send(new ListResponseHeadersPoliciesCommand({ MaxItems: 10 }));
  }));

  return results;
}
