using System.Linq;
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
                    await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = tagTrail }); });
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
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = verifyName }); });
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
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = utName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "ListTags", async () =>
        {
            var ltName = TestRunner.MakeUniqueName("listtag-trail");
            CreateTrailResponse createResp;
            try
            {
                createResp = await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = ltName,
                    S3BucketName = "listtag-bucket",
                    IncludeGlobalServiceEvents = true,
                    IsMultiRegionTrail = false
                });
                await cloudTrailClient.AddTagsAsync(new AddTagsRequest
                {
                    ResourceId = createResp.TrailARN,
                    TagsList = new List<Tag>
                    {
                        new Tag { Key = "ListTest", Value = "value1" }
                    }
                });
                var resp = await cloudTrailClient.ListTagsAsync(new ListTagsRequest
                {
                    ResourceIdList = new List<string> { createResp.TrailARN }
                });
                if (resp.ResourceTagList == null)
                    throw new Exception("resource tag list is nil");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = ltName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "RemoveTags", async () =>
        {
            var rtName = TestRunner.MakeUniqueName("removetag-trail");
            CreateTrailResponse createResp;
            try
            {
                createResp = await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = rtName,
                    S3BucketName = "removetag-bucket",
                    IncludeGlobalServiceEvents = true,
                    IsMultiRegionTrail = false
                });
                await cloudTrailClient.AddTagsAsync(new AddTagsRequest
                {
                    ResourceId = createResp.TrailARN,
                    TagsList = new List<Tag>
                    {
                        new Tag { Key = "ToRemove", Value = "value1" },
                        new Tag { Key = "ToKeep", Value = "value2" }
                    }
                });
                await cloudTrailClient.RemoveTagsAsync(new RemoveTagsRequest
                {
                    ResourceId = createResp.TrailARN,
                    TagsList = new List<Tag> { new Tag { Key = "ToRemove" } }
                });
                var resp = await cloudTrailClient.ListTagsAsync(new ListTagsRequest
                {
                    ResourceIdList = new List<string> { createResp.TrailARN }
                });
                if (resp.ResourceTagList == null || resp.ResourceTagList.Count == 0)
                    throw new Exception("resource tag list is nil or empty");
                if (resp.ResourceTagList[0].TagsList.Any(t => t.Key == "ToRemove"))
                    throw new Exception("tag should have been removed");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = rtName }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "CreateTrail_DefaultFields", async () =>
        {
            var name = TestRunner.MakeUniqueName("defaults");
            try
            {
                var resp = await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "defaults-bucket"
                });
                if (resp.IncludeGlobalServiceEvents != true)
                    throw new Exception("IncludeGlobalServiceEvents should default to true");
                if (resp.IsMultiRegionTrail == true)
                    throw new Exception("IsMultiRegionTrail should default to false");
                if (resp.LogFileValidationEnabled == true)
                    throw new Exception("LogFileValidationEnabled should default to false");
                if (resp.IsOrganizationTrail == true)
                    throw new Exception("IsOrganizationTrail should default to false");
                if (string.IsNullOrEmpty(resp.TrailARN))
                    throw new Exception("TrailARN should be set");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "CreateTrail_Duplicate", async () =>
        {
            var name = TestRunner.MakeUniqueName("dup-trail");
            try
            {
                await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "dup-bucket"
                });
                try
                {
                    await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                    {
                        Name = name,
                        S3BucketName = "dup-bucket"
                    });
                    throw new Exception("expected TrailAlreadyExistsException for duplicate trail");
                }
                catch (AmazonCloudTrailException) { }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "GetTrail_ByARN", async () =>
        {
            var name = TestRunner.MakeUniqueName("arn-trail");
            try
            {
                var createResp = await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "arn-bucket"
                });
                if (string.IsNullOrEmpty(createResp.TrailARN))
                    throw new Exception("trail ARN is nil");
                var getResp = await cloudTrailClient.GetTrailAsync(new GetTrailRequest
                {
                    Name = createResp.TrailARN
                });
                if (getResp.Trail == null || getResp.Trail.Name != name)
                    throw new Exception("trail name mismatch after get by ARN");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "DescribeTrails_ByARN", async () =>
        {
            var name = TestRunner.MakeUniqueName("desc-arn");
            try
            {
                var createResp = await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "desc-arn-bucket"
                });
                var resp = await cloudTrailClient.DescribeTrailsAsync(new DescribeTrailsRequest
                {
                    TrailNameList = new List<string> { createResp.TrailARN }
                });
                if (resp.TrailList == null || resp.TrailList.Count != 1)
                    throw new Exception($"expected 1 trail, got {resp.TrailList?.Count ?? 0}");
                if (resp.TrailList[0].Name != name)
                    throw new Exception("trail name mismatch");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "DescribeTrails_ListAll", async () =>
        {
            var resp = await cloudTrailClient.DescribeTrailsAsync(new DescribeTrailsRequest());
            if (resp.TrailList == null)
                throw new Exception("trail list is nil");
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "GetTrailStatus_AfterStart", async () =>
        {
            var name = TestRunner.MakeUniqueName("status-start");
            try
            {
                await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "status-bucket"
                });
                await cloudTrailClient.StartLoggingAsync(new StartLoggingRequest { Name = name });
                var status = await cloudTrailClient.GetTrailStatusAsync(new GetTrailStatusRequest { Name = name });
                if (status.IsLogging != true)
                    throw new Exception("expected IsLogging=true after StartLogging");
                if (!status.StartLoggingTime.HasValue)
                    throw new Exception("expected StartLoggingTime to be set");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "GetTrailStatus_AfterStop", async () =>
        {
            var name = TestRunner.MakeUniqueName("status-stop");
            try
            {
                await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "stopstat-bucket"
                });
                await cloudTrailClient.StartLoggingAsync(new StartLoggingRequest { Name = name });
                await cloudTrailClient.StopLoggingAsync(new StopLoggingRequest { Name = name });
                var status = await cloudTrailClient.GetTrailStatusAsync(new GetTrailStatusRequest { Name = name });
                if (status.IsLogging == true)
                    throw new Exception("expected IsLogging=false after StopLogging");
                if (!status.StopLoggingTime.HasValue)
                    throw new Exception("expected StopLoggingTime to be set");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "UpdateTrail_EnableLogFileValidation", async () =>
        {
            var name = TestRunner.MakeUniqueName("lfv");
            try
            {
                await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "lfv-bucket"
                });
                var resp = await cloudTrailClient.UpdateTrailAsync(new UpdateTrailRequest
                {
                    Name = name,
                    EnableLogFileValidation = true
                });
                if (resp.LogFileValidationEnabled != true)
                    throw new Exception("expected LogFileValidationEnabled=true");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "CreateTrail_WithLogFileValidation", async () =>
        {
            var name = TestRunner.MakeUniqueName("lfv-create");
            try
            {
                var resp = await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "lfv-create-bucket",
                    EnableLogFileValidation = true,
                    IncludeGlobalServiceEvents = true,
                    IsMultiRegionTrail = true
                });
                if (resp.LogFileValidationEnabled != true)
                    throw new Exception("expected LogFileValidationEnabled=true");
                if (resp.IncludeGlobalServiceEvents != true)
                    throw new Exception("expected IncludeGlobalServiceEvents=true");
                if (resp.IsMultiRegionTrail != true)
                    throw new Exception("expected IsMultiRegionTrail=true");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "ListPublicKeys", async () =>
        {
            var resp = await cloudTrailClient.ListPublicKeysAsync(new ListPublicKeysRequest());
            if (resp.PublicKeyList == null)
                throw new Exception("PublicKeyList is nil");
            if (resp.PublicKeyList.Count == 0)
                throw new Exception("expected at least 1 public key");
            var pk = resp.PublicKeyList[0];
            if (string.IsNullOrEmpty(pk.Fingerprint))
                throw new Exception("expected non-empty Fingerprint");
            if (pk.Value == null || pk.Value.Length == 0)
                throw new Exception("expected non-empty Value (DER bytes)");
            if (!pk.ValidityStartTime.HasValue || !pk.ValidityEndTime.HasValue)
                throw new Exception("expected ValidityStartTime and ValidityEndTime to be set");
            if (pk.ValidityEndTime <= pk.ValidityStartTime)
                throw new Exception("ValidityEndTime should be after ValidityStartTime");
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "ListPublicKeys_TimeFilter", async () =>
        {
            var now = DateTime.UtcNow;
            var past = now.AddHours(-1);
            var future = now.AddHours(1);
            var resp = await cloudTrailClient.ListPublicKeysAsync(new ListPublicKeysRequest
            {
                StartTime = past,
                EndTime = future
            });
            if (resp.PublicKeyList == null)
                throw new Exception("PublicKeyList is nil");
            if (resp.PublicKeyList.Count == 0)
                throw new Exception("expected at least 1 public key in time range");
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "ListPublicKeys_OutsideTimeRange", async () =>
        {
            var farFuture = DateTime.UtcNow.AddYears(10);
            var beyond = farFuture.AddHours(1);
            var resp = await cloudTrailClient.ListPublicKeysAsync(new ListPublicKeysRequest
            {
                StartTime = farFuture,
                EndTime = beyond
            });
            if (resp.PublicKeyList == null)
                throw new Exception("PublicKeyList is nil");
            if (resp.PublicKeyList.Count != 0)
                throw new Exception($"expected 0 public keys outside validity range, got {resp.PublicKeyList.Count}");
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "PutEventSelectors_ExcludeManagementEventSources", async () =>
        {
            var name = TestRunner.MakeUniqueName("emes");
            try
            {
                await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "emes-bucket"
                });
                await cloudTrailClient.PutEventSelectorsAsync(new PutEventSelectorsRequest
                {
                    TrailName = name,
                    EventSelectors = new List<EventSelector>
                    {
                        new EventSelector
                        {
                            ReadWriteType = ReadWriteType.All,
                            IncludeManagementEvents = true,
                            ExcludeManagementEventSources = new List<string> { "kms.amazonaws.com" }
                        }
                    }
                });
                var resp = await cloudTrailClient.GetEventSelectorsAsync(new GetEventSelectorsRequest { TrailName = name });
                if (resp.EventSelectors == null || resp.EventSelectors.Count != 1)
                    throw new Exception($"expected 1 selector, got {resp.EventSelectors?.Count ?? 0}");
                if (resp.EventSelectors[0].ExcludeManagementEventSources == null ||
                    resp.EventSelectors[0].ExcludeManagementEventSources.Count != 1)
                    throw new Exception("expected 1 ExcludeManagementEventSource");
                if (resp.EventSelectors[0].ExcludeManagementEventSources[0] != "kms.amazonaws.com")
                    throw new Exception($"expected kms.amazonaws.com, got {resp.EventSelectors[0].ExcludeManagementEventSources[0]}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "GetEventSelectors_DefaultValues", async () =>
        {
            var name = TestRunner.MakeUniqueName("def-es");
            try
            {
                await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "def-es-bucket"
                });
                var resp = await cloudTrailClient.GetEventSelectorsAsync(new GetEventSelectorsRequest { TrailName = name });
                if (resp.EventSelectors == null || resp.EventSelectors.Count != 1)
                    throw new Exception($"expected 1 default event selector, got {resp.EventSelectors?.Count ?? 0}");
                var es = resp.EventSelectors[0];
                if (es.ReadWriteType != ReadWriteType.All)
                    throw new Exception($"expected default ReadWriteType=All, got {es.ReadWriteType}");
                if (es.IncludeManagementEvents != true)
                    throw new Exception("expected default IncludeManagementEvents=true");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "PutInsightSelectors", async () =>
        {
            var name = TestRunner.MakeUniqueName("insight");
            try
            {
                await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "insight-bucket"
                });
                var resp = await cloudTrailClient.PutInsightSelectorsAsync(new PutInsightSelectorsRequest
                {
                    TrailName = name,
                    InsightSelectors = new List<InsightSelector>
                    {
                        new InsightSelector
                        {
                            InsightType = InsightType.ApiCallRateInsight
                        }
                    }
                });
                if (resp.InsightSelectors == null || resp.InsightSelectors.Count != 1)
                    throw new Exception("expected 1 insight selector in response");
                if (resp.InsightSelectors[0].InsightType != InsightType.ApiCallRateInsight)
                    throw new Exception("insight type mismatch");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "GetInsightSelectors", async () =>
        {
            var name = TestRunner.MakeUniqueName("get-insight");
            try
            {
                await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "get-insight-bucket"
                });
                await cloudTrailClient.PutInsightSelectorsAsync(new PutInsightSelectorsRequest
                {
                    TrailName = name,
                    InsightSelectors = new List<InsightSelector>
                    {
                        new InsightSelector
                        {
                            InsightType = InsightType.ApiErrorRateInsight
                        }
                    }
                });
                var resp = await cloudTrailClient.GetInsightSelectorsAsync(new GetInsightSelectorsRequest { TrailName = name });
                if (resp.InsightSelectors == null || resp.InsightSelectors.Count != 1)
                    throw new Exception($"expected 1 insight selector, got {resp.InsightSelectors?.Count ?? 0}");
                if (resp.InsightSelectors[0].InsightType != InsightType.ApiErrorRateInsight)
                    throw new Exception($"expected ApiErrorRateInsight, got {resp.InsightSelectors[0].InsightType}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "GetInsightSelectors_Empty", async () =>
        {
            var name = TestRunner.MakeUniqueName("empty-insight");
            try
            {
                await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "empty-insight-bucket"
                });
                var resp = await cloudTrailClient.GetInsightSelectorsAsync(new GetInsightSelectorsRequest { TrailName = name });
                if (resp.InsightSelectors != null && resp.InsightSelectors.Count != 0)
                    throw new Exception($"expected 0 insight selectors for new trail, got {resp.InsightSelectors.Count}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "PutResourcePolicy_GetResourcePolicy", async () =>
        {
            var name = TestRunner.MakeUniqueName("policy");
            try
            {
                var createResp = await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "policy-bucket"
                });
                var policyDoc = "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":\"*\",\"Action\":\"cloudtrail:GetTrail\",\"Resource\":\"*\"}]}";
                var putResp = await cloudTrailClient.PutResourcePolicyAsync(new PutResourcePolicyRequest
                {
                    ResourceArn = createResp.TrailARN,
                    ResourcePolicy = policyDoc
                });
                if (putResp.ResourceArn != createResp.TrailARN)
                    throw new Exception("resource ARN mismatch in put response");
                var getResp = await cloudTrailClient.GetResourcePolicyAsync(new GetResourcePolicyRequest
                {
                    ResourceArn = createResp.TrailARN
                });
                if (getResp.ResourcePolicy != policyDoc)
                    throw new Exception($"policy content mismatch, got: {getResp.ResourcePolicy}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "GetResourcePolicy_NotFound", async () =>
        {
            var name = TestRunner.MakeUniqueName("nopolicy");
            try
            {
                var createResp = await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "nopolicy-bucket"
                });
                var resp = await cloudTrailClient.GetResourcePolicyAsync(new GetResourcePolicyRequest
                {
                    ResourceArn = createResp.TrailARN
                });
                if (!string.IsNullOrEmpty(resp.ResourcePolicy))
                    throw new Exception($"expected empty policy, got: {resp.ResourcePolicy}");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "DeleteResourcePolicy", async () =>
        {
            var name = TestRunner.MakeUniqueName("delpolicy");
            try
            {
                var createResp = await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "delpolicy-bucket"
                });
                var policyDoc = "{\"Version\":\"2012-10-17\",\"Statement\":[]}";
                await cloudTrailClient.PutResourcePolicyAsync(new PutResourcePolicyRequest
                {
                    ResourceArn = createResp.TrailARN,
                    ResourcePolicy = policyDoc
                });
                await cloudTrailClient.DeleteResourcePolicyAsync(new DeleteResourcePolicyRequest
                {
                    ResourceArn = createResp.TrailARN
                });
                var resp = await cloudTrailClient.GetResourcePolicyAsync(new GetResourcePolicyRequest
                {
                    ResourceArn = createResp.TrailARN
                });
                if (!string.IsNullOrEmpty(resp.ResourcePolicy))
                    throw new Exception("expected empty policy after delete");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "PutResourcePolicy_NonExistentTrail", async () =>
        {
            var fakeARN = "arn:aws:cloudtrail:us-east-1:123456789012:trail/nonexistent-policy-trail";
            try
            {
                await cloudTrailClient.PutResourcePolicyAsync(new PutResourcePolicyRequest
                {
                    ResourceArn = fakeARN,
                    ResourcePolicy = "{\"Version\":\"2012-10-17\"}"
                });
                throw new Exception("expected TrailNotFoundException for non-existent trail");
            }
            catch (AmazonCloudTrailException) { }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "Tags_Lifecycle", async () =>
        {
            var name = TestRunner.MakeUniqueName("tagcycle");
            try
            {
                var createResp = await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "tagcycle-bucket"
                });
                await cloudTrailClient.AddTagsAsync(new AddTagsRequest
                {
                    ResourceId = createResp.TrailARN,
                    TagsList = new List<Tag>
                    {
                        new Tag { Key = "Key1", Value = "Value1" },
                        new Tag { Key = "Key2", Value = "Value2" },
                        new Tag { Key = "Key3", Value = "Value3" }
                    }
                });
                var listResp = await cloudTrailClient.ListTagsAsync(new ListTagsRequest
                {
                    ResourceIdList = new List<string> { createResp.TrailARN }
                });
                if (listResp.ResourceTagList == null || listResp.ResourceTagList.Count == 0 ||
                    listResp.ResourceTagList[0].TagsList == null || listResp.ResourceTagList[0].TagsList.Count != 3)
                    throw new Exception("expected 3 tags");
                await cloudTrailClient.RemoveTagsAsync(new RemoveTagsRequest
                {
                    ResourceId = createResp.TrailARN,
                    TagsList = new List<Tag> { new Tag { Key = "Key2" } }
                });
                var listResp2 = await cloudTrailClient.ListTagsAsync(new ListTagsRequest
                {
                    ResourceIdList = new List<string> { createResp.TrailARN }
                });
                if (listResp2.ResourceTagList[0].TagsList.Count != 2)
                    throw new Exception($"expected 2 tags after remove, got {listResp2.ResourceTagList[0].TagsList.Count}");
                await cloudTrailClient.AddTagsAsync(new AddTagsRequest
                {
                    ResourceId = createResp.TrailARN,
                    TagsList = new List<Tag> { new Tag { Key = "Key1", Value = "UpdatedValue1" } }
                });
                var listResp3 = await cloudTrailClient.ListTagsAsync(new ListTagsRequest
                {
                    ResourceIdList = new List<string> { createResp.TrailARN }
                });
                var found = listResp3.ResourceTagList[0].TagsList.Any(t => t.Key == "Key1" && t.Value == "UpdatedValue1");
                if (!found)
                    throw new Exception("expected Key1=UpdatedValue1 after update");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "CreateTrail_WithTags", async () =>
        {
            var name = TestRunner.MakeUniqueName("tagtrail");
            try
            {
                await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "tagtrail-bucket",
                    TagsList = new List<Tag>
                    {
                        new Tag { Key = "CreatedBy", Value = "sdk-test" },
                        new Tag { Key = "Env", Value = "test" }
                    }
                });
                var resp = await cloudTrailClient.GetTrailAsync(new GetTrailRequest { Name = name });
                var listResp = await cloudTrailClient.ListTagsAsync(new ListTagsRequest
                {
                    ResourceIdList = new List<string> { resp.Trail.TrailARN }
                });
                if (listResp.ResourceTagList == null || listResp.ResourceTagList.Count == 0 ||
                    listResp.ResourceTagList[0].TagsList == null || listResp.ResourceTagList[0].TagsList.Count != 2)
                    throw new Exception("expected 2 tags");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "LookupEvents_WithTimeRange", async () =>
        {
            var now = DateTime.UtcNow;
            var pastHour = now.AddHours(-1);
            var resp = await cloudTrailClient.LookupEventsAsync(new LookupEventsRequest
            {
                StartTime = pastHour,
                EndTime = now,
                MaxResults = 5
            });
            if (resp.Events == null)
                throw new Exception("events list is nil");
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "StopLogging_NonExistent", async () =>
        {
            try
            {
                await cloudTrailClient.StopLoggingAsync(new StopLoggingRequest { Name = "nonexistent-stop-xyz" });
                throw new Exception("expected TrailNotFoundException for non-existent trail");
            }
            catch (AmazonCloudTrailException) { }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "GetTrailStatus_NonExistent", async () =>
        {
            try
            {
                await cloudTrailClient.GetTrailStatusAsync(new GetTrailStatusRequest { Name = "nonexistent-status-xyz" });
                throw new Exception("expected TrailNotFoundException for non-existent trail");
            }
            catch (AmazonCloudTrailException) { }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "UpdateTrail_NonExistent", async () =>
        {
            try
            {
                await cloudTrailClient.UpdateTrailAsync(new UpdateTrailRequest
                {
                    Name = "nonexistent-update-xyz",
                    S3BucketName = "some-bucket"
                });
                throw new Exception("expected TrailNotFoundException for non-existent trail");
            }
            catch (AmazonCloudTrailException) { }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "GetEventSelectors_NonExistent", async () =>
        {
            try
            {
                await cloudTrailClient.GetEventSelectorsAsync(new GetEventSelectorsRequest { TrailName = "nonexistent-es-xyz" });
                throw new Exception("expected TrailNotFoundException for non-existent trail");
            }
            catch (AmazonCloudTrailException) { }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "PutEventSelectors_NonExistent", async () =>
        {
            try
            {
                await cloudTrailClient.PutEventSelectorsAsync(new PutEventSelectorsRequest
                {
                    TrailName = "nonexistent-es-xyz",
                    EventSelectors = new List<EventSelector>
                    {
                        new EventSelector { ReadWriteType = ReadWriteType.All }
                    }
                });
                throw new Exception("expected TrailNotFoundException for non-existent trail");
            }
            catch (AmazonCloudTrailException) { }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "PutInsightSelectors_NonExistent", async () =>
        {
            try
            {
                await cloudTrailClient.PutInsightSelectorsAsync(new PutInsightSelectorsRequest
                {
                    TrailName = "nonexistent-is-xyz",
                    InsightSelectors = new List<InsightSelector>
                    {
                        new InsightSelector { InsightType = InsightType.ApiCallRateInsight }
                    }
                });
                throw new Exception("expected TrailNotFoundException for non-existent trail");
            }
            catch (AmazonCloudTrailException) { }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "GetInsightSelectors_NonExistent", async () =>
        {
            try
            {
                await cloudTrailClient.GetInsightSelectorsAsync(new GetInsightSelectorsRequest { TrailName = "nonexistent-is-xyz" });
                throw new Exception("expected TrailNotFoundException for non-existent trail");
            }
            catch (AmazonCloudTrailException) { }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "AddTags_NonExistent", async () =>
        {
            try
            {
                await cloudTrailClient.AddTagsAsync(new AddTagsRequest
                {
                    ResourceId = "arn:aws:cloudtrail:us-east-1:123456789012:trail/nonexistent-tag-xyz",
                    TagsList = new List<Tag> { new Tag { Key = "K", Value = "V" } }
                });
                throw new Exception("expected TrailNotFoundException for non-existent trail");
            }
            catch (AmazonCloudTrailException) { }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "RemoveTags_NonExistent", async () =>
        {
            try
            {
                await cloudTrailClient.RemoveTagsAsync(new RemoveTagsRequest
                {
                    ResourceId = "arn:aws:cloudtrail:us-east-1:123456789012:trail/nonexistent-rm-xyz",
                    TagsList = new List<Tag> { new Tag { Key = "K" } }
                });
                throw new Exception("expected TrailNotFoundException for non-existent trail");
            }
            catch (AmazonCloudTrailException) { }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "ListTags_NonExistent", async () =>
        {
            try
            {
                await cloudTrailClient.ListTagsAsync(new ListTagsRequest
                {
                    ResourceIdList = new List<string> { "arn:aws:cloudtrail:us-east-1:123456789012:trail/nonexistent-lt-xyz" }
                });
                throw new Exception("expected TrailNotFoundException for non-existent trail");
            }
            catch (AmazonCloudTrailException) { }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "GetResourcePolicy_NonExistentTrail", async () =>
        {
            var fakeARN = "arn:aws:cloudtrail:us-east-1:123456789012:trail/nonexistent-grp-xyz";
            try
            {
                await cloudTrailClient.GetResourcePolicyAsync(new GetResourcePolicyRequest
                {
                    ResourceArn = fakeARN
                });
                throw new Exception("expected TrailNotFoundException for non-existent trail");
            }
            catch (AmazonCloudTrailException) { }
        }));

        results.Add(await runner.RunTestAsync("cloudtrail", "GetTrailStatus_DefaultIsNotLogging", async () =>
        {
            var name = TestRunner.MakeUniqueName("islog");
            try
            {
                await cloudTrailClient.CreateTrailAsync(new CreateTrailRequest
                {
                    Name = name,
                    S3BucketName = "islog-bucket"
                });
                var status = await cloudTrailClient.GetTrailStatusAsync(new GetTrailStatusRequest { Name = name });
                if (status.IsLogging == true)
                    throw new Exception("expected IsLogging=false for newly created trail");
                if (status.LatestDeliveryTime.HasValue)
                    throw new Exception("expected nil LatestDeliveryTime when not logging");
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await cloudTrailClient.DeleteTrailAsync(new DeleteTrailRequest { Name = name }); });
            }
        }));

        return results;
    }
}
