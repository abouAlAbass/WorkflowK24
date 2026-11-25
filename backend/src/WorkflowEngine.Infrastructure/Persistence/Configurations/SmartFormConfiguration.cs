using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class SmartFormConfiguration : IEntityTypeConfiguration<SmartFormEntity>
{
    public void Configure(EntityTypeBuilder<SmartFormEntity> builder)
    {
        builder.ToTable("SmartForms");

        builder.HasKey(f => f.Id)
            .HasName("PK_SmartForms");

        builder.Property(f => f.Id)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(f => f.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(f => f.Title)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(f => f.Description)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(f => f.Category)
            .HasMaxLength(100)
            .HasDefaultValue("General")
            .IsRequired();

        builder.Property(f => f.LinkedSmartObjectId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(f => f.LinkedWorkflowKey)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(f => f.Status)
            .HasMaxLength(50)
            .HasDefaultValue("Published")
            .IsRequired();

        builder.Property(f => f.FieldsJson)
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(f => f.SubmissionsCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(f => f.ActivePublishedVersionId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(f => f.LatestVersionNumber)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(f => f.Version)
            .HasDefaultValue(1)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(f => f.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.Property(f => f.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasMany(f => f.Versions)
            .WithOne(v => v.SmartForm)
            .HasForeignKey(v => v.SmartFormId)
            .HasConstraintName("FK_SmartFormVersions_SmartForms")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(f => f.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
