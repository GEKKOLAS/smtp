namespace MailTemplateHub.Api;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public PolicyOptions Auth { get; init; } = new();

    public sealed class PolicyOptions
    {
        public int PermitLimit { get; init; } = 10;
        public int WindowMinutes { get; init; } = 15;
    }
}
