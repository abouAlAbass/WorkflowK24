using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class UserAuditLogConfiguration : IEntityTypeConfiguration<UserAuditLog>
{
    public void Configure(EntityTypeBuilder<UserAuditLog> builder)
    {
        builder.ToTable("UserAuditLogs");
        builder.HasKey(a => a.Id).HasName("PK_UserAuditLogs");

        builder.Property(a => a.Id).HasMaxLength(100).IsRequired();
        builder.Property(a => a.ActorId).HasMaxLength(100).IsRequired();
        builder.Property(a => a.ActorEmail).HasMaxLength(255).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.TargetUserId).HasMaxLength(100).IsRequired();
        builder.Property(a => a.TargetEmail).HasMaxLength(255).IsRequired();
        builder.Property(a => a.Details).HasMaxLength(2000);
        builder.Property(a => a.OccurredAt).IsRequired();

        builder.HasIndex(a => a.OccurredAt).HasDatabaseName("IX_UserAuditLogs_OccurredAt");
    }
}
