using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;

namespace OfficeCore.DesktopClient;

public partial class MainWindow : Window
{
    private readonly ServerProcessManager _serverManager = new();
    private DesktopConfig _config;

    public MainWindow()
    {
        InitializeComponent();
        _config = DesktopConfigService.Current;

        // Wire up ServerProcessManager events
        _serverManager.StatusChanged += OnServerStatusChanged;
        _serverManager.LogReceived += OnServerLogReceived;

        InitializeView();
    }

    private async void InitializeView()
    {
        try
        {
            await webView.EnsureCoreWebView2Async(null);
            webView.NavigationCompleted += WebView_NavigationCompleted;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize WebView2: {ex.Message}", "WebView Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // Initialize UI with saved configuration
        DbHostInput.Text = _config.DbHost;
        DbPortInput.Text = _config.DbPort.ToString();
        DbNameInput.Text = _config.DbName;
        DbUserInput.Text = _config.DbUser;
        DbPasswordInput.Password = _config.DbPassword;
        ClientServerUrlInput.Text = _config.ServerUrl;
        RememberChoiceCheckBox.IsChecked = _config.RememberChoice;

        LanUrlTextBlock.Text = NetworkHelper.GetServerShareUrl(5026);

        // Check if user chose to remember their mode
        if (_config.RememberChoice && _config.SelectedMode != DesktopMode.None)
        {
            if (_config.SelectedMode == DesktopMode.Server)
            {
                ShowServerDashboardView();
                // Auto-start server if in remembered server mode
                _ = _serverManager.StartServerAsync(_config);
            }
            else if (_config.SelectedMode == DesktopMode.Client)
            {
                ShowClientConnectView();
            }
        }
        else
        {
            ShowModeSelectView();
        }
    }

    #region View Navigation Helpers

    private void HideAllViews()
    {
        WindowTopHeader.Visibility = Visibility.Visible;
        ModeSelectPanel.Visibility = Visibility.Collapsed;
        ServerDashboardPanel.Visibility = Visibility.Collapsed;
        ClientConnectPanel.Visibility = Visibility.Collapsed;
        WebViewContainer.Visibility = Visibility.Collapsed;
    }

    private void ShowModeSelectView()
    {
        HideAllViews();
        ModeSelectPanel.Visibility = Visibility.Visible;
        ModeBadge.Visibility = Visibility.Collapsed;
        SwitchModeButton.Visibility = Visibility.Collapsed;
    }

    private void ShowServerDashboardView()
    {
        HideAllViews();
        ServerDashboardPanel.Visibility = Visibility.Visible;
        ModeBadge.Visibility = Visibility.Visible;
        SwitchModeButton.Visibility = Visibility.Visible;
        BackToDashboardButton.Content = "← Server Dashboard";

        ModeBadgeText.Text = "Server Station (Host)";
        ModeBadgeDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C026D3"));
        LanUrlTextBlock.Text = NetworkHelper.GetServerShareUrl(5026);
    }

    private void ShowClientConnectView()
    {
        HideAllViews();
        ClientConnectPanel.Visibility = Visibility.Visible;
        ModeBadge.Visibility = Visibility.Visible;
        SwitchModeButton.Visibility = Visibility.Visible;
        BackToDashboardButton.Content = "← Disconnect";

        ModeBadgeText.Text = "Client Terminal";
        ModeBadgeDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
    }

    private void ShowWebView(string url)
    {
        HideAllViews();
        WindowTopHeader.Visibility = Visibility.Collapsed;
        WebViewContainer.Visibility = Visibility.Visible;
        WebFullToolbar.Visibility = Visibility.Visible;
        WebMiniBar.Visibility = Visibility.Collapsed;
        CurrentConnectedUrlText.Text = url;

        if (_config.SelectedMode == DesktopMode.Server)
        {
            BackToDashboardButton.Content = "← Server Dashboard";
            WebMiniBarText.Text = "🖥️ Host Controls ▾";
        }
        else
        {
            BackToDashboardButton.Content = "← Disconnect";
            WebMiniBarText.Text = "💻 Terminal Controls ▾";
        }

        webView.CoreWebView2?.Navigate(url);
    }

    #endregion

    #region Mode Selection Events

    private void SelectServerMode_Click(object sender, RoutedEventArgs e)
    {
        _config.SelectedMode = DesktopMode.Server;
        _config.RememberChoice = RememberChoiceCheckBox.IsChecked == true;
        DesktopConfigService.Save(_config);

        ShowServerDashboardView();
    }

    private void SelectClientMode_Click(object sender, RoutedEventArgs e)
    {
        _config.SelectedMode = DesktopMode.Client;
        _config.RememberChoice = RememberChoiceCheckBox.IsChecked == true;
        DesktopConfigService.Save(_config);

        ShowClientConnectView();
    }

    private void SwitchModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_serverManager.CurrentStatus == ServerStatus.Running)
        {
            var result = MessageBox.Show(
                "The server is currently running. Switching modes will stop the server and disconnect active workstations. Continue?",
                "Confirm Switch Mode", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;
            _serverManager.StopServer();
        }

        _config.RememberChoice = false;
        _config.SelectedMode = DesktopMode.None;
        DesktopConfigService.Save(_config);

        ShowModeSelectView();
    }

    #endregion

    #region Server Dashboard Events

    private async void StartServer_Click(object sender, RoutedEventArgs e)
    {
        SyncConfigFromInputs();
        await _serverManager.StartServerAsync(_config);
    }

