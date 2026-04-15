import {
  CreateVirtualMFADeviceCommand,
  ListVirtualMFADevicesCommand,
  DeleteVirtualMFADeviceCommand,
} from '@aws-sdk/client-iam';
import { IAMTestContext } from './context.js';

export async function runMFATests(ctx: IAMTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client } = ctx;
  const results: import('../../runner.js').TestResult[] = [];

  results.push(await runner.runTest('iam', 'CreateVirtualMFADevice', async () => {
    const resp = await client.send(new CreateVirtualMFADeviceCommand({
      VirtualMFADeviceName: ctx.virtualMFADeviceName,
      Tags: [{ Key: 'Purpose', Value: 'test' }],
    }));
    if (!resp.VirtualMFADevice) throw new Error('virtual mfa device to be defined');
    if (!resp.VirtualMFADevice.SerialNumber) throw new Error('serial number to be defined');
    ctx.virtualMFASerial = resp.VirtualMFADevice.SerialNumber;
  }));

  results.push(await runner.runTest('iam', 'ListVirtualMFADevices', async () => {
    const resp = await client.send(new ListVirtualMFADevicesCommand({}));
    if (!resp.VirtualMFADevices) throw new Error('virtual mfa devices list to be defined');
  }));

  results.push(await runner.runTest('iam', 'DeleteVirtualMFADevice', async () => {
    await client.send(new DeleteVirtualMFADeviceCommand({ SerialNumber: ctx.virtualMFASerial }));
  }));

  return results;
}
