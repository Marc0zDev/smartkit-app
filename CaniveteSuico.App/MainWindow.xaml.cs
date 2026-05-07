using System.IO;
using System.Diagnostics;
using CaniveteSuico.App.Bridge;
using CaniveteSuico.App.Logging;
using Microsoft.Web.WebView2.Core;

namespace CaniveteSuico.App;

public partial class MainWindow : System.Windows.Window
{
    private readonly BridgeDispatcher _dispatcher;
    private CancellationTokenSource? _startupCts;
    private bool _initialized;

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
            _startupCts?.Cancel();
            _startupCts?.Dispose();
            webView.Dispose();
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"WebView2 dispose: {ex.Message}");
        }
    }

    private async void InitializeWebView()
    {
        if (_initialized) return;

        _startupCts?.Cancel();
        _startupCts?.Dispose();
        _startupCts = new CancellationTokenSource();

        SetStartupState("Preparando navegador…", allowRetry: false);

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

            webView.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                if (args.IsSuccess)
                {
                    _initialized = true;
                    Dispatcher.Invoke(() =>
                    {
                        StartupOverlay.Visibility = System.Windows.Visibility.Collapsed;
                        webView.Visibility = System.Windows.Visibility.Visible;
                    });
                }
                else
                {
                    string msg = $"Falha ao carregar a interface ({args.WebErrorStatus}).";
                    AppLogger.Warn(msg);
                    Dispatcher.Invoke(() => SetStartupError(msg));
                }
            };

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

            // Timeout: if WebView doesn't load, show a friendly retry UI.
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(20), _startupCts.Token);
                    if (_startupCts.Token.IsCancellationRequested) return;
                    if (_initialized) return;

                    Dispatcher.Invoke(() =>
                        SetStartupError("Ainda estamos carregando… Se continuar assim, tente novamente ou confira os logs."));
                }
                catch (TaskCanceledException) { /* ignore */ }
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Falha ao inicializar WebView2");
            SetStartupError($"Erro ao inicializar o WebView2: {ex.Message}");
        }
    }

    private void SetStartupState(string message, bool allowRetry)
    {
        StartupStatusText.Text = message;
        StartupProgress.IsIndeterminate = true;
        StartupActions.Visibility = allowRetry ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    private void SetStartupError(string message)
    {
        StartupStatusText.Text = message;
        StartupProgress.IsIndeterminate = false;
        StartupProgress.Value = 100;
        StartupActions.Visibility = System.Windows.Visibility.Visible;
    }

    private void RetryButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            _initialized = false;
            webView.Visibility = System.Windows.Visibility.Hidden;
            StartupOverlay.Visibility = System.Windows.Visibility.Visible;
            SetStartupState("Tentando novamente…", allowRetry: false);
            InitializeWebView();
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "RetryButton_Click");
            SetStartupError($"Falha ao tentar novamente: {ex.Message}");
        }
    }

    private void OpenLogsButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            string path = AppLogger.CurrentLogPath;
            var psi = new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
            {
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"OpenLogsButton_Click: {ex.Message}");
        }
    }
}
