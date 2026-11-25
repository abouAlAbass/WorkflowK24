using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class InstanceEventConfiguration : IEntityTypeConfiguration<InstanceEvent>
{
    public void Configure(EntityTypeBuilder<InstanceEvent> builder)
    {
        builder.ToTable("InstanceEvents");

        builder.HasKey(e => e.Id)
            .HasName("PK_InstanceEvents");

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.InstanceId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Seq)
            .IsRequired();

        builder.Property(e => e.Type)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.NodeId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(e => e.ActorId)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(e => e.ActorType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.PayloadJson)
            .IsRequired(false);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.HasIndex(e => new { e.InstanceId, e.Seq })
            .IsUnique()
            .HasDatabaseName("IX_InstanceEvents_InstanceId_Seq");
    }
}
