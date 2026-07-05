using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailTemplateHub.Infrastructure.Persistence.Configurations;

public sealed class EmailSendJobConfiguration : IEntityTypeConfiguration<EmailSendJob>
{
    public void Configure(EntityTypeBuilder<EmailSendJob> builder)
    {
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(j => j.SubjectSnapshot).HasMaxLength(1000).IsRequired();
        builder.Property(j => j.VariableValues).HasColumnType("jsonb").IsRequired();
        builder.Property(j => j.FailureCode).HasMaxLength(50);
        builder.Property(j => j.IdempotencyKey).HasMaxLength(200);

        builder.HasIndex(j => new { j.UserId, j.Status, j.CreatedAt });
        builder.HasIndex(j => j.ScheduledAt)
            .HasFilter("status = 'Scheduled'")
            .HasDatabaseName("ix_send_jobs_due");
        builder.HasIndex(j => new { j.UserId, j.IdempotencyKey })
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL")
            .HasDatabaseName("ux_send_jobs_idempotency");

        builder.HasOne(j => j.Account)
            .WithMany()
            .HasForeignKey(j => j.ConnectedEmailAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(j => j.TemplateVersion)
            .WithMany()
            .HasForeignKey(j => j.TemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(j => j.Recipients)
            .WithOne()
            .HasForeignKey(r => r.SendJobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(j => j.Attachments)
            .WithOne()
            .HasForeignKey(a => a.SendJobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
