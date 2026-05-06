using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using CaniveteSuico.App.Bridge;
using CaniveteSuico.App.Logging;

namespace CaniveteSuico.App.Services;

/// <summary>
/// Handles action "VIDEO_CONVERT".
///
/// Uses ffmpeg.exe (expected in tools/ next to the app, same folder as yt-dlp.exe).
///
/// Expected data:
///   { "inputPath":  "C:\\video.mp4",
///     "outputFormat": "mp4" | "mkv" | "webm" | "avi" | "mp3" | "aac",
///     "quality":    "high" | "medium" | "low" | "lossless",
///     "outputPath": "C:\\out.mp4"  (optional) }
///
/// Events: PROGRESS (percent + message), SUCCESS, ERROR
/// </summary>
public class VideoConverterService : IBridgeHandler
{
    public string Action => "VIDEO_CONVERT";

    private static readonly Regex DurationRx =
        new(@"Duration:\s*(\d+):(\d+):(\d+)\.(\d+)", RegexOptions.Compiled);

    private static readonly Regex TimeRx =
        new(@"\btime=(\d+):(\d+):(\d+)\.(\d+)", RegexOptions.Compiled);

    private static readonly string[] FfmpegSearchPaths =
    [
        Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe"),
        @"C:\ffmpeg\bin\ffmpeg.exe",
        @"C:\ProgramData\chocolatey\bin\ffmpeg.exe",
    ];

    public async Task HandleAsync(JsonElement data, Action<object> reply)
    {
        string inputPath = data.GetProperty("inputPath").GetString()
            ?? throw new ArgumentException("'inputPath' is required.");

        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Arquivo não encontrado: {inputPath}");

        string outputFormat = data.TryGetProperty("outputFormat", out var fmtProp)
            ? fmtProp.GetString() ?? "mp4"
            : "mp4";

        string quality = data.TryGetProperty("quality", out var qualProp)
            ? qualProp.GetString() ?? "medium"
            : "medium";

        bool isAudioOnly = outputFormat is "mp3" or "aac" or "wav" or "flac";

        string outputPath = data.TryGetProperty("outputPath", out var outProp)
            ? outProp.GetString() ?? BuildDefaultOutput(inputPath, outputFormat)
            : BuildDefaultOutput(inputPath, outputFormat);

        string ffmpegPath = FindFfmpeg();

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        string args = BuildFfmpegArgs(inputPath, outputPath, outputFormat, quality, isAudioOnly);
        AppLogger.Info($"ffmpeg args: {args}");

        reply(new { type = "PROGRESS", action = Action,
                    message = "Iniciando conversão…", percent = 0 });

        var tcs = new TaskCompletionSource();
        double totalSeconds = 0;

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute = false,
            CreateNoWindow  = true,
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            AppLogger.Debug($"[ffmpeg] {e.Data}");

            // Parse total duration (emitted near the start)
            if (totalSeconds == 0)
            {
                var dm = DurationRx.Match(e.Data);
                if (dm.Success)
                {
                    totalSeconds = int.Parse(dm.Groups[1].Value) * 3600
                                 + int.Parse(dm.Groups[2].Value) * 60
                                 + int.Parse(dm.Groups[3].Value)
                                 + int.Parse(dm.Groups[4].Value) / 100.0;
                }
            }

            // Parse current progress time
            var tm = TimeRx.Match(e.Data);
            if (tm.Success && totalSeconds > 0)
            {
                double cur = int.Parse(tm.Groups[1].Value) * 3600
                           + int.Parse(tm.Groups[2].Value) * 60
                           + int.Parse(tm.Groups[3].Value)
                           + int.Parse(tm.Groups[4].Value) / 100.0;

                int pct = (int)Math.Min(95, cur / totalSeconds * 100);
                reply(new { type = "PROGRESS", action = Action,
                            message = $"Convertendo… {pct}%", percent = pct });
            }
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) AppLogger.Debug($"[ffmpeg:out] {e.Data}");
        };

        process.Exited += (_, _) =>
        {
            if (process.ExitCode == 0)
                tcs.TrySetResult();
            else
                tcs.TrySetException(new Exception($"ffmpeg saiu com código {process.ExitCode}."));
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await tcs.Task;

        long sizeOut = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;
        AppLogger.Info($"Video Convert concluído: {outputPath}");

        reply(new { type = "SUCCESS", action = Action,
                    outputPath,
                    sizeFormatted = FormatBytes(sizeOut) });
    }

    private static string BuildFfmpegArgs(string input, string output,
        string fmt, string quality, bool audioOnly)
    {
        // CRF values: lower = better quality / larger file
        string crf = quality switch
        {
            "lossless" => "0",
            "high"     => "18",
            "low"      => "32",
            _          => "23",  // medium
        };

        if (audioOnly)
        {
            return fmt switch
            {
                "mp3"  => $"-i \"{input}\" -vn -acodec libmp3lame -q:a 2 -y \"{output}\"",
                "aac"  => $"-i \"{input}\" -vn -acodec aac -b:a 192k -y \"{output}\"",
                "wav"  => $"-i \"{input}\" -vn -acodec pcm_s16le -y \"{output}\"",
                "flac" => $"-i \"{input}\" -vn -acodec flac -y \"{output}\"",
                _      => $"-i \"{input}\" -vn -y \"{output}\"",
            };
        }

        return fmt switch
        {
            "mp4"  => $"-i \"{input}\" -c:v libx264 -crf {crf} -preset medium -c:a aac -b:a 192k -movflags +faststart -y \"{output}\"",
            "mkv"  => $"-i \"{input}\" -c:v libx265 -crf {crf} -preset medium -c:a aac -b:a 192k -y \"{output}\"",
            "webm" => $"-i \"{input}\" -c:v libvpx-vp9 -crf {crf} -b:v 0 -c:a libopus -y \"{output}\"",
            "avi"  => $"-i \"{input}\" -c:v libxvid -q:v 4 -c:a libmp3lame -q:a 4 -y \"{output}\"",
            "gif"  => $"-i \"{input}\" -vf \"fps=15,scale=480:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" -loop 0 -y \"{output}\"",
            _      => $"-i \"{input}\" -y \"{output}\"",
        };
    }

    private static string FindFfmpeg()
    {
        // Check known paths first
        foreach (string path in FfmpegSearchPaths)
            if (File.Exists(path)) return path;

        // Try PATH via where.exe
        try
        {
            var psi = new ProcessStartInfo("where", "ffmpeg.exe")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var p = Process.Start(psi)!;
            string? line = p.StandardOutput.ReadLine();
            p.WaitForExit();
            if (!string.IsNullOrWhiteSpace(line) && File.Exists(line.Trim()))
                return line.Trim();
        }
        catch { /* not found via where */ }

        throw new FileNotFoundException(
            "ffmpeg.exe não encontrado.\n\n" +
            "Coloque ffmpeg.exe na pasta tools/ junto ao executável, " +
            "ou instale o FFmpeg e adicione ao PATH.\n\n" +
            "Download: https://ffmpeg.org/download.html");
    }

    private static string BuildDefaultOutput(string input, string fmt)
    {
        string dir  = Path.GetDirectoryName(input) ?? ".";
        string name = Path.GetFileNameWithoutExtension(input) + "_convertido." + fmt;
        return Path.Combine(dir, name);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)     return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }
}
