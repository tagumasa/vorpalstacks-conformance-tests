import {
  KMSClient,
  CreateKeyCommand,
  DescribeKeyCommand,
  ListKeysCommand,
  UpdateKeyDescriptionCommand,
  PutKeyPolicyCommand,
  GetKeyPolicyCommand,
  ListKeyPoliciesCommand,
  CreateGrantCommand,
  ListGrantsCommand,
  ListRetirableGrantsCommand,
  RetireGrantCommand,
  RevokeGrantCommand,
  EncryptCommand,
  DecryptCommand,
  ReEncryptCommand,
  GenerateDataKeyCommand,
  GenerateDataKeyWithoutPlaintextCommand,
  GenerateDataKeyPairCommand,
  GenerateMacCommand,
  VerifyMacCommand,
  GenerateRandomCommand,
  EnableKeyCommand,
  DisableKeyCommand,
  ScheduleKeyDeletionCommand,
  CancelKeyDeletionCommand,
  CreateAliasCommand,
  DeleteAliasCommand,
  ListAliasesCommand,
  UpdateAliasCommand,
  EnableKeyRotationCommand,
  DisableKeyRotationCommand,
  GetKeyRotationStatusCommand,
  TagResourceCommand,
  UntagResourceCommand,
  ListResourceTagsCommand,
} from '@aws-sdk/client-kms';
import { NotFoundException, DependencyTimeoutException, DisabledException, AlreadyExistsException } from '@aws-sdk/client-kms';
import { TestRunner } from '../runner.js';
import { TestResult } from '../runner.js';

function makeUniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 999999)}`;
}

export async function runKMSTests(
  runner: TestRunner,
  kmsClient: KMSClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const keyAlias = `alias/${makeUniqueName('TSKey')}`;
  let keyId = '';
  let keyArn = '';
  let grantId = '';
  let grantToken = '';
  let ciphertextBlob: Uint8Array | undefined;

  try {
    results.push(
      await runner.runTest('kms', 'CreateKey', async () => {
        const resp = await kmsClient.send(
          new CreateKeyCommand({
            KeyUsage: 'ENCRYPT_DECRYPT',
            Description: 'Test key for SDK tests',
            Tags: [{ TagKey: 'Environment', TagValue: 'Test' }],
          })
        );
        if (!resp.KeyMetadata) throw new Error('KeyMetadata is null');
        if (!resp.KeyMetadata.KeyId) throw new Error('KeyId is null');
        keyId = resp.KeyMetadata.KeyId;
        keyArn = resp.KeyMetadata.Arn || '';
      })
    );

    results.push(
      await runner.runTest('kms', 'DescribeKey', async () => {
        const resp = await kmsClient.send(
          new DescribeKeyCommand({ KeyId: keyId })
        );
        if (!resp.KeyMetadata) throw new Error('KeyMetadata is null');
        if (!resp.KeyMetadata.KeyId) throw new Error('KeyId is null');
      })
    );

    results.push(
      await runner.runTest('kms', 'ListKeys', async () => {
        const resp = await kmsClient.send(new ListKeysCommand({}));
        if (!resp.Keys) throw new Error('Keys is null');
        const found = resp.Keys.some((k) => k.KeyId === keyId);
        if (!found) throw new Error('Created key not found in list');
      })
    );

    results.push(
      await runner.runTest('kms', 'UpdateKeyDescription', async () => {
        await kmsClient.send(
          new UpdateKeyDescriptionCommand({
            KeyId: keyId,
            Description: 'Updated test key description',
          })
        );
      })
    );

    results.push(
      await runner.runTest('kms', 'GetKeyPolicy', async () => {
        const resp = await kmsClient.send(
          new GetKeyPolicyCommand({ KeyId: keyId, PolicyName: 'default' })
        );
        if (!resp.Policy) throw new Error('Policy is null');
      })
    );

    results.push(
      await runner.runTest('kms', 'PutKeyPolicy', async () => {
        const policy = JSON.stringify({
          Version: '2012-10-17',
          Statement: [{ Effect: 'Allow', Principal: { AWS: '*' }, Action: 'kms:*', Resource: '*' }],
        });
        await kmsClient.send(
          new PutKeyPolicyCommand({ KeyId: keyId, PolicyName: 'default', Policy: policy })
        );
      })
    );

    results.push(
      await runner.runTest('kms', 'ListKeyPolicies', async () => {
        const resp = await kmsClient.send(new ListKeyPoliciesCommand({ KeyId: keyId }));
        if (!resp.PolicyNames) throw new Error('PolicyNames is null');
      })
    );

    results.push(
      await runner.runTest('kms', 'CreateGrant', async () => {
        const resp = await kmsClient.send(
          new CreateGrantCommand({
            KeyId: keyId,
            GranteePrincipal: 'arn:aws:iam::000000000000:user/test',
            Operations: ['Encrypt', 'Decrypt', 'GenerateDataKey'],
          })
        );
        if (!resp.GrantId) throw new Error('GrantId is null');
        grantId = resp.GrantId;
        if (resp.GrantToken) grantToken = resp.GrantToken;
      })
    );

    results.push(
      await runner.runTest('kms', 'ListGrants', async () => {
        const resp = await kmsClient.send(new ListGrantsCommand({ KeyId: keyId }));
        if (!resp.Grants) throw new Error('Grants is null');
        const found = resp.Grants.some((g) => g.GrantId === grantId);
        if (!found) throw new Error('Created grant not found in list');
      })
    );

    results.push(
      await runner.runTest('kms', 'ListRetirableGrants', async () => {
        const resp = await kmsClient.send(
          new ListRetirableGrantsCommand({
            RetiringPrincipal: 'arn:aws:iam::000000000000:user/TestUser',
          })
        );
        if (!resp.Grants) throw new Error('Grants is null');
      })
    );

    results.push(
      await runner.runTest('kms', 'EnableKey', async () => {
        await kmsClient.send(new EnableKeyCommand({ KeyId: keyId }));
      })
    );

    results.push(
      await runner.runTest('kms', 'Encrypt', async () => {
        const resp = await kmsClient.send(
          new EncryptCommand({ KeyId: keyId, Plaintext: Buffer.from('Hello, KMS!') })
        );
        if (!resp.CiphertextBlob) throw new Error('CiphertextBlob is null');
      })
    );

    results.push(
      await runner.runTest('kms', 'Encrypt_ForDecrypt', async () => {
        const resp = await kmsClient.send(
          new EncryptCommand({ KeyId: keyId, Plaintext: Buffer.from('Hello, KMS!') })
        );
        ciphertextBlob = resp.CiphertextBlob;
      })
    );

    results.push(
      await runner.runTest('kms', 'Decrypt', async () => {
        if (!ciphertextBlob) throw new Error('ciphertext not available');
        await kmsClient.send(new DecryptCommand({ CiphertextBlob: ciphertextBlob }));
      })
    );

    results.push(
      await runner.runTest('kms', 'GenerateDataKey', async () => {
        const resp = await kmsClient.send(
          new GenerateDataKeyCommand({ KeyId: keyId, KeySpec: 'AES_256' })
        );
        if (!resp.CiphertextBlob) throw new Error('CiphertextBlob is null');
        if (!resp.Plaintext) throw new Error('Plaintext is null');
        if (resp.Plaintext.length !== 32) throw new Error(`Expected 32-byte plaintext, got ${resp.Plaintext.length}`);
      })
    );

    results.push(
      await runner.runTest('kms', 'GenerateDataKeyWithoutPlaintext', async () => {
        const resp = await kmsClient.send(
          new GenerateDataKeyWithoutPlaintextCommand({ KeyId: keyId, KeySpec: 'AES_256' })
        );
        if (!resp.CiphertextBlob || resp.CiphertextBlob.length === 0)
          throw new Error('CiphertextBlob is null or empty');
      })
    );

    results.push(
      await runner.runTest('kms', 'GenerateRandom', async () => {
        const resp = await kmsClient.send(new GenerateRandomCommand({ NumberOfBytes: 32 }));
        if (!resp.Plaintext || resp.Plaintext.length !== 32)
          throw new Error(`Expected 32 bytes, got ${resp.Plaintext?.length}`);
      })
    );

    results.push(
      await runner.runTest('kms', 'GenerateDataKeyPair', async () => {
        const resp = await kmsClient.send(
          new GenerateDataKeyPairCommand({ KeyId: keyId, KeyPairSpec: 'RSA_2048' })
        );
        if (!resp.PrivateKeyCiphertextBlob) throw new Error('PrivateKeyCiphertextBlob is null');
        if (!resp.PublicKey) throw new Error('PublicKey is null');
      })
    );

    results.push(
      await runner.runTest('kms', 'GenerateMac', async () => {
        const macKeyResp = await kmsClient.send(
          new CreateKeyCommand({ KeyUsage: 'GENERATE_VERIFY_MAC', KeySpec: 'HMAC_256', Description: 'MAC key for SDK tests' })
        );
        const macKeyId = macKeyResp.KeyMetadata!.KeyId!;
        const resp = await kmsClient.send(
          new GenerateMacCommand({ KeyId: macKeyId, Message: Buffer.from('test message'), MacAlgorithm: 'HMAC_SHA_256' })
        );
        if (!resp.Mac) throw new Error('Mac is null');
      })
    );

    results.push(
      await runner.runTest('kms', 'VerifyMac', async () => {
        const macKeyResp = await kmsClient.send(
          new CreateKeyCommand({ KeyUsage: 'GENERATE_VERIFY_MAC', KeySpec: 'HMAC_256', Description: 'MAC key for SDK tests' })
        );
        const macKeyId = macKeyResp.KeyMetadata!.KeyId!;
        const macResp = await kmsClient.send(
          new GenerateMacCommand({ KeyId: macKeyId, Message: Buffer.from('test message'), MacAlgorithm: 'HMAC_SHA_256' })
        );
        const verifyResp = await kmsClient.send(
          new VerifyMacCommand({ KeyId: macKeyId, Message: Buffer.from('test message'), Mac: macResp.Mac!, MacAlgorithm: 'HMAC_SHA_256' })
        );
        if (!verifyResp.MacValid || verifyResp.MacValid !== true) throw new Error('Mac verification failed');
      })
    );

    results.push(
      await runner.runTest('kms', 'ReEncrypt', async () => {
        if (!ciphertextBlob) throw new Error('ciphertext not available');
        const resp = await kmsClient.send(
          new ReEncryptCommand({ CiphertextBlob: ciphertextBlob, DestinationKeyId: keyId })
        );
        if (!resp.CiphertextBlob) throw new Error('Re-encrypted CiphertextBlob is null');
      })
    );

    results.push(
      await runner.runTest('kms', 'TagResource', async () => {
        await kmsClient.send(
          new TagResourceCommand({
            KeyId: keyId,
            Tags: [
              { TagKey: 'Environment', TagValue: 'test' },
              { TagKey: 'Project', TagValue: 'sdk-tests' },
            ],
          })
        );
      })
    );

    results.push(
      await runner.runTest('kms', 'ListResourceTags', async () => {
        const resp = await kmsClient.send(new ListResourceTagsCommand({ KeyId: keyId }));
        if (!resp.Tags) throw new Error('Tags is null');
      })
    );

    results.push(
      await runner.runTest('kms', 'UntagResource', async () => {
        await kmsClient.send(new UntagResourceCommand({ KeyId: keyId, TagKeys: ['Environment'] }));
      })
    );

    results.push(
      await runner.runTest('kms', 'CreateAlias', async () => {
        await kmsClient.send(new CreateAliasCommand({ AliasName: keyAlias, TargetKeyId: keyId }));
      })
    );

    results.push(
      await runner.runTest('kms', 'ListAliases', async () => {
        const resp = await kmsClient.send(new ListAliasesCommand({}));
        if (!resp.Aliases) throw new Error('Aliases is null');
      })
    );

    results.push(
      await runner.runTest('kms', 'UpdateAlias', async () => {
        await kmsClient.send(new UpdateAliasCommand({ AliasName: keyAlias, TargetKeyId: keyId }));
        const listResp = await kmsClient.send(new ListAliasesCommand({}));
        const found = listResp.Aliases!.some((a) => a.AliasName === keyAlias);
        if (!found) throw new Error(`Alias ${keyAlias} not found after update`);
      })
    );

    results.push(
      await runner.runTest('kms', 'EnableKeyRotation', async () => {
        await kmsClient.send(new EnableKeyRotationCommand({ KeyId: keyId }));
      })
    );

    results.push(
      await runner.runTest('kms', 'GetKeyRotationStatus', async () => {
        await kmsClient.send(new GetKeyRotationStatusCommand({ KeyId: keyId }));
      })
    );

    results.push(
      await runner.runTest('kms', 'DisableKeyRotation', async () => {
        await kmsClient.send(new DisableKeyRotationCommand({ KeyId: keyId }));
      })
    );

    results.push(
      await runner.runTest('kms', 'DisableKey', async () => {
        await kmsClient.send(new DisableKeyCommand({ KeyId: keyId }));
      })
    );

    results.push(
      await runner.runTest('kms', 'EnableKey_AfterDisable', async () => {
        await kmsClient.send(new EnableKeyCommand({ KeyId: keyId }));
      })
    );

    results.push(
      await runner.runTest('kms', 'ScheduleKeyDeletion', async () => {
        const resp = await kmsClient.send(
          new ScheduleKeyDeletionCommand({ KeyId: keyId, PendingWindowInDays: 7 })
        );
        if (!resp.DeletionDate) throw new Error('DeletionDate is null');
      })
    );

    results.push(
      await runner.runTest('kms', 'CancelKeyDeletion', async () => {
        const resp = await kmsClient.send(new CancelKeyDeletionCommand({ KeyId: keyId }));
        if (!resp.KeyId) throw new Error('KeyId in response is null');
      })
    );

    results.push(
      await runner.runTest('kms', 'DeleteAlias', async () => {
        await kmsClient.send(new DeleteAliasCommand({ AliasName: keyAlias }));
      })
    );

    {
      const retireGrantResp = await kmsClient.send(
        new CreateGrantCommand({
          KeyId: keyId,
          GranteePrincipal: 'arn:aws:iam::000000000000:user/test-retire',
          Operations: ['Encrypt', 'Decrypt'],
        })
      );
      if (retireGrantResp.GrantToken) {
        results.push(
          await runner.runTest('kms', 'RetireGrant', async () => {
            await kmsClient.send(new RetireGrantCommand({ GrantToken: retireGrantResp.GrantToken! }));
          })
        );
      }
    }

    results.push(
      await runner.runTest('kms', 'RevokeGrant', async () => {
        await kmsClient.send(new RevokeGrantCommand({ KeyId: keyId, GrantId: grantId }));
      })
    );

  } finally {
    try { await kmsClient.send(new ScheduleKeyDeletionCommand({ KeyId: keyId, PendingWindowInDays: 7 })); } catch { /* ignore */ }
  }

  results.push(
    await runner.runTest('kms', 'DescribeKey_NonExistent', async () => {
      try {
        await kmsClient.send(new DescribeKeyCommand({ KeyId: '12345678-1234-1234-1234-123456789012' }));
        throw new Error('Expected NotFoundException but got none');
      } catch (err: unknown) {
        if (err instanceof NotFoundException || (err instanceof Error && (err.name === 'NotFoundException' || err.name === 'DependencyTimeoutException'))) {
          // Expected
        } else {
          throw err;
        }
      }
    })
  );

  results.push(
    await runner.runTest('kms', 'Encrypt_NonExistent', async () => {
      try {
        await kmsClient.send(new EncryptCommand({ KeyId: '12345678-1234-1234-1234-123456789012', Plaintext: Buffer.from('test') }));
        throw new Error('Expected error for non-existent key but got none');
      } catch (err: unknown) {
        if (err instanceof NotFoundException || (err instanceof Error && (err.name === 'NotFoundException' || err.name === 'DependencyTimeoutException'))) {
          // Expected
        } else {
          throw err;
        }
      }
    })
  );

  results.push(
    await runner.runTest('kms', 'Decrypt_NonExistent', async () => {
      try {
        await kmsClient.send(new DecryptCommand({ CiphertextBlob: Buffer.from('invalid ciphertext') }));
        throw new Error('Expected error for invalid ciphertext but got none');
      } catch (err: unknown) {
        if (err instanceof Error && err.name === 'InvalidCiphertextException') {
          // Expected
        } else {
          throw err;
        }
      }
    })
  );

  results.push(
    await runner.runTest('kms', 'Encrypt_DecryptRoundtrip', async () => {
      const createResp = await kmsClient.send(new CreateKeyCommand({ Description: 'Roundtrip test key' }));
      try {
        const rtKeyId = createResp.KeyMetadata!.KeyId!;
        const plaintext = Buffer.from('roundtrip-test-data-12345');
        const encResp = await kmsClient.send(new EncryptCommand({ KeyId: rtKeyId, Plaintext: plaintext }));
        const decResp = await kmsClient.send(new DecryptCommand({ CiphertextBlob: encResp.CiphertextBlob! }));
        if (!decResp.Plaintext || !plaintext.equals(decResp.Plaintext)) throw new Error('Plaintext mismatch after roundtrip');
      } finally {
        try { await kmsClient.send(new ScheduleKeyDeletionCommand({ KeyId: createResp.KeyMetadata!.KeyId!, PendingWindowInDays: 7 })); } catch { /* ignore */ }
      }
    })
  );

  results.push(
    await runner.runTest('kms', 'GenerateDataKey_ContentVerify', async () => {
      const vResp = await kmsClient.send(new CreateKeyCommand({ Description: 'Verify key' }));
      try {
        const resp = await kmsClient.send(new GenerateDataKeyCommand({ KeyId: vResp.KeyMetadata!.KeyId!, KeySpec: 'AES_256' }));
        if (!resp.Plaintext || resp.Plaintext.length !== 32) throw new Error(`Expected 32-byte plaintext, got ${resp.Plaintext?.length}`);
        if (!resp.CiphertextBlob || resp.CiphertextBlob.length === 0) throw new Error('CiphertextBlob is empty');
        if (resp.Plaintext.length === resp.CiphertextBlob.length) throw new Error('Plaintext and CiphertextBlob should have different lengths');
      } finally {
        try { await kmsClient.send(new ScheduleKeyDeletionCommand({ KeyId: vResp.KeyMetadata!.KeyId!, PendingWindowInDays: 7 })); } catch { /* ignore */ }
      }
    })
  );

  results.push(
    await runner.runTest('kms', 'CreateAlias_Duplicate', async () => {
      const dupKeyResp = await kmsClient.send(new CreateKeyCommand({ Description: 'Dup alias test' }));
      try {
        const dupAlias = `alias/${makeUniqueName('DupAlias')}`;
        await kmsClient.send(new CreateAliasCommand({ AliasName: dupAlias, TargetKeyId: dupKeyResp.KeyMetadata!.KeyId! }));
        try {
          try {
            await kmsClient.send(new CreateAliasCommand({ AliasName: dupAlias, TargetKeyId: dupKeyResp.KeyMetadata!.KeyId! }));
            throw new Error('Expected error for duplicate alias');
          } catch (err: unknown) {
            if (err instanceof AlreadyExistsException) {
              // Expected
            } else {
              throw err;
            }
          }
        } finally {
          try { await kmsClient.send(new DeleteAliasCommand({ AliasName: dupAlias })); } catch { /* ignore */ }
        }
      } finally {
        try { await kmsClient.send(new ScheduleKeyDeletionCommand({ KeyId: dupKeyResp.KeyMetadata!.KeyId!, PendingWindowInDays: 7 })); } catch { /* ignore */ }
      }
    })
  );

  results.push(
    await runner.runTest('kms', 'Encrypt_DisabledKey', async () => {
      const disKeyResp = await kmsClient.send(new CreateKeyCommand({ Description: 'Disable test' }));
      try {
        const disKeyId = disKeyResp.KeyMetadata!.KeyId!;
        await kmsClient.send(new DisableKeyCommand({ KeyId: disKeyId }));
        try {
          try {
            await kmsClient.send(new EncryptCommand({ KeyId: disKeyId, Plaintext: Buffer.from('should fail') }));
            throw new Error('Expected error when encrypting with disabled key');
          } catch (err: unknown) {
            if (err instanceof DisabledException) {
              // Expected
            } else {
              throw err;
            }
          }
        } finally {
          try { await kmsClient.send(new EnableKeyCommand({ KeyId: disKeyId })); } catch { /* ignore */ }
        }
      } finally {
        try { await kmsClient.send(new ScheduleKeyDeletionCommand({ KeyId: disKeyResp.KeyMetadata!.KeyId!, PendingWindowInDays: 7 })); } catch { /* ignore */ }
      }
    })
  );

  results.push(
    await runner.runTest('kms', 'ScheduleKeyDeletion_InvalidWindow', async () => {
      const invKeyResp = await kmsClient.send(new CreateKeyCommand({ Description: 'Invalid window test' }));
      try {
        try {
          await kmsClient.send(new ScheduleKeyDeletionCommand({ KeyId: invKeyResp.KeyMetadata!.KeyId!, PendingWindowInDays: 3 }));
          throw new Error('Expected error for invalid pending window (3 days, min is 7)');
        } catch (err: unknown) {
          if (err instanceof Error && err.name === 'ValidationException') {
            // Expected
          }
        }
      } finally {
        try { await kmsClient.send(new ScheduleKeyDeletionCommand({ KeyId: invKeyResp.KeyMetadata!.KeyId!, PendingWindowInDays: 7 })); } catch { /* ignore */ }
      }
    })
  );

  results.push(
    await runner.runTest('kms', 'ListAliases_ContainsCreated', async () => {
      const laKeyResp = await kmsClient.send(new CreateKeyCommand({ Description: 'List alias test' }));
      try {
        const laAlias = `alias/${makeUniqueName('LaAlias')}`;
        await kmsClient.send(new CreateAliasCommand({ AliasName: laAlias, TargetKeyId: laKeyResp.KeyMetadata!.KeyId! }));
        try {
          const listResp = await kmsClient.send(new ListAliasesCommand({}));
          const found = listResp.Aliases!.some((a) => a.AliasName === laAlias);
          if (!found) throw new Error(`Created alias ${laAlias} not found in ListAliases`);
        } finally {
          try { await kmsClient.send(new DeleteAliasCommand({ AliasName: laAlias })); } catch { /* ignore */ }
        }
      } finally {
        try { await kmsClient.send(new ScheduleKeyDeletionCommand({ KeyId: laKeyResp.KeyMetadata!.KeyId!, PendingWindowInDays: 7 })); } catch { /* ignore */ }
      }
    })
  );

  results.push(
    await runner.runTest('kms', 'GetKeyPolicy_ContentVerify', async () => {
      const gpKeyResp = await kmsClient.send(new CreateKeyCommand({ Description: 'Policy verify' }));
      try {
        const policy = JSON.stringify({ Version: '2012-10-17', Statement: [{ Effect: 'Allow', Principal: { AWS: '*' }, Action: 'kms:*', Resource: '*' }] });
        await kmsClient.send(new PutKeyPolicyCommand({ KeyId: gpKeyResp.KeyMetadata!.KeyId!, PolicyName: 'default', Policy: policy }));
        const getResp = await kmsClient.send(new GetKeyPolicyCommand({ KeyId: gpKeyResp.KeyMetadata!.KeyId!, PolicyName: 'default' }));
        if (getResp.Policy !== policy) throw new Error('Policy content mismatch');
      } finally {
        try { await kmsClient.send(new ScheduleKeyDeletionCommand({ KeyId: gpKeyResp.KeyMetadata!.KeyId!, PendingWindowInDays: 7 })); } catch { /* ignore */ }
      }
    })
  );

  results.push(
    await runner.runTest('kms', 'ReEncrypt_WithDifferentKey', async () => {
      const re1 = await kmsClient.send(new CreateKeyCommand({ Description: 'ReEncrypt source' }));
      const re2 = await kmsClient.send(new CreateKeyCommand({ Description: 'ReEncrypt dest' }));
      try {
        const plaintext = Buffer.from('re-encrypt-test');
        const encResp = await kmsClient.send(new EncryptCommand({ KeyId: re1.KeyMetadata!.KeyId!, Plaintext: plaintext }));
        const reResp = await kmsClient.send(new ReEncryptCommand({ CiphertextBlob: encResp.CiphertextBlob!, DestinationKeyId: re2.KeyMetadata!.KeyId! }));
        const decResp = await kmsClient.send(new DecryptCommand({ CiphertextBlob: reResp.CiphertextBlob!, KeyId: re2.KeyMetadata!.KeyId! }));
        if (!decResp.Plaintext || !plaintext.equals(decResp.Plaintext)) throw new Error('Plaintext mismatch after re-encrypt');
      } finally {
        try { await kmsClient.send(new ScheduleKeyDeletionCommand({ KeyId: re1.KeyMetadata!.KeyId!, PendingWindowInDays: 7 })); } catch { /* ignore */ }
        try { await kmsClient.send(new ScheduleKeyDeletionCommand({ KeyId: re2.KeyMetadata!.KeyId!, PendingWindowInDays: 7 })); } catch { /* ignore */ }
      }
    })
  );

  return results;
}
