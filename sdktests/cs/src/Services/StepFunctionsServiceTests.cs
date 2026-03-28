using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static class StepFunctionsServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonStepFunctionsClient sfnClient,
        AmazonIdentityManagementServiceClient iamClient,
        string region)
    {
        var results = new List<TestResult>();
        var stateMachineName = TestRunner.MakeUniqueName("CSStateMachine");
        var roleName = TestRunner.MakeUniqueName("CSSfnRole");
        var roleArn = $"arn:aws:iam::000000000000:role/{roleName}";
        var stateMachineArn = "";

        var trustPolicy = @"{
            ""Version"": ""2012-10-17"",
            ""Statement"": [{
                ""Effect"": ""Allow"",
                ""Principal"": {""Service"": ""states.amazonaws.com""},
                ""Action"": ""sts:AssumeRole""
            }]
        }";

        var stateMachineDefinition = @"{
            ""Comment"": ""A Hello World example"",
            ""StartAt"": ""HelloWorld"",
            ""States"": {
                ""HelloWorld"": {
                    ""Type"": ""Pass"",
                    ""End"": true
                }
            }
        }";

        try
        {
            try
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = roleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            }
            catch { }

            results.Add(await runner.RunTestAsync("stepfunctions", "CreateStateMachine", async () =>
            {
                var resp = await sfnClient.CreateStateMachineAsync(new CreateStateMachineRequest
                {
                    Name = stateMachineName,
                    Definition = stateMachineDefinition,
                    RoleArn = roleArn
                });
                if (resp.StateMachineArn == null)
                    throw new Exception("StateMachineArn is null");
                stateMachineArn = resp.StateMachineArn;
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "DescribeStateMachine", async () =>
            {
                var resp = await sfnClient.DescribeStateMachineAsync(new DescribeStateMachineRequest
                {
                    StateMachineArn = stateMachineArn
                });
                if (resp.StateMachineArn == null)
                    throw new Exception("StateMachineArn is null");
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "ListStateMachines", async () =>
            {
                var resp = await sfnClient.ListStateMachinesAsync(new ListStateMachinesRequest());
                if (resp.StateMachines == null)
                    throw new Exception("StateMachines is null");
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "StartExecution", async () =>
            {
                var input = System.Text.Json.JsonSerializer.Serialize(new { message = "test" });
                var resp = await sfnClient.StartExecutionAsync(new StartExecutionRequest
                {
                    StateMachineArn = stateMachineArn,
                    Input = input
                });
                if (string.IsNullOrEmpty(resp.ExecutionArn))
                    throw new Exception("execution ARN is nil");
            }));

            var executionArn = "";
            results.Add(await runner.RunTestAsync("stepfunctions", "ListExecutions", async () =>
            {
                var resp = await sfnClient.ListExecutionsAsync(new ListExecutionsRequest
                {
                    StateMachineArn = stateMachineArn
                });
                if (resp.Executions == null)
                    throw new Exception("executions list is nil");
                if (resp.Executions.Count > 0)
                    executionArn = resp.Executions[0].ExecutionArn;
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "DescribeExecution", async () =>
            {
                if (string.IsNullOrEmpty(executionArn))
                    throw new Exception("no execution ARN available");
                var resp = await sfnClient.DescribeExecutionAsync(new DescribeExecutionRequest
                {
                    ExecutionArn = executionArn
                });
                if (string.IsNullOrEmpty(resp.Status))
                    throw new Exception("execution status is empty");
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "GetExecutionHistory", async () =>
            {
                if (string.IsNullOrEmpty(executionArn))
                    throw new Exception("no execution ARN available");
                var resp = await sfnClient.GetExecutionHistoryAsync(new GetExecutionHistoryRequest
                {
                    ExecutionArn = executionArn
                });
                if (resp.Events == null)
                    throw new Exception("events list is nil");
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "UpdateStateMachine", async () =>
            {
                var resp = await sfnClient.UpdateStateMachineAsync(new UpdateStateMachineRequest
                {
                    StateMachineArn = stateMachineArn,
                    Definition = stateMachineDefinition
                });
            }));

            var activityName = TestRunner.MakeUniqueName("CSActivity");
            var activityArn = "";
            results.Add(await runner.RunTestAsync("stepfunctions", "CreateActivity", async () =>
            {
                var resp = await sfnClient.CreateActivityAsync(new CreateActivityRequest
                {
                    Name = activityName
                });
                if (string.IsNullOrEmpty(resp.ActivityArn))
                    throw new Exception("ActivityArn is nil");
                activityArn = resp.ActivityArn;
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "DescribeActivity", async () =>
            {
                var resp = await sfnClient.DescribeActivityAsync(new DescribeActivityRequest
                {
                    ActivityArn = activityArn
                });
                if (string.IsNullOrEmpty(resp.Name))
                    throw new Exception("activity name is nil");
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "ListActivities", async () =>
            {
                var resp = await sfnClient.ListActivitiesAsync(new ListActivitiesRequest());
                if (resp.Activities == null)
                    throw new Exception("activities list is nil");
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "TagResource", async () =>
            {
                await sfnClient.TagResourceAsync(new TagResourceRequest
                {
                    ResourceArn = stateMachineArn,
                    Tags = new List<Amazon.StepFunctions.Model.Tag>
                    {
                        new Amazon.StepFunctions.Model.Tag { Key = "Environment", Value = "test" }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "ListTagsForResource", async () =>
            {
                var resp = await sfnClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceArn = stateMachineArn
                });
                if (resp.Tags == null)
                    throw new Exception("tags list is nil");
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "UntagResource", async () =>
            {
                await sfnClient.UntagResourceAsync(new UntagResourceRequest
                {
                    ResourceArn = stateMachineArn,
                    TagKeys = new List<string> { "Environment" }
                });
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "DeleteActivity", async () =>
            {
                await sfnClient.DeleteActivityAsync(new DeleteActivityRequest
                {
                    ActivityArn = activityArn
                });
                activityArn = "";
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "DeleteStateMachine", async () =>
            {
                await sfnClient.DeleteStateMachineAsync(new DeleteStateMachineRequest
                {
                    StateMachineArn = stateMachineArn
                });
                stateMachineArn = "";
            }));
        }
        finally
        {
            try
            {
                if (!string.IsNullOrEmpty(stateMachineArn))
                {
                    await sfnClient.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = stateMachineArn });
                }
            }
            catch { }
            try
            {
                await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = roleName });
            }
            catch { }
        }

        results.Add(await runner.RunTestAsync("stepfunctions", "DescribeNonExistentStateMachine", async () =>
        {
            try
            {
                await sfnClient.DescribeStateMachineAsync(new DescribeStateMachineRequest
                {
                    StateMachineArn = "arn:aws:states:us-east-1:123456789012:stateMachine:NonExistentSM_xyz_12345"
                });
                throw new Exception("Expected error but got none");
            }
            catch (AmazonStepFunctionsException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("stepfunctions", "DeleteStateMachine_NonExistent", async () =>
        {
            try
            {
                await sfnClient.DeleteStateMachineAsync(new DeleteStateMachineRequest
                {
                    StateMachineArn = "arn:aws:states:us-east-1:000000000000:stateMachine:nonexistent-fake-arn"
                });
                throw new Exception("expected error for non-existent state machine");
            }
            catch (AmazonStepFunctionsException) { }
        }));

        results.Add(await runner.RunTestAsync("stepfunctions", "DescribeExecution_NonExistent", async () =>
        {
            try
            {
                await sfnClient.DescribeExecutionAsync(new DescribeExecutionRequest
                {
                    ExecutionArn = "arn:aws:states:us-east-1:000000000000:execution:nonexistent:fake-exec"
                });
                throw new Exception("expected error for non-existent execution");
            }
            catch (AmazonStepFunctionsException) { }
        }));

        results.Add(await runner.RunTestAsync("stepfunctions", "DescribeActivity_NonExistent", async () =>
        {
            try
            {
                await sfnClient.DescribeActivityAsync(new DescribeActivityRequest
                {
                    ActivityArn = "arn:aws:states:us-east-1:000000000000:activity:nonexistent-fake-arn"
                });
                throw new Exception("expected error for non-existent activity");
            }
            catch (AmazonStepFunctionsException) { }
        }));

        results.Add(await runner.RunTestAsync("stepfunctions", "CreateStateMachine_InvalidDefinition", async () =>
        {
            var invalidName = TestRunner.MakeUniqueName("InvalidSM");
            var invalidRoleName = TestRunner.MakeUniqueName("InvalidRole");
            try
            {
                await iamClient.CreateRoleAsync(new CreateRoleRequest
                {
                    RoleName = invalidRoleName,
                    AssumeRolePolicyDocument = trustPolicy
                });
            }
            catch { }
            try
            {
                await sfnClient.CreateStateMachineAsync(new CreateStateMachineRequest
                {
                    Name = invalidName,
                    Definition = "not valid json {{{",
                    RoleArn = $"arn:aws:iam::000000000000:role/{invalidRoleName}"
                });
            }
            catch
            {
                try
                {
                    await sfnClient.DeleteStateMachineAsync(new DeleteStateMachineRequest
                    {
                        StateMachineArn = $"arn:aws:states:{region}:000000000000:stateMachine:{invalidName}"
                    });
                }
                catch { }
                throw;
            }
            try
            {
                await sfnClient.DeleteStateMachineAsync(new DeleteStateMachineRequest
                {
                    StateMachineArn = $"arn:aws:states:{region}:000000000000:stateMachine:{invalidName}"
                });
            }
            catch { }
            try { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = invalidRoleName }); } catch { }
        }));

        var verifySmArn = "";
        var verifySmName = TestRunner.MakeUniqueName("VerifySM");
        results.Add(await runner.RunTestAsync("stepfunctions", "UpdateStateMachine_VerifyDefinition", async () =>
        {
            var verifyRoleName = TestRunner.MakeUniqueName("VerifyRole");
            try { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = verifyRoleName, AssumeRolePolicyDocument = trustPolicy }); } catch { }
            try
            {
                var def1 = @"{""Comment"":""v1"",""StartAt"":""A"",""States"":{""A"":{""Type"":""Pass"",""End"":true}}}";
                var createResp = await sfnClient.CreateStateMachineAsync(new CreateStateMachineRequest
                {
                    Name = verifySmName,
                    Definition = def1,
                    RoleArn = $"arn:aws:iam::000000000000:role/{verifyRoleName}"
                });
                verifySmArn = createResp.StateMachineArn;

                var def2 = @"{""Comment"":""v2"",""StartAt"":""B"",""States"":{""B"":{""Type"":""Pass"",""Result"":""hello"",""End"":true}}}";
                await sfnClient.UpdateStateMachineAsync(new UpdateStateMachineRequest
                {
                    StateMachineArn = verifySmArn,
                    Definition = def2
                });
                var descResp = await sfnClient.DescribeStateMachineAsync(new DescribeStateMachineRequest
                {
                    StateMachineArn = verifySmArn
                });
                if (descResp.Definition != def2)
                    throw new Exception($"definition not updated: got {descResp.Definition}, want {def2}");
            }
            finally
            {
                try { await sfnClient.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = verifySmArn }); } catch { }
                try { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = verifyRoleName }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("stepfunctions", "Execution_PassStateOutput", async () =>
        {
            if (string.IsNullOrEmpty(verifySmArn))
                throw new Exception("state machine ARN not available");
            try
            {
                var verifyRoleName = TestRunner.MakeUniqueName("PassRole");
                try { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = verifyRoleName, AssumeRolePolicyDocument = trustPolicy }); } catch { }
                try
                {
                    var def2 = @"{""Comment"":""v2"",""StartAt"":""B"",""States"":{""B"":{""Type"":""Pass"",""Result"":""hello"",""End"":true}}}";
                    var createResp = await sfnClient.CreateStateMachineAsync(new CreateStateMachineRequest
                    {
                        Name = verifySmName + "Exec",
                        Definition = def2,
                        RoleArn = $"arn:aws:iam::000000000000:role/{verifyRoleName}"
                    });
                    var smArn = createResp.StateMachineArn;
                    try
                    {
                        var startResp = await sfnClient.StartExecutionAsync(new StartExecutionRequest
                        {
                            StateMachineArn = smArn,
                            Input = @"{""value"":42}"
                        });
                        for (int i = 0; i < 10; i++)
                        {
                            await Task.Delay(500);
                            var descResp = await sfnClient.DescribeExecutionAsync(new DescribeExecutionRequest
                            {
                                ExecutionArn = startResp.ExecutionArn
                            });
                            if (descResp.Status == "SUCCEEDED")
                            {
                                if (string.IsNullOrEmpty(descResp.Output))
                                    throw new Exception("execution output is nil");
                                if (descResp.Output != @"""hello""")
                                    throw new Exception($"expected output \"hello\", got {descResp.Output}");
                                return;
                            }
                            if (descResp.Status == "FAILED" || descResp.Status == "ABORTED")
                                throw new Exception($"execution failed with status {descResp.Status}");
                        }
                        throw new Exception("execution did not complete in time");
                    }
                    finally
                    {
                        try { await sfnClient.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = smArn }); } catch { }
                    }
                }
                finally
                {
                    try { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = verifyRoleName }); } catch { }
                }
            }
            finally
            {
                verifySmArn = "";
            }
        }));

        results.Add(await runner.RunTestAsync("stepfunctions", "ListStateMachines_ContainsCreated", async () =>
        {
            var lsmName = TestRunner.MakeUniqueName("LSMTest");
            var lsmRoleName = TestRunner.MakeUniqueName("LSMRole");
            try { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = lsmRoleName, AssumeRolePolicyDocument = trustPolicy }); } catch { }
            try
            {
                var createResp = await sfnClient.CreateStateMachineAsync(new CreateStateMachineRequest
                {
                    Name = lsmName,
                    Definition = stateMachineDefinition,
                    RoleArn = $"arn:aws:iam::000000000000:role/{lsmRoleName}"
                });
                var smArn = createResp.StateMachineArn;
                try
                {
                    var listResp = await sfnClient.ListStateMachinesAsync(new ListStateMachinesRequest());
                    bool found = false;
                    foreach (var sm in listResp.StateMachines)
                    {
                        if (sm.StateMachineArn == smArn)
                        {
                            found = true;
                            if (sm.Name != lsmName)
                                throw new Exception("state machine name mismatch");
                            break;
                        }
                    }
                    if (!found)
                        throw new Exception("state machine not found in list");
                }
                finally
                {
                    try { await sfnClient.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = smArn }); } catch { }
                }
            }
            finally
            {
                try { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = lsmRoleName }); } catch { }
            }
        }));

        return results;
    }
}
