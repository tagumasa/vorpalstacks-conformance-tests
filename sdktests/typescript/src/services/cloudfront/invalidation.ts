import {
  CreateDistributionCommand,
  GetDistributionConfigCommand,
  UpdateDistributionCommand,
  DeleteDistributionCommand,
  CreateInvalidationCommand,
  GetInvalidationCommand,
  ListInvalidationsCommand,
} from '@aws-sdk/client-cloudfront';
import { CloudFrontTestContext, CloudFrontTestSection } from './context.js';
import { makeUniqueName } from '../../helpers.js';

export const runInvalidationTests: CloudFrontTestSection = async (ctx, runner) => {
  const results = [];
  const { client, svc } = ctx;

  const callerRef3 = makeUniqueName("cf-inv");
  const originID3 = makeUniqueName("cf-origin3");
  let invDistID = "";
  results.push(
    await runner.runTest(svc, "CreateDistribution_ForInvalidation", async () => {
      const resp = await client.send(
        new CreateDistributionCommand({
          DistributionConfig: {
            CallerReference: callerRef3,
            Enabled: true,
            Comment: "For invalidation tests",
            Origins: {
              Quantity: 1,
              Items: [
                {
                  Id: originID3,
                  DomainName: "inv-test.example.com",
                  CustomOriginConfig: {
                    HTTPPort: 80,
                    HTTPSPort: 443,
                    OriginProtocolPolicy: "http-only",
                  },
                },
              ],
            },
            DefaultCacheBehavior: {
              TargetOriginId: originID3,
              ViewerProtocolPolicy: "allow-all",
              ForwardedValues: {
                QueryString: false,
                Cookies: { Forward: "none" },
              },
            },
            ViewerCertificate: { CloudFrontDefaultCertificate: true },
            Restrictions: {
              GeoRestriction: { RestrictionType: "none", Quantity: 0 },
            },
          },
        })
      );
      if (!resp.Distribution?.Id) throw new Error('expected Distribution.Id to be defined');
      invDistID = resp.Distribution.Id;
    })
  );

  let invID = "";
  if (invDistID) {
    results.push(
      await runner.runTest(svc, "CreateInvalidation", async () => {
        const resp = await client.send(
          new CreateInvalidationCommand({
            DistributionId: invDistID,
            InvalidationBatch: {
              CallerReference: makeUniqueName("inv-ref"),
              Paths: {
                Quantity: 2,
                Items: ["/index.html", "/images/*"],
              },
            },
          })
        );
        if (!resp.Invalidation?.Id) throw new Error('expected Invalidation.Id to be defined');
        invID = resp.Invalidation.Id;
      })
    );

    if (invID) {
      results.push(
        await runner.runTest(svc, "GetInvalidation", async () => {
          const resp = await client.send(
            new GetInvalidationCommand({
              DistributionId: invDistID,
              Id: invID,
            })
          );
          if (!resp.Invalidation) throw new Error("invalidation to be defined");
          if (!resp.Invalidation.Status) throw new Error("invalidation status to be defined");
        })
      );
    }

    results.push(
      await runner.runTest(svc, "ListInvalidations", async () => {
        const resp = await client.send(
          new ListInvalidationsCommand({ DistributionId: invDistID })
        );
        if (!resp.InvalidationList) throw new Error("invalidation list to be defined");
        if (!resp.InvalidationList.Quantity || resp.InvalidationList.Quantity < 1) {
          throw new Error("expected at least 1 invalidation, got 0");
        }
      })
    );

    results.push(
      await runner.runTest(svc, "Cleanup_InvalidationDist", async () => {
        const getResp = await client.send(
          new GetDistributionConfigCommand({ Id: invDistID })
        );
        if (!getResp.DistributionConfig) throw new Error("no config");
        getResp.DistributionConfig.Enabled = false;
        const updateResp = await client.send(
          new UpdateDistributionCommand({
            Id: invDistID,
            IfMatch: getResp.ETag || "",
            DistributionConfig: getResp.DistributionConfig,
          })
        );
        if (!updateResp.ETag) throw new Error("update ETag to be defined");
        await client.send(
          new DeleteDistributionCommand({
            Id: invDistID,
            IfMatch: updateResp.ETag,
          })
        );
      })
    );
  }

  return results;
};
