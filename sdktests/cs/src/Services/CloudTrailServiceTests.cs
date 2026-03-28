using Amazon;
using Amazon.CloudTrail;
using Amazon.CloudTrail.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class CloudTrailServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonCloudTrailClient cloudTrailClient,
        string region)
    {
        var results = new List<TestResult>();
        var trailName = TestRunner.MakeUniqueName("CSTrail");

        try
        {
            results.Add(await runner.RunTestAsync("cloudtrail", "ListTrails", async () =>
            {
                var resp = await cloudTrailClient.ListTrailsAsync(new ListTrailsRequest());
                if (resp.Trails == null)
                    throw new Exception("Trails is null");
            }));

            results.Add(await runner.RunTestAsync("cloudtrail", "LookupEvents", async () =>
            {
                var resp = await cloudTrailClient.LookupEventsAsync(new LookupEventsRequest
                {
                    LookupAttributes = new List<LookupAttribute>
                    {
                        new LookupAttribute
                        {
                            AttributeKey = "EventSource",
                            AttributeValue = "s3.amazonaws.com"
                        }
                    }
                });
                if (resp.Events == null)
                    throw new Exception("Events is null");
            }));

            results.Add(await runner.RunTestAsync("cloudtrail", "DescribeTrails", async () =>
            {
                var resp = await cloudTrailClient.DescribeTrailsAsync(new DescribeTrailsRequest());
                if (resp.TrailList == null)
                    throw new Exception("TrailList is null");
            }));

            results.Add(await runner.RunTestAsync("cloudtrail", "CreateTrail", async () =>
            {
                var resp = await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = trailName,
                    S3BucketName = "test-bucket",
                    IncludeGlobalServiceEvents = true,
                    IsMultiRegionTrail = true
                });
                if (string.IsNullOrEmpty(resp.Name))
                    throw new Exception("trail name is nil");
            }));

            results.Add(await runner.RunTestAsync("cloudtrail", "GetTrail", async () =>
            {
                var resp = await cloudTrailClient.GetTrailAsync(new GetTrailRequest
                {
                    Name = trailName
                });
                if (resp.Trail == null)
                    throw new Exception("trail is nil");
            }));

            results.Add(await runner.RunTestAsync("cloudtrail", "DescribeTrails_TrailName", async () =>
            {
                var resp = await cloudTrailClient.DescribeTrailsAsync(new DescribeTrailsRequest
                {
                    TrailNameList = new List<string> { trailName }
                });
                if (resp.TrailList == null)
                    throw new Exception("trail list is nil");
            }));

            results.Add(await runner.RunTestAsync("cloudtrail", "StartLogging", async () =>
            {
                await cloudTrailClient.StartLoggingAsync(new StartLoggingRequest
                {
                    Name = trailName
                });
            }));

            results.Add(await runner.RunTestAsync("cloudtrail", "StopLogging", async () =>
            {
                await cloudTrailClient.StopLoggingAsync(new StopLoggingRequest
                {
                    Name = trailName
                });
            }));

            results.Add(await runner.RunTestAsync("cloudtrail", "GetTrailStatus", async () =>
            {
                await cloudTrailClient.GetTrailStatusAsync(new GetTrailStatusRequest
                {
                    Name = trailName
                });
            }));

            results.Add(await runner.RunTestAsync("cloudtrail", "UpdateTrail", async () =>
            {
                await cloudTrailClient.UpdateTrailAsync(new UpdateTrailRequest
                {
                    Name = trailName,
                    S3BucketName = "updated-bucket"
                });
            }));

            results.Add(await runner.RunTestAsync("cloudtrail", "GetEventSelectors", async () =>
            {
                var resp = await cloudTrailClient.GetEventSelectorsAsync(new GetEventSelectorsRequest
                {
                    TrailName = trailName
                });
                if (resp.EventSelectors == null)
                    throw new Exception("event selectors list is nil");
            }));

            results.Add(await runner.RunTestAsync("cloudtrail", "PutEventSelectors", async () =>
            {
                await cloudTrailClient.PutEventSelectorsAsync(new PutEventSelectorsRequest
                {
                    TrailName = trailName,
                    EventSelectors = new List<EventSelector>
                    {
                        new EventSelector
                        {
                            ReadWriteType = ReadWriteType.All,
                            IncludeManagementEvents = true
                        }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("cloudtrail", "AddTags", async () =>
            {
                var tagTrail = TestRunner.MakeUniqueName("tag-trail");
                var createResp = await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = tagTrail,
                    S3BucketName = "test-bucket",
                    IncludeGlobalServiceEvents = true,
                    IsMultiRegionTrail = false
                });
                var tagTrailARN = createResp.TrailARN ?? tagTrail;
                try
                {
                    await cloudTrailClient.AddTagsAsync(new AddTagsRequest
                    {
                        ResourceId = tagTrailARN,
                        TagsList = new List<Tag>
                        {
                            new Tag { Key = "Environment", Value = "test" },
                            new Tag { Key = "Owner", Value = "test-user" }
                        }
                    });
                    var listResp = await cloudTrailClient.ListTagsAsync(new ListTagsRequest
                    {
                        ResourceIdList = new List<string> { tagTrailARN }
                    });
                    if (listResp.ResourceTagList == null)
                        throw new Exception("resource tag list is nil");
                    await cloudTrailClient.RemoveTagsAsync(new RemoveTagsRequest
                    {
                        ResourceId = tagTrailARN,
                        TagsList = new List<Tag> { new Tag { Key = "Environment" } }
                    });
                }
                finally
                {
                    try { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = tagTrail }); } catch { }
                }
            }));

            results.Add(await runner.RunTestAsync("cloudtrail", "DeleteTrail", async () =>
            {
                await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest
                {
                    Name = trailName
                });
            }));
        }
        finally
        {
            try
            {
                await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = trailName });
            }
            catch { }
        }

        results.Add(await runner.RunTestAsync("cloudtrail", "DescribeTrails_TrailNotFound", async () =>
        {
            var resp = await cloudTrailClient.DescribeTrailsAsync(new DescribeTrailsRequest
            {
                TrailNameList = new List<string> { "NonExistentTrail_xyz_12345" }
            });
            if (resp.TrailList != null && resp.TrailList.Count > 0)
                throw new Exception("Expected empty trail list for non-existent trail");
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "GetTrail_NonExistent", async () =>
        {
            try
            {
                await cloudTrailClient.GetTrailAsync(new GetTrailRequest { Name = "nonexistent-trail-xyz" });
                throw new Exception("expected error for non-existent trail");
            }
            catch (AmazonCloudTrailException) { }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "DeleteTrail_NonExistent", async () =>
        {
            try
            {
                await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = "nonexistent-trail-xyz" });
                throw new Exception("expected error for non-existent trail");
            }
            catch (AmazonCloudTrailException) { }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "StartLogging_NonExistent", async () =>
        {
            try
            {
                await cloudTrailClient.StartLoggingAsync(new StartLoggingRequest { Name = "nonexistent-trail-xyz" });
                throw new Exception("expected error for non-existent trail");
            }
            catch (AmazonCloudTrailException) { }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "CreateTrail_ContentVerify", async () =>
        {
            var verifyName = TestRunner.MakeUniqueName("verify-trail");
            await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
            {
                Name = verifyName,
                S3BucketName = "verify-bucket",
                IncludeGlobalServiceEvents = true,
                IsMultiRegionTrail = false
            });
            try
            {
                var resp = await cloudTrailClient.GetTrailAsync(new GetTrailRequest { Name = verifyName });
                if (resp.Trail == null || resp.Trail.S3BucketName != "verify-bucket")
                    throw new Exception("S3 bucket name mismatch");
            }
            finally
            {
                try { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = verifyName }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "UpdateTrail_VerifyChange", async () =>
        {
            var utName = TestRunner.MakeUniqueName("update-trail");
            await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
            {
                Name = utName,
                S3BucketName = "original-bucket",
                IncludeGlobalServiceEvents = true,
                IsMultiRegionTrail = true
            });
            try
            {
                await cloudTrailClient.UpdateTrailAsync(new UpdateTrailRequest
                {
                    Name = utName,
                    S3BucketName = "updated-verify-bucket"
                });
                var resp = await cloudTrailClient.GetTrailAsync(new GetTrailRequest { Name = utName });
                if (resp.Trail == null || resp.Trail.S3BucketName != "updated-verify-bucket")
                    throw new Exception($"S3 bucket name not updated, got {resp.Trail?.S3BucketName}");
            }
            finally
            {
                try { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = utName }); } catch { }
            }
        }));

        return results;
    }
}
