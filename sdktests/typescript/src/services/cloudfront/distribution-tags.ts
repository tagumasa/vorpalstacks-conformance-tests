import {
  CreateDistributionWithTagsCommand,
  GetDistributionConfigCommand,
  UpdateDistributionCommand,
  DeleteDistributionCommand,
  ListTagsForResourceCommand,
  TagResourceCommand,
  UntagResourceCommand,
} from '@aws-sdk/client-cloudfront';
import { CloudFrontTestContext, CloudFrontTestSection } from './context.js';
import { makeUniqueName } from '../../helpers.js';

export const runDistributionTagsTests: CloudFrontTestSection = async (ctx, runner) => {
  const results = [];
  const { client, svc } = ctx;

  const callerRef2 = makeUniqueName("cftags");
  const originID2 = makeUniqueName("cf-origin2");
  let taggedDistID = "";
  let taggedDistETag = "";
  let taggedDistARN = "";
  results.push(
    await runner.runTest(svc, "CreateDistributionWithTags", async () => {
      const resp = await client.send(
        new CreateDistributionWithTagsCommand({
          DistributionConfigWithTags: {
            DistributionConfig: {
              CallerReference: callerRef2,
              Enabled: true,
              Comment: "Tagged distribution",
              Origins: {
                Quantity: 1,
                Items: [
                  {
                    Id: originID2,
                    DomainName: "example.org",
                    CustomOriginConfig: {
                      HTTPPort: 80,
                      HTTPSPort: 443,
                      OriginProtocolPolicy: "http-only",
                    },
                  },
                ],
              },
              DefaultCacheBehavior: {
                TargetOriginId: originID2,
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
            Tags: {
              Items: [
                { Key: "Environment", Value: "test" },
                { Key: "Owner", Value: "sdk-tests" },
              ],
            },
          },
        })
      );
      if (!resp.Distribution?.Id) throw new Error('expected Distribution.Id to be defined');
      taggedDistID = resp.Distribution.Id;
      taggedDistETag = resp.ETag || "";
      taggedDistARN = resp.Distribution.ARN || "";
    })
  );

  if (taggedDistARN) {
    results.push(
      await runner.runTest(svc, "ListTagsForResource_Distribution", async () => {
        const resp = await client.send(
          new ListTagsForResourceCommand({ Resource: taggedDistARN })
        );
        if (resp.Tags === undefined) throw new Error("tags to be defined");
        if (!resp.Tags.Items || resp.Tags.Items.length < 2) {
          throw new Error(`expected at least 2 tags, got ${resp.Tags.Items?.length ?? 0}`);
        }
      })
    );

    results.push(
      await runner.runTest(svc, "TagResource_Distribution", async () => {
        await client.send(
          new TagResourceCommand({
            Resource: taggedDistARN,
            Tags: {
              Items: [{ Key: "ExtraTag", Value: "extra-value" }],
            },
          })
        );
      })
    );

    results.push(
      await runner.runTest(svc, "ListTagsForResource_AfterTag", async () => {
        const resp = await client.send(
          new ListTagsForResourceCommand({ Resource: taggedDistARN })
        );
        if (!resp.Tags?.Items || resp.Tags.Items.length < 3) {
          throw new Error(
            `expected at least 3 tags after TagResource, got ${resp.Tags?.Items?.length ?? 0}`
          );
        }
      })
    );

    results.push(
      await runner.runTest(svc, "UntagResource_Distribution", async () => {
        await client.send(
          new UntagResourceCommand({
            Resource: taggedDistARN,
            TagKeys: { Items: ["ExtraTag"] },
          })
        );
      })
    );

    results.push(
      await runner.runTest(svc, "ListTagsForResource_AfterUntag", async () => {
        const resp = await client.send(
          new ListTagsForResourceCommand({ Resource: taggedDistARN })
        );
        if (!resp.Tags?.Items || resp.Tags.Items.length < 2) {
          throw new Error(
            `expected at least 2 tags after UntagResource, got ${resp.Tags?.Items?.length ?? 0}`
          );
        }
        for (const t of resp.Tags.Items) {
          if (t.Key === "ExtraTag") {
            throw new Error("ExtraTag should have been removed");
          }
        }
      })
    );
  }

  if (taggedDistID && taggedDistETag) {
    results.push(
      await runner.runTest(svc, "Cleanup_TaggedDistribution", async () => {
        const getResp = await client.send(
          new GetDistributionConfigCommand({ Id: taggedDistID })
        );
        if (!getResp.DistributionConfig) throw new Error("no config");
        getResp.DistributionConfig.Enabled = false;
        const updateResp = await client.send(
          new UpdateDistributionCommand({
            Id: taggedDistID,
            IfMatch: taggedDistETag,
            DistributionConfig: getResp.DistributionConfig,
          })
        );
        if (!updateResp.ETag) throw new Error("update ETag to be defined");
        await client.send(
          new DeleteDistributionCommand({
            Id: taggedDistID,
            IfMatch: updateResp.ETag,
          })
        );
      })
    );
  }

  return results;
};
