using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class NodeExecutionConfiguration : IEntityTypeConfiguration<NodeExecution>
{
    public void Configure(EntityTypeBuilder<NodeExecution> builder)
    {
        builder.ToTable("NodeExecutions");

        builder.HasKey(n => n.Id)
            .HasName("PK_NodeExecutions");

        builder.Property(n => n.Id)
            .ValueGeneratedOnAdd();

        builder.Property(n => n.InstanceId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(n => n.NodeId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(n => n.NodeType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(n => n.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(n => n.Attempts)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(n => n.StartedAt)
            .IsRequired();

        builder.Property(n => n.EndedAt)
            .IsRequired(false);

        builder.Property(n => n.ErrorMessage)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.HasIndex(n => new { n.InstanceId, n.StartedAt })
            .HasDatabaseName("IX_NodeExecutions_InstanceId_StartedAt");
    }
}
