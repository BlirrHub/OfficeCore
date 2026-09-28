using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;

namespace OfficeCore.Desktop.Server;

public partial class MainWindow : Window
{
    private readonly ServerProcessManager _serverManager = new();
    private ServerConfig _config;
    private bool _isManualServerStart = false;
    private bool _pendingLaunchWhenLive = false;

    public MainWindow()
    {
        InitializeComponent();
        _config = ServerConfigService.Current;

        _serverManager.StatusChanged += OnServerStatusChanged;
        _serverManager.LogReceived += OnServerLogReceived;

        InitializeView();
    }

    private async void InitializeView()
    {
        try
        {
            var udataDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OfficeCore", "Server_WebView2");
            System.IO.Directory.CreateDirectory(udataDir);
            var env = await CoreWebView2Environment.CreateAsync(null, udataDir);

            await webView.EnsureCoreWebView2Async(env);
            webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            webView.CoreWebView2.WebResourceRequested += (s, args) =>
            {
                args.Request.Headers.SetHeader("X-OfficeCore-Client", "OfficeCore-Desktop-Workstation");
            };
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
        AutoStartServerCheckBox.IsChecked = _config.AutoStartServer;

        LanUrlTextBlock.Text = NetworkHelper.GetServerShareUrl(5026);

        if (_config.AutoStartServer && _serverManager.CurrentStatus == ServerStatus.Stopped)
        {
            _isManualServerStart = false;
            _ = _serverManager.StartServerAsync(_config);
        }
    }

    #region View Navigation

    private void ShowServerDashboardView()
    {
        WindowTopHeader.Visibility = Visibility.Visible;
        ServerDashboardPanel.Visibility = Visibility.Visible;
        WebViewContainer.Visibility = Visibility.Collapsed;
        LanUrlTextBlock.Text = NetworkHelper.GetServerShareUrl(5026);
    }

    private void ShowWebView(string url)
    {
        WindowTopHeader.Visibility = Visibility.Collapsed;
        ServerDashboardPanel.Visibility = Visibility.Collapsed;
        WebViewContainer.Visibility = Visibility.Visible;
        WebFullToolbar.Visibility = Visibility.Visible;
        WebMiniBar.Visibility = Visibility.Collapsed;

        webView.CoreWebView2?.Navigate(url);
    }

    #endregion

    #region Server Dashboard Events

    private async void StartServer_Click(object sender, RoutedEventArgs e)
    {
        _isManualServerStart = true;
        SyncConfigFromInputs();
        await _serverManager.StartServerAsync(_config);
    }

    private void StopServer_Click(object sender, RoutedEventArgs e)
    {
        _isManualServerStart = false;
        _pendingLaunchWhenLive = false;
        _serverManager.StopServer();
    }

    private void LaunchLocalApp_Click(object sender, RoutedEventArgs e)
    {
        if (_serverManager.CurrentStatus == ServerStatus.Running)
        {
            ShowWebView("http://localhost:5026");
            return;
        }

        if (_serverManager.CurrentStatus == ServerStatus.Starting)
        {
            _pendingLaunchWhenLive = true;
            MessageBox.Show(
                "The server is currently starting up.\n\nOfficeCore will open automatically as soon as the server is live and ready.",
                "Starting Server", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            "The server is not currently running. Would you like to start it now?",
            "Server Not Running", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            SyncConfigFromInputs();
            _pendingLaunchWhenLive = true;
            _ = _serverManager.StartServerAsync(_config);
        }
        // If result == MessageBoxResult.No, we intentionally do nothing so the user stays on the Server Station dashboard.
    }

    private void AutoStartServerCheckBox_Click(object sender, RoutedEventArgs e)
    {
        _config.AutoStartServer = AutoStartServerCheckBox.IsChecked == true;
        ServerConfigService.Save(_config);
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
        ServerConfigService.Save(_config);
        DbStatusFeedback.Text = "✓ Database settings saved to server_config.json";
        DbStatusFeedback.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
    }

    private void SyncConfigFromInputs()
    {
        _config.DbHost = DbHostInput.Text.Trim();
        if (int.TryParse(DbPortInput.Text.Trim(), out var port)) _config.DbPort = port;
        _config.DbName = DbNameInput.Text.Trim();
        _config.DbUser = DbUserInput.Text.Trim();
        _config.DbPassword = DbPasswordInput.Password;
        _config.AutoStartServer = AutoStartServerCheckBox.IsChecked == true;
    }

    private void CopyLanUrl_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(LanUrlTextBlock.Text);
        MessageBox.Show($"Server Station URL copied to clipboard:\n{LanUrlTextBlock.Text}\n\nShare this address with other client workstation apps on your office network.", 
                        "Copied to Clipboard", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        ConsoleLogBox.Clear();
    }

    private async void QuickSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (_serverManager.CurrentStatus != ServerStatus.Running)
        {
            MessageBox.Show("Please start the server before creating a snapshot.", "Server Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        QuickSnapshotButton.IsEnabled = false;
        BackupStatusFeedback.Text = "Creating database snapshot...";
        BackupStatusFeedback.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FACC15"));

        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            client.DefaultRequestHeaders.Add("X-OfficeCore-Client", "OfficeCore-Desktop-Workstation");
            client.DefaultRequestHeaders.Add("X-OfficeCore-Station", "ServerStationOrchestrator");

            var payload = System.Text.Json.JsonSerializer.Serialize(new { Label = "Server Station Quick Snapshot" });
            var content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync("http://localhost:5026/api/backup/create", content);
            if (response.IsSuccessStatusCode)
            {
                BackupStatusFeedback.Text = "✓ Snapshot generated and verified successfully!";
                BackupStatusFeedback.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                BackupStatusFeedback.Text = $"✗ Snapshot failed: {err}";
                BackupStatusFeedback.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
            }
        }
        catch (Exception ex)
        {
            BackupStatusFeedback.Text = $"✗ Snapshot error: {ex.Message}";
            BackupStatusFeedback.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
        }
        finally
        {
            QuickSnapshotButton.IsEnabled = true;
        }
    }

    private void OpenBackupConsole_Click(object sender, RoutedEventArgs e)
    {
        if (_serverManager.CurrentStatus != ServerStatus.Running)
        {
            var res = MessageBox.Show(
                "The server is not currently running. Would you like to start it now to access the Disaster Recovery Console?",
                "Server Not Running", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                SyncConfigFromInputs();
                _pendingLaunchWhenLive = true;
                _ = _serverManager.StartServerAsync(_config);
            }
            return;
        }

        ShowWebView("http://localhost:5026/admin/backups");
    }

    private void OnServerStatusChanged(ServerStatus status)
    {
        Dispatcher.Invoke(() =>
        {
            switch (status)
            {
                case ServerStatus.Stopped:
                    _isManualServerStart = false;
                    _pendingLaunchWhenLive = false;
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

                    if (_pendingLaunchWhenLive)
                    {
                        _pendingLaunchWhenLive = false;
                        _isManualServerStart = false;
                        ShowWebView("http://localhost:5026");
                    }
                    else if (_isManualServerStart)
                    {
                        _isManualServerStart = false;
                        var promptResult = MessageBox.Show(
                            "OfficeCore server is now live and healthy on http://localhost:5026.\n\nWould you like to launch OfficeCore now?",
                            "Server Live", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (promptResult == MessageBoxResult.Yes)
                        {
                            ShowWebView("http://localhost:5026");
                        }
                    }
                    break;

                case ServerStatus.Error:
                    _isManualServerStart = false;
                    _pendingLaunchWhenLive = false;
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

    #region WebView Events

    private void BackToDashboard_Click(object sender, RoutedEventArgs e)
    {
        ShowServerDashboardView();
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
        if (!e.IsSuccess && webView.Source != null && webView.Source.ToString() != "about:blank")
        {
            MessageBox.Show(
                $"Failed to connect to the server at {webView.Source}.\n\nEnsure the OfficeCore Server Station is running.",
                "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);

            ShowServerDashboardView();
        }
    }

    #endregion

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _serverManager.Dispose();
        base.OnClosing(e);
    }
}
