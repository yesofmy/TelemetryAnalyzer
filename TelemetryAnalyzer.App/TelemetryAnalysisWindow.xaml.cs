using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using TelemetryAnalyzer.App.Models;

namespace TelemetryAnalyzer.App
{
    public partial class TelemetryAnalysisWindow : Window
    {
        private readonly TelemetrySession _session;
        private List<TelemetryRecord> _records = new();
        private List<AnomalyItem> _anomalies = new();

        private static readonly Brush GridBrush =
            new SolidColorBrush(Color.FromRgb(43, 49, 59));

        private static readonly Brush LabelBrush =
            new SolidColorBrush(Color.FromRgb(155, 163, 175));

        public TelemetryAnalysisWindow(TelemetrySession session)
        {
            InitializeComponent();

            _session = session ??
                throw new ArgumentNullException(nameof(session));

            Loaded += TelemetryAnalysisWindow_Loaded;
        }

        private void TelemetryAnalysisWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            _records = _session.Records
                .OrderBy(record => record.Index)
                .ToList();

            UpdateStatistics();
            DetectAnomalies();
            DrawAllCharts();
        }

        // ---------------------------------------------------------
        // STATISTICS
        // ---------------------------------------------------------

        private void UpdateStatistics()
        {
            SessionNameText.Text = string.IsNullOrWhiteSpace(_session.Name)
                ? "Telemetry Session"
                : _session.Name;

            SessionDetailsText.Text =
                $"Source: {_session.SourceFile}  |  Imported: " +
                _session.ImportedAt.ToString("yyyy-MM-dd HH:mm");

            RecordCountText.Text = _records.Count.ToString("N0");

            List<double> speeds = GetValues(r => r.Speed);
            List<double> rpms = GetValues(r => r.RPM);
            List<double> throttles = GetValues(r => r.Throttle);
            List<double> brakes = GetValues(r => r.Brake);
            List<double> distances = GetValues(r => r.Distance);

            MaxSpeedText.Text = FormatValue(speeds.Count > 0 ? speeds.Max() : null);
            AverageSpeedText.Text = FormatValue(speeds.Count > 0 ? speeds.Average() : null);
            MinSpeedText.Text = FormatValue(speeds.Count > 0 ? speeds.Min() : null);

            MaxRpmText.Text = rpms.Count > 0
                ? rpms.Max().ToString("N0")
                : "N/A";

            AverageRpmText.Text = rpms.Count > 0
                ? rpms.Average().ToString("N0")
                : "N/A";

            AverageThrottleText.Text = FormatPercentage(
                throttles.Count > 0 ? throttles.Average() : null);

            AverageBrakeText.Text = FormatPercentage(
                brakes.Count > 0 ? brakes.Average() : null);

            var times = GetValues(r => r.Time);

            if (times.Count >= 2)
            {
                double duration = times.Max() - times.Min();

                DurationText.Text = duration >= 0
                    ? FormatDuration(duration)
                    : "N/A";
            }
            else
            {
                DurationText.Text = "N/A";
            }

            DistanceText.Text = distances.Count > 0
                ? FormatDistanceRange(distances)
                : "N/A";

            int gearCount = _records
                .Where(r => r.Gear.HasValue)
                .Select(r => r.Gear!.Value)
                .Distinct()
                .Count();

            GearsText.Text = gearCount > 0
                ? gearCount.ToString()
                : "N/A";

            int channelCount = 0;

            if (speeds.Count > 0) channelCount++;
            if (rpms.Count > 0) channelCount++;
            if (throttles.Count > 0) channelCount++;
            if (brakes.Count > 0) channelCount++;
            if (distances.Count > 0) channelCount++;
            if (_records.Any(r => r.Gear.HasValue)) channelCount++;

            ChannelCountText.Text = channelCount.ToString();
        }

        private List<double> GetValues(
            Func<TelemetryRecord, double?> selector)
        {
            return _records
                .Select(selector)
                .Where(value => value.HasValue &&
                                double.IsFinite(value.Value))
                .Select(value => value!.Value)
                .ToList();
        }

        private static string FormatValue(double? value)
        {
            return value.HasValue && double.IsFinite(value.Value)
                ? value.Value.ToString("N1")
                : "N/A";
        }

        private static string FormatPercentage(double? value)
        {
            if (!value.HasValue)
                return "N/A";

            // Accommodate both 0–1 and 0–100 source formats.
            double percentage = value.Value <= 1.0
                ? value.Value * 100.0
                : value.Value;

            return $"{percentage:N1}%";
        }

