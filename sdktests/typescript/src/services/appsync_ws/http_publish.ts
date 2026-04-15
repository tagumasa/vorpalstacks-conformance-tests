import type { TestRunner, TestResult } from '../../runner.js';
import { dialWS, drainAck, sendJSON, httpPost } from './context.js';

export async function runHTTPPublishTests(
  runner: TestRunner,
  wsUrl: string,
  httpUrl: string,
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('appsync-ws', 'HTTP_Publish_Success', async () => {
    const conn = await dialWS(wsUrl);
    try {
      await drainAck(conn);
      const ch = `/default/httppub-${Date.now()}`;
      sendJSON(conn, { type: 'subscribe', id: 'http-sub', channel: ch });
      await conn.readMessage(3000);
      const body = JSON.stringify({ channel: ch, events: ['{"via":"http"}'] });
      const httpResp = await httpPost(httpUrl, body);
      if (httpResp.statusCode !== 200) {
        throw new Error(`HTTP publish returned status ${httpResp.statusCode}`);
      }
      const httpResult = JSON.parse(httpResp.body);
      if (!Array.isArray(httpResult.successful) || httpResult.successful.length !== 1) {
        throw new Error(`expected 1 successful event, got ${JSON.stringify(httpResult.successful)}`);
      }
      const dataMsg = await conn.readMessage(3000);
      if (dataMsg.type !== 'data') {
        throw new Error(`expected data, got ${dataMsg.type}`);
      }
    } finally {
      await conn.close();
    }
  }));

  results.push(await runner.runTest('appsync-ws', 'HTTP_Publish_Error_EmptyChannel', async () => {
    const body = JSON.stringify({ channel: '', events: ['{}'] });
    const httpResp = await httpPost(httpUrl, body);
    if (httpResp.statusCode !== 400) {
      throw new Error(`expected 400, got ${httpResp.statusCode}`);
    }
  }));

  results.push(await runner.runTest('appsync-ws', 'HTTP_Publish_Error_TooManyEvents', async () => {
    const evts: string[] = [];
    for (const _ of [0, 1, 2, 3, 4, 5]) evts.push('{}');
    const body = JSON.stringify({ channel: '/default/test', events: evts });
    const httpResp = await httpPost(httpUrl, body);
    if (httpResp.statusCode !== 400) {
      throw new Error(`expected 400 for too many events, got ${httpResp.statusCode}`);
    }
  }));
}
