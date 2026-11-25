using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class SystemUserConfiguration : IEntityTypeConfiguration<SystemUser>
{
    public void Configure(EntityTypeBuilder<SystemUser> builder)
    {
        builder.ToTable("SystemUsers");

        builder.HasKey(u => u.Id)
            .HasName("PK_SystemUsers");

        builder.Property(u => u.Id)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("UQ_SystemUsers_Email");

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(500)
            .HasDefaultValue("")
            .IsRequired();

        builder.Property(u => u.Role)
            .HasMaxLength(100)
            .HasDefaultValue("End User")
            .IsRequired();

        builder.Property(u => u.Group)
            .HasMaxLength(100)
            .HasDefaultValue("General")
            .IsRequired();

        builder.Property(u => u.Status)
            .HasMaxLength(50)
            .HasDefaultValue("Active")
            .IsRequired();

        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(u => u.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();
    }
}
