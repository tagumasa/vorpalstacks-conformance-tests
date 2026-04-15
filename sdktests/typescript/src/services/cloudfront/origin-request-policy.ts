import {
  ListOriginRequestPoliciesCommand,
  GetOriginRequestPolicyCommand,
  GetOriginRequestPolicyConfigCommand,
  CreateOriginRequestPolicyCommand,
  UpdateOriginRequestPolicyCommand,
  DeleteOriginRequestPolicyCommand,
} from '@aws-sdk/client-cloudfront';
import { CloudFrontTestContext, CloudFrontTestSection } from './context.js';
import { makeUniqueName, assertErrorContains } from '../../helpers.js';

export const runOriginRequestPolicyTests: CloudFrontTestSection = async (ctx, runner) => {
  const results = [];
  const { client, svc } = ctx;

  results.push(
    await runner.runTest(svc, "ListOriginRequestPolicies", async () => {
      const resp = await client.send(
        new ListOriginRequestPoliciesCommand({ MaxItems: 10 })
      );
      if (!resp.OriginRequestPolicyList) throw new Error("origin request policy list to be defined");
    })
  );

  results.push(
    await runner.runTest(svc, "GetOriginRequestPolicy_Managed", async () => {
      const resp = await client.send(
        new GetOriginRequestPolicyCommand({
          Id: "88a5eaf4-2fd4-4709-b370-b4c650ea3fcf",
        })
      );
      if (!resp.OriginRequestPolicy) throw new Error("origin request policy to be defined");
    })
  );

  let orpID = "";
  let orpETag = "";
  results.push(
    await runner.runTest(svc, "CreateOriginRequestPolicy", async () => {
      const resp = await client.send(
        new CreateOriginRequestPolicyCommand({
          OriginRequestPolicyConfig: {
            Name: makeUniqueName("test-orp"),
            Comment: "Test ORP",
            CookiesConfig: { CookieBehavior: "none" },
            HeadersConfig: { HeaderBehavior: "none" },
            QueryStringsConfig: { QueryStringBehavior: "none" },
          },
        })
      );
      if (!resp.OriginRequestPolicy?.Id) throw new Error('expected ORP.Id to be defined');
      orpID = resp.OriginRequestPolicy.Id;
      orpETag = resp.ETag || "";
    })
  );

  if (orpID) {
    results.push(
      await runner.runTest(svc, "GetOriginRequestPolicy_Custom", async () => {
        const resp = await client.send(
          new GetOriginRequestPolicyCommand({ Id: orpID })
        );
        if (!resp.OriginRequestPolicy) throw new Error("origin request policy to be defined");
      })
    );

    results.push(
      await runner.runTest(svc, "GetOriginRequestPolicyConfig", async () => {
        const resp = await client.send(
          new GetOriginRequestPolicyConfigCommand({ Id: orpID })
        );
        if (!resp.OriginRequestPolicyConfig) {
          throw new Error("origin request policy config to be defined");
        }
      })
    );

    results.push(
      await runner.runTest(svc, "UpdateOriginRequestPolicy", async () => {
        const resp = await client.send(
          new UpdateOriginRequestPolicyCommand({
            Id: orpID,
            IfMatch: orpETag,
            OriginRequestPolicyConfig: {
              Name: "updated-orp",
              Comment: "Updated test ORP",
              CookiesConfig: { CookieBehavior: "all" },
              HeadersConfig: { HeaderBehavior: "allViewer" },
              QueryStringsConfig: { QueryStringBehavior: "all" },
            },
          })
        );
        if (!resp.OriginRequestPolicy) throw new Error("updated ORP to be defined");
      })
    );

    results.push(
      await runner.runTest(svc, "DeleteOriginRequestPolicy", async () => {
        await client.send(
          new DeleteOriginRequestPolicyCommand({ Id: orpID })
        );
      })
    );

    results.push(
      await runner.runTest(svc, "GetOriginRequestPolicy_AfterDelete", async () => {
        let err: unknown;
        try {
          await client.send(
            new GetOriginRequestPolicyCommand({ Id: orpID })
          );
        } catch (e) {
          err = e;
        }
        assertErrorContains(err, "NoSuchOriginRequestPolicy");
      })
    );
  }

  return results;
};
