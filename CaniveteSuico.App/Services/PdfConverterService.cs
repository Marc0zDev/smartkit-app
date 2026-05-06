using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CaniveteSuico.App.Bridge;
using CaniveteSuico.App.Logging;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace CaniveteSuico.App.Services;

/// <summary>
/// Handles action "PDF_CONVERT".
/// Converts PDF to DOCX, TXT, HTML or Markdown — no external tools required.
///
/// Input:  { "inputPath": "C:\\doc.pdf",
///           "outputFormat": "docx"|"txt"|"html"|"md"  (default: "docx"),
///           "outputPath": "C:\\doc.docx"  (optional) }
///
/// Reply:  { type:"SUCCESS", action:"PDF_CONVERT", outputPath, pageCount }
/// </summary>
public class PdfConverterService : IBridgeHandler
{
    public string Action => "PDF_CONVERT";

    private static readonly HashSet<string> SupportedFormats =
        new(StringComparer.OrdinalIgnoreCase) { "docx", "txt", "html", "md" };

    private const double HeadingWidthThreshold = 0.55;

    public async Task HandleAsync(JsonElement data, Action<object> reply)
    {
        string inputPath = data.GetProperty("inputPath").GetString()
            ?? throw new ArgumentException("'inputPath' é obrigatório.");

        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Arquivo não encontrado: {inputPath}");

        string format = data.TryGetProperty("outputFormat", out var fmtProp)
            ? fmtProp.GetString() ?? "docx" : "docx";

        format = format.ToLowerInvariant().TrimStart('.');

        if (!SupportedFormats.Contains(format))
            throw new ArgumentException($"Formato não suportado: '{format}'. Use: docx, txt, html, md.");

        string outputPath = data.TryGetProperty("outputPath", out var outProp)
            ? outProp.GetString() ?? BuildDefaultOutput(inputPath, format)
            : BuildDefaultOutput(inputPath, format);

        AppLogger.Info($"Convertendo PDF → {format.ToUpper()}: {Path.GetFileName(inputPath)}");

        int pageCount = await Task.Run(() => Convert(inputPath, outputPath, format));

        AppLogger.Info($"Concluído: {outputPath} ({pageCount} páginas)");
        reply(new { type = "SUCCESS", action = Action, outputPath, pageCount });
    }

    // ── Dispatcher ────────────────────────────────────────────────────────

    private static int Convert(string inputPath, string outputPath, string format)
    {
        using var pdf = PdfDocument.Open(inputPath);
        int pageCount = pdf.NumberOfPages;
        var pages = pdf.GetPages()
                       .Select(p => ExtractParagraphs(p))
                       .ToList();

        switch (format)
        {
            case "docx": WriteDocx(pages, outputPath, pageCount); break;
            case "txt":  WriteTxt (pages, outputPath, pageCount); break;
            case "html": WriteHtml(pages, outputPath, pageCount, Path.GetFileNameWithoutExtension(inputPath)); break;
            case "md":   WriteMd  (pages, outputPath, pageCount); break;
        }

        return pageCount;
    }

    // ── Text extraction ───────────────────────────────────────────────────

    private static List<(string Text, bool IsHeading)> ExtractParagraphs(Page page)
    {
        double pageWidth = page.Width;
        var result = new List<(string, bool)>();

        var words = page.GetWords()
            .OrderByDescending(w => w.BoundingBox.Top)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        if (words.Count == 0) return result;

        // Cluster words → lines
        var lines = new List<List<Word>>();
        List<Word>? currentLine = null;
        double lastY = double.MaxValue;

        foreach (var word in words)
        {
            double y = word.BoundingBox.Top;
            if (currentLine == null || Math.Abs(y - lastY) > 2.5)
            {
                currentLine = new List<Word>();
                lines.Add(currentLine);
                lastY = y;
            }
            currentLine.Add(word);
        }

        double avgFont = words
            .Select(w => w.Letters.FirstOrDefault()?.PointSize ?? 0)
            .Where(s => s > 0).DefaultIfEmpty(12).Average();

        // Cluster lines → paragraphs
        var paragraphGroups = new List<List<List<Word>>>();
        List<List<Word>>? currentPara = null;
        double lastLineY = double.MaxValue;

        foreach (var line in lines)
        {
            double lineY = line.Max(w => w.BoundingBox.Top);
            double lineH = line.Max(w => w.BoundingBox.Height);
            if (currentPara == null || lastLineY - lineY > lineH * 1.5)
            {
                currentPara = new List<List<Word>>();
                paragraphGroups.Add(currentPara);
            }
            currentPara.Add(line);
            lastLineY = lineY;
        }

        foreach (var paraGroup in paragraphGroups)
        {
            var sb = new StringBuilder();
            double paraWidth = 0;

            foreach (var line in paraGroup)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(string.Join(" ", line.Select(w => w.Text)));
                double w = line.Max(w2 => w2.BoundingBox.Right) - line.Min(w2 => w2.BoundingBox.Left);
                if (w > paraWidth) paraWidth = w;
            }

            string text = CollapseSpaces(sb.ToString());
            if (string.IsNullOrWhiteSpace(text)) continue;

            double firstFont = paraGroup.SelectMany(l => l)
                .SelectMany(w => w.Letters).FirstOrDefault()?.PointSize ?? 0;

            bool isHeading = firstFont > avgFont * 1.15
                          && paraWidth < pageWidth * HeadingWidthThreshold;

            result.Add((text, isHeading));
        }

