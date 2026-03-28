import {
  SESv2Client,
  CreateEmailIdentityCommand,
  GetEmailIdentityCommand,
  ListEmailIdentitiesCommand,
  CreateEmailIdentityPolicyCommand,
  GetEmailIdentityPoliciesCommand,
  DeleteEmailIdentityPolicyCommand,
  PutEmailIdentityFeedbackAttributesCommand,
  SendEmailCommand,
  CreateContactListCommand,
  ListContactListsCommand,
  GetContactListCommand,
  DeleteContactListCommand,
  DeleteEmailIdentityCommand,
  GetAccountCommand,
} from "@aws-sdk/client-sesv2";
import { TestRunner, TestResult } from "../runner";

export async function runSESv2Tests(
  runner: TestRunner,
  client: SESv2Client,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const emailAddress = `test-${Date.now()}@example.com`;

  results.push(await runner.runTest("sesv2", "GetAccount", async () => {
    await client.send(new GetAccountCommand({}));
  }));

  results.push(await runner.runTest("sesv2", "CreateEmailIdentity", async () => {
    await client.send(new CreateEmailIdentityCommand({
      EmailIdentity: emailAddress,
    }));
  }));

  results.push(await runner.runTest("sesv2", "GetEmailIdentity", async () => {
    await client.send(new GetEmailIdentityCommand({
      EmailIdentity: emailAddress,
    }));
  }));

  results.push(await runner.runTest("sesv2", "ListEmailIdentities", async () => {
    await client.send(new ListEmailIdentitiesCommand({}));
  }));

  results.push(await runner.runTest("sesv2", "CreateEmailIdentityPolicy", async () => {
    await client.send(new CreateEmailIdentityPolicyCommand({
      EmailIdentity: emailAddress,
      PolicyName: "test-policy",
      Policy: JSON.stringify({
        Version: "2012-10-17",
        Statement: [{
          Effect: "Allow",
          Action: ["ses:SendEmail"],
          Resource: "*",
        }],
      }),
    }));
  }));

  results.push(await runner.runTest("sesv2", "GetEmailIdentityPolicies", async () => {
    await client.send(new GetEmailIdentityPoliciesCommand({
      EmailIdentity: emailAddress,
    }));
  }));

  results.push(await runner.runTest("sesv2", "DeleteEmailIdentityPolicy", async () => {
    await client.send(new DeleteEmailIdentityPolicyCommand({
      EmailIdentity: emailAddress,
      PolicyName: "test-policy",
    }));
  }));

  results.push(await runner.runTest("sesv2", "PutEmailIdentityFeedbackAttributes", async () => {
    await client.send(new PutEmailIdentityFeedbackAttributesCommand({
      EmailIdentity: emailAddress,
    }));
  }));

  results.push(await runner.runTest("sesv2", "SendEmail", async () => {
    await client.send(new SendEmailCommand({
      FromEmailAddress: emailAddress,
      Destination: {
        ToAddresses: [emailAddress],
      },
      Content: {
        Simple: {
          Subject: {
            Data: "Test Subject",
            Charset: "UTF-8",
          },
          Body: {
            Text: {
              Data: "Test Body",
              Charset: "UTF-8",
            },
          },
        },
      },
    }));
  }));

  const contactListName = `test-list-${Date.now()}`;
  results.push(await runner.runTest("sesv2", "CreateContactList", async () => {
    await client.send(new CreateContactListCommand({
      ContactListName: contactListName,
      Topics: [{
        TopicName: "test-topic",
        DisplayName: "Test Topic",
        Description: "Test description",
        DefaultSubscriptionStatus: "OPT_IN",
      }],
    }));
  }));

  results.push(await runner.runTest("sesv2", "ListContactLists", async () => {
    await client.send(new ListContactListsCommand({}));
  }));

  results.push(await runner.runTest("sesv2", "GetContactList", async () => {
    await client.send(new GetContactListCommand({
      ContactListName: contactListName,
    }));
  }));

  results.push(await runner.runTest("sesv2", "DeleteContactList", async () => {
    await client.send(new DeleteContactListCommand({
      ContactListName: contactListName,
    }));
  }));

  results.push(await runner.runTest("sesv2", "DeleteEmailIdentity", async () => {
    await client.send(new DeleteEmailIdentityCommand({
      EmailIdentity: emailAddress,
    }));
  }));

  const nonexistentEmail = `nonexistent-${Date.now()}@example.com`;
  results.push(await runner.runTest("sesv2", "GetEmailIdentity_NonExistent", async () => {
    try {
      await client.send(new GetEmailIdentityCommand({
        EmailIdentity: nonexistentEmail,
      }));
      throw new Error("expected error for non-existent email identity");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for non-existent email identity") {
        throw err;
      }
    }
  }));

  results.push(await runner.runTest("sesv2", "GetContactList_NonExistent", async () => {
    try {
      await client.send(new GetContactListCommand({
        ContactListName: "nonexistent-list-xyz",
      }));
      throw new Error("expected error for non-existent contact list");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for non-existent contact list") {
        throw err;
      }
    }
  }));

  results.push(await runner.runTest("sesv2", "CreateEmailIdentity_Duplicate", async () => {
    const dupEmail = `dup-${Date.now()}@example.com`;
    await client.send(new CreateEmailIdentityCommand({
      EmailIdentity: dupEmail,
    }));
    try {
      await client.send(new CreateEmailIdentityCommand({
        EmailIdentity: dupEmail,
      }));
      throw new Error("expected error for duplicate email identity");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for duplicate email identity") {
        throw err;
      }
    } finally {
      try {
        await client.send(new DeleteEmailIdentityCommand({
          EmailIdentity: dupEmail,
        }));
      } catch {
        // ignore cleanup
      }
    }
  }));

  results.push(await runner.runTest("sesv2", "ListEmailIdentities_VerifyCreated", async () => {
    const verifyEmail = `verify-${Date.now()}@example.com`;
    await client.send(new CreateEmailIdentityCommand({
      EmailIdentity: verifyEmail,
    }));
    const listResp = await client.send(new ListEmailIdentitiesCommand({}));
    const found = listResp.EmailIdentities?.some(
      e => e.IdentityName === verifyEmail
    );
    if (!found) {
      throw new Error("created email identity not found in list");
    }
    try {
      await client.send(new DeleteEmailIdentityCommand({
        EmailIdentity: verifyEmail,
      }));
    } catch {
      // ignore cleanup
    }
  }));

  return results;
}