import {
  NeptunedataClient as NeptuneDataClient,
  ExecuteOpenCypherQueryCommand,
  ExecuteGremlinQueryCommand,
  ExecuteFastResetCommand,
} from '@aws-sdk/client-neptunedata';

export function marshalDoc(v: unknown): string {
  return JSON.stringify(v);
}

export function createQueryHelpers(client: NeptuneDataClient) {
  const cypher = async (q: string) => {
    await client.send(new ExecuteOpenCypherQueryCommand({ openCypherQuery: q }));
  };

  const cypherResult = async (q: string): Promise<string> => {
    const resp = await client.send(new ExecuteOpenCypherQueryCommand({ openCypherQuery: q }));
    return marshalDoc(resp.results);
  };

  const cypherContains = async (q: string, substr: string) => {
    const txt = await cypherResult(q);
    if (!txt.includes(substr)) throw new Error(`expected ${substr} in result, got ${txt}`);
  };

  const gremlin = async (q: string) => {
    await client.send(new ExecuteGremlinQueryCommand({ gremlinQuery: q }));
  };

  const gremlinResult = async (q: string): Promise<string> => {
    const resp = await client.send(new ExecuteGremlinQueryCommand({ gremlinQuery: q }));
    return marshalDoc(resp.result);
  };

  const gremlinContains = async (q: string, substr: string) => {
    const txt = await gremlinResult(q);
    if (!txt.includes(substr)) throw new Error(`expected ${substr} in result, got ${txt}`);
  };

  const fastReset = async () => {
    const initResp = await client.send(
      new ExecuteFastResetCommand({ action: 'initiateDatabaseReset' }),
    );
    const token = initResp.payload?.token;
    if (!token) throw new Error('expected token from initiateDatabaseReset');
    await client.send(
      new ExecuteFastResetCommand({ action: 'performDatabaseReset', token }),
    );
  };

  return { cypher, cypherResult, cypherContains, gremlin, gremlinResult, gremlinContains, fastReset };
}
