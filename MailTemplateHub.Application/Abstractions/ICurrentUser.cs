namespace MailTemplateHub.Application.Abstractions;

/// <summary>Identity of the authenticated caller, populated by the API auth handler.</summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    Guid SessionId { get; }
}

/// <summary>Request metadata used for audit entries.</summary>
public interface IRequestContext
{
    string? Ip { get; }
    string? UserAgent { get; }
}
