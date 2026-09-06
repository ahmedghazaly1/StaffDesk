namespace StaffDesk.Core.Models;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";

    public int AuthPerMinute { get; set; } = 10;
    public int ExportPerMinute { get; set; } = 5;
    public int WritePerMinute { get; set; } = 100;
    public int ReadPerMinute { get; set; } = 300;
}
