using System.IO;
using System.Text.Json;
using CaniveteSuico.App.Bridge;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace CaniveteSuico.App.Services;

/// <summary>
/// Handles action "IMAGE_CONVERT".
/// Expected data: { "inputPath": "C:\\file.jpg", "outputFormat": "png" }
/// Supported output formats: png, jpg, webp, bmp, gif
/// Reply: { type:"SUCCESS", action:"IMAGE_CONVERT", outputPath:"C:\\file.png" }
/// </summary>
public class ImageConverterService : IBridgeHandler
{
    public string Action => "IMAGE_CONVERT";

    private static readonly HashSet<string> SupportedFormats =
        new(StringComparer.OrdinalIgnoreCase) { "png", "jpg", "jpeg", "webp", "bmp", "gif" };

    public async Task HandleAsync(JsonElement data, Action<object> reply)
    {
        string inputPath = data.GetProperty("inputPath").GetString()
            ?? throw new ArgumentException("'inputPath' is required.");

        string outputFormat = data.TryGetProperty("outputFormat", out var fmtProp)
            ? fmtProp.GetString() ?? "png"
            : "png";

        outputFormat = outputFormat.TrimStart('.').ToLowerInvariant();

        if (!SupportedFormats.Contains(outputFormat))
            throw new ArgumentException($"Unsupported output format: '{outputFormat}'. Supported: {string.Join(", ", SupportedFormats)}");

        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Input file not found: {inputPath}");

        string outputDir = Path.GetDirectoryName(inputPath) ?? ".";
        string outputName = Path.GetFileNameWithoutExtension(inputPath) + "." + NormalizeExt(outputFormat);
        string outputPath = Path.Combine(outputDir, outputName);

        using var image = await SixLabors.ImageSharp.Image.LoadAsync(inputPath);

        var encoder = GetEncoder(outputFormat);
        await image.SaveAsync(outputPath, encoder);

        reply(new { type = "SUCCESS", action = Action, outputPath });
    }

    private static string NormalizeExt(string fmt) => fmt == "jpeg" ? "jpg" : fmt;

    private static SixLabors.ImageSharp.Formats.IImageEncoder GetEncoder(string fmt) => fmt switch
    {
        "png" => new PngEncoder(),
        "jpg" or "jpeg" => new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder(),
        "webp" => new SixLabors.ImageSharp.Formats.Webp.WebpEncoder(),
        "bmp" => new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder(),
        "gif" => new SixLabors.ImageSharp.Formats.Gif.GifEncoder(),
        _ => new PngEncoder(),
    };
}
