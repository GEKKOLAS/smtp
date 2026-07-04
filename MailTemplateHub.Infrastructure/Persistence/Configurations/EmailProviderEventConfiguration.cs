using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailTemplateHub.Infrastructure.Persistence.Configurations;

public sealed class EmailProviderEventConfiguration : IEntityTypeConfiguration<EmailProviderEvent>
{
    public void Configure(EntityTypeBuilder<EmailProviderEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Provider).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.EventType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.ProviderErrorCode).HasMaxLength(100);
        builder.Property(e => e.Detail).HasColumnType("jsonb");

        builder.HasIndex(e => new { e.ConnectedEmailAccountId, e.CreatedAt });

        // Append-only; keep events even if the account row is later removed.
        builder.HasOne<ConnectedEmailAccount>()
            .WithMany()
            .HasForeignKey(e => e.ConnectedEmailAccountId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
