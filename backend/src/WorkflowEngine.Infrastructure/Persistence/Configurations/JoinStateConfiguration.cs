using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class JoinStateConfiguration : IEntityTypeConfiguration<JoinState>
{
    public void Configure(EntityTypeBuilder<JoinState> builder)
    {
        builder.ToTable("JoinStates");

        builder.HasKey(j => j.Id)
            .HasName("PK_JoinStates");

        builder.Property(j => j.Id)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(j => j.InstanceId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(j => j.SplitNodeId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(j => j.JoinNodeId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(j => j.ExpectedCount)
            .IsRequired();

        builder.Property(j => j.ArrivedCount)
            .IsRequired();

        builder.Property(j => j.JoinFired)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(j => j.Version)
            .IsConcurrencyToken()
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(j => j.CreatedAt)
            .IsRequired();

        builder.Property(j => j.UpdatedAt)
            .IsRequired();

        builder.HasIndex(j => new { j.InstanceId, j.SplitNodeId, j.JoinNodeId })
            .HasDatabaseName("IX_JoinStates_Instance_Split_Join");
    }
}
