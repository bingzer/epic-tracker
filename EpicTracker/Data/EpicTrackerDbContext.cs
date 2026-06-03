using Microsoft.EntityFrameworkCore;

namespace EpicTracker.Data;

public class EpicTrackerDbContext(DbContextOptions<EpicTrackerDbContext> options) : DbContext(options)
{
    public DbSet<EpicEntity> Epics => Set<EpicEntity>();
    public DbSet<SpecEntity> Specs => Set<SpecEntity>();
    public DbSet<EpicAuditEntity> EpicAudits => Set<EpicAuditEntity>();

    public async Task<EpicEntity> FindEpicOrThrow(string epicId, CancellationToken cancellationToken = default)
    {
        var entity = await Epics
            .Include(e => e.Specs.Where(s => !s.IsAbandoned))
            .FirstOrDefaultAsync(e => e.Id == epicId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"Epic not found: {epicId}");
        }

        return entity;
    }

    public async Task<SpecEntity> FindSpecOrThrow(string specId, CancellationToken cancellationToken = default)
    {
        var entity = await Specs.FirstOrDefaultAsync(e => e.Id == specId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"Spec not found: {specId}");
        }

        return entity;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EpicEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CodingAgents).HasColumnType("TEXT");
            e.HasMany(x => x.Specs).WithOne(x => x.Epic).HasForeignKey(x => x.EpicId);
            e.HasMany(x => x.Audits).WithOne(x => x.Epic).HasForeignKey(x => x.EpicId);
        });

        modelBuilder.Entity<SpecEntity>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<EpicAuditEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
        });
    }
}

