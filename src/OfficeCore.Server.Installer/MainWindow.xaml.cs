using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace OfficeCore.Server.Installer;

public partial class MainWindow : Window
{
    private readonly string _installDir;
    private bool _isCompleted = false;

    public MainWindow()
    {
        InitializeComponent();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _installDir = Path.Combine(localAppData, "Programs", "OfficeCore Server");
        InstallPathBox.Text = _installDir;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCompleted)
        {
            Close();
            return;
        }

        InstallButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        InstallProgressBar.Visibility = Visibility.Visible;
        InstallProgressBar.Value = 10;
        StatusMessageText.Text = "Creating installation directory...";
        StatusMessageText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FACC15"));

        try
        {
            await Task.Run(() =>
            {
                if (!Directory.Exists(_installDir))
                {
                    Directory.CreateDirectory(_installDir);
                }

                Dispatcher.Invoke(() =>
                {
                    InstallProgressBar.Value = 30;
                    StatusMessageText.Text = "Extracting OfficeCore Server Station files...";
                });

                var destExe = Path.Combine(_installDir, "OfficeCore.ServerStation.exe");
                var currentExe = Process.GetCurrentProcess().MainModule?.FileName;

                // 1. Extract embedded resource if available
                var assembly = Assembly.GetExecutingAssembly();
                using var resourceStream = assembly.GetManifestResourceStream("OfficeCore.Server.Installer.OfficeCore.ServerStation.exe") 
                                           ?? assembly.GetManifestResourceStream("OfficeCore.ServerStation.exe");

                if (resourceStream != null)
                {
                    using var fileStream = new FileStream(destExe, FileMode.Create, FileAccess.Write);
                    resourceStream.CopyTo(fileStream);
                }
                else
                {
                    // Fallback to local files or sibling build paths
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string[] candidatePaths = [
                        Path.Combine(baseDir, "OfficeCore.ServerStation.exe"),
                        Path.Combine(baseDir, "..", "..", "..", "..", "OfficeCore.Desktop.Server", "bin", "Debug", "net9.0-windows", "OfficeCore.ServerStation.exe"),
                        Path.Combine(baseDir, "..", "..", "..", "..", "OfficeCore.Desktop.Server", "bin", "Release", "net9.0-windows", "OfficeCore.ServerStation.exe"),
                        Path.Combine(baseDir, "..", "..", "..", "..", "dist", "OfficeCore-ServerStation-Portable", "OfficeCore.ServerStation.exe")
                    ];

                    foreach (var candidate in candidatePaths)
                    {
                        var fullCandidate = Path.GetFullPath(candidate);
                        if (File.Exists(fullCandidate))
                        {
                            File.Copy(fullCandidate, destExe, true);
                            break;
                        }
                    }
                }

                // 1b. Extract embedded Api.zip bundle or fallback to local Api directories
                var apiDir = Path.Combine(_installDir, "Api");
                if (!Directory.Exists(apiDir)) Directory.CreateDirectory(apiDir);

                using var apiZipStream = assembly.GetManifestResourceStream("OfficeCore.Server.Installer.Api.zip")
                                         ?? assembly.GetManifestResourceStream("Api.zip");

                if (apiZipStream != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        InstallProgressBar.Value = 50;
                        StatusMessageText.Text = "Extracting OfficeCore API & Web services bundle...";
                    });

                    using var archive = new ZipArchive(apiZipStream, ZipArchiveMode.Read);
                    archive.ExtractToDirectory(_installDir, overwriteFiles: true);
                }
                else
                {
                    // Fallback to local published Api directory if running unbundled installer
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string[] candidateApiDirs = [
                        Path.Combine(baseDir, "Api"),
                        Path.Combine(baseDir, "..", "..", "..", "..", "dist", "temp_api_payload", "Api"),
                        Path.Combine(baseDir, "..", "..", "..", "..", "dist", "OfficeCore-ServerStation-Portable", "Api"),
                        Path.Combine(baseDir, "..", "..", "..", "..", "dist", "OfficeCore-Api")
                    ];

                    foreach (var cand in candidateApiDirs)
                    {
                        var fullCand = Path.GetFullPath(cand);
                        if (Directory.Exists(fullCand))
                        {
                            CopyDirectory(fullCand, apiDir);
                            break;
                        }
                    }
                }

                // Copy this installer as uninstaller
                if (!string.IsNullOrEmpty(currentExe) && File.Exists(currentExe))
                {
                    var destUninstall = Path.Combine(_installDir, "Uninstall.exe");
                    File.Copy(currentExe, destUninstall, true);
                }

                Dispatcher.Invoke(() =>
                {
                    InstallProgressBar.Value = 75;
                    StatusMessageText.Text = "Creating shortcuts and system registry...";
                });

                // 2. Shortcuts & Startup Configuration
                var createDesktop = false;
                var createStartMenu = false;
                var autoStartup = false;
                Dispatcher.Invoke(() =>
                {
                    createDesktop = DesktopShortcutCheckBox.IsChecked == true;
                    createStartMenu = StartMenuShortcutCheckBox.IsChecked == true;
                    autoStartup = AutoStartupCheckBox.IsChecked == true;
                });

                if (createDesktop)
                {
                    var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(Path.Combine(desktopDir, "OfficeCore Server Station.lnk"), destExe, "OfficeCore Server Host & Database Orchestrator");
                }

                if (createStartMenu)
                {
                    var startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "OfficeCore Server");
                    if (!Directory.Exists(startMenuDir)) Directory.CreateDirectory(startMenuDir);
                    CreateShortcut(Path.Combine(startMenuDir, "OfficeCore Server Station.lnk"), destExe, "OfficeCore Server Host & Database Orchestrator");
                }

                // 3. AutoStartup on Device Turn-On (Windows Startup Run Registry)
                try
                {
                    using var runKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                    if (runKey != null)
                    {
                        if (autoStartup)
                        {
                            runKey.SetValue("OfficeCore Server Station", $"\"{destExe}\"");
                        }
                        else
                        {
                            runKey.DeleteValue("OfficeCore Server Station", false);
                        }
                    }
                }
                catch
                {
                    // Ignore registry issues on restricted environments
                }

                // 4. Register in Windows Programs & Features
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\OfficeCoreServer");
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "OfficeCore Server Station");
                        key.SetValue("DisplayVersion", "1.0.0");
                        key.SetValue("Publisher", "OfficeCore");
                        key.SetValue("InstallLocation", _installDir);
                        key.SetValue("DisplayIcon", destExe);
                        key.SetValue("UninstallString", $"\"{Path.Combine(_installDir, "Uninstall.exe")}\" /uninstall");
                    }
                }
                catch
                {
                    // Ignore registry issues on restricted environments
                }
            });

            InstallProgressBar.Value = 100;
            StatusMessageText.Text = "✓ Installation successfully completed!";
            StatusMessageText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));

            InstallButton.Content = "Finish";
            InstallButton.IsEnabled = true;
            _isCompleted = true;

            if (LaunchAfterCheckBox.IsChecked == true)
            {
                var destExe = Path.Combine(_installDir, "OfficeCore.ServerStation.exe");
                if (File.Exists(destExe))
                {
                    Process.Start(new ProcessStartInfo { FileName = destExe, UseShellExecute = true });
                    Close();
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessageText.Text = $"Installation failed: {ex.Message}";
            StatusMessageText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
            InstallButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string description)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
            shortcut.Description = description;
            shortcut.Save();
        }
        catch
        {
            // Fallback shortcut creation ignored if COM not allowed
        }
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, true);
        }
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destSub = Path.Combine(targetDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSub);
        }
    }
}
