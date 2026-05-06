using System.IO;
using System.Text.Json;
using CaniveteSuico.App.Bridge;
using CaniveteSuico.App.Logging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace CaniveteSuico.App.Services;

/// <summary>
/// Handles action "PDF_COMPRESS".
///
/// Expected data:
///   { "inputPath": "C:\\doc.pdf",
///     "outputPath": "C:\\doc_compressed.pdf"  (optional) }
///
/// Applies stream compression and removes redundant metadata.
/// Most effective for text-heavy PDFs.
/// </summary>
public class CompressPdfService : IBridgeHandler
{
    public string Action => "PDF_COMPRESS";

    public async Task HandleAsync(JsonElement data, Action<object> reply)
    {
        string inputPath = data.GetProperty("inputPath").GetString()
            ?? throw new ArgumentException("'inputPath' is required.");

        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Arquivo não encontrado: {inputPath}");

        string outputPath = data.TryGetProperty("outputPath", out var outProp)
            ? outProp.GetString() ?? BuildDefaultOutput(inputPath)
            : BuildDefaultOutput(inputPath);

        long sizeBefore = new FileInfo(inputPath).Length;

        reply(new { type = "PROGRESS", action = Action,
                    message = "Abrindo PDF…", percent = 10 });

        await Task.Run(() =>
        {
            using var doc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify);

            // Apply compression settings available in PdfSharp
            doc.Options.CompressContentStreams = true;
            doc.Options.NoCompression = false;
            doc.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;

            reply(new { type = "PROGRESS", action = Action,
                        message = $"Comprimindo {doc.PageCount} página(s)…", percent = 60 });

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            doc.Save(outputPath);
        });

        long sizeAfter = new FileInfo(outputPath).Length;
        double savedPct = sizeBefore > 0
            ? Math.Round((1 - (double)sizeAfter / sizeBefore) * 100, 1)
            : 0;

        AppLogger.Info($"PDF Compress: {FormatBytes(sizeBefore)} → {FormatBytes(sizeAfter)} ({savedPct:F1}% redução)");

        reply(new
        {
            type = "SUCCESS", action = Action,
            outputPath,
            sizeBefore, sizeAfter,
            savedPercent = savedPct,
            sizeBeforeFmt = FormatBytes(sizeBefore),
            sizeAfterFmt  = FormatBytes(sizeAfter),
        });
    }

    private static string BuildDefaultOutput(string input)
    {
        string dir  = Path.GetDirectoryName(input) ?? ".";
        string name = Path.GetFileNameWithoutExtension(input) + "_comprimido.pdf";
        return Path.Combine(dir, name);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)     return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }
}
