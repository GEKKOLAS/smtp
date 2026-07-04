using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailTemplateHub.Infrastructure.Persistence.Configurations;

public sealed class OAuthStateConfiguration : IEntityTypeConfiguration<OAuthState>
{
    public void Configure(EntityTypeBuilder<OAuthState> builder)
    {
        builder.ToTable("oauth_states"); // avoid the convention's "o_auth_states"

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Provider).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.PkceVerifier).HasMaxLength(200).IsRequired();
        builder.Property(s => s.ReturnTo).HasMaxLength(200).IsRequired();

        builder.HasIndex(s => s.ExpiresAt); // cleanup sweep

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
