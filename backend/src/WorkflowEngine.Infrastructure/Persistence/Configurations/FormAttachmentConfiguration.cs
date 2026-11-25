using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class FormAttachmentConfiguration : IEntityTypeConfiguration<FormAttachmentEntity>
{
    public void Configure(EntityTypeBuilder<FormAttachmentEntity> builder)
    {
        builder.ToTable("FormAttachments");

        builder.HasKey(a => a.Id)
            .HasName("PK_FormAttachments");

        builder.Property(a => a.Id)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.ContentType)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(a => a.SizeBytes)
            .IsRequired();

        builder.Property(a => a.StoragePath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(a => a.Sha256Hash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(a => a.UploadedByUserId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.WorkflowInstanceId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(a => a.SubmissionId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(a => a.UploadedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasIndex(a => a.UploadedByUserId)
            .HasDatabaseName("IX_FormAttachments_UploadedByUserId");

        builder.HasIndex(a => a.WorkflowInstanceId)
            .HasDatabaseName("IX_FormAttachments_WorkflowInstanceId");
    }
}
