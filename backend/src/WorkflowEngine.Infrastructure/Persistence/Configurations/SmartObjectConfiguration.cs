using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

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

        builder.HasIndex(s => s.Name)
            .IsUnique()
            .HasDatabaseName("UQ_SmartObjects_Name");

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

        builder.Property(s => s.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();
    }
}
