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
        var emailAddress = $"test-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}@example.com";
        var policyName = $"test-policy-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var contactListName = $"test-contactlist-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        try
        {
            results.Add(await runner.RunTestAsync("sesv2", "GetAccount", async () =>
            {
                var resp = await sesv2Client.GetAccountAsync(new GetAccountRequest());
                if (resp == null)
                    throw new Exception("response is null");
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

            results.Add(await runner.RunTestAsync("sesv2", "SendEmail", async () =>
            {
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

            results.Add(await runner.RunTestAsync("sesv2", "DeleteContactList", async () =>
            {
                var resp = await sesv2Client.DeleteContactListAsync(new DeleteContactListRequest
                {
                    ContactListName = contactListName
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
        }
        finally
        {
            try { await sesv2Client.DeleteContactListAsync(new DeleteContactListRequest { ContactListName = contactListName }); } catch { }
            try { await sesv2Client.DeleteEmailIdentityAsync(new DeleteEmailIdentityRequest { EmailIdentity = emailAddress }); } catch { }
        }

        return results;
    }
}
