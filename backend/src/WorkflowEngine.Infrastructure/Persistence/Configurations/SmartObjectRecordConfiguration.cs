using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class SmartObjectRecordConfiguration : IEntityTypeConfiguration<SmartObjectRecordEntity>
{
    public void Configure(EntityTypeBuilder<SmartObjectRecordEntity> builder)
    {
        builder.ToTable("SmartObjectRecords");

        builder.HasKey(r => r.Id)
            .HasName("PK_SmartObjectRecords");

        builder.Property(r => r.Id)
            .ValueGeneratedOnAdd();

        builder.Property(r => r.SmartObjectId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.RecordKey)
            .HasMaxLength(150)
            .IsRequired();

        builder.HasIndex(r => new { r.SmartObjectId, r.RecordKey })
            .IsUnique()
            .HasDatabaseName("UQ_SmartObjectRecords_Object_Key");

        builder.Property(r => r.DataJson)
            .HasColumnType("text")
            .HasDefaultValue("{}")
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();
    }
}
