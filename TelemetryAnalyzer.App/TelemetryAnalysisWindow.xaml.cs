using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TelemetryAnalyzer.App.Models;

namespace TelemetryAnalyzer.App
{
    public partial class TelemetryAnalysisWindow : Window
    {
        private readonly TelemetrySession _session;
        private List<TelemetryRecord> _records = new();

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
            DrawAllCharts();
        }

        private void UpdateStatistics()
        {
            SessionNameText.Text = string.IsNullOrWhiteSpace(_session.Name)
                ? "Telemetry Session"
                : _session.Name;

            SessionDetailsText.Text =
                $"Source: {_session.SourceFile}  |  Imported: " +
                _session.ImportedAt.ToString("yyyy-MM-dd HH:mm");

            RecordCountText.Text = _records.Count.ToString("N0");

            MaxSpeedText.Text = FormatValue(_session.MaximumSpeed);
            AverageSpeedText.Text = FormatValue(_session.AverageSpeed);
            MinSpeedText.Text = FormatValue(_session.MinimumSpeed);

            var times = _records
                .Where(r => r.Time.HasValue &&
                            double.IsFinite(r.Time.Value))
                .Select(r => r.Time!.Value)
                .ToList();

            if (times.Count >= 2)
            {
                double duration = times.Max() - times.Min();

                DurationText.Text = duration >= 0
                    ? FormatDuration(duration)
                    : "—";
            }
            else
            {
                DurationText.Text = "N/A";
            }

            var rpms = _records
                .Where(r => r.RPM.HasValue &&
                            double.IsFinite(r.RPM.Value))
                .Select(r => r.RPM!.Value)
                .ToList();

            MaxRpmText.Text = rpms.Count > 0
                ? rpms.Max().ToString("N0")
                : "N/A";

            var distances = _records
                .Where(r => r.Distance.HasValue &&
                            double.IsFinite(r.Distance.Value))
                .Select(r => r.Distance!.Value)
                .ToList();

            DistanceText.Text = distances.Count > 0
                ? FormatDistanceRange(distances)
                : "N/A";
        }

        private static string FormatValue(double? value)
        {
            return value.HasValue && double.IsFinite(value.Value)
                ? value.Value.ToString("N1")
                : "N/A";
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

            // Display as km when the source appears to use metres.
            if (range >= 1000)
            {
                return $"{range / 1000.0:N2} km";
            }

            return $"{range:N1} units";
        }

        private void ChartCanvas_SizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            DrawAllCharts();
        }

        private void DrawAllCharts()
        {
            if (!IsLoaded || _records.Count == 0)
            {
                return;
            }

            DrawChart(
                SpeedCanvas,
                _records
                    .Where(r => r.Speed.HasValue &&
                                double.IsFinite(r.Speed.Value))
                    .Select(r => new ChartPoint(
                        GetXAxis(r),
                        r.Speed!.Value))
                    .ToList(),
                Color.FromRgb(255, 159, 67),
                "No speed data");

            DrawChart(
                ThrottleCanvas,
                _records
                    .Where(r => r.Throttle.HasValue &&
                                double.IsFinite(r.Throttle.Value))
                    .Select(r => new ChartPoint(
                        GetXAxis(r),
                        r.Throttle!.Value))
                    .ToList(),
                Color.FromRgb(91, 192, 190),
                "No throttle data");

            DrawChart(
                BrakeCanvas,
                _records
                    .Where(r => r.Brake.HasValue &&
                                double.IsFinite(r.Brake.Value))
                    .Select(r => new ChartPoint(
                        GetXAxis(r),
                        r.Brake!.Value))
                    .ToList(),
                Color.FromRgb(239, 100, 97),
                "No brake data");

            DrawChart(
                RpmCanvas,
                _records
                    .Where(r => r.RPM.HasValue &&
                                double.IsFinite(r.RPM.Value))
                    .Select(r => new ChartPoint(
                        GetXAxis(r),
                        r.RPM!.Value))
                    .ToList(),
                Color.FromRgb(167, 139, 250),
                "No RPM data");
        }

        private static double GetXAxis(TelemetryRecord record)
        {
            return record.Time.HasValue &&
                   double.IsFinite(record.Time.Value)
                ? record.Time.Value
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
            {
                return;
            }

            const double left = 48;
            const double right = 12;
            const double top = 12;
            const double bottom = 28;

            double plotWidth = width - left - right;
            double plotHeight = height - top - bottom;

            if (plotWidth <= 0 || plotHeight <= 0)
            {
                return;
            }

            // Draw horizontal grid lines.
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
            {
                maxX = minX + 1;
            }

            // Give constant signals a visible vertical range.
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

            // Axis labels.
            AddText(
                canvas,
                maxY.ToString("N1"),
                2,
                top - 5,
                LabelBrush,
                10);

            AddText(
                canvas,
                minY.ToString("N1"),
                2,
                top + plotHeight - 10,
                LabelBrush,
                10);

            AddText(
                canvas,
                minX.ToString("N1"),
                left,
                height - bottom + 7,
                LabelBrush,
                10);

            AddText(
                canvas,
                maxX.ToString("N1"),
                width - right - 48,
                height - bottom + 7,
                LabelBrush,
                10);

            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush(lineColor),
                StrokeThickness = 1.8,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            // Sort by X to avoid drawing the graph backwards.
            foreach (ChartPoint point in points.OrderBy(p => p.X))
            {
                polyline.Points.Add(Transform(point));
            }

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
