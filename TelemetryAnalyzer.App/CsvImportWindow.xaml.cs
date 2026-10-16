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
            return;

        try
        {
            TelemetryImportResult result = _importer.Import(dialog.FileName);

            FilePathText.Text = dialog.FileName;
            ColumnCountText.Text = result.ColumnCount.ToString("N0");
            RowCountText.Text = result.ValidRowCount.ToString("N0");
            PreviewGrid.ItemsSource = result.Preview.DefaultView;

            StatusText.Text = result.InvalidRowCount == 0
                ? "File processed successfully"
                : $"Imported with {result.InvalidRowCount} invalid row(s)";

            MessageText.Text =
                $"Session: {result.Session.Name} | " +
                $"Preview: {result.Preview.Rows.Count:N0} rows | " +
                $"Invalid rows: {result.InvalidRowCount:N0} | " +
                $"Missing values: {result.MissingValueCount:N0} | " +
                $"Delimiter: {GetDelimiterName(result.Delimiter)}";

            MessageBox.Show(
                $"CSV processing completed.\n\n" +
                $"Session: {result.Session.Name}\n" +
                $"Valid telemetry records: {result.Session.RecordCount:N0}\n" +
                $"Invalid rows: {result.InvalidRowCount:N0}\n" +
                $"Missing values: {result.MissingValueCount:N0}",
                "Telemetry Analyzer",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            PreviewGrid.ItemsSource = null;
            ColumnCountText.Text = "0";
            RowCountText.Text = "0";
            StatusText.Text = "Import failed";
            MessageText.Text = ex.Message;

            MessageBox.Show(
                $"Could not process the selected file.\n\n{ex.Message}",
                "CSV Import Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
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
