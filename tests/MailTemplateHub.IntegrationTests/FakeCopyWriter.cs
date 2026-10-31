using MailTemplateHub.Application.Abstractions.Ai;

namespace MailTemplateHub.IntegrationTests;

/// <summary>
/// Stands in for the Anthropic copy writer: returns whatever the test queued and
/// records the request the handler built.
/// </summary>
public sealed class FakeCopyWriter : IAiCopyWriter
{
    public bool IsConfigured { get; set; } = true;

    public IReadOnlyList<CopyVariation> Next { get; set; } = [];

    public CopyRequest? LastRequest { get; private set; }

    public Task<IReadOnlyList<CopyVariation>> RewriteAsync(CopyRequest request, CancellationToken ct)
    {
        LastRequest = request;
        return Task.FromResult(Next);
    }
}
