using Microsoft.EntityFrameworkCore;

namespace SensorMon.Worker;

// The DbContext is EF Core's main object: it represents a session with the
// database and exposes your tables as DbSet<T> properties.
public sealed class SensorDbContext : DbContext
{
    public SensorDbContext(DbContextOptions<SensorDbContext> options) : base(options) { }

    // Maps to a "Readings" table.
    public DbSet<Reading> Readings => Set<Reading>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // An index on timestamp makes the nightly cleanup DELETE and any
        // time-range queries fast. Good thing to be able to explain.
        modelBuilder.Entity<Reading>()
            .HasIndex(r => r.Timestamp);

        // Store wall-clock local time, not UTC. Npgsql maps DateTime to
        // `timestamp with time zone` by default (which requires Kind=Utc);
        // we override to `timestamp without time zone` so a Kind=Local value
        // from DateTime.Now is stored as-is with no offset conversion.
        modelBuilder.Entity<Reading>()
            .Property(r => r.Timestamp)
            .HasColumnType("timestamp without time zone");
    }
}
