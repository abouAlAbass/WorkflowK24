using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence;

/// <summary>
/// EF Core database context mapped strictly to the enterprise BPM workflow relational schema.
/// </summary>
public class WorkflowEngineDbContext : DbContext
{
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowVersion> WorkflowVersions => Set<WorkflowVersion>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowTask> WorkflowTasks => Set<WorkflowTask>();
    public DbSet<WorkflowError> WorkflowInstanceErrors => Set<WorkflowError>();
    public DbSet<WorkflowAuditLog> WorkflowAuditLogs => Set<WorkflowAuditLog>();
    public DbSet<InstanceEvent> InstanceEvents => Set<InstanceEvent>();
    public DbSet<NodeExecution> NodeExecutions => Set<NodeExecution>();
    public DbSet<CommandLog> CommandLogs => Set<CommandLog>();
    public DbSet<SmartObjectEntity> SmartObjects => Set<SmartObjectEntity>();
    public DbSet<SmartObjectRecordEntity> SmartObjectRecords => Set<SmartObjectRecordEntity>();
    public DbSet<SmartFormEntity> SmartForms => Set<SmartFormEntity>();
    public DbSet<SmartFormVersion> SmartFormVersions => Set<SmartFormVersion>();
    public DbSet<SmartFormSubmissionEntity> SmartFormSubmissions => Set<SmartFormSubmissionEntity>();
    public DbSet<FormAttachmentEntity> FormAttachments => Set<FormAttachmentEntity>();
    public DbSet<SystemUser> SystemUsers => Set<SystemUser>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public WorkflowEngineDbContext(DbContextOptions<WorkflowEngineDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkflowEngineDbContext).Assembly);
    }
}
