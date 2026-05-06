using System.IO;
using System.Text.Json;
using CaniveteSuico.App.Bridge;
using Microsoft.Win32;

namespace CaniveteSuico.App.Services;

/// <summary>
/// Handles action "OPEN_DIALOG".
/// Opens a native Windows file/folder/save dialog and returns the selected path.
///
/// Expected data:
///   { "dialogType": "file"|"folder"|"save", "requestId": "input-id", "title": "...", "filter": "Imagens|*.jpg;*.png" }
///
/// Reply:
///   { type:"DIALOG_RESULT", requestId:"input-id", path:"C:\\..." }
///   path is null/absent if the user cancelled.
/// </summary>
public class FileDialogService : IBridgeHandler
{
    public string Action => "OPEN_DIALOG";

    public async Task HandleAsync(JsonElement data, Action<object> reply)
    {
        string dialogType = data.TryGetProperty("dialogType", out var dt) ? dt.GetString() ?? "file" : "file";
        string requestId  = data.TryGetProperty("requestId",  out var ri) ? ri.GetString() ?? ""     : "";
        string title      = data.TryGetProperty("title",      out var t)  ? t.GetString()  ?? "Selecionar" : "Selecionar";
        string filter     = data.TryGetProperty("filter",     out var f)  ? f.GetString()  ?? "Todos os arquivos|*.*" : "Todos os arquivos|*.*";
        string initialDir = data.TryGetProperty("initialDir", out var id) ? id.GetString() ?? "" : "";

        string? selectedPath = null;

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            selectedPath = dialogType switch
            {
                "folder" => OpenFolderDialog(title, initialDir),
                "save"   => OpenSaveDialog(title, filter, initialDir),
                _        => OpenFileDialog(title, filter, initialDir),
            };
        });

        reply(new { type = "DIALOG_RESULT", action = Action, requestId, path = selectedPath });
    }

    private static string? OpenFileDialog(string title, string filter, string initialDir)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
        };
        if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
            dlg.InitialDirectory = initialDir;

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private static string? OpenSaveDialog(string title, string filter, string initialDir)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = title,
            Filter = filter,
            OverwritePrompt = true,
        };
        if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
            dlg.InitialDirectory = initialDir;

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private static string? OpenFolderDialog(string title, string initialDir)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = title,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
        };
        if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
            dlg.InitialDirectory = initialDir;

        return dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? dlg.SelectedPath
            : null;
    }
}
