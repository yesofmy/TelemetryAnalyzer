using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TelemetryAnalyzer.App.Models;
using TelemetryAnalyzer.Core.Algorithms;

namespace TelemetryAnalyzer.App;

public partial class SignalAnalysisWindow : Window
{
    public SignalAnalysisWindow()
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

        AlgorithmComboBox.ItemsSource = new[]
        {
            "Naive (reference)",
            "Optimized (running sum)"
        };

        AlgorithmComboBox.SelectedIndex = 1;

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
                StatusText.Text =
                    "No imported sessions. Import a CSV file first.";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Could not load sessions: {ex.Message}";
        }
    }

    private void Selection_Changed(
        object sender,
        SelectionChangedEventArgs e)
    {
        // Filtering is started explicitly with the Apply button.
    }


    private void ApplyFilter_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (SessionComboBox.SelectedItem is not TelemetrySession session)
        {
            StatusText.Text = "Select a telemetry session.";
            return;
        }

        if (ChannelComboBox.SelectedItem is not string channel)
        {
            StatusText.Text = "Select a telemetry channel.";
            return;
        }

        if (AlgorithmComboBox.SelectedItem is not string algorithm)
        {
            StatusText.Text = "Select a filtering algorithm.";
            return;
        }

        if (!int.TryParse(WindowSizeTextBox.Text, out int windowSize)
            || windowSize < 1)
        {
            MessageBox.Show(
                "Window size must be a positive whole number.",
                "Invalid window size",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var records = session.Records
            .Where(record => GetValue(record, channel).HasValue)
            .OrderBy(record => record.Index)
            .ToList();

        if (records.Count == 0)
        {
            AnalysisPlot.Plot.Clear();
            AnalysisPlot.Refresh();
            StatusText.Text =
                $"No valid values found for channel {channel}.";
            return;
        }

        if (windowSize > records.Count)
        {
            MessageBox.Show(
                $"Window size cannot exceed the number of available " +
                $"values ({records.Count}).",
                "Window size too large",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        bool hasTime = records.All(record => record.Time.HasValue);

        double[] xValues = hasTime
            ? records.Select(record => record.Time!.Value).ToArray()
            : records.Select(record => (double)record.Index).ToArray();

        double[] originalValues = records
            .Select(record => GetValue(record, channel)!.Value)
            .ToArray();

        // Measure both algorithms on the same input.
        var naiveWatch = Stopwatch.StartNew();
        double[] naiveValues =
            MovingAverageFilter.Apply(originalValues, windowSize);
        naiveWatch.Stop();

        var optimizedWatch = Stopwatch.StartNew();
        double[] optimizedValues =
            OptimizedMovingAverageFilter.Apply(originalValues, windowSize);
        optimizedWatch.Stop();

        double naiveMs = naiveWatch.Elapsed.TotalMilliseconds;
        double optimizedMs = optimizedWatch.Elapsed.TotalMilliseconds;

        double speedup = optimizedMs > 0
            ? naiveMs / optimizedMs
            : 0;

        double maxError = 0;

        for (int i = 0; i < naiveValues.Length; i++)
        {
            double error = Math.Abs(naiveValues[i] - optimizedValues[i]);

            if (error > maxError)
            {
                maxError = error;
            }
        }

        double[] filteredValues =
            algorithm == "Optimized (running sum)"
                ? optimizedValues
                : naiveValues;

        AnalysisPlot.Plot.Clear();

        var originalPlot =
            AnalysisPlot.Plot.Add.Scatter(xValues, originalValues);
        originalPlot.LegendText = "Original";

        var filteredPlot =
            AnalysisPlot.Plot.Add.Scatter(xValues, filteredValues);
        filteredPlot.LegendText = $"Filtered: {algorithm}";

        AnalysisPlot.Plot.Title($"{channel} - Signal Comparison");
        AnalysisPlot.Plot.XLabel(
            hasTime ? "Time (s)" : "Sample index");
        AnalysisPlot.Plot.YLabel(channel);
        AnalysisPlot.Plot.ShowLegend();
        AnalysisPlot.Plot.Axes.AutoScale();

        AnalysisPlot.Refresh();

        StatusText.Text =
            $"Session: {session.Name} | Channel: {channel} | " +
            $"Samples: {originalValues.Length:N0} | Window: {windowSize}\n" +
            $"Naive: {naiveMs:F4} ms | " +
            $"Optimized: {optimizedMs:F4} ms | " +
            $"Speedup: {speedup:F2}x | " +
            $"Max error: {maxError:E3}\n" +
            $"Selected: {algorithm} | " +
            $"Original average: {originalValues.Average():F2} | " +
            $"Filtered average: {filteredValues.Average():F2}";
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
}
