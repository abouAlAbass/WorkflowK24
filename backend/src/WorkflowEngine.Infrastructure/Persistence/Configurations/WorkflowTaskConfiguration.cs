using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class WorkflowTaskConfiguration : IEntityTypeConfiguration<WorkflowTask>
{
    public void Configure(EntityTypeBuilder<WorkflowTask> builder)
    {
        builder.ToTable("WorkflowTasks");

        builder.HasKey(t => t.TaskId)
            .HasName("PK_WorkflowTasks");

        builder.Property(t => t.TaskId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.InstanceId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.NodeId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.TokenId)
            .HasMaxLength(64)
            .IsRequired(false);

        builder.Property(t => t.BranchId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(t => t.TaskTitle)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(t => t.AssigneeType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.AssigneeId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(t => t.OriginalAssigneeId)
            .HasMaxLength(255)
            .HasDefaultValue("")
            .IsRequired();

        builder.Property(t => t.OriginalAssigneeType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(AssigneeType.User)
            .IsRequired();

        builder.Property(t => t.ResolutionPolicy)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(TaskResolutionPolicy.Single)
            .IsRequired();

        builder.Property(t => t.QuorumCount)
            .IsRequired(false);

        builder.Property(t => t.ClaimedByUserId)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(t => t.ClaimedAt)
            .IsRequired(false);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.SelectedOutcome)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(t => t.Comments)
            .IsRequired(false);

        builder.Property(t => t.DueDate)
            .IsRequired(false);

        builder.Property(t => t.DueAt)
            .IsRequired(false);

        builder.Property(t => t.AssignedAt)
            .IsRequired();

        builder.Property(t => t.CompletedAt)
            .IsRequired(false);

        builder.Property(t => t.CompletedBy)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(t => t.CompletedByUserId)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(t => t.OnBehalfOfUserId)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(t => t.EscalatedAt)
            .IsRequired(false);

        builder.Property(t => t.MetadataJson)
            .IsRequired(false);

        builder.Property(t => t.Version)
            .HasDefaultValue(1)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Ignore(t => t.IsActive);

        builder.HasMany(t => t.Slots)
            .WithOne(s => s.Task)
            .HasForeignKey(s => s.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Assignments)
            .WithOne(a => a.Task)
            .HasForeignKey(a => a.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(WorkflowTask.Slots))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(WorkflowTask.Assignments))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(t => new { t.AssigneeId, t.Status, t.AssigneeType })
            .HasDatabaseName("IX_WorkflowTasks_Assignee_Status");

        builder.HasIndex(t => new { t.Status, t.DueAt })
            .HasDatabaseName("IX_WorkflowTasks_Status_DueAt");
    }
}
