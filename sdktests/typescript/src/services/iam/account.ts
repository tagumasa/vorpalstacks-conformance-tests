import {
  GetAccountSummaryCommand,
  GetAccountAuthorizationDetailsCommand,
  CreateAccountAliasCommand,
  ListAccountAliasesCommand,
  DeleteAccountAliasCommand,
  UpdateAccountPasswordPolicyCommand,
  GetAccountPasswordPolicyCommand,
  DeleteAccountPasswordPolicyCommand,
} from '@aws-sdk/client-iam';
import { IAMTestContext } from './context.js';

export async function runAccountTests(ctx: IAMTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client } = ctx;
  const results: import('../../runner.js').TestResult[] = [];

  results.push(await runner.runTest('iam', 'GetAccountSummary', async () => {
    const resp = await client.send(new GetAccountSummaryCommand({}));
    if (!resp.SummaryMap) throw new Error('summary map to be defined');
  }));

  results.push(await runner.runTest('iam', 'GetAccountAuthorizationDetails', async () => {
    const resp = await client.send(new GetAccountAuthorizationDetailsCommand({ Filter: ['User'] }));
    if (!resp.UserDetailList) throw new Error('user detail list to be defined');
  }));

  results.push(await runner.runTest('iam', 'CreateAccountAlias', async () => {
    await client.send(new CreateAccountAliasCommand({ AccountAlias: ctx.accountAlias }));
  }));

  results.push(await runner.runTest('iam', 'ListAccountAliases', async () => {
    const resp = await client.send(new ListAccountAliasesCommand({}));
    if (!resp.AccountAliases) throw new Error('account aliases list to be defined');
  }));

  results.push(await runner.runTest('iam', 'DeleteAccountAlias', async () => {
    await client.send(new DeleteAccountAliasCommand({ AccountAlias: ctx.accountAlias }));
  }));

  results.push(await runner.runTest('iam', 'UpdateAccountPasswordPolicy', async () => {
    await client.send(new UpdateAccountPasswordPolicyCommand({
      MinimumPasswordLength: 12,
      RequireUppercaseCharacters: true,
      RequireLowercaseCharacters: true,
      RequireNumbers: true,
      RequireSymbols: true,
      AllowUsersToChangePassword: true,
      MaxPasswordAge: 90,
      PasswordReusePrevention: 5,
    }));
  }));

  results.push(await runner.runTest('iam', 'GetAccountPasswordPolicy', async () => {
    const resp = await client.send(new GetAccountPasswordPolicyCommand({}));
    if (!resp.PasswordPolicy) throw new Error('password policy to be defined');
  }));

  results.push(await runner.runTest('iam', 'DeleteAccountPasswordPolicy', async () => {
    await client.send(new DeleteAccountPasswordPolicyCommand({}));
  }));

  return results;
}
