using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TelemetryAnalyzer.App;

public partial class RaceReplayWindow : Window
{
    private readonly DispatcherTimer _timer = new();
    private readonly List<TrackPoint> _track = [];
    private readonly List<TelemetryPoint> _telemetry = [];

    private Ellipse? _carMarker;
    private Polyline? _drivenLine;
    private bool _isPlaying;
    private bool _isUpdatingSlider;
    private double _position;
    private double _playbackSpeed = 1.0;
    private double _sampleInterval = 0.25;

    private readonly Brush _trackBrush =
        new SolidColorBrush(Color.FromRgb(75, 85, 99));

    private readonly Brush _drivenBrush =
        new SolidColorBrush(Color.FromRgb(52, 211, 153));

    public RaceReplayWindow()
    {
        InitializeComponent();

        _timer.Interval = TimeSpan.FromMilliseconds(50);
        _timer.Tick += Timer_Tick;

        Loaded += (_, _) => InitializeReplay();
        Closed += (_, _) => _timer.Stop();
    }

    private void InitializeReplay()
    {
        try
        {
            string baseDirectory = AppContext.BaseDirectory;

            string trackFile = System.IO.Path.Combine(
                baseDirectory, "Data", "tracks", "spa.csv");

            string telemetryFile = System.IO.Path.Combine(
                baseDirectory, "Data", "telemetry",
                "spa_telemetry.csv");

            LoadTrack(trackFile);
            LoadTelemetry(telemetryFile);

            if (_track.Count < 2)
                throw new InvalidDataException(
                    "The track file must contain at least two valid points.");

            if (_telemetry.Count < 2)
                throw new InvalidDataException(
                    "The telemetry file must contain at least two valid records.");

            _telemetry.Sort((a, b) => a.Time.CompareTo(b.Time));

            double[] intervals = _telemetry
                .Zip(_telemetry.Skip(1),
                    (a, b) => b.Time - a.Time)
                .Where(delta => delta > 0)
                .ToArray();

            if (intervals.Length > 0)
                _sampleInterval = intervals.Average();

            TimelineSlider.Minimum = 0;
            TimelineSlider.Maximum = _telemetry.Count - 1;
            TimelineSlider.Value = 0;

            DrawTrack();
            UpdateFrame(0);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not load race replay data.\n\n{ex.Message}",
                "Race Replay",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Close();
        }
    }

