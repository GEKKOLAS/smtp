using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailTemplateHub.Infrastructure.Persistence.Configurations;

public sealed class EmailSendAttachmentConfiguration : IEntityTypeConfiguration<EmailSendAttachment>
{
    public void Configure(EntityTypeBuilder<EmailSendAttachment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Disposition).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.ContentId).HasMaxLength(100);
        builder.Property(a => a.FilenameOverride).HasMaxLength(255);

        builder.HasIndex(a => new { a.SendJobId, a.AssetId, a.Disposition })
            .IsUnique()
            .HasDatabaseName("ux_send_attachments_unique");

        // RESTRICT: an asset attached to a send cannot be hard-deleted.
        builder.HasOne(a => a.Asset)
            .WithMany()
            .HasForeignKey(a => a.AssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
