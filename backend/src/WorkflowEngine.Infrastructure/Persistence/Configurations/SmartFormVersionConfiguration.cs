using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class SmartFormVersionConfiguration : IEntityTypeConfiguration<SmartFormVersion>
{
    public void Configure(EntityTypeBuilder<SmartFormVersion> builder)
    {
        builder.ToTable("SmartFormVersions");

        builder.HasKey(v => v.Id)
            .HasName("PK_SmartFormVersions");

        builder.Property(v => v.Id)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(v => v.SmartFormId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(v => v.VersionNumber)
            .IsRequired();

        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(v => v.FieldsJson)
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(v => v.RulesJson)
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(v => v.PublishedAt)
            .IsRequired(false);

        builder.Property(v => v.PublishedBy)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(v => v.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasIndex(v => new { v.SmartFormId, v.VersionNumber })
            .IsUnique()
            .HasDatabaseName("UQ_SmartFormVersions_Form_Ver");
    }
}
