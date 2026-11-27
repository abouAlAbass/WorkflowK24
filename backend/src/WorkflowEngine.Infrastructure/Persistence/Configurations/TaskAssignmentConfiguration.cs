using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> builder)
    {
        builder.ToTable("TaskAssignments");

        builder.HasKey(a => a.Id)
            .HasName("PK_TaskAssignments");

        builder.Property(a => a.Id)
            .ValueGeneratedOnAdd();

        builder.Property(a => a.TaskId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Action)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(a => a.FromAssigneeId)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(a => a.ToAssigneeId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.ToAssigneeType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.ActorUserId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.Reason)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.HasIndex(a => new { a.TaskId, a.CreatedAt })
            .HasDatabaseName("IX_TaskAssignments_Task_CreatedAt");
    }
}
