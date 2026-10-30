using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace OfficeCore.Server.Installer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length > 0 && e.Args[0].Equals("/uninstall", StringComparison.OrdinalIgnoreCase))
        {
            PerformUninstall();
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    private static void PerformUninstall()
    {
        var result = MessageBox.Show(
            "Are you sure you want to remove OfficeCore Server Station from this computer?",
            "Uninstall OfficeCore Server Station", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var installDir = Path.Combine(localAppData, "Programs", "OfficeCore Server");

            // Stop running ServerStation / Api processes before removing files
            foreach (var procName in new[] { "OfficeCore.ServerStation", "OfficeCore.Api" })
            {
                try
                {
                    foreach (var proc in Process.GetProcessesByName(procName))
                    {
                        try
                        {
                            proc.Kill(entireProcessTree: true);
                            proc.WaitForExit(3000);
                        }
                        catch { }
                        finally
                        {
                            proc.Dispose();
                        }
                    }
                }
                catch { }
            }

            var desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "OfficeCore Server Station.lnk");
            var startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "OfficeCore Server");
            var startMenuShortcut = Path.Combine(startMenuDir, "OfficeCore Server Station.lnk");

            if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);
            if (File.Exists(startMenuShortcut)) File.Delete(startMenuShortcut);
            if (Directory.Exists(startMenuDir)) Directory.Delete(startMenuDir, true);

            // Remove auto-startup run keys (both current and legacy names)
            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                runKey?.DeleteValue("OfficeCoreServerStation", false);
                runKey?.DeleteValue("OfficeCore Server Station", false);
            }
            catch
            {
            }

            // Remove Uninstall registry entry
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\OfficeCoreServer", false);

            // Delete installed files and schedule directory cleanup if Uninstall.exe is running from installDir
            if (Directory.Exists(installDir))
            {
                var currentExe = Environment.ProcessPath;
                foreach (var file in Directory.GetFiles(installDir, "*", SearchOption.AllDirectories))
                {
                    if (!string.Equals(file, currentExe, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }

                try
                {
                    Directory.Delete(installDir, true);
                }
                catch
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/C timeout /t 2 /nobreak > NUL & rmdir /s /q \"{installDir}\"",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            WindowStyle = ProcessWindowStyle.Hidden
                        });
                    }
                    catch { }
                }
            }

            MessageBox.Show("OfficeCore Server Station was successfully removed from your computer.",
                            "Uninstall Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Uninstall note: {ex.Message}", "Uninstall", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
