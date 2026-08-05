using System.Diagnostics;
using System.IO;
using System.Windows;

using YTMPresence.Core;
using YTMPresence.Core.Updates;

namespace YTMPresence.TrayWpf;

/// <summary>
/// Zeigt Fortschritt und Status des Update-Downloads an. Download, Verifikation
/// und Prüfungen laufen vollständig in <see cref="UpdateDownloadService"/>;
/// dieses Fenster startet nur noch das bereits verifizierte Setup und beendet
/// die App erst, wenn der Start tatsächlich gelungen ist.
/// </summary>
public partial class UpdateDownloadWindow : Window
{
  private readonly UpdateCheckResult _update;
  private readonly AppSettings _settings;
  private CancellationTokenSource? _downloadCts;
  private VerifiedSetupPackage? _package;
  private Task? _activeOperation;
  private bool _isBusy;
  private bool _setupStarted;

  public UpdateDownloadWindow(UpdateCheckResult update, AppSettings settings)
  {
    InitializeComponent();

    _update = update;
    _settings = settings;

    SummaryText.Text = UiText.UpdateSummary(_update.LatestVersion);
    SetupFileText.Text = _update.SetupAssetName ?? "";
    TargetPathText.Text = TemporaryUpdateWorkspace.GetDefaultRoot();

    var hasSetupDownload = HasCompleteUpdateAssets();
    InstallButton.IsEnabled = hasSetupDownload;
    DownloadOnlyButton.IsEnabled = hasSetupDownload;
    if (!hasSetupDownload)
      SetStatus(string.IsNullOrWhiteSpace(_update.ChecksumsDownloadUrl)
        ? UiText.UpdateNoChecksumsAsset
        : UiText.UpdateNoSetupAsset, isError: true);
  }

  private async void InstallButton_Click(object sender, RoutedEventArgs e)
  {
    await StartUpdateOperationAsync(startInstaller: true);
  }

  private async void DownloadOnlyButton_Click(object sender, RoutedEventArgs e)
  {
    await StartUpdateOperationAsync(startInstaller: false);
  }

  private async Task StartUpdateOperationAsync(bool startInstaller)
  {
    if (_activeOperation is not null)
      return;

    var operation = RunUpdateAsync(startInstaller);
    _activeOperation = operation;
    try
    {
      await operation;
    }
    finally
    {
      if (ReferenceEquals(_activeOperation, operation))
        _activeOperation = null;
    }
  }

  /// <summary>Behandelt alle Fehler selbst; die aufrufenden Eventhandler bleiben dadurch sicher.</summary>
  private async Task RunUpdateAsync(bool startInstaller)
  {
    if (_isBusy || _setupStarted || !HasCompleteUpdateAssets())
      return;

    _isBusy = true;
    InstallButton.IsEnabled = false;
    DownloadOnlyButton.IsEnabled = false;
    OpenReleaseButton.IsEnabled = false;
    CancelButton.Content = UiText.ButtonCancel;
    _downloadCts = new CancellationTokenSource();

    // Kein Wiederverwenden eines früheren Downloads: jeder Versuch lädt und
    // verifiziert neu in einem eigenen Verzeichnis.
    DisposePackage();

    try
    {
      SetStatus(UiText.UpdateStatusPreparing, isError: false);
      DownloadProgress.IsIndeterminate = true;
      DownloadProgress.Value = 0;

      var options = new UpdateDownloadOptions
      {
        UrlValidator = UpdateUrlValidator.ForSettings(_settings)
      };

      VerifiedSetupPackage package;
      using (var service = new UpdateDownloadService(options))
      {
        var progress = new Progress<UpdateDownloadProgress>(UpdateProgressDisplay);
        package = await service.DownloadAndVerifyAsync(_update, progress, _downloadCts.Token);
      }

      _package = package;
      DownloadProgress.IsIndeterminate = false;
      DownloadProgress.Value = 100;
      ProgressText.Text = UiText.UpdateProgressComplete(FormatBytes(package.Length));
      TargetPathText.Text = package.FilePath;
      SetupFileText.Text = package.FileName;

      if (!startInstaller)
      {
        SetStatus(UiText.UpdateStatusDownloadedOnly(package.FilePath), isError: false);
        ResetButtons();
        return;
      }

      SetStatus(UiText.UpdateStatusStartingSetup, isError: false);

      // Erneute Prüfung (Existenz, Name, Größe, Hash) direkt vor dem Start;
      // die Datei bleibt während des Starts gegen Veränderung gesperrt.
      await UpdateInstaller.LaunchVerifiedSetupAsync(package, StartSetupProcess, _downloadCts.Token);

      _setupStarted = true;
      SetStatus(UiText.UpdateStatusSetupStarted, isError: false);
      CancelButton.Content = UiText.ButtonClose;

      // Kurz anzeigen lassen, dann die App regulär beenden: Der Installer
      // muss die laufende Instanz dann nicht mehr beenden.
      await Task.Delay(TimeSpan.FromSeconds(1.5));
      System.Windows.Application.Current.Shutdown();
    }
    catch (OperationCanceledException)
    {
      DownloadProgress.IsIndeterminate = false;
      SetStatus(UiText.UpdateStatusCancelled, isError: true);
      ResetButtons();
    }
    catch (Exception ex) when (ex is UpdateSecurityException or UpdateDownloadException)
    {
      Logger.Error(ex, "Update download or verification failed.");
      DownloadProgress.IsIndeterminate = false;
      SetStatus(UiText.UpdateStatusFailed(ex.Message), isError: true);
      ResetButtons();
    }
    catch (Exception ex)
    {
      Logger.Error(ex, "Unexpected update failure.");
      DownloadProgress.IsIndeterminate = false;
      SetStatus(UiText.UpdateStatusFailed(ex.Message), isError: true);
      ResetButtons();
    }
    finally
    {
      _isBusy = false;
      _downloadCts?.Dispose();
      _downloadCts = null;
    }
  }

