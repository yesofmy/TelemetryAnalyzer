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
    private const int BenchmarkRepetitions = 10;

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
                ClearResults();
                StatusText.Text =
                    "No imported sessions. Import a CSV file first.";
            }
        }
        catch (Exception ex)
        {
            ClearResults();
            StatusText.Text =
                $"Could not load sessions: {ex.Message}";
        }
    }

    private void Selection_Changed(
        object sender,
        SelectionChangedEventArgs e)
    {
        // Filtering starts only after clicking Apply filter.
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

        try
        {
            var records = session.Records
                .Where(record => GetValue(record, channel).HasValue)
                .OrderBy(record => record.Index)
                .ToList();

            if (records.Count == 0)
            {
                ClearResults();
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

            // Warm-up runs are excluded from the measurements.
            _ = MovingAverageFilter.Apply(originalValues, windowSize);
            _ = OptimizedMovingAverageFilter.Apply(
                originalValues,
                windowSize);

            // Measure the average execution time of the reference algorithm.
            double[] naiveValues = Array.Empty<double>();

            var naiveWatch = Stopwatch.StartNew();

            for (int i = 0; i < BenchmarkRepetitions; i++)
            {
                naiveValues =
                    MovingAverageFilter.Apply(originalValues, windowSize);
            }

            naiveWatch.Stop();

            // Measure the average execution time of the optimized algorithm.
            double[] optimizedValues = Array.Empty<double>();

            var optimizedWatch = Stopwatch.StartNew();

            for (int i = 0; i < BenchmarkRepetitions; i++)
            {
                optimizedValues =
                    OptimizedMovingAverageFilter.Apply(
                        originalValues,
                        windowSize);
            }

            optimizedWatch.Stop();

            double naiveMs =
                naiveWatch.Elapsed.TotalMilliseconds / BenchmarkRepetitions;

            double optimizedMs =
                optimizedWatch.Elapsed.TotalMilliseconds / BenchmarkRepetitions;

            double speedup = optimizedMs > 0
                ? naiveMs / optimizedMs
                : 0;

            // Compare the numerical results of both implementations.
            double maxError = 0;

            for (int i = 0; i < naiveValues.Length; i++)
            {
                double error = Math.Abs(
                    naiveValues[i] - optimizedValues[i]);

                if (error > maxError)
                {
                    maxError = error;
                }
            }

            double[] filteredValues =
                algorithm == "Optimized (running sum)"
                    ? optimizedValues
                    : naiveValues;

            DrawOriginalPlot(
                xValues,
                originalValues,
                channel,
                hasTime);

            DrawFilteredPlot(
                xValues,
                filteredValues,
                channel,
                algorithm,
                hasTime);

            UpdateStatistics(
                originalValues,
                filteredValues,
                naiveMs,
                optimizedMs,
                speedup,
                maxError,
                windowSize);

            StatusText.Text =
                $"Session: {session.Name} | Channel: {channel} | " +
                $"Selected algorithm: {algorithm} | " +
                $"Benchmark repetitions: {BenchmarkRepetitions}";
        }
        catch (Exception ex)
        {
            ClearResults();
            StatusText.Text = $"Signal analysis failed: {ex.Message}";
        }
    }

    private void DrawOriginalPlot(
        double[] xValues,
        double[] values,
        string channel,
        bool hasTime)
    {
        OriginalPlot.Plot.Clear();

        var signal = OriginalPlot.Plot.Add.Scatter(xValues, values);
        signal.LegendText = "Original signal";

        OriginalPlot.Plot.Title($"{channel} - Original Signal");
        OriginalPlot.Plot.XLabel(
            hasTime ? "Time (s)" : "Sample index");
        OriginalPlot.Plot.YLabel(channel);
        OriginalPlot.Plot.ShowLegend();
        OriginalPlot.Plot.Axes.AutoScale();

        OriginalPlot.Refresh();
    }

    private void DrawFilteredPlot(
        double[] xValues,
        double[] values,
        string channel,
        string algorithm,
        bool hasTime)
    {
        FilteredPlot.Plot.Clear();

        var signal = FilteredPlot.Plot.Add.Scatter(xValues, values);
        signal.LegendText = "Filtered signal";

        FilteredPlot.Plot.Title($"{channel} - Filtered Signal");
        FilteredPlot.Plot.XLabel(
            hasTime ? "Time (s)" : "Sample index");
        FilteredPlot.Plot.YLabel(channel);
        FilteredPlot.Plot.ShowLegend();
        FilteredPlot.Plot.Axes.AutoScale();

        FilteredPlot.Refresh();
    }

    private void UpdateStatistics(
        double[] originalValues,
        double[] filteredValues,
        double naiveMs,
        double optimizedMs,
        double speedup,
        double maxError,
        int windowSize)
    {
        SampleCountText.Text = originalValues.Length.ToString("N0");

        NaiveTimeText.Text = $"{naiveMs:F4} ms";
        OptimizedTimeText.Text = $"{optimizedMs:F4} ms";

        SpeedupText.Text = speedup > 0
            ? $"{speedup:F2}x"
            : "—";

        ErrorText.Text = maxError.ToString("E3");

        OriginalAverageText.Text =
            originalValues.Average().ToString("F2");

        FilteredAverageText.Text =
            filteredValues.Average().ToString("F2");

        AppliedWindowText.Text = windowSize.ToString();
    }

    private void ClearResults()
    {
        OriginalPlot.Plot.Clear();
        FilteredPlot.Plot.Clear();

        OriginalPlot.Refresh();
        FilteredPlot.Refresh();

        SampleCountText.Text = "—";
        NaiveTimeText.Text = "—";
        OptimizedTimeText.Text = "—";
        SpeedupText.Text = "—";
        ErrorText.Text = "—";
        OriginalAverageText.Text = "—";
        FilteredAverageText.Text = "—";
        AppliedWindowText.Text = "—";
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