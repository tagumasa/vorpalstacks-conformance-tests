import {
  ListKeyGroupsCommand,
  CreateCachePolicyCommand,
  ListCachePoliciesCommand,
  GetCachePolicyCommand,
  DeleteCachePolicyCommand,
} from '@aws-sdk/client-cloudfront';
import { CloudFrontTestContext, CloudFrontTestSection } from './context.js';

export const runKeyGroupsTests: CloudFrontTestSection = async (ctx, runner) => {
  const results = [];
  const { client, svc } = ctx;

  results.push(
    await runner.runTest(svc, "ListKeyGroups", async () => {
      const resp = await client.send(new ListKeyGroupsCommand({ MaxItems: 10 }));
      if (!resp.KeyGroupList) throw new Error("key group list to be defined");
    })
  );

  const pgTs = Date.now();
  const pgIDs: string[] = [];
  results.push(
    await runner.runTest(svc, "ListCachePolicies_Pagination", async () => {
      const indices = [0, 1, 2, 3, 4];
      for (const i of indices) {
        const resp = await client.send(
          new CreateCachePolicyCommand({
            CachePolicyConfig: {
              Name: `pagcp-${pgTs}-${i}`,
              Comment: "pagination test",
              DefaultTTL: 3600,
              MaxTTL: 86400,
              MinTTL: 0,
            },
          })
        );
        if (resp.CachePolicy?.Id) pgIDs.push(resp.CachePolicy.Id);
      }

      let pageCount = 0;
      let totalCount = 0;
      let marker: string | undefined;
      try {
        do {
          const resp = await client.send(
            new ListCachePoliciesCommand({
              Marker: marker,
              MaxItems: 2,
            })
          );
          pageCount++;
          if (resp.CachePolicyList?.Items) {
            totalCount += resp.CachePolicyList.Items.length;
          }
          if (
            resp.CachePolicyList?.NextMarker &&
            resp.CachePolicyList.NextMarker !== ""
          ) {
            marker = resp.CachePolicyList.NextMarker;
          } else {
            marker = undefined;
          }
        } while (marker !== undefined);
      } finally {
        for (const id of pgIDs) {
          try {
            const gr = await client.send(
              new GetCachePolicyCommand({ Id: id })
            );
            if (gr.ETag) {
              await client.send(
                new DeleteCachePolicyCommand({
                  Id: id,
                  IfMatch: gr.ETag,
                })
              );
            }
          } catch {
            // ignore cleanup errors
          }
        }
      }

      if (pageCount < 2) {
        throw new Error(
          `expected at least 2 pages, got ${pageCount} (total items: ${totalCount})`
        );
      }
    })
  );

  return results;
};
