using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class ExternalUserLoginConfiguration : IEntityTypeConfiguration<ExternalUserLogin>
{
    public void Configure(EntityTypeBuilder<ExternalUserLogin> builder)
    {
        builder.ToTable("ExternalUserLogins");

        builder.HasKey(e => e.Id)
            .HasName("PK_ExternalUserLogins");

        builder.Property(e => e.Id)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ProviderId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ExternalSubject)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.Email)
            .HasMaxLength(255)
            .HasDefaultValue("")
            .IsRequired();

        builder.Property(e => e.LastLoginAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasIndex(e => new { e.ProviderId, e.ExternalSubject })
            .IsUnique()
            .HasDatabaseName("UQ_ExternalUserLogins_Provider_Sub");

        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_ExternalUserLogins_UserId");

        builder.HasOne<SystemUser>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ExternalUserLogins_SystemUsers");

        builder.HasOne<AuthProvider>()
            .WithMany()
            .HasForeignKey(e => e.ProviderId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ExternalUserLogins_AuthProviders");
    }
}
