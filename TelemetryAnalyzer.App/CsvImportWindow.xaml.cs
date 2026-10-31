
using Microsoft.Win32;
using System.Windows;
using TelemetryAnalyzer.App.Services;

namespace TelemetryAnalyzer.App;

public partial class CsvImportWindow : Window
{
    private readonly CsvTelemetryImporter _importer = new();

    public CsvImportWindow()
    {
        InitializeComponent();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Select telemetry CSV file",
            Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        ResetPreview();

        try
        {
            TelemetryImportResult result =
                _importer.Import(dialog.FileName);

            // Display file information.
            FilePathText.Text = dialog.FileName;
            ColumnCountText.Text =
                result.ColumnCount.ToString("N0");

            RowCountText.Text =
                result.ValidRowCount.ToString("N0");

            PreviewGrid.ItemsSource =
                result.Preview.DefaultView;

            // Display detected channels.
            DetectedChannelsText.Text =
                result.DetectedChannels.Count > 0
                    ? string.Join(", ", result.DetectedChannels)
                    : "No supported channels detected";

            // Display validation warnings.
            WarningsText.Text = result.HasWarnings
                ? string.Join(Environment.NewLine, result.Warnings)
                : "No validation warnings";

            // Update import status.
            StatusText.Text = result.HasWarnings
                ? "Validation completed with warnings"
                : "File validated successfully";

            StatusText.Foreground = result.HasWarnings
                ? System.Windows.Media.Brushes.Orange
                : System.Windows.Media.Brushes.LightGreen;

            MessageText.Text =
                $"Valid records: {result.ValidRowCount:N0} | " +
                $"Invalid rows: {result.InvalidRowCount:N0} | " +
                $"Missing values: {result.MissingValueCount:N0} | " +
                $"Empty rows: {result.EmptyRowCount:N0} | " +
                $"Delimiter: {GetDelimiterName(result.Delimiter)}";

            // Ask for confirmation before saving.
            string confirmationMessage =
                $"CSV validation completed.\n\n" +
                $"Session: {result.Session.Name}\n" +
                $"Columns: {result.ColumnCount:N0}\n" +
                $"Valid records: {result.ValidRowCount:N0}\n" +
                $"Invalid rows: {result.InvalidRowCount:N0}\n" +
                $"Missing values: {result.MissingValueCount:N0}\n" +
                $"Empty rows: {result.EmptyRowCount:N0}\n\n" +
                $"Detected channels:\n" +
                $"{string.Join(", ", result.DetectedChannels)}\n\n" +
                (result.HasWarnings
                    ? $"Warnings:\n{string.Join("\n", result.Warnings)}\n\n"
                    : string.Empty) +
                "Do you want to save this session to the database?";

            MessageBoxResult confirmation = MessageBox.Show(
                confirmationMessage,
                "Confirm Telemetry Import",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
            {
                StatusText.Text = "Import cancelled";
                StatusText.Foreground =
                    System.Windows.Media.Brushes.Orange;

                MessageText.Text =
                    "The file was validated but the session was not saved.";

                return;
            }

            // Save only after confirmation.
            TelemetryAnalyzer.App.App.Database.SaveSession(
                result.Session);

            StatusText.Text = "Session imported successfully";
            StatusText.Foreground =
                System.Windows.Media.Brushes.LightGreen;

            MessageText.Text =
                $"Session '{result.Session.Name}' was saved successfully.";

            MessageBox.Show(
                $"Telemetry session imported successfully.\n\n" +
                $"Session: {result.Session.Name}\n" +
                $"Records saved: {result.ValidRowCount:N0}",
                "Import Completed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ResetPreview();

            StatusText.Text = "Import failed";
            StatusText.Foreground =
                System.Windows.Media.Brushes.OrangeRed;

            MessageText.Text = ex.Message;

            MessageBox.Show(
                $"Could not process the selected file.\n\n{ex.Message}",
                "CSV Import Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ResetPreview()
    {
        FilePathText.Text = "No file selected";

        ColumnCountText.Text = "0";
        RowCountText.Text = "0";

        PreviewGrid.ItemsSource = null;

        DetectedChannelsText.Text = "No channels detected";
        WarningsText.Text = "No validation performed";

        StatusText.Text = "Validating file...";
        StatusText.Foreground =
            System.Windows.Media.Brushes.Orange;

        MessageText.Text = "Processing selected CSV file...";
    }

    private static string GetDelimiterName(char delimiter)
    {
        return delimiter switch
        {
            ',' => "comma",
            ';' => "semicolon",
            '\t' => "tab",
            _ => "unknown"
        };
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
