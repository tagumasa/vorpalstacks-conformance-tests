import type { TestRunner, TestResult } from '../../runner.js';
import { dialWS, drainAck, sendJSON, readMessages, messageTypes } from './context.js';

export async function runPublishTests(
  runner: TestRunner,
  wsUrl: string,
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('appsync-ws', 'WebSocket_PublishAndReceiveData', async () => {
    const conn = await dialWS(wsUrl);
    try {
      await drainAck(conn);
      const ch = `/default/pubtest-${Date.now()}`;
      sendJSON(conn, { type: 'subscribe', id: 'sub-pub', channel: ch });
      await conn.readMessage(3000);
      sendJSON(conn, { type: 'publish', id: 'pub-1', channel: ch, events: ['{"msg":"hello"}'] });
      const msgs = await readMessages(conn, 2, 3000);
      let pubResp: Record<string, unknown> | undefined;
      let dataMsg: Record<string, unknown> | undefined;
      for (const m of msgs) {
        if (m.type === 'publish_success') pubResp = m;
        if (m.type === 'data') dataMsg = m;
      }
      if (!pubResp) {
        throw new Error(`expected publish_success, got types: ${messageTypes(msgs).join(',')}`);
      }
      if (!dataMsg) {
        throw new Error(`expected data message, got types: ${messageTypes(msgs).join(',')}`);
      }
      if (dataMsg.id !== 'sub-pub') {
        throw new Error(`expected data id sub-pub, got ${dataMsg.id}`);
      }
      const eventStr = dataMsg.event as string;
      if (!eventStr) {
        throw new Error(`event field is missing or not a string`);
      }
      const events = JSON.parse(eventStr);
      if (!Array.isArray(events) || events.length !== 1) {
        throw new Error(`expected 1 event, got ${events.length}`);
      }
    } finally {
      await conn.close();
    }
  }));

  results.push(await runner.runTest('appsync-ws', 'WebSocket_PublishError_EmptyEvents', async () => {
    const conn = await dialWS(wsUrl);
    try {
      await drainAck(conn);
      sendJSON(conn, { type: 'publish', id: 'pub-empty', channel: '/default/test', events: [] });
      const resp = await conn.readMessage(3000);
      if (resp.type !== 'publish_error') {
        throw new Error(`expected publish_error, got ${resp.type}`);
      }
    } finally {
      await conn.close();
    }
  }));

  results.push(await runner.runTest('appsync-ws', 'WebSocket_MultiSubscriberFanOut', async () => {
    const conn1 = await dialWS(wsUrl);
    try {
      const conn2 = await dialWS(wsUrl);
      try {
        await drainAck(conn1);
        await drainAck(conn2);
        const ch = `/default/fanout-${Date.now()}`;
        sendJSON(conn1, { type: 'subscribe', id: 'fan-sub-1', channel: ch });
        await conn1.readMessage(3000);
        sendJSON(conn2, { type: 'subscribe', id: 'fan-sub-2', channel: ch });
        await conn2.readMessage(3000);
        sendJSON(conn1, { type: 'publish', id: 'fan-pub', channel: ch, events: ['{"fan":"out"}'] });
        const msgs1 = await readMessages(conn1, 2, 3000);
        let foundPubAck = false;
        let foundData1 = false;
        for (const m of msgs1) {
          if (m.type === 'publish_success') foundPubAck = true;
          if (m.type === 'data') foundData1 = true;
        }
        if (!foundPubAck) {
          throw new Error(`conn1: expected publish_success, got types: ${messageTypes(msgs1).join(',')}`);
        }
        if (!foundData1) {
          throw new Error(`conn1: expected data, got types: ${messageTypes(msgs1).join(',')}`);
        }
        const data2 = await conn2.readMessage(3000);
        if (data2.type !== 'data') {
          throw new Error(`conn2: expected data, got ${data2.type}`);
        }
      } finally {
        await conn2.close();
      }
    } finally {
      await conn1.close();
    }
  }));

  results.push(await runner.runTest('appsync-ws', 'WebSocket_WildcardChannel', async () => {
    const conn = await dialWS(wsUrl);
    try {
      await drainAck(conn);
      const ns = `wildns-${Date.now()}`;
      sendJSON(conn, { type: 'subscribe', id: 'wild-sub', channel: `/${ns}/*` });
      await conn.readMessage(3000);
      sendJSON(conn, { type: 'publish', id: 'wild-pub', channel: `/${ns}/topic1`, events: ['{"wildcard":true}'] });
      const msgs = await readMessages(conn, 2, 3000);
      let foundPublish = false;
      let foundData = false;
      for (const m of msgs) {
        if (m.type === 'publish_success') foundPublish = true;
        if (m.type === 'data') foundData = true;
      }
      if (!foundPublish) {
        throw new Error(`wildcard: expected publish_success, got types: ${messageTypes(msgs).join(',')}`);
      }
      if (!foundData) {
        throw new Error(`wildcard: expected data, got types: ${messageTypes(msgs).join(',')}`);
      }
    } finally {
      await conn.close();
    }
  }));
}
