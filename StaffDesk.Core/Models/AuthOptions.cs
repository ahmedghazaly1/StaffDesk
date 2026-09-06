namespace StaffDesk.Core.Models;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;
    public int LockoutAfterFailures { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int DelayBaseMs { get; set; } = 200;
    public int DelayMaxMs { get; set; } = 4000;
}
