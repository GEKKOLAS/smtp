using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailTemplateHub.Infrastructure.Persistence.Configurations;

public sealed class ConnectedEmailAccountConfiguration : IEntityTypeConfiguration<ConnectedEmailAccount>
{
    public void Configure(EntityTypeBuilder<ConnectedEmailAccount> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Provider).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.ProviderAccountId).HasMaxLength(255).IsRequired();
        builder.Property(a => a.EmailAddress).HasColumnType("citext").IsRequired();
        builder.Property(a => a.DisplayName).HasMaxLength(255);
        builder.Property(a => a.TenantId).HasMaxLength(255);
        builder.Property(a => a.GrantedScopes).HasColumnType("text[]");
        builder.Property(a => a.State).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(a => a.StateReason).HasMaxLength(50);

        builder.HasIndex(a => a.UserId);

        // Reconnect upserts on this identity triple.
        builder.HasIndex(a => new { a.UserId, a.Provider, a.ProviderAccountId })
            .IsUnique()
            .HasFilter("deleted_at IS NULL");

        // At most one default account per user.
        builder.HasIndex(a => a.UserId)
            .IsUnique()
            .HasFilter("is_default AND deleted_at IS NULL")
            .HasDatabaseName("ux_connected_email_accounts_one_default");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Token)
            .WithOne(t => t.Account!)
            .HasForeignKey<OAuthToken>(t => t.ConnectedEmailAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(a => a.DeletedAt == null);
    }
}
