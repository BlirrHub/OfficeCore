using System;
using System.IO;
using System.Linq;
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

            // ── Production Security: disable right-click menu and DevTools (F12) ──
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;


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
        DisasterRecoveryPanel.Visibility = Visibility.Collapsed;
        LanUrlTextBlock.Text = NetworkHelper.GetServerShareUrl(5026);
    }

    private void ShowDisasterRecoveryView()
    {
        WindowTopHeader.Visibility = Visibility.Visible;
        ServerDashboardPanel.Visibility = Visibility.Collapsed;
        WebViewContainer.Visibility = Visibility.Collapsed;
        DisasterRecoveryPanel.Visibility = Visibility.Visible;
        _ = LoadDisasterRecoveryDataAsync();
    }

    private void ShowWebView(string url)
    {
        WindowTopHeader.Visibility = Visibility.Collapsed;
        ServerDashboardPanel.Visibility = Visibility.Collapsed;
        DisasterRecoveryPanel.Visibility = Visibility.Collapsed;
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
        ShowDisasterRecoveryView();
    }

    #endregion

    #region Disaster Recovery & Database Backups

    private readonly DesktopBackupService _backupService = new();
    private BackupDashboardData? _currentBackupData;

    private async Task LoadDisasterRecoveryDataAsync()
    {
        try
        {
            DrActionFeedbackText.Text = "Loading snapshots...";
            DrActionFeedbackText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FACC15"));

            var isRunning = _serverManager.CurrentStatus == ServerStatus.Running;
            var data = await _backupService.LoadDashboardDataAsync(isRunning);
            _currentBackupData = data;

            // Engine status
            if (data.IsPgToolsAvailable)
            {
                DrEngineStatusText.Text = "✓ Ready (pg_dump detected)";
                DrEngineStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
            }
            else
            {
                DrEngineStatusText.Text = "✗ Missing pg_dump.exe";
                DrEngineStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
            }
            DrEngineBinPathText.Text = !string.IsNullOrEmpty(data.PgBinPath) ? data.PgBinPath : "Configure path in settings below";

            // Snapshots count
            var total = data.Backups.Count;
            var autoCount = data.Backups.Count(b => b.IsAutomatic);
            var preCount = data.Backups.Count(b => b.IsPreRestore);
            var manualCount = total - autoCount - preCount;

            DrTotalSnapshotsText.Text = $"{total} archives";
            DrSnapshotsBreakdownText.Text = $"{manualCount} manual, {autoCount} auto, {preCount} safety";

            // Storage footprint
            DrStorageFootprintText.Text = BackupItemViewModel.FormatBytes(data.TotalSizeBytes);

            // Auto-schedule
            if (data.Settings.AutoBackupEnabled)
            {
                DrScheduleRoutineText.Text = $"Daily at {data.Settings.ScheduledTime}";
                DrRetentionText.Text = $"Retaining last {data.Settings.RetentionCount} backups";
            }
            else
            {
                DrScheduleRoutineText.Text = "Schedule Disabled";
                DrRetentionText.Text = "Automated backups turned off";
            }

            // Sync settings form
            AutoBackupEnabledCheck.IsChecked = data.Settings.AutoBackupEnabled;
            AutoBackupTimeInput.Text = data.Settings.ScheduledTime;
            AutoBackupRetentionInput.Text = data.Settings.RetentionCount.ToString();
            CustomPgBinPathInput.Text = data.Settings.CustomPgBinPath ?? "";

            // Bind list
            SnapshotsItemsControl.ItemsSource = data.Backups;
            NoBackupsPlaceholder.Visibility = data.Backups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            DrActionFeedbackText.Text = "";
        }
        catch (Exception ex)
        {
            DrActionFeedbackText.Text = $"Error loading data: {ex.Message}";
            DrActionFeedbackText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
        }
    }

    private void RefreshBackups_Click(object sender, RoutedEventArgs e)
    {
        _ = LoadDisasterRecoveryDataAsync();
    }

    private void OpenBackupsFolder_Click(object sender, RoutedEventArgs e)
    {
        _backupService.OpenBackupsFolderInExplorer();
    }

    private void ToggleScheduleSettings_Click(object sender, RoutedEventArgs e)
    {
        ScheduleSettingsCard.Visibility = ScheduleSettingsCard.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private async void SaveScheduleSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(AutoBackupRetentionInput.Text.Trim(), out var retention) || retention < 1)
            retention = 14;

        var settings = new BackupSettingsData
        {
            AutoBackupEnabled = AutoBackupEnabledCheck.IsChecked == true,
            ScheduledTime = string.IsNullOrWhiteSpace(AutoBackupTimeInput.Text) ? "23:00" : AutoBackupTimeInput.Text.Trim(),
            RetentionCount = retention,
            CustomPgBinPath = string.IsNullOrWhiteSpace(CustomPgBinPathInput.Text) ? null : CustomPgBinPathInput.Text.Trim()
        };

        var saved = await _backupService.SaveSettingsAsync(settings);
        if (saved)
        {
            ScheduleSettingsFeedback.Text = "✓ Backup schedule and retention settings saved successfully!";
            ScheduleSettingsFeedback.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
            _ = LoadDisasterRecoveryDataAsync();
        }
        else
        {
            ScheduleSettingsFeedback.Text = "✗ Failed to save settings.";
            ScheduleSettingsFeedback.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
        }
    }

    private async void CreateSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (_serverManager.CurrentStatus != ServerStatus.Running)
        {
            MessageBox.Show("Please start the OfficeCore server before creating a snapshot.", "Server Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        CreateSnapshotButton.IsEnabled = false;
        DrActionFeedbackText.Text = "Creating complete database snapshot...";
        DrActionFeedbackText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FACC15"));

        var (success, msg) = await _backupService.CreateSnapshotAsync("Manual Server Station Snapshot");
        DrActionFeedbackText.Text = success ? $"✓ {msg}" : $"✗ {msg}";
        DrActionFeedbackText.Foreground = success 
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));

        CreateSnapshotButton.IsEnabled = true;
        _ = LoadDisasterRecoveryDataAsync();
    }

    private async void UploadBackup_Click(object sender, RoutedEventArgs e)
    {
        if (_serverManager.CurrentStatus != ServerStatus.Running)
        {
            MessageBox.Show("Please start the OfficeCore server before importing a backup.", "Server Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select PostgreSQL Backup File to Restore",
            Filter = "PostgreSQL Dump (*.dump;*.sql)|*.dump;*.sql|All Files (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            var confirm = MessageBox.Show(
                $"WARNING: You are about to import and restore:\n{dlg.FileName}\n\n" +
                "Restoring will take an automatic safety pre-restore snapshot first, then restore the entire database.\n\n" +
                "Are you sure you want to proceed?",
                "Confirm Disaster Recovery Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            DrActionFeedbackText.Text = "Importing and restoring database (this may take several minutes)...";
            DrActionFeedbackText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FACC15"));

            var (success, msg) = await _backupService.UploadAndRestoreAsync(dlg.FileName);
            DrActionFeedbackText.Text = success ? $"✓ {msg}" : $"✗ {msg}";
            DrActionFeedbackText.Foreground = success 
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));

            _ = LoadDisasterRecoveryDataAsync();
        }
    }

    private async void ItemRestore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not string fileName || string.IsNullOrWhiteSpace(fileName))
            return;

        if (_serverManager.CurrentStatus != ServerStatus.Running)
        {
            MessageBox.Show("Please start the OfficeCore server before restoring a backup.", "Server Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"WARNING: You are about to restore the system database from:\n{fileName}\n\n" +
            "An automatic safety pre-restore snapshot will be taken first, and then all records will be restored to this exact point in time.\n\n" +
            "Do you want to proceed?",
            "Confirm Database Restoration", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        DrActionFeedbackText.Text = $"Restoring database from '{fileName}' (safety snapshot underway)...";
        DrActionFeedbackText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FACC15"));

        var (success, msg) = await _backupService.RestoreSnapshotAsync(fileName);
        DrActionFeedbackText.Text = success ? $"✓ {msg}" : $"✗ {msg}";
        DrActionFeedbackText.Foreground = success 
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));

        _ = LoadDisasterRecoveryDataAsync();
    }

    private void ItemExport_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not string filePath || !File.Exists(filePath))
            return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Database Snapshot Copy",
            FileName = Path.GetFileName(filePath),
            Filter = "PostgreSQL Dump (*.dump)|*.dump|All Files (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                File.Copy(filePath, dlg.FileName, overwrite: true);
                DrActionFeedbackText.Text = $"✓ Snapshot successfully exported to: {dlg.FileName}";
                DrActionFeedbackText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export file: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void ItemDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not string fileName || string.IsNullOrWhiteSpace(fileName))
            return;

        var confirm = MessageBox.Show(
            $"Are you sure you want to delete database snapshot:\n{fileName}?",
            "Delete Snapshot", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var deleted = await _backupService.DeleteSnapshotAsync(fileName);
        if (deleted)
        {
            DrActionFeedbackText.Text = $"✓ Snapshot '{fileName}' deleted.";
            DrActionFeedbackText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
            _ = LoadDisasterRecoveryDataAsync();
        }
        else
        {
            DrActionFeedbackText.Text = $"✗ Failed to delete snapshot '{fileName}'.";
            DrActionFeedbackText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
        }
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

    private void GoBackButton_Click(object sender, RoutedEventArgs e)
    {
        if (webView.CoreWebView2 != null && webView.CoreWebView2.CanGoBack)
        {
            webView.CoreWebView2.GoBack();
        }
    }

    private void GoForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (webView.CoreWebView2 != null && webView.CoreWebView2.CanGoForward)
        {
            webView.CoreWebView2.GoForward();
        }
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
