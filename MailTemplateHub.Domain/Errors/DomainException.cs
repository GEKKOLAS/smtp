namespace MailTemplateHub.Domain.Errors;

/// <summary>
/// Invariant violation raised by domain code. Carries a machine-readable code
/// (see <see cref="ErrorCodes"/>) that the API maps to a ProblemDetails errorCode.
/// </summary>
public class DomainException(string code, string? message = null)
    : Exception(message ?? code)
{
    public string Code { get; } = code;
}
