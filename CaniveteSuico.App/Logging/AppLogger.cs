using System.IO;
using System.Runtime.CompilerServices;

namespace CaniveteSuico.App.Logging;

/// <summary>
/// Minimal static logger — writes to Console (visible in the debug console window)
/// and to a daily rotating log file under %LocalAppData%\CaniveteSuico\logs\.
/// </summary>
public static class AppLogger
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CaniveteSuico", "logs");

    private static readonly string LogFile = Path.Combine(
        LogDir, $"app-{DateTime.Now:yyyy-MM-dd}.log");

    private static readonly object FileLock = new();

    static AppLogger()
    {
        Directory.CreateDirectory(LogDir);
    }

    public static void Info(string message,
        [CallerMemberName] string member = "",
        [CallerFilePath]   string file   = "")
        => Write("INFO ", message, member, file);

    public static void Warn(string message,
        [CallerMemberName] string member = "",
        [CallerFilePath]   string file   = "")
        => Write("WARN ", message, member, file);

    public static void Error(string message,
        [CallerMemberName] string member = "",
        [CallerFilePath]   string file   = "")
        => Write("ERROR", message, member, file);

    public static void Error(Exception ex, string context = "",
        [CallerMemberName] string member = "",
        [CallerFilePath]   string file   = "")
    {
        string msg = string.IsNullOrEmpty(context)
            ? $"{ex.GetType().Name}: {ex.Message}"
            : $"{context} — {ex.GetType().Name}: {ex.Message}";

        Write("ERROR", msg, member, file);

        if (ex.StackTrace is not null)
            Write("TRACE", ex.StackTrace, member, file);
    }

    public static void Debug(string message,
        [CallerMemberName] string member = "",
        [CallerFilePath]   string file   = "")
        => Write("DEBUG", message, member, file);

    // ── internals ─────────────────────────────────────────────────────────

    private static void Write(string level, string message, string member, string file)
    {
        string shortFile = Path.GetFileNameWithoutExtension(file);
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string line = $"[{timestamp}] [{level}] [{shortFile}.{member}] {message}";

        // Console (visible in the debug window attached by AllocConsole)
        ConsoleColor prev = Console.ForegroundColor;
        Console.ForegroundColor = level switch
        {
            "ERROR" => ConsoleColor.Red,
            "WARN " => ConsoleColor.Yellow,
            "DEBUG" => ConsoleColor.DarkGray,
            _       => ConsoleColor.White,
        };
        Console.WriteLine(line);
        Console.ForegroundColor = prev;

        // File (thread-safe)
        lock (FileLock)
        {
            try { File.AppendAllText(LogFile, line + Environment.NewLine); }
            catch { /* never crash the app because of logging */ }
        }
    }

    /// <summary>Path to the current log file, for display in the UI.</summary>
    public static string CurrentLogPath => LogFile;
}
