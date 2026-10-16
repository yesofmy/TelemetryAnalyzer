using Microsoft.EntityFrameworkCore;
using TelemetryAnalyzer.App.Models;

namespace TelemetryAnalyzer.App.Data;

public sealed class TelemetryDbContext : DbContext
{
    public DbSet<TelemetrySession> Sessions => Set<TelemetrySession>();

    public DbSet<TelemetryRecord> Records => Set<TelemetryRecord>();

    public TelemetryDbContext(DbContextOptions<TelemetryDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TelemetrySession>(entity =>
        {
            entity.HasKey(session => session.Id);

            entity.Property(session => session.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(session => session.SourceFile)
                .IsRequired();

            entity.HasMany(session => session.Records)
                .WithOne(record => record.Session)
                .HasForeignKey(record => record.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TelemetryRecord>(entity =>
        {
            entity.HasKey(record => record.Id);

            entity.HasIndex(record => new
            {
                record.SessionId,
                record.Index
            });
        });
    }
}
