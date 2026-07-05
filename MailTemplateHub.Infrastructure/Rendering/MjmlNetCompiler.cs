using MailTemplateHub.Application.Abstractions.Rendering;
using Mjml.Net;

namespace MailTemplateHub.Infrastructure.Rendering;

/// <summary>
/// In-process MJML compiler via Mjml.Net (spec 08 §2.3). If port-fidelity gaps
/// appear, this is the seam to swap for a Node sidecar.
/// </summary>
internal sealed class MjmlNetCompiler : IMjmlCompiler
{
    private readonly MjmlRenderer _renderer = new();

    public MjmlCompileResult Compile(string mjml)
    {
        var options = new MjmlOptions { Beautify = false };
        var (html, errors) = _renderer.Render(mjml, options);

        var mapped = errors
            .Select(e => new MjmlError(e.Position.LineNumber, e.Position.LinePosition, e.Error))
            .ToList();

        return new MjmlCompileResult(html, mapped);
    }
}
