using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailTemplateHub.Infrastructure.Persistence.Configurations;

public sealed class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();

        builder.HasIndex(t => new { t.UserId, t.IsArchived }).HasFilter("deleted_at IS NULL");
        builder.HasIndex(t => new { t.UserId, t.Name })
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ux_email_templates_user_name");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Convenience pointer to the current version; no cascade (versions are history).
        builder.HasOne(t => t.CurrentVersion)
            .WithMany()
            .HasForeignKey(t => t.CurrentVersionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.Versions)
            .WithOne(v => v.Template)
            .HasForeignKey(v => v.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(t => t.DeletedAt == null);
    }
}
