import {
  ListResponseHeadersPoliciesCommand,
  GetResponseHeadersPolicyCommand,
  GetResponseHeadersPolicyConfigCommand,
  CreateResponseHeadersPolicyCommand,
  UpdateResponseHeadersPolicyCommand,
  DeleteResponseHeadersPolicyCommand,
} from '@aws-sdk/client-cloudfront';
import { CloudFrontTestContext, CloudFrontTestSection } from './context.js';
import { makeUniqueName, assertErrorContains } from '../../helpers.js';

export const runResponseHeadersPolicyTests: CloudFrontTestSection = async (ctx, runner) => {
  const results = [];
  const { client, svc } = ctx;

  results.push(
    await runner.runTest(svc, "ListResponseHeadersPolicies", async () => {
      const resp = await client.send(
        new ListResponseHeadersPoliciesCommand({ MaxItems: 10 })
      );
      if (!resp.ResponseHeadersPolicyList) {
        throw new Error("response headers policy list to be defined");
      }
    })
  );

  const rhpName = makeUniqueName("test-rhp");
  let rhpID = "";
  let rhpETag = "";
  results.push(
    await runner.runTest(svc, "CreateResponseHeadersPolicy", async () => {
      const resp = await client.send(
        new CreateResponseHeadersPolicyCommand({
          ResponseHeadersPolicyConfig: {
            Name: rhpName,
            Comment: "Test RHP",
            ServerTimingHeadersConfig: {
              Enabled: true,
              SamplingRate: 0.5,
            },
            SecurityHeadersConfig: {
              XSSProtection: {
                Override: true,
                Protection: true,
                ModeBlock: true,
              },
              ContentTypeOptions: { Override: true },
            },
          },
        })
      );
      if (!resp.ResponseHeadersPolicy?.Id) throw new Error('expected RHP.Id to be defined');
      rhpID = resp.ResponseHeadersPolicy.Id;
      rhpETag = resp.ETag || "";
    })
  );

  if (rhpID) {
    results.push(
      await runner.runTest(svc, "GetResponseHeadersPolicy", async () => {
        const resp = await client.send(
          new GetResponseHeadersPolicyCommand({ Id: rhpID })
        );
        if (!resp.ResponseHeadersPolicy) throw new Error("response headers policy to be defined");
      })
    );

    results.push(
      await runner.runTest(svc, "GetResponseHeadersPolicyConfig", async () => {
        const resp = await client.send(
          new GetResponseHeadersPolicyConfigCommand({ Id: rhpID })
        );
        if (!resp.ResponseHeadersPolicyConfig) {
          throw new Error("response headers policy config to be defined");
        }
      })
    );

    results.push(
      await runner.runTest(svc, "UpdateResponseHeadersPolicy", async () => {
        const resp = await client.send(
          new UpdateResponseHeadersPolicyCommand({
            Id: rhpID,
            IfMatch: rhpETag,
            ResponseHeadersPolicyConfig: {
              Name: rhpName + "-updated",
              Comment: "Updated RHP",
              CustomHeadersConfig: {
                Quantity: 1,
                Items: [
                  {
                    Header: "X-Custom-Header",
                    Value: "custom-value",
                    Override: true,
                  },
                ],
              },
            },
          })
        );
        if (!resp.ResponseHeadersPolicy) throw new Error("updated RHP to be defined");
      })
    );

    results.push(
      await runner.runTest(svc, "DeleteResponseHeadersPolicy", async () => {
        await client.send(
          new DeleteResponseHeadersPolicyCommand({ Id: rhpID })
        );
      })
    );

    results.push(
      await runner.runTest(svc, "GetResponseHeadersPolicy_AfterDelete", async () => {
        let err: unknown;
        try {
          await client.send(
            new GetResponseHeadersPolicyCommand({ Id: rhpID })
          );
        } catch (e) {
          err = e;
        }
        assertErrorContains(err, "NoSuchResponseHeadersPolicy");
      })
    );
  }

  return results;
};