  /// <summary>
  /// Startet ausschließlich den übergebenen, bereits verifizierten absoluten Pfad.
  /// Keine Argumente, keine Shell-Interpolation, kein cmd/PowerShell.
  /// </summary>
  private static bool StartSetupProcess(string setupPath)
  {
    var startInfo = new ProcessStartInfo
    {
      FileName = setupPath,
      WorkingDirectory = Path.GetDirectoryName(setupPath) ?? Path.GetTempPath(),
      // ShellExecute ist für den Installer-Start nötig (u. a. für eine mögliche
      // UAC-Abfrage); gestartet wird nur der verifizierte absolute Pfad.
      UseShellExecute = true
    };

    using var process = Process.Start(startInfo);
    if (process is null)
    {
      Logger.Warn($"Setup launch returned no process handle: {setupPath}");
      return false;
    }

    Logger.Info($"Started update setup process {process.Id}: {setupPath}");
    return true;
  }

  private void OpenReleaseButton_Click(object sender, RoutedEventArgs e)
  {
    try
    {
      Process.Start(new ProcessStartInfo(_update.ReleaseUrl) { UseShellExecute = true });
    }
    catch (Exception ex)
    {
      Logger.Error(ex, $"Error opening release URL: {_update.ReleaseUrl}");
      SetStatus(UiText.UpdateOpenReleaseFailed, isError: true);
    }
  }

  private void CancelButton_Click(object sender, RoutedEventArgs e)
  {
    if (_isBusy && _downloadCts is { IsCancellationRequested: false })
    {
      _downloadCts.Cancel();
      return;
    }

    Close();
  }

  protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
  {
    if (_isBusy && _downloadCts is { IsCancellationRequested: false } && !_setupStarted)
      _downloadCts.Cancel();

    base.OnClosing(e);
  }

  protected override void OnClosed(EventArgs e)
  {
    // Bei laufender I/O übernimmt CancelAndCleanupAsync nach dem Abbruch das
    // Aufräumen; ein synchrones Löschen könnte sonst mit dem offenen Stream
    // kollidieren und ein verwaistes Verzeichnis hinterlassen.
    if (!_isBusy)
      DisposePackage();
    base.OnClosed(e);
  }

  public async Task CancelAndCleanupAsync()
  {
    // Shutdown() wird nach erfolgreichem Start innerhalb von RunUpdateAsync
    // aufgerufen. Auf genau diese Task zu warten würde den Shutdown zyklisch
    // blockieren; der Launch-Lock ist zu diesem Zeitpunkt bereits freigegeben.
    if (_setupStarted)
    {
      DisposePackage();
      return;
    }

    if (_downloadCts is { IsCancellationRequested: false })
      _downloadCts.Cancel();

    var operation = _activeOperation;
    if (operation is not null)
    {
      try
      {
        await operation;
      }
      catch (Exception ex)
      {
        Logger.Warn($"Update operation did not finish cleanly during shutdown: {ex.Message}");
      }
    }

    DisposePackage();
  }

  private void DisposePackage()
  {
    try
    {
      _package?.Dispose();
    }
    catch (Exception ex)
    {
      Logger.Warn($"Update package cleanup failed: {ex.Message}");
    }

    _package = null;
  }

  private void UpdateProgressDisplay(UpdateDownloadProgress progress)
  {
    DownloadProgress.IsIndeterminate = progress.TotalBytes is null;

    if (progress.TotalBytes is > 0)
    {
      var percent = Math.Clamp(progress.BytesDownloaded * 100.0 / progress.TotalBytes.Value, 0, 100);
      DownloadProgress.Value = percent;
      ProgressText.Text = UiText.UpdateProgressWithTotal(
        percent, FormatBytes(progress.BytesDownloaded), FormatBytes(progress.TotalBytes.Value));
      return;
    }

    ProgressText.Text = UiText.UpdateProgressPlain(FormatBytes(progress.BytesDownloaded));
  }

  private static string FormatBytes(long bytes)
  {
    string[] units = ["B", "KB", "MB", "GB"];
    var value = (double)bytes;
    var unit = 0;

    while (value >= 1024 && unit < units.Length - 1)
    {
      value /= 1024;
      unit++;
    }

    return $"{value:0.##} {units[unit]}";
  }

  private void ResetButtons()
  {
    var hasSetupDownload = HasCompleteUpdateAssets();
    InstallButton.IsEnabled = hasSetupDownload && !_setupStarted;
    DownloadOnlyButton.IsEnabled = hasSetupDownload && !_setupStarted;
    OpenReleaseButton.IsEnabled = true;
    CancelButton.Content = UiText.ButtonClose;
  }

  private bool HasCompleteUpdateAssets()
    => !string.IsNullOrWhiteSpace(_update.SetupDownloadUrl) &&
       !string.IsNullOrWhiteSpace(_update.ChecksumsDownloadUrl);

  private void SetStatus(string text, bool isError)
  {
    StatusText.Text = text;
    StatusText.Foreground = isError
        ? System.Windows.Media.Brushes.Firebrick
        : System.Windows.Media.Brushes.DarkGreen;
  }
}
