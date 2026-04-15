import { DynamoDBClient } from '@aws-sdk/client-dynamodb';
import { TestRunner, TestResult } from '../../runner.js';

export interface DynamoDBTestContext {
  client: DynamoDBClient;
  ts: string;
  tableName: string;
  tableARN: string;
  compTableName: string;
}

export type DynamoDBTestSection = (ctx: DynamoDBTestContext, runner: TestRunner) => Promise<TestResult[]>;

export function createDynamoDBTestContext(
  endpoint: string,
  region: string,
  credentials: { accessKeyId: string; secretAccessKey: string },
): DynamoDBTestContext {
  const ts = String(Date.now());
  const tableName = `TestTable-${ts}`;
  const tableARN = `arn:aws:dynamodb:${region}:000000000000:table/${tableName}`;
  const compTableName = `CompTable-${ts}`;

  return {
    client: new DynamoDBClient({ endpoint, region, credentials }),
    ts,
    tableName,
    tableARN,
    compTableName,
  };
}
