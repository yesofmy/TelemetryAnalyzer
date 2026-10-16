
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TelemetryAnalyzer.App.Data;
using TelemetryAnalyzer.App.Models;

namespace TelemetryAnalyzer.App;

public partial class MainWindow : Window
{
    private readonly List<UIElement> _dashboardElements = [];

    private static readonly SolidColorBrush PanelBrush =
        new(Color.FromRgb(26, 30, 37));

    private static readonly SolidColorBrush PanelLightBrush =
        new(Color.FromRgb(36, 42, 51));

    private static readonly SolidColorBrush BorderBrushColor =
        new(Color.FromRgb(48, 54, 65));

    private static readonly SolidColorBrush AccentBrush =
        new(Color.FromRgb(255, 159, 67));

    private static readonly SolidColorBrush MutedBrush =
        new(Color.FromRgb(146, 153, 165));

    private static readonly SolidColorBrush WhiteBrush =
        new(Color.FromRgb(255, 255, 255));

    private static readonly SolidColorBrush GreenBrush =
        new(Color.FromRgb(85, 214, 160));

    public MainWindow()
    {
        InitializeComponent();

        _dashboardElements.AddRange(
            MainContent.Children.Cast<UIElement>());

        SetActiveNavigation(NavDashboard);
        RefreshDashboard();
    }

    // ---------------------------------------------------------
    // NAVIGATION
    // ---------------------------------------------------------

    private void SetActiveNavigation(Button activeButton)
    {
        Button[] buttons =
        [
            NavDashboard,
            NavSessions,
            NavTelemetry,
            NavAnalysis,
            NavTracks,
            NavSettings
        ];

        foreach (Button button in buttons)
        {
            button.Background = Brushes.Transparent;
            button.Foreground =
                new SolidColorBrush(Color.FromRgb(174, 181, 192));
            button.FontWeight = FontWeights.Normal;
        }

        activeButton.Background =
            new SolidColorBrush(Color.FromRgb(48, 39, 31));

        activeButton.Foreground = AccentBrush;
        activeButton.FontWeight = FontWeights.SemiBold;
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e)
    {
        MainContent.Children.Clear();

        foreach (UIElement element in _dashboardElements)
        {
            MainContent.Children.Add(element);
        }

        PageTitle.Text = "Dashboard";
        PageSubtitle.Text = "Overview of your telemetry workspace";

        SetActiveNavigation(NavDashboard);
        RefreshDashboard();
    }

    private void Sessions_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNavigation(NavSessions);

        PageTitle.Text = "Sessions & Import";
        PageSubtitle.Text = "Browse and manage imported telemetry sessions";

