using System.IO;
using System.Text.Json;
using CaniveteSuico.App.Bridge;
using CaniveteSuico.App.Logging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace CaniveteSuico.App.Services;

/// <summary>
/// Handles action "PDF_MERGE".
///
/// Expected data:
///   { "files": ["C:\\a.pdf", "C:\\b.pdf", ...],
///     "outputPath": "C:\\merged.pdf"  (optional) }
///
/// Merges all input PDFs into a single output PDF, preserving page order.
/// </summary>
public class MergePdfService : IBridgeHandler
{
    public string Action => "PDF_MERGE";

    public async Task HandleAsync(JsonElement data, Action<object> reply)
    {
        var filesArray = data.GetProperty("files");
        var files = new List<string>();

        foreach (var el in filesArray.EnumerateArray())
        {
            string? path = el.GetString();
            if (!string.IsNullOrWhiteSpace(path))
                files.Add(path);
        }

        if (files.Count < 2)
            throw new ArgumentException("Informe pelo menos dois arquivos PDF para mesclar.");

        foreach (var f in files)
            if (!File.Exists(f))
                throw new FileNotFoundException($"Arquivo não encontrado: {f}");

        string outputPath = data.TryGetProperty("outputPath", out var outProp)
            ? outProp.GetString() ?? BuildDefaultOutput(files[0])
            : BuildDefaultOutput(files[0]);

        reply(new { type = "PROGRESS", action = Action,
                    message = $"Mesclando {files.Count} arquivos…", percent = 0 });

        await Task.Run(() =>
        {
            using var output = new PdfDocument();

            for (int fi = 0; fi < files.Count; fi++)
            {
                AppLogger.Debug($"PDF Merge: adicionando {files[fi]}");
                using var input = PdfReader.Open(files[fi], PdfDocumentOpenMode.Import);

                for (int pi = 0; pi < input.PageCount; pi++)
                    output.AddPage(input.Pages[pi]);

                int pct = (int)((fi + 1) / (double)files.Count * 90);
                reply(new { type = "PROGRESS", action = Action,
                            message = $"Processado: {Path.GetFileName(files[fi])} ({fi + 1}/{files.Count})",
                            percent = pct });
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            output.Save(outputPath);
        });

        int totalPages = 0;
        using (var check = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import))
            totalPages = check.PageCount;

        AppLogger.Info($"PDF Merge concluído: {outputPath} ({totalPages} páginas)");

        reply(new { type = "SUCCESS", action = Action,
                    outputPath, totalPages, fileCount = files.Count });
    }

    private static string BuildDefaultOutput(string firstFile)
    {
        string dir = Path.GetDirectoryName(firstFile)
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(dir, $"mesclado_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
    }
}
