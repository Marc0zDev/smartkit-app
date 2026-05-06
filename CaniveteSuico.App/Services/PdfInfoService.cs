using System.IO;
using System.Text.Json;
using CaniveteSuico.App.Bridge;
using PdfSharp.Pdf.IO;

namespace CaniveteSuico.App.Services;

/// <summary>
/// Handles action "PDF_INFO".
/// Returns basic metadata about a PDF file (page count, file size).
/// Called by the split tab as soon as a file is selected.
///
/// Expected data: { "path": "C:\\file.pdf" }
/// Reply: { type:"PDF_INFO", pageCount:N, fileSizeFmt:"2.1 MB", fileName:"file.pdf" }
/// </summary>
public class PdfInfoService : IBridgeHandler
{
    public string Action => "PDF_INFO";

    public Task HandleAsync(JsonElement data, Action<object> reply)
    {
        string path = data.GetProperty("path").GetString()
            ?? throw new ArgumentException("'path' is required.");

        if (!File.Exists(path))
            throw new FileNotFoundException($"Arquivo não encontrado: {path}");

        using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);

        long bytes = new FileInfo(path).Length;
        string sizeFmt = bytes >= 1_048_576
            ? $"{bytes / 1_048_576.0:F1} MB"
            : $"{bytes / 1_024.0:F0} KB";

        reply(new
        {
            type        = "PDF_INFO",
            action      = Action,
            pageCount   = doc.PageCount,
            fileSizeFmt = sizeFmt,
            fileName    = Path.GetFileName(path),
        });

        return Task.CompletedTask;
    }
}
