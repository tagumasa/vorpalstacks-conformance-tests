namespace VorpalStacks.SDK.Tests;

public static class TestHelpers
{
    public static async Task SafeCleanupAsync(Func<Task> cleanup)
    {
        try { await cleanup(); } catch { }
    }

    public static async Task AssertThrowsAsync<TException>(Func<Task> action, string? containsMessage = null) where TException : Exception
    {
        var ex = await AssertThrowsAsync(typeof(TException), action, containsMessage);
    }

    public static async Task<Exception> AssertThrowsAsync(Type exceptionType, Func<Task> action, string? containsMessage = null)
    {
        try
        {
            await action();
            throw new Exception($"Expected {exceptionType.Name} but no exception was thrown");
        }
        catch (Exception ex) when (exceptionType.IsAssignableFrom(ex.GetType()))
        {
            if (containsMessage != null && !ex.Message.Contains(containsMessage))
                throw new Exception($"Expected error message to contain \"{containsMessage}\" but got: {ex.Message}");
            return ex;
        }
    }

    public static async Task AssertThrowsAnyAsync(Func<Task> action, string? containsMessage = null)
    {
        try
        {
            await action();
            throw new Exception("Expected an exception but none was thrown");
        }
        catch (Exception ex) when (containsMessage == null || ex.Message.Contains(containsMessage))
        {
        }
    }

    public static async Task<List<TestResult>> RunTagTestsAsync(
        TestRunner runner,
        string service,
        string resourceArn,
        Func<string, string, Task> tagResourceAsync,
        Func<string, Task> untagResourceAsync,
        Func<Task> listAndVerifyTagsAsync)
    {
        var results = new List<TestResult>();

        results.Add(await runner.RunTestAsync(service, "TagResource", async () =>
        {
            await tagResourceAsync("env", "test");
            await tagResourceAsync("team", "conformance");
        }));

        results.Add(await runner.RunTestAsync(service, "ListTagsOfResource", async () =>
        {
            await listAndVerifyTagsAsync();
        }));

        results.Add(await runner.RunTestAsync(service, "UntagResource", async () =>
        {
            await untagResourceAsync("env");
        }));

        results.Add(await runner.RunTestAsync(service, "ListTagsOfResource_AfterUntag", async () =>
        {
            await listAndVerifyTagsAsync();
        }));

        return results;
    }

    public static string MakeArn(string service, string region, string resourceType, string resourceName)
    {
        return $"arn:aws:{service}:{region}:000000000000:{resourceType}/{resourceName}";
    }
}
