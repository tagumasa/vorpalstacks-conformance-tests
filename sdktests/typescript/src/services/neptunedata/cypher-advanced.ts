import {
  NeptunedataClient as NeptuneDataClient,
  ExecuteOpenCypherQueryCommand,
  ExecuteOpenCypherExplainQueryCommand,
} from '@aws-sdk/client-neptunedata';
import { TestRunner, TestResult } from '../../runner.js';
import { createQueryHelpers } from './context.js';

export async function runCypherTests(
  client: NeptuneDataClient,
  runner: TestRunner,
  results: TestResult[],
): Promise<void> {
  const s = 'neptunedata';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(s, name, fn));
  const { cypher, cypherResult, cypherContains, fastReset } = createQueryHelpers(client);

  await r('ExecuteOpenCypherQuery_CreateNode', async () => {
    await cypher("CREATE (n:Person {name: 'marko', age: 29})");
  });

  await r('ExecuteOpenCypherQuery_CreateMoreNodes', async () => {
    await cypher("CREATE (n:Person {name: 'vadas', age: 27})");
    await cypher("CREATE (n:Person {name: 'josh', age: 32})");
    await cypher("CREATE (n:Software {name: 'lop', lang: 'java'})");
  });

  await r('ExecuteOpenCypherQuery_CreateRelationships', async () => {
    await cypher("MATCH (a:Person {name: 'marko'}), (b:Person {name: 'vadas'}) CREATE (a)-[:KNOWS {weight: 0.5}]->(b)");
    await cypher("MATCH (a:Person {name: 'marko'}), (b:Person {name: 'josh'}) CREATE (a)-[:KNOWS {weight: 1.0}]->(b)");
    await cypher("MATCH (a:Person {name: 'marko'}), (b:Software {name: 'lop'}) CREATE (a)-[:CREATED {weight: 0.4}]->(b)");
  });

  await r('ExecuteOpenCypherQuery_MatchAllNodes', async () => {
    const txt = await cypherResult("MATCH (n) RETURN n.name ORDER BY n.name");
    for (const name of ['"marko"', '"vadas"', '"josh"', '"lop"']) {
      if (!txt.includes(name)) throw new Error(`expected node name ${name} in results, got ${txt}`);
    }
  });

  await r('ExecuteOpenCypherQuery_MatchByProperty', async () => {
    await cypherContains("MATCH (n:Person {name: 'marko'}) RETURN n.age", '29');
  });

  await r('ExecuteOpenCypherQuery_Traversal', async () => {
    const txt = await cypherResult("MATCH (a:Person {name: 'marko'})-[:KNOWS]->(friend) RETURN friend.name");
    for (const name of ['"vadas"', '"josh"']) {
      if (!txt.includes(name)) throw new Error(`expected friend ${name} in traversal results, got ${txt}`);
    }
  });

  await r('ExecuteOpenCypherQuery_Aggregation', async () => {
    await cypherContains("MATCH (n:Person) RETURN count(n) AS cnt", '3');
  });

  await r('ExecuteOpenCypherQuery_Parameters', async () => {
    const resp = await client.send(
      new ExecuteOpenCypherQueryCommand({
        openCypherQuery: 'MATCH (n:Person {name: $name}) RETURN n.age',
        parameters: '{"name": "marko"}',
      }),
    );
    const txt = JSON.stringify(resp.results);
    if (!txt.includes('29')) throw new Error(`expected age 29 from parameterised query, got ${txt}`);
  });

  await r('ExecuteOpenCypherQuery_Delete', async () => {
    await cypher("MATCH (n:Software {name: 'lop'}) DETACH DELETE n");
  });

  await r('ExecuteOpenCypherQuery_VerifyDelete', async () => {
    await cypherContains("MATCH (n:Software) RETURN count(n) AS cnt", '0');
  });

  await r('ExecuteOpenCypherExplainQuery', async () => {
    const resp = await client.send(
      new ExecuteOpenCypherExplainQueryCommand({
        openCypherQuery: 'MATCH (n) RETURN n LIMIT 1',
        explainMode: 'static',
      }),
    );
    if (!resp.results) throw new Error('expected non-nil explain results');
  });

  await r('Cypher_Advanced_Reset', async () => {
    await fastReset();
  });

  await cypher("CREATE (a:Person {name:'alice',age:25}), (b:Person {name:'bob',age:30}), (c:Person {name:'charlie',age:35}), (d:Person {name:'dave',age:40})");
  await cypher("MATCH (a:Person {name:'alice'}), (b:Person {name:'bob'}) CREATE (a)-[:KNOWS {weight:0.5}]->(b)");
  await cypher("MATCH (a:Person {name:'alice'}), (c:Person {name:'charlie'}) CREATE (a)-[:KNOWS {weight:1.0}]->(c)");
  await cypher("MATCH (b:Person {name:'bob'}), (d:Person {name:'dave'}) CREATE (b)-[:KNOWS {weight:0.7}]->(d)");
  await cypher("MATCH (c:Person {name:'charlie'}), (d:Person {name:'dave'}) CREATE (c)-[:KNOWS {weight:0.3}]->(d)");
  await cypher("MATCH (a:Person {name:'alice'}), (b:Person {name:'bob'}) CREATE (a)-[:WORKS_WITH]->(b)");
  await cypher("MATCH (b:Person {name:'bob'}), (c:Person {name:'charlie'}) CREATE (b)-[:WORKS_WITH]->(c)");

  await r('Cypher_WhereClause', async () => {
    await cypherContains("MATCH (n:Person) WHERE n.age > 30 RETURN n.name", '"charlie"');
  });

  await r('Cypher_WhereAND', async () => {
    await cypherContains("MATCH (n:Person) WHERE n.age > 25 AND n.age < 40 RETURN n.name", '"bob"');
  });

  await r('Cypher_WhereOR', async () => {
    const txt = await cypherResult("MATCH (n:Person) WHERE n.name = 'alice' OR n.name = 'dave' RETURN n.name ORDER BY n.name");
    if (!txt.includes('"alice"') || !txt.includes('"dave"')) {
      throw new Error(`expected alice and dave, got ${txt}`);
    }
  });

  await r('Cypher_WhereNOT', async () => {
    const txt = await cypherResult("MATCH (n:Person) WHERE NOT n.name = 'alice' AND NOT n.name = 'dave' RETURN n.name ORDER BY n.name");
    if (!txt.includes('"bob"') || !txt.includes('"charlie"')) {
      throw new Error(`expected bob and charlie, got ${txt}`);
    }
  });

  await r('Cypher_WhereContains', async () => {
    await cypherContains("MATCH (n:Person) WHERE n.name CONTAINS 'li' RETURN n.name", '"alice"');
  });

  await r('Cypher_WhereStartsWith', async () => {
    await cypherContains("MATCH (n:Person) WHERE n.name STARTS WITH 'al' RETURN n.name", '"alice"');
  });

  await r('Cypher_WhereEndsWith', async () => {
    await cypherContains("MATCH (n:Person) WHERE n.name ENDS WITH 'ie' RETURN n.name", '"charlie"');
  });

  await r('Cypher_WhereIN', async () => {
    const txt = await cypherResult("MATCH (n:Person) WHERE n.name IN ['alice', 'dave'] RETURN n.name ORDER BY n.name");
    if (!txt.includes('"alice"') || !txt.includes('"dave"')) {
      throw new Error(`expected alice and dave, got ${txt}`);
    }
  });

  await r('Cypher_WhereIsNull', async () => {
    await cypher("CREATE (n:Person {name:'eve'})");
    await cypherContains("MATCH (n:Person) WHERE n.age IS NULL RETURN n.name", '"eve"');
  });

  await r('Cypher_WhereIsNotNull', async () => {
    const txt = await cypherResult("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN count(n) AS cnt");
    if (!txt.includes('4')) throw new Error(`expected 4 persons with age, got ${txt}`);
  });

  await r('Cypher_ReturnDistinct', async () => {
    const txt = await cypherResult("MATCH (n:Person)-[:KNOWS]->(m) RETURN DISTINCT m.name ORDER BY m.name");
    for (const name of ['"bob"', '"charlie"', '"dave"']) {
      if (!txt.includes(name)) throw new Error(`expected ${name} in DISTINCT results, got ${txt}`);
    }
  });

  await r('Cypher_OrderByDesc', async () => {
    const txt = await cypherResult("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN n.name, n.age ORDER BY n.age DESC");
    const idx = txt.indexOf('"dave"');
    const idx2 = txt.indexOf('"alice"');
    if (idx > idx2) throw new Error(`expected dave before alice in DESC order, got ${txt}`);
  });

  await r('Cypher_Skip', async () => {
    await cypherContains("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN n.name ORDER BY n.age SKIP 2", '"charlie"');
  });

  await r('Cypher_Limit', async () => {
    await cypherContains("MATCH (n:Person) RETURN n.name LIMIT 1", '"alice"');
  });

  await r('Cypher_SkipAndLimit', async () => {
    const txt = await cypherResult("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN n.name ORDER BY n.age SKIP 1 LIMIT 1");
    if (!txt.includes('"bob"')) throw new Error(`expected bob at skip 1 limit 1, got ${txt}`);
  });

  await r('Cypher_AggregationSum', async () => {
    await cypherContains("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN sum(n.age) AS total", '130');
  });

  await r('Cypher_AggregationAvg', async () => {
    await cypherContains("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN avg(n.age) AS avg", '32.5');
  });

  await r('Cypher_AggregationMin', async () => {
    await cypherContains("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN min(n.age) AS mn", '25');
  });

  await r('Cypher_AggregationMax', async () => {
    await cypherContains("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN max(n.age) AS mx", '40');
  });

  await r('Cypher_AggregationCollect', async () => {
    const txt = await cypherResult("MATCH (n:Person) WHERE n.age IS NOT NULL RETURN collect(n.name) AS names");
    for (const name of ['"alice"', '"bob"', '"charlie"', '"dave"']) {
      if (!txt.includes(name)) throw new Error(`expected ${name} in collect, got ${txt}`);
    }
  });

  await r('Cypher_CountDistinct', async () => {
    await cypherContains("MATCH (n:Person)-[:KNOWS]->(m) RETURN count(DISTINCT m.name) AS cnt", '3');
  });

  await r('Cypher_SetProperty', async () => {
    await cypher("MATCH (n:Person {name:'alice'}) SET n.age = 26");
    await cypherContains("MATCH (n:Person {name:'alice'}) RETURN n.age", '26');
  });

  await r('Cypher_SetMergeProperties', async () => {
    await cypher("MATCH (n:Person {name:'alice'}) SET n += {city:'NYC',active:true}");
    await cypherContains("MATCH (n:Person {name:'alice'}) RETURN n.city", '"NYC"');
  });

  await r('Cypher_SetLabel', async () => {
    await cypher("MATCH (n:Person {name:'alice'}) SET n:Employee");
    await cypherContains("MATCH (n:Employee) RETURN n.name", '"alice"');
  });

  await r('Cypher_RemoveLabel', async () => {
    await cypher("MATCH (n:Person {name:'alice'}) REMOVE n:Employee");
    const txt = await cypherResult("MATCH (n:Employee) RETURN count(n) AS cnt");
    if (!txt.includes('0')) throw new Error(`expected 0 employees after REMOVE, got ${txt}`);
  });

  await r('Cypher_RemoveProperty', async () => {
    await cypher("MATCH (n:Person {name:'alice'}) REMOVE n.city");
    const txt = await cypherResult("MATCH (n:Person {name:'alice'}) RETURN n.city");
    if (txt.includes('"NYC"')) throw new Error(`expected city removed, got ${txt}`);
  });

  await r('Cypher_DeleteNonDetach', async () => {
    await cypher("CREATE (n:Temp {name:'temp_node'})");
    await cypher("MATCH (n:Temp) DELETE n");
    await cypherContains("MATCH (n:Temp) RETURN count(n) AS cnt", '0');
  });

  await r('Cypher_OptionalMatch', async () => {
    const txt = await cypherResult("MATCH (a:Person {name:'alice'}) OPTIONAL MATCH (a)-[:WORKS_WITH]->(c) RETURN a.name, c.name ORDER BY c.name");
    if (!txt.includes('"alice"')) throw new Error(`expected alice, got ${txt}`);
    if (!txt.includes('"bob"')) throw new Error(`expected bob (WORKS_WITH target), got ${txt}`);
  });

  await r('Cypher_OptionalMatchNull', async () => {
    const txt = await cypherResult("MATCH (a:Person {name:'dave'}) OPTIONAL MATCH (a)-[:WORKS_WITH]->(c) RETURN a.name, c.name");
    if (!txt.includes('"dave"')) throw new Error(`expected dave, got ${txt}`);
  });

  await r('Cypher_WithClause', async () => {
    await cypherContains("MATCH (n:Person) WHERE n.age IS NOT NULL WITH n.name AS name ORDER BY name LIMIT 2 RETURN name", '"alice"');
  });

  await r('Cypher_WithAggregation', async () => {
    await cypherContains("MATCH (n:Person)-[:KNOWS]->(m) WITH m.name AS friend, count(*) AS cnt RETURN friend, cnt ORDER BY cnt DESC", '"bob"');
  });

  await r('Cypher_Unwind', async () => {
    await cypher("UNWIND [10,20,30] AS x CREATE (n:Number {val:x})");
    await cypherContains("MATCH (n:Number) RETURN sum(n.val) AS total", '60');
  });

  await r('Cypher_MultiHop', async () => {
    await cypherContains("MATCH (a:Person {name:'alice'})-[:KNOWS]->(b)-[:KNOWS]->(c) RETURN c.name", '"dave"');
  });

  await r('Cypher_VarLengthPath', async () => {
    await cypherContains("MATCH (a:Person {name:'alice'})-[:KNOWS*1..2]->(b) RETURN DISTINCT b.name ORDER BY b.name", '"dave"');
  });

  await r('Cypher_CommaPatterns', async () => {
    const txt = await cypherResult("MATCH (a:Person {name:'alice'}), (b:Person {name:'bob'}) RETURN a.name, b.name");
    if (!txt.includes('"alice"') || !txt.includes('"bob"')) {
      throw new Error(`expected both alice and bob, got ${txt}`);
    }
  });

  await r('Cypher_MultiMatch', async () => {
    await cypherContains("MATCH (a:Person {name:'alice'})-[:KNOWS]->(b) MATCH (b)-[:KNOWS]->(c) RETURN c.name", '"dave"');
  });

  await r('Cypher_MergeCreate', async () => {
    await cypher("MERGE (n:Animal {name:'rex'})");
    await cypherContains("MATCH (n:Animal) RETURN n.name", '"rex"');
  });

  await r('Cypher_MergeIdempotent', async () => {
    await cypher("MATCH (n:Animal {name:'rex'}) DELETE n");
    await cypher("MERGE (n:Animal {name:'rex'}) ON CREATE SET n.species = 'dog'");
    await cypherContains("MATCH (n:Animal {name:'rex'}) RETURN n.species", '"dog"');
  });

  await r('Cypher_MergeOnMatchSet', async () => {
    await cypher("MERGE (n:Animal {name:'rex'}) ON MATCH SET n.visits = 1");
    await cypherContains("MATCH (n:Animal {name:'rex'}) RETURN n.visits", '1');
  });

  await r('Cypher_TypeFunction', async () => {
    await cypherContains("MATCH (a:Person {name:'alice'})-[r:KNOWS]->(b) RETURN type(r)", '"KNOWS"');
  });

  await r('Cypher_IdFunction', async () => {
    await cypherResult("MATCH (n:Person {name:'alice'}) RETURN id(n) AS nid");
  });

  await r('Cypher_CoalesceFunction', async () => {
    await cypherContains("MATCH (n:Person {name:'eve'}) RETURN coalesce(n.age, 0) AS age", '0');
  });

  await r('Cypher_CaseExpression', async () => {
    await cypherContains("MATCH (n:Person {name:'alice'}) RETURN CASE WHEN n.age > 30 THEN 'senior' ELSE 'junior' END AS category", '"junior"');
  });

  await r('Cypher_Profile', async () => {
    await client.send(
      new ExecuteOpenCypherExplainQueryCommand({
        openCypherQuery: 'PROFILE MATCH (n) RETURN n LIMIT 1',
        explainMode: 'details',
      }),
    );
  });

  await r('Cypher_MatchEdgeReturn', async () => {
    await cypherContains("MATCH (a:Person {name:'alice'})-[r:KNOWS]->(b) RETURN r.weight", '0.5');
  });

  await r('Cypher_IncomingDirection', async () => {
    await cypherContains("MATCH (b:Person {name:'bob'})<-[:KNOWS]-(a) RETURN a.name", '"alice"');
  });

  await r('ErrorCase_InvalidCypherSyntax', async () => {
    try {
      await cypher('INVALID CYPHER QUERY');
      throw new Error('expected error for invalid cypher syntax');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for invalid cypher syntax') throw err;
    }
  });

  await r('ErrorCase_EmptyCypherQuery', async () => {
    try {
      await cypher('');
      throw new Error('expected error for empty cypher query');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for empty cypher query') throw err;
    }
  });
}