        private static string FormatDuration(double seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);

            if (seconds >= 3600)
            {
                return $"{(int)time.TotalHours:D2}:" +
                       $"{time.Minutes:D2}:{time.Seconds:D2}";
            }

            return $"{(int)time.TotalMinutes:D2}:" +
                   $"{time.Seconds:D2}.{time.Milliseconds / 10:D2}";
        }

        private static string FormatDistanceRange(List<double> values)
        {
            double range = values.Max() - values.Min();

            if (range >= 1000)
                return $"{range / 1000.0:N2} km";

            return $"{range:N1} units";
        }

        // ---------------------------------------------------------
        // ANOMALY DETECTION
        // ---------------------------------------------------------

        private void DetectAnomalies()
        {
            _anomalies.Clear();

            for (int i = 1; i < _records.Count; i++)
            {
                TelemetryRecord previous = _records[i - 1];
                TelemetryRecord current = _records[i];

                CheckChange(
                    previous.Speed,
                    current.Speed,
                    current.Index,
                    "Speed",
                    35.0,
                    "Large speed change between consecutive records.");

                CheckChange(
                    previous.RPM,
                    current.RPM,
                    current.Index,
                    "RPM",
                    1500.0,
                    "Large engine speed change between consecutive records.");

                CheckPercentageChange(
                    previous.Throttle,
                    current.Throttle,
                    current.Index,
                    "Throttle",
                    "Large throttle input change between consecutive records.");

                CheckPercentageChange(
                    previous.Brake,
                    current.Brake,
                    current.Index,
                    "Brake",
                    "Large brake input change between consecutive records.");
            }

            AnomaliesGrid.ItemsSource = null;
            AnomaliesGrid.ItemsSource = _anomalies;

            AnomalyCountText.Text = _anomalies.Count.ToString();

            AnomalyStatusText.Text = _anomalies.Count == 0
                ? "No potential anomalies detected"
                : $"{_anomalies.Count} potential anomalies detected";
        }

        private void CheckChange(
            double? previous,
            double? current,
            int recordIndex,
            string channel,
            double threshold,
            string description)
        {
            if (!IsValid(previous) || !IsValid(current))
                return;

            double change = current!.Value - previous!.Value;

            if (Math.Abs(change) < threshold)
                return;

            _anomalies.Add(new AnomalyItem
            {
                RecordIndex = recordIndex,
                Channel = channel,
                Value = current.Value.ToString("N2"),
                Change = change.ToString("+0.00;-0.00;0.00"),
                Description = description
            });
        }

        private void CheckPercentageChange(
            double? previous,
            double? current,
            int recordIndex,
            string channel,
            string description)
        {
            if (!IsValid(previous) || !IsValid(current))
                return;

            double scale = Math.Max(
                Math.Abs(previous!.Value),
                Math.Abs(current!.Value)) <= 1.0
                    ? 100.0
                    : 1.0;

            double change = (current.Value - previous.Value) * scale;

            if (Math.Abs(change) < 50.0)
                return;

            _anomalies.Add(new AnomalyItem
            {
                RecordIndex = recordIndex,
                Channel = channel,
                Value = FormatPercentage(current.Value),
                Change = $"{change:+0.0;-0.0;0.0}%",
                Description = description
            });
        }

        private static bool IsValid(double? value)
        {
            return value.HasValue && double.IsFinite(value.Value);
        }

        // ---------------------------------------------------------
        // CHARTS
        // ---------------------------------------------------------

        private void ChartCanvas_SizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            DrawAllCharts();
        }

        private void DrawAllCharts()
        {
            if (!IsLoaded || _records.Count == 0)
                return;

            DrawChart(
                SpeedCanvas,
                _records
                    .Where(r => IsValid(r.Speed))
                    .Select(r => new ChartPoint(GetXAxis(r), r.Speed!.Value))
                    .ToList(),
                Color.FromRgb(255, 159, 67),
                "No speed data");

            DrawChart(
                ThrottleCanvas,
                _records
                    .Where(r => IsValid(r.Throttle))
                    .Select(r => new ChartPoint(GetXAxis(r), r.Throttle!.Value))
                    .ToList(),
                Color.FromRgb(91, 192, 190),
                "No throttle data");

            DrawChart(
                BrakeCanvas,
                _records
                    .Where(r => IsValid(r.Brake))
                    .Select(r => new ChartPoint(GetXAxis(r), r.Brake!.Value))
                    .ToList(),
                Color.FromRgb(239, 100, 97),
                "No brake data");

            DrawChart(
                RpmCanvas,
                _records
                    .Where(r => IsValid(r.RPM))
                    .Select(r => new ChartPoint(GetXAxis(r), r.RPM!.Value))
                    .ToList(),
                Color.FromRgb(167, 139, 250),
                "No RPM data");
        }

        private static double GetXAxis(TelemetryRecord record)
        {
            return IsValid(record.Time)
                ? record.Time!.Value
                : record.Index;
        }

        private void DrawChart(
            Canvas canvas,
            List<ChartPoint> points,
            Color lineColor,
            string emptyMessage)
        {
            canvas.Children.Clear();

            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;

            if (width < 80 || height < 60)
                return;

            const double left = 48;
            const double right = 12;
            const double top = 12;
            const double bottom = 28;

            double plotWidth = width - left - right;
            double plotHeight = height - top - bottom;

            if (plotWidth <= 0 || plotHeight <= 0)
                return;

            for (int i = 0; i <= 4; i++)
            {
                double y = top + plotHeight * i / 4.0;

                canvas.Children.Add(new Line
                {
                    X1 = left,
                    Y1 = y,
                    X2 = width - right,
                    Y2 = y,
                    Stroke = GridBrush,
                    StrokeThickness = 1
                });
            }

            if (points.Count < 2)
            {
                AddText(
                    canvas,
                    points.Count == 1
                        ? "Only one valid data point"
                        : emptyMessage,
                    left + 8,
                    top + 15,
                    LabelBrush,
                    12);

                return;
            }

            double minX = points.Min(p => p.X);
            double maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);

            if (maxX <= minX)
                maxX = minX + 1;

            if (maxY <= minY)
            {
                double padding = Math.Max(1, Math.Abs(maxY) * 0.05);
                minY -= padding;
                maxY += padding;
            }

            double yPadding = (maxY - minY) * 0.05;
            minY -= yPadding;
            maxY += yPadding;

            Point Transform(ChartPoint point)
            {
                double px = left +
                    (point.X - minX) / (maxX - minX) * plotWidth;

                double py = top + plotHeight -
                    (point.Y - minY) / (maxY - minY) * plotHeight;

                return new Point(px, py);
            }

            AddText(canvas, maxY.ToString("N1"), 2, top - 5, LabelBrush, 10);
            AddText(canvas, minY.ToString("N1"), 2,
                top + plotHeight - 10, LabelBrush, 10);

            AddText(canvas, minX.ToString("N1"), left,
                height - bottom + 7, LabelBrush, 10);

            AddText(canvas, maxX.ToString("N1"), width - right - 48,
                height - bottom + 7, LabelBrush, 10);

            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush(lineColor),
                StrokeThickness = 1.8,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            foreach (ChartPoint point in points.OrderBy(p => p.X))
                polyline.Points.Add(Transform(point));

            canvas.Children.Add(polyline);

            AddText(
                canvas,
                "Time / sample",
                left + plotWidth / 2 - 35,
                height - 15,
                LabelBrush,
                9);
        }

        private static void AddText(
            Canvas canvas,
            string text,
            double x,
            double y,
            Brush foreground,
            double fontSize)
        {
            var label = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                FontSize = fontSize,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y);
            canvas.Children.Add(label);
        }

        // ---------------------------------------------------------
        // CSV EXPORT
        // ---------------------------------------------------------

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export telemetry analysis",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = CreateFileName() + "_analysis.csv",
                DefaultExt = ".csv"
            };

            if (dialog.ShowDialog() != true)
                return;

            var csv = new StringBuilder();

            csv.AppendLine("Telemetry Analysis");
            csv.AppendLine($"Session,{CsvEscape(_session.Name)}");
            csv.AppendLine($"Records,{_records.Count}");
            csv.AppendLine($"Maximum Speed,{GetSummaryValue(r => r.Speed, "max")}");
            csv.AppendLine($"Average Speed,{GetSummaryValue(r => r.Speed, "avg")}");
            csv.AppendLine($"Minimum Speed,{GetSummaryValue(r => r.Speed, "min")}");
            csv.AppendLine($"Maximum RPM,{GetSummaryValue(r => r.RPM, "max")}");
            csv.AppendLine($"Average RPM,{GetSummaryValue(r => r.RPM, "avg")}");
            csv.AppendLine($"Average Throttle,{GetSummaryValue(r => r.Throttle, "avg")}");
            csv.AppendLine($"Average Brake,{GetSummaryValue(r => r.Brake, "avg")}");
            csv.AppendLine($"Potential Anomalies,{_anomalies.Count}");
            csv.AppendLine();
            csv.AppendLine("Record,Channel,Value,Change,Description");

            foreach (AnomalyItem anomaly in _anomalies)
            {
                csv.AppendLine(string.Join(",",
                    anomaly.RecordIndex,
                    CsvEscape(anomaly.Channel),
                    CsvEscape(anomaly.Value),
                    CsvEscape(anomaly.Change),
                    CsvEscape(anomaly.Description)));
            }

            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));

            MessageBox.Show(
                "CSV report was exported successfully.",
                "Export Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ---------------------------------------------------------
        // JSON EXPORT
        // ---------------------------------------------------------

        private void ExportJson_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export telemetry analysis",
                Filter = "JSON files (*.json)|*.json",
                FileName = CreateFileName() + "_analysis.json",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() != true)
                return;

            var report = new
            {
                Session = new
                {
                    _session.Name,
                    _session.SourceFile,
                    _session.ImportedAt
                },
                Statistics = new
                {
                    RecordCount = _records.Count,
                    MaximumSpeed = GetSummaryNumber(r => r.Speed, "max"),
                    AverageSpeed = GetSummaryNumber(r => r.Speed, "avg"),
                    MinimumSpeed = GetSummaryNumber(r => r.Speed, "min"),
                    MaximumRpm = GetSummaryNumber(r => r.RPM, "max"),
                    AverageRpm = GetSummaryNumber(r => r.RPM, "avg"),
                    AverageThrottle = GetSummaryNumber(r => r.Throttle, "avg"),
                    AverageBrake = GetSummaryNumber(r => r.Brake, "avg"),
                    DurationSeconds = GetDurationSeconds(),
                    DistanceRange = GetDistanceRange(),
                    GearsUsed = _records
                        .Where(r => r.Gear.HasValue)
                        .Select(r => r.Gear!.Value)
                        .Distinct()
                        .OrderBy(g => g)
                        .ToList(),
                    PotentialAnomalyCount = _anomalies.Count
                },
                Anomalies = _anomalies
            };

            string json = JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            File.WriteAllText(dialog.FileName, json, Encoding.UTF8);

            MessageBox.Show(
                "JSON report was exported successfully.",
                "Export Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ---------------------------------------------------------
        // HTML EXPORT
        // ---------------------------------------------------------

        private void ExportHtml_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export HTML analysis report",
                Filter = "HTML files (*.html)|*.html",
                FileName = CreateFileName() + "_report.html",
                DefaultExt = ".html"
            };

            if (dialog.ShowDialog() != true)
                return;

            var html = new StringBuilder();

            html.Append("""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>Telemetry Analysis Report</title>
                <style>
                body{font-family:Arial,sans-serif;background:#111318;color:#f0f2f5;margin:35px}
                h1{color:#ff9f43}h2{margin-top:35px;color:#ff9f43}
                .muted{color:#9299a5}
                .grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:12px}
                .card{background:#1b2028;border:1px solid #303641;border-radius:8px;padding:16px}
                .value{font-size:23px;font-weight:bold;margin-top:8px}
                table{width:100%;border-collapse:collapse;margin-top:15px;background:#1b2028}
                th,td{text-align:left;padding:11px;border-bottom:1px solid #303641}
                th{background:#252a33;color:#ff9f43}
                tr:nth-child(even){background:#202630}
                </style>
                </head>
                <body>
                """);

            html.Append("<h1>Telemetry Analysis Report</h1>");
            html.Append("<p class='muted'>Session: ");
            html.Append(WebUtility.HtmlEncode(_session.Name));
            html.Append("<br>Source: ");
            html.Append(WebUtility.HtmlEncode(_session.SourceFile));
            html.Append("<br>Imported: ");
            html.Append(WebUtility.HtmlEncode(
                _session.ImportedAt.ToString("yyyy-MM-dd HH:mm")));
            html.Append("</p>");

            html.Append("<h2>Session Statistics</h2><div class='grid'>");

            AddHtmlCard(html, "Data Records", _records.Count.ToString("N0"));
            AddHtmlCard(html, "Maximum Speed", GetSummaryValue(r => r.Speed, "max"));
            AddHtmlCard(html, "Average Speed", GetSummaryValue(r => r.Speed, "avg"));
            AddHtmlCard(html, "Minimum Speed", GetSummaryValue(r => r.Speed, "min"));
            AddHtmlCard(html, "Maximum RPM", GetSummaryValue(r => r.RPM, "max"));
            AddHtmlCard(html, "Average RPM", GetSummaryValue(r => r.RPM, "avg"));
            AddHtmlCard(html, "Average Throttle", FormatPercentage(GetSummaryNumber(r => r.Throttle, "avg")));
            AddHtmlCard(html, "Average Brake", FormatPercentage(GetSummaryNumber(r => r.Brake, "avg")));
            AddHtmlCard(html, "Duration", GetDurationSeconds()?.ToString("N2") is string duration ? duration + " s" : "N/A");
            AddHtmlCard(html, "Distance Range", GetDistanceRange()?.ToString("N2") ?? "N/A");
            AddHtmlCard(html, "Potential Anomalies", _anomalies.Count.ToString());

            html.Append("</div>");
            html.Append("<h2>Potential Anomalies</h2>");

            if (_anomalies.Count == 0)
            {
                html.Append("<p class='muted'>No potential anomalies were detected.</p>");
            }
            else
            {
                html.Append("""
                    <table>
                    <thead><tr>
                    <th>Record</th><th>Channel</th><th>Value</th>
                    <th>Change</th><th>Description</th>
                    </tr></thead><tbody>
                    """);

                foreach (AnomalyItem anomaly in _anomalies)
                {
                    html.Append("<tr>");
                    html.Append($"<td>{anomaly.RecordIndex}</td>");
                    html.Append($"<td>{WebUtility.HtmlEncode(anomaly.Channel)}</td>");
                    html.Append($"<td>{WebUtility.HtmlEncode(anomaly.Value)}</td>");
                    html.Append($"<td>{WebUtility.HtmlEncode(anomaly.Change)}</td>");
                    html.Append($"<td>{WebUtility.HtmlEncode(anomaly.Description)}</td>");
                    html.Append("</tr>");
                }

                html.Append("</tbody></table>");
            }

            html.Append("""
                <p class="muted" style="margin-top:35px">
                Anomaly detection uses heuristic thresholds to identify unusual
                changes between consecutive records. These results do not
                establish the presence of a vehicle fault.
                </p>
                </body></html>
                """);

            File.WriteAllText(dialog.FileName, html.ToString(), Encoding.UTF8);

            MessageBox.Show(
                "HTML report was exported successfully.",
                "Export Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private static void AddHtmlCard(
            StringBuilder html,
            string title,
            string value)
        {
            html.Append("<div class='card'><div class='muted'>");
            html.Append(WebUtility.HtmlEncode(title));
            html.Append("</div><div class='value'>");
            html.Append(WebUtility.HtmlEncode(value));
            html.Append("</div></div>");
        }

        // ---------------------------------------------------------
        // EXPORT HELPERS
        // ---------------------------------------------------------

        private double? GetSummaryNumber(
            Func<TelemetryRecord, double?> selector,
            string operation)
        {
            List<double> values = GetValues(selector);

            if (values.Count == 0)
                return null;

            return operation switch
            {
                "min" => values.Min(),
                "max" => values.Max(),
                "avg" => values.Average(),
                _ => null
            };
        }

        private string GetSummaryValue(
            Func<TelemetryRecord, double?> selector,
            string operation)
        {
            double? value = GetSummaryNumber(selector, operation);
            return value?.ToString("N2") ?? "N/A";
        }

        private double? GetDurationSeconds()
        {
            List<double> times = GetValues(r => r.Time);

            return times.Count >= 2
                ? times.Max() - times.Min()
                : null;
        }

        private double? GetDistanceRange()
        {
            List<double> distances = GetValues(r => r.Distance);

            return distances.Count > 0
                ? distances.Max() - distances.Min()
                : null;
        }

        private static string CsvEscape(string? value)
        {
            value ??= string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private string CreateFileName()
        {
            string name = string.IsNullOrWhiteSpace(_session.Name)
                ? "telemetry_session"
                : _session.Name;

            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name.Replace(' ', '_');
        }

        // ---------------------------------------------------------
        // INTERNAL MODELS
        // ---------------------------------------------------------

        public sealed class AnomalyItem
        {
            public int RecordIndex { get; set; }
            public string Channel { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public string Change { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        private sealed class ChartPoint
        {
            public double X { get; }
            public double Y { get; }

            public ChartPoint(double x, double y)
            {
                X = x;
                Y = y;
            }
        }
    }
}