namespace TelemetryAnalyzer.App.Models;

public sealed class TelemetryRecord
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public TelemetrySession? Session { get; set; }

    public int Index { get; set; }

    public double? Time { get; set; }

    public double? Speed { get; set; }

    public double? RPM { get; set; }

    public int? Gear { get; set; }

    public double? Throttle { get; set; }

    public double? Brake { get; set; }

    public double? Distance { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }
}
