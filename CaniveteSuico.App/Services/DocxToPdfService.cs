using System.IO;
using System.Text;
using System.Text.Json;
using CaniveteSuico.App.Bridge;
using CaniveteSuico.App.Logging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace CaniveteSuico.App.Services;

/// <summary>
/// Handles action "DOCX_TO_PDF".
/// Converts a Word (.docx) document to PDF using:
///   1. DocumentFormat.OpenXml  — extract paragraphs/headings
///   2. In-memory HTML generation
///   3. PuppeteerSharp (Edge headless) — print HTML → PDF
///
/// Input:  { "inputPath":  "C:\\doc.docx",
///           "outputPath": "C:\\doc.pdf"   (optional) }
///
/// Progress:  { type:"PROGRESS", action:"DOCX_TO_PDF", status, message }
/// Final:     { type:"SUCCESS",  action:"DOCX_TO_PDF", outputPath, pageCount }
/// </summary>
public class DocxToPdfService : IBridgeHandler
{
    public string Action => "DOCX_TO_PDF";

    private static readonly string[] EdgeCandidates =
    [
        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    ];

    public async Task HandleAsync(JsonElement data, Action<object> reply)
    {
        string inputPath = data.GetProperty("inputPath").GetString()
            ?? throw new ArgumentException("'inputPath' é obrigatório.");

        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Arquivo não encontrado: {inputPath}");

        string outputPath = data.TryGetProperty("outputPath", out var outProp)
            ? outProp.GetString() ?? BuildDefaultOutput(inputPath)
            : BuildDefaultOutput(inputPath);

        AppLogger.Info($"Convertendo DOCX → PDF: {Path.GetFileName(inputPath)}");

        // ── Step 1: Extract content from DOCX ────────────────────────────
        reply(new { type = "PROGRESS", action = Action, status = "reading",
                    message = "Lendo documento Word…" });

        string html = await Task.Run(() => DocxToHtml(inputPath));

        // ── Step 2: Write HTML to temp file ──────────────────────────────
        string tempHtml = Path.Combine(Path.GetTempPath(),
            $"caniveteSuico_{Guid.NewGuid():N}.html");

        await File.WriteAllTextAsync(tempHtml, html, Encoding.UTF8);

        try
        {
            // ── Step 3: Launch Edge headless ─────────────────────────────
            reply(new { type = "PROGRESS", action = Action, status = "launching",
                        message = "Iniciando Microsoft Edge em modo headless…" });

            string edgePath = FindEdge();
            var launchOptions = new LaunchOptions
            {
                Headless = true,
                ExecutablePath = edgePath,
                Args = ["--no-sandbox", "--disable-dev-shm-usage"],
            };

            await using var browser = await Puppeteer.LaunchAsync(launchOptions);

            // ── Step 4: Navigate and print ───────────────────────────────
            reply(new { type = "PROGRESS", action = Action, status = "printing",
                        message = "Gerando PDF…" });

            await using var page = await browser.NewPageAsync();
            await page.SetViewportAsync(new ViewPortOptions { Width = 1280, Height = 900 });

            string fileUri = new Uri(tempHtml).AbsoluteUri;
            await page.GoToAsync(fileUri,
                new NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle0], Timeout = 30_000 });

            await page.PdfAsync(outputPath, new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new MarginOptions
                {
                    Top = "20mm", Bottom = "20mm", Left = "20mm", Right = "20mm",
                },
            });
        }
        finally
        {
            if (File.Exists(tempHtml))
                File.Delete(tempHtml);
        }

        AppLogger.Info($"DOCX→PDF concluído: {outputPath}");
        reply(new { type = "SUCCESS", action = Action, outputPath });
    }

    // ── DOCX → HTML ───────────────────────────────────────────────────────

    private static string DocxToHtml(string docxPath)
    {
        using var doc = WordprocessingDocument.Open(docxPath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return WrapHtml("<p>(Documento vazio)</p>", "Documento");

        string docTitle = Path.GetFileNameWithoutExtension(docxPath);
        var sb = new StringBuilder();

        foreach (var element in body.ChildElements)
        {
            if (element is Paragraph para)
            {
                string style = GetParaStyle(para);
                string text  = ExtractText(para);
                if (string.IsNullOrWhiteSpace(text)) continue;

                string tag = style switch
                {
                    "Heading1" or "1" => "h1",
                    "Heading2" or "2" => "h2",
                    "Heading3" or "3" => "h3",
                    _                 => "p",
                };
                sb.AppendLine($"<{tag}>{EscHtml(text)}</{tag}>");
            }
            else if (element is Table)
            {
                sb.AppendLine(TableToHtml(element as Table));
            }
        }

        return WrapHtml(sb.ToString(), docTitle);
    }

    private static string GetParaStyle(Paragraph para)
    {
        return para.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? string.Empty;
    }

    private static string ExtractText(Paragraph para)
    {
        var sb = new StringBuilder();
        foreach (var run in para.Descendants<Run>())
        {
            foreach (var child in run.ChildElements)
            {
                if (child is Text t)             sb.Append(t.Text);
                else if (child is Break)         sb.Append('\n');
                else if (child is TabChar)       sb.Append("    ");
            }
        }
        return sb.ToString();
    }

    private static string TableToHtml(Table? table)
    {
        if (table is null) return string.Empty;
        var sb = new StringBuilder("<table>");
        foreach (var row in table.Descendants<TableRow>())
        {
            sb.Append("<tr>");
            foreach (var cell in row.Descendants<TableCell>())
            {
                string cellText = string.Join(" ", cell.Descendants<Paragraph>()
                    .Select(p => ExtractText(p)));
                sb.Append($"<td>{EscHtml(cellText)}</td>");
            }
            sb.Append("</tr>");
        }
        sb.Append("</table>");
        return sb.ToString();
    }

    private static string WrapHtml(string body, string title)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"pt-BR\">");
        sb.AppendLine("<head><meta charset=\"UTF-8\"/>");
        sb.AppendLine($"<title>{EscHtml(title)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("  @page { size: A4; margin: 20mm; }");
        sb.AppendLine("  body { font-family: 'Calibri', 'Segoe UI', sans-serif; font-size: 11pt; color: #111; line-height: 1.6; }");
        sb.AppendLine("  h1 { font-size: 18pt; color: #2E74B5; margin: 1em 0 .4em; }");
        sb.AppendLine("  h2 { font-size: 14pt; color: #2E74B5; margin: 1em 0 .3em; }");
        sb.AppendLine("  h3 { font-size: 12pt; color: #333;    margin: .8em 0 .2em; }");
        sb.AppendLine("  p  { margin: 0 0 .6em; }");
        sb.AppendLine("  table { border-collapse: collapse; width: 100%; margin: .8em 0; }");
        sb.AppendLine("  td, th { border: 1px solid #ccc; padding: 4px 8px; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine(body);
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string EscHtml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string FindEdge()
    {
        foreach (string path in EdgeCandidates)
            if (File.Exists(path)) return path;

        throw new InvalidOperationException(
            "Microsoft Edge não encontrado no sistema.");
    }

    private static string BuildDefaultOutput(string inputPath)
    {
        string dir  = Path.GetDirectoryName(inputPath) ?? ".";
        string name = Path.GetFileNameWithoutExtension(inputPath) + ".pdf";
        return Path.Combine(dir, name);
    }
}
