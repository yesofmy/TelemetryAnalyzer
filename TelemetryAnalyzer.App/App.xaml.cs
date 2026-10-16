using System.Windows;
using TelemetryAnalyzer.App.Data;

namespace TelemetryAnalyzer.App;

public partial class App : Application
{
    public static TelemetryDatabase Database { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            Database.Initialize();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not initialize the telemetry database.\n\n{ex.Message}",
                "Database Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
        }
    }
}
