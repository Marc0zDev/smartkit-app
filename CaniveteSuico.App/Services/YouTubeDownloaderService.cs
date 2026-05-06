using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using CaniveteSuico.App.Bridge;
using CaniveteSuico.App.Logging;

namespace CaniveteSuico.App.Services;

/// <summary>
/// Handles action "YOUTUBE_DOWNLOAD".
///
/// Supports:
///   format  = "video" (default) | "audio"
///   quality = "best" (default) | "1080" | "720" | "480" | "360"
///
/// For audio: runs -x --audio-format mp3, fires ITEM_COMPLETE after [ExtractAudio] Destination.
/// For video: uses -f selector + --merge-output-format mp4.
///
/// Events: PLAYLIST_INFO · ITEM_START · PROGRESS · LOG · ITEM_COMPLETE · SUCCESS
/// </summary>
public class YouTubeDownloaderService : IBridgeHandler
{
    public string Action => "YOUTUBE_DOWNLOAD";

    private static readonly Regex ProgressRx =
        new(@"\[download\]\s+(\d+(?:\.\d+)?)%", RegexOptions.Compiled);

    public async Task HandleAsync(JsonElement data, Action<object> reply)
    {
        string url = data.GetProperty("url").GetString()
            ?? throw new ArgumentException("'url' is required.");

        string outputDir = data.TryGetProperty("outputDir", out var dirProp)
            ? dirProp.GetString() ?? DefaultDownloads()
            : DefaultDownloads();

        string format = data.TryGetProperty("format", out var fmtProp)
            ? fmtProp.GetString() ?? "video"
            : "video";

        string quality = data.TryGetProperty("quality", out var qualProp)
            ? qualProp.GetString() ?? "best"
            : "best";

        bool isAudio = string.Equals(format, "audio", StringComparison.OrdinalIgnoreCase);

        Directory.CreateDirectory(outputDir);

        string ytDlpPath = Path.Combine(AppContext.BaseDirectory, "tools", "yt-dlp.exe");
        if (!File.Exists(ytDlpPath))
            throw new FileNotFoundException($"yt-dlp.exe not found at: {ytDlpPath}");

        string outputTemplate = Path.Combine(outputDir, "%(title)s.%(ext)s");

        string formatArgs = BuildFormatArgs(isAudio, quality);

        string args = $"--newline --no-simulate --no-quiet " +
                      $"--print \"YTMETA_T:%(title)s\" " +
                      $"--print \"YTMETA_P:%(playlist_title)s\" " +
                      $"--print \"YTMETA_N:%(n_entries)s\" " +
                      $"--print \"YTMETA_I:%(playlist_index)s\" " +
                      $"{formatArgs} " +
                      $"-o \"{outputTemplate}\" \"{url}\"";

        AppLogger.Info($"yt-dlp args: {args}");

        var psi = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var tcs = new TaskCompletionSource();
        var completedFiles = new List<CompletedItem>();

        string pendingTitle = "", pendingPl = "";
        int pendingN = 0, pendingI = 0;

        string currentTitle  = "";
        string currentFile   = "";
        string playlistTitle = "";
        int    totalItems    = 1;
        int    currentIndex  = 1;
        bool   plAnnounced   = false;

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            AppLogger.Debug($"[yt-dlp] {e.Data}");

            // ── YTMETA_T — title (always first of the 4 meta lines) ───────
            if (e.Data.StartsWith("YTMETA_T:"))
            {
                pendingTitle = e.Data["YTMETA_T:".Length..];
                pendingPl = ""; pendingN = 0; pendingI = 0;
                currentFile = "";
                return;
            }

            if (e.Data.StartsWith("YTMETA_P:"))
            {
                string raw = e.Data["YTMETA_P:".Length..];
                pendingPl = raw == "NA" ? "" : raw;
                return;
            }

            if (e.Data.StartsWith("YTMETA_N:"))
            {
                string raw = e.Data["YTMETA_N:".Length..];
                if (raw != "NA") int.TryParse(raw, out pendingN);
                return;
            }

            // ── YTMETA_I — last meta line: flush and emit ────────────────
            if (e.Data.StartsWith("YTMETA_I:"))
            {
                string raw = e.Data["YTMETA_I:".Length..];
                if (raw != "NA") int.TryParse(raw, out pendingI);

                currentTitle = pendingTitle;
                if (pendingN > 0) totalItems  = pendingN;
                if (pendingI > 0) currentIndex = pendingI;

                bool isPlaylist = !string.IsNullOrWhiteSpace(pendingPl) || totalItems > 1;

                if (isPlaylist && !plAnnounced)
                {
                    playlistTitle = pendingPl;
                    plAnnounced = true;
                    reply(new { type = "PLAYLIST_INFO", action = Action,
                                playlistTitle, total = totalItems });
                }

                reply(new { type = "ITEM_START", action = Action,
                            title = currentTitle, index = currentIndex, total = totalItems });
                return;
            }

            // ── Progress percentage ──────────────────────────────────────
            var prog = ProgressRx.Match(e.Data);
            if (prog.Success && double.TryParse(prog.Groups[1].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double pct))
            {
                reply(new { type = "PROGRESS", action = Action, percent = (int)pct,
                            index = currentIndex, total = totalItems });
                return;
            }

            // ── Destination path ─────────────────────────────────────────
            if (e.Data.StartsWith("[download] Destination:", StringComparison.OrdinalIgnoreCase))
            {
                currentFile = e.Data["[download] Destination:".Length..].Trim();
                return;
            }

            // ── Audio extraction destination (final .mp3 path) ───────────
            if (e.Data.StartsWith("[ExtractAudio] Destination:", StringComparison.OrdinalIgnoreCase))
            {
                currentFile = e.Data["[ExtractAudio] Destination:".Length..].Trim();

                if (isAudio && !string.IsNullOrEmpty(currentFile))
                {
                    var item = new CompletedItem(currentTitle, currentFile, currentIndex, totalItems);
                    lock (completedFiles) completedFiles.Add(item);
                    reply(new { type = "ITEM_COMPLETE", action = Action,
                                title = item.Title, file = item.File,
                                index = item.Index, total = item.Total,
                                format = "audio" });
                    currentFile = "";
                }
                return;
            }

            // ── Already downloaded ───────────────────────────────────────
            if (e.Data.StartsWith("[download]", StringComparison.OrdinalIgnoreCase) &&
                e.Data.Contains("has already been downloaded", StringComparison.OrdinalIgnoreCase))
            {
                string line = e.Data["[download]".Length..].Trim();
                int hasIdx = line.IndexOf(" has already", StringComparison.OrdinalIgnoreCase);
                if (hasIdx > 0) currentFile = line[..hasIdx].Trim();

                if (!string.IsNullOrEmpty(currentFile))
                {
                    var item = new CompletedItem(currentTitle, currentFile, currentIndex, totalItems);
                    lock (completedFiles) completedFiles.Add(item);
                    reply(new { type = "ITEM_COMPLETE", action = Action,
                                title = item.Title, file = item.File,
                                index = item.Index, total = item.Total,
                                alreadyCached = true,
                                format = isAudio ? "audio" : "video" });
                    currentFile = "";
                }
                return;
            }

            // ── 100% / download finished — for video mode only ───────────
            if (!isAudio &&
                (e.Data.StartsWith("[download] 100%", StringComparison.OrdinalIgnoreCase) ||
                 (e.Data.StartsWith("[download]", StringComparison.OrdinalIgnoreCase) &&
                  e.Data.Contains("in 00:", StringComparison.OrdinalIgnoreCase))))
            {
                if (!string.IsNullOrEmpty(currentFile))
                {
                    var item = new CompletedItem(currentTitle, currentFile, currentIndex, totalItems);
                    lock (completedFiles) completedFiles.Add(item);
                    reply(new { type = "ITEM_COMPLETE", action = Action,
                                title = item.Title, file = item.File,
                                index = item.Index, total = item.Total,
                                format = "video" });
                    currentFile = "";
                }
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            AppLogger.Debug($"[yt-dlp:err] {e.Data}");
            reply(new { type = "LOG", action = Action, message = e.Data });
        };

        process.Exited += (_, _) =>
        {
            if (process.ExitCode == 0)
                tcs.TrySetResult();
            else
                tcs.TrySetException(new Exception($"yt-dlp saiu com código {process.ExitCode}."));
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await tcs.Task;

        // Fallback: if ITEM_COMPLETE was never fired but we have a currentFile
        if (completedFiles.Count == 0 && !string.IsNullOrEmpty(currentFile))
        {
            completedFiles.Add(new CompletedItem(currentTitle, currentFile, 1, 1));
            reply(new { type = "ITEM_COMPLETE", action = Action,
                        title = currentTitle, file = currentFile, index = 1, total = 1,
                        format = isAudio ? "audio" : "video" });
        }

        AppLogger.Info($"Download concluído. {completedFiles.Count} arquivo(s).");

        reply(new
        {
            type = "SUCCESS",
            action = Action,
            files = completedFiles.Select(f => new { f.Title, f.File }).ToArray(),
            total = completedFiles.Count,
            playlistTitle = plAnnounced ? playlistTitle : (string?)null,
            format = isAudio ? "audio" : "video",
        });
    }

    private static string BuildFormatArgs(bool isAudio, string quality)
    {
        if (isAudio)
            return "-x --audio-format mp3 --audio-quality 0";

        return quality switch
        {
            "1080" => "-f \"bestvideo[height<=1080][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=1080]+bestaudio/best[height<=1080]\" --merge-output-format mp4",
            "720"  => "-f \"bestvideo[height<=720][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=720]+bestaudio/best[height<=720]\" --merge-output-format mp4",
            "480"  => "-f \"bestvideo[height<=480][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=480]+bestaudio/best[height<=480]\" --merge-output-format mp4",
            "360"  => "-f \"bestvideo[height<=360][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=360]+bestaudio/best[height<=360]\" --merge-output-format mp4",
            _      => "--merge-output-format mp4",
        };
    }

    private static string DefaultDownloads() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    private record CompletedItem(string Title, string File, int Index, int Total);
}
