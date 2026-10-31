using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TelemetryAnalyzer.App.Models;

namespace TelemetryAnalyzer.App;

public partial class TelemetryPlotTestWindow : Window
{
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
        LoadSessions();
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

        if (records.Count == 0)
        {
            SingleTelemetryPlot.Refresh();
            ClearSingleStats();

            SingleChartTitle.Text = channel;

            StatusText.Text =
                $"Session: {session.Name} | No valid {channel} values found.";

            return;
        }

        var xValues = GetXValues(records);

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

        DrawChannel(
            SpeedPlot,
            SpeedStatsText,
            records,
            "Speed");

        DrawChannel(
            RpmPlot,
            RpmStatsText,
            records,
            "RPM");

        DrawChannel(
            ThrottlePlot,
            ThrottleStatsText,
            records,
            "Throttle");

        DrawChannel(
            BrakePlot,
            BrakeStatsText,
            records,
            "Brake");

        StatusText.Text =
            $"Session: {session.Name} | Displaying four telemetry channels";
    }

    private void DrawChannel(
        ScottPlot.WPF.WpfPlot plot,
        TextBlock statsText,
        System.Collections.Generic.List<TelemetryRecord> records,
        string channel)
    {
        var validRecords = records
            .Where(record => GetValue(record, channel).HasValue)
            .ToList();

        plot.Plot.Clear();

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

    private static double[] GetXValues(
        System.Collections.Generic.List<TelemetryRecord> records)
    {
        bool hasTime = records.All(record => record.Time.HasValue);

        return hasTime
            ? records.Select(record => record.Time!.Value).ToArray()
            : records.Select(record => (double)record.Index).ToArray();
    }

    private static string GetXAxisLabel(
        System.Collections.Generic.List<TelemetryRecord> records)
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
    }
}