
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TelemetryAnalyzer.App.Models;
using ScottPlot.Plottables;

namespace TelemetryAnalyzer.App;

public partial class TelemetryPlotTestWindow : Window
{
    private VerticalLine? _singleCursorLine;
    private VerticalLine? _speedCursorLine;
    private VerticalLine? _rpmCursorLine;
    private VerticalLine? _throttleCursorLine;
    private VerticalLine? _brakeCursorLine;

    private static readonly ScottPlot.Color CursorColor =
        ScottPlot.Color.FromHex("#FF9F43");

    public TelemetryPlotTestWindow()
    {
        InitializeComponent();

        DisplayModeComboBox.ItemsSource = new[]
        {
            "Single Channel",
            "Multi Channel"
        };

        DisplayModeComboBox.SelectedIndex = 0;

        ChannelComboBox.ItemsSource = new[]
        {
            "Speed",
            "RPM",
            "Throttle",
            "Brake"
        };

        ChannelComboBox.SelectedIndex = 0;

        LoadSessions();
    }

    private void LoadSessions()
    {
        try
        {
            var sessions = App.Database.GetSessions();

            SessionComboBox.ItemsSource = sessions;

            if (sessions.Count > 0)
            {
                SessionComboBox.SelectedIndex = 0;
            }
            else
            {
                ClearAllPlots();

                StatusText.Text =
                    "No imported sessions found. Import a CSV file first.";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Could not load sessions: {ex.Message}";
        }
    }

    private void SessionComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        DrawTelemetry();
    }

