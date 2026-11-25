using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("SystemSettings");

        builder.HasKey(s => s.Key)
            .HasName("PK_SystemSettings");

        builder.Property(s => s.Key)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(s => s.Value)
            .HasDefaultValue("")
            .IsRequired();

        builder.Property(s => s.Category)
            .HasMaxLength(100)
            .HasDefaultValue("General")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();
    }
}
