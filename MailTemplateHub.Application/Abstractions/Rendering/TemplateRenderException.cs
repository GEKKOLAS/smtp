using MailTemplateHub.Application.Common;

namespace MailTemplateHub.Application.Abstractions.Rendering;

/// <summary>MJML compile failure — carries positions for the editor (maps to 422).</summary>
public sealed class MjmlInvalidException(IReadOnlyList<MjmlError> errors)
    : AppException("template.mjml_invalid", "The MJML source could not be compiled.")
{
    public IReadOnlyList<MjmlError> Errors { get; } = errors;
}

/// <summary>Required variables missing in strict mode (maps to 422).</summary>
public sealed class MissingVariablesException(IReadOnlyList<string> missing)
    : AppException("template.variables_missing", "Required variables are missing.")
{
    public IReadOnlyList<string> Missing { get; } = missing;
}
