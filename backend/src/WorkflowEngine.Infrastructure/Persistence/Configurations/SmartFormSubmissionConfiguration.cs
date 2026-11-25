using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class SmartFormSubmissionConfiguration : IEntityTypeConfiguration<SmartFormSubmissionEntity>
{
    public void Configure(EntityTypeBuilder<SmartFormSubmissionEntity> builder)
    {
        builder.ToTable("SmartFormSubmissions");

        builder.HasKey(s => s.Id)
            .HasName("PK_SmartFormSubmissions");

        builder.Property(s => s.Id)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.SmartFormId)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(s => s.SmartFormId)
            .HasDatabaseName("IX_SmartFormSubmissions_FormId");

        builder.Property(s => s.SubmittedBy)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.DataJson)
            .HasDefaultValue("{}")
            .IsRequired();

        builder.Property(s => s.LinkedInstanceId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(s => s.IdempotencyKey)
            .HasMaxLength(128)
            .IsRequired(false);

        // Unique per (form, key). NULL keys are never considered duplicates: SQL Server's provider emits
        // an implicit "IS NOT NULL" filter for unique indexes over nullable columns, and PostgreSQL / SQLite
        // treat NULLs as distinct in unique indexes.
        builder.HasIndex(s => new { s.SmartFormId, s.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("UX_SmartFormSubmissions_Form_IdempotencyKey");

        builder.Property(s => s.SubmittedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();
    }
}
