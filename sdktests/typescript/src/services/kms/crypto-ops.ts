import type { KMSClient } from '@aws-sdk/client-kms';
import type { TestRunner, TestResult } from '../../runner.js';
import { safeCleanup, assertErrorContains } from '../../helpers.js';
import {
  EncryptCommand, DecryptCommand, GenerateDataKeyCommand, GenerateDataKeyWithoutPlaintextCommand,
  GenerateRandomCommand, ReEncryptCommand, EnableKeyCommand, DisableKeyCommand,
  CreateKeyCommand, ScheduleKeyDeletionCommand,
  GenerateDataKeyPairCommand, GenerateDataKeyPairWithoutPlaintextCommand,
} from '@aws-sdk/client-kms';
import type { KmsState } from './context.js';

export async function runCryptoTests(
  runner: TestRunner,
  client: KMSClient,
  state: KmsState,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('kms', 'Encrypt', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new EnableKeyCommand({ KeyId: state.keyID }));
    const resp = await client.send(new EncryptCommand({
      KeyId: state.keyID, Plaintext: new TextEncoder().encode('Hello, KMS!'),
    }));
    if (!resp.CiphertextBlob) throw new Error('expected CiphertextBlob to be defined');
  }));

  results.push(await runner.runTest('kms', 'Encrypt (for Decrypt)', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const resp = await client.send(new EncryptCommand({
      KeyId: state.keyID, Plaintext: new TextEncoder().encode('Hello, KMS!'),
    }));
    state.ciphertextBlob = resp.CiphertextBlob;
  }));

  results.push(await runner.runTest('kms', 'Decrypt', async () => {
    if (!state.ciphertextBlob) throw new Error('ciphertext not available');
    await client.send(new DecryptCommand({ CiphertextBlob: state.ciphertextBlob }));
  }));

  results.push(await runner.runTest('kms', 'Encrypt_DecryptRoundtrip', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new EnableKeyCommand({ KeyId: state.keyID }));
    const plaintext = new TextEncoder().encode('roundtrip-test-data-12345');
    const encResp = await client.send(new EncryptCommand({ KeyId: state.keyID, Plaintext: plaintext }));
    const decResp = await client.send(new DecryptCommand({ CiphertextBlob: encResp.CiphertextBlob! }));
    if (new TextDecoder().decode(decResp.Plaintext) !== new TextDecoder().decode(plaintext)) {
      throw new Error('plaintext mismatch');
    }
  }));

  results.push(await runner.runTest('kms', 'GenerateDataKey', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const resp = await client.send(new GenerateDataKeyCommand({ KeyId: state.keyID, KeySpec: 'AES_256' }));
    if (!resp.CiphertextBlob?.length) throw new Error('expected CiphertextBlob to be non-empty');
    if (resp.Plaintext?.length !== 32) throw new Error(`expected 32-byte plaintext, got ${resp.Plaintext?.length}`);
  }));

  results.push(await runner.runTest('kms', 'GenerateDataKeyWithoutPlaintext', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const resp = await client.send(new GenerateDataKeyWithoutPlaintextCommand({ KeyId: state.keyID, KeySpec: 'AES_256' }));
    if (!resp.CiphertextBlob?.length) throw new Error('expected CiphertextBlob to be non-empty');
  }));

  results.push(await runner.runTest('kms', 'GenerateRandom', async () => {
    const resp = await client.send(new GenerateRandomCommand({ NumberOfBytes: 32 }));
    if (resp.Plaintext?.length !== 32) throw new Error(`expected 32-byte plaintext, got ${resp.Plaintext?.length}`);
  }));

  results.push(await runner.runTest('kms', 'ReEncrypt', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    if (!state.ciphertextBlob) throw new Error('ciphertext not available');
    const resp = await client.send(new ReEncryptCommand({
      CiphertextBlob: state.ciphertextBlob, DestinationKeyId: state.keyID,
    }));
    if (!resp.CiphertextBlob) throw new Error('expected CiphertextBlob to be defined');
  }));

  results.push(await runner.runTest('kms', 'GenerateDataKey_ContentVerify', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new EnableKeyCommand({ KeyId: state.keyID }));
    const resp = await client.send(new GenerateDataKeyCommand({ KeyId: state.keyID, KeySpec: 'AES_256' }));
    if (resp.Plaintext?.length !== 32) throw new Error(`expected 32-byte plaintext, got ${resp.Plaintext?.length}`);
    if (!resp.CiphertextBlob?.length) throw new Error('ciphertext blob is empty');
    if (resp.Plaintext.length === resp.CiphertextBlob.length) {
      throw new Error('plaintext and ciphertext should have different lengths');
    }
  }));

  results.push(await runner.runTest('kms', 'GenerateDataKey_NumberOfBytes', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new EnableKeyCommand({ KeyId: state.keyID }));
    const resp = await client.send(new GenerateDataKeyCommand({ KeyId: state.keyID, NumberOfBytes: 64 }));
    if (resp.Plaintext?.length !== 64) throw new Error(`expected 64-byte plaintext, got ${resp.Plaintext?.length}`);
  }));

  results.push(await runner.runTest('kms', 'GenerateRandom_VariousSizes', async () => {
    for (const size of [1, 16, 128, 1024]) {
      const resp = await client.send(new GenerateRandomCommand({ NumberOfBytes: size }));
      if (resp.Plaintext?.length !== size) throw new Error(`expected ${size} bytes, got ${resp.Plaintext?.length}`);
    }
  }));

  results.push(await runner.runTest('kms', 'ReEncrypt_WithDifferentKey', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const newKeyResp = await client.send(new CreateKeyCommand({ Description: 'ReEncrypt target key' }));
    const newKeyID = newKeyResp.KeyMetadata?.KeyId;
    if (!newKeyID) throw new Error('expected new key ID');
    try {
      const plaintext = new TextEncoder().encode('re-encrypt-test');
      const encResp = await client.send(new EncryptCommand({ KeyId: state.keyID, Plaintext: plaintext }));
      const reResp = await client.send(new ReEncryptCommand({
        CiphertextBlob: encResp.CiphertextBlob!, DestinationKeyId: newKeyID,
      }));
      const decResp = await client.send(new DecryptCommand({
        CiphertextBlob: reResp.CiphertextBlob!, KeyId: newKeyID,
      }));
      if (new TextDecoder().decode(decResp.Plaintext) !== new TextDecoder().decode(plaintext)) {
        throw new Error('plaintext mismatch after re-encrypt');
      }
    } finally {
      await safeCleanup(() => client.send(new ScheduleKeyDeletionCommand({ KeyId: newKeyID, PendingWindowInDays: 7 })));
    }
  }));

  results.push(await runner.runTest('kms', 'Encrypt_DisabledKey', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new DisableKeyCommand({ KeyId: state.keyID }));
    try {
      await client.send(new EncryptCommand({ KeyId: state.keyID, Plaintext: new TextEncoder().encode('should fail') }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
      assertErrorContains(err, 'DisabledException');
    } finally {
      await client.send(new EnableKeyCommand({ KeyId: state.keyID }));
    }
  }));

  results.push(await runner.runTest('kms', 'Encrypt_ByKeyARN', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new EnableKeyCommand({ KeyId: state.keyID }));
    const { DescribeKeyCommand } = await import('@aws-sdk/client-kms');
    const descResp = await client.send(new DescribeKeyCommand({ KeyId: state.keyID }));
    const keyARN = descResp.KeyMetadata?.Arn;
    if (!keyARN) throw new Error('expected ARN');
    const plaintext = new TextEncoder().encode('encrypt-by-arn-test');
    const encResp = await client.send(new EncryptCommand({ KeyId: keyARN, Plaintext: plaintext }));
    const decResp = await client.send(new DecryptCommand({ CiphertextBlob: encResp.CiphertextBlob! }));
    if (new TextDecoder().decode(decResp.Plaintext) !== new TextDecoder().decode(plaintext)) {
      throw new Error('plaintext mismatch');
    }
  }));

  results.push(await runner.runTest('kms', 'Encrypt_ByAlias', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new EnableKeyCommand({ KeyId: state.keyID }));
    const { CreateAliasCommand, DeleteAliasCommand } = await import('@aws-sdk/client-kms');
    const testAlias = `alias/encrypt-test-${Date.now()}`;
    await client.send(new CreateAliasCommand({ AliasName: testAlias, TargetKeyId: state.keyID }));
    try {
      const plaintext = new TextEncoder().encode('encrypt-by-alias-test');
      const encResp = await client.send(new EncryptCommand({ KeyId: testAlias, Plaintext: plaintext }));
      const decResp = await client.send(new DecryptCommand({ CiphertextBlob: encResp.CiphertextBlob! }));
      if (new TextDecoder().decode(decResp.Plaintext) !== new TextDecoder().decode(plaintext)) {
        throw new Error('plaintext mismatch');
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteAliasCommand({ AliasName: testAlias })));
    }
  }));

  results.push(await runner.runTest('kms', 'Encrypt_EncryptionContext', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    const plaintext = new TextEncoder().encode('context-test-data');
    const encResp = await client.send(new EncryptCommand({
      KeyId: state.keyID, Plaintext: plaintext,
      EncryptionContext: { project: 'test', stage: 'dev' },
      EncryptionAlgorithm: 'SYMMETRIC_DEFAULT',
    }));
    const decResp = await client.send(new DecryptCommand({
      CiphertextBlob: encResp.CiphertextBlob!,
      EncryptionContext: { project: 'test', stage: 'dev' },
    }));
    if (new TextDecoder().decode(decResp.Plaintext) !== new TextDecoder().decode(plaintext)) {
      throw new Error('plaintext mismatch');
    }
  }));

  results.push(await runner.runTest('kms', 'GenerateDataKeyPair', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new EnableKeyCommand({ KeyId: state.keyID }));
    const resp = await client.send(new GenerateDataKeyPairCommand({ KeyId: state.keyID, KeyPairSpec: 'RSA_2048' }));
    if (!resp.PrivateKeyCiphertextBlob?.length) throw new Error('private key ciphertext is empty');
    if (!resp.PrivateKeyPlaintext?.length) throw new Error('private key plaintext is empty');
    if (!resp.PublicKey?.length) throw new Error('public key is empty');
    if (resp.KeyPairSpec !== 'RSA_2048') throw new Error(`expected KeyPairSpec=RSA_2048, got ${resp.KeyPairSpec}`);
  }));

  results.push(await runner.runTest('kms', 'GenerateDataKeyPairWithoutPlaintext', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new EnableKeyCommand({ KeyId: state.keyID }));
    const resp = await client.send(new GenerateDataKeyPairWithoutPlaintextCommand({ KeyId: state.keyID, KeyPairSpec: 'RSA_2048' }));
    if (!resp.PrivateKeyCiphertextBlob?.length) throw new Error('private key ciphertext is empty');
    if (!resp.PublicKey?.length) throw new Error('public key is empty');
  }));

  results.push(await runner.runTest('kms', 'Encrypt_WrongKeyUsage', async () => {
    if (!state.hmacKeyID) throw new Error('HMAC key ID not available');
    try {
      await client.send(new EncryptCommand({ KeyId: state.hmacKeyID, Plaintext: new TextEncoder().encode('test') }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
      assertErrorContains(err, 'InvalidKeyUsageException');
    }
  }));

  results.push(await runner.runTest('kms', 'Encrypt_SignVerifyKey', async () => {
    if (!state.rsaKeyID) throw new Error('RSA key ID not available');
    try {
      await client.send(new EncryptCommand({ KeyId: state.rsaKeyID, Plaintext: new TextEncoder().encode('should fail') }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
      assertErrorContains(err, 'InvalidKeyUsageException');
    }
  }));

  results.push(await runner.runTest('kms', 'ReEncrypt_InvalidCiphertext', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    try {
      await client.send(new ReEncryptCommand({
        CiphertextBlob: new TextEncoder().encode('not valid ciphertext'), DestinationKeyId: state.keyID,
      }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
    }
  }));

  results.push(await runner.runTest('kms', 'GenerateDataKey_DisabledKey', async () => {
    if (!state.keyID) throw new Error('key ID not available');
    await client.send(new DisableKeyCommand({ KeyId: state.keyID }));
    try {
      await client.send(new GenerateDataKeyCommand({ KeyId: state.keyID, KeySpec: 'AES_256' }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
    } finally {
      await client.send(new EnableKeyCommand({ KeyId: state.keyID }));
    }
  }));

  return results;
}
