using System.Runtime.InteropServices;
using System.Windows;
using CaniveteSuico.App.Logging;

namespace CaniveteSuico.App;

public partial class App : System.Windows.Application
{
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    protected override void OnStartup(StartupEventArgs e)
    {
#if DEBUG
        // Diagnostic console only in Debug builds (never in Release / installed app).
        AllocConsole();
        Console.Title = $"{AppInfo.DisplayName} — Console de Diagnóstico";
        Console.OutputEncoding = System.Text.Encoding.UTF8;
#endif

        // Required for PDFsharp to handle PDFs with Windows-1252 / Latin-1 encoded text
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        AppLogger.Info($"App iniciado. Logs em: {AppLogger.CurrentLogPath}");
        AppLogger.Info($"BaseDirectory: {AppContext.BaseDirectory}");

        // Catch any unhandled exception on the UI thread
        DispatcherUnhandledException += (_, args) =>
        {
            AppLogger.Error(args.Exception, "DispatcherUnhandledException");
#if DEBUG
            string hint = "Veja o console para detalhes.";
#else
            string hint = $"Detalhes no log:\n{AppLogger.CurrentLogPath}";
#endif
            System.Windows.MessageBox.Show(
                $"Erro não tratado:\n\n{args.Exception.Message}\n\n{hint}",
                AppInfo.DisplayName, MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // Catch unhandled exceptions on background threads
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                AppLogger.Error(ex, "AppDomain.UnhandledException");
        };

        // Catch exceptions from async Task continuations (fire-and-forget tasks)
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogger.Error(args.Exception, "UnobservedTaskException");
            args.SetObserved();
        };

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLogger.Info("App encerrado.");
#if DEBUG
        try { FreeConsole(); } catch { /* ignore */ }
#endif
        base.OnExit(e);
    }
}
