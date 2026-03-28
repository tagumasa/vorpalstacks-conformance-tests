using Amazon.Scheduler;
using Amazon.Scheduler.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static class SchedulerServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonSchedulerClient schedulerClient,
        AmazonIdentityManagementServiceClient iamClient,
        string region)
    {
        var results = new List<TestResult>();
        var scheduleName = TestRunner.MakeUniqueName("CSSchedule");
        var roleName = TestRunner.MakeUniqueName("CSSchedRole");
        var roleArn = $"arn:aws:iam::000000000000:role/{roleName}";

        var trustPolicy = @"{
            ""Version"": ""2012-10-17"",
            ""Statement"": [{
                ""Effect"": ""Allow"",
                ""Principal"": {""Service"": ""scheduler.amazonaws.com""},
                ""Action"": ""sts:AssumeRole""
            }]
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

            results.Add(await runner.RunTestAsync("scheduler", "ListSchedules", async () =>
            {
                var resp = await schedulerClient.ListSchedulesAsync(new ListSchedulesRequest());
                if (resp.Schedules == null)
                    throw new Exception("Schedules is null");
            }));

            results.Add(await runner.RunTestAsync("scheduler", "CreateSchedule", async () =>
            {
                await schedulerClient.CreateScheduleAsync(new CreateScheduleRequest
                {
                    Name = scheduleName,
                    ScheduleExpression = "rate(30 minutes)",
                    Target = new Target
                    {
                        Arn = $"arn:aws:lambda:{region}:000000000000:function:TestFunction",
                        RoleArn = roleArn
                    },
                    FlexibleTimeWindow = new FlexibleTimeWindow
                    {
                        Mode = FlexibleTimeWindowMode.OFF
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("scheduler", "GetSchedule", async () =>
            {
                var resp = await schedulerClient.GetScheduleAsync(new GetScheduleRequest
                {
                    Name = scheduleName
                });
                if (resp.Name == null)
                    throw new Exception("Name is null");
            }));

            results.Add(await runner.RunTestAsync("scheduler", "UpdateSchedule", async () =>
            {
                await schedulerClient.UpdateScheduleAsync(new UpdateScheduleRequest
                {
                    Name = scheduleName,
                    ScheduleExpression = "rate(60 minutes)",
                    Target = new Target
                    {
                        Arn = $"arn:aws:lambda:{region}:000000000000:function:TestFunction",
                        RoleArn = roleArn
                    },
                    FlexibleTimeWindow = new FlexibleTimeWindow { Mode = FlexibleTimeWindowMode.OFF }
                });
            }));

            results.Add(await runner.RunTestAsync("scheduler", "DeleteSchedule", async () =>
            {
                await schedulerClient.DeleteScheduleAsync(new DeleteScheduleRequest
                {
                    Name = scheduleName
                });
            }));

            results.Add(await runner.RunTestAsync("scheduler", "GetSchedule_NonExistent", async () =>
            {
                try
                {
                    await schedulerClient.GetScheduleAsync(new GetScheduleRequest
                    {
                        Name = "NonExistentSchedule_xyz_12345"
                    });
                    throw new Exception("Expected error but got none");
                }
                catch (ResourceNotFoundException)
                {
                }
            }));

            results.Add(await runner.RunTestAsync("scheduler", "DeleteSchedule_NonExistent", async () =>
            {
                try
                {
                    await schedulerClient.DeleteScheduleAsync(new DeleteScheduleRequest { Name = "nonexistent-schedule-xyz" });
                    throw new Exception("expected error for non-existent schedule");
                }
                catch (ResourceNotFoundException) { }
            }));

            var scheduleArn = $"arn:aws:scheduler:{region}:000000000000:schedule/{scheduleName}";
            results.Add(await runner.RunTestAsync("scheduler", "TagResource", async () =>
            {
                await schedulerClient.TagResourceAsync(new TagResourceRequest
                {
                    ResourceArn = scheduleArn,
                    Tags = new List<Amazon.Scheduler.Model.Tag>
                    {
                        new Amazon.Scheduler.Model.Tag { Key = "Environment", Value = "test" }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("scheduler", "ListTagsForResource", async () =>
            {
                var resp = await schedulerClient.ListTagsForResourceAsync(new ListTagsForResourceRequest { ResourceArn = scheduleArn });
                if (resp.Tags == null)
                    throw new Exception("tags list is nil");
            }));

            results.Add(await runner.RunTestAsync("scheduler", "UntagResource", async () =>
            {
                await schedulerClient.UntagResourceAsync(new UntagResourceRequest
                {
                    ResourceArn = scheduleArn,
                    TagKeys = new List<string> { "Environment" }
                });
            }));
        }
        finally
        {
            try
            {
                await schedulerClient.DeleteScheduleAsync(new DeleteScheduleRequest { Name = scheduleName });
            }
            catch { }
            try
            {
                await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = roleName });
            }
            catch { }
        }

        results.Add(await runner.RunTestAsync("scheduler", "CreateSchedule_DuplicateName", async () =>
        {
            var dupName = TestRunner.MakeUniqueName("DupSchedule");
            var dupRoleName = TestRunner.MakeUniqueName("DupSchedRole");
            try { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = dupRoleName, AssumeRolePolicyDocument = trustPolicy }); } catch { }
            try
            {
                var dupRoleArn = $"arn:aws:iam::000000000000:role/{dupRoleName}";
                await schedulerClient.CreateScheduleAsync(new CreateScheduleRequest
                {
                    Name = dupName,
                    ScheduleExpression = "rate(30 minutes)",
                    Target = new Target { Arn = $"arn:aws:lambda:{region}:000000000000:function:TestFn", RoleArn = dupRoleArn },
                    FlexibleTimeWindow = new FlexibleTimeWindow { Mode = FlexibleTimeWindowMode.OFF }
                });
                try
                {
                    await schedulerClient.CreateScheduleAsync(new CreateScheduleRequest
                    {
                        Name = dupName,
                        ScheduleExpression = "rate(60 minutes)",
                        Target = new Target { Arn = $"arn:aws:lambda:{region}:000000000000:function:TestFn", RoleArn = dupRoleArn },
                        FlexibleTimeWindow = new FlexibleTimeWindow { Mode = FlexibleTimeWindowMode.OFF }
                    });
                    throw new Exception("expected error for duplicate schedule name");
                }
                catch (ConflictException) { }
            }
            finally
            {
                try { await schedulerClient.DeleteScheduleAsync(new DeleteScheduleRequest { Name = dupName }); } catch { }
                try { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = dupRoleName }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("scheduler", "UpdateSchedule_VerifyExpression", async () =>
        {
            var updName = TestRunner.MakeUniqueName("UpdSchedule");
            var updRoleName = TestRunner.MakeUniqueName("UpdSchedRole");
            try { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = updRoleName, AssumeRolePolicyDocument = trustPolicy }); } catch { }
            try
            {
                var updRoleArn = $"arn:aws:iam::000000000000:role/{updRoleName}";
                await schedulerClient.CreateScheduleAsync(new CreateScheduleRequest
                {
                    Name = updName,
                    ScheduleExpression = "rate(30 minutes)",
                    Target = new Target { Arn = $"arn:aws:lambda:{region}:000000000000:function:TestFn", RoleArn = updRoleArn },
                    FlexibleTimeWindow = new FlexibleTimeWindow { Mode = FlexibleTimeWindowMode.OFF }
                });
                var newExpr = "rate(60 minutes)";
                await schedulerClient.UpdateScheduleAsync(new UpdateScheduleRequest
                {
                    Name = updName,
                    ScheduleExpression = newExpr,
                    Target = new Target { Arn = $"arn:aws:lambda:{region}:000000000000:function:TestFn", RoleArn = updRoleArn },
                    FlexibleTimeWindow = new FlexibleTimeWindow { Mode = FlexibleTimeWindowMode.OFF }
                });
                var getResp = await schedulerClient.GetScheduleAsync(new GetScheduleRequest { Name = updName });
                if (getResp.ScheduleExpression != newExpr)
                    throw new Exception($"schedule expression not updated, got {getResp.ScheduleExpression}");
            }
            finally
            {
                try { await schedulerClient.DeleteScheduleAsync(new DeleteScheduleRequest { Name = updName }); } catch { }
                try { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = updRoleName }); } catch { }
            }
        }));

        return results;
    }
}
