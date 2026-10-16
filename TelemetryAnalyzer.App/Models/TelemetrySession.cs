using System.ComponentModel.DataAnnotations.Schema;

namespace TelemetryAnalyzer.App.Models;

public sealed class TelemetrySession
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SourceFile { get; set; } = string.Empty;

    public DateTime ImportedAt { get; set; } = DateTime.Now;

    public List<TelemetryRecord> Records { get; set; } = [];

    [NotMapped]
    public int RecordCount => Records.Count;

    [NotMapped]
    public double? MinimumSpeed =>
        Records
            .Where(record => record.Speed.HasValue)
            .Select(record => record.Speed!.Value)
            .DefaultIfEmpty()
            .Any()
                ? Records
                    .Where(record => record.Speed.HasValue)
                    .Select(record => record.Speed!.Value)
                    .Min()
                : null;

    [NotMapped]
    public double? MaximumSpeed =>
        Records
            .Where(record => record.Speed.HasValue)
            .Select(record => record.Speed!.Value)
            .DefaultIfEmpty()
            .Any()
                ? Records
                    .Where(record => record.Speed.HasValue)
                    .Select(record => record.Speed!.Value)
                    .Max()
                : null;

    [NotMapped]
    public double? AverageSpeed =>
        Records.Any(record => record.Speed.HasValue)
            ? Records
                .Where(record => record.Speed.HasValue)
                .Average(record => record.Speed!.Value)
            : null;
}
