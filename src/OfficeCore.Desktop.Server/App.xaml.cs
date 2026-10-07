using System.Linq;
using System.Windows;

namespace OfficeCore.Desktop.Server;

public partial class App : System.Windows.Application
{
    public static bool StartInBackground { get; private set; } = false;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Any(a => a.Equals("--background", System.StringComparison.OrdinalIgnoreCase) ||
                            a.Equals("--minimized", System.StringComparison.OrdinalIgnoreCase) ||
                            a.Equals("-b", System.StringComparison.OrdinalIgnoreCase)))
        {
            StartInBackground = true;
        }
    }
}
