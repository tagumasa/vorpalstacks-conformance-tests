import {
  PutObjectCommand,
  SelectObjectContentCommand,
} from '@aws-sdk/client-s3';
import { S3TestContext, S3TestSection } from './context.js';
import { assertNotNil } from '../../helpers.js';

export const runSelectTests: S3TestSection = async (ctx, runner) => {
  const results = [];
  const { client, bucketName } = ctx;

  await client.send(new PutObjectCommand({
    Bucket: bucketName,
    Key: 'select-data.csv',
    Body: 'name,age\nAlice,30\nBob,25\n',
  }));

  results.push(await runner.runTest('s3', 'SelectObjectContent', async () => {
    const resp = await client.send(new SelectObjectContentCommand({
      Bucket: bucketName,
      Key: 'select-data.csv',
      Expression: "SELECT s.name FROM s3object s WHERE s.age = '30'",
      ExpressionType: 'SQL',
      InputSerialization: {
        CSV: { FileHeaderInfo: 'USE' },
      },
      OutputSerialization: {
        CSV: {},
      },
    }));
    assertNotNil(resp.Payload, 'Payload');

    const recordResults: string[] = [];
    for await (const event of resp.Payload) {
      if (event.Records && event.Records.Payload) {
        recordResults.push(new TextDecoder().decode(event.Records.Payload));
      }
    }

    if (recordResults.length === 0) {
      throw new Error('expected at least one record result');
    }
  }));

  return results;
};
