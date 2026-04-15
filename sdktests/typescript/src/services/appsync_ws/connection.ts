import type { TestRunner, TestResult } from '../../runner.js';
import { dialWS, drainAck, sendJSON } from './context.js';

export async function runConnectionTests(
  runner: TestRunner,
  wsUrl: string,
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('appsync-ws', 'WebSocket_ConnectionAck', async () => {
    const conn = await dialWS(wsUrl);
    try {
      const msg = await conn.readMessage(3000);
      if (msg.type !== 'connection_ack') {
        throw new Error(`expected connection_ack, got ${msg.type}`);
      }
      if (msg.connectionTimeoutMs === undefined) {
        throw new Error('connection_ack missing connectionTimeoutMs');
      }
    } finally {
      await conn.close();
    }
  }));

  results.push(await runner.runTest('appsync-ws', 'WebSocket_ConnectionInit_Accepted', async () => {
    const conn = await dialWS(wsUrl);
    try {
      await drainAck(conn);
      sendJSON(conn, { type: 'connection_init' });
      try {
        await conn.readMessage(2000);
        throw new Error('expected no response to connection_init (server should accept silently), but got a message');
      } catch (err) {
        if (err instanceof Error && err.message === 'read timeout') return;
        throw err;
      }
    } finally {
      await conn.close();
    }
  }));

  results.push(await runner.runTest('appsync-ws', 'WebSocket_UnknownMessageType', async () => {
    const conn = await dialWS(wsUrl);
    try {
      await drainAck(conn);
      sendJSON(conn, { type: 'bogus_type' });
      const resp = await conn.readMessage(3000);
      if (resp.type !== 'error') {
        throw new Error(`expected error, got ${resp.type}`);
      }
    } finally {
      await conn.close();
    }
  }));
}
