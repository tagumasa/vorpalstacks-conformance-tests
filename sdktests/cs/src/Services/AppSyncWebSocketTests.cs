using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace VorpalStacks.SDK.Tests.Services;

public static class AppSyncWebSocketTests
{
    private const string WsTestEndpoint = "ws://127.0.0.1:8086/event/realtime";
    private const string WsTestHTTPEndpoint = "http://127.0.0.1:8086/event";

    private static string JStr(Dictionary<string, JsonElement> d, string key) =>
        d.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : null;

    public static async Task<List<TestResult>> RunTests(TestRunner runner)
    {
        var results = new List<TestResult>();

        results.Add(await runner.RunTestAsync("appsync-ws", "WebSocket_ConnectionAck", async () =>
        {
            using var conn = await DialWSAsync();
            var msg = await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));
            if (JStr(msg, "type") != "connection_ack") throw new Exception($"expected connection_ack, got {JStr(msg, "type")}");
            if (!msg.ContainsKey("connectionTimeoutMs")) throw new Exception("connection_ack missing connectionTimeoutMs");
        }));

        results.Add(await runner.RunTestAsync("appsync-ws", "WebSocket_SubscribeSuccess", async () =>
        {
            using var conn = await DialWSAsync();
            await DrainAckAsync(conn);

            await conn.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "subscribe", id = "sub-1", channel = "/default/test" })), WebSocketMessageType.Text, true, CancellationToken.None);
            var resp = await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));
            if (JStr(resp, "type") != "subscribe_success") throw new Exception($"expected subscribe_success, got {JStr(resp, "type")}");
            if (JStr(resp, "id") != "sub-1") throw new Exception($"expected id sub-1, got {JStr(resp, "id")}");
        }));

        results.Add(await runner.RunTestAsync("appsync-ws", "WebSocket_SubscribeError_InvalidChannel", async () =>
        {
            using var conn = await DialWSAsync();
            await DrainAckAsync(conn);

            await conn.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "subscribe", id = "sub-bad-ch", channel = "" })), WebSocketMessageType.Text, true, CancellationToken.None);
            var resp = await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));
            if (JStr(resp, "type") != "subscribe_error") throw new Exception($"expected subscribe_error, got {JStr(resp, "type")}");
        }));

        results.Add(await runner.RunTestAsync("appsync-ws", "WebSocket_SubscribeError_DuplicateId", async () =>
        {
            using var conn = await DialWSAsync();
            await DrainAckAsync(conn);

            var subMsg = JsonSerializer.Serialize(new { type = "subscribe", id = "sub-dup", channel = "/default/ch1" });
            await conn.SendAsync(Encoding.UTF8.GetBytes(subMsg), WebSocketMessageType.Text, true, CancellationToken.None);
            await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));

            await conn.SendAsync(Encoding.UTF8.GetBytes(subMsg), WebSocketMessageType.Text, true, CancellationToken.None);
            var resp = await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));
            if (JStr(resp, "type") != "subscribe_error") throw new Exception($"expected subscribe_error for duplicate, got {JStr(resp, "type")}");
        }));

        results.Add(await runner.RunTestAsync("appsync-ws", "WebSocket_SubscribeError_InvalidSubId", async () =>
        {
            using var conn = await DialWSAsync();
            await DrainAckAsync(conn);

            await conn.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "subscribe", id = "", channel = "/default/test" })), WebSocketMessageType.Text, true, CancellationToken.None);
            var resp = await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));
            if (JStr(resp, "type") != "subscribe_error") throw new Exception($"expected subscribe_error, got {JStr(resp, "type")}");
        }));

        results.Add(await runner.RunTestAsync("appsync-ws", "WebSocket_PublishAndReceiveData", async () =>
        {
            using var conn = await DialWSAsync();
            await DrainAckAsync(conn);

            var ch = $"/default/pubtest-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            await conn.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "subscribe", id = "sub-pub", channel = ch })), WebSocketMessageType.Text, true, CancellationToken.None);
            await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));

            await conn.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "publish", id = "pub-1", channel = ch, events = new[] { "{\"msg\":\"hello\"}" } })), WebSocketMessageType.Text, true, CancellationToken.None);

            var msgs = await ReadMessagesAsync(conn, TimeSpan.FromSeconds(3), 2);
            var pubResp = msgs.FirstOrDefault(m => JStr(m, "type") == "publish_success");
            var dataMsg = msgs.FirstOrDefault(m => JStr(m, "type") == "data");
            if (pubResp == null) throw new Exception($"expected publish_success, got types: {string.Join(",", msgs.Select(m => m["type"]))}");
            if (dataMsg == null) throw new Exception($"expected data message, got types: {string.Join(",", msgs.Select(m => m["type"]))}");
            if (JStr(dataMsg, "id") != "sub-pub") throw new Exception($"expected data id sub-pub, got {JStr(dataMsg, "id")}");
        }));

        results.Add(await runner.RunTestAsync("appsync-ws", "WebSocket_PublishError_EmptyEvents", async () =>
        {
            using var conn = await DialWSAsync();
            await DrainAckAsync(conn);

            await conn.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "publish", id = "pub-empty", channel = "/default/test", events = new string[0] })), WebSocketMessageType.Text, true, CancellationToken.None);
            var resp = await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));
            if (JStr(resp, "type") != "publish_error") throw new Exception($"expected publish_error, got {JStr(resp, "type")}");
        }));

        results.Add(await runner.RunTestAsync("appsync-ws", "WebSocket_UnsubscribeSuccess", async () =>
        {
            using var conn = await DialWSAsync();
            await DrainAckAsync(conn);

            await conn.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "subscribe", id = "sub-unsub", channel = "/default/unsub-test" })), WebSocketMessageType.Text, true, CancellationToken.None);
            await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));

            await conn.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "unsubscribe", id = "sub-unsub" })), WebSocketMessageType.Text, true, CancellationToken.None);
            var resp = await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));
            if (JStr(resp, "type") != "unsubscribe_success") throw new Exception($"expected unsubscribe_success, got {JStr(resp, "type")}");
            if (JStr(resp, "id") != "sub-unsub") throw new Exception($"expected id sub-unsub, got {JStr(resp, "id")}");
        }));

        results.Add(await runner.RunTestAsync("appsync-ws", "WebSocket_UnsubscribeError_UnknownId", async () =>
        {
            using var conn = await DialWSAsync();
            await DrainAckAsync(conn);

            await conn.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "unsubscribe", id = "nonexistent" })), WebSocketMessageType.Text, true, CancellationToken.None);
            var resp = await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));
            if (JStr(resp, "type") != "unsubscribe_error") throw new Exception($"expected unsubscribe_error, got {JStr(resp, "type")}");
        }));

        results.Add(await runner.RunTestAsync("appsync-ws", "WebSocket_ConnectionInit_Accepted", async () =>
        {
            using var conn = await DialWSAsync();
            await DrainAckAsync(conn);

            await conn.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "connection_init" })), WebSocketMessageType.Text, true, CancellationToken.None);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                var buffer = new byte[1024];
                var result = await conn.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                throw new Exception("expected no response to connection_init (server should accept silently), but got a message");
            }
            catch (OperationCanceledException) { }
        }));

        results.Add(await runner.RunTestAsync("appsync-ws", "WebSocket_UnknownMessageType", async () =>
        {
            using var conn = await DialWSAsync();
            await DrainAckAsync(conn);

            await conn.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "bogus_type" })), WebSocketMessageType.Text, true, CancellationToken.None);
            var resp = await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));
            if (JStr(resp, "type") != "error") throw new Exception($"expected error, got {JStr(resp, "type")}");
        }));

        results.Add(await runner.RunTestAsync("appsync-ws", "WebSocket_MultiSubscriberFanOut", async () =>
        {
            using var conn1 = await DialWSAsync();
            using var conn2 = await DialWSAsync();
            await DrainAckAsync(conn1);
            await DrainAckAsync(conn2);

            var ch = $"/default/fanout-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            await conn1.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "subscribe", id = "fan-sub-1", channel = ch })), WebSocketMessageType.Text, true, CancellationToken.None);
            await ReadMessageAsync(conn1, TimeSpan.FromSeconds(3));
            await conn2.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "subscribe", id = "fan-sub-2", channel = ch })), WebSocketMessageType.Text, true, CancellationToken.None);
            await ReadMessageAsync(conn2, TimeSpan.FromSeconds(3));

            await conn1.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "publish", id = "fan-pub", channel = ch, events = new[] { "{\"fan\":\"out\"}" } })), WebSocketMessageType.Text, true, CancellationToken.None);

            var msgs1 = await ReadMessagesAsync(conn1, TimeSpan.FromSeconds(3), 2);
            if (!msgs1.Any(m => JStr(m, "type") == "publish_success")) throw new Exception($"conn1: expected publish_success");
            if (!msgs1.Any(m => JStr(m, "type") == "data")) throw new Exception($"conn1: expected data");

            var data2 = await ReadMessageAsync(conn2, TimeSpan.FromSeconds(3));
            if (JStr(data2, "type") != "data") throw new Exception($"conn2: expected data, got {JStr(data2, "type")}");
        }));

        results.Add(await runner.RunTestAsync("appsync-ws", "WebSocket_WildcardChannel", async () =>
        {
            using var conn = await DialWSAsync();
            await DrainAckAsync(conn);

            var ns = $"wildns-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            await conn.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "subscribe", id = "wild-sub", channel = $"/{ns}/*" })), WebSocketMessageType.Text, true, CancellationToken.None);
            await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));

            await conn.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "publish", id = "wild-pub", channel = $"/{ns}/topic1", events = new[] { "{\"wildcard\":true}" } })), WebSocketMessageType.Text, true, CancellationToken.None);

            var msgs = await ReadMessagesAsync(conn, TimeSpan.FromSeconds(3), 2);
            if (!msgs.Any(m => JStr(m, "type") == "publish_success")) throw new Exception("wildcard: expected publish_success");
            if (!msgs.Any(m => JStr(m, "type") == "data")) throw new Exception("wildcard: expected data");
        }));

        results.Add(await runner.RunTestAsync("appsync-ws", "WebSocket_UnsubscribeStopsDelivery", async () =>
        {
            using var conn = await DialWSAsync();
            await DrainAckAsync(conn);

            var ch = $"/default/unsubstop-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            await conn.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "subscribe", id = "stop-sub", channel = ch })), WebSocketMessageType.Text, true, CancellationToken.None);
            await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));

            await conn.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "unsubscribe", id = "stop-sub" })), WebSocketMessageType.Text, true, CancellationToken.None);
            await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));

            await conn.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "publish", id = "stop-pub", channel = ch, events = new[] { "{\"after\":\"unsub\"}" } })), WebSocketMessageType.Text, true, CancellationToken.None);
            await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                var buffer = new byte[1024];
                await conn.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                throw new Exception("expected timeout after unsubscribe, but got a message");
            }
            catch (OperationCanceledException) { }
        }));

        results.Add(await runner.RunTestAsync("appsync-ws", "HTTP_Publish_Success", async () =>
        {
            using var conn = await DialWSAsync();
            await DrainAckAsync(conn);

            var ch = $"/default/httppub-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            await conn.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "subscribe", id = "http-sub", channel = ch })), WebSocketMessageType.Text, true, CancellationToken.None);
            await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));

            var body = JsonSerializer.Serialize(new { channel = ch, events = new[] { "{\"via\":\"http\"}" } });
            using var http = new HttpClient();
            var httpResp = await http.PostAsync(WsTestHTTPEndpoint, new StringContent(body, System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json")));
            if (!httpResp.IsSuccessStatusCode) throw new Exception($"HTTP publish returned status {(int)httpResp.StatusCode}");
            var httpResult = JsonDocument.Parse(await httpResp.Content.ReadAsStringAsync());
            if (!httpResult.RootElement.TryGetProperty("successful", out var successful) || successful.GetArrayLength() != 1) throw new Exception($"expected 1 successful event");

            var dataMsg = await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));
            if (JStr(dataMsg, "type") != "data") throw new Exception($"expected data, got {JStr(dataMsg, "type")}");
        }));

        results.Add(await runner.RunTestAsync("appsync-ws", "HTTP_Publish_Error_EmptyChannel", async () =>
        {
            using var http = new HttpClient();
            var httpResp = await http.PostAsync(WsTestHTTPEndpoint, new StringContent("{\"channel\":\"\",\"events\":[\"{}\"]}", System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json")));
            if (httpResp.StatusCode != System.Net.HttpStatusCode.BadRequest) throw new Exception($"expected 400, got {(int)httpResp.StatusCode}");
        }));

        results.Add(await runner.RunTestAsync("appsync-ws", "HTTP_Publish_Error_TooManyEvents", async () =>
        {
            var evts = Enumerable.Repeat("{}", 6).ToArray();
            var body = JsonSerializer.Serialize(new { channel = "/default/test", events = evts });
            using var http = new HttpClient();
            var httpResp = await http.PostAsync(WsTestHTTPEndpoint, new StringContent(body, System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json")));
            if (httpResp.StatusCode != System.Net.HttpStatusCode.BadRequest) throw new Exception($"expected 400 for too many events, got {(int)httpResp.StatusCode}");
        }));

        return results;
    }

    private static async Task<ClientWebSocket> DialWSAsync()
    {
        var ws = new ClientWebSocket();
        ws.Options.AddSubProtocol("aws-appsync-event-ws");
        await ws.ConnectAsync(new Uri(WsTestEndpoint), CancellationToken.None);
        return ws;
    }

    private static async Task<Dictionary<string, JsonElement>> DrainAckAsync(ClientWebSocket conn)
    {
        var msg = await ReadMessageAsync(conn, TimeSpan.FromSeconds(3));
        if (JStr(msg, "type") != "connection_ack") throw new Exception($"expected connection_ack during drain, got {JStr(msg, "type")}");
        return msg;
    }

    private static async Task<Dictionary<string, JsonElement>> ReadMessageAsync(ClientWebSocket conn, TimeSpan timeout)
    {
        var cts = new CancellationTokenSource(timeout);
        using var ms = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await conn.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
            if (result.MessageType == WebSocketMessageType.Close) throw new Exception("WebSocket closed unexpectedly");
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        var json = Encoding.UTF8.GetString(ms.ToArray());
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new Dictionary<string, JsonElement>();
    }

    private static async Task<List<Dictionary<string, JsonElement>>> ReadMessagesAsync(ClientWebSocket conn, TimeSpan timeout, int count)
    {
        var deadline = DateTime.UtcNow + timeout;
        var msgs = new List<Dictionary<string, JsonElement>>();
        while (msgs.Count < count)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) break;
            try
            {
                var msg = await ReadMessageAsync(conn, remaining);
                msgs.Add(msg);
            }
            catch { break; }
        }
        if (msgs.Count < count) throw new Exception($"expected {count} messages, got {msgs.Count}");
        return msgs;
    }
}
