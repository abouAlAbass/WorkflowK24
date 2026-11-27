using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class UserConnectionCredentialConfiguration : IEntityTypeConfiguration<UserConnectionCredential>
{
    public void Configure(EntityTypeBuilder<UserConnectionCredential> builder)
    {
        builder.ToTable("UserConnectionCredentials");

        builder.HasKey(u => u.Id)
            .HasName("PK_UserConnectionCredentials");

        builder.Property(u => u.Id)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.UserId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.ConnectionId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.EncryptedSecretJson)
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasIndex(u => new { u.UserId, u.ConnectionId })
            .IsUnique()
            .HasDatabaseName("UQ_UserConnectionCredentials_User_Connection");
    }
}
