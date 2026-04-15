import {
  SESv2Client,
  CreateEmailIdentityCommand,
  GetEmailIdentityCommand,
  DeleteEmailIdentityCommand,
  PutEmailIdentityDkimAttributesCommand,
  PutEmailIdentityDkimSigningAttributesCommand,
  PutEmailIdentityFeedbackAttributesCommand,
  PutEmailIdentityMailFromAttributesCommand,
  PutEmailIdentityConfigurationSetAttributesCommand,
  CreateEmailIdentityPolicyCommand,
  GetEmailIdentityPoliciesCommand,
  UpdateEmailIdentityPolicyCommand,
  DeleteEmailIdentityPolicyCommand,
  CreateConfigurationSetCommand,
  DeleteConfigurationSetCommand,
  SendEmailCommand,
} from '@aws-sdk/client-sesv2';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runEmailIdentityTests(
  runner: TestRunner,
  client: SESv2Client,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const emailAddress = `test-${makeUniqueName('sesv2')}@example.com`;

  results.push(await runner.runTest('sesv2', 'CreateEmailIdentity_ForGet', async () => {
    await client.send(new CreateEmailIdentityCommand({ EmailIdentity: emailAddress }));
  }));

  results.push(await runner.runTest('sesv2', 'GetEmailIdentity', async () => {
    const resp = await client.send(new GetEmailIdentityCommand({ EmailIdentity: emailAddress }));
    if (resp.IdentityType !== 'EMAIL_ADDRESS') throw new Error(`expected EMAIL_ADDRESS, got ${resp.IdentityType}`);
  }));

  results.push(await runner.runTest('sesv2', 'DeleteEmailIdentity_Email', async () => {
    await client.send(new DeleteEmailIdentityCommand({ EmailIdentity: emailAddress }));
  }));

  const domainIdentity = makeUniqueName('ts-sesv2-domain') + '.example.com';
  const emailIdentity = `test-${makeUniqueName('sesv2-email')}@example.com`;

  results.push(await runner.runTest('sesv2', 'CreateEmailIdentity_Email', async () => {
    await client.send(new CreateEmailIdentityCommand({ EmailIdentity: emailIdentity }));
  }));

  results.push(await runner.runTest('sesv2', 'CreateEmailIdentity_Domain', async () => {
    const resp = await client.send(new CreateEmailIdentityCommand({ EmailIdentity: domainIdentity }));
    if (!resp) throw new Error('response is null');
    if (resp.DkimAttributes === undefined) throw new Error('DKIM attributes undefined for domain identity');
  }));

  results.push(await runner.runTest('sesv2', 'PutEmailIdentityDkimAttributes', async () => {
    await client.send(new PutEmailIdentityDkimAttributesCommand({
      EmailIdentity: domainIdentity, SigningEnabled: true,
    }));
  }));

  results.push(await runner.runTest('sesv2', 'PutEmailIdentityDkimSigningAttributes', async () => {
    await client.send(new PutEmailIdentityDkimSigningAttributesCommand({
      EmailIdentity: domainIdentity, SigningAttributesOrigin: 'AWS_SES',
    }));
  }));

  results.push(await runner.runTest('sesv2', 'PutEmailIdentityFeedbackAttributes', async () => {
    await client.send(new PutEmailIdentityFeedbackAttributesCommand({
      EmailIdentity: emailIdentity, EmailForwardingEnabled: true,
    }));
  }));

  results.push(await runner.runTest('sesv2', 'PutEmailIdentityMailFromAttributes', async () => {
    await client.send(new PutEmailIdentityMailFromAttributesCommand({
      EmailIdentity: domainIdentity,
      MailFromDomain: `mail.${domainIdentity}`,
      BehaviorOnMxFailure: 'USE_DEFAULT_VALUE',
    }));
  }));

  results.push(await runner.runTest('sesv2', 'PutEmailIdentityConfigurationSetAttributes', async () => {
    const csName2 = makeUniqueName('ts-sesv2-cs2');
    await client.send(new CreateConfigurationSetCommand({ ConfigurationSetName: csName2 }));
    try {
      await client.send(new PutEmailIdentityConfigurationSetAttributesCommand({
        EmailIdentity: emailIdentity, ConfigurationSetName: csName2,
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteConfigurationSetCommand({ ConfigurationSetName: csName2 })));
    }
  }));

  const policyName = makeUniqueName('ts-sesv2-policy');

  results.push(await runner.runTest('sesv2', 'CreateEmailIdentityPolicy', async () => {
    await client.send(new CreateEmailIdentityPolicyCommand({
      EmailIdentity: emailIdentity, PolicyName: policyName,
      Policy: '{"Version":"2008-10-17","Statement":[]}',
    }));
  }));

  results.push(await runner.runTest('sesv2', 'GetEmailIdentityPolicies', async () => {
    const resp = await client.send(new GetEmailIdentityPoliciesCommand({ EmailIdentity: emailIdentity }));
    if (resp.Policies === undefined) throw new Error('policies map is undefined');
  }));

  results.push(await runner.runTest('sesv2', 'UpdateEmailIdentityPolicy', async () => {
    await client.send(new UpdateEmailIdentityPolicyCommand({
      EmailIdentity: emailIdentity, PolicyName: policyName,
      Policy: '{"Version":"2008-10-17","Statement":[{"Effect":"Allow","Principal":"*","Action":"SES:SendEmail","Resource":"*"}]}',
    }));
  }));

  results.push(await runner.runTest('sesv2', 'DeleteEmailIdentityPolicy', async () => {
    await client.send(new DeleteEmailIdentityPolicyCommand({
      EmailIdentity: emailIdentity, PolicyName: policyName,
    }));
  }));

  results.push(await runner.runTest('sesv2', 'DeleteEmailIdentity_Domain', async () => {
    await client.send(new DeleteEmailIdentityCommand({ EmailIdentity: domainIdentity }));
  }));

  results.push(await runner.runTest('sesv2', 'SendEmail', async () => {
    const sendEmail = `send-${makeUniqueName('sesv2')}@example.com`;
    try {
      await client.send(new CreateEmailIdentityCommand({ EmailIdentity: sendEmail }));
      const resp = await client.send(new SendEmailCommand({
        FromEmailAddress: sendEmail,
        Destination: { ToAddresses: [sendEmail] },
        Content: { Simple: { Subject: { Data: 'Test Subject' }, Body: { Text: { Data: 'Test Body' } } } },
      }));
      if (!resp.MessageId) throw new Error('message ID is undefined');
    } finally {
      await safeCleanup(() => client.send(new DeleteEmailIdentityCommand({ EmailIdentity: sendEmail })));
    }
  }));

  return results;
}
