using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailTemplateHub.Infrastructure.Persistence.Configurations;

public sealed class OAuthTokenConfiguration : IEntityTypeConfiguration<OAuthToken>
{
    public void Configure(EntityTypeBuilder<OAuthToken> builder)
    {
        builder.ToTable("oauth_tokens"); // avoid the convention's "o_auth_tokens"

        // PK == FK to the account (1:1).
        builder.HasKey(t => t.ConnectedEmailAccountId);

        builder.Property(t => t.AccessTokenCiphertext).IsRequired();
        builder.Property(t => t.AccessTokenNonce).IsRequired();
        builder.Property(t => t.WrappedDek).IsRequired();
        builder.Property(t => t.KekVersion).IsRequired();

        builder.HasIndex(t => t.AccessTokenExpiresAt);

        // Tokens follow the account's soft-delete visibility.
        builder.HasQueryFilter(t => t.Account!.DeletedAt == null);
    }
}
