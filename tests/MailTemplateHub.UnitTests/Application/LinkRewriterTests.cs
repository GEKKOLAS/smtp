using MailTemplateHub.Application.Features.Tracking;
using MailTemplateHub.Domain.Entities;

namespace MailTemplateHub.UnitTests.Application;

public class LinkRewriterTests
{
    private static string Track(string target, int position) => $"https://t.test/{position}";

    [Fact]
    public void Tracks_http_links_once_per_url_and_keeps_the_rest()
    {
        const string html = """
            <p><a href="https://shop.test/menu?a=1&amp;b=2" style="color:red">Menu</a></p>
            <a target="_blank" href='https://shop.test/menu?a=1&amp;b=2'>Menu again</a>
            <a href="https://shop.test/book">Book</a>
            <a href="mailto:hi@shop.test">Mail</a> <a href="#top">Top</a> <img src="cid:logo">
            """;

        var result = LinkRewriter.Rewrite(html, "Menu: https://shop.test/menu?a=1&b=2", null, Track);

        Assert.Equal(
            [new RewrittenLink("https://shop.test/menu?a=1&b=2", "https://shop.test/menu?a=1&b=2", 0),
             new RewrittenLink("https://shop.test/book", "https://shop.test/book", 1)],
            result.Links);
        Assert.Contains("""<a href="https://t.test/0" style="color:red">Menu</a>""", result.Html);
        Assert.Contains("""<a target="_blank" href='https://t.test/0'>Menu again</a>""", result.Html);
        Assert.Contains("""href="https://t.test/1">Book""", result.Html);
        Assert.Contains("mailto:hi@shop.test", result.Html);
        Assert.Contains("""href="#top">""", result.Html);
        Assert.Equal("Menu: https://t.test/0", result.Text);
    }

    [Theory]
    [InlineData("""<a href="https://shop.test/unsubscribe?u=1">Leave</a>""")]
    [InlineData("""<a href="https://shop.test/prefs">Unsubscribe</a>""")]
    [InlineData("""<a href="https://shop.test/p">Darse de baja</a>""")]
    [InlineData("""<a href="https://shop.test/opt-out">Stop</a>""")]
    public void Never_touches_unsubscribe_links(string html)
    {
        var result = LinkRewriter.Rewrite(html, "", "spring", Track);

        Assert.Equal(html, result.Html);
        Assert.Empty(result.Links);
    }

    [Fact]
    public void Utm_without_tracking_tags_links_in_place()
    {
        var result = LinkRewriter.Rewrite(
            """<a href="https://shop.test/menu#dessert">Menu</a>""", "https://shop.test/menu#dessert", "Spring launch", null);

        const string tagged = "https://shop.test/menu?utm_source=mail-template-hub&utm_medium=email&utm_campaign=Spring%20launch#dessert";
        Assert.Contains($"""href="{tagged.Replace("&", "&amp;")}">""", result.Html);
        Assert.Equal(tagged, result.Text);
        Assert.Equal(tagged, Assert.Single(result.Links).TargetUrl);
    }

    [Theory]
    [InlineData("https://a.test/", "https://a.test/?utm_source=mail-template-hub&utm_medium=email&utm_campaign=c")]
    [InlineData("https://a.test/?x=1", "https://a.test/?x=1&utm_source=mail-template-hub&utm_medium=email&utm_campaign=c")]
    [InlineData("https://a.test/?utm_source=news", "https://a.test/?utm_source=news")]
    [InlineData("https://a.test/?x=1&UTM_Campaign=mine", "https://a.test/?x=1&UTM_Campaign=mine")]
    public void Utm_tags_never_override_the_authors_own(string url, string expected) =>
        Assert.Equal(expected, LinkRewriter.AddUtm(url, "c"));

    [Fact]
    public void Text_part_replaces_longer_urls_first()
    {
        const string html = """<a href="https://a.test">Home</a><a href="https://a.test/deals">Deals</a>""";

        var result = LinkRewriter.Rewrite(html, "https://a.test/deals and https://a.test", null, Track);

        Assert.Equal("https://t.test/1 and https://t.test/0", result.Text);
    }
}

public class ClickClassifierTests
{
    private static readonly DateTimeOffset Delivered = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    private const string Chrome =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0 Safari/537.36";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Microsoft Office/16.0 (Windows NT 10.0; Microsoft Outlook 16.0.1)")]
    [InlineData("Mozilla/5.0 (compatible; Proofpoint URL Defense)")]
    [InlineData("python-requests/2.32")]
    [InlineData("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)")]
    public void Scanners_and_missing_agents_are_not_people(string? userAgent) =>
        Assert.Equal(ClickClassifications.Scanner,
            ClickClassifier.Classify(userAgent, Delivered, Delivered.AddMinutes(5), Window));

    [Fact]
    public void A_click_seconds_after_delivery_is_a_scanner() =>
        Assert.Equal(ClickClassifications.Scanner,
            ClickClassifier.Classify(Chrome, Delivered, Delivered.AddSeconds(3), Window));

    [Theory]
    [InlineData(Chrome)]
    [InlineData("Mozilla/5.0 (Linux; Android 14; CUBOT X30) AppleWebKit/537.36 Chrome/141.0 Mobile Safari/537.36")]
    public void A_browser_later_on_is_a_person(string userAgent) =>
        Assert.Equal(ClickClassifications.Human,
            ClickClassifier.Classify(userAgent, Delivered, Delivered.AddMinutes(5), Window));
}
