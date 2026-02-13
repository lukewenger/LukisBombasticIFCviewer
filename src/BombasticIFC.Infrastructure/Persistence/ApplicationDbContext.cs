using BombasticIFC.Application.Common.Interfaces;
using BombasticIFC.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BombasticIFC.Infrastructure.Persistence;

/// <summary>
/// Database context for the application
/// </summary>
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<IfcModel> IfcModels => Set<IfcModel>();
    public DbSet<ConversionJob> ConversionJobs => Set<ConversionJob>();
    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Global query filters for soft delete
        modelBuilder.Entity<IfcModel>().HasQueryFilter(m => !m.IsDeleted);
        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }
}
