using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class WorkflowAuditLogConfiguration : IEntityTypeConfiguration<WorkflowAuditLog>
{
    public void Configure(EntityTypeBuilder<WorkflowAuditLog> builder)
    {
        builder.ToTable("WorkflowAuditLogs");

        builder.HasKey(a => a.LogId)
            .HasName("PK_WorkflowAuditLogs");

        builder.Property(a => a.LogId)
            .ValueGeneratedOnAdd();

        builder.Property(a => a.InstanceId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.SequenceNumber)
            .IsRequired();

        builder.Property(a => a.NodeId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(a => a.NodeType)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(a => a.EventType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.EventSummary)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(a => a.OutcomeBranch)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(a => a.StateSnapshotJson)
            .IsRequired(false);

        builder.Property(a => a.LoggedAt)
            .IsRequired();

        builder.HasIndex(a => new { a.InstanceId, a.SequenceNumber })
            .HasDatabaseName("IX_WorkflowAuditLogs_Instance_Seq");
    }
}
