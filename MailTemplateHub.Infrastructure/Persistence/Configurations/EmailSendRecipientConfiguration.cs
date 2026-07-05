using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailTemplateHub.Infrastructure.Persistence.Configurations;

public sealed class EmailSendRecipientConfiguration : IEntityTypeConfiguration<EmailSendRecipient>
{
    public void Configure(EntityTypeBuilder<EmailSendRecipient> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.EmailAddress).HasColumnType("citext").IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.VariableOverrides).HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.FailureCode).HasMaxLength(50);

        builder.HasIndex(r => new { r.SendJobId, r.Status });
        builder.HasIndex(r => r.EmailAddress);
        builder.HasIndex(r => new { r.SendJobId, r.EmailAddress })
            .IsUnique()
            .HasDatabaseName("ux_send_recipients_job_email");
    }
}
