import {
  CreateBucketCommand,
  PutBucketTaggingCommand,
  GetBucketTaggingCommand,
  DeleteBucketTaggingCommand,
  PutBucketAclCommand,
  GetBucketAclCommand,
  PutBucketPolicyCommand,
  GetBucketPolicyCommand,
  DeleteBucketPolicyCommand,
  PutBucketCorsCommand,
  GetBucketCorsCommand,
  DeleteBucketCorsCommand,
  PutBucketEncryptionCommand,
  GetBucketEncryptionCommand,
  DeleteBucketEncryptionCommand,
  PutBucketVersioningCommand,
  GetBucketVersioningCommand,
  PutBucketLifecycleConfigurationCommand,
  GetBucketLifecycleConfigurationCommand,
  DeleteBucketLifecycleCommand,
  PutBucketWebsiteCommand,
  GetBucketWebsiteCommand,
  DeleteBucketWebsiteCommand,
  PutObjectLockConfigurationCommand,
  GetObjectLockConfigurationCommand,
  PutBucketNotificationConfigurationCommand,
  GetBucketNotificationConfigurationCommand,
  PutBucketLoggingCommand,
  GetBucketLoggingCommand,
  PutPublicAccessBlockCommand,
  GetPublicAccessBlockCommand,
  DeletePublicAccessBlockCommand,
  PutBucketOwnershipControlsCommand,
  GetBucketOwnershipControlsCommand,
  DeleteBucketOwnershipControlsCommand,
  PutBucketRequestPaymentCommand,
  GetBucketRequestPaymentCommand,
  PutBucketAccelerateConfigurationCommand,
  GetBucketAccelerateConfigurationCommand,
  BucketCannedACL,
} from '@aws-sdk/client-s3';
import { S3TestContext, S3TestSection, s3CleanupBucket } from './context.js';
import { assertNotNil, assertErrorContains } from '../../helpers.js';

