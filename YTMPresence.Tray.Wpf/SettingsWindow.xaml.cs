using System.Windows;
using YTMPresence.Core;

namespace YTMPresence.TrayWpf;

public partial class SettingsWindow : Window
{
  private readonly AppSettings _settings;
  private readonly string _settingsPath;

  private readonly string _initialDiscordClientId;
  private readonly string _initialListenHost;
  private readonly int _initialListenPort;
  private readonly string _initialWebSocketPath;
  private readonly string _initialSecurityToken;

  public event EventHandler<SettingsSavedEventArgs>? SettingsSaved;

  public SettingsWindow(AppSettings settings, string settingsPath)
  {
    InitializeComponent();

    _settings = settings;
    _settingsPath = settingsPath;

    _initialDiscordClientId = settings.DiscordClientId;
    _initialListenHost = settings.ListenHost;
    _initialListenPort = settings.ListenPort;
    _initialWebSocketPath = settings.WebSocketPath;
    _initialSecurityToken = settings.SecurityToken;

    LoadSettings();
  }

  private void LoadSettings()
  {
    SecurityTokenBox.Text = _settings.SecurityToken;
    ListenHostBox.Text = _settings.ListenHost;
    ListenPortBox.Text = _settings.ListenPort.ToString();
    WebSocketPathBox.Text = _settings.WebSocketPath;

    MinUpdateSecondsBox.Text = _settings.MinDiscordUpdateSeconds.ToString();
    IdleClearSecondsBox.Text = _settings.IdleClearSeconds.ToString();
    PausedClearSecondsBox.Text = _settings.PausedClearSeconds.ToString();
    OnlyShowWhenPlayingBox.IsChecked = _settings.OnlyShowWhenPlaying;
    AdBehaviorBox.SelectedValue = _settings.AdBehavior.ToString();

    DiscordClientIdBox.Text = _settings.DiscordClientId;
  }

  private void CopyTokenButton_Click(object sender, RoutedEventArgs e)
  {
    try
    {
      System.Windows.Clipboard.SetText(SecurityTokenBox.Text.Trim());
      SetStatus(UiText.SettingsTokenCopied, isError: false);
    }
    catch
    {
      SetStatus(UiText.SettingsTokenCopyFailed, isError: true);
    }
  }

  private void GenerateTokenButton_Click(object sender, RoutedEventArgs e)
  {
    SecurityTokenBox.Text = SecurityTokenHelper.GenerateSecureToken();
    SetStatus(UiText.SettingsTokenGenerated, isError: false);
  }

  private void SaveButton_Click(object sender, RoutedEventArgs e)
  {
    if (!TryReadInt(ListenPortBox, UiText.LabelPort, 1, 65535, out var listenPort)) return;
    if (!TryReadInt(MinUpdateSecondsBox, UiText.LabelDiscordUpdateInterval, 5, 300, out var minUpdateSeconds)) return;
    if (!TryReadInt(IdleClearSecondsBox, UiText.LabelIdleClearSeconds, 10, 3600, out var idleClearSeconds)) return;
    if (!TryReadInt(PausedClearSecondsBox, UiText.LabelPausedClearSeconds, 10, 3600, out var pausedClearSeconds)) return;

    var listenHost = ListenHostBox.Text.Trim();
    if (string.IsNullOrWhiteSpace(listenHost))
    {
      SetStatus(UiText.SettingsHostEmpty, isError: true);
      ListenHostBox.Focus();
      return;
    }

    if (listenHost.Contains("://", StringComparison.Ordinal) ||
        listenHost.Contains('/') ||
        listenHost.Contains('\\'))
    {
      SetStatus(UiText.SettingsHostNotUrl, isError: true);
      ListenHostBox.Focus();
      return;
    }

    var webSocketPath = WebSocketPathBox.Text.Trim();
    if (string.IsNullOrWhiteSpace(webSocketPath))
    {
      SetStatus(UiText.SettingsWebSocketPathEmpty, isError: true);
      WebSocketPathBox.Focus();
      return;
    }

    if (!webSocketPath.StartsWith('/'))
      webSocketPath = "/" + webSocketPath;

    if (webSocketPath.Any(char.IsWhiteSpace) ||
        webSocketPath.Contains('?') ||
        webSocketPath.Contains('#'))
    {
      SetStatus(UiText.SettingsWebSocketPathInvalid, isError: true);
      WebSocketPathBox.Focus();
      return;
    }

    var discordClientId = DiscordClientIdBox.Text.Trim();
    if (string.IsNullOrWhiteSpace(discordClientId))
    {
      SetStatus(UiText.SettingsClientIdEmpty, isError: true);
      DiscordClientIdBox.Focus();
      return;
    }

    var token = SecurityTokenBox.Text.Trim();
    if (string.IsNullOrWhiteSpace(token))
    {
      SetStatus(UiText.SettingsTokenEmpty, isError: true);
      return;
    }

    var selectedAdBehavior = ReadAdBehavior();

    _settings.DiscordClientId = discordClientId;
    _settings.ListenHost = listenHost;
    _settings.ListenPort = listenPort;
    _settings.WebSocketPath = webSocketPath;
    _settings.MinDiscordUpdateSeconds = minUpdateSeconds;
    _settings.IdleClearSeconds = idleClearSeconds;
    _settings.PausedClearSeconds = pausedClearSeconds;
    _settings.OnlyShowWhenPlaying = OnlyShowWhenPlayingBox.IsChecked == true;
    _settings.AdBehavior = selectedAdBehavior;
    _settings.SecurityToken = token;

    SettingsStore.Save(_settingsPath, _settings);

    var requiresRestart =
        !string.Equals(_settings.DiscordClientId, _initialDiscordClientId, StringComparison.Ordinal) ||
        !string.Equals(_settings.ListenHost, _initialListenHost, StringComparison.Ordinal) ||
        _settings.ListenPort != _initialListenPort ||
        !string.Equals(_settings.WebSocketPath, _initialWebSocketPath, StringComparison.Ordinal) ||
        !string.Equals(_settings.SecurityToken, _initialSecurityToken, StringComparison.Ordinal);

    SettingsSaved?.Invoke(this, new SettingsSavedEventArgs(requiresRestart));
    Close();
  }

  private AdBehavior ReadAdBehavior()
  {
    var selectedValue = AdBehaviorBox.SelectedValue?.ToString();
    return Enum.TryParse<AdBehavior>(selectedValue, out var result)
        ? result
        : AdBehavior.ShowAdvertisement;
  }

  private bool TryReadInt(System.Windows.Controls.TextBox box, string label, int min, int max, out int value)
  {
    if (!int.TryParse(box.Text.Trim(), out value))
    {
      SetStatus(UiText.SettingsNotANumber(label), isError: true);
      box.Focus();
      return false;
    }

    if (value < min || value > max)
    {
      SetStatus(UiText.SettingsOutOfRange(label, min, max), isError: true);
      box.Focus();
      return false;
    }

    return true;
  }

  private void SetStatus(string text, bool isError)
  {
    StatusText.Text = text;
    StatusText.Foreground = isError
        ? System.Windows.Media.Brushes.Firebrick
        : System.Windows.Media.Brushes.DarkGreen;
  }
}

public sealed class SettingsSavedEventArgs : EventArgs
{
  public SettingsSavedEventArgs(bool requiresServerRestart)
  {
    RequiresServerRestart = requiresServerRestart;
  }

  public bool RequiresServerRestart { get; }
}
