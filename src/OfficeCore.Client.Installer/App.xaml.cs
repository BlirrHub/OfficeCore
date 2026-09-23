using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace OfficeCore.Client.Installer;

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
            "Are you sure you want to remove OfficeCore Workstation from this computer?",
            "Uninstall OfficeCore Workstation", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var installDir = Path.Combine(localAppData, "Programs", "OfficeCore Client");

            var desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "OfficeCore Workstation.lnk");
            var startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "OfficeCore");
            var startMenuShortcut = Path.Combine(startMenuDir, "OfficeCore Workstation.lnk");

            if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);
            if (File.Exists(startMenuShortcut)) File.Delete(startMenuShortcut);
            if (Directory.Exists(startMenuDir)) Directory.Delete(startMenuDir, true);

            // Remove registry
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\OfficeCoreClient", false);

            MessageBox.Show("OfficeCore Workstation was successfully removed from your computer.",
                            "Uninstall Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Uninstall note: {ex.Message}", "Uninstall", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
