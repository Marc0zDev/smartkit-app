using Velopack;
using System.Runtime.InteropServices;

namespace CaniveteSuico.App;

/// <summary>
/// Custom WPF entry point required by Velopack.
/// VelopackApp.Build().Run() MUST execute before anything else —
/// it handles the installer lifecycle (first-run, update, uninstall hooks).
/// </summary>
public class Program
{
    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        // Helps Windows show correct taskbar name/icon + grouping identity.
        // Safe to ignore failure (older Windows / restricted env).
        try { SetCurrentProcessExplicitAppUserModelID(AppInfo.AppUserModelId); } catch { /* ignore */ }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
