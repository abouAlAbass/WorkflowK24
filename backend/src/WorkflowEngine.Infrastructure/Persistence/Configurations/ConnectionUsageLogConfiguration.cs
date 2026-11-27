using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class ConnectionUsageLogConfiguration : IEntityTypeConfiguration<ConnectionUsageLog>
{
    public void Configure(EntityTypeBuilder<ConnectionUsageLog> builder)
    {
        builder.ToTable("ConnectionUsageLogs");

        builder.HasKey(l => l.Id)
            .HasName("PK_ConnectionUsageLogs");

        builder.Property(l => l.Id)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.ConnectionId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.InstanceId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(l => l.WorkflowKey)
            .HasMaxLength(150)
            .IsRequired(false);

        builder.Property(l => l.NodeId)
            .HasMaxLength(150)
            .IsRequired(false);

        builder.Property(l => l.ActorId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(l => l.Success)
            .IsRequired();

        builder.Property(l => l.DurationMs)
            .IsRequired();

        builder.Property(l => l.ExecutedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasIndex(l => new { l.ConnectionId, l.ExecutedAt })
            .HasDatabaseName("IX_ConnectionUsageLogs_Connection_ExecutedAt");
    }
}
