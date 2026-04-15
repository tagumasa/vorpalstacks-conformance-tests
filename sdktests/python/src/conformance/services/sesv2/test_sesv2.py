import json

import pytest


@pytest.fixture(scope="class")
def email_identity(sesv2_client, unique_name):
    email_address = unique_name("email") + "@example.com"
    sesv2_client.create_email_identity(EmailIdentity=email_address)
    yield email_address
    try:
        sesv2_client.delete_email_identity(EmailIdentity=email_address)
    except Exception:
        pass


class TestAccount:
    def test_get_account(self, sesv2_client):
        sesv2_client.get_account()


class TestEmailIdentity:
    def test_create_email_identity(self, sesv2_client, unique_name):
        email_address = unique_name("email") + "@example.com"
        sesv2_client.create_email_identity(EmailIdentity=email_address)
        try:
            sesv2_client.delete_email_identity(EmailIdentity=email_address)
        except Exception:
            pass

    def test_get_email_identity(self, sesv2_client, email_identity):
        sesv2_client.get_email_identity(EmailIdentity=email_identity)

    def test_list_email_identities(self, sesv2_client):
        sesv2_client.list_email_identities()

    def test_delete_email_identity(self, sesv2_client, email_identity):
        sesv2_client.delete_email_identity(EmailIdentity=email_identity)

    def test_nonexistent(self, sesv2_client, unique_name):
        with pytest.raises(Exception):
            sesv2_client.get_email_identity(
                EmailIdentity=unique_name("ne") + "@example.com"
            )

    def test_duplicate(self, sesv2_client, unique_name):
        dup_email = unique_name("dup") + "@example.com"
        sesv2_client.create_email_identity(EmailIdentity=dup_email)
        try:
            with pytest.raises(Exception):
                sesv2_client.create_email_identity(EmailIdentity=dup_email)
        finally:
            try:
                sesv2_client.delete_email_identity(EmailIdentity=dup_email)
            except Exception:
                pass

    def test_list_verify_created(self, sesv2_client, unique_name):
        verify_email = unique_name("verify") + "@example.com"
        sesv2_client.create_email_identity(EmailIdentity=verify_email)
        list_resp = sesv2_client.list_email_identities()
        found = any(
            e.get("IdentityName") == verify_email
            for e in list_resp.get("EmailIdentities", [])
        )
        assert found, "created email identity not found in list"
        try:
            sesv2_client.delete_email_identity(EmailIdentity=verify_email)
        except Exception:
            pass


class TestEmailIdentityPolicy:
    def test_create_email_identity_policy(self, sesv2_client, email_identity):
        sesv2_client.create_email_identity_policy(
            EmailIdentity=email_identity,
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

    def test_get_email_identity_policies(self, sesv2_client, email_identity):
        sesv2_client.create_email_identity_policy(
            EmailIdentity=email_identity,
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
        sesv2_client.get_email_identity_policies(EmailIdentity=email_identity)

    def test_delete_email_identity_policy(self, sesv2_client, email_identity):
        sesv2_client.create_email_identity_policy(
            EmailIdentity=email_identity,
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
        sesv2_client.delete_email_identity_policy(
            EmailIdentity=email_identity, PolicyName="test-policy"
        )


class TestPutEmailIdentityFeedbackAttributes:
    def test_put_email_identity_feedback_attributes(self, sesv2_client, email_identity):
        sesv2_client.put_email_identity_feedback_attributes(
            EmailIdentity=email_identity,
            EmailForwardingEnabled=False,
        )


class TestSendEmail:
    def test_send_email(self, sesv2_client, email_identity):
        sesv2_client.send_email(
            FromEmailAddress=email_identity,
            Destination={"ToAddresses": [email_identity]},
            Content={
                "Simple": {
                    "Subject": {"Data": "Test Subject", "Charset": "UTF-8"},
                    "Body": {
                        "Text": {"Data": "Test Body", "Charset": "UTF-8"},
                    },
                }
            },
        )


class TestContactList:
    def test_create_contact_list(self, sesv2_client, unique_name):
        contact_list_name = unique_name("clist")
        sesv2_client.create_contact_list(
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

    def test_list_contact_lists(self, sesv2_client, unique_name):
        contact_list_name = unique_name("clist")
        sesv2_client.create_contact_list(ContactListName=contact_list_name)
        try:
            sesv2_client.list_contact_lists()
        finally:
            try:
                sesv2_client.delete_contact_list(ContactListName=contact_list_name)
            except Exception:
                pass

    def test_get_contact_list(self, sesv2_client, unique_name):
        contact_list_name = unique_name("clist")
        sesv2_client.create_contact_list(ContactListName=contact_list_name)
        try:
            sesv2_client.get_contact_list(ContactListName=contact_list_name)
        finally:
            try:
                sesv2_client.delete_contact_list(ContactListName=contact_list_name)
            except Exception:
                pass

    def test_delete_contact_list(self, sesv2_client, unique_name):
        contact_list_name = unique_name("clist")
        sesv2_client.create_contact_list(ContactListName=contact_list_name)
        sesv2_client.delete_contact_list(ContactListName=contact_list_name)

    def test_nonexistent(self, sesv2_client):
        with pytest.raises(Exception):
            sesv2_client.get_contact_list(ContactListName="nonexistent-list-xyz")
