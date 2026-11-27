using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class WorkflowTokenConfiguration : IEntityTypeConfiguration<WorkflowToken>
{
    public void Configure(EntityTypeBuilder<WorkflowToken> builder)
    {
        builder.ToTable("WorkflowTokens");

        builder.HasKey(t => t.Id)
            .HasName("PK_WorkflowTokens");

        builder.Property(t => t.Id)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(t => t.InstanceId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.ParentTokenId)
            .HasMaxLength(64)
            .IsRequired(false);

        builder.Property(t => t.NodeId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.BranchId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(t => t.SplitNodeId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(t => t.BranchVariablesJson)
            .IsRequired(false);

        builder.Property(t => t.Version)
            .IsConcurrencyToken()
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .IsRequired();

        builder.Property(t => t.CompletedAt)
            .IsRequired(false);

        builder.Ignore(t => t.IsOpen);

        builder.HasIndex(t => new { t.InstanceId, t.Status })
            .HasDatabaseName("IX_WorkflowTokens_Instance_Status");

        builder.HasIndex(t => new { t.InstanceId, t.SplitNodeId, t.BranchId })
            .HasDatabaseName("IX_WorkflowTokens_Instance_Split_Branch");
    }
}
