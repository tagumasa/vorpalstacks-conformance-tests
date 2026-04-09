using Amazon;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class SESv2ServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonSimpleEmailServiceV2Client sesv2Client,
        string region)
    {
        var results = new List<TestResult>();
        var uid = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var emailAddress = $"test-{uid}@example.com";
        var policyName = $"test-policy-{uid}";
        var contactListName = $"test-contactlist-{uid}";
        var configSetName = TestRunner.MakeUniqueName("test-cs");
        var configSetNameTags = TestRunner.MakeUniqueName("test-cs-tags");
        var configSetNameEvent = TestRunner.MakeUniqueName("test-cs-event");
        var configSetNamePagination = TestRunner.MakeUniqueName("test-cs-pg1");
        var configSetNamePagination2 = TestRunner.MakeUniqueName("test-cs-pg2");
        var domainIdentity = TestRunner.MakeUniqueName("testdomain") + ".example.com";
        var domainIdentityDelete = TestRunner.MakeUniqueName("testdel") + ".example.com";
        var templateName = TestRunner.MakeUniqueName("test-template");
        var poolName = TestRunner.MakeUniqueName("test-pool");
        var poolNameDelete = TestRunner.MakeUniqueName("test-pool-del");
        var contactListName2 = TestRunner.MakeUniqueName("test-cl2");
        var contactListNameFull = TestRunner.MakeUniqueName("test-cl-full");
        var contactListNameUpdate = TestRunner.MakeUniqueName("test-cl-upd");
        var contactListNameContacts = TestRunner.MakeUniqueName("test-cl-contacts");
        var contactEmail = $"contact-{uid}@example.com";
        var contactEmail2 = $"contact2-{uid}@example.com";
        var suppressedEmail = $"suppressed-{uid}@example.com";

        try
        {
            results.Add(await runner.RunTestAsync("sesv2", "GetAccount", async () =>
            {
                var resp = await sesv2Client.GetAccountAsync(new GetAccountRequest());
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutAccountSendingAttributes", async () =>
            {
                var resp = await sesv2Client.PutAccountSendingAttributesAsync(new PutAccountSendingAttributesRequest
                {
                    SendingEnabled = true
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutAccountSuppressionAttributes", async () =>
            {
                var resp = await sesv2Client.PutAccountSuppressionAttributesAsync(new PutAccountSuppressionAttributesRequest
                {
                    SuppressedReasons = new List<string> { "BOUNCE", "COMPLAINT" }
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutAccountDetails", async () =>
            {
                var resp = await sesv2Client.PutAccountDetailsAsync(new PutAccountDetailsRequest
                {
                    MailType = "TRANSACTIONAL",
                    WebsiteURL = "https://example.com",
                    ContactLanguage = "EN",
                    UseCaseDescription = "Test use case"
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutAccountVdmAttributes", async () =>
            {
                var resp = await sesv2Client.PutAccountVdmAttributesAsync(new PutAccountVdmAttributesRequest
                {
                    VdmAttributes = new VdmAttributes
                    {
                        DashboardAttributes = new DashboardAttributes
                        {
                            EngagementMetrics = "ENABLED"
                        }
                    }
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutAccountDedicatedIpWarmupAttributes", async () =>
            {
                var resp = await sesv2Client.PutAccountDedicatedIpWarmupAttributesAsync(new PutAccountDedicatedIpWarmupAttributesRequest
                {
                    AutoWarmupEnabled = true
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "CreateConfigurationSet", async () =>
            {
                var resp = await sesv2Client.CreateConfigurationSetAsync(new CreateConfigurationSetRequest
                {
                    ConfigurationSetName = configSetName,
                    Tags = new List<Tag> { new Tag { Key = "Environment", Value = "test" } }
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "GetConfigurationSet", async () =>
            {
                var resp = await sesv2Client.GetConfigurationSetAsync(new GetConfigurationSetRequest
                {
                    ConfigurationSetName = configSetName
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "ListConfigurationSets", async () =>
            {
                var resp = await sesv2Client.ListConfigurationSetsAsync(new ListConfigurationSetsRequest
                {
                    PageSize = 10
                });
                if (resp.ConfigurationSets == null)
                    throw new Exception("configuration sets list is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutConfigurationSetSendingOptions", async () =>
            {
                var resp = await sesv2Client.PutConfigurationSetSendingOptionsAsync(new PutConfigurationSetSendingOptionsRequest
                {
                    ConfigurationSetName = configSetName,
                    SendingEnabled = true
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutConfigurationSetReputationOptions", async () =>
            {
                var resp = await sesv2Client.PutConfigurationSetReputationOptionsAsync(new PutConfigurationSetReputationOptionsRequest
                {
                    ConfigurationSetName = configSetName,
                    ReputationMetricsEnabled = true
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutConfigurationSetDeliveryOptions", async () =>
            {
                var resp = await sesv2Client.PutConfigurationSetDeliveryOptionsAsync(new PutConfigurationSetDeliveryOptionsRequest
                {
                    ConfigurationSetName = configSetName,
                    TlsPolicy = "REQUIRE"
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutConfigurationSetSuppressionOptions", async () =>
            {
                var resp = await sesv2Client.PutConfigurationSetSuppressionOptionsAsync(new PutConfigurationSetSuppressionOptionsRequest
                {
                    ConfigurationSetName = configSetName,
                    SuppressedReasons = new List<string> { "BOUNCE", "COMPLAINT" }
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutConfigurationSetTrackingOptions", async () =>
            {
                var resp = await sesv2Client.PutConfigurationSetTrackingOptionsAsync(new PutConfigurationSetTrackingOptionsRequest
                {
                    ConfigurationSetName = configSetName,
                    CustomRedirectDomain = "example.com"
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutConfigurationSetVdmOptions", async () =>
            {
                var resp = await sesv2Client.PutConfigurationSetVdmOptionsAsync(new PutConfigurationSetVdmOptionsRequest
                {
                    ConfigurationSetName = configSetName,
                    VdmOptions = new VdmOptions
                    {
                        DashboardOptions = new DashboardOptions
                        {
                            EngagementMetrics = "ENABLED"
                        }
                    }
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutConfigurationSetArchivingOptions", async () =>
            {
                var resp = await sesv2Client.PutConfigurationSetArchivingOptionsAsync(new PutConfigurationSetArchivingOptionsRequest
                {
                    ConfigurationSetName = configSetName
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "CreateConfigurationSetEventDestination", async () =>
            {
                try
                {
                    await sesv2Client.CreateConfigurationSetAsync(new CreateConfigurationSetRequest
                    {
                        ConfigurationSetName = configSetNameEvent
                    });
                }
                catch { }

                var resp = await sesv2Client.CreateConfigurationSetEventDestinationAsync(new CreateConfigurationSetEventDestinationRequest
                {
                    ConfigurationSetName = configSetNameEvent,
                    EventDestinationName = "test-event-dest",
                    EventDestination = new EventDestinationDefinition
                    {
                        Enabled = true,
                        MatchingEventTypes = new List<string> { "SEND", "DELIVERY", "BOUNCE" },
                        SnsDestination = new SnsDestination
                        {
                            TopicArn = $"arn:aws:sns:{region}:000000000000:test-topic"
                        }
                    }
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "GetConfigurationSetEventDestinations", async () =>
            {
                var resp = await sesv2Client.GetConfigurationSetEventDestinationsAsync(new GetConfigurationSetEventDestinationsRequest
                {
                    ConfigurationSetName = configSetNameEvent
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "UpdateConfigurationSetEventDestination", async () =>
            {
                var resp = await sesv2Client.UpdateConfigurationSetEventDestinationAsync(new UpdateConfigurationSetEventDestinationRequest
                {
                    ConfigurationSetName = configSetNameEvent,
                    EventDestinationName = "test-event-dest",
                    EventDestination = new EventDestinationDefinition
                    {
                        Enabled = false,
                        MatchingEventTypes = new List<string> { "SEND", "DELIVERY" },
                        SnsDestination = new SnsDestination
                        {
                            TopicArn = $"arn:aws:sns:{region}:000000000000:test-topic"
                        }
                    }
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "DeleteConfigurationSetEventDestination", async () =>
            {
                var resp = await sesv2Client.DeleteConfigurationSetEventDestinationAsync(new DeleteConfigurationSetEventDestinationRequest
                {
                    ConfigurationSetName = configSetNameEvent,
                    EventDestinationName = "test-event-dest"
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "TagResource_ConfigSet", async () =>
            {
                try
                {
                    await sesv2Client.CreateConfigurationSetAsync(new CreateConfigurationSetRequest
                    {
                        ConfigurationSetName = configSetNameTags
                    });
                }
                catch { }

                var resp = await sesv2Client.TagResourceAsync(new TagResourceRequest
                {
                    ResourceArn = $"arn:aws:ses:{region}:000000000000:configuration-set/{configSetNameTags}",
                    Tags = new List<Tag> { new Tag { Key = "Name", Value = "test-tag" } }
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "ListTagsForResource_ConfigSet", async () =>
            {
                var resp = await sesv2Client.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceArn = $"arn:aws:ses:{region}:000000000000:configuration-set/{configSetNameTags}"
                });
                if (resp.Tags == null)
                    throw new Exception("tags is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "UntagResource_ConfigSet", async () =>
            {
                var resp = await sesv2Client.UntagResourceAsync(new UntagResourceRequest
                {
                    ResourceArn = $"arn:aws:ses:{region}:000000000000:configuration-set/{configSetNameTags}",
                    TagKeys = new List<string> { "Name" }
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "DeleteConfigurationSet", async () =>
            {
                var delName = TestRunner.MakeUniqueName("test-cs-del");
                try
                {
                    await sesv2Client.CreateConfigurationSetAsync(new CreateConfigurationSetRequest
                    {
                        ConfigurationSetName = delName
                    });
                }
                catch { }

                var resp = await sesv2Client.DeleteConfigurationSetAsync(new DeleteConfigurationSetRequest
                {
                    ConfigurationSetName = delName
                });
                if (resp == null)
                    throw new Exception("response is null");

                try
                {
                    await sesv2Client.GetConfigurationSetAsync(new GetConfigurationSetRequest
                    {
                        ConfigurationSetName = delName
                    });
                    throw new Exception("expected error for deleted configuration set");
                }
                catch (AmazonSimpleEmailServiceV2Exception) { }
            }));

            results.Add(await runner.RunTestAsync("sesv2", "CreateEmailIdentity", async () =>
            {
                var resp = await sesv2Client.CreateEmailIdentityAsync(new CreateEmailIdentityRequest
                {
                    EmailIdentity = emailAddress
                });
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "CreateEmailIdentity_Email", async () =>
            {
                var emailAddr2 = $"test-email-{TestRunner.MakeUniqueName("eid")}@example.com";
                try
                {
                    var resp = await sesv2Client.CreateEmailIdentityAsync(new CreateEmailIdentityRequest
                    {
                        EmailIdentity = emailAddr2
                    });
                    if (resp == null)
                        throw new Exception("response is null");
                }
                finally
                {
                    try { await sesv2Client.DeleteEmailIdentityAsync(new DeleteEmailIdentityRequest { EmailIdentity = emailAddr2 }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("sesv2", "CreateEmailIdentity_Domain", async () =>
            {
                var resp = await sesv2Client.CreateEmailIdentityAsync(new CreateEmailIdentityRequest
                {
                    EmailIdentity = domainIdentity
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "GetEmailIdentity", async () =>
            {
                var resp = await sesv2Client.GetEmailIdentityAsync(new GetEmailIdentityRequest
                {
                    EmailIdentity = emailAddress
                });
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "ListEmailIdentities", async () =>
            {
                var resp = await sesv2Client.ListEmailIdentitiesAsync(new ListEmailIdentitiesRequest
                {
                    PageSize = 10
                });
                if (resp.EmailIdentities == null)
                    throw new Exception("email identities list is nil");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutEmailIdentityDkimAttributes", async () =>
            {
                var resp = await sesv2Client.PutEmailIdentityDkimAttributesAsync(new PutEmailIdentityDkimAttributesRequest
                {
                    EmailIdentity = emailAddress,
                    SigningEnabled = true
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutEmailIdentityDkimSigningAttributes", async () =>
            {
                var resp = await sesv2Client.PutEmailIdentityDkimSigningAttributesAsync(new PutEmailIdentityDkimSigningAttributesRequest
                {
                    EmailIdentity = emailAddress,
                    SigningAttributesOrigin = "AWS_SES"
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutEmailIdentityFeedbackAttributes", async () =>
            {
                var resp = await sesv2Client.PutEmailIdentityFeedbackAttributesAsync(new PutEmailIdentityFeedbackAttributesRequest
                {
                    EmailIdentity = emailAddress,
                    EmailForwardingEnabled = true
                });
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutEmailIdentityMailFromAttributes", async () =>
            {
                var resp = await sesv2Client.PutEmailIdentityMailFromAttributesAsync(new PutEmailIdentityMailFromAttributesRequest
                {
                    EmailIdentity = domainIdentity,
                    MailFromDomain = $"mail.{domainIdentity}"
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutEmailIdentityConfigurationSetAttributes", async () =>
            {
                var resp = await sesv2Client.PutEmailIdentityConfigurationSetAttributesAsync(new PutEmailIdentityConfigurationSetAttributesRequest
                {
                    EmailIdentity = emailAddress,
                    ConfigurationSetName = configSetName
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "CreateEmailIdentityPolicy", async () =>
            {
                var resp = await sesv2Client.CreateEmailIdentityPolicyAsync(new CreateEmailIdentityPolicyRequest
                {
                    EmailIdentity = emailAddress,
                    PolicyName = policyName,
                    Policy = "{\"Version\":\"2008-10-17\",\"Statement\":[]}"
                });
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "GetEmailIdentityPolicies", async () =>
            {
                var resp = await sesv2Client.GetEmailIdentityPoliciesAsync(new GetEmailIdentityPoliciesRequest
                {
                    EmailIdentity = emailAddress
                });
                if (resp.Policies == null)
                    throw new Exception("policies map is nil");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "UpdateEmailIdentityPolicy", async () =>
            {
                var resp = await sesv2Client.UpdateEmailIdentityPolicyAsync(new UpdateEmailIdentityPolicyRequest
                {
                    EmailIdentity = emailAddress,
                    PolicyName = policyName,
                    Policy = "{\"Version\":\"2008-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Action\":\"ses:SendEmail\",\"Resource\":\"*\"}]}"
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "DeleteEmailIdentityPolicy", async () =>
            {
                var resp = await sesv2Client.DeleteEmailIdentityPolicyAsync(new DeleteEmailIdentityPolicyRequest
                {
                    EmailIdentity = emailAddress,
                    PolicyName = policyName
                });
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "DeleteEmailIdentity", async () =>
            {
                var resp = await sesv2Client.DeleteEmailIdentityAsync(new DeleteEmailIdentityRequest
                {
                    EmailIdentity = emailAddress
                });
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "DeleteEmailIdentity_Domain", async () =>
            {
                try
                {
                    await sesv2Client.CreateEmailIdentityAsync(new CreateEmailIdentityRequest
                    {
                        EmailIdentity = domainIdentityDelete
                    });
                }
                catch { }

                var resp = await sesv2Client.DeleteEmailIdentityAsync(new DeleteEmailIdentityRequest
                {
                    EmailIdentity = domainIdentityDelete
                });
                if (resp == null)
                    throw new Exception("response is null");

                try
                {
                    await sesv2Client.GetEmailIdentityAsync(new GetEmailIdentityRequest
                    {
                        EmailIdentity = domainIdentityDelete
                    });
                    throw new Exception("expected error for deleted domain identity");
                }
                catch (AmazonSimpleEmailServiceV2Exception) { }
            }));

            results.Add(await runner.RunTestAsync("sesv2", "CreateEmailTemplate", async () =>
            {
                var resp = await sesv2Client.CreateEmailTemplateAsync(new CreateEmailTemplateRequest
                {
                    TemplateContent = new EmailTemplateContent
                    {
                        Subject = "Test Subject {{subject}}",
                        Html = "<h1>Hello {{name}}</h1>",
                        Text = "Hello {{name}}"
                    },
                    TemplateName = templateName
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "GetEmailTemplate", async () =>
            {
                var resp = await sesv2Client.GetEmailTemplateAsync(new GetEmailTemplateRequest
                {
                    TemplateName = templateName
                });
                if (resp.TemplateContent == null)
                    throw new Exception("template content is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "ListEmailTemplates", async () =>
            {
                var resp = await sesv2Client.ListEmailTemplatesAsync(new ListEmailTemplatesRequest
                {
                    PageSize = 10
                });
                if (resp.TemplatesMetadata == null)
                    throw new Exception("templates metadata is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "UpdateEmailTemplate", async () =>
            {
                var resp = await sesv2Client.UpdateEmailTemplateAsync(new UpdateEmailTemplateRequest
                {
                    TemplateContent = new EmailTemplateContent
                    {
                        Subject = "Updated Subject {{subject}}",
                        Html = "<h1>Updated Hello {{name}}</h1>",
                        Text = "Updated Hello {{name}}"
                    },
                    TemplateName = templateName
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "TestRenderEmailTemplate", async () =>
            {
                var resp = await sesv2Client.TestRenderEmailTemplateAsync(new TestRenderEmailTemplateRequest
                {
                    TemplateName = templateName,
                    TemplateData = "{\"subject\":\"Test\",\"name\":\"World\"}"
                });
                if (string.IsNullOrEmpty(resp.RenderedTemplate))
                    throw new Exception("rendered template is null or empty");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "DeleteEmailTemplate", async () =>
            {
                var resp = await sesv2Client.DeleteEmailTemplateAsync(new DeleteEmailTemplateRequest
                {
                    TemplateName = templateName
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "SendEmail", async () =>
            {
                try
                {
                    await sesv2Client.CreateEmailIdentityAsync(new CreateEmailIdentityRequest
                    {
                        EmailIdentity = emailAddress
                    });
                }
                catch { }

                var resp = await sesv2Client.SendEmailAsync(new SendEmailRequest
                {
                    FromEmailAddress = emailAddress,
                    Destination = new Destination
                    {
                        ToAddresses = [emailAddress]
                    },
                    Content = new EmailContent
                    {
                        Simple = new Message
                        {
                            Subject = new Content
                            {
                                Data = "Test Subject"
                            },
                            Body = new Body
                            {
                                Text = new Content
                                {
                                    Data = "Test Body"
                                }
                            }
                        }
                    }
                });
                if (string.IsNullOrEmpty(resp.MessageId))
                    throw new Exception("message ID is nil");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "CreateDedicatedIpPool", async () =>
            {
                var resp = await sesv2Client.CreateDedicatedIpPoolAsync(new CreateDedicatedIpPoolRequest
                {
                    PoolName = poolName,
                    Tags = new List<Tag> { new Tag { Key = "Environment", Value = "test" } }
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "GetDedicatedIpPool", async () =>
            {
                var resp = await sesv2Client.GetDedicatedIpPoolAsync(new GetDedicatedIpPoolRequest
                {
                    PoolName = poolName
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "ListDedicatedIpPools", async () =>
            {
                var resp = await sesv2Client.ListDedicatedIpPoolsAsync(new ListDedicatedIpPoolsRequest
                {
                    PageSize = 10
                });
                if (resp.DedicatedIpPools == null)
                    throw new Exception("dedicated ip pools list is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "DeleteDedicatedIpPool", async () =>
            {
                try
                {
                    await sesv2Client.CreateDedicatedIpPoolAsync(new CreateDedicatedIpPoolRequest
                    {
                        PoolName = poolNameDelete
                    });
                }
                catch { }

                var resp = await sesv2Client.DeleteDedicatedIpPoolAsync(new DeleteDedicatedIpPoolRequest
                {
                    PoolName = poolNameDelete
                });
                if (resp == null)
                    throw new Exception("response is null");

                try
                {
                    await sesv2Client.GetDedicatedIpPoolAsync(new GetDedicatedIpPoolRequest
                    {
                        PoolName = poolNameDelete
                    });
                    throw new Exception("expected error for deleted dedicated ip pool");
                }
                catch (AmazonSimpleEmailServiceV2Exception) { }
            }));

            results.Add(await runner.RunTestAsync("sesv2", "PutSuppressedDestination", async () =>
            {
                var resp = await sesv2Client.PutSuppressedDestinationAsync(new PutSuppressedDestinationRequest
                {
                    EmailAddress = suppressedEmail,
                    Reason = SuppressionListReason.BOUNCE
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "GetSuppressedDestination", async () =>
            {
                var resp = await sesv2Client.GetSuppressedDestinationAsync(new GetSuppressedDestinationRequest
                {
                    EmailAddress = suppressedEmail
                });
                if (resp.SuppressedDestination == null)
                    throw new Exception("suppressed destination is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "ListSuppressedDestinations", async () =>
            {
                var resp = await sesv2Client.ListSuppressedDestinationsAsync(new ListSuppressedDestinationsRequest
                {
                    PageSize = 10
                });
                if (resp.SuppressedDestinationSummaries == null)
                    throw new Exception("suppressed destinations list is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "DeleteSuppressedDestination", async () =>
            {
                var resp = await sesv2Client.DeleteSuppressedDestinationAsync(new DeleteSuppressedDestinationRequest
                {
                    EmailAddress = suppressedEmail
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "CreateContactList", async () =>
            {
                var resp = await sesv2Client.CreateContactListAsync(new CreateContactListRequest
                {
                    ContactListName = contactListName
                });
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "ListContactLists", async () =>
            {
                var resp = await sesv2Client.ListContactListsAsync(new ListContactListsRequest
                {
                    PageSize = 10
                });
                if (resp.ContactLists == null)
                    throw new Exception("contact lists is nil");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "GetContactList", async () =>
            {
                var resp = await sesv2Client.GetContactListAsync(new GetContactListRequest
                {
                    ContactListName = contactListName
                });
                if (string.IsNullOrEmpty(resp.ContactListName))
                    throw new Exception("contact list name is nil");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "UpdateContactList", async () =>
            {
                try
                {
                    await sesv2Client.CreateContactListAsync(new CreateContactListRequest
                    {
                        ContactListName = contactListNameUpdate
                    });
                }
                catch { }

                var resp = await sesv2Client.UpdateContactListAsync(new UpdateContactListRequest
                {
                    ContactListName = contactListNameUpdate,
                    Description = "Updated description"
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "CreateContact", async () =>
            {
                try
                {
                    await sesv2Client.CreateContactListAsync(new CreateContactListRequest
                    {
                        ContactListName = contactListNameContacts
                    });
                }
                catch { }

                var resp = await sesv2Client.CreateContactAsync(new CreateContactRequest
                {
                    ContactListName = contactListNameContacts,
                    EmailAddress = contactEmail,
                    AttributesData = "{\"Name\":\"Test Contact\"}"
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "GetContact", async () =>
            {
                var resp = await sesv2Client.GetContactAsync(new GetContactRequest
                {
                    ContactListName = contactListNameContacts,
                    EmailAddress = contactEmail
                });
                if (string.IsNullOrEmpty(resp.EmailAddress))
                    throw new Exception("contact email address is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "ListContacts", async () =>
            {
                var resp = await sesv2Client.ListContactsAsync(new ListContactsRequest
                {
                    ContactListName = contactListNameContacts,
                    PageSize = 10
                });
                if (resp.Contacts == null)
                    throw new Exception("contacts list is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "UpdateContact", async () =>
            {
                var resp = await sesv2Client.UpdateContactAsync(new UpdateContactRequest
                {
                    ContactListName = contactListNameContacts,
                    EmailAddress = contactEmail,
                    AttributesData = "{\"Name\":\"Updated Contact\"}"
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "DeleteContact", async () =>
            {
                try
                {
                    await sesv2Client.CreateContactAsync(new CreateContactRequest
                    {
                        ContactListName = contactListNameContacts,
                        EmailAddress = contactEmail2
                    });
                }
                catch { }

                var resp = await sesv2Client.DeleteContactAsync(new DeleteContactRequest
                {
                    ContactListName = contactListNameContacts,
                    EmailAddress = contactEmail2
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("sesv2", "DeleteContactList", async () =>
            {
                try
                {
                    await sesv2Client.CreateContactListAsync(new CreateContactListRequest
                    {
                        ContactListName = contactListName2
                    });
                }
                catch { }

                var resp = await sesv2Client.DeleteContactListAsync(new DeleteContactListRequest
                {
                    ContactListName = contactListName2
                });
                if (resp == null)
                    throw new Exception("response is nil");

                try
                {
                    await sesv2Client.GetContactListAsync(new GetContactListRequest
                    {
                        ContactListName = contactListName2
                    });
                    throw new Exception("expected error for deleted contact list");
                }
                catch (AmazonSimpleEmailServiceV2Exception) { }
            }));

            results.Add(await runner.RunTestAsync("sesv2", "ListConfigurationSets_Pagination", async () =>
            {
                try
                {
                    await sesv2Client.CreateConfigurationSetAsync(new CreateConfigurationSetRequest
                    {
                        ConfigurationSetName = configSetNamePagination
                    });
                    await sesv2Client.CreateConfigurationSetAsync(new CreateConfigurationSetRequest
                    {
                        ConfigurationSetName = configSetNamePagination2
                    });
                }
                catch { }

                var allNames = new List<string>();
                var nextToken = "";
                do
                {
                    var resp = await sesv2Client.ListConfigurationSetsAsync(new ListConfigurationSetsRequest
                    {
                        PageSize = 1,
                        NextToken = string.IsNullOrEmpty(nextToken) ? null : nextToken
                    });
                    if (resp.ConfigurationSets != null)
                        allNames.AddRange(resp.ConfigurationSets);
                    nextToken = resp.NextToken ?? "";
                } while (!string.IsNullOrEmpty(nextToken));

                if (allNames.Count == 0)
                    throw new Exception("no configuration sets returned");
            }));
        }
        finally
        {
            try { await sesv2Client.DeleteContactListAsync(new DeleteContactListRequest { ContactListName = contactListName }); } catch { }
            try { await sesv2Client.DeleteContactListAsync(new DeleteContactListRequest { ContactListName = contactListNameContacts }); } catch { }
            try { await sesv2Client.DeleteContactListAsync(new DeleteContactListRequest { ContactListName = contactListNameUpdate }); } catch { }
            try { await sesv2Client.DeleteContactListAsync(new DeleteContactListRequest { ContactListName = contactListNameFull }); } catch { }
            try { await sesv2Client.DeleteContactListAsync(new DeleteContactListRequest { ContactListName = contactListName2 }); } catch { }
            try { await sesv2Client.DeleteEmailIdentityAsync(new DeleteEmailIdentityRequest { EmailIdentity = emailAddress }); } catch { }
            try { await sesv2Client.DeleteEmailIdentityAsync(new DeleteEmailIdentityRequest { EmailIdentity = domainIdentity }); } catch { }
            try { await sesv2Client.DeleteEmailIdentityAsync(new DeleteEmailIdentityRequest { EmailIdentity = domainIdentityDelete }); } catch { }
            try { await sesv2Client.DeleteConfigurationSetAsync(new DeleteConfigurationSetRequest { ConfigurationSetName = configSetName }); } catch { }
            try { await sesv2Client.DeleteConfigurationSetAsync(new DeleteConfigurationSetRequest { ConfigurationSetName = configSetNameTags }); } catch { }
            try { await sesv2Client.DeleteConfigurationSetAsync(new DeleteConfigurationSetRequest { ConfigurationSetName = configSetNameEvent }); } catch { }
            try { await sesv2Client.DeleteConfigurationSetAsync(new DeleteConfigurationSetRequest { ConfigurationSetName = configSetNamePagination }); } catch { }
            try { await sesv2Client.DeleteConfigurationSetAsync(new DeleteConfigurationSetRequest { ConfigurationSetName = configSetNamePagination2 }); } catch { }
            try { await sesv2Client.DeleteEmailTemplateAsync(new DeleteEmailTemplateRequest { TemplateName = templateName }); } catch { }
            try { await sesv2Client.DeleteDedicatedIpPoolAsync(new DeleteDedicatedIpPoolRequest { PoolName = poolName }); } catch { }
            try { await sesv2Client.DeleteDedicatedIpPoolAsync(new DeleteDedicatedIpPoolRequest { PoolName = poolNameDelete }); } catch { }
            try { await sesv2Client.DeleteSuppressedDestinationAsync(new DeleteSuppressedDestinationRequest { EmailAddress = suppressedEmail }); } catch { }
        }

        return results;
    }
}
