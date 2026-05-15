using System.Diagnostics;
using System.Windows;

using YTMPresence.Core;

namespace YTMPresence.TrayWpf;

public partial class OnboardingWindow : Window
{
  private readonly AppSettings _settings;
  private readonly string _settingsPath;

  public event EventHandler? OpenSettingsRequested;

  public OnboardingWindow(AppSettings settings, string settingsPath)
  {
    InitializeComponent();

    _settings = settings;
    _settingsPath = settingsPath;

    SecurityTokenBox.Text = _settings.SecurityToken;
    CompanionUrlBox.Text = _settings.GetWebSocketEndpoint();
  }

  private void CopyTokenButton_Click(object sender, RoutedEventArgs e)
  {
    CopyToClipboard(SecurityTokenBox.Text, "Token kopiert.");
  }

  private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
  {
    CopyToClipboard(CompanionUrlBox.Text, "Companion URL kopiert.");
  }

  private void OpenYtmButton_Click(object sender, RoutedEventArgs e)
  {
    try
    {
      Process.Start(new ProcessStartInfo("https://music.youtube.com/") { UseShellExecute = true });
    }
    catch
    {
      SetStatus("YouTube Music konnte nicht geöffnet werden.", isError: true);
    }
  }

  private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
  {
    OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
  }

  private void DoneButton_Click(object sender, RoutedEventArgs e)
  {
    try
    {
      _settings.HasSeenOnboarding = true;
      SettingsStore.Save(_settingsPath, _settings);
      Close();
    }
    catch
    {
      SetStatus("Status konnte nicht gespeichert werden.", isError: true);
    }
  }

  private void LaterButton_Click(object sender, RoutedEventArgs e)
  {
    Close();
  }

  private void CopyToClipboard(string value, string status)
  {
    try
    {
      System.Windows.Clipboard.SetText(value);
      SetStatus(status, isError: false);
    }
    catch
    {
      SetStatus("Kopieren fehlgeschlagen.", isError: true);
    }
  }

  private void SetStatus(string text, bool isError)
  {
    StatusText.Text = text;
    StatusText.Foreground = isError
        ? System.Windows.Media.Brushes.Firebrick
        : System.Windows.Media.Brushes.DarkGreen;
  }
}
