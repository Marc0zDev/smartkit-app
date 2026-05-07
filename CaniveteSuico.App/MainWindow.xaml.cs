using System.IO;
using CaniveteSuico.App.Bridge;
using CaniveteSuico.App.Logging;
using Microsoft.Web.WebView2.Core;

namespace CaniveteSuico.App;

public partial class MainWindow : System.Windows.Window
{
    private readonly BridgeDispatcher _dispatcher;

    public MainWindow()
    {
        InitializeComponent();
        Closed += OnClosedCleanup;
        _dispatcher = new BridgeDispatcher(webView);
        InitializeWebView();
    }

    private void OnClosedCleanup(object? sender, EventArgs e)
    {
        try
        {
            webView.Dispose();
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"WebView2 dispose: {ex.Message}");
        }
    }

    private async void InitializeWebView()
    {
        try
        {
            AppLogger.Info("Iniciando WebView2...");

            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CaniveteSuico");

            AppLogger.Debug($"UserDataFolder: {userDataFolder}");

            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder);

            await webView.EnsureCoreWebView2Async(env);
            AppLogger.Info("CoreWebView2 pronto.");

            string wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            AppLogger.Debug($"wwwroot: {wwwroot}");

            if (!Directory.Exists(wwwroot))
            {
                string msg = $"Pasta wwwroot não encontrada:\n{wwwroot}\n\nReconstrua o projeto.";
                AppLogger.Error(msg);
                System.Windows.MessageBox.Show(msg, AppInfo.DisplayName,
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "app.local", wwwroot, CoreWebView2HostResourceAccessKind.Allow);

            webView.CoreWebView2.WebMessageReceived += (s, e) =>
            {
                string json = e.TryGetWebMessageAsString();
                AppLogger.Debug($"Mensagem recebida do front: {json}");
                _ = _dispatcher.DispatchAsync(json);
            };

            // Log WebView2 console messages (console.log, console.error, etc.)
            webView.CoreWebView2.WebResourceResponseReceived += (s, e) =>
            {
                if (e.Response.StatusCode >= 400)
                    AppLogger.Warn($"WebResource {e.Response.StatusCode}: {e.Request.Uri}");
            };

            webView.CoreWebView2.Navigate("https://app.local/index.html");
            AppLogger.Info("Navegando para https://app.local/index.html");

            // Check for updates ~5 s after startup so it doesn't slow the initial load
            _ = Task.Delay(5_000).ContinueWith(_ => _dispatcher.Updater.CheckAsync());
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Falha ao inicializar WebView2");
            System.Windows.MessageBox.Show(
                $"Erro ao inicializar WebView2:\n\n{ex.Message}",
                AppInfo.DisplayName,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }
}
