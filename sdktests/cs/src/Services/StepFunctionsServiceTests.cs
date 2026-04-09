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

        var trustPolicy = IamHelpers.StatesTrustPolicy;

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
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = invalidRoleName }); });
        }));

        var verifySmArn = "";
        var verifySmName = TestRunner.MakeUniqueName("VerifySM");
        results.Add(await runner.RunTestAsync("stepfunctions", "UpdateStateMachine_VerifyDefinition", async () =>
        {
            var verifyRoleName = TestRunner.MakeUniqueName("VerifyRole");
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = verifyRoleName, AssumeRolePolicyDocument = trustPolicy }); });
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
                await TestHelpers.SafeCleanupAsync(async () => { await sfnClient.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = verifySmArn }); });
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = verifyRoleName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("stepfunctions", "Execution_PassStateOutput", async () =>
        {
            if (string.IsNullOrEmpty(verifySmArn))
                throw new Exception("state machine ARN not available");
            try
            {
                var verifyRoleName = TestRunner.MakeUniqueName("PassRole");
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = verifyRoleName, AssumeRolePolicyDocument = trustPolicy }); });
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
                        await TestHelpers.SafeCleanupAsync(async () => { await sfnClient.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = smArn }); });
                    }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = verifyRoleName }); });
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
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = lsmRoleName, AssumeRolePolicyDocument = trustPolicy }); });
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
                    await TestHelpers.SafeCleanupAsync(async () => { await sfnClient.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = smArn }); });
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = lsmRoleName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("stepfunctions", "ValidateStateMachineDefinition_Valid", async () =>
        {
            var validDef = @"{""Comment"":""valid"",""StartAt"":""S1"",""States"":{""S1"":{""Type"":""Pass"",""End"":true}}}";
            var resp = await sfnClient.ValidateStateMachineDefinitionAsync(new ValidateStateMachineDefinitionRequest
            {
                Definition = validDef
            });
            if (resp == null)
                throw new Exception("response is null");
        }));

        results.Add(await runner.RunTestAsync("stepfunctions", "ValidateStateMachineDefinition_Invalid", async () =>
        {
            try
            {
                var invalidDef = @"{""StartAt"":""MissingState""}";
                await sfnClient.ValidateStateMachineDefinitionAsync(new ValidateStateMachineDefinitionRequest
                {
                    Definition = invalidDef
                });
            }
            catch (AmazonStepFunctionsException) { }
        }));

        results.Add(await runner.RunTestAsync("stepfunctions", "GetStateMachine", async () =>
        {
            var gsmName = TestRunner.MakeUniqueName("GSMSM");
            var gsmRoleName = TestRunner.MakeUniqueName("GSMRole");
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = gsmRoleName, AssumeRolePolicyDocument = trustPolicy }); });
            try
            {
                var createResp = await sfnClient.CreateStateMachineAsync(new CreateStateMachineRequest
                {
                    Name = gsmName,
                    Definition = stateMachineDefinition,
                    RoleArn = $"arn:aws:iam::000000000000:role/{gsmRoleName}"
                });
                var smArn = createResp.StateMachineArn;
                try
                {
                    var resp = await sfnClient.DescribeStateMachineAsync(new DescribeStateMachineRequest { StateMachineArn = smArn });
                    if (resp.StateMachineArn != smArn)
                        throw new Exception("state machine ARN mismatch");
                    if (string.IsNullOrEmpty(resp.Definition))
                        throw new Exception("definition is null");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await sfnClient.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = smArn }); });
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = gsmRoleName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("stepfunctions", "DescribeStateMachineForExecution", async () =>
        {
            var dsmfeName = TestRunner.MakeUniqueName("DSMFESM");
            var dsmfeRoleName = TestRunner.MakeUniqueName("DSMFERole");
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = dsmfeRoleName, AssumeRolePolicyDocument = trustPolicy }); });
            try
            {
                var createResp = await sfnClient.CreateStateMachineAsync(new CreateStateMachineRequest
                {
                    Name = dsmfeName,
                    Definition = stateMachineDefinition,
                    RoleArn = $"arn:aws:iam::000000000000:role/{dsmfeRoleName}"
                });
                var smArn = createResp.StateMachineArn;
                try
                {
                    var startResp = await sfnClient.StartExecutionAsync(new StartExecutionRequest
                    {
                        StateMachineArn = smArn,
                        Input = "{}"
                    });
                    var resp = await sfnClient.DescribeStateMachineForExecutionAsync(new DescribeStateMachineForExecutionRequest
                    {
                        ExecutionArn = startResp.ExecutionArn
                    });
                    if (resp.StateMachineArn == null)
                        throw new Exception("state machine ARN is null");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await sfnClient.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = smArn }); });
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = dsmfeRoleName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("stepfunctions", "StartSyncExecution", async () =>
        {
            var sseName = TestRunner.MakeUniqueName("SSESM");
            var sseRoleName = TestRunner.MakeUniqueName("SSERole");
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = sseRoleName, AssumeRolePolicyDocument = trustPolicy }); });
            try
            {
                var passDef = @"{""StartAt"":""Pass"",""States"":{""Pass"":{""Type"":""Pass"",""Result"":""done"",""End"":true}}}";
                var createResp = await sfnClient.CreateStateMachineAsync(new CreateStateMachineRequest
                {
                    Name = sseName,
                    Definition = passDef,
                    RoleArn = $"arn:aws:iam::000000000000:role/{sseRoleName}"
                });
                var smArn = createResp.StateMachineArn;
                try
                {
                    try
                    {
                        var resp = await sfnClient.StartSyncExecutionAsync(new StartSyncExecutionRequest
                        {
                            StateMachineArn = smArn,
                            Input = "{}"
                        });
                        if (resp == null)
                            throw new Exception("response is null");
                    }
                    catch (AmazonStepFunctionsException) { }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await sfnClient.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = smArn }); });
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = sseRoleName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("stepfunctions", "TestState_Pass", async () =>
        {
            var passDef = @"{""StartAt"":""Pass"",""States"":{""Pass"":{""Type"":""Pass"",""Result"":{""x"":1},""End"":true}}}";
            try
            {
                var resp = await sfnClient.TestStateAsync(new TestStateRequest
                {
                    Definition = passDef,
                    RoleArn = roleArn,
                    Input = "{}"
                });
                if (resp == null)
                    throw new Exception("response is null");
            }
            catch (AmazonStepFunctionsException) { }
        }));

        var psvSmArn = "";
        var psvSmName = TestRunner.MakeUniqueName("PSVSM");
        var psvRoleName = TestRunner.MakeUniqueName("PSVRole");
        await TestHelpers.SafeCleanupAsync(async () => { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = psvRoleName, AssumeRolePolicyDocument = trustPolicy }); });
        try
        {
            var createResp = await sfnClient.CreateStateMachineAsync(new CreateStateMachineRequest
            {
                Name = psvSmName,
                Definition = stateMachineDefinition,
                RoleArn = $"arn:aws:iam::000000000000:role/{psvRoleName}"
            });
            psvSmArn = createResp.StateMachineArn;

            results.Add(await runner.RunTestAsync("stepfunctions", "PublishStateMachineVersion", async () =>
            {
                var resp = await sfnClient.PublishStateMachineVersionAsync(new PublishStateMachineVersionRequest
                {
                    StateMachineArn = psvSmArn
                });
                if (resp == null || string.IsNullOrEmpty(resp.StateMachineVersionArn))
                    throw new Exception("state machine version ARN is null");
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "ListStateMachineVersions", async () =>
            {
                var resp = await sfnClient.ListStateMachineVersionsAsync(new ListStateMachineVersionsRequest
                {
                    StateMachineArn = psvSmArn
                });
                if (resp.StateMachineVersions == null)
                    throw new Exception("state machine versions list is null");
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "PublishStateMachineVersion_Second", async () =>
            {
                await sfnClient.UpdateStateMachineAsync(new UpdateStateMachineRequest
                {
                    StateMachineArn = psvSmArn,
                    Definition = @"{""Comment"":""v2"",""StartAt"":""X"",""States"":{""X"":{""Type"":""Pass"",""End"":true}}}"
                });
                var resp = await sfnClient.PublishStateMachineVersionAsync(new PublishStateMachineVersionRequest
                {
                    StateMachineArn = psvSmArn
                });
                if (resp == null || string.IsNullOrEmpty(resp.StateMachineVersionArn))
                    throw new Exception("second version ARN is null");
            }));

            string? versionArnToDelete = null;
            results.Add(await runner.RunTestAsync("stepfunctions", "DeleteStateMachineVersion", async () =>
            {
                var listResp = await sfnClient.ListStateMachineVersionsAsync(new ListStateMachineVersionsRequest
                {
                    StateMachineArn = psvSmArn
                });
                if (listResp.StateMachineVersions != null && listResp.StateMachineVersions.Count > 0)
                {
                    versionArnToDelete = listResp.StateMachineVersions[0].StateMachineVersionArn;
                    await sfnClient.DeleteStateMachineVersionAsync(new DeleteStateMachineVersionRequest
                    {
                        StateMachineVersionArn = versionArnToDelete
                    });
                }
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "DeleteStateMachineVersion_NonExistent", async () =>
            {
                try
                {
                    await sfnClient.DeleteStateMachineVersionAsync(new DeleteStateMachineVersionRequest
                    {
                        StateMachineVersionArn = $"arn:aws:states:{region}:000000000000:stateMachine:fake:version-1"
                    });
                    throw new Exception("expected error for non-existent state machine version");
                }
                catch (AmazonStepFunctionsException) { }
            }));
        }
        finally
        {
            await TestHelpers.SafeCleanupAsync(async () => { await sfnClient.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = psvSmArn }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = psvRoleName }); });
        }

        var aliasSmArn = "";
        var aliasSmName = TestRunner.MakeUniqueName("AliasSM");
        var aliasRoleName = TestRunner.MakeUniqueName("AliasRole");
        var aliasName = TestRunner.MakeUniqueName("CSAlias");
        await TestHelpers.SafeCleanupAsync(async () => { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = aliasRoleName, AssumeRolePolicyDocument = trustPolicy }); });
        try
        {
            var createResp = await sfnClient.CreateStateMachineAsync(new CreateStateMachineRequest
            {
                Name = aliasSmName,
                Definition = stateMachineDefinition,
                RoleArn = $"arn:aws:iam::000000000000:role/{aliasRoleName}"
            });
            aliasSmArn = createResp.StateMachineArn;

            results.Add(await runner.RunTestAsync("stepfunctions", "CreateStateMachineAlias", async () =>
            {
                var resp = await sfnClient.CreateStateMachineAliasAsync(new CreateStateMachineAliasRequest
                {
                    Name = aliasName,
                    RoutingConfiguration = new List<RoutingConfigurationListItem>
                    {
                        new RoutingConfigurationListItem
                        {
                            StateMachineVersionArn = aliasSmArn,
                            Weight = 100
                        }
                    }
                });
                if (resp == null || string.IsNullOrEmpty(resp.StateMachineAliasArn))
                    throw new Exception("state machine alias ARN is null");
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "DescribeStateMachineAlias", async () =>
            {
                var aliasArn = $"arn:aws:states:{region}:000000000000:stateMachineAlias:{aliasName}";
                var resp = await sfnClient.DescribeStateMachineAliasAsync(new DescribeStateMachineAliasRequest
                {
                    StateMachineAliasArn = aliasArn
                });
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "ListStateMachineAliases", async () =>
            {
                var resp = await sfnClient.ListStateMachineAliasesAsync(new ListStateMachineAliasesRequest
                {
                    StateMachineArn = aliasSmArn
                });
                if (resp.StateMachineAliases == null)
                    throw new Exception("state machine aliases list is null");
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "UpdateStateMachineAlias", async () =>
            {
                var aliasArn = $"arn:aws:states:{region}:000000000000:stateMachineAlias:{aliasName}";
                await sfnClient.UpdateStateMachineAliasAsync(new UpdateStateMachineAliasRequest
                {
                    StateMachineAliasArn = aliasArn,
                    RoutingConfiguration = new List<RoutingConfigurationListItem>
                    {
                        new RoutingConfigurationListItem
                        {
                            StateMachineVersionArn = aliasSmArn,
                            Weight = 100
                        }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "DeleteStateMachineAlias", async () =>
            {
                var aliasArn = $"arn:aws:states:{region}:000000000000:stateMachineAlias:{aliasName}";
                await sfnClient.DeleteStateMachineAliasAsync(new DeleteStateMachineAliasRequest
                {
                    StateMachineAliasArn = aliasArn
                });
            }));

            results.Add(await runner.RunTestAsync("stepfunctions", "DeleteStateMachineAlias_NonExistent", async () =>
            {
                try
                {
                    await sfnClient.DeleteStateMachineAliasAsync(new DeleteStateMachineAliasRequest
                    {
                        StateMachineAliasArn = $"arn:aws:states:{region}:000000000000:stateMachineAlias:nonexistent-xyz"
                    });
                    throw new Exception("expected error for non-existent alias");
                }
                catch (AmazonStepFunctionsException) { }
            }));
        }
        finally
        {
            try { await sfnClient.DeleteStateMachineAliasAsync(new DeleteStateMachineAliasRequest { StateMachineAliasArn = $"arn:aws:states:{region}:000000000000:stateMachineAlias:{aliasName}" }); } catch { }
            await TestHelpers.SafeCleanupAsync(async () => { await sfnClient.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = aliasSmArn }); });
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = aliasRoleName }); });
        }

        results.Add(await runner.RunTestAsync("stepfunctions", "CreateStateMachineAlias_Duplicate", async () =>
        {
            var dupAliasName = TestRunner.MakeUniqueName("DupAlias");
            var dupAliasRoleName = TestRunner.MakeUniqueName("DupAliasRole");
            var dupAliasSmName = TestRunner.MakeUniqueName("DupAliasSM");
            await TestHelpers.SafeCleanupAsync(async () => { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = dupAliasRoleName, AssumeRolePolicyDocument = trustPolicy }); });
            try
            {
                var createResp = await sfnClient.CreateStateMachineAsync(new CreateStateMachineRequest
                {
                    Name = dupAliasSmName,
                    Definition = stateMachineDefinition,
                    RoleArn = $"arn:aws:iam::000000000000:role/{dupAliasRoleName}"
                });
                var dupSmArn = createResp.StateMachineArn;
                try
                {
                    await sfnClient.CreateStateMachineAliasAsync(new CreateStateMachineAliasRequest
                    {
                        Name = dupAliasName,
                        RoutingConfiguration = new List<RoutingConfigurationListItem>
                        {
                            new RoutingConfigurationListItem { StateMachineVersionArn = dupSmArn, Weight = 100 }
                        }
                    });
                    try
                    {
                        await sfnClient.CreateStateMachineAliasAsync(new CreateStateMachineAliasRequest
                        {
                            Name = dupAliasName,
                            RoutingConfiguration = new List<RoutingConfigurationListItem>
                            {
                                new RoutingConfigurationListItem { StateMachineVersionArn = dupSmArn, Weight = 100 }
                            }
                        });
                        throw new Exception("expected error for duplicate alias");
                    }
                    catch (AmazonStepFunctionsException) { }
                }
                finally
                {
                    try { await sfnClient.DeleteStateMachineAliasAsync(new DeleteStateMachineAliasRequest { StateMachineAliasArn = $"arn:aws:states:{region}:000000000000:stateMachineAlias:{dupAliasName}" }); } catch { }
                    await TestHelpers.SafeCleanupAsync(async () => { await sfnClient.DeleteStateMachineAsync(new DeleteStateMachineRequest { StateMachineArn = dupSmArn }); });
                }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = dupAliasRoleName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("stepfunctions", "ListStateMachines_Pagination", async () =>
        {
            var resp = await sfnClient.ListStateMachinesAsync(new ListStateMachinesRequest { MaxResults = 10 });
            if (resp.StateMachines == null)
                throw new Exception("state machines list is null");
            if (!string.IsNullOrEmpty(resp.NextToken))
            {
                var resp2 = await sfnClient.ListStateMachinesAsync(new ListStateMachinesRequest
                {
                    MaxResults = 10,
                    NextToken = resp.NextToken
                });
                if (resp2.StateMachines == null)
                    throw new Exception("state machines list page 2 is null");
            }
        }));

        return results;
    }
}
