using Microsoft.Win32;

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

using YTMPresence.Core;

using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;
using ToolStripSeparator = System.Windows.Forms.ToolStripSeparator;
using ToolTipIcon = System.Windows.Forms.ToolTipIcon;

namespace YTMPresence.TrayWpf;

public partial class App : System.Windows.Application
{
  private const string RunRegistryValueName = "YTM Presence";

  private NotifyIcon? _trayIcon;

  private ToolStripMenuItem? _serverStatusItem;
  private ToolStripMenuItem? _extensionStatusItem;
  private ToolStripMenuItem? _discordStatusItem;
  private ToolStripMenuItem? _lastTrackItem;
  private ToolStripMenuItem? _securityItem;

  private ToolStripMenuItem? _autostartItem;
  private ToolStripMenuItem? _onlyShowWhenPlayingItem;
  private ToolStripMenuItem? _ignoreAdsItem;
  private SettingsWindow? _settingsWindow;
  private PlayerWindow? _playerWindow;

  private readonly string _settingsPath = SettingsStore.GetDefaultSettingsPath();
  private AppSettings _settings = new();
  private CompanionServer? _server;

  private DispatcherTimer? _statusTimer;

  private Mutex? _singleInstanceMutex;
  private bool _ownsSingleInstanceMutex;

  protected override async void OnStartup(StartupEventArgs e)
  {
    base.OnStartup(e);

    Logger.Initialize();
    Logger.Info("App OnStartup called.");

    DispatcherUnhandledException += App_DispatcherUnhandledException;

    if (!TryAcquireSingleInstanceMutex())
    {
      Logger.Warn("Second instance was prevented.");

      System.Windows.MessageBox.Show(
          UiText.AlreadyRunningMessage,
          UiText.AlreadyRunningTitle,
          MessageBoxButton.OK,
          MessageBoxImage.Information);

      Shutdown();
      return;
    }

    try
    {
      _settings = SettingsStore.LoadOrCreateDefault(_settingsPath);
      SettingsStore.Save(_settingsPath, _settings);

      CreateTray();

      _server = new CompanionServer(_settings);
      await StartServerSafeAsync();

      _statusTimer = new DispatcherTimer(
          TimeSpan.FromSeconds(1),
          DispatcherPriority.Background,
          (_, __) => UpdateTrayStatus(),
          Dispatcher);

      _statusTimer.Start();

      Logger.Info("App started successfully.");
    }
    catch (Exception ex)
    {
      Logger.Error(ex, "Error while starting app.");

      System.Windows.MessageBox.Show(
          UiText.StartupErrorMessage,
          UiText.StartupErrorTitle,
          MessageBoxButton.OK,
          MessageBoxImage.Error);

      Shutdown();
    }
  }

  protected override async void OnExit(ExitEventArgs e)
  {
    Logger.Info("App is shutting down.");

    try { _statusTimer?.Stop(); } catch { }
    try { _trayIcon?.Dispose(); } catch { }

    if (_server is not null)
    {
      try { await _server.DisposeAsync(); }
      catch (Exception ex) { Logger.Error(ex, "Error while disposing server."); }
    }

    ReleaseSingleInstanceMutex();

    base.OnExit(e);
  }

  private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
  {
    Logger.Error(e.Exception, "Unhandled UI exception.");
    e.Handled = true;

    try
    {
      _trayIcon?.ShowBalloonTip(
          4000,
          UiText.UnexpectedErrorTitle,
          UiText.UnexpectedErrorMessage,
          ToolTipIcon.Error);
    }
    catch
    {
    }
  }

