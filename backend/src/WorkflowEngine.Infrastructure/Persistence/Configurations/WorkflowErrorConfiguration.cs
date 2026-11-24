using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class WorkflowErrorConfiguration : IEntityTypeConfiguration<WorkflowError>
{
    public void Configure(EntityTypeBuilder<WorkflowError> builder)
    {
        builder.ToTable("WorkflowInstanceErrors");

        builder.HasKey(e => e.ErrorId)
            .HasName("PK_WorkflowInstanceErrors");

        builder.Property(e => e.ErrorId)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.InstanceId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.NodeId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.NodeType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ErrorCode)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .IsRequired();

        builder.Property(e => e.StackTrace)
            .IsRequired(false);

        builder.Property(e => e.InputSnapshotJson)
            .IsRequired(false);

        builder.Property(e => e.RetryCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(e => e.IsResolved)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(e => e.OccurredAt)
            .IsRequired();

        builder.Property(e => e.ResolvedAt)
            .IsRequired(false);

        builder.HasIndex(e => new { e.InstanceId, e.OccurredAt })
            .HasDatabaseName("IX_WorkflowInstanceErrors_InstanceID");
    }
}
