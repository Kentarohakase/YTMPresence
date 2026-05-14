using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using YTMPresence.Core;

namespace YTMPresence.TrayWpf;

public partial class PlayerWindow : Window
{
  private readonly Func<CompanionStatus?> _getStatus;
  private readonly Func<string, Task<bool>> _sendCommand;
  private readonly AppSettings _settings;
  private readonly string _settingsPath;
  private readonly DispatcherTimer _timer;

  private string? _currentCoverUrl;
  private string? _currentTrackUrl;
  private bool _isInitializing;
  private bool _isSendingCommand;

  public PlayerWindow(
      Func<CompanionStatus?> getStatus,
      Func<string, Task<bool>> sendCommand,
      AppSettings settings,
      string settingsPath)
  {
    InitializeComponent();

    _getStatus = getStatus;
    _sendCommand = sendCommand;
    _settings = settings;
    _settingsPath = settingsPath;
    _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, __) => UpdateStatus(), Dispatcher);

    Loaded += (_, __) =>
    {
      ApplySavedWindowState();
      UpdateStatus();
      _timer.Start();
    };

    Closing += (_, __) => SaveWindowState();
    Closed += (_, __) => _timer.Stop();
  }

  private void UpdateStatus()
  {
    var status = _getStatus();
    if (status is null || string.IsNullOrWhiteSpace(status.LastTitle))
    {
      ShowEmpty(status);
      return;
    }

    TitleText.Text = status.LastTitle;
    ArtistText.Text = string.IsNullOrWhiteSpace(status.LastArtist) ? "YouTube Music" : status.LastArtist;
    AlbumText.Text = string.IsNullOrWhiteSpace(status.LastAlbum) ? "" : status.LastAlbum;

    var position = EstimatePosition(status);
    var duration = NormalizeDuration(status.LastDurationSeconds);

    Progress.Maximum = duration > 0 ? duration : 1;
    Progress.Value = duration > 0 ? Math.Clamp(position, 0, duration) : 0;

    PositionText.Text = FormatTime(position);
    DurationText.Text = duration > 0 ? FormatTime(duration) : "--:--";
    PlayStateText.Text = status.LastIsPlaying == true ? "Laeuft" : "Pausiert";
    PlayPauseButton.Content = status.LastIsPlaying == true ? "Pause" : "Play";

    _currentTrackUrl = status.LastTrackUrl;
    OpenTrackButton.IsEnabled = !string.IsNullOrWhiteSpace(_currentTrackUrl);
    SetCommandButtonsEnabled(status.ConnectedClients > 0);

    SetCover(status.LastAlbumArtUrl);
  }

  private void ShowEmpty(CompanionStatus? status)
  {
    TitleText.Text = "Kein Track";
    ArtistText.Text = "Warte auf YouTube Music";
    AlbumText.Text = "";
    Progress.Maximum = 1;
    Progress.Value = 0;
    PositionText.Text = "0:00";
    DurationText.Text = "--:--";
    PlayStateText.Text = "Bereit";
    PlayPauseButton.Content = "Play";
    OpenTrackButton.IsEnabled = false;
    SetCommandButtonsEnabled(status?.ConnectedClients > 0);
    _currentTrackUrl = null;
    SetCover(null);
  }

  private void SetCommandButtonsEnabled(bool isEnabled)
  {
    var effectiveEnabled = isEnabled && !_isSendingCommand;
    PreviousButton.IsEnabled = effectiveEnabled;
    PlayPauseButton.IsEnabled = effectiveEnabled;
    NextButton.IsEnabled = effectiveEnabled;

    if (!isEnabled)
      CommandStatusText.Text = "Keine Extension verbunden";
    else if (!_isSendingCommand && CommandStatusText.Text == "Keine Extension verbunden")
      CommandStatusText.Text = "";
  }

  private void SetCover(string? coverUrl)
  {
    if (string.Equals(_currentCoverUrl, coverUrl, StringComparison.Ordinal))
      return;

    _currentCoverUrl = coverUrl;

    if (string.IsNullOrWhiteSpace(coverUrl))
    {
      CoverImage.Source = null;
      CoverFallbackText.Visibility = Visibility.Visible;
      return;
    }

    try
    {
      var image = new BitmapImage();
      image.BeginInit();
      image.UriSource = new Uri(coverUrl, UriKind.Absolute);
      image.CacheOption = BitmapCacheOption.OnLoad;
      image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
      image.EndInit();

      CoverImage.Source = image;
      CoverFallbackText.Visibility = Visibility.Collapsed;
    }
    catch
    {
      CoverImage.Source = null;
      CoverFallbackText.Visibility = Visibility.Visible;
    }
  }

  private void ApplySavedWindowState()
  {
    _isInitializing = true;

    Width = _settings.PlayerWindow.Width;
    Height = _settings.PlayerWindow.Height;

    if (_settings.PlayerWindow.Left is double left && _settings.PlayerWindow.Top is double top)
    {
      Left = left;
      Top = top;
    }
    else
    {
      WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    Topmost = _settings.PlayerWindow.AlwaysOnTop;
    TopMostBox.IsChecked = Topmost;

    _isInitializing = false;
  }

  private void SaveWindowState()
  {
    if (WindowState == WindowState.Normal)
    {
      _settings.PlayerWindow.Left = Left;
      _settings.PlayerWindow.Top = Top;
      _settings.PlayerWindow.Width = Width;
      _settings.PlayerWindow.Height = Height;
    }

    _settings.PlayerWindow.AlwaysOnTop = Topmost;

    try
    {
      SettingsStore.Save(_settingsPath, _settings);
    }
    catch
    {
    }
  }

  private static double EstimatePosition(CompanionStatus status)
  {
    var position = status.LastPositionSeconds ?? 0;
    if (status.LastIsPlaying == true && status.LastMessageUtc is not null)
      position += (DateTimeOffset.UtcNow - status.LastMessageUtc.Value).TotalSeconds;

    var duration = NormalizeDuration(status.LastDurationSeconds);
    return duration > 0 ? Math.Clamp(position, 0, duration) : Math.Max(0, position);
  }

  private static double NormalizeDuration(double? value)
  {
    if (value is not double d || !double.IsFinite(d) || d <= 0)
      return 0;

    return d;
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

  private void OpenTrackButton_Click(object sender, RoutedEventArgs e)
  {
    if (string.IsNullOrWhiteSpace(_currentTrackUrl))
      return;

    try
    {
      Process.Start(new ProcessStartInfo(_currentTrackUrl) { UseShellExecute = true });
    }
    catch
    {
    }
  }

  private async Task SendCommandAsync(string command, string label)
  {
    if (_isSendingCommand)
      return;

    _isSendingCommand = true;
    SetCommandButtonsEnabled(isEnabled: false);
    CommandStatusText.Text = $"{label}...";

    try
    {
      var sent = await _sendCommand(command);
      CommandStatusText.Text = sent ? $"{label} gesendet" : "Keine Extension verbunden";
    }
    catch
    {
      CommandStatusText.Text = $"{label} fehlgeschlagen";
    }
    finally
    {
      _isSendingCommand = false;
      var status = _getStatus();
      SetCommandButtonsEnabled(status?.ConnectedClients > 0);
    }
  }

  private async void PreviousButton_Click(object sender, RoutedEventArgs e)
  {
    await SendCommandAsync("previous", "Zurueck");
  }

  private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
  {
    await SendCommandAsync("play-pause", "Play/Pause");
  }

  private async void NextButton_Click(object sender, RoutedEventArgs e)
  {
    await SendCommandAsync("next", "Weiter");
  }

  private void TopMostBox_Changed(object sender, RoutedEventArgs e)
  {
    if (_isInitializing)
      return;

    Topmost = TopMostBox.IsChecked == true;
    _settings.PlayerWindow.AlwaysOnTop = Topmost;

    try
    {
      SettingsStore.Save(_settingsPath, _settings);
    }
    catch
    {
    }
  }
}
