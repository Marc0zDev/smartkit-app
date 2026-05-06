using System.IO;
using System.Text.Json;
using CaniveteSuico.App.Bridge;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace CaniveteSuico.App.Services;

/// <summary>
/// Handles action "HTML_TO_PDF".
///
/// Supported input types (field "inputType"):
///   "file"  — local .html path or http(s) URL  (default, backward-compatible)
///   "code"  — raw HTML string passed in "htmlCode" field
///
/// For URLs:  waits for Networkidle0 (external resources may load).
/// For files/code: waits for Load only (no network traffic expected).
///
/// Progress events: launching · navigating · printing
/// </summary>
public class HtmlToPdfService : IBridgeHandler
{
    public string Action => "HTML_TO_PDF";

    private static readonly string[] EdgeCandidates =
    [
        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    ];

    public async Task HandleAsync(JsonElement data, Action<object> reply)
    {
        string inputType = data.TryGetProperty("inputType", out var typeProp)
            ? typeProp.GetString() ?? "file"
            : "file";

        bool isCodeMode = string.Equals(inputType, "code", StringComparison.OrdinalIgnoreCase);

        // ── Resolve source ────────────────────────────────────────────────
        string htmlCode   = "";
        string inputPath  = "";
        bool   isUrl      = false;

        if (isCodeMode)
        {
            htmlCode = data.TryGetProperty("htmlCode", out var codeProp)
                ? codeProp.GetString() ?? ""
                : "";

            if (string.IsNullOrWhiteSpace(htmlCode))
                throw new ArgumentException("'htmlCode' is required when inputType is 'code'.");
        }
        else
        {
            inputPath = data.TryGetProperty("input", out var inProp)
                ? inProp.GetString() ?? ""
                : data.TryGetProperty("input", out var inProp2) ? inProp2.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(inputPath))
                throw new ArgumentException("'input' (URL or file path) is required.");

            isUrl = inputPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                 || inputPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            if (!isUrl && !File.Exists(inputPath))
                throw new FileNotFoundException($"HTML file not found: {inputPath}");
        }

        // ── Resolve output path ───────────────────────────────────────────
        string outputPath = data.TryGetProperty("outputPath", out var outProp)
            ? outProp.GetString() ?? BuildDefaultOutput(inputPath, isUrl, isCodeMode)
            : BuildDefaultOutput(inputPath, isUrl, isCodeMode);

        // ── Step 1: Launch Edge headless ──────────────────────────────────
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
        await using var page    = await browser.NewPageAsync();
        await page.SetViewportAsync(new ViewPortOptions { Width = 1280, Height = 900 });

        // ── Step 2: Load content ──────────────────────────────────────────
        if (isCodeMode)
        {
            reply(new { type = "PROGRESS", action = Action, status = "navigating",
                        message = "Renderizando código HTML…" });

            await page.SetContentAsync(htmlCode,
                new NavigationOptions { WaitUntil = [WaitUntilNavigation.Load], Timeout = 15_000 });
        }
        else if (isUrl)
        {
            reply(new { type = "PROGRESS", action = Action, status = "navigating",
                        message = $"Carregando URL: {inputPath}" });

            await page.GoToAsync(inputPath,
                new NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle0], Timeout = 30_000 });
        }
        else
        {
            reply(new { type = "PROGRESS", action = Action, status = "navigating",
                        message = $"Carregando arquivo: {Path.GetFileName(inputPath)}" });

            await page.GoToAsync(new Uri(inputPath).AbsoluteUri,
                new NavigationOptions { WaitUntil = [WaitUntilNavigation.Load], Timeout = 15_000 });
        }

        // ── Step 3: Print to PDF ──────────────────────────────────────────
        reply(new { type = "PROGRESS", action = Action, status = "printing",
                    message = "Gerando PDF…" });

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await page.PdfAsync(outputPath, new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions
            {
                Top = "15mm", Bottom = "15mm", Left = "15mm", Right = "15mm",
            },
        });

        reply(new { type = "SUCCESS", action = Action, outputPath });
    }

    private static string FindEdge()
    {
        foreach (string path in EdgeCandidates)
            if (File.Exists(path)) return path;

        throw new InvalidOperationException(
            "Microsoft Edge não encontrado. " +
            "Certifique-se de que o Edge está instalado no sistema.");
    }

    private static string BuildDefaultOutput(string inputPath, bool isUrl, bool isCodeMode)
    {
        string downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        if (isCodeMode)
            return Path.Combine(downloads, $"pagina_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        if (isUrl)
        {
            string safeName = Uri.TryCreate(inputPath, UriKind.Absolute, out var uri)
                ? uri.Host.Replace('.', '_')
                : "pagina";
            return Path.Combine(downloads, safeName + ".pdf");
        }

        string dir  = Path.GetDirectoryName(inputPath) ?? downloads;
        string name = Path.GetFileNameWithoutExtension(inputPath) + ".pdf";
        return Path.Combine(dir, name);
    }
}
