import { AthenaClient } from '@aws-sdk/client-athena';
import { ServiceContext, TestRunner } from '../../runner.js';
import { makeUniqueName } from '../../helpers.js';

export interface AthenaTestContext {
  client: AthenaClient;
  runner: TestRunner;
  svcCtx: ServiceContext;
  wgName: string;
  catalogName: string;
  nqName: string;
  updatedNqName: string;
  oldNameReusable: string;
  psName: string;
  nqId: string;
  reusableNqId: string;
  secondNqId: string;
  queryExecutionId: string;
  psWorkGroup: string;
  catalogArn: string;
  wgArn: string;
}

export function createAthenaTestContext(
  runner: TestRunner,
  svcCtx: ServiceContext,
): AthenaTestContext {
  return {
    client: new AthenaClient({
      endpoint: svcCtx.endpoint,
      region: svcCtx.region,
      credentials: svcCtx.credentials,
    }),
    runner,
    svcCtx,
    wgName: makeUniqueName('testwg'),
    catalogName: makeUniqueName('testcat'),
    nqName: makeUniqueName('testnq'),
    updatedNqName: makeUniqueName('updatednq'),
    oldNameReusable: makeUniqueName('oldnamereuse'),
    psName: makeUniqueName('testps'),
    nqId: '',
    reusableNqId: '',
    secondNqId: '',
    queryExecutionId: '',
    psWorkGroup: '',
    catalogArn: '',
    wgArn: '',
  };
}
