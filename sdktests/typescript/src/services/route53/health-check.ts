import {
  Route53Client,
  CreateHealthCheckCommand,
  GetHealthCheckCommand,
  UpdateHealthCheckCommand,
  DeleteHealthCheckCommand,
  ListHealthChecksCommand,
} from '@aws-sdk/client-route-53';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup, assertErrorContains } from '../../helpers.js';

export async function runHealthCheckTests(
  runner: TestRunner,
  client: Route53Client,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('route53', 'CreateHealthCheck', async () => {
    const resp = await client.send(new CreateHealthCheckCommand({
      CallerReference: makeUniqueName('hcref-'),
      HealthCheckConfig: {
        Type: 'HTTP',
        ResourcePath: '/health',
        FullyQualifiedDomainName: 'example.com',
        RequestInterval: 30,
        FailureThreshold: 3,
        MeasureLatency: true,
        Disabled: false,
        EnableSNI: true,
        IPAddress: '192.0.2.1',
        Port: 443,
        Inverted: false,
        InsufficientDataHealthStatus: 'LastKnownStatus',
      },
    }));
    const hcID = resp.HealthCheck?.Id;
    if (hcID) await safeCleanup(() => client.send(new DeleteHealthCheckCommand({ HealthCheckId: hcID })));
  }));

  let healthCheckID = '';
  results.push(await runner.runTest('route53', 'CreateHealthCheck_GetID', async () => {
    const resp = await client.send(new CreateHealthCheckCommand({
      CallerReference: makeUniqueName('hcref2-'),
      HealthCheckConfig: {
        Type: 'TCP',
        FullyQualifiedDomainName: 'hc.example.com',
        Port: 8080,
      },
    }));
    const id = resp.HealthCheck?.Id;
    if (!id) throw new Error('expected health check ID to be defined');
    healthCheckID = id;
  }));

  if (healthCheckID) {
    results.push(await runner.runTest('route53', 'GetHealthCheck', async () => {
      const resp = await client.send(new GetHealthCheckCommand({ HealthCheckId: healthCheckID }));
      const cfg = resp.HealthCheck?.HealthCheckConfig;
      if (!cfg) throw new Error('expected HealthCheck config to be defined');
      if (cfg.Type !== 'TCP') throw new Error(`health check type mismatch: got ${cfg.Type}`);
    }));

    results.push(await runner.runTest('route53', 'UpdateHealthCheck', async () => {
      const resp = await client.send(new UpdateHealthCheckCommand({
        HealthCheckId: healthCheckID,
        ResourcePath: '/updated',
        FailureThreshold: 5,
        Disabled: true,
        Inverted: true,
        EnableSNI: false,
        FullyQualifiedDomainName: 'updated.example.com',
      }));
      if (!resp.HealthCheck) throw new Error('expected HealthCheck to be defined after update');
    }));

    results.push(await runner.runTest('route53', 'UpdateHealthCheck_VerifyContent', async () => {
      const resp = await client.send(new GetHealthCheckCommand({ HealthCheckId: healthCheckID }));
      const cfg = resp.HealthCheck?.HealthCheckConfig;
      if (cfg?.FailureThreshold !== 5) throw new Error(`failure threshold mismatch: got ${cfg?.FailureThreshold}`);
      if (cfg?.ResourcePath !== '/updated') throw new Error(`resource path mismatch: got ${cfg?.ResourcePath}`);
      if (cfg?.Disabled !== true) throw new Error('expected disabled=true');
      if (cfg?.Inverted !== true) throw new Error('expected inverted=true');
    }));

    results.push(await runner.runTest('route53', 'DeleteHealthCheck', async () => {
      await client.send(new DeleteHealthCheckCommand({ HealthCheckId: healthCheckID }));
    }));

    results.push(await runner.runTest('route53', 'GetHealthCheck_NonExistent', async () => {
      try {
        await client.send(new GetHealthCheckCommand({ HealthCheckId: '00000000-0000-0000-0000-000000000000' }));
        throw new Error('expected error');
      } catch (err) {
        assertErrorContains(err, 'NoSuchHealthCheck');
      }
    }));

    results.push(await runner.runTest('route53', 'DeleteHealthCheck_NonExistent', async () => {
      try {
        await client.send(new DeleteHealthCheckCommand({ HealthCheckId: '00000000-0000-0000-0000-000000000000' }));
        throw new Error('expected error');
      } catch (err) {
        assertErrorContains(err, 'NoSuchHealthCheck');
      }
    }));
  }

  results.push(await runner.runTest('route53', 'ListHealthChecks', async () => {
    const resp = await client.send(new ListHealthChecksCommand({ MaxItems: 10 }));
    if (!resp.HealthChecks) throw new Error('expected HealthChecks to be defined');
  }));

  results.push(await runner.runTest('route53', 'HealthCheckConfig_DefaultPort', async () => {
    const resp = await client.send(new CreateHealthCheckCommand({
      CallerReference: makeUniqueName('hcref-port-'),
      HealthCheckConfig: { Type: 'HTTP', FullyQualifiedDomainName: 'porttest.example.com' },
    }));
    const hcID = resp.HealthCheck?.Id;
    if (!hcID) throw new Error('expected health check ID');
    try {
      const getResp = await client.send(new GetHealthCheckCommand({ HealthCheckId: hcID }));
      if (getResp.HealthCheck?.HealthCheckConfig?.Port !== 80) {
        throw new Error(`expected default port 80, got ${getResp.HealthCheck?.HealthCheckConfig?.Port}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteHealthCheckCommand({ HealthCheckId: hcID })));
    }
  }));

  return results;
}
