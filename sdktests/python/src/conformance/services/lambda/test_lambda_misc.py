def test_get_account_settings(lambda_client):
    resp = lambda_client.get_account_settings()
    assert resp.get("AccountLimit"), "account limit is nil"
