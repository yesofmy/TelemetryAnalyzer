using Microsoft.EntityFrameworkCore;
using TelemetryAnalyzer.App.Models;
using System.IO;

namespace TelemetryAnalyzer.App.Data;

public sealed class TelemetryDatabase
{
    private readonly DbContextOptions<TelemetryDbContext> _options;

    public string DatabasePath { get; }

    public TelemetryDatabase()
    {
        string appFolder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "TelemetryAnalyzer");

        Directory.CreateDirectory(appFolder);

        DatabasePath = Path.Combine(appFolder, "telemetry.db");

        _options = new DbContextOptionsBuilder<TelemetryDbContext>()
            .UseSqlite($"Data Source={DatabasePath}")
            .Options;
    }

    public TelemetryDbContext CreateContext()
    {
        return new TelemetryDbContext(_options);
    }

    public void Initialize()
    {
        using TelemetryDbContext context = CreateContext();
        context.Database.EnsureCreated();
    }

    public TelemetrySession SaveSession(TelemetrySession session)
    {
        using TelemetryDbContext context = CreateContext();

        session.Id = 0;

        foreach (TelemetryRecord record in session.Records)
        {
            record.Id = 0;
            record.SessionId = 0;
            record.Session = null;
        }

        context.Sessions.Add(session);
        context.SaveChanges();

        return session;
    }

    public List<TelemetrySession> GetSessions()
    {
        using TelemetryDbContext context = CreateContext();

        return context.Sessions
            .AsNoTracking()
            .Include(session => session.Records)
            .OrderByDescending(session => session.ImportedAt)
            .ToList();
    }
}
