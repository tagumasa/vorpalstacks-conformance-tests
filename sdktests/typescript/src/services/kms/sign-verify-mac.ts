import type { KMSClient } from '@aws-sdk/client-kms';
import type { TestRunner, TestResult } from '../../runner.js';
import { safeCleanup, assertErrorContains } from '../../helpers.js';
import {
  CreateKeyCommand, GetPublicKeyCommand, SignCommand, VerifyCommand,
  EnableKeyCommand, DisableKeyCommand, ScheduleKeyDeletionCommand,
  GenerateMacCommand, VerifyMacCommand,
} from '@aws-sdk/client-kms';
import type { KmsState } from './context.js';

export async function runSignVerifyTests(
  runner: TestRunner,
  client: KMSClient,
  state: KmsState,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('kms', 'CreateKey_RSA', async () => {
    const resp = await client.send(new CreateKeyCommand({
      Description: 'RSA Sign/Verify Key', KeyUsage: 'SIGN_VERIFY', KeySpec: 'RSA_2048',
    }));
    if (!resp.KeyMetadata?.KeyId) throw new Error('expected KeyMetadata.KeyId');
    state.rsaKeyID = resp.KeyMetadata.KeyId;
  }));

  if (state.rsaKeyID) {
    results.push(await runner.runTest('kms', 'GetPublicKey_RSA', async () => {
      const resp = await client.send(new GetPublicKeyCommand({ KeyId: state.rsaKeyID }));
      if (!resp.PublicKey?.length) throw new Error('expected PublicKey to be non-empty');
      if (resp.KeySpec !== 'RSA_2048') throw new Error(`expected KeySpec=RSA_2048, got ${resp.KeySpec}`);
      if (resp.KeyUsage !== 'SIGN_VERIFY') throw new Error(`expected KeyUsage=SIGN_VERIFY, got ${resp.KeyUsage}`);
      if (!resp.SigningAlgorithms?.length) throw new Error('expected SigningAlgorithms to be non-empty');
    }));

    const message = new TextEncoder().encode('message to sign');

    results.push(await runner.runTest('kms', 'Sign_RSA', async () => {
      const resp = await client.send(new SignCommand({
        KeyId: state.rsaKeyID, Message: message, MessageType: 'RAW', SigningAlgorithm: 'RSASSA_PKCS1_V1_5_SHA_256',
      }));
      if (!resp.Signature?.length) throw new Error('expected Signature to be non-empty');
      state.signature = resp.Signature;
    }));

    results.push(await runner.runTest('kms', 'Verify_RSA', async () => {
      if (!state.signature) throw new Error('signature not available');
      const resp = await client.send(new VerifyCommand({
        KeyId: state.rsaKeyID, Message: message, Signature: state.signature,
        MessageType: 'RAW', SigningAlgorithm: 'RSASSA_PKCS1_V1_5_SHA_256',
      }));
      if (!resp.SignatureValid) throw new Error('expected signature to be valid');
    }));

    results.push(await runner.runTest('kms', 'Verify_RSA_InvalidSignature', async () => {
      const badSig = new Uint8Array(256).fill(0xff);
      const resp = await client.send(new VerifyCommand({
        KeyId: state.rsaKeyID, Message: new TextEncoder().encode('different message'),
        Signature: badSig, MessageType: 'RAW', SigningAlgorithm: 'RSASSA_PKCS1_V1_5_SHA_256',
      }));
      if (resp.SignatureValid) throw new Error('expected invalid signature');
    }));

    results.push(await runner.runTest('kms', 'Sign_DisabledKey', async () => {
      await client.send(new DisableKeyCommand({ KeyId: state.rsaKeyID }));
      try {
        await client.send(new SignCommand({
          KeyId: state.rsaKeyID, Message: new TextEncoder().encode('test'),
          MessageType: 'RAW', SigningAlgorithm: 'RSASSA_PKCS1_V1_5_SHA_256',
        }));
        throw new Error('expected error');
      } catch (err) {
        if (err instanceof Error && err.message === 'expected error') throw err;
        assertErrorContains(err, 'DisabledException');
      } finally {
        await client.send(new EnableKeyCommand({ KeyId: state.rsaKeyID }));
      }
    }));

    results.push(await runner.runTest('kms', 'Sign_InvalidAlgorithm', async () => {
      try {
        await client.send(new SignCommand({
          KeyId: state.rsaKeyID, Message: new TextEncoder().encode('test'),
          MessageType: 'RAW', SigningAlgorithm: 'INVALID_ALGORITHM' as any,
        }));
        throw new Error('expected error');
      } catch (err) {
        if (err instanceof Error && err.message === 'expected error') throw err;
      }
    }));
  }

  results.push(await runner.runTest('kms', 'CreateKey_HMAC', async () => {
    const resp = await client.send(new CreateKeyCommand({
      Description: 'HMAC Key', KeyUsage: 'GENERATE_VERIFY_MAC', KeySpec: 'HMAC_256',
    }));
    if (!resp.KeyMetadata?.KeyId) throw new Error('expected KeyMetadata.KeyId');
    state.hmacKeyID = resp.KeyMetadata.KeyId;
  }));

  if (state.hmacKeyID) {
    const macMessage = new TextEncoder().encode('message to mac');

    results.push(await runner.runTest('kms', 'GenerateMac', async () => {
      const resp = await client.send(new GenerateMacCommand({
        KeyId: state.hmacKeyID, Message: macMessage, MacAlgorithm: 'HMAC_SHA_256',
      }));
      if (!resp.Mac?.length) throw new Error('expected MAC to be non-empty');
      state.macValue = resp.Mac;
    }));

    results.push(await runner.runTest('kms', 'VerifyMac', async () => {
      if (!state.macValue) throw new Error('MAC value not available');
      const resp = await client.send(new VerifyMacCommand({
        KeyId: state.hmacKeyID, Message: macMessage, Mac: state.macValue, MacAlgorithm: 'HMAC_SHA_256',
      }));
      if (!resp.MacValid) throw new Error('expected MAC to be valid');
    }));

    results.push(await runner.runTest('kms', 'VerifyMac_InvalidMac', async () => {
      const badMac = new Uint8Array(32).fill(0xff);
      const resp = await client.send(new VerifyMacCommand({
        KeyId: state.hmacKeyID, Message: macMessage, Mac: badMac, MacAlgorithm: 'HMAC_SHA_256',
      }));
      if (resp.MacValid) throw new Error('expected invalid MAC');
    }));
  }

  return results;
}
