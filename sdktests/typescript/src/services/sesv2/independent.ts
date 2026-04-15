import {
  SESv2Client,
  GetAccountCommand,
  PutAccountDetailsCommand,
  PutAccountSendingAttributesCommand,
  PutAccountSuppressionAttributesCommand,
  PutAccountVdmAttributesCommand,
  PutAccountDedicatedIpWarmupAttributesCommand,
  ListEmailIdentitiesCommand,
  SendEmailCommand,
  CreateEmailIdentityCommand,
  DeleteEmailIdentityCommand,
  CreateContactListCommand,
  GetContactListCommand,
  ListContactListsCommand,
  DeleteContactListCommand,
  UpdateContactListCommand,
  CreateContactCommand,
  GetContactCommand,
  ListContactsCommand,
  DeleteContactCommand,
  UpdateContactCommand,
  ListSuppressedDestinationsCommand,
  DeleteSuppressedDestinationCommand,
  PutSuppressedDestinationCommand,
  GetSuppressedDestinationCommand,
  CreateEmailTemplateCommand,
  GetEmailTemplateCommand,
  UpdateEmailTemplateCommand,
  DeleteEmailTemplateCommand,
  ListEmailTemplatesCommand,
  TestRenderEmailTemplateCommand,
  CreateDedicatedIpPoolCommand,
  GetDedicatedIpPoolCommand,
  ListDedicatedIpPoolsCommand,
  DeleteDedicatedIpPoolCommand,
} from '@aws-sdk/client-sesv2';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runIndependentTests(
  runner: TestRunner,
  client: SESv2Client,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('sesv2', 'GetAccount', async () => {
    const resp = await client.send(new GetAccountCommand({}));
    if (!resp) throw new Error('response to be defined');
  }));

  results.push(await runner.runTest('sesv2', 'ListEmailIdentities', async () => {
    const resp = await client.send(new ListEmailIdentitiesCommand({}));
    if (resp.EmailIdentities === undefined) throw new Error('EmailIdentities is undefined');
  }));

  const contactListName = makeUniqueName('ts-sesv2-cl');

  results.push(await runner.runTest('sesv2', 'CreateContactList', async () => {
    await client.send(new CreateContactListCommand({
      ContactListName: contactListName,
      Topics: [{ TopicName: 'test-topic', DisplayName: 'Test Topic', Description: 'Test description', DefaultSubscriptionStatus: 'OPT_IN' }],
    }));
  }));

  results.push(await runner.runTest('sesv2', 'GetContactList', async () => {
    const resp = await client.send(new GetContactListCommand({ ContactListName: contactListName }));
    if (!resp.ContactListName) throw new Error('ContactListName is null');
  }));

  results.push(await runner.runTest('sesv2', 'ListContactLists', async () => {
    const resp = await client.send(new ListContactListsCommand({}));
    if (resp.ContactLists === undefined) throw new Error('ContactLists is undefined');
  }));

  const contactEmail = `contact-${makeUniqueName('sesv2')}@example.com`;
  results.push(await runner.runTest('sesv2', 'CreateContact', async () => {
    await client.send(new CreateContactCommand({
      ContactListName: contactListName, EmailAddress: contactEmail,
      AttributesData: JSON.stringify({ name: 'Test Contact' }),
    }));
  }));

  results.push(await runner.runTest('sesv2', 'GetContact', async () => {
    const resp = await client.send(new GetContactCommand({ ContactListName: contactListName, EmailAddress: contactEmail }));
    if (!resp.EmailAddress) throw new Error('EmailAddress is null');
  }));

  results.push(await runner.runTest('sesv2', 'ListContacts', async () => {
    const resp = await client.send(new ListContactsCommand({ ContactListName: contactListName }));
    if (resp.Contacts === undefined) throw new Error('Contacts is undefined');
  }));

  results.push(await runner.runTest('sesv2', 'DeleteContact', async () => {
    await client.send(new DeleteContactCommand({ ContactListName: contactListName, EmailAddress: contactEmail }));
  }));

  results.push(await runner.runTest('sesv2', 'DeleteContactList', async () => {
    await client.send(new DeleteContactListCommand({ ContactListName: contactListName }));
  }));

  results.push(await runner.runTest('sesv2', 'PutAccountDetails', async () => {
    await client.send(new PutAccountDetailsCommand({
      MailType: 'DEVELOPER' as any, WebsiteURL: 'https://example.com', ContactLanguage: 'EN',
    }));
  }));

  const suppressedEmail = `suppressed-${makeUniqueName('sesv2')}@example.com`;

  results.push(await runner.runTest('sesv2', 'PutSuppressedDestination', async () => {
    await client.send(new PutSuppressedDestinationCommand({
      EmailAddress: suppressedEmail,
      Reason: 'BOUNCE' as any,
    }));
  }));

  results.push(await runner.runTest('sesv2', 'GetSuppressedDestination', async () => {
    const resp = await client.send(new GetSuppressedDestinationCommand({
      EmailAddress: suppressedEmail,
    }));
    if (!resp.SuppressedDestination) throw new Error('SuppressedDestination is undefined');
  }));

  results.push(await runner.runTest('sesv2', 'ListSuppressedDestinations', async () => {
    const resp = await client.send(new ListSuppressedDestinationsCommand({}));
    if (resp.SuppressedDestinationSummaries === undefined) throw new Error('SuppressedDestinationSummaries is undefined');
  }));

  results.push(await runner.runTest('sesv2', 'DeleteSuppressedDestination', async () => {
    await client.send(new DeleteSuppressedDestinationCommand({
      EmailAddress: suppressedEmail,
    }));
  }));

  const templateName = makeUniqueName('ts-sesv2-tpl');

  results.push(await runner.runTest('sesv2', 'CreateEmailTemplate', async () => {
    await client.send(new CreateEmailTemplateCommand({
      TemplateName: templateName,
      TemplateContent: { Subject: 'Hello {{name}}', Html: '<h1>Hello {{name}}</h1>', Text: 'Hello {{name}}' },
    }));
  }));

  results.push(await runner.runTest('sesv2', 'GetEmailTemplate', async () => {
    const resp = await client.send(new GetEmailTemplateCommand({ TemplateName: templateName }));
    if (!resp.TemplateContent) throw new Error('TemplateContent is null');
  }));

  results.push(await runner.runTest('sesv2', 'UpdateEmailTemplate', async () => {
    await client.send(new UpdateEmailTemplateCommand({
      TemplateName: templateName,
      TemplateContent: { Subject: 'Updated {{name}}', Html: '<h1>Updated {{name}}</h1>', Text: 'Updated {{name}}' },
    }));
  }));

  results.push(await runner.runTest('sesv2', 'DeleteEmailTemplate', async () => {
    await client.send(new DeleteEmailTemplateCommand({ TemplateName: templateName }));
  }));

  results.push(await runner.runTest('sesv2', 'SendEmail_MultiByteSubject', async () => {
    const mbEmail = `mb-${makeUniqueName('sesv2')}@example.com`;
    try {
      await client.send(new CreateEmailIdentityCommand({ EmailIdentity: mbEmail }));
      await client.send(new SendEmailCommand({
        FromEmailAddress: mbEmail,
        Destination: { ToAddresses: [mbEmail] },
        Content: { Simple: { Subject: { Data: 'テスト件名', Charset: 'UTF-8' }, Body: { Text: { Data: 'テスト本文', Charset: 'UTF-8' } } } },
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteEmailIdentityCommand({ EmailIdentity: mbEmail })));
    }
  }));

  results.push(await runner.runTest('sesv2', 'PutAccountSendingAttributes', async () => {
    await client.send(new PutAccountSendingAttributesCommand({ SendingEnabled: true }));
  }));

  results.push(await runner.runTest('sesv2', 'PutAccountSuppressionAttributes', async () => {
    await client.send(new PutAccountSuppressionAttributesCommand({
      SuppressedReasons: ['BOUNCE', 'COMPLAINT'],
    }));
  }));

  results.push(await runner.runTest('sesv2', 'PutAccountVdmAttributes', async () => {
    await client.send(new PutAccountVdmAttributesCommand({ VdmAttributes: { VdmEnabled: 'ENABLED' } }));
  }));

  results.push(await runner.runTest('sesv2', 'PutAccountDedicatedIpWarmupAttributes', async () => {
    await client.send(new PutAccountDedicatedIpWarmupAttributesCommand({ AutoWarmupEnabled: false }));
  }));

  results.push(await runner.runTest('sesv2', 'ListEmailTemplates', async () => {
    const resp = await client.send(new ListEmailTemplatesCommand({ PageSize: 10 }));
    if (resp.TemplatesMetadata === undefined) throw new Error('templates metadata is undefined');
  }));

  results.push(await runner.runTest('sesv2', 'TestRenderEmailTemplate', async () => {
    const renderTpl = makeUniqueName('ts-sesv2-render');
    try {
      await client.send(new CreateEmailTemplateCommand({
        TemplateName: renderTpl,
        TemplateContent: { Subject: 'Hello {{name}}', Html: '<p>{{name}}</p>', Text: '{{name}}' },
      }));
      const resp = await client.send(new TestRenderEmailTemplateCommand({
        TemplateName: renderTpl, TemplateData: '{"name":"World"}',
      }));
      if (resp.RenderedTemplate === undefined) throw new Error('rendered template is undefined');
    } finally {
      await safeCleanup(() => client.send(new DeleteEmailTemplateCommand({ TemplateName: renderTpl })));
    }
  }));

  const poolName = makeUniqueName('ts-sesv2-pool');
  try {
    results.push(await runner.runTest('sesv2', 'CreateDedicatedIpPool', async () => {
      await client.send(new CreateDedicatedIpPoolCommand({ PoolName: poolName, ScalingMode: 'STANDARD' }));
    }));

    results.push(await runner.runTest('sesv2', 'GetDedicatedIpPool', async () => {
      const resp = await client.send(new GetDedicatedIpPoolCommand({ PoolName: poolName }));
      if (!resp.DedicatedIpPool?.PoolName || resp.DedicatedIpPool.PoolName !== poolName) {
        throw new Error(`expected pool name ${poolName}, got ${resp.DedicatedIpPool}`);
      }
    }));

    results.push(await runner.runTest('sesv2', 'ListDedicatedIpPools', async () => {
      const resp = await client.send(new ListDedicatedIpPoolsCommand({ PageSize: 10 }));
      if (resp.DedicatedIpPools === undefined) throw new Error('dedicated IP pools list is undefined');
    }));
  } finally {
    await safeCleanup(() => client.send(new DeleteDedicatedIpPoolCommand({ PoolName: poolName })));
  }

  results.push(await runner.runTest('sesv2', 'DeleteDedicatedIpPool', async () => {
    const delPool = makeUniqueName('ts-sesv2-delpool');
    await client.send(new CreateDedicatedIpPoolCommand({ PoolName: delPool, ScalingMode: 'STANDARD' }));
    await client.send(new DeleteDedicatedIpPoolCommand({ PoolName: delPool }));
  }));

  results.push(await runner.runTest('sesv2', 'PutSuppressedDestination', async () => {
    await client.send(new PutSuppressedDestinationCommand({
      EmailAddress: `suppressed-${makeUniqueName('sesv2')}@example.com`, Reason: 'BOUNCE',
    }));
  }));

  results.push(await runner.runTest('sesv2', 'GetSuppressedDestination', async () => {
    const supEmail = `gsup-${makeUniqueName('sesv2')}@example.com`;
    await client.send(new PutSuppressedDestinationCommand({ EmailAddress: supEmail, Reason: 'BOUNCE' }));
    const resp = await client.send(new GetSuppressedDestinationCommand({ EmailAddress: supEmail }));
    if (resp.SuppressedDestination === undefined) throw new Error('suppressed destination is undefined');
  }));

  const updateClName = makeUniqueName('ts-sesv2-update-cl');
  try {
    await client.send(new CreateContactListCommand({
      ContactListName: updateClName,
      Topics: [{ TopicName: 'Updates', DefaultSubscriptionStatus: 'OPT_IN', Description: 'Product updates', DisplayName: 'Updates' }],
    }));

    results.push(await runner.runTest('sesv2', 'UpdateContactList', async () => {
      await client.send(new UpdateContactListCommand({
        ContactListName: updateClName,
        Topics: [{ TopicName: 'Newsletter', DefaultSubscriptionStatus: 'OPT_IN', Description: 'Weekly newsletter', DisplayName: 'Newsletter' }],
      }));
    }));

    const updateContactEmail = `contact-${makeUniqueName('sesv2-update')}@example.com`;
    await client.send(new CreateContactCommand({
      ContactListName: updateClName, EmailAddress: updateContactEmail,
      TopicPreferences: [{ TopicName: 'Updates', SubscriptionStatus: 'OPT_IN' }],
    }));

    results.push(await runner.runTest('sesv2', 'UpdateContact', async () => {
      await client.send(new UpdateContactCommand({
        ContactListName: updateClName, EmailAddress: updateContactEmail,
        TopicPreferences: [{ TopicName: 'Newsletter', SubscriptionStatus: 'OPT_IN' }],
        UnsubscribeAll: false,
      }));
    }));
  } finally {
    await safeCleanup(() => client.send(new DeleteContactListCommand({ ContactListName: updateClName })));
  }

  return results;
}
