import {
  ListCachePoliciesCommand,
  GetCachePolicyCommand,
  GetCachePolicyConfigCommand,
  CreateCachePolicyCommand,
  UpdateCachePolicyCommand,
  DeleteCachePolicyCommand,
} from '@aws-sdk/client-cloudfront';
import { CloudFrontTestContext, CloudFrontTestSection } from './context.js';
import { makeUniqueName, assertErrorContains } from '../../helpers.js';

export const runCachePolicyTests: CloudFrontTestSection = async (ctx, runner) => {
  const results = [];
  const { client, svc } = ctx;

  results.push(
    await runner.runTest(svc, "ListCachePolicies", async () => {
      const resp = await client.send(
        new ListCachePoliciesCommand({ MaxItems: 10 })
      );
      if (!resp.CachePolicyList) throw new Error("cache policy list to be defined");
    })
  );

  results.push(
    await runner.runTest(svc, "GetCachePolicy_Managed", async () => {
      const resp = await client.send(
        new GetCachePolicyCommand({
          Id: "658327ea-f89d-4fab-a63d-7e88639e58f6",
        })
      );
      if (!resp.CachePolicy) throw new Error("cache policy to be defined");
    })
  );

  results.push(
    await runner.runTest(svc, "GetCachePolicyConfig_Managed", async () => {
      const resp = await client.send(
        new GetCachePolicyConfigCommand({
          Id: "658327ea-f89d-4fab-a63d-7e88639e58f6",
        })
      );
      if (!resp.CachePolicyConfig) throw new Error("cache policy config to be defined");
    })
  );

  let cachePolicyID = "";
  let cachePolicyETag = "";
  results.push(
    await runner.runTest(svc, "CreateCachePolicy", async () => {
      const resp = await client.send(
        new CreateCachePolicyCommand({
          CachePolicyConfig: {
            Name: makeUniqueName("test-cp"),
            Comment: "Test cache policy",
            DefaultTTL: 3600,
            MaxTTL: 86400,
            MinTTL: 0,
          },
        })
      );
      if (!resp.CachePolicy?.Id) throw new Error('expected CachePolicy.Id to be defined');
      cachePolicyID = resp.CachePolicy.Id;
      cachePolicyETag = resp.ETag || "";
    })
  );

  if (cachePolicyID) {
    results.push(
      await runner.runTest(svc, "GetCachePolicy_Custom", async () => {
        const resp = await client.send(
          new GetCachePolicyCommand({ Id: cachePolicyID })
        );
        if (!resp.CachePolicy) throw new Error("cache policy to be defined");
      })
    );

    results.push(
      await runner.runTest(svc, "GetCachePolicyConfig_Custom", async () => {
        const resp = await client.send(
          new GetCachePolicyConfigCommand({ Id: cachePolicyID })
        );
        if (!resp.CachePolicyConfig) throw new Error("cache policy config to be defined");
      })
    );

    results.push(
      await runner.runTest(svc, "UpdateCachePolicy", async () => {
        const resp = await client.send(
          new UpdateCachePolicyCommand({
            Id: cachePolicyID,
            IfMatch: cachePolicyETag,
            CachePolicyConfig: {
              Name: "updated-cache-policy",
              Comment: "Updated test cache policy",
              DefaultTTL: 1800,
              MaxTTL: 7200,
              MinTTL: 60,
            },
          })
        );
        if (!resp.CachePolicy) throw new Error("updated cache policy to be defined");
        if (resp.CachePolicy.CachePolicyConfig?.DefaultTTL !== 1800) {
          throw new Error("cache policy DefaultTTL not updated");
        }
      })
    );

    results.push(
      await runner.runTest(svc, "DeleteCachePolicy", async () => {
        await client.send(
          new DeleteCachePolicyCommand({ Id: cachePolicyID })
        );
      })
    );

    results.push(
      await runner.runTest(svc, "GetCachePolicy_AfterDelete", async () => {
        let err: unknown;
        try {
          await client.send(
            new GetCachePolicyCommand({ Id: cachePolicyID })
          );
        } catch (e) {
          err = e;
        }
        assertErrorContains(err, "NoSuchCachePolicy");
      })
    );
  }

  return results;
};
