using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace OfficeCore.Client.Installer;

public partial class MainWindow : Window
{
    private readonly string _installDir;
    private bool _isCompleted = false;

    public MainWindow()
    {
        InitializeComponent();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _installDir = Path.Combine(localAppData, "Programs", "OfficeCore Client");
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
                    StatusMessageText.Text = "Extracting OfficeCore Workstation files...";
                });

                var destExe = Path.Combine(_installDir, "OfficeCore.Client.exe");
                var currentExe = Process.GetCurrentProcess().MainModule?.FileName;

                // 1. Extract embedded resource if available
                var assembly = Assembly.GetExecutingAssembly();
                using var resourceStream = assembly.GetManifestResourceStream("OfficeCore.Client.Installer.OfficeCore.Client.exe") 
                                           ?? assembly.GetManifestResourceStream("OfficeCore.Client.exe");

                if (resourceStream != null)
                {
                    using var fileStream = new FileStream(destExe, FileMode.Create, FileAccess.Write);
                    resourceStream.CopyTo(fileStream);
                }
                else
                {
                    // Fallback to local file or development build paths
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string[] candidatePaths = [
                        Path.Combine(baseDir, "OfficeCore.Client.exe"),
                        Path.Combine(baseDir, "..", "..", "..", "..", "OfficeCore.Desktop.Client", "bin", "Debug", "net9.0-windows", "OfficeCore.Client.exe"),
                        Path.Combine(baseDir, "..", "..", "..", "..", "OfficeCore.Desktop.Client", "bin", "Release", "net9.0-windows", "OfficeCore.Client.exe"),
                        Path.Combine(baseDir, "..", "..", "..", "..", "dist", "OfficeCore-Client-Portable", "OfficeCore.Client.exe")
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

                // Copy this installer as uninstaller
                if (!string.IsNullOrEmpty(currentExe) && File.Exists(currentExe))
                {
                    var destUninstall = Path.Combine(_installDir, "Uninstall.exe");
                    File.Copy(currentExe, destUninstall, true);
                }

                Dispatcher.Invoke(() =>
                {
                    InstallProgressBar.Value = 65;
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
                    CreateShortcut(Path.Combine(desktopDir, "OfficeCore Workstation.lnk"), destExe, "OfficeCore Workstation Terminal");
                }

                if (createStartMenu)
                {
                    var startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "OfficeCore");
                    if (!Directory.Exists(startMenuDir)) Directory.CreateDirectory(startMenuDir);
                    CreateShortcut(Path.Combine(startMenuDir, "OfficeCore Workstation.lnk"), destExe, "OfficeCore Workstation Terminal");
                }

                // 3. AutoStartup on Device Turn-On (Windows Startup Run Registry)
                try
                {
                    using var runKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                    if (runKey != null)
                    {
                        if (autoStartup)
                        {
                            runKey.SetValue("OfficeCore Workstation", $"\"{destExe}\"");
                        }
                        else
                        {
                            runKey.DeleteValue("OfficeCore Workstation", false);
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
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\OfficeCoreClient");
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "OfficeCore Workstation");
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
                var destExe = Path.Combine(_installDir, "OfficeCore.Client.exe");
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
}
