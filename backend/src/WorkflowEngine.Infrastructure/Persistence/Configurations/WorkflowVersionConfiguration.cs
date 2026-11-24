using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class WorkflowVersionConfiguration : IEntityTypeConfiguration<WorkflowVersion>
{
    public void Configure(EntityTypeBuilder<WorkflowVersion> builder)
    {
        builder.ToTable("WorkflowVersions");

        builder.HasKey(v => v.VersionId)
            .HasName("PK_WorkflowVersions");

        builder.Property(v => v.VersionId)
            .ValueGeneratedOnAdd();

        builder.Property(v => v.DefinitionId)
            .IsRequired();

        builder.Property(v => v.VersionNumber)
            .IsRequired();

        builder.Property(v => v.GraphJson)
            .IsRequired();

        builder.Property(v => v.Changelog)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(v => v.IsActive)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(v => v.PublishedBy)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(v => v.PublishedAt)
            .IsRequired();

        builder.HasIndex(v => new { v.DefinitionId, v.VersionNumber })
            .IsUnique()
            .HasDatabaseName("UQ_WorkflowVersions_Def_Ver");
    }
}
