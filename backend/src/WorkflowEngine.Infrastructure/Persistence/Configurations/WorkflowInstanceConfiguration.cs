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

        builder.Property(i => i.SuspendedFromStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired(false);

        builder.Property(i => i.SuspendedAt)
            .IsRequired(false);

        builder.Property(i => i.Version)
            .IsConcurrencyToken()
            .IsRequired()
            .HasDefaultValue(1);

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

        builder.Property(i => i.SensitiveVariableNamesJson)
            .IsRequired(false);

        builder.Property(i => i.ExecutionStackJson)
            .IsRequired(false);

        builder.Property(i => i.SmartFormVersionId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(i => i.StartedAt)
            .IsRequired();

        builder.Property(i => i.CompletedAt)
            .IsRequired(false);

        builder.Property(i => i.LastUpdatedAt)
            .IsRequired();

        builder.HasOne(i => i.WorkflowVersion)
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

        builder.HasMany(i => i.Events)
            .WithOne(e => e.Instance)
            .HasForeignKey(e => e.InstanceId)
            .HasConstraintName("FK_InstanceEvents_Instances")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.NodeExecutions)
            .WithOne(n => n.Instance)
            .HasForeignKey(n => n.InstanceId)
            .HasConstraintName("FK_NodeExecutions_Instances")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Tokens)
            .WithOne(t => t.Instance)
            .HasForeignKey(t => t.InstanceId)
            .HasConstraintName("FK_WorkflowTokens_Instances")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.JoinStates)
            .WithOne(j => j.Instance)
            .HasForeignKey(j => j.InstanceId)
            .HasConstraintName("FK_JoinStates_Instances")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Tasks).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(i => i.Errors).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(i => i.AuditLogs).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(i => i.Events).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(i => i.NodeExecutions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(i => i.Tokens).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(i => i.JoinStates).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(i => new { i.Status, i.CurrentNodeId })
            .HasDatabaseName("IX_WorkflowInstances_Status_CurrentNode");
    }
}
