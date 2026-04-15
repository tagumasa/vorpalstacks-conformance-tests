import {
  KinesisClient,
  DescribeStreamCommand,
} from '@aws-sdk/client-kinesis';

export const TEST_KMS_KEY = 'arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012';

export interface StreamState {
  name: string;
  arn: string;
  shardIds: string[];
  created: boolean;
}

export async function waitForActive(
  client: KinesisClient,
  streamName: string,
  maxAttempts = 30,
): Promise<{ streamARN: string; shardIds: string[] }> {
  for (let attempt = 0; attempt < maxAttempts; attempt++) {
    const resp = await client.send(new DescribeStreamCommand({ StreamName: streamName }));
    const desc = resp.StreamDescription;
    if (desc?.StreamStatus === 'ACTIVE') {
      return {
        streamARN: desc.StreamARN ?? '',
        shardIds: (desc.Shards ?? []).map(s => s.ShardId).filter((id): id is string => !!id),
      };
    }
    await new Promise(resolve => setTimeout(resolve, 1000));
  }
  throw new Error(`stream ${streamName} did not become active`);
}
