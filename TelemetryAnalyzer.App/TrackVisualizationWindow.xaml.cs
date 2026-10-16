
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TelemetryAnalyzer.App
{
    public partial class TrackVisualizationWindow : Window
    {
        private readonly List<TrackPoint> _trackPoints = new();

        private const double CanvasPadding = 40;

        private static readonly Brush TrackBrush =
            new SolidColorBrush(Color.FromRgb(65, 73, 82));

        private static readonly Brush BoundaryBrush =
            new SolidColorBrush(Color.FromRgb(220, 225, 230));

        private static readonly Brush CenterLineBrush =
            new SolidColorBrush(Color.FromRgb(255, 159, 67));

        private static readonly Brush GridBrush =
            new SolidColorBrush(Color.FromRgb(35, 40, 47));

        public TrackVisualizationWindow()
        {
            InitializeComponent();
            Loaded += TrackVisualizationWindow_Loaded;
        }

        private void TrackVisualizationWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            LoadTrackData();
            DrawTrack();
        }

        private void LoadTrackData()
        {
            _trackPoints.Clear();

            string filePath = System.IO.Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "tracks",
                "spa.csv");

            if (!File.Exists(filePath))
            {
                TrackPointsValue.Text = "Not found";

                MessageBox.Show(
                    $"CSV file not found:\n{filePath}\n\n" +
                    "Check Data/tracks/spa.csv and its copy settings.",
                    "Track data error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            try
            {
                using var reader = new StreamReader(filePath);

                string? line;
                int lineNumber = 0;

                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    line = line.Trim();

                    if (string.IsNullOrWhiteSpace(line) ||
                        line.StartsWith("#"))
                    {
                        continue;
                    }

                    string[] columns = line.Split(',');

                    if (columns.Length < 4)
                    {
                        throw new FormatException(
                            $"Expected four CSV columns at line {lineNumber}.");
                    }

                    // Initialize variables before TryParse to avoid CS0165.
                    double x = 0;
                    double y = 0;
                    double rightWidth = 0;
                    double leftWidth = 0;

                    bool valid =
                        double.TryParse(
                            columns[0].Trim(),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out x) &&
                        double.TryParse(
                            columns[1].Trim(),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out y) &&
                        double.TryParse(
                            columns[2].Trim(),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out rightWidth) &&
                        double.TryParse(
                            columns[3].Trim(),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out leftWidth);

                    // Skip a text header if one is present.
                    if (!valid &&
                        _trackPoints.Count == 0 &&
                        columns[0].Contains(
                            "x",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!valid)
                    {
                        throw new FormatException(
                            $"Invalid numeric values at line {lineNumber}.");
                    }

                    if (!double.IsFinite(x) ||
                        !double.IsFinite(y) ||
                        !double.IsFinite(rightWidth) ||
                        !double.IsFinite(leftWidth) ||
                        rightWidth <= 0 ||
                        leftWidth <= 0)
                    {
                        throw new FormatException(
                            $"Invalid coordinates or widths at line {lineNumber}.");
                    }

                    _trackPoints.Add(
                        new TrackPoint(x, y, rightWidth, leftWidth));
                }

                if (_trackPoints.Count < 3)
                {
                    throw new FormatException(
                        "The CSV file contains fewer than three valid points.");
                }

                TrackPointsValue.Text =
                    _trackPoints.Count.ToString(
                        "N0",
                        CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                _trackPoints.Clear();
                TrackPointsValue.Text = "Error";

                MessageBox.Show(
                    $"Could not load the track:\n{ex.Message}",
                    "Track data error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void TrackCanvas_SizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            DrawTrack();
        }

        private void DrawTrack()
        {
            if (TrackCanvas == null)
            {
                return;
            }

            TrackCanvas.Children.Clear();

            double width = TrackCanvas.ActualWidth;
            double height = TrackCanvas.ActualHeight;

            if (width <= 0 || height <= 0 || _trackPoints.Count < 3)
            {
                return;
            }

            DrawGrid(width, height);

            double minX = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double minY = double.PositiveInfinity;
            double maxY = double.NegativeInfinity;

            foreach (TrackPoint p in _trackPoints)
            {
                double halfWidth = Math.Max(p.LeftWidth, p.RightWidth);

                minX = Math.Min(minX, p.X - halfWidth);
                maxX = Math.Max(maxX, p.X + halfWidth);
                minY = Math.Min(minY, p.Y - halfWidth);
                maxY = Math.Max(maxY, p.Y + halfWidth);
            }

            double dataWidth = maxX - minX;
            double dataHeight = maxY - minY;

            if (dataWidth <= 0 || dataHeight <= 0)
            {
                return;
            }

            double availableWidth = Math.Max(
                1,
                width - 2 * CanvasPadding);

            double availableHeight = Math.Max(
                1,
                height - 2 * CanvasPadding);

            double scale = Math.Min(
                availableWidth / dataWidth,
                availableHeight / dataHeight);

            double drawnWidth = dataWidth * scale;
            double drawnHeight = dataHeight * scale;

            double offsetX = (width - drawnWidth) / 2;
            double offsetY = (height - drawnHeight) / 2;

            Point Transform(double x, double y)
            {
                return new Point(
                    offsetX + (x - minX) * scale,
                    height - offsetY - (y - minY) * scale);
            }

            var leftEdge = new List<Point>();
            var rightEdge = new List<Point>();
            var centerLine = new List<Point>();

            for (int i = 0; i < _trackPoints.Count; i++)
            {
                TrackPoint current = _trackPoints[i];

                TrackPoint previous = _trackPoints[
                    (i - 1 + _trackPoints.Count) % _trackPoints.Count];

                TrackPoint next = _trackPoints[
                    (i + 1) % _trackPoints.Count];

                double dx = next.X - previous.X;
                double dy = next.Y - previous.Y;

                double length = Math.Sqrt(dx * dx + dy * dy);

                if (length < 0.000001)
                {
                    Point point = Transform(current.X, current.Y);

                    centerLine.Add(point);
                    leftEdge.Add(point);
                    rightEdge.Add(point);

                    continue;
                }

                dx /= length;
                dy /= length;

                double normalX = dy;
                double normalY = -dx;

                double leftX =
                    current.X - normalX * current.LeftWidth;

                double leftY =
                    current.Y - normalY * current.LeftWidth;

                double rightX =
                    current.X + normalX * current.RightWidth;

                double rightY =
                    current.Y + normalY * current.RightWidth;

                centerLine.Add(Transform(current.X, current.Y));
                leftEdge.Add(Transform(leftX, leftY));
                rightEdge.Add(Transform(rightX, rightY));
            }

            var surfacePoints = new PointCollection(
                leftEdge.Concat(rightEdge.AsEnumerable().Reverse()));

            TrackCanvas.Children.Add(new Polygon
            {
                Points = surfacePoints,
                Fill = TrackBrush,
                Stroke = BoundaryBrush,
                StrokeThickness = 1,
                StrokeLineJoin = PenLineJoin.Round
            });

            AddPolyline(leftEdge, BoundaryBrush, 1.2);
            AddPolyline(rightEdge, BoundaryBrush, 1.2);
            AddPolyline(centerLine, CenterLineBrush, 1.6);

            if (leftEdge.Count > 0 && rightEdge.Count > 0)
            {
                TrackCanvas.Children.Add(new Line
                {
                    X1 = leftEdge[0].X,
                    Y1 = leftEdge[0].Y,
                    X2 = rightEdge[0].X,
                    Y2 = rightEdge[0].Y,
                    Stroke = Brushes.White,
                    StrokeThickness = 3
                });

                AddLabel(
                    "FIRST CSV POINT",
                    centerLine[0].X + 8,
                    centerLine[0].Y - 24);
            }

            AddLabel(
                $"SPA-FRANCORCHAMPS | {_trackPoints.Count:N0} points",
                14,
                12);
        }

        private void DrawGrid(double width, double height)
        {
            const double spacing = 40;

            for (double x = 0; x < width; x += spacing)
            {
                TrackCanvas.Children.Add(new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = height,
                    Stroke = GridBrush,
                    StrokeThickness = 0.5
                });
            }

            for (double y = 0; y < height; y += spacing)
            {
                TrackCanvas.Children.Add(new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = width,
                    Y2 = y,
                    Stroke = GridBrush,
                    StrokeThickness = 0.5
                });
            }
        }

        private void AddPolyline(
            IEnumerable<Point> points,
            Brush stroke,
            double thickness)
        {
            TrackCanvas.Children.Add(new Polyline
            {
                Points = new PointCollection(points),
                Stroke = stroke,
                StrokeThickness = thickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });
        }

        private void AddLabel(string text, double x, double y)
        {
            var label = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(
                    Color.FromArgb(210, 18, 22, 28)),
                Padding = new Thickness(5, 3, 5, 3)
            };

            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y);

            TrackCanvas.Children.Add(label);
        }

        private sealed class TrackPoint
        {
            public double X { get; }
            public double Y { get; }
            public double RightWidth { get; }
            public double LeftWidth { get; }

            public TrackPoint(
                double x,
                double y,
                double rightWidth,
                double leftWidth)
            {
                X = x;
                Y = y;
                RightWidth = rightWidth;
                LeftWidth = leftWidth;
            }
        }
    }
}
