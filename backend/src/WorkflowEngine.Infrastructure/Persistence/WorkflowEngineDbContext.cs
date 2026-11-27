using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence;

/// <summary>
/// EF Core database context mapped strictly to the enterprise BPM workflow relational schema.
/// Implements <see cref="IDataProtectionKeyContext"/> so ASP.NET Core Data Protection keys
/// persist across restarts and multi-instance deployments.
/// </summary>
public class WorkflowEngineDbContext : DbContext, IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowVersion> WorkflowVersions => Set<WorkflowVersion>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowTask> WorkflowTasks => Set<WorkflowTask>();
    public DbSet<TaskSlot> TaskSlots => Set<TaskSlot>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<DelegationRule> DelegationRules => Set<DelegationRule>();
    public DbSet<WorkflowError> WorkflowInstanceErrors => Set<WorkflowError>();
    public DbSet<WorkflowAuditLog> WorkflowAuditLogs => Set<WorkflowAuditLog>();
    public DbSet<InstanceEvent> InstanceEvents => Set<InstanceEvent>();
    public DbSet<NodeExecution> NodeExecutions => Set<NodeExecution>();
    public DbSet<WorkflowToken> WorkflowTokens => Set<WorkflowToken>();
    public DbSet<JoinState> JoinStates => Set<JoinState>();
    public DbSet<CommandLog> CommandLogs => Set<CommandLog>();
    public DbSet<SmartObjectEntity> SmartObjects => Set<SmartObjectEntity>();
    public DbSet<SmartObjectRecordEntity> SmartObjectRecords => Set<SmartObjectRecordEntity>();
    public DbSet<SmartFormEntity> SmartForms => Set<SmartFormEntity>();
    public DbSet<SmartFormVersion> SmartFormVersions => Set<SmartFormVersion>();
    public DbSet<SmartFormSubmissionEntity> SmartFormSubmissions => Set<SmartFormSubmissionEntity>();
    public DbSet<FormAttachmentEntity> FormAttachments => Set<FormAttachmentEntity>();
    public DbSet<SystemUser> SystemUsers => Set<SystemUser>();
    public DbSet<UserAuditLog> UserAuditLogs => Set<UserAuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<ConnectionPermission> ConnectionPermissions => Set<ConnectionPermission>();
    public DbSet<UserConnectionCredential> UserConnectionCredentials => Set<UserConnectionCredential>();
    public DbSet<ConnectionEnvironmentConfig> ConnectionEnvironmentConfigs => Set<ConnectionEnvironmentConfig>();
    public DbSet<ConnectionUsageLog> ConnectionUsageLogs => Set<ConnectionUsageLog>();
    public DbSet<AuthProvider> AuthProviders => Set<AuthProvider>();
    public DbSet<ExternalUserLogin> ExternalUserLogins => Set<ExternalUserLogin>();
    public DbSet<ExternalUserToken> ExternalUserTokens => Set<ExternalUserToken>();

    public WorkflowEngineDbContext(DbContextOptions<WorkflowEngineDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkflowEngineDbContext).Assembly);

        // PostgreSQL JSONB + GIN indexing for SmartObjects, SmartObjectRecords, and SmartFormSubmissions
        if (Database.IsNpgsql())
        {
            modelBuilder.Entity<SmartObjectEntity>(entity =>
            {
                entity.Property(e => e.FieldsJson).HasColumnType("jsonb");
                entity.Property(e => e.MethodsJson).HasColumnType("jsonb");
            });

            modelBuilder.Entity<SmartObjectRecordEntity>(entity =>
            {
                entity.Property(e => e.DataJson).HasColumnType("jsonb");
                entity.HasIndex(e => e.DataJson)
                    .HasMethod("gin")
                    .HasDatabaseName("IX_SmartObjectRecords_DataJson_Gin");
            });

            modelBuilder.Entity<SmartFormSubmissionEntity>(entity =>
            {
                entity.Property(e => e.DataJson).HasColumnType("jsonb");
                entity.HasIndex(e => e.DataJson)
                    .HasMethod("gin")
                    .HasDatabaseName("IX_SmartFormSubmissions_DataJson_Gin");
            });
        }
    }
}
