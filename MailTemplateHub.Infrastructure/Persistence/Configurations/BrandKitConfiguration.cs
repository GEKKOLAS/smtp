using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailTemplateHub.Infrastructure.Persistence.Configurations;

public sealed class BrandKitConfiguration : IEntityTypeConfiguration<BrandKit>
{
    public void Configure(EntityTypeBuilder<BrandKit> builder)
    {
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Name).HasMaxLength(80).IsRequired();
        builder.Property(k => k.HeadingFont).HasMaxLength(40).IsRequired();
        builder.Property(k => k.BodyFont).HasMaxLength(40).IsRequired();
        builder.Property(k => k.Voice).HasMaxLength(500);

        // Value objects live in jsonb: they are always read and written whole.
        builder.OwnsOne(k => k.Colors, b => b.ToJson("colors"));
        builder.OwnsOne(k => k.Button, b => b.ToJson("button"));
        builder.OwnsOne(k => k.Radius, b => b.ToJson("radius"));
        builder.OwnsOne(k => k.Spacing, b => b.ToJson("spacing"));

        builder.HasIndex(k => k.UserId);
        builder.HasIndex(k => k.UserId, "ux_brand_kits_user_default")
            .IsUnique()
            .HasFilter("is_default")
            .HasDatabaseName("ux_brand_kits_user_default");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(k => k.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(k => k.LogoAsset)
            .WithMany()
            .HasForeignKey(k => k.LogoAssetId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
