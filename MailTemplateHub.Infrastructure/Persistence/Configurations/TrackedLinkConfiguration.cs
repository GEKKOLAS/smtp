using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailTemplateHub.Infrastructure.Persistence.Configurations;

public sealed class TrackedLinkConfiguration : IEntityTypeConfiguration<TrackedLink>
{
    public void Configure(EntityTypeBuilder<TrackedLink> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Token).HasMaxLength(32).IsRequired();
        builder.Property(l => l.TargetUrl).HasMaxLength(4096).IsRequired();

        builder.HasIndex(l => l.Token).IsUnique();
        builder.HasIndex(l => l.SendJobId);
        builder.HasIndex(l => l.RecipientId);

        builder.HasOne<EmailSendJob>()
            .WithMany()
            .HasForeignKey(l => l.SendJobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Recipient)
            .WithMany()
            .HasForeignKey(l => l.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(l => l.Clicks)
            .WithOne()
            .HasForeignKey(c => c.TrackedLinkId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProcessedInboxNoticeConfiguration : IEntityTypeConfiguration<ProcessedInboxNotice>
{
    public void Configure(EntityTypeBuilder<ProcessedInboxNotice> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.ProviderMessageId).HasMaxLength(512).IsRequired();
        builder.HasIndex(n => new { n.ConnectedEmailAccountId, n.ProviderMessageId }).IsUnique();
        builder.HasIndex(n => n.ProcessedAt);

        builder.HasOne<ConnectedEmailAccount>()
            .WithMany()
            .HasForeignKey(n => n.ConnectedEmailAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class LinkClickConfiguration : IEntityTypeConfiguration<LinkClick>
{
    public void Configure(EntityTypeBuilder<LinkClick> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Classification).HasMaxLength(16).IsRequired();
        builder.HasIndex(c => c.TrackedLinkId);
        builder.HasIndex(c => c.OccurredAt);
    }
}
