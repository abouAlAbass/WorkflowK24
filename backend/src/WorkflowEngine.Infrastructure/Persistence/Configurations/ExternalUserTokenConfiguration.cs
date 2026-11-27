using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class ExternalUserTokenConfiguration : IEntityTypeConfiguration<ExternalUserToken>
{
    public void Configure(EntityTypeBuilder<ExternalUserToken> builder)
    {
        builder.ToTable("ExternalUserTokens");

        builder.HasKey(t => t.Id)
            .HasName("PK_ExternalUserTokens");

        builder.Property(t => t.Id)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.UserId)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(t => t.ProviderId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.EncryptedAccessToken)
            .IsRequired();

        builder.Property(t => t.EncryptedRefreshToken)
            .IsRequired(false);

        builder.Property(t => t.ExpiresAt)
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasIndex(t => new { t.UserId, t.ProviderId })
            .IsUnique()
            .HasDatabaseName("UQ_ExternalUserTokens_User_Provider");

        builder.HasIndex(t => t.UserId)
            .HasDatabaseName("IX_ExternalUserTokens_UserId");
    }
}
