import json
import time
from ..runner import TestRunner, TestResult


async def run_sesv2_tests(
    runner: TestRunner,
    endpoint: str,
    region: str,
) -> list[TestResult]:
    results: list[TestResult] = []
    import boto3

    session = boto3.Session(
        aws_access_key_id="test",
        aws_secret_access_key="test",
    )
    client = session.client("sesv2", endpoint_url=endpoint, region_name=region)

    email_address = f"test-{int(time.time() * 1000)}@example.com"

    def _get_account():
        client.get_account()

    results.append(await runner.run_test("sesv2", "GetAccount", _get_account))

    def _create_email_identity():
        client.create_email_identity(EmailIdentity=email_address)

    results.append(
        await runner.run_test("sesv2", "CreateEmailIdentity", _create_email_identity)
    )

    def _get_email_identity():
        client.get_email_identity(EmailIdentity=email_address)

    results.append(
        await runner.run_test("sesv2", "GetEmailIdentity", _get_email_identity)
    )

    def _list_email_identities():
        client.list_email_identities()

    results.append(
        await runner.run_test("sesv2", "ListEmailIdentities", _list_email_identities)
    )

    def _create_email_identity_policy():
        client.create_email_identity_policy(
            EmailIdentity=email_address,
            PolicyName="test-policy",
            Policy=json.dumps(
                {
                    "Version": "2012-10-17",
                    "Statement": [
                        {
                            "Effect": "Allow",
                            "Action": ["ses:SendEmail"],
                            "Resource": "*",
                        }
                    ],
                }
            ),
        )

    results.append(
        await runner.run_test(
            "sesv2", "CreateEmailIdentityPolicy", _create_email_identity_policy
        )
    )

    def _get_email_identity_policies():
        client.get_email_identity_policies(EmailIdentity=email_address)

    results.append(
        await runner.run_test(
            "sesv2", "GetEmailIdentityPolicies", _get_email_identity_policies
        )
    )

    def _delete_email_identity_policy():
        client.delete_email_identity_policy(
            EmailIdentity=email_address, PolicyName="test-policy"
        )

    results.append(
        await runner.run_test(
            "sesv2", "DeleteEmailIdentityPolicy", _delete_email_identity_policy
        )
    )

    def _put_email_identity_feedback_attributes():
        client.put_email_identity_feedback_attributes(
            EmailIdentity=email_address,
            EmailForwardingEnabled=False,
        )

    results.append(
        await runner.run_test(
            "sesv2",
            "PutEmailIdentityFeedbackAttributes",
            _put_email_identity_feedback_attributes,
        )
    )

    def _send_email():
        client.send_email(
            FromEmailAddress=email_address,
            Destination={"ToAddresses": [email_address]},
            Content={
                "Simple": {
                    "Subject": {"Data": "Test Subject", "Charset": "UTF-8"},
                    "Body": {
                        "Text": {"Data": "Test Body", "Charset": "UTF-8"},
                    },
                }
            },
        )

    results.append(await runner.run_test("sesv2", "SendEmail", _send_email))

    contact_list_name = f"test-list-{int(time.time() * 1000)}"

    def _create_contact_list():
        client.create_contact_list(
            ContactListName=contact_list_name,
            Topics=[
                {
                    "TopicName": "test-topic",
                    "DisplayName": "Test Topic",
                    "Description": "Test description",
                    "DefaultSubscriptionStatus": "OPT_IN",
                }
            ],
        )

    results.append(
        await runner.run_test("sesv2", "CreateContactList", _create_contact_list)
    )

    def _list_contact_lists():
        client.list_contact_lists()

    results.append(
        await runner.run_test("sesv2", "ListContactLists", _list_contact_lists)
    )

    def _get_contact_list():
        client.get_contact_list(ContactListName=contact_list_name)

    results.append(await runner.run_test("sesv2", "GetContactList", _get_contact_list))

    def _delete_contact_list():
        client.delete_contact_list(ContactListName=contact_list_name)

    results.append(
        await runner.run_test("sesv2", "DeleteContactList", _delete_contact_list)
    )

    def _delete_email_identity():
        client.delete_email_identity(EmailIdentity=email_address)

    results.append(
        await runner.run_test("sesv2", "DeleteEmailIdentity", _delete_email_identity)
    )

    def _get_email_identity_nonexistent():
        try:
            client.get_email_identity(
                EmailIdentity=f"nonexistent-{int(time.time() * 1000)}@example.com"
            )
            raise Exception("expected error for non-existent email identity")
        except Exception as e:
            if str(e) == "expected error for non-existent email identity":
                raise

    results.append(
        await runner.run_test(
            "sesv2", "GetEmailIdentity_NonExistent", _get_email_identity_nonexistent
        )
    )

    def _get_contact_list_nonexistent():
        try:
            client.get_contact_list(ContactListName="nonexistent-list-xyz")
            raise Exception("expected error for non-existent contact list")
        except Exception as e:
            if str(e) == "expected error for non-existent contact list":
                raise

    results.append(
        await runner.run_test(
            "sesv2", "GetContactList_NonExistent", _get_contact_list_nonexistent
        )
    )

    def _create_email_identity_duplicate():
        dup_email = f"dup-{int(time.time() * 1000)}@example.com"
        client.create_email_identity(EmailIdentity=dup_email)
        try:
            client.create_email_identity(EmailIdentity=dup_email)
            raise Exception("expected error for duplicate email identity")
        except Exception as e:
            if str(e) == "expected error for duplicate email identity":
                raise
        finally:
            try:
                client.delete_email_identity(EmailIdentity=dup_email)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "sesv2", "CreateEmailIdentity_Duplicate", _create_email_identity_duplicate
        )
    )

    def _list_email_identities_verify_created():
        verify_email = f"verify-{int(time.time() * 1000)}@example.com"
        client.create_email_identity(EmailIdentity=verify_email)
        list_resp = client.list_email_identities()
        found = any(
            e.get("IdentityName") == verify_email
            for e in list_resp.get("EmailIdentities", [])
        )
        if not found:
            raise Exception("created email identity not found in list")
        try:
            client.delete_email_identity(EmailIdentity=verify_email)
        except Exception:
            pass

    results.append(
        await runner.run_test(
            "sesv2",
            "ListEmailIdentities_VerifyCreated",
            _list_email_identities_verify_created,
        )
    )

    return results
