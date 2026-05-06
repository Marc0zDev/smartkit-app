using System.Text.Json;
using CaniveteSuico.App.Logging;
using CaniveteSuico.App.Services;
using Microsoft.Web.WebView2.Wpf;
using Velopack;

namespace CaniveteSuico.App.Bridge;

public class BridgeDispatcher
{
    private readonly WebView2 _webView;
    private readonly Dictionary<string, IBridgeHandler> _handlers;

    /// <summary>Exposed so MainWindow can kick off the background update check.</summary>
    public readonly AppUpdater Updater;

    public BridgeDispatcher(WebView2 webView)
    {
        _webView = webView;
        Updater  = new AppUpdater(Reply);

        var handlers = new IBridgeHandler[]
        {
            new YouTubeDownloaderService(),
            new ImageConverterService(),
            new PdfConverterService(),
            new DocxToPdfService(),
            new HtmlToPdfService(),
            new PdfInfoService(),
            new MergePdfService(),
            new SplitPdfService(),
            new CompressPdfService(),
            new VideoConverterService(),
            new DownloadSchedulerService(Reply),   // singleton with proactive reply
            new UpdateBridgeHandler(Updater),
            new FileDialogService(),
            new OpenFileService(),
        };

        _handlers = handlers.ToDictionary(h => h.Action, StringComparer.OrdinalIgnoreCase);
        AppLogger.Info($"BridgeDispatcher pronto. Ações registradas: {string.Join(", ", _handlers.Keys)}");
    }

    public async Task DispatchAsync(string json)
    {
        BridgeMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<BridgeMessage>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"JSON inválido recebido: {json}");
            Reply(new { type = "ERROR", message = "JSON inválido." });
            return;
        }

        if (message is null || string.IsNullOrWhiteSpace(message.Action))
        {
            AppLogger.Warn($"Mensagem sem campo 'action': {json}");
            Reply(new { type = "ERROR", message = "Campo 'action' ausente." });
            return;
        }

        if (!_handlers.TryGetValue(message.Action, out var handler))
        {
            AppLogger.Warn($"Ação desconhecida: {message.Action}");
            Reply(new { type = "ERROR", action = message.Action, message = $"Ação desconhecida: {message.Action}" });
            return;
        }

        AppLogger.Info($"→ {message.Action}");
        try
        {
            await handler.HandleAsync(message.Data, payload =>
            {
                AppLogger.Debug($"← {message.Action}: {JsonSerializer.Serialize(payload)}");
                Reply(payload);
            });
            AppLogger.Info($"✓ {message.Action} concluído");
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Erro ao executar {message.Action}");
            Reply(new { type = "ERROR", action = message.Action, message = ex.Message });
        }
    }

    private void Reply(object payload)
    {
        string json = JsonSerializer.Serialize(payload);
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _webView.CoreWebView2?.PostWebMessageAsJson(json);
        });
    }
}
