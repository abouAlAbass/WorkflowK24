using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
    {
        builder.ToTable("WorkflowInstances");

        builder.HasKey(i => i.InstanceId)
            .HasName("PK_WorkflowInstances");

        builder.Property(i => i.InstanceId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.VersionId)
            .IsRequired();

        builder.Property(i => i.WorkflowKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(i => i.CurrentNodeId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(i => i.CurrentNodeType)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(i => i.InitiatorId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(i => i.ContextVariablesJson)
            .IsRequired(false);

        builder.Property(i => i.StartedAt)
            .IsRequired();

        builder.Property(i => i.CompletedAt)
            .IsRequired(false);

        builder.Property(i => i.LastUpdatedAt)
            .IsRequired();

        builder.HasOne(i => i.Version)
            .WithMany()
            .HasForeignKey(i => i.VersionId)
            .HasConstraintName("FK_WorkflowInstances_Versions")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Tasks)
            .WithOne(t => t.Instance)
            .HasForeignKey(t => t.InstanceId)
            .HasConstraintName("FK_WorkflowTasks_Instances")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Errors)
            .WithOne(e => e.Instance)
            .HasForeignKey(e => e.InstanceId)
            .HasConstraintName("FK_WorkflowInstanceErrors_Instances")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.AuditLogs)
            .WithOne(a => a.Instance)
            .HasForeignKey(a => a.InstanceId)
            .HasConstraintName("FK_WorkflowAuditLogs_Instances")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.Status, i.CurrentNodeId })
            .HasDatabaseName("IX_WorkflowInstances_Status_CurrentNode");
    }
}
