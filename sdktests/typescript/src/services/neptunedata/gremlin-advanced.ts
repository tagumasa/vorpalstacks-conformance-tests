import {
  NeptunedataClient as NeptuneDataClient,
  ExecuteGremlinQueryCommand,
  ExecuteGremlinExplainQueryCommand,
  ExecuteGremlinProfileQueryCommand,
  ExecuteFastResetCommand,
  GetLoaderJobStatusCommand,
  CancelLoaderJobCommand,
} from '@aws-sdk/client-neptunedata';
import { TestRunner, TestResult } from '../../runner.js';
import { createQueryHelpers } from './context.js';

export async function runGremlinTests(
  client: NeptuneDataClient,
  runner: TestRunner,
  results: TestResult[],
): Promise<void> {
  const s = 'neptunedata';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(s, name, fn));
  const { cypher, cypherContains, gremlin, gremlinResult, gremlinContains, fastReset } = createQueryHelpers(client);

  await r('Gremlin_ResetGraph', async () => {
    await fastReset();
  });

  await r('ExecuteGremlinQuery_AddVertex', async () => {
    const resp = await client.send(
      new ExecuteGremlinQueryCommand({
        gremlinQuery: "g.addV('person').property('name','marko').property('age',29)",
      }),
    );
    if (!resp.requestId) throw new Error('expected non-empty requestId');
  });

  await r('ExecuteGremlinQuery_AddMoreVertices', async () => {
    const queries = [
      "g.addV('person').property('name','vadas').property('age',27)",
      "g.addV('person').property('name','josh').property('age',32)",
      "g.addV('software').property('name','lop').property('lang','java')",
    ];
    for (const q of queries) {
      await gremlin(q);
    }
  });

  await r('ExecuteGremlinQuery_AddEdges', async () => {
    const queries = [
      "g.V().has('name','marko').addE('knows').to(g.V().has('name','vadas')).property('weight',0.5)",
      "g.V().has('name','marko').addE('knows').to(g.V().has('name','josh')).property('weight',1.0)",
      "g.V().has('name','marko').addE('created').to(g.V().has('name','lop')).property('weight',0.4)",
    ];
    for (const q of queries) {
      await gremlin(q);
    }
  });

  await r('ExecuteGremlinQuery_Count', async () => {
    const txt = await gremlinResult('g.V().count()');
    if (!txt.includes('4')) throw new Error(`expected count 4 in gremlin result, got ${txt}`);
  });

  await r('ExecuteGremlinQuery_Traversal', async () => {
    const txt = await gremlinResult("g.V().has('name','marko').out('knows').values('name')");
    for (const name of ['vadas', 'josh']) {
      if (!txt.includes(name)) throw new Error(`expected '${name}' in gremlin traversal results, got ${txt}`);
    }
  });

  await r('ExecuteGremlinQuery_ValueMap', async () => {
    const txt = await gremlinResult("g.V().has('name','marko').valueMap()");
    for (const key of ['name', 'age']) {
      if (!txt.includes(key)) throw new Error(`expected key '${key}' in valueMap result, got ${txt}`);
    }
  });

  await r('ExecuteGremlinQuery_HasLabel', async () => {
    const txt = await gremlinResult("g.V().hasLabel('software').count()");
    if (!txt.includes('1')) throw new Error(`expected count 1 for software label, got ${txt}`);
  });

  await r('ExecuteGremlinQuery_EdgeCount', async () => {
    const txt = await gremlinResult('g.E().count()');
    if (!txt.includes('3')) throw new Error(`expected count 3 for edges, got ${txt}`);
  });

  await r('ExecuteGremlinQuery_DropVertex', async () => {
    await gremlin("g.V().has('name','lop').drop()");
  });

  await r('ExecuteGremlinQuery_VerifyDrop', async () => {
    const txt = await gremlinResult("g.V().has('name','lop').count()");
    if (!txt.includes('0')) throw new Error(`expected count 0 after drop, got ${txt}`);
  });

  await r('ExecuteGremlinExplainQuery', async () => {
    const resp = await client.send(
      new ExecuteGremlinExplainQueryCommand({ gremlinQuery: 'g.V().count()' }),
    );
    if (!resp.output) throw new Error('expected non-nil explain output');
  });

  await r('ExecuteGremlinProfileQuery', async () => {
    const resp = await client.send(
      new ExecuteGremlinProfileQueryCommand({ gremlinQuery: 'g.V().count()' }),
    );
    if (!resp.output) throw new Error('expected non-nil profile output');
  });

  await r('Gremlin_Advanced_Reset', async () => {
    await fastReset();
  });

  await gremlin("g.addV('person').property('name','alice').property('age',25)");
  await gremlin("g.addV('person').property('name','bob').property('age',30)");
  await gremlin("g.addV('person').property('name','charlie').property('age',35)");
  await gremlin("g.addV('person').property('name','dave').property('age',40)");
  await gremlin("g.V().has('name','alice').addE('knows').to(g.V().has('name','bob')).property('weight',0.5)");
  await gremlin("g.V().has('name','alice').addE('knows').to(g.V().has('name','charlie')).property('weight',1.0)");
  await gremlin("g.V().has('name','bob').addE('knows').to(g.V().has('name','dave')).property('weight',0.7)");
  await gremlin("g.V().has('name','charlie').addE('knows').to(g.V().has('name','dave')).property('weight',0.3)");

  await r('Gremlin_In', async () => {
    await gremlinContains("g.V().has('name','bob').in('knows').values('name')", '"alice"');
  });

  await r('Gremlin_Both', async () => {
    const txt = await gremlinResult("g.V().has('name','bob').both('knows').values('name').order()");
    if (!txt.includes('"alice"') || !txt.includes('"dave"')) {
      throw new Error(`expected alice and dave, got ${txt}`);
    }
  });

  await r('Gremlin_OutE', async () => {
    const txt = await gremlinResult("g.V().has('name','alice').outE('knows').count()");
    if (!txt.includes('2')) throw new Error(`expected 2 outgoing edges from alice, got ${txt}`);
  });

  await r('Gremlin_InE', async () => {
    const txt = await gremlinResult("g.V().has('name','bob').inE('knows').count()");
    if (!txt.includes('1')) throw new Error(`expected 1 incoming edge to bob, got ${txt}`);
  });

  await r('Gremlin_BothE', async () => {
    const txt = await gremlinResult("g.V().has('name','bob').bothE().count()");
    if (!txt.includes('2')) throw new Error(`expected 2 edges for bob (1 in + 1 out), got ${txt}`);
  });

  await r('Gremlin_OutV', async () => {
    await gremlinContains("g.E().hasLabel('knows').outV().has('name','alice').count()", '2');
  });

  await r('Gremlin_InV', async () => {
    await gremlinContains("g.E().hasLabel('knows').inV().has('name','bob').count()", '1');
  });

  await r('Gremlin_OtherV', async () => {
    await gremlinContains("g.V().has('name','alice').outE('knows').otherV().has('name','bob').count()", '1');
  });

  await r('Gremlin_HasPredicate', async () => {
    await gremlinContains("g.V().has('age',gt(30)).values('name')", '"charlie"');
  });

  await r('Gremlin_HasGte', async () => {
    const txt = await gremlinResult("g.V().has('age',gte(30)).count()");
    if (!txt.includes('3')) throw new Error(`expected 3 vertices with age>=30, got ${txt}`);
  });

  await r('Gremlin_HasLt', async () => {
    await gremlinContains("g.V().has('age',lt(30)).values('name')", '"alice"');
  });

  await r('Gremlin_HasNeq', async () => {
    const txt = await gremlinResult("g.V().has('name',neq('alice')).count()");
    if (!txt.includes('3')) throw new Error(`expected 3 non-alice vertices, got ${txt}`);
  });

  await r('Gremlin_HasBetween', async () => {
    const txt = await gremlinResult("g.V().has('age',between(25,35)).count()");
    if (!txt.includes('3')) throw new Error(`expected 3 vertices with age between 25-35, got ${txt}`);
  });

  await r('Gremlin_HasWithin', async () => {
    await gremlinContains("g.V().has('name',within('alice','dave')).values('name').order()", '"alice"');
  });

  await r('Gremlin_HasNot', async () => {
    await cypher("CREATE (n:Person {name:'frank'})");
    await gremlinContains("g.V().hasNot('age').values('name')", '"frank"');
  });

  await r('Gremlin_StartingWith', async () => {
    await gremlinContains("g.V().has('name',startingWith('al')).values('name')", '"alice"');
  });

  await r('Gremlin_Containing', async () => {
    await gremlinContains("g.V().has('name',containing('li')).values('name')", '"alice"');
  });

  await r('Gremlin_EndingWith', async () => {
    await gremlinContains("g.V().has('name',endingWith('ie')).values('name')", '"charlie"');
  });

  await r('Gremlin_Id', async () => {
    await gremlinResult("g.V().has('name','alice').id()");
  });

  await r('Gremlin_Label', async () => {
    await gremlinContains("g.V().has('name','alice').label()", '"person"');
  });

  await r('Gremlin_Keys', async () => {
    const txt = await gremlinResult("g.V().has('name','alice').keys()");
    if (!txt.includes('"name"') || !txt.includes('"age"')) {
      throw new Error(`expected name and age keys, got ${txt}`);
    }
  });

  await r('Gremlin_Properties', async () => {
    await gremlinContains("g.V().has('name','alice').properties('name')", '"name"');
  });

  await r('Gremlin_PropertyMap', async () => {
    await gremlinContains("g.V().has('name','alice').propertyMap('name','age')", '"alice"');
  });

  await r('Gremlin_PropertyMutation', async () => {
    await gremlin("g.V().has('name','alice').property('city','NYC')");
    await gremlinContains("g.V().has('name','alice').values('city')", '"NYC"');
  });

  await r('Gremlin_AsSelect', async () => {
    await gremlinContains("g.V().has('name','alice').as('a').out('knows').as('b').select('a','b').by('name')", '"alice"');
  });

  await r('Gremlin_WhereTraversal', async () => {
    await gremlinContains("g.V().where(out().count().is(gt(0))).values('name').dedup()", '"alice"');
  });

  await r('Gremlin_Filter', async () => {
    await gremlinContains("g.V().filter(out('knows').count().is(gt(1))).values('name')", '"alice"');
  });

  await r('Gremlin_Limit', async () => {
    await gremlinContains("g.V().hasLabel('person').limit(2).count()", '2');
  });

  await r('Gremlin_Range', async () => {
    await gremlinContains("g.V().hasLabel('person').range(1,3).count()", '2');
  });

  await r('Gremlin_Skip', async () => {
    const txt = await gremlinResult("g.V().hasLabel('person').order().by('name').skip(2).values('name')");
    if (!txt.includes('"charlie"')) throw new Error(`expected charlie at skip 2, got ${txt}`);
  });

  await r('Gremlin_Dedup', async () => {
    await gremlin("g.addV('dup').property('name','dup1')");
    await gremlin("g.addV('dup').property('name','dup1')");
    const txt = await gremlinResult("g.V().hasLabel('dup').dedup().by('name').count()");
    if (!txt.includes('1')) throw new Error(`expected 1 after dedup by name, got ${txt}`);
  });

  await r('Gremlin_OrderByAsc', async () => {
    const txt = await gremlinResult("g.V().hasLabel('person').order().by('name',asc).values('name').limit(1)");
    if (!txt.includes('"alice"')) throw new Error(`expected alice first in ASC order, got ${txt}`);
  });

  await r('Gremlin_OrderByDesc', async () => {
    const txt = await gremlinResult("g.V().hasLabel('person').order().by('name',desc).values('name').limit(1)");
    if (!txt.includes('"dave"')) throw new Error(`expected dave first in DESC order, got ${txt}`);
  });

  await r('Gremlin_GroupCount', async () => {
    const txt = await gremlinResult("g.V().hasLabel('person').groupCount().by('name')");
    for (const name of ['"alice"', '"bob"', '"charlie"', '"dave"']) {
      if (!txt.includes(name)) throw new Error(`expected ${name} in groupCount, got ${txt}`);
    }
  });

  await r('Gremlin_GroupBy', async () => {
    const txt = await gremlinResult("g.V().hasLabel('person').group().by('name').by('age')");
    if (!txt.includes('"alice"') || !txt.includes('"bob"')) {
      throw new Error(`expected grouped names, got ${txt}`);
    }
  });

  await r('Gremlin_Path', async () => {
    const txt = await gremlinResult("g.V().has('name','alice').out('knows').limit(1).path()");
    if (!txt.includes('"alice"')) throw new Error(`expected alice in path, got ${txt}`);
  });

  await r('Gremlin_Union', async () => {
    const txt = await gremlinResult("g.V().has('name','bob').union(in('knows'),out('knows')).values('name').dedup().order()");
    if (!txt.includes('"alice"') || !txt.includes('"dave"')) {
      throw new Error(`expected alice and dave from union, got ${txt}`);
    }
  });

  await r('Gremlin_Coalesce', async () => {
    await gremlinContains("g.V().has('name','bob').coalesce(out('nonexistent'),in('knows')).values('name')", '"alice"');
  });

  await r('Gremlin_Choose', async () => {
    await gremlinContains("g.V().has('name','alice').choose(has('age'),values('age'),values('name'))", '25');
  });

  await r('Gremlin_Optional', async () => {
    const txt = await gremlinResult("g.V().has('name','dave').optional(out('knows')).values('name')");
    if (txt.includes('"charlie"')) {
      throw new Error(`optional should return original when no outgoing knows, got ${txt}`);
    }
  });

  await r('Gremlin_RepeatTimes', async () => {
    await gremlinContains("g.V().has('name','alice').repeat(out('knows')).times(2).values('name')", '"dave"');
  });

  await r('Gremlin_RepeatEmit', async () => {
    const txt = await gremlinResult("g.V().has('name','alice').repeat(out('knows')).emit().values('name').dedup().order()");
    for (const name of ['"bob"', '"charlie"', '"dave"']) {
      if (!txt.includes(name)) throw new Error(`expected ${name} in repeat-emit, got ${txt}`);
    }
  });

  await r('Gremlin_Fold', async () => {
    const txt = await gremlinResult("g.V().hasLabel('person').values('name').fold()");
    for (const name of ['"alice"', '"bob"', '"charlie"', '"dave"']) {
      if (!txt.includes(name)) throw new Error(`expected ${name} in fold, got ${txt}`);
    }
  });

  await r('Gremlin_Unfold', async () => {
    await gremlinContains("g.V().hasLabel('person').values('name').fold().unfold().count()", '4');
  });

  await r('Gremlin_Constant', async () => {
    await gremlinContains("g.V().has('name','alice').constant('hello').limit(1)", '"hello"');
  });

  await r('Gremlin_Is', async () => {
    await gremlinContains("g.V().hasLabel('person').values('age').is(gt(30))", '35');
  });

  await r('Gremlin_Not', async () => {
    await gremlinContains("g.V().hasLabel('person').not(out('knows')).values('name')", '"dave"');
  });

  await r('Gremlin_And', async () => {
    await gremlinContains("g.V().and(out('knows'),in('knows')).values('name')", '"bob"');
  });

  await r('Gremlin_Or', async () => {
    const txt = await gremlinResult("g.V().or(has('name','alice'),has('name','dave')).values('name').order()");
    if (!txt.includes('"alice"') || !txt.includes('"dave"')) {
      throw new Error(`expected alice or dave, got ${txt}`);
    }
  });

  await r('Gremlin_Mean', async () => {
    await gremlinContains("g.V().hasLabel('person').values('age').mean()", '32.5');
  });

  await r('Gremlin_Sum', async () => {
    await gremlinContains("g.V().hasLabel('person').values('age').sum()", '130');
  });

  await r('Gremlin_Min', async () => {
    await gremlinContains("g.V().hasLabel('person').values('age').min()", '25');
  });

  await r('Gremlin_Max', async () => {
    await gremlinContains("g.V().hasLabel('person').values('age').max()", '40');
  });

  await r('Gremlin_Tail', async () => {
    const txt = await gremlinResult("g.V().hasLabel('person').order().by('name').tail(1).values('name')");
    if (!txt.includes('"dave"')) throw new Error(`expected dave as tail, got ${txt}`);
  });

  await r('Gremlin_Inject', async () => {
    await gremlinContains("g.inject('a','b').count()", '2');
  });

  await r('Gremlin_MergeV', async () => {
    await gremlin("g.mergeV([~label:'item',name:'widget',price:10])");
    await gremlinContains("g.V().has('name','widget').values('price')", '10');
  });

  await r('Gremlin_MergeVIdempotent', async () => {
    await gremlin("g.mergeV([~label:'item',name:'widget',price:10])");
    const txt = await gremlinResult("g.V().has('name','widget').count()");
    if (!txt.includes('1')) throw new Error(`expected 1 widget after idempotent mergeV, got ${txt}`);
  });

  await r('Gremlin_ElementMap', async () => {
    const txt = await gremlinResult("g.V().has('name','alice').elementMap()");
    if (!txt.includes('"alice"') || !txt.includes('"person"')) {
      throw new Error(`expected alice and person in elementMap, got ${txt}`);
    }
  });

  await r('Gremlin_Project', async () => {
    await gremlinContains("g.V().as('a').out('knows').as('b').project('from','to').by('name').limit(1)", '"from"');
  });

  await r('Gremlin_CountLocal', async () => {
    await gremlinContains("g.V().hasLabel('person').values('name').fold().count(local)", '4');
  });

  await r('Gremlin_MultiHop', async () => {
    await gremlinContains("g.V().has('name','alice').out().out().values('name')", '"dave"');
  });

  await r('Gremlin_SimplePath', async () => {
    const txt = await gremlinResult("g.V().has('name','alice').repeat(__.out('knows')).times(2).simplePath().values('name').dedup()");
    if (!txt.includes('"dave"')) throw new Error(`expected dave in simplePath result, got ${txt}`);
  });

  await r('ErrorCase_InvalidGremlinSyntax', async () => {
    try {
      await gremlin('g.INVALID_STEP()');
      throw new Error('expected error for invalid gremlin syntax');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for invalid gremlin syntax') throw err;
    }
  });

  await r('ErrorCase_FastResetInvalidToken', async () => {
    try {
      await client.send(
        new ExecuteFastResetCommand({
          action: 'performDatabaseReset',
          token: 'invalid-token-12345',
        }),
      );
      throw new Error('expected error for invalid fast reset token');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for invalid fast reset token') throw err;
    }
  });

  await r('ErrorCase_NonExistentLoaderJob', async () => {
    try {
      await client.send(
        new GetLoaderJobStatusCommand({ loadId: 'nonexistent-load-id' }),
      );
      throw new Error('expected error for non-existent loader job');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for non-existent loader job') throw err;
    }
  });

  await r('ErrorCase_CancelNonExistentLoaderJob', async () => {
    try {
      await client.send(
        new CancelLoaderJobCommand({ loadId: 'nonexistent-load-id' }),
      );
      throw new Error('expected error for cancel non-existent loader job');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for cancel non-existent loader job') throw err;
    }
  });

  await r('ErrorCase_EmptyGremlinQuery', async () => {
    try {
      await gremlin('');
      throw new Error('expected error for empty gremlin query');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for empty gremlin query') throw err;
    }
  });
}
