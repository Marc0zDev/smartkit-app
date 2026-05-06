using System.Diagnostics;
using System.IO;
using System.Text.Json;
using CaniveteSuico.App.Bridge;
using CaniveteSuico.App.Logging;

namespace CaniveteSuico.App.Services;

/// <summary>
/// Handles action "OPEN_FILE".
/// Opens a local file with the default Windows application (e.g. Windows Media Player for videos).
/// Also handles "OPEN_FOLDER" to reveal a file in Explorer.
///
/// Input: { "path": "C:\\..." }
/// </summary>
public class OpenFileService : IBridgeHandler
{
    public string Action => "OPEN_FILE";

    public Task HandleAsync(JsonElement data, Action<object> reply)
    {
        string path = data.GetProperty("path").GetString()
            ?? throw new ArgumentException("'path' is required.");

        if (!File.Exists(path) && !Directory.Exists(path))
            throw new FileNotFoundException($"Arquivo não encontrado: {path}");

        bool revealInExplorer = data.TryGetProperty("reveal", out var rv) && rv.GetBoolean();

        if (revealInExplorer)
        {
            // Open Explorer and select the file
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true,
            });
            AppLogger.Info($"Revelando no Explorer: {path}");
        }
        else
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
            AppLogger.Info($"Abrindo arquivo: {path}");
        }

        reply(new { type = "SUCCESS", action = Action, path });
        return Task.CompletedTask;
    }
}
