using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class DelegationRuleConfiguration : IEntityTypeConfiguration<DelegationRule>
{
    public void Configure(EntityTypeBuilder<DelegationRule> builder)
    {
        builder.ToTable("DelegationRules");

        builder.HasKey(r => r.Id)
            .HasName("PK_DelegationRules");

        builder.Property(r => r.Id)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(r => r.DelegatorId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(r => r.DelegateId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(r => r.Scope)
            .HasMaxLength(255)
            .HasDefaultValue("*")
            .IsRequired();

        builder.Property(r => r.ValidFrom)
            .IsRequired();

        builder.Property(r => r.ValidTo)
            .IsRequired();

        builder.Property(r => r.Active)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(r => r.Reason)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(r => r.CreatedBy)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.HasIndex(r => new { r.DelegatorId, r.Active, r.ValidFrom, r.ValidTo })
            .HasDatabaseName("IX_DelegationRules_Delegator_Active_Window");
    }
}
