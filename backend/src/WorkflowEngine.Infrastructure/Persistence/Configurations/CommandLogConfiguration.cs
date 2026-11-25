using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class CommandLogConfiguration : IEntityTypeConfiguration<CommandLog>
{
    public void Configure(EntityTypeBuilder<CommandLog> builder)
    {
        builder.ToTable("CommandLogs");

        builder.HasKey(c => c.CommandId)
            .HasName("PK_CommandLogs");

        builder.Property(c => c.CommandId)
            .ValueGeneratedNever();

        builder.Property(c => c.InstanceId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.CommandType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.ResultJson)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.HasIndex(c => c.InstanceId)
            .HasDatabaseName("IX_CommandLogs_InstanceId");

        builder.HasOne(c => c.Instance)
            .WithMany()
            .HasForeignKey(c => c.InstanceId)
            .HasConstraintName("FK_CommandLogs_Instances")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
