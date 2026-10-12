using System.Diagnostics;
using System.Windows;

using YTMPresence.Core;

namespace YTMPresence.TrayWpf;

public partial class OnboardingWindow : Window
{
  private readonly AppSettings _settings;
  private readonly string _settingsPath;

  public event EventHandler? OpenSettingsRequested;

  public OnboardingWindow(AppSettings settings, string settingsPath, string version)
  {
    InitializeComponent();

    _settings = settings;
    _settingsPath = settingsPath;

    SecurityTokenBox.Text = _settings.SecurityToken;
    CompanionUrlBox.Text = _settings.GetWebSocketEndpoint();
    VersionText.Text = UiText.AppVersion(version);
  }

  private void CopyTokenButton_Click(object sender, RoutedEventArgs e)
  {
    CopyToClipboard(SecurityTokenBox.Text, UiText.OnboardingTokenCopied);
  }

  private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
  {
    CopyToClipboard(CompanionUrlBox.Text, UiText.OnboardingUrlCopied);
  }

  private void OpenYtmButton_Click(object sender, RoutedEventArgs e)
    => OpenLink("https://music.youtube.com/");

  private void OpenStoreButton_Click(object sender, RoutedEventArgs e)
    => OpenLink("https://chromewebstore.google.com/detail/ytm-discord-presence-brid/obdinnbalnbmpkcaopnhnkdgdkcgligi");

  private void OpenFirefoxButton_Click(object sender, RoutedEventArgs e)
    => OpenLink("https://github.com/Kentarohakase/YTMPresence#firefox-desktop");

  private void OpenLink(string url)
  {
    try
    {
      Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    catch
    {
      SetStatus(UiText.OnboardingOpenLinkFailed, isError: true);
    }
  }

  private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
  {
    OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
  }

  private void DoneButton_Click(object sender, RoutedEventArgs e)
  {
    var previouslyCompleted = _settings.HasSeenOnboarding;
    try
    {
      _settings.HasSeenOnboarding = true;
      SettingsStore.Save(_settingsPath, _settings);
      Close();
    }
    catch
    {
      _settings.HasSeenOnboarding = previouslyCompleted;
      SetStatus(UiText.OnboardingSaveFailed, isError: true);
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
      SetStatus(UiText.OnboardingCopyFailed, isError: true);
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
