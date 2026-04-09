using System.Text.Json;
using Amazon.Neptunedata;
using Amazon.Neptunedata.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static class NeptuneDataServiceTests
{
    private static string MarshalDoc(object? v)
    {
        if (v == null) return "null";
        var opts = new JsonSerializerOptions { WriteIndented = false };
        return JsonSerializer.Serialize(v, opts);
    }

    public static async Task<List<TestResult>> RunTests(TestRunner runner, AmazonNeptunedataClient client, string region)
    {
        var results = new List<TestResult>();

        // === Engine Status ===

        results.Add(await runner.RunTestAsync("neptunedata", "GetEngineStatus", async () =>
        {
            var resp = await client.GetEngineStatusAsync(new GetEngineStatusRequest());
            if (resp.Status != "healthy") throw new Exception($"expected status=healthy, got {resp.Status}");
            if (string.IsNullOrEmpty(resp.StartTime)) throw new Exception("expected non-empty startTime");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "GetEngineStatus_GremlinVersion", async () =>
        {
            var resp = await client.GetEngineStatusAsync(new GetEngineStatusRequest());
            if (resp.Gremlin == null || string.IsNullOrEmpty(resp.Gremlin.Version)) throw new Exception("expected non-empty gremlin version");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "GetEngineStatus_OpenCypherVersion", async () =>
        {
            var resp = await client.GetEngineStatusAsync(new GetEngineStatusRequest());
            if (resp.Opencypher == null || string.IsNullOrEmpty(resp.Opencypher.Version)) throw new Exception("expected non-empty opencypher version");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "GetEngineStatus_Role", async () =>
        {
            var resp = await client.GetEngineStatusAsync(new GetEngineStatusRequest());
            if (resp.Role != "writer" && resp.Role != "reader") throw new Exception($"expected role=writer|reader, got {resp.Role}");
        }));

        // === Fast Reset ===

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteFastReset_Initiate", async () =>
        {
            var resp = await client.ExecuteFastResetAsync(new ExecuteFastResetRequest
            {
                Action = "initiateDatabaseReset"
            });
            if (resp.Payload == null || string.IsNullOrEmpty(resp.Payload.Token)) throw new Exception("expected non-empty fast reset token");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteFastReset_Perform", async () =>
        {
            var initResp = await client.ExecuteFastResetAsync(new ExecuteFastResetRequest
            {
                Action = "initiateDatabaseReset"
            });
            if (initResp.Payload == null || string.IsNullOrEmpty(initResp.Payload.Token)) throw new Exception("expected non-empty token from initiateDatabaseReset");

            var performResp = await client.ExecuteFastResetAsync(new ExecuteFastResetRequest
            {
                Action = "performDatabaseReset",
                Token = initResp.Payload.Token
            });
            if (string.IsNullOrEmpty(performResp.Status)) throw new Exception("expected non-empty status from performDatabaseReset");
        }));

        // === OpenCypher Queries ===

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteOpenCypherQuery_CreateNode", async () =>
        {
            await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest
            {
                OpenCypherQuery = "CREATE (n:Person {name: 'marko', age: 29})"
            });
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteOpenCypherQuery_CreateMoreNodes", async () =>
        {
            await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = "CREATE (n:Person {name: 'vadas', age: 27})" });
            await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = "CREATE (n:Person {name: 'josh', age: 32})" });
            await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = "CREATE (n:Software {name: 'lop', lang: 'java'})" });
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteOpenCypherQuery_CreateRelationships", async () =>
        {
            await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = "MATCH (a:Person {name: 'marko'}), (b:Person {name: 'vadas'}) CREATE (a)-[:KNOWS {weight: 0.5}]->(b)" });
            await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = "MATCH (a:Person {name: 'marko'}), (b:Person {name: 'josh'}) CREATE (a)-[:KNOWS {weight: 1.0}]->(b)" });
            await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = "MATCH (a:Person {name: 'marko'}), (b:Software {name: 'lop'}) CREATE (a)-[:CREATED {weight: 0.4}]->(b)" });
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteOpenCypherQuery_MatchAllNodes", async () =>
        {
            var resp = await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = "MATCH (n) RETURN n.name ORDER BY n.name" });
            var s = MarshalDoc(resp.Results);
            foreach (var name in new[] { "\"marko\"", "\"vadas\"", "\"josh\"", "\"lop\"" })
            {
                if (!s.Contains(name)) throw new Exception($"expected node name {name} in results, got {s}");
            }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteOpenCypherQuery_MatchByProperty", async () =>
        {
            var resp = await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = "MATCH (n:Person {name: 'marko'}) RETURN n.age" });

            var s = MarshalDoc(resp.Results);
            if (!s.Contains("29")) throw new Exception($"expected age 29 in results, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteOpenCypherQuery_Traversal", async () =>
        {
            var resp = await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = "MATCH (a:Person {name: 'marko'})-[:KNOWS]->(friend) RETURN friend.name" });

            var s = MarshalDoc(resp.Results);
            foreach (var name in new[] { "\"vadas\"", "\"josh\"" })
            {
                if (!s.Contains(name)) throw new Exception($"expected friend {name} in traversal results, got {s}");
            }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteOpenCypherQuery_Aggregation", async () =>
        {
            var resp = await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = "MATCH (n:Person) RETURN count(n) AS cnt" });

            var s = MarshalDoc(resp.Results);
            if (!s.Contains("3")) throw new Exception($"expected count 3 in results, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteOpenCypherQuery_Parameters", async () =>
        {
            var resp = await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest
            {
                OpenCypherQuery = "MATCH (n:Person {name: $name}) RETURN n.age",
                Parameters = "{\"name\": \"marko\"}"
            });

            var s = MarshalDoc(resp.Results);
            if (!s.Contains("29")) throw new Exception($"expected age 29 from parameterised query, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteOpenCypherQuery_Delete", async () =>
        {
            await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = "MATCH (n:Software {name: 'lop'}) DETACH DELETE n" });
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteOpenCypherQuery_VerifyDelete", async () =>
        {
            var resp = await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = "MATCH (n:Software) RETURN count(n) AS cnt" });

            var s = MarshalDoc(resp.Results);
            if (!s.Contains("0")) throw new Exception($"expected count 0 after delete, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteOpenCypherExplainQuery", async () =>
        {
            var resp = await client.ExecuteOpenCypherExplainQueryAsync(new ExecuteOpenCypherExplainQueryRequest
            {
                OpenCypherQuery = "MATCH (n) RETURN n LIMIT 1",
                ExplainMode = OpenCypherExplainMode.Static
            });

        }));

        // === Gremlin Queries (after reset) ===

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_ResetGraph", async () =>
        {
            var initResp = await client.ExecuteFastResetAsync(new ExecuteFastResetRequest { Action = "initiateDatabaseReset" });
            if (initResp.Payload == null || string.IsNullOrEmpty(initResp.Payload.Token)) throw new Exception("expected token from initiateDatabaseReset");
            await client.ExecuteFastResetAsync(new ExecuteFastResetRequest { Action = "performDatabaseReset", Token = initResp.Payload.Token });
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteGremlinQuery_AddVertex", async () =>
        {
            var resp = await client.ExecuteGremlinQueryAsync(new ExecuteGremlinQueryRequest
            {
                GremlinQuery = "g.addV('person').property('name','marko').property('age',29)"
            });
            if (string.IsNullOrEmpty(resp.RequestId)) throw new Exception("expected non-empty requestId");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteGremlinQuery_AddMoreVertices", async () =>
        {
            var queries = new[]
            {
                "g.addV('person').property('name','vadas').property('age',27)",
                "g.addV('person').property('name','josh').property('age',32)",
                "g.addV('software').property('name','lop').property('lang','java')"
            };
            foreach (var q in queries)
                await client.ExecuteGremlinQueryAsync(new ExecuteGremlinQueryRequest { GremlinQuery = q });
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteGremlinQuery_AddEdges", async () =>
        {
            var queries = new[]
            {
                "g.V().has('name','marko').addE('knows').to(g.V().has('name','vadas')).property('weight',0.5)",
                "g.V().has('name','marko').addE('knows').to(g.V().has('name','josh')).property('weight',1.0)",
                "g.V().has('name','marko').addE('created').to(g.V().has('name','lop')).property('weight',0.4)"
            };
            foreach (var q in queries)
                await client.ExecuteGremlinQueryAsync(new ExecuteGremlinQueryRequest { GremlinQuery = q });
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteGremlinQuery_Count", async () =>
        {
            var resp = await client.ExecuteGremlinQueryAsync(new ExecuteGremlinQueryRequest { GremlinQuery = "g.V().count()" });
            var s = MarshalDoc(resp.Result);
            if (!s.Contains("4")) throw new Exception($"expected count 4 in gremlin result, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteGremlinQuery_Traversal", async () =>
        {
            var resp = await client.ExecuteGremlinQueryAsync(new ExecuteGremlinQueryRequest { GremlinQuery = "g.V().has('name','marko').out('knows').values('name')" });
            var s = MarshalDoc(resp.Result);
            foreach (var name in new[] { "vadas", "josh" })
            {
                if (!s.Contains(name)) throw new Exception($"expected '{name}' in gremlin traversal results, got {s}");
            }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteGremlinQuery_ValueMap", async () =>
        {
            var resp = await client.ExecuteGremlinQueryAsync(new ExecuteGremlinQueryRequest { GremlinQuery = "g.V().has('name','marko').valueMap()" });
            var s = MarshalDoc(resp.Result);
            foreach (var key in new[] { "name", "age" })
            {
                if (!s.Contains(key)) throw new Exception($"expected key '{key}' in valueMap result, got {s}");
            }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteGremlinQuery_HasLabel", async () =>
        {
            var resp = await client.ExecuteGremlinQueryAsync(new ExecuteGremlinQueryRequest { GremlinQuery = "g.V().hasLabel('software').count()" });
            var s = MarshalDoc(resp.Result);
            if (!s.Contains("1")) throw new Exception($"expected count 1 for software label, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteGremlinQuery_EdgeCount", async () =>
        {
            var resp = await client.ExecuteGremlinQueryAsync(new ExecuteGremlinQueryRequest { GremlinQuery = "g.E().count()" });
            var s = MarshalDoc(resp.Result);
            if (!s.Contains("3")) throw new Exception($"expected count 3 for edges, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteGremlinQuery_DropVertex", async () =>
        {
            await client.ExecuteGremlinQueryAsync(new ExecuteGremlinQueryRequest { GremlinQuery = "g.V().has('name','lop').drop()" });
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteGremlinQuery_VerifyDrop", async () =>
        {
            var resp = await client.ExecuteGremlinQueryAsync(new ExecuteGremlinQueryRequest { GremlinQuery = "g.V().has('name','lop').count()" });
            var s = MarshalDoc(resp.Result);
            if (!s.Contains("0")) throw new Exception($"expected count 0 after drop, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteGremlinExplainQuery", async () =>
        {
            var resp = await client.ExecuteGremlinExplainQueryAsync(new ExecuteGremlinExplainQueryRequest { GremlinQuery = "g.V().count()" });
            if (resp.Output == null) throw new Exception("expected non-nil explain output");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ExecuteGremlinProfileQuery", async () =>
        {
            var resp = await client.ExecuteGremlinProfileQueryAsync(new ExecuteGremlinProfileQueryRequest { GremlinQuery = "g.V().count()" });
            if (resp.Output == null) throw new Exception("expected non-nil profile output");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "GetGremlinQueryStatus", async () =>
        {
            var resp = await client.ExecuteGremlinQueryAsync(new ExecuteGremlinQueryRequest { GremlinQuery = "g.V().count()" });
            if (string.IsNullOrEmpty(resp.RequestId)) throw new Exception("expected requestId from gremlin query");

            var statusResp = await client.GetGremlinQueryStatusAsync(new GetGremlinQueryStatusRequest { QueryId = resp.RequestId });
            if (statusResp.QueryId != resp.RequestId) throw new Exception($"queryId mismatch: expected {resp.RequestId}, got {statusResp.QueryId}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "GetOpenCypherQueryStatus", async () =>
        {
            await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = "MATCH (n) RETURN count(n)" });
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "GetPropertygraphStatistics", async () =>
        {
            var resp = await client.GetPropertygraphStatisticsAsync(new GetPropertygraphStatisticsRequest());
            if (resp.Status == null) throw new Exception("expected non-nil status");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "GetPropertygraphSummary", async () =>
        {
            var resp = await client.GetPropertygraphSummaryAsync(new GetPropertygraphSummaryRequest { Mode = GraphSummaryType.Basic });
            if (resp.StatusCode == null) throw new Exception("expected non-nil statusCode");
        }));

        // === Loader Jobs ===

        string? loaderJobID = null;

        results.Add(await runner.RunTestAsync("neptunedata", "StartLoaderJob", async () =>
        {
            var resp = await client.StartLoaderJobAsync(new StartLoaderJobRequest
            {
                Source = "s3://test-bucket/data",
                Format = "csv",
                IamRoleArn = "arn:aws:iam::000000000000:role/NeptuneLoadRole",
                S3BucketRegion = "us-east-1"
            });
            if (resp.Payload == null) throw new Exception("expected non-nil loader job payload");
            var data = MarshalDoc(resp.Payload);
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("loadId", out var lid) && lid.ValueKind == JsonValueKind.String)
                loaderJobID = lid.GetString();
            else
                throw new Exception($"expected loadId in payload, got {data}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "GetLoaderJobStatus", async () =>
        {
            if (string.IsNullOrEmpty(loaderJobID)) throw new Exception("no loader job ID from StartLoaderJob");
            var resp = await client.GetLoaderJobStatusAsync(new GetLoaderJobStatusRequest { LoadId = loaderJobID });
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ListLoaderJobs", async () =>
        {
            var resp = await client.ListLoaderJobsAsync(new ListLoaderJobsRequest());
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "CancelLoaderJob", async () =>
        {
            if (loaderJobID == null) throw new Exception("no loader job ID from StartLoaderJob");
            await client.CancelLoaderJobAsync(new CancelLoaderJobRequest { LoadId = loaderJobID });
        }));

        // === Unsupported Operations ===

        results.Add(await runner.RunTestAsync("neptunedata", "GetSparqlStatistics_Unsupported", async () =>
        {
            try { await client.GetSparqlStatisticsAsync(new GetSparqlStatisticsRequest()); throw new Exception("expected error for unsupported SPARQL statistics"); }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "GetRDFGraphSummary_Unsupported", async () =>
        {
            try { await client.GetRDFGraphSummaryAsync(new GetRDFGraphSummaryRequest()); throw new Exception("expected error for unsupported RDF graph summary"); }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "StartMLDataProcessingJob_Unsupported", async () =>
        {
            try { await client.StartMLDataProcessingJobAsync(new StartMLDataProcessingJobRequest { InputDataS3Location = "s3://test/ml-input", ProcessedDataS3Location = "s3://test/ml-output" }); throw new Exception("expected error for unsupported ML data processing job"); }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ListGremlinQueries", async () =>
        {
            await client.ListGremlinQueriesAsync(new ListGremlinQueriesRequest());
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ListOpenCypherQueries", async () =>
        {
            await client.ListOpenCypherQueriesAsync(new ListOpenCypherQueriesRequest());
        }));

        // === Cypher Advanced Tests ===

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_Advanced_Reset", async () =>
        {
            var initResp = await client.ExecuteFastResetAsync(new ExecuteFastResetRequest { Action = "initiateDatabaseReset" });
            if (initResp.Payload == null || string.IsNullOrEmpty(initResp.Payload.Token)) throw new Exception("expected token");
            await client.ExecuteFastResetAsync(new ExecuteFastResetRequest { Action = "performDatabaseReset", Token = initResp.Payload.Token });
        }));

        async Task Cypher(string q) => await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = q });
        async Task<string> CypherResult(string q)
        {
            var resp = await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = q });
            return MarshalDoc(resp.Results);
        }
        async Task CypherContains(string q, string substr)
        {
            var s = await CypherResult(q);
            if (!s.Contains(substr)) throw new Exception($"expected {substr} in result, got {s}");
        }

        await Cypher("CREATE (a:Person {name:'alice',age:25}), (b:Person {name:'bob',age:30}), (c:Person {name:'charlie',age:35}), (d:Person {name:'dave',age:40})");
        await Cypher("MATCH (a:Person {name:'alice'}), (b:Person {name:'bob'}) CREATE (a)-[:KNOWS {weight:0.5}]->(b)");
        await Cypher("MATCH (a:Person {name:'alice'}), (c:Person {name:'charlie'}) CREATE (a)-[:KNOWS {weight:1.0}]->(c)");
        await Cypher("MATCH (b:Person {name:'bob'}), (d:Person {name:'dave'}) CREATE (b)-[:KNOWS {weight:0.7}]->(d)");
        await Cypher("MATCH (c:Person {name:'charlie'}), (d:Person {name:'dave'}) CREATE (c)-[:KNOWS {weight:0.3}]->(d)");
        await Cypher("MATCH (a:Person {name:'alice'}), (b:Person {name:'bob'}) CREATE (a)-[:WORKS_WITH]->(b)");
        await Cypher("MATCH (b:Person {name:'bob'}), (c:Person {name:'charlie'}) CREATE (b)-[:WORKS_WITH]->(c)");

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_WhereClause", async () => await CypherContains("MATCH (n:Person) WHERE n.age > 30 RETURN n.name", "\"charlie\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_WhereAND", async () => await CypherContains("MATCH (n:Person) WHERE n.age > 25 AND n.age < 40 RETURN n.name", "\"bob\"")));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_WhereOR", async () =>
        {
            var s = await CypherResult("MATCH (n:Person) WHERE n.name = 'alice' OR n.name = 'dave' RETURN n.name ORDER BY n.name");
            if (!s.Contains("\"alice\"") || !s.Contains("\"dave\"")) throw new Exception($"expected alice and dave, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_WhereNOT", async () =>
        {
            var s = await CypherResult("MATCH (n:Person) WHERE NOT n.name = 'alice' AND NOT n.name = 'dave' RETURN n.name ORDER BY n.name");
            if (!s.Contains("\"bob\"") || !s.Contains("\"charlie\"")) throw new Exception($"expected bob and charlie, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_WhereContains", async () => await CypherContains("MATCH (n:Person) WHERE n.name CONTAINS 'li' RETURN n.name", "\"alice\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_WhereStartsWith", async () => await CypherContains("MATCH (n:Person) WHERE n.name STARTS WITH 'al' RETURN n.name", "\"alice\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_WhereEndsWith", async () => await CypherContains("MATCH (n:Person) WHERE n.name ENDS WITH 'ie' RETURN n.name", "\"charlie\"")));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_WhereIN", async () =>
        {
            var s = await CypherResult("MATCH (n:Person) WHERE n.name IN ['alice', 'dave'] RETURN n.name ORDER BY n.name");
            if (!s.Contains("\"alice\"") || !s.Contains("\"dave\"")) throw new Exception($"expected alice and dave, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_WhereIsNull", async () =>
        {
            await Cypher("CREATE (n:Person {name:'eve'})");
            await CypherContains("MATCH (n:Person) WHERE n.age IS NULL RETURN n.name", "\"eve\"");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_WhereIsNotNull", async () =>
        {
            var s = await CypherResult("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN count(n) AS cnt");
            if (!s.Contains("4")) throw new Exception($"expected 4 persons with age, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_ReturnDistinct", async () =>
        {
            var s = await CypherResult("MATCH (n:Person)-[:KNOWS]->(m) RETURN DISTINCT m.name ORDER BY m.name");
            foreach (var name in new[] { "\"bob\"", "\"charlie\"", "\"dave\"" })
            {
                if (!s.Contains(name)) throw new Exception($"expected {name} in DISTINCT results, got {s}");
            }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_OrderByDesc", async () =>
        {
            var s = await CypherResult("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN n.name, n.age ORDER BY n.age DESC");
            if (s.IndexOf("\"dave\"") > s.IndexOf("\"alice\"")) throw new Exception($"expected dave before alice in DESC order, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_Skip", async () => await CypherContains("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN n.name ORDER BY n.age SKIP 2", "\"charlie\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_Limit", async () => await CypherContains("MATCH (n:Person) RETURN n.name LIMIT 1", "\"alice\"")));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_SkipAndLimit", async () =>
        {
            var s = await CypherResult("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN n.name ORDER BY n.age SKIP 1 LIMIT 1");
            if (!s.Contains("\"bob\"")) throw new Exception($"expected bob at skip 1 limit 1, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_AggregationSum", async () => await CypherContains("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN sum(n.age) AS total", "130")));
        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_AggregationAvg", async () => await CypherContains("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN avg(n.age) AS avg", "32.5")));
        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_AggregationMin", async () => await CypherContains("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN min(n.age) AS mn", "25")));
        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_AggregationMax", async () => await CypherContains("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN max(n.age) AS mx", "40")));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_AggregationCollect", async () =>
        {
            var s = await CypherResult("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN collect(n.name) AS names");
            foreach (var name in new[] { "\"alice\"", "\"bob\"", "\"charlie\"", "\"dave\"" })
            {
                if (!s.Contains(name)) throw new Exception($"expected {name} in collect, got {s}");
            }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_CountDistinct", async () => await CypherContains("MATCH (n:Person)-[:KNOWS]->(m) RETURN count(DISTINCT m.name) AS cnt", "3")));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_SetProperty", async () =>
        {
            await Cypher("MATCH (n:Person {name:'alice'}) SET n.age = 26");
            await CypherContains("MATCH (n:Person {name:'alice'}) RETURN n.age", "26");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_SetMergeProperties", async () =>
        {
            await Cypher("MATCH (n:Person {name:'alice'}) SET n += {city:'NYC',active:true}");
            await CypherContains("MATCH (n:Person {name:'alice'}) RETURN n.city", "\"NYC\"");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_SetLabel", async () =>
        {
            await Cypher("MATCH (n:Person {name:'alice'}) SET n:Employee");
            await CypherContains("MATCH (n:Employee) RETURN n.name", "\"alice\"");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_RemoveLabel", async () =>
        {
            await Cypher("MATCH (n:Person {name:'alice'}) REMOVE n:Employee");
            var s = await CypherResult("MATCH (n:Employee) RETURN count(n) AS cnt");
            if (!s.Contains("0")) throw new Exception($"expected 0 employees after REMOVE, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_RemoveProperty", async () =>
        {
            await Cypher("MATCH (n:Person {name:'alice'}) REMOVE n.city");
            var s = await CypherResult("MATCH (n:Person {name:'alice'}) RETURN n.city");
            if (s.Contains("\"NYC\"")) throw new Exception($"expected city removed, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_DeleteNonDetach", async () =>
        {
            await Cypher("CREATE (n:Temp {name:'temp_node'})");
            await Cypher("MATCH (n:Temp) DELETE n");
            await CypherContains("MATCH (n:Temp) RETURN count(n) AS cnt", "0");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_OptionalMatch", async () =>
        {
            var s = await CypherResult("MATCH (a:Person {name:'alice'}) OPTIONAL MATCH (a)-[:WORKS_WITH]->(c) RETURN a.name, c.name ORDER BY c.name");
            if (!s.Contains("\"alice\"")) throw new Exception($"expected alice, got {s}");
            if (!s.Contains("\"bob\"")) throw new Exception($"expected bob (WORKS_WITH target), got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_OptionalMatchNull", async () =>
        {
            var s = await CypherResult("MATCH (a:Person {name:'dave'}) OPTIONAL MATCH (a)-[:WORKS_WITH]->(c) RETURN a.name, c.name");
            if (!s.Contains("\"dave\"")) throw new Exception($"expected dave, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_WithClause", async () => await CypherContains("MATCH (n:Person) WHERE n.age IS NOT NULL WITH n.name AS name ORDER BY name LIMIT 2 RETURN name", "\"alice\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_WithAggregation", async () => await CypherContains("MATCH (n:Person)-[:KNOWS]->(m) WITH m.name AS friend, count(*) AS cnt RETURN friend, cnt ORDER BY cnt DESC", "\"bob\"")));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_Unwind", async () =>
        {
            await Cypher("UNWIND [10,20,30] AS x CREATE (n:Number {val:x})");
            await CypherContains("MATCH (n:Number) RETURN sum(n.val) AS total", "60");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_MultiHop", async () => await CypherContains("MATCH (a:Person {name:'alice'})-[:KNOWS]->(b)-[:KNOWS]->(c) RETURN c.name", "\"dave\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_VarLengthPath", async () => await CypherContains("MATCH (a:Person {name:'alice'})-[:KNOWS*1..2]->(b) RETURN DISTINCT b.name ORDER BY b.name", "\"dave\"")));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_CommaPatterns", async () =>
        {
            var s = await CypherResult("MATCH (a:Person {name:'alice'}), (b:Person {name:'bob'}) RETURN a.name, b.name");
            if (!s.Contains("\"alice\"") || !s.Contains("\"bob\"")) throw new Exception($"expected both alice and bob, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_MultiMatch", async () => await CypherContains("MATCH (a:Person {name:'alice'})-[:KNOWS]->(b) MATCH (b)-[:KNOWS]->(c) RETURN c.name", "\"dave\"")));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_MergeCreate", async () =>
        {
            await Cypher("MERGE (n:Animal {name:'rex'})");
            await CypherContains("MATCH (n:Animal) RETURN n.name", "\"rex\"");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_MergeIdempotent", async () =>
        {
            await Cypher("MATCH (n:Animal {name:'rex'}) DELETE n");
            await Cypher("MERGE (n:Animal {name:'rex'}) ON CREATE SET n.species = 'dog'");
            await CypherContains("MATCH (n:Animal {name:'rex'}) RETURN n.species", "\"dog\"");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_MergeOnMatchSet", async () =>
        {
            await Cypher("MERGE (n:Animal {name:'rex'}) ON MATCH SET n.visits = 1");
            await CypherContains("MATCH (n:Animal {name:'rex'}) RETURN n.visits", "1");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_TypeFunction", async () => await CypherContains("MATCH (a:Person {name:'alice'})-[r:KNOWS]->(b) RETURN type(r)", "\"KNOWS\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_IdFunction", async () => await CypherResult("MATCH (n:Person {name:'alice'}) RETURN id(n) AS nid")));
        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_CoalesceFunction", async () => await CypherContains("MATCH (n:Person {name:'eve'}) RETURN coalesce(n.age, 0) AS age", "0")));
        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_CaseExpression", async () => await CypherContains("MATCH (n:Person {name:'alice'}) RETURN CASE WHEN n.age > 30 THEN 'senior' ELSE 'junior' END AS category", "\"junior\"")));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_Profile", async () =>
        {
            await client.ExecuteOpenCypherExplainQueryAsync(new ExecuteOpenCypherExplainQueryRequest
            {
                OpenCypherQuery = "PROFILE MATCH (n) RETURN n LIMIT 1",
                ExplainMode = OpenCypherExplainMode.Details
            });
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_MatchEdgeReturn", async () => await CypherContains("MATCH (a:Person {name:'alice'})-[r:KNOWS]->(b) RETURN r.weight", "0.5")));
        results.Add(await runner.RunTestAsync("neptunedata", "Cypher_IncomingDirection", async () => await CypherContains("MATCH (b:Person {name:'bob'})<-[:KNOWS]-(a) RETURN a.name", "\"alice\"")));

        // === Gremlin Advanced Tests ===

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Advanced_Reset", async () =>
        {
            var initResp = await client.ExecuteFastResetAsync(new ExecuteFastResetRequest { Action = "initiateDatabaseReset" });
            if (initResp.Payload == null || string.IsNullOrEmpty(initResp.Payload.Token)) throw new Exception("expected token");
            await client.ExecuteFastResetAsync(new ExecuteFastResetRequest { Action = "performDatabaseReset", Token = initResp.Payload.Token });
        }));

        async Task Gremlin(string q) => await client.ExecuteGremlinQueryAsync(new ExecuteGremlinQueryRequest { GremlinQuery = q });
        async Task<string> GremlinResult(string q)
        {
            var resp = await client.ExecuteGremlinQueryAsync(new ExecuteGremlinQueryRequest { GremlinQuery = q });
            return MarshalDoc(resp.Result);
        }
        async Task GremlinContains(string q, string substr)
        {
            var s = await GremlinResult(q);
            if (!s.Contains(substr)) throw new Exception($"expected {substr} in result, got {s}");
        }

        await Gremlin("g.addV('person').property('name','alice').property('age',25)");
        await Gremlin("g.addV('person').property('name','bob').property('age',30)");
        await Gremlin("g.addV('person').property('name','charlie').property('age',35)");
        await Gremlin("g.addV('person').property('name','dave').property('age',40)");
        await Gremlin("g.V().has('name','alice').addE('knows').to(g.V().has('name','bob')).property('weight',0.5)");
        await Gremlin("g.V().has('name','alice').addE('knows').to(g.V().has('name','charlie')).property('weight',1.0)");
        await Gremlin("g.V().has('name','bob').addE('knows').to(g.V().has('name','dave')).property('weight',0.7)");
        await Gremlin("g.V().has('name','charlie').addE('knows').to(g.V().has('name','dave')).property('weight',0.3)");

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_In", async () => await GremlinContains("g.V().has('name','bob').in('knows').values('name')", "\"alice\"")));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Both", async () =>
        {
            var s = await GremlinResult("g.V().has('name','bob').both('knows').values('name').order()");
            if (!s.Contains("\"alice\"") || !s.Contains("\"dave\"")) throw new Exception($"expected alice and dave, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_OutE", async () =>
        {
            var s = await GremlinResult("g.V().has('name','alice').outE('knows').count()");
            if (!s.Contains("2")) throw new Exception($"expected 2 outgoing edges from alice, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_InE", async () =>
        {
            var s = await GremlinResult("g.V().has('name','bob').inE('knows').count()");
            if (!s.Contains("1")) throw new Exception($"expected 1 incoming edge to bob, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_BothE", async () =>
        {
            var s = await GremlinResult("g.V().has('name','bob').bothE().count()");
            if (!s.Contains("2")) throw new Exception($"expected 2 edges for bob (1 in + 1 out), got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_OutV", async () => await GremlinContains("g.E().hasLabel('knows').outV().has('name','alice').count()", "2")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_InV", async () => await GremlinContains("g.E().hasLabel('knows').inV().has('name','bob').count()", "1")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_OtherV", async () => await GremlinContains("g.V().has('name','alice').outE('knows').otherV().has('name','bob').count()", "1")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_HasPredicate", async () => await GremlinContains("g.V().has('age',P.gt(30)).values('name')", "\"charlie\"")));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_HasGte", async () =>
        {
            var s = await GremlinResult("g.V().has('age',P.gte(30)).count()");
            if (!s.Contains("3")) throw new Exception($"expected 3 vertices with age>=30, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_HasLt", async () => await GremlinContains("g.V().has('age',P.lt(30)).values('name')", "\"alice\"")));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_HasNeq", async () =>
        {
            var s = await GremlinResult("g.V().has('name',P.neq('alice')).count()");
            if (!s.Contains("3")) throw new Exception($"expected 3 non-alice vertices, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_HasBetween", async () =>
        {
            var s = await GremlinResult("g.V().has('age',P.between(25,35)).count()");
            if (!s.Contains("3")) throw new Exception($"expected 3 vertices with age between 25-35, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_HasWithin", async () => await GremlinContains("g.V().has('name',P.within('alice','dave')).values('name').order()", "\"alice\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_HasNot", async () => { await Cypher("CREATE (n:Person {name:'frank'})"); await GremlinContains("g.V().hasNot('age').values('name')", "\"frank\""); }));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_StartingWith", async () => await GremlinContains("g.V().has('name',P.startingWith('al')).values('name')", "\"alice\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Containing", async () => await GremlinContains("g.V().has('name',P.containing('li')).values('name')", "\"alice\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_EndingWith", async () => await GremlinContains("g.V().has('name',P.endingWith('ie')).values('name')", "\"charlie\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Id", async () => await GremlinResult("g.V().has('name','alice').id()")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Label", async () => await GremlinContains("g.V().has('name','alice').label()", "\"person\"")));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Keys", async () =>
        {
            var s = await GremlinResult("g.V().has('name','alice').keys()");
            if (!s.Contains("\"name\"") || !s.Contains("\"age\"")) throw new Exception($"expected name and age keys, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Properties", async () => await GremlinContains("g.V().has('name','alice').properties('name')", "\"name\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_PropertyMap", async () => await GremlinContains("g.V().has('name','alice').propertyMap('name','age')", "\"alice\"")));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_PropertyMutation", async () =>
        {
            await Gremlin("g.V().has('name','alice').property('city','NYC')");
            await GremlinContains("g.V().has('name','alice').values('city')", "\"NYC\"");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_AsSelect", async () => await GremlinContains("g.V().has('name','alice').as('a').out('knows').as('b').select('a','b').by('name')", "\"alice\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_WhereTraversal", async () => await GremlinContains("g.V().where(out().count().is(gt(0))).values('name').dedup()", "\"alice\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Filter", async () => await GremlinContains("g.V().filter(out('knows').count().is(gt(1))).values('name')", "\"alice\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Limit", async () => await GremlinContains("g.V().hasLabel('person').limit(2).count()", "2")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Range", async () => await GremlinContains("g.V().hasLabel('person').range(1,3).count()", "2")));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Skip", async () =>
        {
            var s = await GremlinResult("g.V().hasLabel('person').order().by('name').skip(2).values('name')");
            if (!s.Contains("\"charlie\"")) throw new Exception($"expected charlie at skip 2, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Dedup", async () =>
        {
            await Gremlin("g.addV('dup').property('name','dup1')");
            await Gremlin("g.addV('dup').property('name','dup1')");
            var s = await GremlinResult("g.V().hasLabel('dup').dedup().by('name').count()");
            if (!s.Contains("1")) throw new Exception($"expected 1 after dedup by name, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_OrderByAsc", async () =>
        {
            var s = await GremlinResult("g.V().hasLabel('person').order().by('name',asc).values('name').limit(1)");
            if (!s.Contains("\"alice\"")) throw new Exception($"expected alice first in ASC order, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_OrderByDesc", async () =>
        {
            var s = await GremlinResult("g.V().hasLabel('person').order().by('name',desc).values('name').limit(1)");
            if (!s.Contains("\"dave\"")) throw new Exception($"expected dave first in DESC order, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_GroupCount", async () =>
        {
            var s = await GremlinResult("g.V().hasLabel('person').groupCount().by('name')");
            foreach (var name in new[] { "\"alice\"", "\"bob\"", "\"charlie\"", "\"dave\"" })
            {
                if (!s.Contains(name)) throw new Exception($"expected {name} in groupCount, got {s}");
            }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_GroupBy", async () =>
        {
            var s = await GremlinResult("g.V().hasLabel('person').group().by('name').by('age')");
            if (!s.Contains("\"alice\"") || !s.Contains("\"bob\"")) throw new Exception($"expected grouped names, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Path", async () =>
        {
            var s = await GremlinResult("g.V().has('name','alice').out('knows').limit(1).path()");
            if (!s.Contains("\"alice\"")) throw new Exception($"expected alice in path, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Union", async () =>
        {
            var s = await GremlinResult("g.V().has('name','bob').union(in('knows'),out('knows')).values('name').dedup().order()");
            if (!s.Contains("\"alice\"") || !s.Contains("\"dave\"")) throw new Exception($"expected alice and dave from union, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Coalesce", async () => await GremlinContains("g.V().has('name','bob').coalesce(out('nonexistent'),in('knows')).values('name')", "\"alice\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Choose", async () => await GremlinContains("g.V().has('name','alice').choose(has('age'),values('age'),values('name'))", "25")));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Optional", async () =>
        {
            var s = await GremlinResult("g.V().has('name','dave').optional(out('knows')).values('name')");
            if (s.Contains("\"charlie\"")) throw new Exception($"optional should return original when no outgoing knows, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_RepeatTimes", async () => await GremlinContains("g.V().has('name','alice').repeat(out('knows')).times(2).values('name')", "\"dave\"")));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_RepeatEmit", async () =>
        {
            var s = await GremlinResult("g.V().has('name','alice').repeat(out('knows')).emit().values('name').dedup().order()");
            foreach (var name in new[] { "\"bob\"", "\"charlie\"", "\"dave\"" })
            {
                if (!s.Contains(name)) throw new Exception($"expected {name} in repeat-emit, got {s}");
            }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Fold", async () =>
        {
            var s = await GremlinResult("g.V().hasLabel('person').values('name').fold()");
            foreach (var name in new[] { "\"alice\"", "\"bob\"", "\"charlie\"", "\"dave\"" })
            {
                if (!s.Contains(name)) throw new Exception($"expected {name} in fold, got {s}");
            }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Unfold", async () => await GremlinContains("g.V().hasLabel('person').values('name').fold().unfold().count()", "4")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Constant", async () => await GremlinContains("g.V().has('name','alice').constant('hello').limit(1)", "\"hello\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Is", async () => await GremlinContains("g.V().hasLabel('person').values('age').is(P.gt(30))", "35")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Not", async () => await GremlinContains("g.V().hasLabel('person').not(out('knows')).values('name')", "\"dave\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_And", async () => await GremlinContains("g.V().and(out('knows'),in('knows')).values('name')", "\"bob\"")));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Or", async () =>
        {
            var s = await GremlinResult("g.V().or(has('name','alice'),has('name','dave')).values('name').order()");
            if (!s.Contains("\"alice\"") || !s.Contains("\"dave\"")) throw new Exception($"expected alice or dave, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Mean", async () => await GremlinContains("g.V().hasLabel('person').values('age').mean()", "32.5")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Sum", async () => await GremlinContains("g.V().hasLabel('person').values('age').sum()", "130")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Min", async () => await GremlinContains("g.V().hasLabel('person').values('age').min()", "25")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Max", async () => await GremlinContains("g.V().hasLabel('person').values('age').max()", "40")));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Tail", async () =>
        {
            var s = await GremlinResult("g.V().hasLabel('person').order().by('name').tail(1).values('name')");
            if (!s.Contains("\"dave\"")) throw new Exception($"expected dave as tail, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Inject", async () => await GremlinContains("g.inject('a','b').count()", "2")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_MergeV", async () => { await Gremlin("g.mergeV([~label:'item',name:'widget',price:10])"); await GremlinContains("g.V().has('name','widget').values('price')", "10"); }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_MergeVIdempotent", async () =>
        {
            await Gremlin("g.mergeV([~label:'item',name:'widget',price:10])");
            var s = await GremlinResult("g.V().has('name','widget').count()");
            if (!s.Contains("1")) throw new Exception($"expected 1 widget after idempotent mergeV, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_ElementMap", async () =>
        {
            var s = await GremlinResult("g.V().has('name','alice').elementMap()");
            if (!s.Contains("\"alice\"") || !s.Contains("\"person\"")) throw new Exception($"expected alice and person in elementMap, got {s}");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_Project", async () => await GremlinContains("g.V().as('a').out('knows').as('b').project('from','to').by('name').limit(1)", "\"from\"")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_CountLocal", async () => await GremlinContains("g.V().hasLabel('person').values('name').fold().count(local)", "4")));
        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_MultiHop", async () => await GremlinContains("g.V().has('name','alice').out().out().values('name')", "\"dave\"")));

        results.Add(await runner.RunTestAsync("neptunedata", "Gremlin_SimplePath", async () =>
        {
            var s = await GremlinResult("g.V().has('name','alice').repeat(__.out('knows')).times(2).simplePath().values('name').dedup()");
            if (!s.Contains("\"dave\"")) throw new Exception($"expected dave in simplePath result, got {s}");
        }));

        // === Server API Tests ===

        results.Add(await runner.RunTestAsync("neptunedata", "CancelGremlinQuery", async () =>
        {
            try { await client.CancelGremlinQueryAsync(new CancelGremlinQueryRequest { QueryId = "nonexistent-query-id" }); throw new Exception("expected error for cancelling non-existent query"); }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "CancelOpenCypherQuery", async () =>
        {
            await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = "MATCH (n) RETURN count(n)" });
            await client.ListOpenCypherQueriesAsync(new ListOpenCypherQueriesRequest());
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ManagePropertygraphStatistics_Disable", async () =>
        {
            await client.ManagePropertygraphStatisticsAsync(new ManagePropertygraphStatisticsRequest { Mode = "disableAutocompute" });
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ManagePropertygraphStatistics_Enable", async () =>
        {
            await client.ManagePropertygraphStatisticsAsync(new ManagePropertygraphStatisticsRequest { Mode = "enableAutocompute" });
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ManagePropertygraphStatistics_Refresh", async () =>
        {
            var resp = await client.ManagePropertygraphStatisticsAsync(new ManagePropertygraphStatisticsRequest { Mode = "refresh" });
            if (resp.Status == null) throw new Exception("expected non-nil status from refresh");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "DeletePropertygraphStatistics", async () =>
        {
            var resp = await client.DeletePropertygraphStatisticsAsync(new DeletePropertygraphStatisticsRequest());
            if (resp.Status == null) throw new Exception("expected non-nil status from delete statistics");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "GetPropertygraphStream", async () =>
        {
            var resp = await client.GetPropertygraphStreamAsync(new GetPropertygraphStreamRequest());
            if (resp.Format == null) throw new Exception("expected non-nil format from stream");
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "GetPropertygraphSummary_Detailed", async () =>
        {
            await client.GetPropertygraphSummaryAsync(new GetPropertygraphSummaryRequest { Mode = GraphSummaryType.Detailed });
        }));

        // === Error Cases ===

        results.Add(await runner.RunTestAsync("neptunedata", "ErrorCase_InvalidCypherSyntax", async () =>
        {
            try { await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = "INVALID CYPHER QUERY" }); throw new Exception("expected error for invalid cypher syntax"); }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ErrorCase_InvalidGremlinSyntax", async () =>
        {
            try { await client.ExecuteGremlinQueryAsync(new ExecuteGremlinQueryRequest { GremlinQuery = "g.INVALID_STEP()" }); throw new Exception("expected error for invalid gremlin syntax"); }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ErrorCase_FastResetInvalidToken", async () =>
        {
            try { await client.ExecuteFastResetAsync(new ExecuteFastResetRequest { Action = "performDatabaseReset", Token = "invalid-token-12345" }); throw new Exception("expected error for invalid fast reset token"); }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ErrorCase_NonExistentLoaderJob", async () =>
        {
            try { await client.GetLoaderJobStatusAsync(new GetLoaderJobStatusRequest { LoadId = "nonexistent-load-id" }); throw new Exception("expected error for non-existent loader job"); }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ErrorCase_CancelNonExistentLoaderJob", async () =>
        {
            try { await client.CancelLoaderJobAsync(new CancelLoaderJobRequest { LoadId = "nonexistent-load-id" }); throw new Exception("expected error for cancel non-existent loader job"); }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ErrorCase_EmptyCypherQuery", async () =>
        {
            try { await client.ExecuteOpenCypherQueryAsync(new ExecuteOpenCypherQueryRequest { OpenCypherQuery = "" }); throw new Exception("expected error for empty cypher query"); }
            catch { }
        }));

        results.Add(await runner.RunTestAsync("neptunedata", "ErrorCase_EmptyGremlinQuery", async () =>
        {
            try { await client.ExecuteGremlinQueryAsync(new ExecuteGremlinQueryRequest { GremlinQuery = "" }); throw new Exception("expected error for empty gremlin query"); }
            catch { }
        }));

        return results;
    }
}
