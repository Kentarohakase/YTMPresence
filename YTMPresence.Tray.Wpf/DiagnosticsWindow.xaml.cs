using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Threading;

using YTMPresence.Core;

namespace YTMPresence.TrayWpf;

public partial class DiagnosticsWindow : Window
{
  private readonly Func<CompanionStatus?> _getStatus;
  private readonly AppSettings _settings;
  private readonly string _settingsPath;
  private readonly DispatcherTimer _timer;

  private string _lastReport = "";

  public DiagnosticsWindow(Func<CompanionStatus?> getStatus, AppSettings settings, string settingsPath)
  {
    InitializeComponent();

    _getStatus = getStatus;
    _settings = settings;
    _settingsPath = settingsPath;
    _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, __) => UpdateStatus(), Dispatcher);

    Loaded += (_, __) =>
    {
      UpdateStatus();
      _timer.Start();
    };

    Closed += (_, __) => _timer.Stop();
  }

  private void UpdateStatus()
  {
    var status = _getStatus();
    var version = GetAppVersion();
    var endpoint = _settings.GetWebSocketEndpoint();
    var logPath = Logger.GetCurrentLogFilePath();

    VersionText.Text = version;
    EndpointText.Text = endpoint;
    SettingsText.Text = _settingsPath;
    LogText.Text = logPath;
    UpdatedText.Text = DateTime.Now.ToString("HH:mm:ss");

    if (status is null)
    {
      ServerText.Text = "Keine Serverdaten";
      ExtensionText.Text = "Unbekannt";
      DiscordText.Text = "Unbekannt";
      TrackText.Text = "Kein Track";
      ProgressText.Text = "--";
      SecurityText.Text = "Unbekannt";
      _lastReport = BuildReport(null, version, endpoint, logPath);
      return;
    }

    ServerText.Text = status.IsRunning ? "Läuft" : "Gestoppt";
    ExtensionText.Text = status.ConnectedClients > 0
        ? $"Verbunden ({status.ConnectedClients}), letzte Daten: {AgeText(status.LastMessageUtc)}"
        : $"Nicht verbunden, letzte Daten: {AgeText(status.LastMessageUtc)}";

    DiscordText.Text = status.DiscordOk
        ? $"OK, letzter Erfolg: {AgeText(status.LastDiscordOkUtc)}"
        : $"Fehler: {status.LastDiscordError ?? "unknown"}";

    TrackText.Text = string.IsNullOrWhiteSpace(status.LastTitle)
        ? "Kein Track"
        : $"{status.LastTitle} - {status.LastArtist ?? "YouTube Music"}";

    ProgressText.Text = FormatProgress(status.LastPositionSeconds, status.LastDurationSeconds, status.LastIsPlaying);
    SecurityText.Text = status.UnauthorizedMessages > 0
        ? $"{status.UnauthorizedMessages} ungueltige Token, zuletzt: {AgeText(status.LastUnauthorizedUtc)}"
        : "OK";

    _lastReport = BuildReport(status, version, endpoint, logPath);
  }

  private string BuildReport(CompanionStatus? status, string version, string endpoint, string logPath)
  {
    var sb = new StringBuilder();
    sb.AppendLine("YTMPresence Diagnose");
    sb.AppendLine($"Version: {version}");
    sb.AppendLine($"Endpoint: {endpoint}");
    sb.AppendLine($"Settings: {_settingsPath}");
    sb.AppendLine($"Log: {logPath}");

    if (status is null)
    {
      sb.AppendLine("Status: keine Serverdaten");
      return sb.ToString();
    }

    sb.AppendLine($"Server: {(status.IsRunning ? "running" : "stopped")}");
    sb.AppendLine($"Extension clients: {status.ConnectedClients}");
    sb.AppendLine($"Last message: {status.LastMessageUtc?.ToString("O") ?? "never"}");
    sb.AppendLine($"Discord OK: {status.DiscordOk}");
    sb.AppendLine($"Discord error: {status.LastDiscordError ?? ""}");
    sb.AppendLine($"Track: {status.LastTitle ?? ""} - {status.LastArtist ?? ""}");
    sb.AppendLine($"Playing: {status.LastIsPlaying?.ToString() ?? ""}");
    sb.AppendLine($"Progress: {FormatProgress(status.LastPositionSeconds, status.LastDurationSeconds, status.LastIsPlaying)}");
    sb.AppendLine($"Unauthorized: {status.UnauthorizedMessages}");
    sb.AppendLine($"Last unauthorized: {status.LastUnauthorizedUtc?.ToString("O") ?? "never"}");
    return sb.ToString();
  }

  private static string FormatProgress(double? position, double? duration, bool? isPlaying)
  {
    var state = isPlaying == true ? "laeuft" : "pausiert";
    if (duration is not double d || d <= 0)
      return $"{FormatTime(position ?? 0)} / --:-- ({state})";

    return $"{FormatTime(position ?? 0)} / {FormatTime(d)} ({state})";
  }

  private static string FormatTime(double seconds)
  {
    if (!double.IsFinite(seconds) || seconds < 0)
      seconds = 0;

    var ts = TimeSpan.FromSeconds(seconds);
    return ts.Hours > 0
        ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
        : $"{ts.Minutes}:{ts.Seconds:D2}";
  }

  private static string AgeText(DateTimeOffset? value)
  {
    if (value is null)
      return "nie";

    var age = DateTimeOffset.UtcNow - value.Value;
    if (age < TimeSpan.FromSeconds(5)) return "gerade eben";
    if (age < TimeSpan.FromMinutes(1)) return $"{(int)age.TotalSeconds}s";
    if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes}m";
    return $"{(int)age.TotalHours}h";
  }

  private static string GetAppVersion()
  {
    var assembly = Assembly.GetExecutingAssembly();
    var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    return string.IsNullOrWhiteSpace(version) ? "dev" : version;
  }

  private void CopyButton_Click(object sender, RoutedEventArgs e)
  {
    try
    {
      System.Windows.Clipboard.SetText(_lastReport);
      StatusText.Text = "Diagnose kopiert.";
    }
    catch
    {
      StatusText.Text = "Kopieren fehlgeschlagen.";
    }
  }

  private void CloseButton_Click(object sender, RoutedEventArgs e)
  {
    Close();
  }
}
