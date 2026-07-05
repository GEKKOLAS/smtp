namespace MailTemplateHub.Application.Common;

/// <summary>Base for use-case failures mapped to ProblemDetails by the API layer.</summary>
public abstract class AppException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Missing or not owned by the caller — always surfaced as 404 (spec 04 §5).</summary>
public sealed class NotFoundException(string code = "not_found", string message = "Resource not found.")
    : AppException(code, message);

public sealed class UnauthorizedAppException(string code, string message)
    : AppException(code, message);

public class ConflictException(string code, string message)
    : AppException(code, message);
