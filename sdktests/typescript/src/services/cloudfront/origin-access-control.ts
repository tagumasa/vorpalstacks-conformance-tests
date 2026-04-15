import {
  ListOriginAccessControlsCommand,
  CreateOriginAccessControlCommand,
  GetOriginAccessControlCommand,
  GetOriginAccessControlConfigCommand,
  UpdateOriginAccessControlCommand,
  DeleteOriginAccessControlCommand,
} from '@aws-sdk/client-cloudfront';
import { CloudFrontTestContext, CloudFrontTestSection } from './context.js';
import { makeUniqueName } from '../../helpers.js';

export const runOriginAccessControlTests: CloudFrontTestSection = async (ctx, runner) => {
  const results = [];
  const { client, svc } = ctx;

  results.push(
    await runner.runTest(svc, "ListOriginAccessControls", async () => {
      const resp = await client.send(
        new ListOriginAccessControlsCommand({ MaxItems: 10 })
      );
      if (!resp.OriginAccessControlList) throw new Error("OAC list to be defined");
    })
  );

  const oacName = makeUniqueName("cf-oac");
  let oacID = "";
  let oacETag = "";
  results.push(
    await runner.runTest(svc, "CreateOriginAccessControl", async () => {
      const resp = await client.send(
        new CreateOriginAccessControlCommand({
          OriginAccessControlConfig: {
            Name: oacName,
            OriginAccessControlOriginType: "s3",
            SigningBehavior: "never",
            SigningProtocol: "sigv4",
          },
        })
      );
      if (!resp.OriginAccessControl?.Id) throw new Error('expected OAC.Id to be defined');
      oacID = resp.OriginAccessControl.Id;
      oacETag = resp.ETag || "";
    })
  );

  if (oacID) {
    results.push(
      await runner.runTest(svc, "GetOriginAccessControl", async () => {
        const resp = await client.send(
          new GetOriginAccessControlCommand({ Id: oacID })
        );
        if (!resp.OriginAccessControl) throw new Error("OAC to be defined");
      })
    );

    results.push(
      await runner.runTest(svc, "GetOriginAccessControlConfig", async () => {
        const resp = await client.send(
          new GetOriginAccessControlConfigCommand({ Id: oacID })
        );
        if (!resp.OriginAccessControlConfig) throw new Error("OAC config to be defined");
        if (resp.OriginAccessControlConfig.Name !== oacName) {
          throw new Error("OAC config name mismatch");
        }
      })
    );

    results.push(
      await runner.runTest(svc, "UpdateOriginAccessControl", async () => {
        const updatedName = oacName + "-updated";
        const resp = await client.send(
          new UpdateOriginAccessControlCommand({
            Id: oacID,
            IfMatch: oacETag,
            OriginAccessControlConfig: {
              Name: updatedName,
              OriginAccessControlOriginType: "s3",
              SigningBehavior: "always",
              SigningProtocol: "sigv4",
              Description: "updated description",
            },
          })
        );
        if (!resp.OriginAccessControl) throw new Error("updated OAC to be defined");
        if (resp.OriginAccessControl.OriginAccessControlConfig?.Name !== updatedName) {
          throw new Error("OAC name not updated");
        }
      })
    );

    results.push(
      await runner.runTest(svc, "DeleteOriginAccessControl", async () => {
        const resp = await client.send(
          new DeleteOriginAccessControlCommand({ Id: oacID })
        );
        if (!resp) throw new Error("response to be defined");
      })
    );
  }

  return results;
};
