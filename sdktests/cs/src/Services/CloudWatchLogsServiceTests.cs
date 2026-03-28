using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class CloudWatchLogsServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonCloudWatchLogsClient cloudWatchLogsClient,
        string region)
    {
        var results = new List<TestResult>();
        var logGroupName = TestRunner.MakeUniqueName("CSLogGroup");
        var logStreamName = TestRunner.MakeUniqueName("CSLogStream");

        try
        {
            results.Add(await runner.RunTestAsync("logs", "CreateLogGroup", async () =>
            {
                var resp = await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest
                {
                    LogGroupName = logGroupName
                });
            }));

            results.Add(await runner.RunTestAsync("logs", "DescribeLogGroups", async () =>
            {
                var resp = await cloudWatchLogsClient.DescribeLogGroupsAsync(new DescribeLogGroupsRequest());
                if (resp.LogGroups == null)
                    throw new Exception("log groups list is nil");
            }));

            results.Add(await runner.RunTestAsync("logs", "DescribeLogStreams", async () =>
            {
                var resp = await cloudWatchLogsClient.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
                {
                    LogGroupName = logGroupName
                });
                if (resp.LogStreams == null)
                    throw new Exception("log streams list is nil");
            }));

            results.Add(await runner.RunTestAsync("logs", "CreateLogStream", async () =>
            {
                await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = logGroupName,
                    LogStreamName = logStreamName
                });
            }));

            results.Add(await runner.RunTestAsync("logs", "PutLogEvents", async () =>
            {
                await cloudWatchLogsClient.PutLogEventsAsync(new PutLogEventsRequest
                {
                    LogGroupName = logGroupName,
                    LogStreamName = logStreamName,
                    LogEvents = new List<InputLogEvent>
                    {
                        new InputLogEvent
                        {
                            Message = "Test log message",
                            Timestamp = DateTime.UtcNow
                        }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("logs", "GetLogEvents", async () =>
            {
                var resp = await cloudWatchLogsClient.GetLogEventsAsync(new GetLogEventsRequest
                {
                    LogGroupName = logGroupName,
                    LogStreamName = logStreamName
                });
                if (resp.Events == null)
                    throw new Exception("events list is nil");
            }));

            results.Add(await runner.RunTestAsync("logs", "FilterLogEvents", async () =>
            {
                var resp = await cloudWatchLogsClient.FilterLogEventsAsync(new FilterLogEventsRequest
                {
                    LogGroupName = logGroupName
                });
                if (resp.Events == null)
                    throw new Exception("events list is nil");
            }));

            results.Add(await runner.RunTestAsync("logs", "PutRetentionPolicy", async () =>
            {
                await cloudWatchLogsClient.PutRetentionPolicyAsync(new PutRetentionPolicyRequest
                {
                    LogGroupName = logGroupName,
                    RetentionInDays = 7
                });
            }));

            results.Add(await runner.RunTestAsync("logs", "DeleteLogStream", async () =>
            {
                await cloudWatchLogsClient.DeleteLogStreamAsync(new DeleteLogStreamRequest
                {
                    LogGroupName = logGroupName,
                    LogStreamName = logStreamName
                });
            }));

            results.Add(await runner.RunTestAsync("logs", "DeleteLogGroup", async () =>
            {
                await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest
                {
                    LogGroupName = logGroupName
                });
            }));
        }
        finally
        {
            try
            {
                await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest
                {
                    LogGroupName = logGroupName
                });
            }
            catch { }
        }

        results.Add(await runner.RunTestAsync("logs", "CreateLogGroup_Duplicate", async () =>
        {
            var dupGroupName = TestRunner.MakeUniqueName("DupLogGroup");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest
            {
                LogGroupName = dupGroupName
            });
            try
            {
                try
                {
                    await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest
                    {
                        LogGroupName = dupGroupName
                    });
                    throw new Exception("expected error for duplicate log group");
                }
                catch (ResourceAlreadyExistsException) { }
            }
            finally
            {
                try { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = dupGroupName }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "DeleteLogGroup_NonExistent", async () =>
        {
            try
            {
                await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest
                {
                    LogGroupName = "nonexistent-log-group-xyz"
                });
                throw new Exception("expected error for non-existent log group");
            }
            catch (ResourceNotFoundException) { }
        }));

        results.Add(await runner.RunTestAsync("logs", "PutLogEvents_GetLogEvents_Roundtrip", async () =>
        {
            var rtGroupName = TestRunner.MakeUniqueName("RTLogGroup");
            var rtStreamName = TestRunner.MakeUniqueName("RTLogStream");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = rtGroupName });
            try
            {
                await cloudWatchLogsClient.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = rtGroupName,
                    LogStreamName = rtStreamName
                });
                var testMessage = "roundtrip-log-message-verify-12345";
                await cloudWatchLogsClient.PutLogEventsAsync(new PutLogEventsRequest
                {
                    LogGroupName = rtGroupName,
                    LogStreamName = rtStreamName,
                    LogEvents = new List<InputLogEvent>
                    {
                        new InputLogEvent { Message = testMessage, Timestamp = DateTime.UtcNow }
                    }
                });
                var resp = await cloudWatchLogsClient.GetLogEventsAsync(new GetLogEventsRequest
                {
                    LogGroupName = rtGroupName,
                    LogStreamName = rtStreamName
                });
                if (resp.Events.Count == 0)
                    throw new Exception("no events returned");
                if (resp.Events[0].Message != testMessage)
                    throw new Exception($"message mismatch: got {resp.Events[0].Message}, want {testMessage}");
            }
            finally
            {
                try { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = rtGroupName }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("logs", "DescribeLogGroups_ContainsCreated", async () =>
        {
            var dlgName = TestRunner.MakeUniqueName("DLGGroup");
            await cloudWatchLogsClient.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = dlgName });
            try
            {
                var resp = await cloudWatchLogsClient.DescribeLogGroupsAsync(new DescribeLogGroupsRequest
                {
                    LogGroupNamePrefix = dlgName
                });
                if (resp.LogGroups.Count != 1)
                    throw new Exception($"expected 1 log group, got {resp.LogGroups.Count}");
                if (resp.LogGroups[0].LogGroupName != dlgName)
                    throw new Exception("log group name mismatch");
            }
            finally
            {
                try { await cloudWatchLogsClient.DeleteLogGroupAsync(new DeleteLogGroupRequest { LogGroupName = dlgName }); } catch { }
            }
        }));

        return results;
    }
}
