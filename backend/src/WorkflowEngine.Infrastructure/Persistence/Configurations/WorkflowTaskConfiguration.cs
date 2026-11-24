using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

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

        builder.Property(t => t.AssignedAt)
            .IsRequired();

        builder.Property(t => t.CompletedAt)
            .IsRequired(false);

        builder.Property(t => t.CompletedBy)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.HasIndex(t => new { t.AssigneeId, t.Status, t.AssigneeType })
            .HasDatabaseName("IX_WorkflowTasks_Assignee_Status");
    }
}
