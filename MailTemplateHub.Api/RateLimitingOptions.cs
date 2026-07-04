namespace MailTemplateHub.Api;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public PolicyOptions Auth { get; init; } = new() { PermitLimit = 10, WindowMinutes = 15 };
    public PolicyOptions Oauth { get; init; } = new() { PermitLimit = 10, WindowMinutes = 10 };
    public PolicyOptions Upload { get; init; } = new() { PermitLimit = 60, WindowMinutes = 60 };

    public sealed class PolicyOptions
    {
        public int PermitLimit { get; init; } = 10;
        public int WindowMinutes { get; init; } = 15;
    }
}
