using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class ConnectionPermissionConfiguration : IEntityTypeConfiguration<ConnectionPermission>
{
    public void Configure(EntityTypeBuilder<ConnectionPermission> builder)
    {
        builder.ToTable("ConnectionPermissions");

        builder.HasKey(p => p.Id)
            .HasName("PK_ConnectionPermissions");

        builder.Property(p => p.Id)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.ConnectionId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.SubjectType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.SubjectId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.CanUse)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(p => p.CanManage)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasIndex(p => new { p.ConnectionId, p.SubjectType, p.SubjectId })
            .IsUnique()
            .HasDatabaseName("UQ_ConnectionPermissions_Connection_Subject");
    }
}
