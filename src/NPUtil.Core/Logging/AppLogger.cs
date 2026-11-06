namespace NPUtil.Core.Logging;

public enum LogLevel
{
    Trace,
    Debug,
    Info,
    Warning,
    Error
}

public record LogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Source,
    string Message,
    Exception? Exception = null
);

public interface ILogger
{
    void Log(LogLevel level, string source, string message, Exception? exception = null);
    void Debug(string source, string message);
    void Info(string source, string message);
    void Warning(string source, string message, Exception? exception = null);
    void Error(string source, string message, Exception? exception = null);
}

public class AppLogger : ILogger
{
    private static readonly object _lock = new();
    private static readonly List<LogEntry> _recentEntries = new(1024);
    private static string? _logFilePath;

    public static event EventHandler<LogEntry>? LogEntryAdded;

    public static void Initialize(string? customLogPath = null)
    {
        lock (_lock)
        {
            var logDir = customLogPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NPUtil", "Logs");
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir, "nputil.log");

            // Rotate if log file exceeds 10 MB
            if (File.Exists(_logFilePath) && new FileInfo(_logFilePath).Length > 10 * 1024 * 1024)
            {
                var backup = Path.Combine(logDir, $"nputil-{DateTime.UtcNow:yyyyMMddHHmmss}.log");
                try { File.Move(_logFilePath, backup); } catch { /* ignore */ }
            }
        }
    }

    public static IReadOnlyList<LogEntry> GetRecentEntries()
    {
        lock (_lock)
        {
            return _recentEntries.ToList();
        }
    }

    public void Log(LogLevel level, string source, string message, Exception? exception = null)
    {
        var entry = new LogEntry(DateTime.Now, level, source, message, exception);

        lock (_lock)
        {
            if (_recentEntries.Count >= 1000)
            {
                _recentEntries.RemoveAt(0);
            }
            _recentEntries.Add(entry);

            if (!string.IsNullOrEmpty(_logFilePath))
            {
                try
                {
                    var line = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{entry.Level}] [{entry.Source}] {entry.Message}";
                    if (entry.Exception != null)
                    {
                        line += Environment.NewLine + entry.Exception;
                    }
                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
                }
                catch
                {
                    // Fail-safe: logging should never crash the host application
                }
            }
        }

        try
        {
            LogEntryAdded?.Invoke(this, entry);
        }
        catch { /* ignored */ }
    }

    public void Debug(string source, string message) => Log(LogLevel.Debug, source, message);
    public void Info(string source, string message) => Log(LogLevel.Info, source, message);
    public void Warning(string source, string message, Exception? exception = null) => Log(LogLevel.Warning, source, message, exception);
    public void Error(string source, string message, Exception? exception = null) => Log(LogLevel.Error, source, message, exception);
}
