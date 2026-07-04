using System.ComponentModel.DataAnnotations;

namespace MailTemplateHub.Infrastructure;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>Dev/test convenience; production applies migrations in the deploy pipeline.</summary>
    public bool ApplyMigrationsOnStartup { get; init; }
}
