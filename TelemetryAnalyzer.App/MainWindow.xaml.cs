using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace TelemetryAnalyzer.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e)
    {
        PageTitle.Text = "Dashboard";
        PageSubtitle.Text = "Overview of your telemetry workspace";
        MainContent.Visibility = Visibility.Visible;
    }

    private void Sessions_Click(object sender, RoutedEventArgs e)
    {
        ShowPlaceholder(
            "Sessions & Import",
            "Manage and import your telemetry sessions.");
    }

    private void Telemetry_Click(object sender, RoutedEventArgs e)
    {
        ShowPlaceholder(
            "Telemetry Studio",
            "Telemetry channels and lap data visualization.");
    }

    private void Analysis_Click(object sender, RoutedEventArgs e)
    {
        ShowPlaceholder(
            "Signal Analysis",
            "Signal filters, statistics and analysis tools.");
    }

    private void Tracks_Click(object sender, RoutedEventArgs e)
    {
        ShowPlaceholder(
            "Tracks",
            "Track configurations and sector reference data.");
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        ShowPlaceholder(
            "Settings",
            "Application preferences and data configuration.");
    }

    private void ShowPlaceholder(string title, string subtitle)
    {
        PageTitle.Text = title;
        PageSubtitle.Text = subtitle;
        MainContent.Visibility = Visibility.Visible;

        MainContent.Children.Clear();

        Border panel = new()
        {
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(26, 30, 37)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(48, 54, 65)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(28),
            Margin = new Thickness(0, 0, 0, 16)
        };

        StackPanel content = new();

        content.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = System.Windows.Media.Brushes.White
        });

        content.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 13,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(146, 153, 165)),
            Margin = new Thickness(0, 10, 0, 20)
        });

        content.Children.Add(new TextBlock
        {
            Text = "This section will be implemented in a later development step.",
            FontSize = 13,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(255, 159, 67)),
            TextWrapping = TextWrapping.Wrap
        });

        panel.Child = content;
        MainContent.Children.Add(panel);
    }

    private void ImportCsv_Click(object sender, RoutedEventArgs e)
    {
        CsvImportWindow window = new()
        {
            Owner = this
        };

        window.ShowDialog();
    }
}
