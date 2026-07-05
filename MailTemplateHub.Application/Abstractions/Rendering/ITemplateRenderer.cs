namespace MailTemplateHub.Application.Abstractions.Rendering;

public sealed record RenderRequest(
    TemplateContent Content,
    IReadOnlyDictionary<string, string?> Variables,
    bool Strict,
    IReadOnlyDictionary<Guid, string> AssetUrls);

public sealed record RenderWarning(string Code, string Message, int? Line = null);

public sealed record RenderedEmail(
    string Subject,
    string? Preheader,
    string Html,
    string Text,
    IReadOnlyList<RenderWarning> Warnings);

/// <summary>
/// The template rendering pipeline (spec 08-rendering.md): validate variables,
/// compile MJML, sanitize, render variables, inline CSS, resolve assets, and
/// generate plain text. Deterministic given identical inputs.
/// </summary>
public interface ITemplateRenderer
{
    RenderedEmail Render(RenderRequest request);
}

/// <summary>Compiles MJML to responsive HTML. Swappable for a Node sidecar (spec 08 §2.3).</summary>
public interface IMjmlCompiler
{
    MjmlCompileResult Compile(string mjml);
}

public sealed record MjmlCompileResult(string Html, IReadOnlyList<MjmlError> Errors)
{
    public bool Success => Errors.Count == 0;
}

public sealed record MjmlError(int Line, int Column, string Message);

/// <summary>Allowlist-based HTML sanitizer (spec 04 §3). Security-critical; unit-tested.</summary>
public interface IHtmlSanitizer
{
    string Sanitize(string html);
}
