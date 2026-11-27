using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class TaskSlotConfiguration : IEntityTypeConfiguration<TaskSlot>
{
    public void Configure(EntityTypeBuilder<TaskSlot> builder)
    {
        builder.ToTable("TaskSlots");

        builder.HasKey(s => s.Id)
            .HasName("PK_TaskSlots");

        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd();

        builder.Property(s => s.TaskId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.AssigneeUserId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.Outcome)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(s => s.Comments)
            .IsRequired(false);

        builder.Property(s => s.ClaimedAt)
            .IsRequired(false);

        builder.Property(s => s.CompletedAt)
            .IsRequired(false);

        builder.Property(s => s.CompletedByUserId)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(s => s.OnBehalfOfUserId)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.HasIndex(s => new { s.TaskId, s.AssigneeUserId })
            .IsUnique()
            .HasDatabaseName("UQ_TaskSlots_Task_Assignee");
    }
}
