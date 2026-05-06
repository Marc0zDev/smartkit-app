using System.Runtime.InteropServices;
using System.Windows;
using CaniveteSuico.App.Logging;

namespace CaniveteSuico.App;

public partial class App : System.Windows.Application
{
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    protected override void OnStartup(StartupEventArgs e)
    {
        // Open a console window so log output is visible while developing.
        // In a Release build you can remove this call (or wrap in #if DEBUG).
        AllocConsole();

        Console.Title = "CaniveteSuico — Console de Diagnóstico";
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Required for PDFsharp to handle PDFs with Windows-1252 / Latin-1 encoded text
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        AppLogger.Info($"App iniciado. Logs em: {AppLogger.CurrentLogPath}");
        AppLogger.Info($"BaseDirectory: {AppContext.BaseDirectory}");

        // Catch any unhandled exception on the UI thread
        DispatcherUnhandledException += (_, args) =>
        {
            AppLogger.Error(args.Exception, "DispatcherUnhandledException");
            System.Windows.MessageBox.Show(
                $"Erro não tratado:\n\n{args.Exception.Message}\n\nVeja o console para detalhes.",
                "CaniveteSuico", MessageBoxButton.OK, MessageBoxImage.Error);
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
        base.OnExit(e);
    }
}
