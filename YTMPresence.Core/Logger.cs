using System.Text;

namespace YTMPresence.Core;

public static class Logger
{
  private const long MaxLogFileBytes = 5 * 1024 * 1024; // 5 MB
  private const int MaxArchivedLogs = 3;

  private static readonly object SyncRoot = new();

  private static bool _initialized;
  private static string _logDirectory = string.Empty;
  private static string _currentLogFilePath = string.Empty;

  public static void Initialize(string appName = "YTMPresence")
  {
    lock (SyncRoot)
    {
      if (_initialized)
        return;

      _logDirectory = GetLogDirectory(appName);
      Directory.CreateDirectory(_logDirectory);

      _currentLogFilePath = Path.Combine(_logDirectory, "app.log");
      _initialized = true;

      WriteInternal("INFO", $"Logger initialisiert. Log-Datei: {_currentLogFilePath}");
    }
  }

  public static string GetLogDirectoryPath()
  {
    EnsureInitialized();
    return _logDirectory;
  }

  public static string GetCurrentLogFilePath()
  {
    EnsureInitialized();
    return _currentLogFilePath;
  }

  public static void Info(string message) => Write("INFO", message);
  public static void Warn(string message) => Write("WARN", message);
  public static void Debug(string message) => Write("DEBUG", message);

  public static void Error(string message) => Write("ERROR", message);

  public static void Error(Exception ex, string? message = null)
  {
    var sb = new StringBuilder();

    if (!string.IsNullOrWhiteSpace(message))
      sb.AppendLine(message);

    sb.AppendLine(ex.GetType().FullName ?? ex.GetType().Name);
    sb.AppendLine(ex.Message);

    if (!string.IsNullOrWhiteSpace(ex.StackTrace))
      sb.AppendLine(ex.StackTrace);

    Write("ERROR", sb.ToString().TrimEnd());
  }

  private static void Write(string level, string message)
  {
    EnsureInitialized();

    lock (SyncRoot)
    {
      try
      {
        RollIfNeeded();

        WriteInternal(level, message);
      }
      catch
      {
        // Logging darf die App nie crashen lassen.
      }
    }
  }

  private static void WriteInternal(string level, string message)
  {
    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
    File.AppendAllText(_currentLogFilePath, line, Encoding.UTF8);
  }

  private static void RollIfNeeded()
  {
    if (!File.Exists(_currentLogFilePath))
      return;

    var info = new FileInfo(_currentLogFilePath);
    if (info.Length < MaxLogFileBytes)
      return;

    // app.2.log -> app.3.log, app.1.log -> app.2.log, app.log -> app.1.log
    for (var i = MaxArchivedLogs; i >= 1; i--)
    {
      var source = Path.Combine(_logDirectory, i == 1 ? "app.log" : $"app.{i - 1}.log");
      var target = Path.Combine(_logDirectory, $"app.{i}.log");

      if (!File.Exists(source))
        continue;

      if (File.Exists(target))
        File.Delete(target);

      File.Move(source, target);
    }
  }

  private static void EnsureInitialized()
  {
    if (_initialized)
      return;

    Initialize();
  }

  private static string GetLogDirectory(string appName)
  {
    if (OperatingSystem.IsWindows())
    {
      var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
      return Path.Combine(appData, appName, "logs");
    }

    if (OperatingSystem.IsMacOS())
    {
      var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
      return Path.Combine(home, "Library", "Application Support", appName, "logs");
    }

    var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
    if (!string.IsNullOrWhiteSpace(xdg))
      return Path.Combine(xdg, appName, "logs");

    var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    return Path.Combine(userHome, ".config", appName, "logs");
  }
}