    private void LoadTrack(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException(
                "Track CSV was not found.", filePath);

        foreach (string rawLine in File.ReadLines(filePath))
        {
            string line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line) ||
                line.StartsWith("#") ||
                line.StartsWith("x_m", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] fields = line.Split(',');

            if (fields.Length < 2)
                continue;

            if (!TryNumber(fields[0], out double x) ||
                !TryNumber(fields[1], out double y))
            {
                continue;
            }

            double rightWidth = 6.0;
            double leftWidth = 6.0;

            if (fields.Length > 2)
                TryNumber(fields[2], out rightWidth);

            if (fields.Length > 3)
                TryNumber(fields[3], out leftWidth);

            _track.Add(new TrackPoint(
                x, y,
                Math.Max(0.1, rightWidth),
                Math.Max(0.1, leftWidth)));
        }
    }

    private void LoadTelemetry(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException(
                "Telemetry CSV was not found.", filePath);

        using StreamReader reader = new(filePath);

        string? headerLine = reader.ReadLine();

        if (headerLine is null)
            throw new InvalidDataException(
                "The telemetry CSV is empty.");

        string[] headers = headerLine
            .Split(',')
            .Select(h => h.Trim().Trim('"'))
            .ToArray();

        int Column(string name) =>
            Array.FindIndex(headers,
                h => h.Equals(name,
                    StringComparison.OrdinalIgnoreCase));

        int timeCol = Column("Time");
        int distanceCol = Column("Distance");
        int speedCol = Column("Speed");
        int xCol = Column("X");
        int yCol = Column("Y");
        int throttleCol = Column("Throttle");
        int brakeCol = Column("Brake");
        int rpmCol = Column("RPM");
        int gearCol = Column("Gear");

        if (timeCol < 0 || speedCol < 0)
            throw new InvalidDataException(
                "Telemetry CSV must contain Time and Speed columns.");

        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string[] fields = line.Split(',');

            if (!TryField(fields, timeCol, out double time) ||
                !TryField(fields, speedCol, out double speed))
            {
                continue;
            }

            _telemetry.Add(new TelemetryPoint
            {
                Time = time,
                Distance = ReadField(fields, distanceCol),
                Speed = speed,
                X = ReadField(fields, xCol),
                Y = ReadField(fields, yCol),
                Throttle = Clamp01(ReadField(fields, throttleCol) ?? 0),
                Brake = Clamp01(ReadField(fields, brakeCol) ?? 0),
                RPM = ReadField(fields, rpmCol) ?? 0,
                Gear = (int)(ReadField(fields, gearCol) ?? 0)
            });
        }
    }

    private static bool TryNumber(string text, out double value)
    {
        return double.TryParse(
            text.Trim().Trim('"'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool TryField(
        string[] fields, int index, out double value)
    {
        value = 0;

        return index >= 0 &&
               index < fields.Length &&
               TryNumber(fields[index], out value);
    }

    private static double? ReadField(string[] fields, int index)
    {
        return TryField(fields, index, out double value)
            ? value
            : null;
    }

    private static double Clamp01(double value) =>
        Math.Clamp(value, 0.0, 1.0);

    private void DrawTrack()
    {
        if (TrackCanvas.ActualWidth < 10 ||
            TrackCanvas.ActualHeight < 10 ||
            _track.Count < 2)
        {
            return;
        }

        TrackCanvas.Children.Clear();

        double minX = _track.Min(p => p.X);
        double maxX = _track.Max(p => p.X);
        double minY = _track.Min(p => p.Y);
        double maxY = _track.Max(p => p.Y);

        double padding = 35;
        double width = Math.Max(1, TrackCanvas.ActualWidth - 2 * padding);
        double height = Math.Max(1, TrackCanvas.ActualHeight - 2 * padding);

        double rangeX = Math.Max(1, maxX - minX);
        double rangeY = Math.Max(1, maxY - minY);

        double scale = Math.Min(width / rangeX, height / rangeY);

        double offsetX =
            (TrackCanvas.ActualWidth - rangeX * scale) / 2;

        double offsetY =
            (TrackCanvas.ActualHeight - rangeY * scale) / 2;

        Point Map(double x, double y) => new(
            offsetX + (x - minX) * scale,
            TrackCanvas.ActualHeight -
            (offsetY + (y - minY) * scale));

        Point[] center = _track
            .Select(p => Map(p.X, p.Y))
            .ToArray();

        Point[] rightBoundary = new Point[_track.Count];
        Point[] leftBoundary = new Point[_track.Count];

        for (int i = 0; i < _track.Count; i++)
        {
            Point previous = center[Math.Max(0, i - 1)];
            Point next = center[Math.Min(center.Length - 1, i + 1)];

            double dx = next.X - previous.X;
            double dy = next.Y - previous.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            if (length < 0.001)
                length = 1;

            double normalX = -dy / length;
            double normalY = dx / length;

            rightBoundary[i] = new Point(
                center[i].X + normalX * _track[i].RightWidth * scale,
                center[i].Y + normalY * _track[i].RightWidth * scale);

            leftBoundary[i] = new Point(
                center[i].X - normalX * _track[i].LeftWidth * scale,
                center[i].Y - normalY * _track[i].LeftWidth * scale);
        }

        AddPolyline(rightBoundary, Brushes.WhiteSmoke, 2);
        AddPolyline(leftBoundary, Brushes.WhiteSmoke, 2);
        AddPolyline(center, _trackBrush, 1);

        _drivenLine = new Polyline
        {
            Stroke = _drivenBrush,
            StrokeThickness = 3,
            StrokeLineJoin = PenLineJoin.Round
        };

        TrackCanvas.Children.Add(_drivenLine);

        _carMarker = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = Brushes.OrangeRed,
            Stroke = Brushes.White,
            StrokeThickness = 2
        };

        TrackCanvas.Children.Add(_carMarker);

        UpdateMarker();
    }

    private void AddPolyline(
        IEnumerable<Point> points, Brush brush, double thickness)
    {
        Polyline line = new()
        {
            Points = new PointCollection(points),
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round
        };

        TrackCanvas.Children.Add(line);
    }

    private Point TrackPointAt(double progress)
    {
        double position = Math.Clamp(progress, 0, 1)
                          * (_track.Count - 1);

        int index = (int)position;
        int next = Math.Min(index + 1, _track.Count - 1);
        double fraction = position - index;

        TrackPoint a = _track[index];
        TrackPoint b = _track[next];

        double x = a.X + (b.X - a.X) * fraction;
        double y = a.Y + (b.Y - a.Y) * fraction;

        double minX = _track.Min(p => p.X);
        double maxX = _track.Max(p => p.X);
        double minY = _track.Min(p => p.Y);
        double maxY = _track.Max(p => p.Y);

        double padding = 35;
        double width = Math.Max(1, TrackCanvas.ActualWidth - 2 * padding);
        double height = Math.Max(1, TrackCanvas.ActualHeight - 2 * padding);

        double scale = Math.Min(
            width / Math.Max(1, maxX - minX),
            height / Math.Max(1, maxY - minY));

        double offsetX =
            (TrackCanvas.ActualWidth -
             (maxX - minX) * scale) / 2;

        double offsetY =
            (TrackCanvas.ActualHeight -
             (maxY - minY) * scale) / 2;

        return new Point(
            offsetX + (x - minX) * scale,
            TrackCanvas.ActualHeight -
            (offsetY + (y - minY) * scale));
    }

    private void UpdateMarker()
    {
        if (_carMarker is null || _drivenLine is null ||
            _telemetry.Count == 0 || _track.Count == 0)
        {
            return;
        }

        double progress = _telemetry.Count <= 1
            ? 0
            : _position / (_telemetry.Count - 1);

        Point point = TrackPointAt(progress);

        Canvas.SetLeft(_carMarker, point.X - _carMarker.Width / 2);
        Canvas.SetTop(_carMarker, point.Y - _carMarker.Height / 2);

        Point[] drivenPoints = Enumerable.Range(
                0, Math.Clamp((int)_position + 1, 1, _track.Count))
            .Select(i => TrackPointAt(
                (double)i / Math.Max(1, _track.Count - 1)))
            .ToArray();

        _drivenLine.Points = new PointCollection(drivenPoints);
    }

    private void UpdateFrame(int index)
    {
        if (_telemetry.Count == 0)
            return;

        index = Math.Clamp(index, 0, _telemetry.Count - 1);

        TelemetryPoint data = _telemetry[index];

        SpeedText.Text = data.Speed.ToString("0.0");
        RpmText.Text = data.RPM.ToString("0");
        GearText.Text = data.Gear == 0 ? "N" : data.Gear.ToString();

        ThrottleBar.Value = data.Throttle;
        BrakeBar.Value = data.Brake;

        ThrottleText.Text = $"{data.Throttle * 100:0}%";
        BrakeText.Text = $"{data.Brake * 100:0}%";

        TimeText.Text = data.Time.ToString(
            "00.00", CultureInfo.InvariantCulture) + " s";

        ProgressText.Text =
            $"Record {index + 1} / {_telemetry.Count}";

        UpdateMarker();
        DrawGraph(SpeedGraph, p => p.Speed, Brushes.DodgerBlue);
        DrawGraph(ControlsGraph, p => p.Throttle, Brushes.LimeGreen);
        DrawGraph(RpmGraph, p => p.RPM, Brushes.Gold);
    }

    private void DrawGraph(
        Canvas canvas,
        Func<TelemetryPoint, double> selector,
        Brush brush)
    {
        canvas.Children.Clear();

        if (_telemetry.Count < 2 ||
            canvas.ActualWidth < 10 ||
            canvas.ActualHeight < 10)
        {
            return;
        }

        double padding = 8;
        double width = canvas.ActualWidth - 2 * padding;
        double height = canvas.ActualHeight - 2 * padding;

        double maxValue = _telemetry.Max(selector);

        if (maxValue <= 0)
            maxValue = 1;

        PointCollection points = new();

        for (int i = 0; i < _telemetry.Count; i++)
        {
            double x = padding +
                (double)i / (_telemetry.Count - 1) * width;

            double y = canvas.ActualHeight - padding -
                selector(_telemetry[i]) / maxValue * height;

            points.Add(new Point(x, y));
        }

        canvas.Children.Add(new Polyline
        {
            Points = points,
            Stroke = brush,
            StrokeThickness = 1.5
        });

        double cursorX = padding +
            _position / Math.Max(1, _telemetry.Count - 1) * width;

        canvas.Children.Add(new Line
        {
            X1 = cursorX,
            X2 = cursorX,
            Y1 = padding,
            Y2 = canvas.ActualHeight - padding,
            Stroke = Brushes.White,
            StrokeThickness = 1,
            Opacity = 0.65
        });
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_isPlaying || _telemetry.Count == 0)
            return;

        double step = _timer.Interval.TotalSeconds
                      * _playbackSpeed
                      / Math.Max(0.001, _sampleInterval);

        _position += step;

        if (_position >= _telemetry.Count - 1)
        {
            _position = _telemetry.Count - 1;
            PausePlayback();
        }

        _isUpdatingSlider = true;
        TimelineSlider.Value = _position;
        _isUpdatingSlider = false;

        UpdateFrame((int)Math.Round(_position));
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_telemetry.Count == 0)
            return;

        if (_isPlaying)
        {
            PausePlayback();
        }
        else
        {
            if (_position >= _telemetry.Count - 1)
                _position = 0;

            _isPlaying = true;
            PlayPauseButton.Content = "Pause";
            _timer.Start();
        }
    }

    private void PausePlayback()
    {
        _isPlaying = false;
        _timer.Stop();
        PlayPauseButton.Content = "Play";
    }

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        PausePlayback();

        _position = 0;
        TimelineSlider.Value = 0;
        UpdateFrame(0);
    }

    private void TimelineSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingSlider || _telemetry.Count == 0)
            return;

        _position = TimelineSlider.Value;
        UpdateFrame((int)Math.Round(_position));
    }

    private void PlaybackSpeedCombo_SelectionChanged(
        object sender, SelectionChangedEventArgs e)
    {
        if (PlaybackSpeedCombo.SelectedItem is not ComboBoxItem item)
            return;

        string value = item.Content?.ToString() ?? "1x";

        _playbackSpeed = value switch
        {
            "0.5x" => 0.5,
            "2x" => 2.0,
            "4x" => 4.0,
            _ => 1.0
        };
    }

    private void TrackCanvas_SizeChanged(
        object sender, SizeChangedEventArgs e)
    {
        if (_track.Count > 1)
            DrawTrack();
    }

    private sealed record TrackPoint(
        double X, double Y,
        double RightWidth, double LeftWidth);

    private sealed class TelemetryPoint
    {
        public double Time { get; set; }
        public double? Distance { get; set; }
        public double Speed { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
        public double Throttle { get; set; }
        public double Brake { get; set; }
        public double RPM { get; set; }
        public int Gear { get; set; }
    }
}
