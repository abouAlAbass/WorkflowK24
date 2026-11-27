using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class SmartObjectConfiguration : IEntityTypeConfiguration<SmartObjectEntity>
{
    public void Configure(EntityTypeBuilder<SmartObjectEntity> builder)
    {
        builder.ToTable("SmartObjects");

        builder.HasKey(s => s.Id)
            .HasName("PK_SmartObjects");

        builder.Property(s => s.Id)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(s => s.Version)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(SmartObjectStatus.Draft)
            .IsRequired();

        builder.HasIndex(s => new { s.Name, s.Version })
            .IsUnique()
            .HasDatabaseName("UQ_SmartObjects_Name_Version");

        builder.HasIndex(s => s.Name)
            .HasDatabaseName("IX_SmartObjects_Name");

        builder.Property(s => s.DisplayName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(s => s.Category)
            .HasMaxLength(100)
            .HasDefaultValue("Custom")
            .IsRequired();

        builder.Property(s => s.RecordsCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(s => s.FieldsJson)
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(s => s.MethodsJson)
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(s => s.SourceConnectionId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(s => s.SourceEntityName)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(s => s.PrimaryKeyField)
            .HasMaxLength(150)
            .IsRequired(false);

        builder.Property(s => s.PublishedAt)
            .IsRequired(false);

        builder.Property(s => s.PublishedBy)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(s => s.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();
    }
}
