namespace TelemetryAnalyzer.App.Models;

public sealed class TelemetrySession
{
    public string Name { get; set; } = string.Empty;

    public string SourceFile { get; set; } = string.Empty;

    public DateTime ImportedAt { get; set; } = DateTime.Now;

    public List<TelemetryRecord> Records { get; set; } = [];

    public int RecordCount => Records.Count;

    public double? MinimumSpeed =>
        GetSpeedValues().DefaultIfEmpty().Any()
            ? GetSpeedValues().Min()
            : null;

    public double? MaximumSpeed =>
        GetSpeedValues().DefaultIfEmpty().Any()
            ? GetSpeedValues().Max()
            : null;

    public double? AverageSpeed =>
        GetSpeedValues().DefaultIfEmpty().Any()
            ? GetSpeedValues().Average()
            : null;

    private IEnumerable<double> GetSpeedValues()
    {
        return Records
            .Where(record => record.Speed.HasValue)
            .Select(record => record.Speed!.Value);
    }
}