        ShowSessionsPage();
    }

    private void Telemetry_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNavigation(NavTelemetry);

        TelemetryPlotTestWindow window = new()
        {
            Owner = this
        };

        window.ShowDialog();
        RefreshDashboard();
    }

    private void Analysis_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNavigation(NavAnalysis);

        SignalAnalysisWindow window = new()
        {
            Owner = this
        };

        window.ShowDialog();
        RefreshDashboard();
    }

    private void Tracks_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNavigation(NavTracks);

        PageTitle.Text = "Tracks";
        PageSubtitle.Text = "Circuit library and track visualization";

        ShowTracksPage();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNavigation(NavSettings);

        PageTitle.Text = "Settings";
        PageSubtitle.Text = "Application information and local storage";

        ShowSettingsPage();
    }

    // ---------------------------------------------------------
    // DASHBOARD REFRESH
    // ---------------------------------------------------------

    private void RefreshDashboard()
    {
        try
        {
            List<TelemetrySession> sessions =
                App.Database.GetSessions();

            int totalRecords = sessions.Sum(
                session => session.Records.Count);

            SessionsValue.Text = sessions.Count.ToString("N0");
            DataPointsValue.Text = totalRecords.ToString("N0");

            List<TelemetrySession> recentSessions =
                sessions.Take(5).ToList();

            RecentSessionsList.ItemsSource = recentSessions;

            EmptySessionsPanel.Visibility =
                recentSessions.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            DatabaseStatusText.Text = "Connected";
            DatabaseStatusText.Foreground = GreenBrush;

            TelemetryStatusText.Text =
                totalRecords > 0 ? "Data available" : "Not imported";

            TelemetryStatusText.Foreground =
                totalRecords > 0 ? GreenBrush : AccentBrush;
        }
        catch (Exception)
        {
            SessionsValue.Text = "—";
            DataPointsValue.Text = "—";

            RecentSessionsList.ItemsSource = null;
            EmptySessionsPanel.Visibility = Visibility.Visible;

            DatabaseStatusText.Text = "Unavailable";
            DatabaseStatusText.Foreground =
                new SolidColorBrush(Color.FromRgb(255, 93, 93));

            TelemetryStatusText.Text = "Unavailable";
            TelemetryStatusText.Foreground =
                new SolidColorBrush(Color.FromRgb(255, 93, 93));
        }
    }

    // ---------------------------------------------------------
    // CSV IMPORT
    // ---------------------------------------------------------

    private void ImportCsv_Click(object sender, RoutedEventArgs e)
    {
        CsvImportWindow window = new()
        {
            Owner = this
        };

        window.ShowDialog();

        RefreshDashboard();

        if (PageTitle.Text == "Sessions & Import")
        {
            ShowSessionsPage();
        }
    }

    // ---------------------------------------------------------
    // SESSIONS PAGE
    // ---------------------------------------------------------

    private void ShowSessionsPage()
    {
        MainContent.Children.Clear();

        Border introPanel = CreatePanel();

        StackPanel introContent = new();

        introContent.Children.Add(
            CreateHeading("Your telemetry sessions"));

        introContent.Children.Add(CreateMutedText(
            "Review imported files and the number of telemetry records in each session.",
            8));

        Button importButton = CreatePrimaryButton("+  Import CSV");

        importButton.Margin = new Thickness(0, 18, 0, 0);
        importButton.HorizontalAlignment = HorizontalAlignment.Left;
        importButton.Click += ImportCsv_Click;

        introContent.Children.Add(importButton);
        introPanel.Child = introContent;

        MainContent.Children.Add(introPanel);

        Border listPanel = CreatePanel();
        listPanel.Margin = new Thickness(0, 16, 0, 0);

        StackPanel listContent = new();

        listContent.Children.Add(CreateHeading("Imported sessions"));

        listContent.Children.Add(CreateMutedText(
            "The list is loaded from your local database.",
            6));

        try
        {
            List<TelemetrySession> sessions =
                App.Database.GetSessions();

            if (sessions.Count == 0)
            {
                listContent.Children.Add(CreateEmptyMessage(
                    "No sessions have been imported yet.",
                    "Use Import CSV to add your first telemetry file."));
            }
            else
            {
                TextBlock countText = CreateMutedText(
                    $"{sessions.Count:N0} session(s) · " +
                    $"{sessions.Sum(s => s.Records.Count):N0} records",
                    16);

                listContent.Children.Add(countText);

                foreach (TelemetrySession session in sessions)
                {
                    listContent.Children.Add(CreateSessionCard(session));
                }
            }
        }
        catch (Exception ex)
        {
            listContent.Children.Add(CreateErrorMessage(
                "Unable to load sessions",
                ex.Message));
        }

        listPanel.Child = listContent;
        MainContent.Children.Add(listPanel);
    }

    private Border CreateSessionCard(TelemetrySession session)
    {
        Border card = new()
        {
            Background = PanelLightBrush,
            BorderBrush = BorderBrushColor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 12, 0, 0)
        };

        Grid grid = new();

        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });

        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = GridLength.Auto
        });

        StackPanel details = new();

        details.Children.Add(new TextBlock
        {
            Text = session.Name,
            Foreground = WhiteBrush,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        details.Children.Add(new TextBlock
        {
            Text = $"Imported: {session.ImportedAt:yyyy-MM-dd HH:mm}",
            Foreground = MutedBrush,
            FontSize = 11,
            Margin = new Thickness(0, 6, 0, 0)
        });

        Grid.SetColumn(details, 0);
        grid.Children.Add(details);

        StackPanel recordInfo = new()
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0)
        };

        recordInfo.Children.Add(new TextBlock
        {
            Text = session.Records.Count.ToString("N0"),
            Foreground = AccentBrush,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right
        });

        recordInfo.Children.Add(new TextBlock
        {
            Text = "records",
            Foreground = MutedBrush,
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Right
        });

        Grid.SetColumn(recordInfo, 1);
        grid.Children.Add(recordInfo);

        card.Child = grid;
        return card;
    }

    // ---------------------------------------------------------
    // TRACKS PAGE
    // ---------------------------------------------------------

    private void ShowTracksPage()
    {
        MainContent.Children.Clear();

        Border introPanel = CreatePanel();

        StackPanel introContent = new();

        introContent.Children.Add(CreateHeading("Circuit library"));

        introContent.Children.Add(CreateMutedText(
            "Explore circuit layouts and open a track visualization.",
            8));

        introPanel.Child = introContent;
        MainContent.Children.Add(introPanel);

        Border trackPanel = CreatePanel();
        trackPanel.Margin = new Thickness(0, 16, 0, 0);

        Grid trackGrid = new();

        trackGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(180)
        });

        trackGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });

        // Track preview placeholder
        Border preview = new()
        {
            Background = new SolidColorBrush(
                Color.FromRgb(23, 27, 34)),
            BorderBrush = BorderBrushColor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            MinHeight = 135,
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 18, 0)
        };

        Grid previewGrid = new();

        previewGrid.Children.Add(new TextBlock
        {
            Text = "SPA",
            FontSize = 27,
            FontWeight = FontWeights.Bold,
            Foreground = AccentBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        previewGrid.Children.Add(new TextBlock
        {
            Text = "BELGIUM",
            FontSize = 9,
            Foreground = MutedBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 8)
        });

        preview.Child = previewGrid;
        Grid.SetColumn(preview, 0);
        trackGrid.Children.Add(preview);

        StackPanel details = new()
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        details.Children.Add(new TextBlock
        {
            Text = "Circuit de Spa-Francorchamps",
            Foreground = WhiteBrush,
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        details.Children.Add(CreateMutedText(
            "Stavelot, Belgium · Formula 1",
            7));

        details.Children.Add(CreateMutedText(
            "Reference lap length: approximately 7.004 km",
            5));

        details.Children.Add(CreateMutedText(
            "Open the schematic circuit layout with corner markers and start/finish line.",
            10));

        Button viewButton = CreatePrimaryButton("View track  →");

        viewButton.HorizontalAlignment = HorizontalAlignment.Left;
        viewButton.Margin = new Thickness(0, 16, 0, 0);
        viewButton.Click += OpenSpaTrack_Click;

        details.Children.Add(viewButton);

        Grid.SetColumn(details, 1);
        trackGrid.Children.Add(details);

        trackPanel.Child = trackGrid;
        MainContent.Children.Add(trackPanel);

        Border infoPanel = CreatePanel();
        infoPanel.Margin = new Thickness(0, 16, 0, 0);

        StackPanel infoContent = new();

        infoContent.Children.Add(CreateHeading("Track visualization"));

        infoContent.Children.Add(CreateMutedText(
            "The current version displays a schematic track layout. " +
            "Real GPS geometry and Formula 1 telemetry can be connected in a later stage.",
            8));

        infoPanel.Child = infoContent;
        MainContent.Children.Add(infoPanel);
    }

    private void OpenSpaTrack_Click(object sender, RoutedEventArgs e)
    {
        TrackVisualizationWindow window = new()
        {
            Owner = this
        };

        window.ShowDialog();
    }

    // ---------------------------------------------------------
    // SETTINGS PAGE
    // ---------------------------------------------------------

    private void ShowSettingsPage()
    {
        MainContent.Children.Clear();

        Border appPanel = CreatePanel();

        StackPanel appContent = new();

        appContent.Children.Add(CreateHeading("Application"));

        appContent.Children.Add(CreateMutedText(
            "General information about this installation.",
            6));

        appContent.Children.Add(CreateSettingRow(
            "Application name",
            "Telemetry Analyzer"));

        appContent.Children.Add(CreateSettingRow(
            "Version",
            "0.1.0"));

        appContent.Children.Add(CreateSettingRow(
            "Framework",
            ".NET 10 / WPF"));

        appPanel.Child = appContent;
        MainContent.Children.Add(appPanel);

        Border storagePanel = CreatePanel();
        storagePanel.Margin = new Thickness(0, 16, 0, 0);

        StackPanel storageContent = new();

        storageContent.Children.Add(CreateHeading("Local data storage"));

        string appDataDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "TelemetryAnalyzer");

        string databasePath = Path.Combine(
            appDataDirectory,
            "telemetry.db");

        storageContent.Children.Add(CreateMutedText(
            "The application database is stored locally on this computer.",
            6));

        storageContent.Children.Add(new TextBlock
        {
            Text = databasePath,
            Foreground = AccentBrush,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0)
        });

        Button openFolderButton = CreateSecondaryButton(
            "Open data folder");

        openFolderButton.HorizontalAlignment = HorizontalAlignment.Left;
        openFolderButton.Margin = new Thickness(0, 16, 0, 0);

        openFolderButton.Click += (_, _) =>
        {
            try
            {
                if (!Directory.Exists(appDataDirectory))
                {
                    MessageBox.Show(
                        "The application data folder does not exist yet.",
                        "Telemetry Analyzer",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{appDataDirectory}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to open the data folder.\n{ex.Message}",
                    "Telemetry Analyzer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };

        storageContent.Children.Add(openFolderButton);

        storagePanel.Child = storageContent;
        MainContent.Children.Add(storagePanel);
    }

    // ---------------------------------------------------------
    // REUSABLE UI HELPERS
    // ---------------------------------------------------------

    private static Border CreatePanel()
    {
        return new Border
        {
            Background = PanelBrush,
            BorderBrush = BorderBrushColor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(22)
        };
    }

    private static TextBlock CreateHeading(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = WhiteBrush,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static TextBlock CreateMutedText(
        string text,
        double topMargin = 0)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = MutedBrush,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, topMargin, 0, 0)
        };
    }

    private static Button CreatePrimaryButton(string text)
    {
        return new Button
        {
            Content = text,
            Background = AccentBrush,
            Foreground = new SolidColorBrush(
                Color.FromRgb(17, 19, 24)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(17, 11, 17, 11),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand
        };
    }

    private static Button CreateSecondaryButton(string text)
    {
        return new Button
        {
            Content = text,
            Background = PanelLightBrush,
            Foreground = WhiteBrush,
            BorderBrush = BorderBrushColor,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(15, 10, 15, 10),
            FontSize = 12,
            Cursor = System.Windows.Input.Cursors.Hand
        };
    }

    private static UIElement CreateEmptyMessage(
        string title,
        string description)
    {
        Border panel = new()
        {
            Background = PanelLightBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18),
            Margin = new Thickness(0, 16, 0, 0)
        };

        StackPanel content = new();

        content.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = WhiteBrush,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold
        });

        content.Children.Add(CreateMutedText(description, 7));

        panel.Child = content;
        return panel;
    }

    private static UIElement CreateErrorMessage(
        string title,
        string description)
    {
        Border panel = new()
        {
            Background = new SolidColorBrush(
                Color.FromRgb(52, 31, 34)),
            BorderBrush = new SolidColorBrush(
                Color.FromRgb(120, 55, 60)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 16, 0, 0)
        };

        StackPanel content = new();

        content.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = new SolidColorBrush(
                Color.FromRgb(255, 120, 120)),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold
        });

        content.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = WhiteBrush,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 7, 0, 0)
        });

        panel.Child = content;
        return panel;
    }

    private static UIElement CreateSettingRow(
        string label,
        string value)
    {
        Grid grid = new()
        {
            Margin = new Thickness(0, 15, 0, 0)
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });

        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = GridLength.Auto
        });

        TextBlock labelText = new()
        {
            Text = label,
            Foreground = MutedBrush,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        TextBlock valueText = new()
        {
            Text = value,
            Foreground = WhiteBrush,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(12, 0, 0, 0)
        };

        Grid.SetColumn(labelText, 0);
        Grid.SetColumn(valueText, 1);

        grid.Children.Add(labelText);
        grid.Children.Add(valueText);

        return grid;
    }
}