export const runBucketConfigTests: S3TestSection = async (ctx, runner) => {
  const results = [];
  const { client, bucketName, lockBucket } = ctx;

  // ========== BUCKET TAGGING ==========
  results.push(await runner.runTest('s3', 'PutBucketTagging', async () => {
    await client.send(new PutBucketTaggingCommand({
      Bucket: bucketName,
      Tagging: {
        TagSet: [
          { Key: 'Environment', Value: 'Test' },
        ],
      },
    }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketTagging', async () => {
    const resp = await client.send(new GetBucketTaggingCommand({ Bucket: bucketName }));
    assertNotNil(resp.TagSet, 'TagSet');
    const found = resp.TagSet.some(
      (t) => t.Key === 'Environment' && t.Value === 'Test',
    );
    if (!found) {
      throw new Error('expected tag Environment=Test not found in TagSet');
    }
  }));

  results.push(await runner.runTest('s3', 'DeleteBucketTagging', async () => {
    await client.send(new DeleteBucketTaggingCommand({ Bucket: bucketName }));
  }));

  // ========== BUCKET ACL ==========
  results.push(await runner.runTest('s3', 'PutBucketAcl', async () => {
    await client.send(new PutBucketAclCommand({
      Bucket: bucketName,
      ACL: BucketCannedACL.private,
    }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketAcl', async () => {
    const resp = await client.send(new GetBucketAclCommand({ Bucket: bucketName }));
    assertNotNil(resp.Owner, 'Owner');
  }));

  // ========== BUCKET POLICY ==========
  results.push(await runner.runTest('s3', 'PutBucketPolicy', async () => {
    const policy = `{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":"*","Action":"s3:GetObject","Resource":"arn:aws:s3:::${bucketName}/*"}]}`;
    await client.send(new PutBucketPolicyCommand({
      Bucket: bucketName,
      Policy: policy,
    }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketPolicy', async () => {
    const resp = await client.send(new GetBucketPolicyCommand({ Bucket: bucketName }));
    assertNotNil(resp.Policy, 'Policy');
    if (!resp.Policy.includes('Allow')) {
      throw new Error('policy missing expected content');
    }
  }));

  results.push(await runner.runTest('s3', 'DeleteBucketPolicy', async () => {
    await client.send(new DeleteBucketPolicyCommand({ Bucket: bucketName }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketPolicy_NotFound', async () => {
    let err: unknown;
    try {
      await client.send(new GetBucketPolicyCommand({ Bucket: bucketName }));
    } catch (e) {
      err = e;
    }
    assertErrorContains(err, 'NoSuchBucketPolicy');
  }));

  // ========== BUCKET CORS ==========
  results.push(await runner.runTest('s3', 'PutBucketCors', async () => {
    await client.send(new PutBucketCorsCommand({
      Bucket: bucketName,
      CORSConfiguration: {
        CORSRules: [
          {
            AllowedMethods: ['GET', 'PUT'],
            AllowedOrigins: ['https://example.com'],
            MaxAgeSeconds: 3600,
          },
        ],
      },
    }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketCors', async () => {
    const resp = await client.send(new GetBucketCorsCommand({ Bucket: bucketName }));
    if (!resp.CORSRules || resp.CORSRules.length === 0) {
      throw new Error('expected at least one CORS rule');
    }
    if (resp.CORSRules[0].MaxAgeSeconds !== 3600) {
      throw new Error(`MaxAgeSeconds mismatch, got ${resp.CORSRules[0].MaxAgeSeconds}`);
    }
  }));

  results.push(await runner.runTest('s3', 'DeleteBucketCors', async () => {
    await client.send(new DeleteBucketCorsCommand({ Bucket: bucketName }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketCors_NotFound', async () => {
    let err: unknown;
    try {
      await client.send(new GetBucketCorsCommand({ Bucket: bucketName }));
    } catch (e) {
      err = e;
    }
    assertErrorContains(err, 'NoSuchCORSConfiguration');
  }));

  // ========== BUCKET ENCRYPTION ==========
  results.push(await runner.runTest('s3', 'PutBucketEncryption', async () => {
    await client.send(new PutBucketEncryptionCommand({
      Bucket: bucketName,
      ServerSideEncryptionConfiguration: {
        Rules: [
          {
            ApplyServerSideEncryptionByDefault: {
              SSEAlgorithm: 'AES256',
            },
          },
        ],
      },
    }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketEncryption', async () => {
    const resp = await client.send(new GetBucketEncryptionCommand({ Bucket: bucketName }));
    assertNotNil(resp.ServerSideEncryptionConfiguration, 'ServerSideEncryptionConfiguration');
    if (!resp.ServerSideEncryptionConfiguration.Rules || resp.ServerSideEncryptionConfiguration.Rules.length === 0) {
      throw new Error('expected at least one encryption rule');
    }
  }));

  results.push(await runner.runTest('s3', 'DeleteBucketEncryption', async () => {
    await client.send(new DeleteBucketEncryptionCommand({ Bucket: bucketName }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketEncryption_NotFound', async () => {
    let err: unknown;
    try {
      await client.send(new GetBucketEncryptionCommand({ Bucket: bucketName }));
    } catch (e) {
      err = e;
    }
    assertErrorContains(err, 'ServerSideEncryptionConfigurationNotFoundError');
  }));

  // ========== BUCKET VERSIONING ==========
  results.push(await runner.runTest('s3', 'PutBucketVersioning_Enabled', async () => {
    await client.send(new PutBucketVersioningCommand({
      Bucket: bucketName,
      VersioningConfiguration: { Status: 'Enabled' },
    }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketVersioning_Enabled', async () => {
    const resp = await client.send(new GetBucketVersioningCommand({ Bucket: bucketName }));
    if (resp.Status !== 'Enabled') {
      throw new Error(`expected Enabled, got ${resp.Status}`);
    }
  }));

  results.push(await runner.runTest('s3', 'PutBucketVersioning_Suspended', async () => {
    await client.send(new PutBucketVersioningCommand({
      Bucket: bucketName,
      VersioningConfiguration: { Status: 'Suspended' },
    }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketVersioning_Suspended', async () => {
    const resp = await client.send(new GetBucketVersioningCommand({ Bucket: bucketName }));
    if (resp.Status !== 'Suspended') {
      throw new Error(`expected Suspended, got ${resp.Status}`);
    }
  }));

  // ========== BUCKET LIFECYCLE ==========
  results.push(await runner.runTest('s3', 'PutBucketLifecycleConfiguration', async () => {
    await client.send(new PutBucketLifecycleConfigurationCommand({
      Bucket: bucketName,
      LifecycleConfiguration: {
        Rules: [
          {
            ID: 'test-expire-rule',
            Status: 'Enabled',
            Filter: { Prefix: 'logs/' },
            Expiration: { Days: 30 },
          },
        ],
      },
    }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketLifecycleConfiguration', async () => {
    const resp = await client.send(new GetBucketLifecycleConfigurationCommand({ Bucket: bucketName }));
    if (!resp.Rules || resp.Rules.length === 0) {
      throw new Error('expected at least one lifecycle rule');
    }
    if (resp.Rules[0].ID !== 'test-expire-rule') {
      throw new Error(`rule ID mismatch, got ${resp.Rules[0].ID}`);
    }
  }));

  results.push(await runner.runTest('s3', 'DeleteBucketLifecycleConfiguration', async () => {
    await client.send(new DeleteBucketLifecycleCommand({ Bucket: bucketName }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketLifecycleConfiguration_NotFound', async () => {
    let err: unknown;
    try {
      await client.send(new GetBucketLifecycleConfigurationCommand({ Bucket: bucketName }));
    } catch (e) {
      err = e;
    }
    assertErrorContains(err, 'NoSuchLifecycleConfiguration');
  }));

  // ========== BUCKET WEBSITE ==========
  results.push(await runner.runTest('s3', 'PutBucketWebsite', async () => {
    await client.send(new PutBucketWebsiteCommand({
      Bucket: bucketName,
      WebsiteConfiguration: {
        IndexDocument: { Suffix: 'index.html' },
        ErrorDocument: { Key: 'error.html' },
      },
    }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketWebsite', async () => {
    const resp = await client.send(new GetBucketWebsiteCommand({ Bucket: bucketName }));
    if (!resp.IndexDocument || resp.IndexDocument.Suffix !== 'index.html') {
      throw new Error(`IndexDocument Suffix mismatch, got ${JSON.stringify(resp.IndexDocument)}`);
    }
    if (!resp.ErrorDocument || resp.ErrorDocument.Key !== 'error.html') {
      throw new Error(`ErrorDocument Key mismatch, got ${JSON.stringify(resp.ErrorDocument)}`);
    }
  }));

  results.push(await runner.runTest('s3', 'DeleteBucketWebsite', async () => {
    await client.send(new DeleteBucketWebsiteCommand({ Bucket: bucketName }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketWebsite_NotFound', async () => {
    let err: unknown;
    try {
      await client.send(new GetBucketWebsiteCommand({ Bucket: bucketName }));
    } catch (e) {
      err = e;
    }
    assertErrorContains(err, 'NoSuchWebsiteConfiguration');
  }));

  // ========== OBJECT LOCK CONFIGURATION ==========
  results.push(await runner.runTest('s3', 'PutObjectLockConfiguration', async () => {
    await client.send(new CreateBucketCommand({
      Bucket: lockBucket,
      ObjectLockEnabledForBucket: true,
    }));

    await client.send(new PutObjectLockConfigurationCommand({
      Bucket: lockBucket,
      ObjectLockConfiguration: {
        ObjectLockEnabled: 'Enabled',
        Rule: {
          DefaultRetention: {
            Mode: 'GOVERNANCE',
            Days: 10,
          },
        },
      },
    }));
  }));

  results.push(await runner.runTest('s3', 'GetObjectLockConfiguration', async () => {
    const resp = await client.send(new GetObjectLockConfigurationCommand({ Bucket: lockBucket }));
    assertNotNil(resp.ObjectLockConfiguration, 'ObjectLockConfiguration');
    if (resp.ObjectLockConfiguration.ObjectLockEnabled !== 'Enabled') {
      throw new Error(`expected Enabled, got ${resp.ObjectLockConfiguration.ObjectLockEnabled}`);
    }
  }));

  // ========== BUCKET NOTIFICATION ==========
  results.push(await runner.runTest('s3', 'PutBucketNotificationConfiguration', async () => {
    await client.send(new PutBucketNotificationConfigurationCommand({
      Bucket: bucketName,
      NotificationConfiguration: {
        TopicConfigurations: [
          {
            TopicArn: 'arn:aws:sns:us-east-1:123456789012:test-topic',
            Events: ['s3:ObjectCreated:Put'],
          },
        ],
      },
    }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketNotificationConfiguration', async () => {
    const resp = await client.send(new GetBucketNotificationConfigurationCommand({ Bucket: bucketName }));
    if (!resp.TopicConfigurations || resp.TopicConfigurations.length === 0) {
      throw new Error('expected at least one topic configuration');
    }
  }));

  // ========== BUCKET LOGGING ==========
  results.push(await runner.runTest('s3', 'PutBucketLogging', async () => {
    await client.send(new PutBucketLoggingCommand({
      Bucket: bucketName,
      BucketLoggingStatus: {
        LoggingEnabled: {
          TargetBucket: bucketName,
          TargetPrefix: 'logs/',
        },
      },
    }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketLogging', async () => {
    const resp = await client.send(new GetBucketLoggingCommand({ Bucket: bucketName }));
    if (resp.LoggingEnabled) {
      if (resp.LoggingEnabled.TargetPrefix !== 'logs/') {
        throw new Error(`TargetPrefix mismatch, got ${resp.LoggingEnabled.TargetPrefix}`);
      }
    }
  }));

  // ========== PUBLIC ACCESS BLOCK ==========
  results.push(await runner.runTest('s3', 'PutPublicAccessBlock', async () => {
    await client.send(new PutPublicAccessBlockCommand({
      Bucket: bucketName,
      PublicAccessBlockConfiguration: {
        BlockPublicAcls: true,
        IgnorePublicAcls: true,
        BlockPublicPolicy: true,
        RestrictPublicBuckets: true,
      },
    }));
  }));

  results.push(await runner.runTest('s3', 'GetPublicAccessBlock', async () => {
    const resp = await client.send(new GetPublicAccessBlockCommand({ Bucket: bucketName }));
    const cfg = resp.PublicAccessBlockConfiguration;
    assertNotNil(cfg, 'PublicAccessBlockConfiguration');
    if (cfg.BlockPublicAcls !== true) {
      throw new Error('BlockPublicAcls should be true');
    }
    if (cfg.RestrictPublicBuckets !== true) {
      throw new Error('RestrictPublicBuckets should be true');
    }
  }));

  results.push(await runner.runTest('s3', 'DeletePublicAccessBlock', async () => {
    await client.send(new DeletePublicAccessBlockCommand({ Bucket: bucketName }));
  }));

  results.push(await runner.runTest('s3', 'GetPublicAccessBlock_NotFound', async () => {
    let err: unknown;
    try {
      await client.send(new GetPublicAccessBlockCommand({ Bucket: bucketName }));
    } catch (e) {
      err = e;
    }
    assertErrorContains(err, 'NoSuchPublicAccessBlockConfiguration');
  }));

  // ========== BUCKET OWNERSHIP CONTROLS ==========
  results.push(await runner.runTest('s3', 'PutBucketOwnershipControls', async () => {
    await client.send(new PutBucketOwnershipControlsCommand({
      Bucket: bucketName,
      OwnershipControls: {
        Rules: [
          { ObjectOwnership: 'BucketOwnerPreferred' },
        ],
      },
    }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketOwnershipControls', async () => {
    const resp = await client.send(new GetBucketOwnershipControlsCommand({ Bucket: bucketName }));
    const oc = resp.OwnershipControls;
    if (!oc || !oc.Rules || oc.Rules.length === 0) {
      throw new Error('expected at least one ownership controls rule');
    }
    const rule = oc.Rules[0];
    if (!rule || rule.ObjectOwnership !== 'BucketOwnerPreferred') {
      throw new Error(`ObjectOwnership mismatch, got ${rule?.ObjectOwnership}`);
    }
  }));

  results.push(await runner.runTest('s3', 'DeleteBucketOwnershipControls', async () => {
    await client.send(new DeleteBucketOwnershipControlsCommand({ Bucket: bucketName }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketOwnershipControls_NotFound', async () => {
    let err: unknown;
    try {
      await client.send(new GetBucketOwnershipControlsCommand({ Bucket: bucketName }));
    } catch (e) {
      err = e;
    }
    assertErrorContains(err, 'OwnershipControlsNotFoundError');
  }));

  // ========== BUCKET REQUEST PAYMENT ==========
  results.push(await runner.runTest('s3', 'PutBucketRequestPayment', async () => {
    await client.send(new PutBucketRequestPaymentCommand({
      Bucket: bucketName,
      RequestPaymentConfiguration: {
        Payer: 'Requester',
      },
    }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketRequestPayment', async () => {
    const resp = await client.send(new GetBucketRequestPaymentCommand({ Bucket: bucketName }));
    if (resp.Payer !== 'Requester') {
      throw new Error(`expected Requester, got ${resp.Payer}`);
    }
  }));

  // ========== BUCKET ACCELERATE CONFIGURATION ==========
  results.push(await runner.runTest('s3', 'PutBucketAccelerateConfiguration', async () => {
    await client.send(new PutBucketAccelerateConfigurationCommand({
      Bucket: bucketName,
      AccelerateConfiguration: {
        Status: 'Suspended',
      },
    }));
  }));

  results.push(await runner.runTest('s3', 'GetBucketAccelerateConfiguration', async () => {
    const resp = await client.send(new GetBucketAccelerateConfigurationCommand({ Bucket: bucketName }));
    if (resp.Status !== 'Suspended') {
      throw new Error(`expected Suspended, got ${resp.Status}`);
    }
  }));

  return results;
};
