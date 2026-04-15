import {
  ListDistributionsCommand,
  CreateDistributionCommand,
  GetDistributionCommand,
  GetDistributionConfigCommand,
  UpdateDistributionCommand,
  DeleteDistributionCommand,
  ListDistributionsByWebACLIdCommand,
} from '@aws-sdk/client-cloudfront';
import { CloudFrontTestContext, CloudFrontTestSection } from './context.js';
import { makeUniqueName, assertErrorContains } from '../../helpers.js';

export const runDistributionTests: CloudFrontTestSection = async (ctx, runner) => {
  const results = [];
  const { client, svc } = ctx;

  results.push(
    await runner.runTest(svc, "ListDistributions", async () => {
      const resp = await client.send(new ListDistributionsCommand({ MaxItems: 10 }));
      if (!resp.DistributionList) throw new Error("distribution list to be defined");
    })
  );

  const callerRef = makeUniqueName("cf-dist");
  const originID = makeUniqueName("cf-origin");
  let distID = "";
  let distETag = "";
  results.push(
    await runner.runTest(svc, "CreateDistribution", async () => {
      const resp = await client.send(
        new CreateDistributionCommand({
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
                    OriginSslProtocols: { Quantity: 1, Items: ["TLSv1.2"] },
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
                CachedMethods: { Quantity: 2, Items: ["HEAD", "GET"] },
              },
              ForwardedValues: {
                QueryString: false,
                Cookies: { Forward: "none" },
              },
              MinTTL: 0,
              DefaultTTL: 3600,
              MaxTTL: 86400,
            },
            ViewerCertificate: { CloudFrontDefaultCertificate: true },
            Restrictions: {
              GeoRestriction: { RestrictionType: "none", Quantity: 0 },
            },
          },
        })
      );
      if (!resp.Distribution?.Id) throw new Error('expected Distribution.Id to be defined');
      distID = resp.Distribution.Id;
      distETag = resp.ETag || "";
    })
  );

  if (distID) {
    results.push(
      await runner.runTest(svc, "GetDistribution", async () => {
        const resp = await client.send(new GetDistributionCommand({ Id: distID }));
        if (!resp.Distribution) throw new Error("distribution to be defined");
        if (!resp.ETag || resp.ETag === "") throw new Error("ETag header is missing");
      })
    );

    results.push(
      await runner.runTest(svc, "GetDistributionConfig", async () => {
        const resp = await client.send(
          new GetDistributionConfigCommand({ Id: distID })
        );
        if (!resp.DistributionConfig) throw new Error("distribution config to be defined");
      })
    );

    results.push(
      await runner.runTest(svc, "ListDistributionsAfterCreate", async () => {
        const resp = await client.send(new ListDistributionsCommand({ MaxItems: 10 }));
        if (
          !resp.DistributionList?.Quantity ||
          resp.DistributionList.Quantity < 1
        ) {
          throw new Error("expected at least 1 distribution, got 0");
        }
      })
    );

    let updateETag = "";
    results.push(
      await runner.runTest(svc, "UpdateDistribution", async () => {
        const getResp = await client.send(
          new GetDistributionConfigCommand({ Id: distID })
        );
        if (!getResp.DistributionConfig) throw new Error("no config");
        getResp.DistributionConfig.Enabled = false;
        const resp = await client.send(
          new UpdateDistributionCommand({
            Id: distID,
            IfMatch: distETag,
            DistributionConfig: getResp.DistributionConfig,
          })
        );
        if (resp.ETag) updateETag = resp.ETag;
      })
    );

    if (updateETag) {
      results.push(
        await runner.runTest(svc, "DeleteDistribution", async () => {
          const resp = await client.send(
            new DeleteDistributionCommand({
              Id: distID,
              IfMatch: updateETag,
            })
          );
          if (!resp) throw new Error("response to be defined");
        })
      );
    }

    results.push(
      await runner.runTest(svc, "GetDistributionAfterDelete", async () => {
        let err: unknown;
        try {
          await client.send(new GetDistributionCommand({ Id: distID }));
        } catch (e) {
          err = e;
        }
        assertErrorContains(err, "NoSuchDistribution");
      })
    );
  }

  results.push(
    await runner.runTest(svc, "ListDistributionsByWebACLId", async () => {
      const resp = await client.send(
        new ListDistributionsByWebACLIdCommand({
          WebACLId: "12345678-1234-1234-1234-123456789012",
        })
      );
      if (!resp.DistributionList) throw new Error("distribution list to be defined");
    })
  );

  return results;
};