    private void StopServer_Click(object sender, RoutedEventArgs e)
    {
        _serverManager.StopServer();
    }

    private void LaunchLocalApp_Click(object sender, RoutedEventArgs e)
    {
        if (_serverManager.CurrentStatus != ServerStatus.Running)
        {
            var result = MessageBox.Show(
                "The server is not currently running. Would you like to start it now?",
                "Server Not Running", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SyncConfigFromInputs();
                _ = _serverManager.StartServerAsync(_config);
            }
        }

        ShowWebView("http://localhost:5026");
    }

    private async void TestDbConnection_Click(object sender, RoutedEventArgs e)
    {
        DbStatusFeedback.Text = "Testing connection to PostgreSQL...";
        DbStatusFeedback.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FACC15"));

        var host = DbHostInput.Text.Trim();
        if (!int.TryParse(DbPortInput.Text.Trim(), out var port)) port = 5432;

        var isReachable = await _serverManager.TestDatabasePortAsync(host, port);

        if (isReachable)
        {
            DbStatusFeedback.Text = $"✓ PostgreSQL is active and listening on {host}:{port}";
            DbStatusFeedback.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
        }
        else
        {
            DbStatusFeedback.Text = $"✗ Cannot connect to PostgreSQL on {host}:{port}. Is the service running?";
            DbStatusFeedback.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
        }
    }

    private void SaveDbSettings_Click(object sender, RoutedEventArgs e)
    {
        SyncConfigFromInputs();
        DesktopConfigService.Save(_config);
        DbStatusFeedback.Text = "✓ Database settings saved to desktop_config.json";
        DbStatusFeedback.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
    }

    private void SyncConfigFromInputs()
    {
        _config.DbHost = DbHostInput.Text.Trim();
        if (int.TryParse(DbPortInput.Text.Trim(), out var port)) _config.DbPort = port;
        _config.DbName = DbNameInput.Text.Trim();
        _config.DbUser = DbUserInput.Text.Trim();
        _config.DbPassword = DbPasswordInput.Password;
    }

    private void CopyLanUrl_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(LanUrlTextBlock.Text);
        MessageBox.Show($"Server URL copied to clipboard:\n{LanUrlTextBlock.Text}\n\nShare this with other terminals on your office network.", 
                        "Copied to Clipboard", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        ConsoleLogBox.Clear();
    }

    private void OnServerStatusChanged(ServerStatus status)
    {
        Dispatcher.Invoke(() =>
        {
            switch (status)
            {
                case ServerStatus.Stopped:
                    ServerStatusText.Text = "STOPPED";
                    ServerStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A1A1AA"));
                    ServerStatusPill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27272A"));
                    StartServerButton.IsEnabled = true;
                    StopServerButton.IsEnabled = false;
                    break;

                case ServerStatus.Starting:
                    ServerStatusText.Text = "STARTING...";
                    ServerStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FACC15"));
                    ServerStatusPill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#713F12"));
                    StartServerButton.IsEnabled = false;
                    StopServerButton.IsEnabled = true;
                    break;

                case ServerStatus.Running:
                    ServerStatusText.Text = "RUNNING (LIVE)";
                    ServerStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
                    ServerStatusPill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#064E3B"));
                    StartServerButton.IsEnabled = false;
                    StopServerButton.IsEnabled = true;
                    break;

                case ServerStatus.Error:
                    ServerStatusText.Text = "ERROR";
                    ServerStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
                    ServerStatusPill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F1D1D"));
                    StartServerButton.IsEnabled = true;
                    StopServerButton.IsEnabled = false;
                    break;
            }
        });
    }

    private void OnServerLogReceived(string line)
    {
        Dispatcher.Invoke(() =>
        {
            ConsoleLogBox.AppendText($"{line}\n");
            ConsoleLogBox.ScrollToEnd();
        });
    }

    #endregion

    #region Client Connection Events

    private void ClientConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var url = ClientServerUrlInput.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "http://" + url;
        }

        _config.ServerUrl = url;
        DesktopConfigService.Save(_config);

        ShowWebView(url);
    }

    #endregion

    #region WebView Events

    private void BackToDashboard_Click(object sender, RoutedEventArgs e)
    {
        if (_config.SelectedMode == DesktopMode.Server)
        {
            ShowServerDashboardView();
        }
        else
        {
            ShowClientConnectView();
        }
    }

    private void ReloadWebView_Click(object sender, RoutedEventArgs e)
    {
        webView.CoreWebView2?.Reload();
    }

    private void ToggleWebToolbar_Click(object sender, RoutedEventArgs e)
    {
        if (WebFullToolbar.Visibility == Visibility.Visible)
        {
            WebFullToolbar.Visibility = Visibility.Collapsed;
            WebMiniBar.Visibility = Visibility.Visible;
        }
        else
        {
            WebFullToolbar.Visibility = Visibility.Visible;
            WebMiniBar.Visibility = Visibility.Collapsed;
        }
    }

    private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            MessageBox.Show(
                $"Failed to connect to the server at {webView.Source}.\n\nEnsure the OfficeCore Server Station is running and accessible on your network.",
                "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);

            if (_config.SelectedMode == DesktopMode.Server)
                ShowServerDashboardView();
            else
                ShowClientConnectView();
        }
    }

    #endregion

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Stop server process cleanly if closing
        _serverManager.Dispose();
        base.OnClosing(e);
    }
}