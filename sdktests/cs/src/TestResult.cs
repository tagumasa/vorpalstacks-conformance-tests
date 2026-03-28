namespace VorpalStacks.SDK.Tests;

public class TestResult
{
    public string Service { get; set; } = "";
    public string TestName { get; set; } = "";
    public string Status { get; set; } = "PASS";
    public string? Error { get; set; }
    public long DurationMs { get; set; }
}
