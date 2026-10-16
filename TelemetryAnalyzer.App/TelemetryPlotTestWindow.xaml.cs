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
                TelemetryPlot.Plot.Clear();
                TelemetryPlot.Refresh();

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
        DrawSelectedChannel();
    }

    private void ChannelComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        DrawSelectedChannel();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadSessions();
    }

    private void DrawSelectedChannel()
    {
        if (SessionComboBox.SelectedItem is not TelemetrySession session)
            return;

        if (ChannelComboBox.SelectedItem is not string channel)
            return;

        var records = session.Records
            .Where(record => GetValue(record, channel).HasValue)
            .OrderBy(record => record.Index)
            .ToList();

        TelemetryPlot.Plot.Clear();

        if (records.Count == 0)
        {
            TelemetryPlot.Refresh();

            StatusText.Text =
                $"Session: {session.Name} | No valid {channel} values found.";

            return;
        }

        bool hasTime = records.All(record => record.Time.HasValue);

        double[] xValues = hasTime
            ? records.Select(record => record.Time!.Value).ToArray()
            : records.Select(record => (double)record.Index).ToArray();

        double[] yValues = records
            .Select(record => GetValue(record, channel)!.Value)
            .ToArray();

        TelemetryPlot.Plot.Add.Scatter(xValues, yValues);
        TelemetryPlot.Plot.Title($"{channel} - {session.Name}");
        TelemetryPlot.Plot.XLabel(
            hasTime ? "Time (s)" : "Sample index");
        TelemetryPlot.Plot.YLabel(GetUnit(channel));
        TelemetryPlot.Plot.Axes.AutoScale();

        TelemetryPlot.Refresh();

        StatusText.Text =
            $"Session: {session.Name} | Channel: {channel} | " +
            $"Points: {yValues.Length:N0} | " +
            $"Min: {yValues.Min():F2} | " +
            $"Max: {yValues.Max():F2} | " +
            $"Average: {yValues.Average():F2}";
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
            "Speed" => "Speed (km/h, if source uses km/h)",
            "RPM" => "Engine speed (RPM)",
            "Throttle" => "Throttle (source units)",
            "Brake" => "Brake (source units)",
            _ => channel
        };
    }
}