        return result;
    }

    private static string CollapseSpaces(string s) =>
        Regex.Replace(s.Trim(), @"\s{2,}", " ");

    // ── DOCX writer ───────────────────────────────────────────────────────

    private static void WriteDocx(List<List<(string Text, bool IsHeading)>> pages, string path, int pageCount)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new Document();
        var body = main.Document.AppendChild(new Body());

        AddDocxStyles(main);

        for (int i = 0; i < pages.Count; i++)
        {
            foreach (var (text, isHeading) in pages[i])
            {
                var para = new Paragraph();
                var props = new ParagraphProperties();

                if (isHeading)
                    props.AppendChild(new ParagraphStyleId { Val = "Heading1" });
                else
                    props.AppendChild(new SpacingBetweenLines { After = "120", Line = "276", LineRule = LineSpacingRuleValues.Auto });

                para.AppendChild(props);
                var run = new Run();
                if (isHeading) run.AppendChild(new RunProperties(new Bold()));
                run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
                para.AppendChild(run);
                body.AppendChild(para);
            }

            if (i < pages.Count - 1)
            {
                var pbPara = new Paragraph();
                pbPara.AppendChild(new Run(new Break { Type = BreakValues.Page }));
                body.AppendChild(pbPara);
            }
        }

        body.AppendChild(new SectionProperties());
        main.Document.Save();
    }

    private static void AddDocxStyles(MainDocumentPart main)
    {
        var sp = main.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        var normal = new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true };
        normal.AppendChild(new StyleName { Val = "Normal" });
        normal.AppendChild(new StyleRunProperties(new FontSize { Val = "24" }));
        styles.AppendChild(normal);

        var h1 = new Style { Type = StyleValues.Paragraph, StyleId = "Heading1" };
        h1.AppendChild(new StyleName { Val = "heading 1" });
        h1.AppendChild(new BasedOn { Val = "Normal" });
        h1.AppendChild(new StyleParagraphProperties(new SpacingBetweenLines { Before = "240", After = "120" }));
        h1.AppendChild(new StyleRunProperties(
            new Bold(), new FontSize { Val = "32" },
            new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "2E74B5" }));
        styles.AppendChild(h1);

        sp.Styles = styles;
        styles.Save();
    }

    // ── TXT writer ────────────────────────────────────────────────────────

    private static void WriteTxt(List<List<(string Text, bool IsHeading)>> pages, string path, int pageCount)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < pages.Count; i++)
        {
            if (i > 0) sb.AppendLine().AppendLine($"{'─', -60}  Página {i + 1}").AppendLine();

            foreach (var (text, isHeading) in pages[i])
            {
                if (isHeading)
                {
                    sb.AppendLine();
                    sb.AppendLine(text.ToUpperInvariant());
                    sb.AppendLine(new string('─', Math.Min(text.Length, 60)));
                }
                else
                {
                    sb.AppendLine(text);
                }
                sb.AppendLine();
            }
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    // ── HTML writer ───────────────────────────────────────────────────────

    private static void WriteHtml(List<List<(string Text, bool IsHeading)>> pages, string path, int pageCount, string title)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"pt-BR\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\"/>");
        sb.AppendLine($"<title>{EscHtml(title)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("  body { font-family: 'Segoe UI', sans-serif; max-width: 860px; margin: 40px auto; color: #222; line-height: 1.7; }");
        sb.AppendLine("  h1   { color: #2E74B5; border-bottom: 2px solid #2E74B5; padding-bottom: 4px; }");
        sb.AppendLine("  h2   { color: #2E74B5; }");
        sb.AppendLine("  p    { margin: 0 0 .8em; }");
        sb.AppendLine("  hr   { border: none; border-top: 1px solid #ccc; margin: 2em 0; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine($"<h1>{EscHtml(title)}</h1>");

        for (int i = 0; i < pages.Count; i++)
        {
            if (i > 0) sb.AppendLine($"<hr/><p><small>Página {i + 1}</small></p>");
            foreach (var (text, isHeading) in pages[i])
            {
                sb.AppendLine(isHeading
                    ? $"<h2>{EscHtml(text)}</h2>"
                    : $"<p>{EscHtml(text)}</p>");
            }
        }

        sb.AppendLine("</body></html>");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    // ── Markdown writer ───────────────────────────────────────────────────

    private static void WriteMd(List<List<(string Text, bool IsHeading)>> pages, string path, int pageCount)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < pages.Count; i++)
        {
            if (i > 0) sb.AppendLine().AppendLine($"---").AppendLine($"> Página {i + 1}").AppendLine();

            foreach (var (text, isHeading) in pages[i])
            {
                sb.AppendLine(isHeading ? $"## {text}" : text);
                sb.AppendLine();
            }
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string EscHtml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string BuildDefaultOutput(string inputPath, string format)
    {
        string dir  = Path.GetDirectoryName(inputPath) ?? ".";
        string name = Path.GetFileNameWithoutExtension(inputPath) + "." + format;
        return Path.Combine(dir, name);
    }
}
