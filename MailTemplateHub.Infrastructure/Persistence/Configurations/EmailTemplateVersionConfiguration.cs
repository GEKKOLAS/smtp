using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailTemplateHub.Infrastructure.Persistence.Configurations;

public sealed class EmailTemplateVersionConfiguration : IEntityTypeConfiguration<EmailTemplateVersion>
{
    public void Configure(EntityTypeBuilder<EmailTemplateVersion> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Subject).HasMaxLength(500).IsRequired();
        builder.Property(v => v.Preheader).HasMaxLength(500);
        builder.Property(v => v.EditorKind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(v => v.GrapesProject).HasColumnType("jsonb");
        builder.Property(v => v.VariablesSchema).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(v => new { v.TemplateId, v.VersionNumber })
            .IsUnique()
            .HasDatabaseName("ux_template_versions_number");

        builder.HasMany(v => v.TemplateAssets)
            .WithOne()
            .HasForeignKey(a => a.TemplateVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Immutable: no soft delete, no updated_at.
    }
}
