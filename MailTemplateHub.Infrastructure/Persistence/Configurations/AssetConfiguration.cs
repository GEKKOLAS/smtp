using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailTemplateHub.Infrastructure.Persistence.Configurations;

public sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.Access).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.UploadState).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.OriginalFilename).HasMaxLength(255).IsRequired();
        builder.Property(a => a.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(a => a.MimeType).HasMaxLength(150).IsRequired();

        builder.HasIndex(a => a.StorageKey).IsUnique();
        builder.HasIndex(a => new { a.UserId, a.Kind }).HasFilter("deleted_at IS NULL");

        // Dedupe: an identical file per user resolves to one row.
        builder.HasIndex(a => new { a.UserId, a.ChecksumSha256 })
            .IsUnique()
            .HasFilter("deleted_at IS NULL AND upload_state = 'Ready'")
            .HasDatabaseName("ux_assets_user_checksum");

        // Cleanup sweep target for abandoned uploads.
        builder.HasIndex(a => new { a.UploadState, a.CreatedAt })
            .HasDatabaseName("ix_assets_pending");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(a => a.DeletedAt == null);
    }
}
