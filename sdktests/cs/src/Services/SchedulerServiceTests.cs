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

        var trustPolicy = IamHelpers.SchedulerTrustPolicy;

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

            results.Add(await runner.RunTestAsync("scheduler", "CreateSchedule", async () =>
            {
                var resp = await schedulerClient.CreateScheduleAsync(new CreateScheduleRequest
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
                if (resp == null)
                    throw new Exception("response is null");
            }));

            results.Add(await runner.RunTestAsync("scheduler", "GetSchedule", async () =>
            {
                var resp = await schedulerClient.GetScheduleAsync(new GetScheduleRequest
                {
                    Name = scheduleName
                });
                if (resp.Name == null)
                    throw new Exception("schedule name is nil");
            }));

            results.Add(await runner.RunTestAsync("scheduler", "ListSchedules", async () =>
            {
                var resp = await schedulerClient.ListSchedulesAsync(new ListSchedulesRequest());
                if (resp.Schedules == null)
                    throw new Exception("schedules list is nil");
            }));

            results.Add(await runner.RunTestAsync("scheduler", "UpdateSchedule", async () =>
            {
                var resp = await schedulerClient.UpdateScheduleAsync(new UpdateScheduleRequest
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
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("scheduler", "TagResource", async () =>
            {
                var scheduleArn = $"arn:aws:scheduler:{region}:000000000000:schedule/{scheduleName}";
                var resp = await schedulerClient.TagResourceAsync(new TagResourceRequest
                {
                    ResourceArn = scheduleArn,
                    Tags = new List<Amazon.Scheduler.Model.Tag>
                    {
                        new Amazon.Scheduler.Model.Tag { Key = "Environment", Value = "test" }
                    }
                });
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("scheduler", "ListTagsForResource", async () =>
            {
                var scheduleArn = $"arn:aws:scheduler:{region}:000000000000:schedule/{scheduleName}";
                var resp = await schedulerClient.ListTagsForResourceAsync(new ListTagsForResourceRequest { ResourceArn = scheduleArn });
                if (resp.Tags == null)
                    throw new Exception("tags list is nil");
            }));

            results.Add(await runner.RunTestAsync("scheduler", "UntagResource", async () =>
            {
                var scheduleArn = $"arn:aws:scheduler:{region}:000000000000:schedule/{scheduleName}";
                var resp = await schedulerClient.UntagResourceAsync(new UntagResourceRequest
                {
                    ResourceArn = scheduleArn,
                    TagKeys = new List<string> { "Environment" }
                });
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("scheduler", "GetSchedule_ContentVerify", async () =>
            {
                var getResp = await schedulerClient.GetScheduleAsync(new GetScheduleRequest { Name = scheduleName });
                if (getResp.Name == null || getResp.Name != scheduleName)
                    throw new Exception("name mismatch");
                if (getResp.ScheduleExpression == null || getResp.ScheduleExpression != "rate(60 minutes)")
                    throw new Exception($"expression mismatch: {getResp.ScheduleExpression}");
                if (getResp.Arn == null)
                    throw new Exception("ARN is nil");
                if (getResp.CreationDate == null)
                    throw new Exception("CreationDate is nil");
                if (getResp.LastModificationDate == null)
                    throw new Exception("LastModificationDate is nil");
                if (getResp.Target == null)
                    throw new Exception("Target is nil");
                if (getResp.FlexibleTimeWindow == null || getResp.FlexibleTimeWindow.Mode != FlexibleTimeWindowMode.OFF)
                    throw new Exception("FlexibleTimeWindow mode mismatch");
            }));

            results.Add(await runner.RunTestAsync("scheduler", "DeleteSchedule", async () =>
            {
                var resp = await schedulerClient.DeleteScheduleAsync(new DeleteScheduleRequest { Name = scheduleName });
                if (resp == null)
                    throw new Exception("response is nil");
            }));

            results.Add(await runner.RunTestAsync("scheduler", "GetSchedule_NonExistent", async () =>
            {
                try
                {
                    await schedulerClient.GetScheduleAsync(new GetScheduleRequest { Name = "nonexistent-schedule-xyz" });
                    throw new Exception("Expected ResourceNotFoundException but got none");
                }
                catch (ResourceNotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("scheduler", "DeleteSchedule_NonExistent", async () =>
            {
                try
                {
                    await schedulerClient.DeleteScheduleAsync(new DeleteScheduleRequest { Name = "nonexistent-schedule-xyz" });
                    throw new Exception("Expected ResourceNotFoundException but got none");
                }
                catch (ResourceNotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("scheduler", "CreateSchedule_DuplicateName", async () =>
            {
                var dupName = TestRunner.MakeUniqueName("DupSchedule");
                var dupRoleName = TestRunner.MakeUniqueName("DupSchedRole");
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = dupRoleName, AssumeRolePolicyDocument = trustPolicy }); });
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
                    await TestHelpers.SafeCleanupAsync(async () => { await schedulerClient.DeleteScheduleAsync(new DeleteScheduleRequest { Name = dupName }); });
                    await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = dupRoleName }); });
                }
            }));

            results.Add(await runner.RunTestAsync("scheduler", "UpdateSchedule_VerifyExpression", async () =>
            {
                var updName = TestRunner.MakeUniqueName("UpdSchedule");
                var updRoleName = TestRunner.MakeUniqueName("UpdSchedRole");
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = updRoleName, AssumeRolePolicyDocument = trustPolicy }); });
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
                    await TestHelpers.SafeCleanupAsync(async () => { await schedulerClient.DeleteScheduleAsync(new DeleteScheduleRequest { Name = updName }); });
                    await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = updRoleName }); });
                }
            }));

            var groupName = TestRunner.MakeUniqueName("CSGroup");

            results.Add(await runner.RunTestAsync("scheduler", "CreateScheduleGroup", async () =>
            {
                var resp = await schedulerClient.CreateScheduleGroupAsync(new CreateScheduleGroupRequest
                {
                    Name = groupName
                });
                if (resp == null || resp.ScheduleGroupArn == null)
                    throw new Exception("response or ARN is nil");
            }));

            results.Add(await runner.RunTestAsync("scheduler", "GetScheduleGroup", async () =>
            {
                var resp = await schedulerClient.GetScheduleGroupAsync(new GetScheduleGroupRequest { Name = groupName });
                if (resp.Name == null || resp.Name != groupName)
                    throw new Exception($"expected group name {groupName}, got {resp.Name}");
            }));

            results.Add(await runner.RunTestAsync("scheduler", "ListScheduleGroups", async () =>
            {
                var resp = await schedulerClient.ListScheduleGroupsAsync(new ListScheduleGroupsRequest());
                if (resp.ScheduleGroups == null)
                    throw new Exception("schedule groups list is nil");
            }));

            results.Add(await runner.RunTestAsync("scheduler", "GetScheduleGroup_NonExistent", async () =>
            {
                try
                {
                    await schedulerClient.GetScheduleGroupAsync(new GetScheduleGroupRequest { Name = "nonexistent-group-xyz" });
                    throw new Exception("Expected ResourceNotFoundException but got none");
                }
                catch (ResourceNotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("scheduler", "CreateScheduleGroup_DuplicateName", async () =>
            {
                var dupGroupName = TestRunner.MakeUniqueName("DupGroup");
                try
                {
                    await schedulerClient.CreateScheduleGroupAsync(new CreateScheduleGroupRequest { Name = dupGroupName });
                    try
                    {
                        await schedulerClient.CreateScheduleGroupAsync(new CreateScheduleGroupRequest { Name = dupGroupName });
                        throw new Exception("expected error for duplicate group name");
                    }
                    catch (ConflictException) { }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await schedulerClient.DeleteScheduleGroupAsync(new DeleteScheduleGroupRequest { Name = dupGroupName }); });
                }
            }));

            results.Add(await runner.RunTestAsync("scheduler", "ListScheduleGroups_ContainsCreated", async () =>
            {
                var resp = await schedulerClient.ListScheduleGroupsAsync(new ListScheduleGroupsRequest());
                var found = resp.ScheduleGroups.Any(g => g.Name == groupName);
                if (!found)
                    throw new Exception($"created group {groupName} not found in list");
            }));

            results.Add(await runner.RunTestAsync("scheduler", "CreateSchedule_WithGroupName", async () =>
            {
                var schedName = TestRunner.MakeUniqueName("GroupSched");
                var groupRoleName = TestRunner.MakeUniqueName("GroupSchedRole");
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = groupRoleName, AssumeRolePolicyDocument = trustPolicy }); });
                try
                {
                    var groupRoleArn = $"arn:aws:iam::000000000000:role/{groupRoleName}";
                    await schedulerClient.CreateScheduleAsync(new CreateScheduleRequest
                    {
                        Name = schedName,
                        GroupName = groupName,
                        ScheduleExpression = "rate(30 minutes)",
                        Target = new Target { Arn = $"arn:aws:lambda:{region}:000000000000:function:TestFn", RoleArn = groupRoleArn },
                        FlexibleTimeWindow = new FlexibleTimeWindow { Mode = FlexibleTimeWindowMode.OFF }
                    });
                    try
                    {
                        var getResp = await schedulerClient.GetScheduleAsync(new GetScheduleRequest
                        {
                            Name = schedName,
                            GroupName = groupName
                        });
                        if (getResp.GroupName == null || getResp.GroupName != groupName)
                            throw new Exception($"expected group name {groupName}, got {getResp.GroupName}");
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await schedulerClient.DeleteScheduleAsync(new DeleteScheduleRequest { Name = schedName, GroupName = groupName }); });
                    }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = groupRoleName }); });
                }
            }));

            results.Add(await runner.RunTestAsync("scheduler", "UpdateSchedule_NonExistent", async () =>
            {
                try
                {
                    await schedulerClient.UpdateScheduleAsync(new UpdateScheduleRequest
                    {
                        Name = "nonexistent-schedule-xyz",
                        ScheduleExpression = "rate(30 minutes)",
                        Target = new Target { Arn = $"arn:aws:lambda:{region}:000000000000:function:TestFn", RoleArn = roleArn },
                        FlexibleTimeWindow = new FlexibleTimeWindow { Mode = FlexibleTimeWindowMode.OFF }
                    });
                    throw new Exception("Expected ResourceNotFoundException but got none");
                }
                catch (ResourceNotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("scheduler", "UpdateSchedule_StateToggle", async () =>
            {
                var stateName = TestRunner.MakeUniqueName("StateSchedule");
                var stateRoleName = TestRunner.MakeUniqueName("StateSchedRole");
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = stateRoleName, AssumeRolePolicyDocument = trustPolicy }); });
                try
                {
                    var stateRoleArn = $"arn:aws:iam::000000000000:role/{stateRoleName}";
                    await schedulerClient.CreateScheduleAsync(new CreateScheduleRequest
                    {
                        Name = stateName,
                        ScheduleExpression = "rate(30 minutes)",
                        Target = new Target { Arn = $"arn:aws:lambda:{region}:000000000000:function:TestFn", RoleArn = stateRoleArn },
                        FlexibleTimeWindow = new FlexibleTimeWindow { Mode = FlexibleTimeWindowMode.OFF }
                    });
                    try
                    {
                        await schedulerClient.UpdateScheduleAsync(new UpdateScheduleRequest
                        {
                            Name = stateName,
                            ScheduleExpression = "rate(30 minutes)",
                            Target = new Target { Arn = $"arn:aws:lambda:{region}:000000000000:function:TestFn", RoleArn = stateRoleArn },
                            FlexibleTimeWindow = new FlexibleTimeWindow { Mode = FlexibleTimeWindowMode.OFF },
                            State = ScheduleState.DISABLED
                        });
                        var getResp = await schedulerClient.GetScheduleAsync(new GetScheduleRequest { Name = stateName });
                        if (getResp.State != ScheduleState.DISABLED)
                            throw new Exception($"expected DISABLED, got {getResp.State}");
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await schedulerClient.DeleteScheduleAsync(new DeleteScheduleRequest { Name = stateName }); });
                    }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = stateRoleName }); });
                }
            }));

            results.Add(await runner.RunTestAsync("scheduler", "ListSchedules_NamePrefix", async () =>
            {
                var prefixName = TestRunner.MakeUniqueName("PrefixSched");
                var prefixRoleName = TestRunner.MakeUniqueName("PrefixSchedRole");
                await TestHelpers.SafeCleanupAsync(async () => { await iamClient.CreateRoleAsync(new CreateRoleRequest { RoleName = prefixRoleName, AssumeRolePolicyDocument = trustPolicy }); });
                try
                {
                    var prefixRoleArn = $"arn:aws:iam::000000000000:role/{prefixRoleName}";
                    await schedulerClient.CreateScheduleAsync(new CreateScheduleRequest
                    {
                        Name = prefixName,
                        ScheduleExpression = "rate(30 minutes)",
                        Target = new Target { Arn = $"arn:aws:lambda:{region}:000000000000:function:TestFn", RoleArn = prefixRoleArn },
                        FlexibleTimeWindow = new FlexibleTimeWindow { Mode = FlexibleTimeWindowMode.OFF }
                    });
                    try
                    {
                        var prefix = prefixName[..^8];
                        var resp = await schedulerClient.ListSchedulesAsync(new ListSchedulesRequest { NamePrefix = prefix });
                        var found = resp.Schedules.Any(s => s.Name == prefixName);
                        if (!found)
                            throw new Exception($"schedule {prefixName} not found with prefix {prefix}");
                    }
                    finally
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await schedulerClient.DeleteScheduleAsync(new DeleteScheduleRequest { Name = prefixName }); });
                    }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = prefixRoleName }); });
                }
            }));

            results.Add(await runner.RunTestAsync("scheduler", "TagResource_ScheduleGroup", async () =>
            {
                var tagGroupName = TestRunner.MakeUniqueName("TagGroup");
                var groupResp = await schedulerClient.CreateScheduleGroupAsync(new CreateScheduleGroupRequest { Name = tagGroupName });
                try
                {
                    await schedulerClient.TagResourceAsync(new TagResourceRequest
                    {
                        ResourceArn = groupResp.ScheduleGroupArn,
                        Tags = new List<Amazon.Scheduler.Model.Tag>
                        {
                            new Amazon.Scheduler.Model.Tag { Key = "Env", Value = "prod" }
                        }
                    });
                    var tagResp = await schedulerClient.ListTagsForResourceAsync(new ListTagsForResourceRequest { ResourceArn = groupResp.ScheduleGroupArn });
                    var found = tagResp.Tags.Any(t => t.Key == "Env");
                    if (!found)
                        throw new Exception("tag Env not found on schedule group");
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await schedulerClient.DeleteScheduleGroupAsync(new DeleteScheduleGroupRequest { Name = tagGroupName }); });
                }
            }));

            results.Add(await runner.RunTestAsync("scheduler", "ListTagsForResource_ScheduleGroup", async () =>
            {
                var groupResp = await schedulerClient.GetScheduleGroupAsync(new GetScheduleGroupRequest { Name = groupName });
                await schedulerClient.ListTagsForResourceAsync(new ListTagsForResourceRequest { ResourceArn = groupResp.Arn });
            }));

            results.Add(await runner.RunTestAsync("scheduler", "UntagResource_NonExistentKey", async () =>
            {
                var scheduleArn = $"arn:aws:scheduler:{region}:000000000000:schedule/{scheduleName}";
                await schedulerClient.UntagResourceAsync(new UntagResourceRequest
                {
                    ResourceArn = scheduleArn,
                    TagKeys = new List<string> { "NonExistentKey" }
                });
            }));

            results.Add(await runner.RunTestAsync("scheduler", "CreateSchedule_InvalidExpression", async () =>
            {
                var invName = TestRunner.MakeUniqueName("InvExprSched");
                try
                {
                    await schedulerClient.CreateScheduleAsync(new CreateScheduleRequest
                    {
                        Name = invName,
                        ScheduleExpression = "not-a-valid-expression",
                        Target = new Target { Arn = $"arn:aws:lambda:{region}:000000000000:function:TestFn", RoleArn = roleArn },
                        FlexibleTimeWindow = new FlexibleTimeWindow { Mode = FlexibleTimeWindowMode.OFF }
                    });
                    throw new Exception("expected error for invalid schedule expression");
                }
                catch (AmazonSchedulerException) { }
            }));

            results.Add(await runner.RunTestAsync("scheduler", "DeleteScheduleGroup_NonExistent", async () =>
            {
                try
                {
                    await schedulerClient.DeleteScheduleGroupAsync(new DeleteScheduleGroupRequest { Name = "nonexistent-group-xyz" });
                    throw new Exception("Expected ResourceNotFoundException but got none");
                }
                catch (ResourceNotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("scheduler", "DeleteScheduleGroup", async () =>
            {
                var delGroupName = TestRunner.MakeUniqueName("DelGroup");
                await schedulerClient.CreateScheduleGroupAsync(new CreateScheduleGroupRequest { Name = delGroupName });
                await schedulerClient.DeleteScheduleGroupAsync(new DeleteScheduleGroupRequest { Name = delGroupName });
                try
                {
                    await schedulerClient.GetScheduleGroupAsync(new GetScheduleGroupRequest { Name = delGroupName });
                    throw new Exception("expected error after deleting group");
                }
                catch (ResourceNotFoundException) { }
            }));

            await TestHelpers.SafeCleanupAsync(async () => { await schedulerClient.DeleteScheduleGroupAsync(new DeleteScheduleGroupRequest { Name = groupName }); });

            results.Add(await runner.RunTestAsync("scheduler", "ListScheduleGroups_Pagination", async () =>
            {
                var pgTs = TestRunner.MakeUniqueName("PagGroup");
                var pgGroups = new List<string>();
                try
                {
                    for (int i = 0; i < 5; i++)
                    {
                        var name = $"{pgTs}-{i}";
                        await schedulerClient.CreateScheduleGroupAsync(new CreateScheduleGroupRequest { Name = name });
                        pgGroups.Add(name);
                    }

                    var allGroups = new List<string>();
                    string? nextToken = null;
                    do
                    {
                        var resp = await schedulerClient.ListScheduleGroupsAsync(new ListScheduleGroupsRequest
                        {
                            MaxResults = 2,
                            NextToken = nextToken
                        });
                        foreach (var g in resp.ScheduleGroups)
                        {
                            if (g.Name != null && g.Name.StartsWith(pgTs))
                                allGroups.Add(g.Name);
                        }
                        nextToken = resp.NextToken;
                    } while (nextToken != null);

                    if (allGroups.Count != 5)
                        throw new Exception($"expected 5 paginated schedule groups, got {allGroups.Count}");
                }
                finally
                {
                    foreach (var gn in pgGroups)
                    {
                        await TestHelpers.SafeCleanupAsync(async () => { await schedulerClient.DeleteScheduleGroupAsync(new DeleteScheduleGroupRequest { Name = gn }); });
                    }
                }
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

        return results;
    }
}