    private void DisplayModeComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateDisplayMode();
        DrawTelemetry();
    }

    private void ChannelComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        DrawTelemetry();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        int? selectedSessionId =
            (SessionComboBox.SelectedItem as TelemetrySession)?.Id;

        LoadSessions();

        if (selectedSessionId.HasValue &&
            SessionComboBox.ItemsSource is IEnumerable<TelemetrySession> sessions)
        {
            TelemetrySession? existingSession =
                sessions.FirstOrDefault(session =>
                    session.Id == selectedSessionId.Value);

            if (existingSession is not null)
                SessionComboBox.SelectedItem = existingSession;
        }

        DrawTelemetry();
    }

    private void ResetZoom_Click(object sender, RoutedEventArgs e)
    {
        SingleTelemetryPlot.Plot.Axes.AutoScale();
        SpeedPlot.Plot.Axes.AutoScale();
        RpmPlot.Plot.Axes.AutoScale();
        ThrottlePlot.Plot.Axes.AutoScale();
        BrakePlot.Plot.Axes.AutoScale();

        SingleTelemetryPlot.Refresh();
        SpeedPlot.Refresh();
        RpmPlot.Refresh();
        ThrottlePlot.Refresh();
        BrakePlot.Refresh();

        CursorValueText.Text = "X: -- | Y: --";
        StatusText.Text = "Chart zoom has been reset.";
    }

    private void UpdateDisplayMode()
    {
        bool multiMode =
            DisplayModeComboBox.SelectedItem as string == "Multi Channel";

        SingleChannelPanel.Visibility =
            multiMode ? Visibility.Collapsed : Visibility.Visible;

        MultiChannelPanel.Visibility =
            multiMode ? Visibility.Visible : Visibility.Collapsed;

        ChannelComboBox.IsEnabled = !multiMode;
    }

    private void DrawTelemetry()
    {
        if (SessionComboBox.SelectedItem is not TelemetrySession session)
            return;

        CursorValueText.Text = "X: -- | Y: --";

        if (DisplayModeComboBox.SelectedItem as string == "Multi Channel")
        {
            DrawMultiChannel(session);
        }
        else
        {
            DrawSingleChannel(session);
        }
    }

    private void DrawSingleChannel(TelemetrySession session)
    {
        if (ChannelComboBox.SelectedItem is not string channel)
            return;

        var records = session.Records
            .Where(record => GetValue(record, channel).HasValue)
            .OrderBy(record => record.Index)
            .ToList();

        SingleTelemetryPlot.Plot.Clear();
        _singleCursorLine = null;

        if (records.Count == 0)
        {
            SingleTelemetryPlot.Refresh();
            ClearSingleStats();

            SingleChartTitle.Text = channel;

            StatusText.Text =
                $"Session: {session.Name} | No valid {channel} values found.";

            return;
        }

        double[] xValues = GetXValues(records);

        double[] yValues = records
            .Select(record => GetValue(record, channel)!.Value)
            .ToArray();

        var scatter = SingleTelemetryPlot.Plot.Add.Scatter(xValues, yValues);
        scatter.LineWidth = 2;

        SingleTelemetryPlot.Plot.Title($"{channel} - {session.Name}");
        SingleTelemetryPlot.Plot.XLabel(GetXAxisLabel(records));
        SingleTelemetryPlot.Plot.YLabel(GetUnit(channel));
        SingleTelemetryPlot.Plot.Axes.AutoScale();

        SingleTelemetryPlot.Refresh();

        SingleChartTitle.Text = channel.ToUpperInvariant();

        SingleMinText.Text = yValues.Min().ToString("F2");
        SingleMaxText.Text = yValues.Max().ToString("F2");
        SingleAverageText.Text = yValues.Average().ToString("F2");
        SinglePointsText.Text = yValues.Length.ToString("N0");

        StatusText.Text =
            $"Session: {session.Name} | Channel: {channel} | " +
            $"Points: {yValues.Length:N0}";
    }

    private void DrawMultiChannel(TelemetrySession session)
    {
        var records = session.Records
            .OrderBy(record => record.Index)
            .ToList();

        DrawChannel(SpeedPlot, SpeedStatsText, records, "Speed");
        DrawChannel(RpmPlot, RpmStatsText, records, "RPM");
        DrawChannel(ThrottlePlot, ThrottleStatsText, records, "Throttle");
        DrawChannel(BrakePlot, BrakeStatsText, records, "Brake");

        StatusText.Text =
            $"Session: {session.Name} | Displaying four telemetry channels";
    }

    private void DrawChannel(
        ScottPlot.WPF.WpfPlot plot,
        TextBlock statsText,
        List<TelemetryRecord> records,
        string channel)
    {
        var validRecords = records
            .Where(record => GetValue(record, channel).HasValue)
            .ToList();

        plot.Plot.Clear();
        ResetCursorReference(plot);

        if (validRecords.Count == 0)
        {
            plot.Refresh();
            statsText.Text = "No valid data available";
            return;
        }

        double[] xValues = GetXValues(validRecords);

        double[] yValues = validRecords
            .Select(record => GetValue(record, channel)!.Value)
            .ToArray();

        var scatter = plot.Plot.Add.Scatter(xValues, yValues);
        scatter.LineWidth = 2;

        plot.Plot.Title(channel);
        plot.Plot.XLabel(GetXAxisLabel(validRecords));
        plot.Plot.YLabel(GetUnit(channel));
        plot.Plot.Axes.AutoScale();

        plot.Refresh();

        statsText.Text =
            $"Min: {yValues.Min():F2} | " +
            $"Max: {yValues.Max():F2} | " +
            $"Avg: {yValues.Average():F2}";
    }

    private void TelemetryPlot_MouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (sender is not ScottPlot.WPF.WpfPlot plot)
            return;

        if (SessionComboBox.SelectedItem is not TelemetrySession session)
            return;

        string channel;

        if (plot == SingleTelemetryPlot)
            channel = ChannelComboBox.SelectedItem as string ?? "Speed";
        else if (plot == SpeedPlot)
            channel = "Speed";
        else if (plot == RpmPlot)
            channel = "RPM";
        else if (plot == ThrottlePlot)
            channel = "Throttle";
        else if (plot == BrakePlot)
            channel = "Brake";
        else
            return;

        var records = session.Records
            .Where(record => GetValue(record, channel).HasValue)
            .OrderBy(record => record.Index)
            .ToList();

        if (records.Count == 0)
            return;

        Point mousePosition = e.GetPosition(plot);

        ScottPlot.Pixel pixel = new(
            (int)mousePosition.X,
            (int)mousePosition.Y);

        ScottPlot.Coordinates coordinates =
            plot.Plot.GetCoordinates(pixel);

        bool hasTime = records.All(record => record.Time.HasValue);

        TelemetryRecord? nearestRecord = records.MinBy(record =>
        {
            double x = hasTime
                ? record.Time!.Value
                : record.Index;

            return Math.Abs(x - coordinates.X);
        });

        if (nearestRecord is null)
            return;

        double xValue = hasTime
            ? nearestRecord.Time!.Value
            : nearestRecord.Index;

        double? yValue = GetValue(nearestRecord, channel);

        if (!yValue.HasValue)
            return;

        UpdateCursorLine(plot, xValue);

        string xLabel = hasTime ? "Time" : "Index";

        CursorValueText.Text =
            $"{xLabel}: {xValue:F3} | {channel}: {yValue.Value:F2}";

        plot.Refresh();
    }

    private void UpdateCursorLine(
        ScottPlot.WPF.WpfPlot plot,
        double xValue)
    {
        VerticalLine? cursorLine = GetCursorLine(plot);

        if (cursorLine is null)
        {
            cursorLine = plot.Plot.Add.VerticalLine(xValue);

            cursorLine.LineWidth = 2;
            cursorLine.Color = CursorColor;

            SetCursorLine(plot, cursorLine);
        }
        else
        {
            cursorLine.X = xValue;
        }
    }

    private VerticalLine? GetCursorLine(
        ScottPlot.WPF.WpfPlot plot)
    {
        if (plot == SingleTelemetryPlot)
            return _singleCursorLine;

        if (plot == SpeedPlot)
            return _speedCursorLine;

        if (plot == RpmPlot)
            return _rpmCursorLine;

        if (plot == ThrottlePlot)
            return _throttleCursorLine;

        if (plot == BrakePlot)
            return _brakeCursorLine;

        return null;
    }

    private void SetCursorLine(
        ScottPlot.WPF.WpfPlot plot,
        VerticalLine cursorLine)
    {
        if (plot == SingleTelemetryPlot)
            _singleCursorLine = cursorLine;
        else if (plot == SpeedPlot)
            _speedCursorLine = cursorLine;
        else if (plot == RpmPlot)
            _rpmCursorLine = cursorLine;
        else if (plot == ThrottlePlot)
            _throttleCursorLine = cursorLine;
        else if (plot == BrakePlot)
            _brakeCursorLine = cursorLine;
    }

    private void ResetCursorReference(
        ScottPlot.WPF.WpfPlot plot)
    {
        if (plot == SingleTelemetryPlot)
            _singleCursorLine = null;
        else if (plot == SpeedPlot)
            _speedCursorLine = null;
        else if (plot == RpmPlot)
            _rpmCursorLine = null;
        else if (plot == ThrottlePlot)
            _throttleCursorLine = null;
        else if (plot == BrakePlot)
            _brakeCursorLine = null;
    }

    private void TelemetryPlot_MouseLeave(
        object sender,
        MouseEventArgs e)
    {
        CursorValueText.Text = "X: -- | Y: --";
    }

    private static double[] GetXValues(
        List<TelemetryRecord> records)
    {
        bool hasTime = records.All(record => record.Time.HasValue);

        return hasTime
            ? records.Select(record => record.Time!.Value).ToArray()
            : records.Select(record => (double)record.Index).ToArray();
    }

    private static string GetXAxisLabel(
        List<TelemetryRecord> records)
    {
        return records.All(record => record.Time.HasValue)
            ? "Time (s)"
            : "Sample index";
    }

    private static double? GetValue(
        TelemetryRecord record,
        string channel)
    {
        return channel switch
        {
            "Speed" => record.Speed,
            "RPM" => record.RPM,
            "Throttle" => record.Throttle,
            "Brake" => record.Brake,
            _ => null
        };
    }

    private static string GetUnit(string channel)
    {
        return channel switch
        {
            "Speed" => "Speed (source units)",
            "RPM" => "Engine speed (RPM)",
            "Throttle" => "Throttle (source units)",
            "Brake" => "Brake (source units)",
            _ => channel
        };
    }

    private void ClearSingleStats()
    {
        SingleMinText.Text = "--";
        SingleMaxText.Text = "--";
        SingleAverageText.Text = "--";
        SinglePointsText.Text = "--";
    }

    private void ClearAllPlots()
    {
        SingleTelemetryPlot.Plot.Clear();
        SpeedPlot.Plot.Clear();
        RpmPlot.Plot.Clear();
        ThrottlePlot.Plot.Clear();
        BrakePlot.Plot.Clear();

        _singleCursorLine = null;
        _speedCursorLine = null;
        _rpmCursorLine = null;
        _throttleCursorLine = null;
        _brakeCursorLine = null;

        SingleTelemetryPlot.Refresh();
        SpeedPlot.Refresh();
        RpmPlot.Refresh();
        ThrottlePlot.Refresh();
        BrakePlot.Refresh();

        ClearSingleStats();

        SpeedStatsText.Text = "--";
        RpmStatsText.Text = "--";
        ThrottleStatsText.Text = "--";
        BrakeStatsText.Text = "--";

        CursorValueText.Text = "X: -- | Y: --";
    }
}
