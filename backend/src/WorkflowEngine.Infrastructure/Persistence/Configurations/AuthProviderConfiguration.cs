using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class AuthProviderConfiguration : IEntityTypeConfiguration<AuthProvider>
{
    public void Configure(EntityTypeBuilder<AuthProvider> builder)
    {
        builder.ToTable("AuthProviders");

        builder.HasKey(p => p.Id)
            .HasName("PK_AuthProviders");

        builder.Property(p => p.Id)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.HasIndex(p => p.Name)
            .IsUnique()
            .HasDatabaseName("UQ_AuthProviders_Name");

        builder.Property(p => p.Protocol)
            .HasMaxLength(50)
            .HasDefaultValue(AuthProvider.DefaultProtocol)
            .IsRequired();

        builder.Property(p => p.Authority)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(p => p.ClientId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.EncryptedClientSecret)
            .IsRequired(false);

        builder.Property(p => p.Scopes)
            .HasMaxLength(500)
            .HasDefaultValue(AuthProvider.DefaultScopes)
            .IsRequired();

        builder.Property(p => p.RoleMappingJson)
            .HasDefaultValue("{}")
            .IsRequired();

        builder.Property(p => p.JitProvisioningEnabled)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(p => p.AllowEmailMatching)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(p => p.AllowAdminRoleMapping)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(p => p.DefaultRole)
            .HasMaxLength(100)
            .HasDefaultValue(AuthProvider.DefaultUserRole)
            .IsRequired();

        builder.Property(p => p.IsEnabled)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(p => p.Version)
            .IsConcurrencyToken()
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();
    }
}
