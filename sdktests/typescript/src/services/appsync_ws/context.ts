import WebSocket from 'ws';
import * as http from 'http';

export interface WSConn {
  ws: WebSocket;
  readMessage(timeout: number): Promise<Record<string, unknown>>;
  close(): Promise<void>;
}

export function dialWS(url: string): Promise<WSConn> {
  return new Promise((resolve, reject) => {
    const queue: Record<string, unknown>[] = [];
    const waiters: { resolve: (v: Record<string, unknown>) => void; timer: NodeJS.Timeout }[] = [];
    let closed = false;

    const ws = new WebSocket(url, 'aws-appsync-event-ws');

    ws.on('message', (data: WebSocket.RawData) => {
      const msg = JSON.parse(data.toString());
      if (waiters.length > 0) {
        const w = waiters.shift()!;
        clearTimeout(w.timer);
        w.resolve(msg);
      } else {
        queue.push(msg);
      }
    });

    function readMessage(timeout: number): Promise<Record<string, unknown>> {
      return new Promise((res, rej) => {
        if (closed) { rej(new Error('connection closed')); return; }
        if (queue.length > 0) {
          res(queue.shift()!);
          return;
        }
        const timer = setTimeout(() => {
          const idx = waiters.findIndex(w => w.resolve === res);
          if (idx >= 0) waiters.splice(idx, 1);
          rej(new Error('read timeout'));
        }, timeout);
        waiters.push({ resolve: res, timer });
      });
    }

    function close(): Promise<void> {
      closed = true;
      for (const { timer } of waiters) clearTimeout(timer);
      waiters.length = 0;
      queue.length = 0;
      return new Promise((res) => {
        ws.on('close', () => res());
        ws.close();
      });
    }

    ws.on('open', () => resolve({ ws, readMessage, close }));
    ws.on('error', (err) => reject(err));
  });
}

export async function drainAck(conn: WSConn): Promise<void> {
  const msg = await conn.readMessage(3000);
  if (msg.type !== 'connection_ack') {
    throw new Error(`expected connection_ack during drain, got ${msg.type}`);
  }
}

export async function readMessages(conn: WSConn, count: number, timeout: number): Promise<Record<string, unknown>[]> {
  const msgs: Record<string, unknown>[] = [];
  const deadline = Date.now() + timeout;
  for (const _ of Array(count).keys()) {
    const remaining = deadline - Date.now();
    if (remaining <= 0) break;
    msgs.push(await conn.readMessage(remaining));
  }
  if (msgs.length < count) {
    throw new Error(`expected ${count} messages, got ${msgs.length}`);
  }
  return msgs;
}

export function messageTypes(msgs: Record<string, unknown>[]): string[] {
  return msgs.map(m => (m.type as string) || '');
}

export function sendJSON(conn: WSConn, msg: Record<string, unknown>): void {
  conn.ws.send(JSON.stringify(msg));
}

export function httpPost(url: string, body: string): Promise<{ statusCode: number; body: string }> {
  return new Promise((resolve, reject) => {
    const parsed = new URL(url);
    const req = http.request({
      hostname: parsed.hostname,
      port: parsed.port || 80,
      path: parsed.pathname,
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Content-Length': Buffer.byteLength(body),
      },
    }, (res) => {
      let data = '';
      res.on('data', (chunk: Buffer) => { data += chunk.toString(); });
      res.on('end', () => resolve({ statusCode: res.statusCode ?? 0, body: data }));
    });
    req.on('error', reject);
    req.write(body);
    req.end();
  });
}
