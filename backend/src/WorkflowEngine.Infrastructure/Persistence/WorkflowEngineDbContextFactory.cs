using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WorkflowEngine.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for standard EF Core CLI migrations tooling.
/// </summary>
public class WorkflowEngineDbContextFactory : IDesignTimeDbContextFactory<WorkflowEngineDbContext>
{
    public WorkflowEngineDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WorkflowEngineDbContext>();

        // Default to SQL Server for design-time migrations generation
        var connectionString = "Server=(localdb)\\mssqllocaldb;Database=WorkflowEngineDb;Trusted_Connection=True;MultipleActiveResultSets=true";
        optionsBuilder.UseSqlServer(connectionString, b =>
        {
            b.MigrationsAssembly(typeof(WorkflowEngineDbContext).Assembly.FullName);
        });

        return new WorkflowEngineDbContext(optionsBuilder.Options);
    }
}
