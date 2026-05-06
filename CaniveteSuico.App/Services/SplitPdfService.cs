using System.IO;
using System.Text.Json;
using CaniveteSuico.App.Bridge;
using CaniveteSuico.App.Logging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace CaniveteSuico.App.Services;

/// <summary>
/// Handles action "PDF_SPLIT".
///
/// mode = "selection"  data includes pages:[1,3,5] (1-indexed)
///                     → generates ONE output PDF with only those pages
///
/// mode = "parts"      data includes parts:N
///                     → splits into N roughly equal PDFs
///
/// mode = "all"        → one PDF per page
///
/// Optional: outputDir (defaults to same folder as input)
/// </summary>
public class SplitPdfService : IBridgeHandler
{
    public string Action => "PDF_SPLIT";

    public async Task HandleAsync(JsonElement data, Action<object> reply)
    {
        string inputPath = data.GetProperty("inputPath").GetString()
            ?? throw new ArgumentException("'inputPath' is required.");

        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Arquivo não encontrado: {inputPath}");

        string mode = data.TryGetProperty("mode", out var modeProp)
            ? modeProp.GetString() ?? "all"
            : "all";

        string outputDir = data.TryGetProperty("outputDir", out var dirProp)
            ? dirProp.GetString() ?? Path.GetDirectoryName(inputPath)!
            : Path.GetDirectoryName(inputPath)!;

        Directory.CreateDirectory(outputDir);

        string baseName = Path.GetFileNameWithoutExtension(inputPath);
        var outputFiles = new List<string>();

        reply(new { type = "PROGRESS", action = Action, message = "Lendo PDF…", percent = 5 });

        await Task.Run(() =>
        {
            using var input = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
            int total = input.PageCount;

            AppLogger.Info($"PDF Split: {total} págs, mode={mode}");

            switch (mode)
            {
                case "selection":
                    outputFiles.Add(ExtractSelection(data, input, total, outputDir, baseName, reply));
                    break;

                case "parts":
                    ExtractParts(data, input, total, outputDir, baseName, reply, outputFiles);
                    break;

                default: // "all"
                    ExtractAllPages(input, total, outputDir, baseName, reply, outputFiles);
                    break;
            }
        });

        AppLogger.Info($"PDF Split concluído: {outputFiles.Count} arquivo(s) em {outputDir}");

        reply(new { type = "SUCCESS", action = Action,
                    outputDir, fileCount = outputFiles.Count,
                    files = outputFiles.ToArray() });
    }

    // ── mode: selection ─────────────────────────────────────────────────
    private static string ExtractSelection(JsonElement data, PdfDocument input, int total,
        string outputDir, string baseName, Action<object> reply)
    {
        var selectedPages = new List<int>();
        if (data.TryGetProperty("pages", out var pagesArr))
            foreach (var el in pagesArr.EnumerateArray())
                selectedPages.Add(el.GetInt32());

        if (selectedPages.Count == 0)
            throw new ArgumentException("Nenhuma página selecionada.");

        reply(new { type = "PROGRESS", action = "PDF_SPLIT",
                    message = $"Extraindo {selectedPages.Count} página(s)…", percent = 40 });

        using var output = new PdfDocument();
        foreach (int pg in selectedPages.OrderBy(p => p))
            if (pg >= 1 && pg <= total)
                output.AddPage(input.Pages[pg - 1]);

        string outPath = Path.Combine(outputDir, $"{baseName}_extraido.pdf");
        output.Save(outPath);
        return outPath;
    }

    // ── mode: parts ──────────────────────────────────────────────────────
    private static void ExtractParts(JsonElement data, PdfDocument input, int total,
        string outputDir, string baseName, Action<object> reply, List<string> outputFiles)
    {
        int parts = data.TryGetProperty("parts", out var partsProp)
            ? partsProp.GetInt32()
            : 2;

        parts = Math.Max(2, Math.Min(parts, total));
        int pagesPerPart = (int)Math.Ceiling((double)total / parts);

        for (int p = 0; p < parts; p++)
        {
            int start = p * pagesPerPart;
            int end   = Math.Min(start + pagesPerPart, total);
            if (start >= total) break;

            using var output = new PdfDocument();
            for (int i = start; i < end; i++)
                output.AddPage(input.Pages[i]);

            string outPath = Path.Combine(outputDir, $"{baseName}_parte{p + 1}de{parts}.pdf");
            output.Save(outPath);
            outputFiles.Add(outPath);

            int pct = (int)((p + 1) / (double)parts * 90 + 5);
            reply(new { type = "PROGRESS", action = "PDF_SPLIT",
                        message = $"Parte {p + 1}/{parts} salva ({end - start} pág.)", percent = pct });
        }
    }

    // ── mode: all ────────────────────────────────────────────────────────
    private static void ExtractAllPages(PdfDocument input, int total,
        string outputDir, string baseName, Action<object> reply, List<string> outputFiles)
    {
        for (int i = 0; i < total; i++)
        {
            using var output = new PdfDocument();
            output.AddPage(input.Pages[i]);

            string outPath = Path.Combine(outputDir, $"{baseName}_p{i + 1:D4}.pdf");
            output.Save(outPath);
            outputFiles.Add(outPath);

            if (i % 5 == 0 || i == total - 1)
            {
                int pct = (int)((i + 1) / (double)total * 90 + 5);
                reply(new { type = "PROGRESS", action = "PDF_SPLIT",
                            message = $"Página {i + 1}/{total}…", percent = pct });
            }
        }
    }
}
