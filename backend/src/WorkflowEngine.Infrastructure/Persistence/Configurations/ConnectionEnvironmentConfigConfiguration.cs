using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class ConnectionEnvironmentConfigConfiguration : IEntityTypeConfiguration<ConnectionEnvironmentConfig>
{
    public void Configure(EntityTypeBuilder<ConnectionEnvironmentConfig> builder)
    {
        builder.ToTable("ConnectionEnvironmentConfigs");

        builder.HasKey(e => e.Id)
            .HasName("PK_ConnectionEnvironmentConfigs");

        builder.Property(e => e.Id)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ConnectionId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.EnvironmentName)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.BaseUrlOverride)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(e => e.EncryptedCredentialsJsonOverride)
            .IsRequired(false);

        builder.Property(e => e.ExternalSecretKeyOverride)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasIndex(e => new { e.ConnectionId, e.EnvironmentName })
            .IsUnique()
            .HasDatabaseName("UQ_ConnectionEnvironmentConfigs_Connection_Env");
    }
}
