using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.ToTable("WorkflowDefinitions");

        builder.HasKey(d => d.DefinitionId)
            .HasName("PK_WorkflowDefinitions");

        builder.Property(d => d.DefinitionId)
            .ValueGeneratedOnAdd();

        builder.Property(d => d.WorkflowKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(d => d.WorkflowKey)
            .IsUnique()
            .HasDatabaseName("UQ_WorkflowDefinitions_Key");

        builder.Property(d => d.Name)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.Description)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(d => d.Category)
            .HasMaxLength(100)
            .HasDefaultValue("General")
            .IsRequired();

        builder.Property(d => d.CreatedBy)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .IsRequired();

        builder.HasMany(d => d.Versions)
            .WithOne(v => v.Definition)
            .HasForeignKey(v => v.DefinitionId)
            .HasConstraintName("FK_WorkflowVersions_Definitions")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.Versions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
