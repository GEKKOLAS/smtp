using System.Text;
using System.Text.Json;
using MailTemplateHub.Application.Features.BrandKits;
using MailTemplateHub.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailTemplateHub.Api.Controllers;

[ApiController]
[Route("api/v1/brand-kits")]
[Authorize]
public sealed class BrandKitsController(BrandKitsHandler handler) : ControllerBase
{
    private static readonly JsonSerializerOptions TokenJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(new { items = await handler.ListAsync(ct) });

    /// <summary>The fonts a kit may use, with their email-safe fallback stacks.</summary>
    [HttpGet("fonts")]
    public IActionResult Fonts()
        => Ok(new { items = BrandFonts.All });

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await handler.GetAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveBrandKitCommand request, CancellationToken ct)
    {
        var kit = await handler.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = kit.Id }, kit);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveBrandKitCommand request, CancellationToken ct)
        => Ok(await handler.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/default")]
    public async Task<IActionResult> SetDefault(Guid id, CancellationToken ct)
        => Ok(await handler.SetDefaultAsync(id, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await handler.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Downloads the kit as design tokens: JSON (default) or CSS custom properties.</summary>
    [HttpGet("{id:guid}/tokens")]
    public async Task<IActionResult> Tokens(Guid id, [FromQuery] string? format, CancellationToken ct)
    {
        var tokens = await handler.GetTokensAsync(id, ct);
        var slug = BrandTokens.Slug(tokens.Name);

        return format?.ToLowerInvariant() switch
        {
            null or "" or "json" => File(JsonSerializer.SerializeToUtf8Bytes(tokens, TokenJson),
                "application/json", $"{slug}-tokens.json"),
            "css" => File(Encoding.UTF8.GetBytes(BrandTokens.ToCss(tokens)), "text/css", $"{slug}-tokens.css"),
            _ => BadRequest(new { errorCode = "brand_kit.unknown_format", message = "Format must be json or css." }),
        };
    }
}