  private bool TryAcquireSingleInstanceMutex()
  {
    try
    {
      var sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
      var safeSid = string.Concat(sid.Select(c => char.IsLetterOrDigit(c) ? c : '_'));

      var mutexName = $@"Local\YTMPresence_{safeSid}";
      _singleInstanceMutex = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out var createdNew);
      _ownsSingleInstanceMutex = createdNew;

      return createdNew;
    }
    catch (Exception ex)
    {
      Logger.Error(ex, "Error creating single instance mutex.");
      return true;
    }
  }

  private void ReleaseSingleInstanceMutex()
  {
    try
    {
      if (_singleInstanceMutex is not null)
      {
        if (_ownsSingleInstanceMutex)
          _singleInstanceMutex.ReleaseMutex();

        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
        _ownsSingleInstanceMutex = false;
      }
    }
    catch (Exception ex)
    {
      Logger.Error(ex, "Error releasing single instance mutex.");
    }
  }

  private void CreateTray()
  {
    _serverStatusItem = new ToolStripMenuItem(UiText.ServerStarting) { Enabled = false };
    _extensionStatusItem = new ToolStripMenuItem(UiText.ExtensionWaiting) { Enabled = false };
    _discordStatusItem = new ToolStripMenuItem(UiText.DiscordUnknown) { Enabled = false };

    _lastTrackItem = new ToolStripMenuItem(UiText.TrackNone) { Enabled = false };
    _securityItem = new ToolStripMenuItem(UiText.SecurityNone) { Enabled = false };

    _onlyShowWhenPlayingItem = new ToolStripMenuItem(UiText.OnlyShowWhenPlaying)
    {
      CheckOnClick = true,
      Checked = _settings.OnlyShowWhenPlaying
    };
    _onlyShowWhenPlayingItem.Click += (_, __) => SavePresenceOptions();

    _ignoreAdsItem = new ToolStripMenuItem(UiText.IgnoreAds)
    {
      CheckOnClick = true,
      Checked = _settings.AdBehavior == AdBehavior.Ignore
    };
    _ignoreAdsItem.Click += (_, __) => SavePresenceOptions();

    _autostartItem = new ToolStripMenuItem(UiText.AutostartEnable)
    {
      CheckOnClick = true,
      Checked = IsAutoStartEnabled()
    };
    _autostartItem.Click += (_, __) =>
    {
      try
      {
        SetAutoStart(_autostartItem.Checked);
        Logger.Info($"Autostart set to {_autostartItem.Checked}.");
      }
      catch (Exception ex)
      {
        Logger.Error(ex, "Error toggling autostart.");
        _autostartItem.Checked = IsAutoStartEnabled();
      }
    };

    var copyTokenItem = new ToolStripMenuItem(UiText.CopyToken);
    copyTokenItem.Click += (_, __) => CopyTokenToClipboard(showBalloon: true);

    var rotateTokenItem = new ToolStripMenuItem(UiText.GenerateNewToken);
    rotateTokenItem.Click += (_, __) => RotateToken();

    var settingsItem = new ToolStripMenuItem(UiText.Settings);
    settingsItem.Click += (_, __) => ShowSettingsWindow();

    var playerItem = new ToolStripMenuItem(UiText.OpenPlayer);
    playerItem.Click += (_, __) => ShowPlayerWindow();

    var openYtmItem = new ToolStripMenuItem(UiText.OpenYtm);
    openYtmItem.Click += (_, __) => OpenUrl("https://music.youtube.com/");

    var openLogItem = new ToolStripMenuItem(UiText.OpenLog);
    openLogItem.Click += (_, __) => OpenPath(Logger.GetCurrentLogFilePath());

    var openLogFolderItem = new ToolStripMenuItem(UiText.OpenLogFolder);
    openLogFolderItem.Click += (_, __) => OpenPath(Logger.GetLogDirectoryPath());

    var exitItem = new ToolStripMenuItem(UiText.Exit);
    exitItem.Click += (_, __) => Shutdown();

    _trayIcon = new NotifyIcon
    {
      Visible = true,
      Text = $"{UiText.AppName} {GetAppVersion()}",
      Icon = LoadTrayIcon() ?? System.Drawing.SystemIcons.Application
    };

    var versionItem = new ToolStripMenuItem(UiText.AppVersion(GetAppVersion())) { Enabled = false };

    var menu = new ContextMenuStrip();
    menu.Items.Add(versionItem);
    menu.Items.Add(new ToolStripSeparator());
    menu.Items.Add(_serverStatusItem);
    menu.Items.Add(_extensionStatusItem);
    menu.Items.Add(_discordStatusItem);
    menu.Items.Add(new ToolStripSeparator());
    menu.Items.Add(_lastTrackItem);
    menu.Items.Add(_securityItem);
    menu.Items.Add(new ToolStripSeparator());
    menu.Items.Add(_onlyShowWhenPlayingItem);
    menu.Items.Add(_ignoreAdsItem);
    menu.Items.Add(new ToolStripSeparator());
    menu.Items.Add(_autostartItem);
    menu.Items.Add(playerItem);
    menu.Items.Add(settingsItem);
    menu.Items.Add(copyTokenItem);
    menu.Items.Add(rotateTokenItem);
    menu.Items.Add(openYtmItem);
    menu.Items.Add(openLogItem);
    menu.Items.Add(openLogFolderItem);
    menu.Items.Add(new ToolStripSeparator());
    menu.Items.Add(exitItem);

    _trayIcon.ContextMenuStrip = menu;
  }

  private static System.Drawing.Icon? LoadTrayIcon()
  {
    try
    {
      var assembly = Assembly.GetExecutingAssembly();
      const string resourceName = "YTMPresence.TrayWpf.Assets.app.ico";

      using var stream = assembly.GetManifestResourceStream(resourceName);
      if (stream is null)
        return null;

      return new System.Drawing.Icon(stream);
    }
    catch
    {
      return null;
    }
  }

  private async Task StartServerSafeAsync()
  {
    try
    {
      await _server!.StartAsync();
      Logger.Info("Server started successfully.");
    }
    catch (Exception ex)
    {
      Logger.Error(ex, "Error while starting server.");
    }
  }

  private void UpdateTrayStatus()
  {
    if (_server is null)
      return;

    var status = _server.GetStatusSnapshot();

    _serverStatusItem!.Text = status.IsRunning
        ? UiText.ServerRunning(_settings.ListenHost, _settings.ListenPort, _settings.WebSocketPath)
        : UiText.ServerStopped;

    var clientCount = status.ConnectedClients;
    var lastMsgAge = status.LastMessageUtc is null
        ? UiText.JustNow
        : AgeText(DateTimeOffset.UtcNow - status.LastMessageUtc.Value);

    _extensionStatusItem!.Text = clientCount > 0
        ? UiText.ExtensionConnected(clientCount, lastMsgAge)
        : UiText.ExtensionDisconnected(lastMsgAge);

    if (status.DiscordOk)
    {
      var okAge = status.LastDiscordOkUtc is null
          ? ""
          : " · " + AgeText(DateTimeOffset.UtcNow - status.LastDiscordOkUtc.Value);

      _discordStatusItem!.Text = UiText.DiscordOk(okAge);
    }
    else
    {
      var err = string.IsNullOrWhiteSpace(status.LastDiscordError)
          ? "unknown error"
          : status.LastDiscordError;

      _discordStatusItem!.Text = UiText.DiscordError(Truncate(err, 60));
    }

    if (!string.IsNullOrWhiteSpace(status.LastTitle) && !string.IsNullOrWhiteSpace(status.LastArtist))
    {
      _lastTrackItem!.Text = UiText.TrackInfo(
          status.LastIsPlaying == true,
          Truncate($"{status.LastTitle} – {status.LastArtist}", 90));
    }
    else
    {
      _lastTrackItem!.Text = UiText.TrackNone;
    }

    if (status.UnauthorizedMessages > 0)
    {
      var age = status.LastUnauthorizedUtc is null
          ? ""
          : " · " + AgeText(DateTimeOffset.UtcNow - status.LastUnauthorizedUtc.Value);

      _securityItem!.Text = UiText.SecurityInvalid(status.UnauthorizedMessages, age);
    }
    else
    {
      _securityItem!.Text = UiText.SecurityOk;
    }

    if (_trayIcon is not null)
    {
      var mode = clientCount > 0 ? UiText.TooltipActive : UiText.TooltipWaiting;
      _trayIcon.Text = Truncate($"{UiText.AppName} {GetAppVersion()} – {mode}", 63);
    }
  }

  private void SavePresenceOptions()
  {
    try
    {
      _settings.OnlyShowWhenPlaying = _onlyShowWhenPlayingItem?.Checked == true;
      _settings.AdBehavior = _ignoreAdsItem?.Checked == true
          ? AdBehavior.Ignore
          : AdBehavior.ShowAdvertisement;

      SettingsStore.Save(_settingsPath, _settings);
      Logger.Info(
          $"Presence options updated. OnlyShowWhenPlaying={_settings.OnlyShowWhenPlaying}, " +
          $"AdBehavior={_settings.AdBehavior}.");
    }
    catch (Exception ex)
    {
      Logger.Error(ex, "Error saving presence options.");

      if (_onlyShowWhenPlayingItem is not null)
        _onlyShowWhenPlayingItem.Checked = _settings.OnlyShowWhenPlaying;

      if (_ignoreAdsItem is not null)
        _ignoreAdsItem.Checked = _settings.AdBehavior == AdBehavior.Ignore;
    }
  }

  private void ShowSettingsWindow()
  {
    if (_settingsWindow is not null)
    {
      _settingsWindow.Activate();
      return;
    }

    _settingsWindow = new SettingsWindow(_settings, _settingsPath);
    _settingsWindow.Closed += (_, __) => _settingsWindow = null;
    _settingsWindow.SettingsSaved += (_, args) =>
    {
      SyncPresenceMenuItems();

      if (args.RequiresServerRestart)
        _ = RestartServerAsync();

      Logger.Info($"Settings window saved. Restart required: {args.RequiresServerRestart}.");
    };

    _settingsWindow.Show();
    _settingsWindow.Activate();
  }

  private void ShowPlayerWindow()
  {
    if (_playerWindow is not null)
    {
      _playerWindow.Activate();
      return;
    }

    _playerWindow = new PlayerWindow(() => _server?.GetStatusSnapshot());
    _playerWindow.Closed += (_, __) => _playerWindow = null;
    _playerWindow.Show();
    _playerWindow.Activate();
  }

  private void SyncPresenceMenuItems()
  {
    if (_onlyShowWhenPlayingItem is not null)
      _onlyShowWhenPlayingItem.Checked = _settings.OnlyShowWhenPlaying;

    if (_ignoreAdsItem is not null)
      _ignoreAdsItem.Checked = _settings.AdBehavior == AdBehavior.Ignore;
  }

  private void RotateToken()
  {
    try
    {
      _settings.SecurityToken = SecurityTokenHelper.GenerateSecureToken();
      SettingsStore.Save(_settingsPath, _settings);

      System.Windows.Clipboard.SetText(_settings.SecurityToken);

      _ = RestartServerAsync();

      Logger.Info("Security token regenerated.");

      _trayIcon?.ShowBalloonTip(
          4000,
          UiText.TokenRenewedTitle,
          UiText.TokenRenewedMessage,
          ToolTipIcon.Info);
    }
    catch (Exception ex)
    {
      Logger.Error(ex, "Error regenerating token.");

      _trayIcon?.ShowBalloonTip(
          4000,
          UiText.ErrorTitle,
          UiText.TokenRenewErrorMessage,
          ToolTipIcon.Error);
    }
  }

  private void CopyTokenToClipboard(bool showBalloon)
  {
    try
    {
      System.Windows.Clipboard.SetText(_settings.SecurityToken ?? "");
      Logger.Info("Security token copied to clipboard.");

      if (showBalloon)
      {
        _trayIcon?.ShowBalloonTip(
            2500,
            UiText.TokenCopyTitle,
            UiText.TokenCopyMessage,
            ToolTipIcon.Info);
      }
    }
    catch (Exception ex)
    {
      Logger.Error(ex, "Error copying token.");
    }
  }

  private async Task RestartServerAsync()
  {
    try
    {
      Logger.Info("Server restarting.");

      if (_server is not null)
        await _server.StopAsync(CancellationToken.None);
    }
    catch (Exception ex)
    {
      Logger.Error(ex, "Error stopping server before restart.");
    }

    _server = new CompanionServer(_settings);
    await StartServerSafeAsync();
  }

  private static string AgeText(TimeSpan age)
  {
    if (age < TimeSpan.FromSeconds(5)) return UiText.JustNow;
    if (age < TimeSpan.FromMinutes(1)) return UiText.SecondsAgo((int)age.TotalSeconds);
    if (age < TimeSpan.FromHours(1)) return UiText.MinutesAgo((int)age.TotalMinutes);
    return UiText.HoursAgo((int)age.TotalHours);
  }

  private static string Truncate(string text, int max)
      => text.Length <= max ? text : text[..max];

  private static string GetAppVersion()
  {
    var assembly = Assembly.GetExecutingAssembly();
    var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    return string.IsNullOrWhiteSpace(version) ? "dev" : version;
  }

  private static void OpenUrl(string url)
  {
    try
    {
      Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    catch (Exception ex)
    {
      Logger.Error(ex, $"Error opening URL: {url}");
    }
  }

  private static void OpenPath(string path)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(path))
        return;

      if (Directory.Exists(path) || File.Exists(path))
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
    catch (Exception ex)
    {
      Logger.Error(ex, $"Error opening path: {path}");
    }
  }

  private static bool IsAutoStartEnabled()
  {
    const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    using var key = Registry.CurrentUser.OpenSubKey(runKey, writable: false);
    return key?.GetValue(RunRegistryValueName) is string s && !string.IsNullOrWhiteSpace(s);
  }

  private static void SetAutoStart(bool enable)
  {
    const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    using var key = Registry.CurrentUser.CreateSubKey(runKey);

    if (!enable)
    {
      key.DeleteValue(RunRegistryValueName, throwOnMissingValue: false);
      return;
    }

    var exePath = Environment.ProcessPath ?? throw new InvalidOperationException("ProcessPath not available.");
    key.SetValue(RunRegistryValueName, $"\"{exePath}\"");
  }
}
