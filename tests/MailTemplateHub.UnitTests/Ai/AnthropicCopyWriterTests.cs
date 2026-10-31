using MailTemplateHub.Application.Abstractions.Ai;
using MailTemplateHub.Application.Features.Ai;
using MailTemplateHub.Infrastructure.Ai;

namespace MailTemplateHub.UnitTests.Ai;

public class AnthropicCopyWriterTests
{
    [Fact]
    public void Parses_variations_from_the_first_text_block()
    {
        const string body = """
            {"content": [
              {"type": "thinking", "thinking": "..."},
              {"type": "text", "text": "Here you go:\n{\"variations\": [{\"text\": \"Book your table\", \"note\": \"Shorter.\"}, {\"text\": \"Save your seat\"}]}"}
            ]}
            """;

        var variations = AnthropicCopyWriter.Parse(body);

        Assert.Equal([new CopyVariation("Book your table", "Shorter."), new CopyVariation("Save your seat", null)], variations);
    }

    [Theory]
    [InlineData("""{"content": [{"type": "text", "text": "Sorry, I can't help with that."}]}""")]
    [InlineData("""{"content": [{"type": "text", "text": "{\"other\": []}"}]}""")]
    [InlineData("""{"error": "overloaded"}""")]
    public void Unusable_responses_raise_a_generation_error(string body) =>
        Assert.Throws<AiGenerationException>(() => AnthropicCopyWriter.Parse(body));

    [Fact]
    public void Prompt_carries_voice_context_and_the_original_text()
    {
        var prompt = AnthropicCopyWriter.BuildUserPrompt(
            new CopyRequest("subject", "Hi {{firstName}}", "promotional", 3, "Warm.", "Spring launch"));

        Assert.Contains("Kind: subject", prompt);
        Assert.Contains("Number of variations: 3", prompt);
        Assert.Contains("Brand voice to follow: Warm.", prompt);
        Assert.Contains("Context (facts you may rely on): Spring launch", prompt);
        Assert.EndsWith("```\nHi {{firstName}}\n```", prompt.ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData("Hi {{firstName}} and {{ lastName }}", new[] { "firstName", "lastName" })]
    [InlineData("Raw {{{html}}} block", new[] { "html" })]
    [InlineData("No placeholders", new string[0])]
    public void Placeholders_are_normalized(string text, string[] expected) =>
        Assert.True(RewriteCopyHandler.Placeholders(text).SetEquals(expected));
}
