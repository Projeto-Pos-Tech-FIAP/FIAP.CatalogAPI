using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Domain.Entities;
using FIAP.CatalogAPI.Domain.Interfaces;
using FIAP.CatalogAPI.Infrastructure.Data.Audit;
using Microsoft.EntityFrameworkCore;

namespace FIAP.CatalogAPI.Infrastructure.Data.Context;

public sealed class CatalogDbContext : DbContext
{
    private readonly IAuditService? _auditService;

    public DbSet<Game> Games { get; set; }
    public DbSet<Genre> Genres { get; set; }
    public DbSet<GameGenre> GameGenres { get; set; }
    public DbSet<Library> Libraries { get; set; }
    public DbSet<LibraryGame> LibraryGames { get; set; }

    public CatalogDbContext(DbContextOptions<CatalogDbContext> options, IAuditService? auditService = null)
        : base(options)
    {
        _auditService = auditService;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var auditEntries = CaptureAuditEntries();

        var result = await base.SaveChangesAsync(cancellationToken);

        if (_auditService != null && auditEntries.Count > 0)
        {
            try
            {
                var logs = auditEntries.Select(e => e.ToAuditLog());
                await _auditService.SaveAuditLogsAsync(logs, cancellationToken);
            }
            catch
            {
                // Audit failure must not affect the main operation
            }
        }

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(CatalogDbContext)
                    .GetMethod(nameof(ConfigureSoftDeleteFilter),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    ?.MakeGenericMethod(entityType.ClrType);

                method?.Invoke(null, [modelBuilder]);
            }
        }
    }

    private static void ConfigureSoftDeleteFilter<T>(ModelBuilder modelBuilder) where T : class, ISoftDelete
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted);
    }

    private List<AuditEntryCapture> CaptureAuditEntries()
    {
        if (_auditService is null) return [];

        ChangeTracker.DetectChanges();

        var entries = new List<AuditEntryCapture>();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            entries.Add(new AuditEntryCapture(entry));
        }

        return entries;
    }
}
