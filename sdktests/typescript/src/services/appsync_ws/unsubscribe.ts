import type { TestRunner, TestResult } from '../../runner.js';
import { dialWS, drainAck, sendJSON } from './context.js';

export async function runUnsubscribeTests(
  runner: TestRunner,
  wsUrl: string,
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('appsync-ws', 'WebSocket_UnsubscribeSuccess', async () => {
    const conn = await dialWS(wsUrl);
    try {
      await drainAck(conn);
      sendJSON(conn, { type: 'subscribe', id: 'sub-unsub', channel: '/default/unsub-test' });
      await conn.readMessage(3000);
      sendJSON(conn, { type: 'unsubscribe', id: 'sub-unsub' });
      const resp = await conn.readMessage(3000);
      if (resp.type !== 'unsubscribe_success') {
        throw new Error(`expected unsubscribe_success, got ${resp.type}`);
      }
      if (resp.id !== 'sub-unsub') {
        throw new Error(`expected id sub-unsub, got ${resp.id}`);
      }
    } finally {
      await conn.close();
    }
  }));

  results.push(await runner.runTest('appsync-ws', 'WebSocket_UnsubscribeError_UnknownId', async () => {
    const conn = await dialWS(wsUrl);
    try {
      await drainAck(conn);
      sendJSON(conn, { type: 'unsubscribe', id: 'nonexistent' });
      const resp = await conn.readMessage(3000);
      if (resp.type !== 'unsubscribe_error') {
        throw new Error(`expected unsubscribe_error, got ${resp.type}`);
      }
    } finally {
      await conn.close();
    }
  }));

  results.push(await runner.runTest('appsync-ws', 'WebSocket_UnsubscribeStopsDelivery', async () => {
    const conn = await dialWS(wsUrl);
    try {
      await drainAck(conn);
      const ch = `/default/unsubstop-${Date.now()}`;
      sendJSON(conn, { type: 'subscribe', id: 'stop-sub', channel: ch });
      await conn.readMessage(3000);
      sendJSON(conn, { type: 'unsubscribe', id: 'stop-sub' });
      await conn.readMessage(3000);
      sendJSON(conn, { type: 'publish', id: 'stop-pub', channel: ch, events: ['{"after":"unsub"}'] });
      await conn.readMessage(3000);
      try {
        await conn.readMessage(2000);
        throw new Error('expected timeout after unsubscribe, but got a message');
      } catch (err) {
        if (err instanceof Error && err.message === 'read timeout') return;
        throw err;
      }
    } finally {
      await conn.close();
    }
  }));
}
