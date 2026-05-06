using Velopack;

namespace CaniveteSuico.App;

/// <summary>
/// Custom WPF entry point required by Velopack.
/// VelopackApp.Build().Run() MUST execute before anything else —
/// it handles the installer lifecycle (first-run, update, uninstall hooks).
/// </summary>
public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
