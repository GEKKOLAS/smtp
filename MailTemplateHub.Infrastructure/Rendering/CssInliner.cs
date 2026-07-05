namespace MailTemplateHub.Infrastructure.Rendering;

/// <summary>
/// Moves &lt;style&gt; rules onto element style attributes (spec 08 §2.6). Many
/// clients (notably Gmail) support inline styles more reliably than &lt;style&gt;
/// blocks; media queries are retained for clients that do support them.
/// </summary>
internal static class CssInliner
{
    public static string Inline(string html)
    {
        var result = PreMailer.Net.PreMailer.MoveCssInline(
            html,
            removeStyleElements: false,
            stripIdAndClassAttributes: false,
            removeComments: false);
        return result.Html;
    }
}
