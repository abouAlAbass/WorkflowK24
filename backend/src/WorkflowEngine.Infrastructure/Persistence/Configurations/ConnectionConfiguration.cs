using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class ConnectionConfiguration : IEntityTypeConfiguration<Connection>
{
    public void Configure(EntityTypeBuilder<Connection> builder)
    {
        builder.ToTable("Connections");

        builder.HasKey(c => c.Id)
            .HasName("PK_Connections");

        builder.Property(c => c.Id)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.HasIndex(c => c.Name)
            .IsUnique()
            .HasDatabaseName("UQ_Connections_Name");

        builder.Property(c => c.ServiceType)
            .HasMaxLength(50)
            .HasDefaultValue("rest")
            .IsRequired();

        builder.Property(c => c.BaseUrl)
            .HasMaxLength(1000)
            .HasDefaultValue("")
            .IsRequired();

        builder.Property(c => c.AuthType)
            .HasMaxLength(50)
            .HasDefaultValue("none")
            .IsRequired();

        builder.Property(c => c.AuthMode)
            .HasMaxLength(50)
            .HasDefaultValue("static")
            .IsRequired();

        builder.Property(c => c.SecretSource)
            .HasMaxLength(50)
            .HasDefaultValue("internal_encrypted")
            .IsRequired();

        builder.Property(c => c.SecretRef)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(c => c.EncryptedCredentialsJson)
            .IsRequired(false);

        builder.Property(c => c.ExternalSecretKey)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(c => c.AllowedHostsJson)
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(c => c.DefaultHeadersJson)
            .IsRequired(false);

        builder.Property(c => c.AllowWrite)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(c => c.MaxRows)
            .HasDefaultValue(Connection.DefaultMaxRows)
            .IsRequired();

        builder.Property(c => c.SecretExpiresAt)
            .IsRequired(false);

        builder.Property(c => c.LastRotatedAt)
            .IsRequired(false);

        builder.Property(c => c.HealthStatus)
            .HasMaxLength(32)
            .HasDefaultValue("Unknown")
            .IsRequired();

        builder.Property(c => c.LastTestedAt)
            .IsRequired(false);

        builder.Property(c => c.LastUsedAt)
            .IsRequired(false);

        builder.Property(c => c.LastErrorSanitized)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(c => c.Version)
            .IsConcurrencyToken()
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(c => c.CreatedBy)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasMany(c => c.Permissions)
            .WithOne(p => p.Connection)
            .HasForeignKey(p => p.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.UserCredentials)
            .WithOne(u => u.Connection)
            .HasForeignKey(u => u.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.EnvironmentConfigs)
            .WithOne(e => e.Connection)
            .HasForeignKey(e => e.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.UsageLogs)
            .WithOne(l => l.Connection)
            .HasForeignKey(l => l.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Connection.Permissions))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Connection.UserCredentials))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Connection.EnvironmentConfigs))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Connection.UsageLogs))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
