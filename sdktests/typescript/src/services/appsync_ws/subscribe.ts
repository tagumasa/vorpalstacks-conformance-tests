import type { TestRunner, TestResult } from '../../runner.js';
import { dialWS, drainAck, sendJSON } from './context.js';

export async function runSubscribeTests(
  runner: TestRunner,
  wsUrl: string,
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('appsync-ws', 'WebSocket_SubscribeSuccess', async () => {
    const conn = await dialWS(wsUrl);
    try {
      await drainAck(conn);
      sendJSON(conn, { type: 'subscribe', id: 'sub-1', channel: '/default/test' });
      const resp = await conn.readMessage(3000);
      if (resp.type !== 'subscribe_success') {
        throw new Error(`expected subscribe_success, got ${resp.type}`);
      }
      if (resp.id !== 'sub-1') {
        throw new Error(`expected id sub-1, got ${resp.id}`);
      }
    } finally {
      await conn.close();
    }
  }));

  results.push(await runner.runTest('appsync-ws', 'WebSocket_SubscribeError_InvalidChannel', async () => {
    const conn = await dialWS(wsUrl);
    try {
      await drainAck(conn);
      sendJSON(conn, { type: 'subscribe', id: 'sub-bad-ch', channel: '' });
      const resp = await conn.readMessage(3000);
      if (resp.type !== 'subscribe_error') {
        throw new Error(`expected subscribe_error, got ${resp.type}`);
      }
    } finally {
      await conn.close();
    }
  }));

  results.push(await runner.runTest('appsync-ws', 'WebSocket_SubscribeError_DuplicateId', async () => {
    const conn = await dialWS(wsUrl);
    try {
      await drainAck(conn);
      sendJSON(conn, { type: 'subscribe', id: 'sub-dup', channel: '/default/ch1' });
      await conn.readMessage(3000);
      sendJSON(conn, { type: 'subscribe', id: 'sub-dup', channel: '/default/ch1' });
      const resp = await conn.readMessage(3000);
      if (resp.type !== 'subscribe_error') {
        throw new Error(`expected subscribe_error for duplicate, got ${resp.type}`);
      }
    } finally {
      await conn.close();
    }
  }));

  results.push(await runner.runTest('appsync-ws', 'WebSocket_SubscribeError_InvalidSubId', async () => {
    const conn = await dialWS(wsUrl);
    try {
      await drainAck(conn);
      sendJSON(conn, { type: 'subscribe', id: '', channel: '/default/test' });
      const resp = await conn.readMessage(3000);
      if (resp.type !== 'subscribe_error') {
        throw new Error(`expected subscribe_error, got ${resp.type}`);
      }
    } finally {
      await conn.close();
    }
  }));
}
