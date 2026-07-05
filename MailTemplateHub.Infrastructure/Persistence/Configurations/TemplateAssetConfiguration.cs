using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailTemplateHub.Infrastructure.Persistence.Configurations;

public sealed class TemplateAssetConfiguration : IEntityTypeConfiguration<TemplateAsset>
{
    public void Configure(EntityTypeBuilder<TemplateAsset> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Usage).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.ContentId).HasMaxLength(100);

        builder.HasIndex(a => new { a.TemplateVersionId, a.AssetId, a.Usage })
            .IsUnique()
            .HasDatabaseName("ux_template_assets_unique");
        builder.HasIndex(a => a.AssetId);

        // RESTRICT drives the "asset in use" check on asset delete (spec 05).
        builder.HasOne(a => a.Asset)
            .WithMany()
            .HasForeignKey(a => a.AssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
