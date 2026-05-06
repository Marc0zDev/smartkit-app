using System.Text.Json;
using CaniveteSuico.App.Bridge;
using CaniveteSuico.App.Logging;

namespace CaniveteSuico.App.Services;

/// <summary>
/// Handles action "SCHEDULER".
///
/// Singleton service that maintains a list of scheduled YouTube downloads
/// and executes them at their scheduled time via a background timer.
///
/// Commands (field "command"):
///   ADD    – { url, format, quality, outputDir, scheduledTime (ISO 8601) }
///   LIST   – returns current job list
///   CANCEL – { jobId }
///   CLEAR  – removes done/error/cancelled jobs
///
/// Proactive events pushed via _globalReply:
///   { type: "SCHEDULER_UPDATE", jobs: [...] }
/// </summary>
public class DownloadSchedulerService : IBridgeHandler
{
    public string Action => "SCHEDULER";

    private readonly Action<object> _globalReply;
    private readonly List<ScheduledJob> _jobs = [];
    private readonly SemaphoreSlim _runLock = new(1, 1);
    private readonly System.Threading.Timer _timer;
    private readonly YouTubeDownloaderService _ytService = new();

    public DownloadSchedulerService(Action<object> globalReply)
    {
        _globalReply = globalReply;
        _timer = new System.Threading.Timer(OnTick, null,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30));
    }

    public Task HandleAsync(JsonElement data, Action<object> reply)
    {
        string command = data.TryGetProperty("command", out var cmdProp)
            ? cmdProp.GetString() ?? ""
            : "";

        switch (command.ToUpperInvariant())
        {
            case "ADD":
            {
                string url      = data.GetProperty("url").GetString() ?? "";
                string format   = data.TryGetProperty("format",   out var f) ? f.GetString() ?? "video" : "video";
                string quality  = data.TryGetProperty("quality",  out var q) ? q.GetString() ?? "best"  : "best";
                string outDir   = data.TryGetProperty("outputDir", out var d) ? d.GetString() ?? "" : "";
                string timeStr  = data.TryGetProperty("scheduledTime", out var t) ? t.GetString() ?? "" : "";

                if (!DateTime.TryParse(timeStr, null,
                        System.Globalization.DateTimeStyles.RoundtripKind,
                        out DateTime scheduledTime))
                {
                    reply(new { type = "ERROR", action = Action, message = "Data/hora inválida." });
                    return Task.CompletedTask;
                }

                var job = new ScheduledJob
                {
                    Url           = url,
                    Format        = format,
                    Quality       = quality,
                    OutputDir     = outDir,
                    ScheduledTime = scheduledTime.ToLocalTime(),
                };
                lock (_jobs) _jobs.Add(job);

                AppLogger.Info($"Scheduler: job adicionado {job.Id} → {job.ScheduledTime:HH:mm}");
                reply(new { type = "SCHEDULER_JOB_ADDED", action = Action, jobId = job.Id });
                BroadcastJobList();
                break;
            }

            case "LIST":
                reply(SerializeList());
                break;

            case "CANCEL":
            {
                string jobId = data.TryGetProperty("jobId", out var jid) ? jid.GetString() ?? "" : "";
                lock (_jobs)
                {
                    var job = _jobs.FirstOrDefault(j => j.Id == jobId);
                    if (job is { Status: JobStatus.Pending })
                    {
                        job.Status = JobStatus.Cancelled;
                        AppLogger.Info($"Scheduler: job cancelado {jobId}");
                    }
                }
                BroadcastJobList();
                break;
            }

            case "CLEAR":
                lock (_jobs)
                    _jobs.RemoveAll(j => j.Status is JobStatus.Done or JobStatus.Error or JobStatus.Cancelled);
                BroadcastJobList();
                break;
        }

        return Task.CompletedTask;
    }

    private async void OnTick(object? state)
    {
        List<ScheduledJob> due;
        lock (_jobs)
        {
            due = _jobs
                .Where(j => j.Status == JobStatus.Pending && j.ScheduledTime <= DateTime.Now)
                .ToList();
        }

        if (due.Count == 0) return;

        // Run one job at a time
        if (!await _runLock.WaitAsync(0)) return;
        try
        {
            foreach (var job in due)
            {
                await RunJobAsync(job);
            }
        }
        finally
        {
            _runLock.Release();
        }
    }

    private async Task RunJobAsync(ScheduledJob job)
    {
        job.Status = JobStatus.Running;
        AppLogger.Info($"Scheduler: executando job {job.Id} ({job.Url})");
        BroadcastJobList();

        try
        {
            string jobJson = JsonSerializer.Serialize(new
            {
                url       = job.Url,
                format    = job.Format,
                quality   = job.Quality,
                outputDir = string.IsNullOrWhiteSpace(job.OutputDir) ? (object?)null : job.OutputDir,
            });

            using var doc = JsonDocument.Parse(jobJson);
            var data = doc.RootElement;

            await _ytService.HandleAsync(data, payload =>
            {
                // Forward yt-dlp events tagged with jobId and action=SCHEDULER
                string raw = JsonSerializer.Serialize(payload);
                using var pDoc = JsonDocument.Parse(raw);
                // Rebuild with scheduler metadata
                var dict = pDoc.RootElement.EnumerateObject()
                    .ToDictionary(p => p.Name, p => (object)p.Value.ToString());

                _globalReply(new
                {
                    type    = "SCHEDULER_EVENT",
                    jobId   = job.Id,
                    action  = "SCHEDULER",
                    payload = payload,
                });
            });

            job.Status = JobStatus.Done;
            AppLogger.Info($"Scheduler: job {job.Id} concluído.");
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Error;
            job.ErrorMessage = ex.Message;
            AppLogger.Error(ex, $"Scheduler: job {job.Id} falhou");
        }

        BroadcastJobList();
    }

    private void BroadcastJobList()
    {
        _globalReply(SerializeList());
    }

    private object SerializeList()
    {
        List<object> snapshot;
        lock (_jobs)
        {
            snapshot = _jobs.Select(j => (object)new
            {
                j.Id,
                j.Url,
                j.Format,
                j.Quality,
                j.OutputDir,
                scheduledTime = j.ScheduledTime.ToString("o"),
                status        = j.Status.ToString().ToLowerInvariant(),
                j.ErrorMessage,
            }).ToList();
        }

        return new { type = "SCHEDULER_UPDATE", action = "SCHEDULER", jobs = snapshot };
    }
}

// ── Domain models ─────────────────────────────────────────────────────────

public class ScheduledJob
{
    public string   Id            { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string   Url           { get; init; } = "";
    public string   Format        { get; init; } = "video";
    public string   Quality       { get; init; } = "best";
    public string   OutputDir     { get; init; } = "";
    public DateTime ScheduledTime { get; init; }
    public JobStatus Status       { get; set; } = JobStatus.Pending;
    public string?  ErrorMessage  { get; set; }
}

public enum JobStatus { Pending, Running, Done, Error, Cancelled }
