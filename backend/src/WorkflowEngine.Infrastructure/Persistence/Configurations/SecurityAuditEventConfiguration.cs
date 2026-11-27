using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class SecurityAuditEventConfiguration : IEntityTypeConfiguration<SecurityAuditEvent>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEvent> builder)
    {
        builder.ToTable("SecurityAuditEvents");
        builder.HasKey(e => e.Id).HasName("PK_SecurityAuditEvents");

        builder.Property(e => e.Id).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EventType)
            .HasConversion<string>()
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(e => e.ActorId).HasMaxLength(150).IsRequired();
        builder.Property(e => e.TargetType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.TargetId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.MetadataJson).HasMaxLength(4000);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_SecurityAuditEvents_CreatedAt");
        builder.HasIndex(e => e.EventType).HasDatabaseName("IX_SecurityAuditEvents_EventType");
        builder.HasIndex(e => new { e.TargetType, e.TargetId }).HasDatabaseName("IX_SecurityAuditEvents_Target");
    }
}
