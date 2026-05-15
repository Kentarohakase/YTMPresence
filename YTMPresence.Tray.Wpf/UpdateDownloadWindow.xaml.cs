using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;

using YTMPresence.Core;

namespace YTMPresence.TrayWpf;

public partial class UpdateDownloadWindow : Window
{
  private readonly UpdateCheckResult _update;
  private readonly string _setupFileName;
  private readonly string _targetPath;
  private CancellationTokenSource? _downloadCts;
  private bool _isInstalling;

  public UpdateDownloadWindow(UpdateCheckResult update)
  {
    InitializeComponent();

    _update = update;
    _setupFileName = GetSetupFileName(update);
    _targetPath = Path.Combine(Path.GetTempPath(), "YTMPresence", "updates", update.LatestVersion, _setupFileName);

    SummaryText.Text = $"YTMPresence {_update.LatestVersion} ist verfügbar. Das Setup wird heruntergeladen, geprüft und anschließend gestartet.";
    SetupFileText.Text = _setupFileName;
    TargetPathText.Text = _targetPath;

    InstallButton.IsEnabled = !string.IsNullOrWhiteSpace(_update.SetupDownloadUrl);
    if (!InstallButton.IsEnabled)
      SetStatus("Dieses Release enthält kein Setup-Asset. Öffne stattdessen die Release-Seite.", isError: true);
  }

  private async void InstallButton_Click(object sender, RoutedEventArgs e)
  {
    await DownloadAndVerifyAsync(startInstaller: true);
  }

  private async void DownloadOnlyButton_Click(object sender, RoutedEventArgs e)
  {
    await DownloadAndVerifyAsync(startInstaller: false);
  }

  private async Task DownloadAndVerifyAsync(bool startInstaller)
  {
    if (string.IsNullOrWhiteSpace(_update.SetupDownloadUrl))
      return;

    InstallButton.IsEnabled = false;
    DownloadOnlyButton.IsEnabled = false;
    OpenReleaseButton.IsEnabled = false;
    CancelButton.Content = "Abbrechen";
    _downloadCts = new CancellationTokenSource();

    try
    {
      SetStatus("Download wird vorbereitet...", isError: false);
      Directory.CreateDirectory(Path.GetDirectoryName(_targetPath)!);

      if (File.Exists(_targetPath))
        File.Delete(_targetPath);

      await DownloadFileAsync(_update.SetupDownloadUrl, _targetPath, _downloadCts.Token);
      await VerifyChecksumIfAvailableAsync(_targetPath, _downloadCts.Token);

      if (!startInstaller)
      {
        SetStatus($"Setup wurde heruntergeladen und geprüft: {_targetPath}", isError: false);
        ResetButtons();
        return;
      }

      SetStatus("Setup wird gestartet. YTM Presence beendet sich gleich.", isError: false);
      Logger.Info($"Starting downloaded update setup: {_targetPath}");
      Process.Start(new ProcessStartInfo(_targetPath) { UseShellExecute = true });

      _isInstalling = true;
      _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(
        (Action)(() => System.Windows.Application.Current.Shutdown()));
      Close();
    }
    catch (OperationCanceledException)
    {
      SetStatus("Download abgebrochen.", isError: true);
      ResetButtons();
    }
    catch (Exception ex)
    {
      Logger.Error(ex, "Update download or install failed.");
      SetStatus($"Update konnte nicht installiert werden: {ex.Message}", isError: true);
      ResetButtons();
    }
    finally
    {
      _downloadCts?.Dispose();
      _downloadCts = null;
    }
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
      SetStatus("Release-Seite konnte nicht geöffnet werden.", isError: true);
    }
  }

  private void CancelButton_Click(object sender, RoutedEventArgs e)
  {
    if (_downloadCts is not null)
    {
      _downloadCts.Cancel();
      return;
    }

    Close();
  }

  protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
  {
    if (_downloadCts is not null && !_downloadCts.IsCancellationRequested && !_isInstalling)
      _downloadCts.Cancel();

    base.OnClosing(e);
  }

  private async Task DownloadFileAsync(string url, string targetPath, CancellationToken ct)
  {
    using var http = CreateHttpClient();
    using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
    response.EnsureSuccessStatusCode();

    var totalBytes = response.Content.Headers.ContentLength;
    DownloadProgress.IsIndeterminate = totalBytes is null;
    DownloadProgress.Value = 0;

    await using var source = await response.Content.ReadAsStreamAsync(ct);
    await using var destination = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

    var buffer = new byte[128 * 1024];
    long downloadedBytes = 0;

    while (true)
    {
      var read = await source.ReadAsync(buffer, ct);
      if (read == 0)
        break;

      await destination.WriteAsync(buffer.AsMemory(0, read), ct);
      downloadedBytes += read;
      UpdateProgress(downloadedBytes, totalBytes);
    }

    DownloadProgress.IsIndeterminate = false;
    DownloadProgress.Value = 100;
    ProgressText.Text = $"Download abgeschlossen: {FormatBytes(downloadedBytes)}";
  }

  private async Task VerifyChecksumIfAvailableAsync(string setupPath, CancellationToken ct)
  {
    if (string.IsNullOrWhiteSpace(_update.ChecksumsDownloadUrl))
    {
      SetStatus("Keine SHA256SUMS.txt gefunden. Setup wird ohne Prüfsummenvergleich gestartet.", isError: false);
      return;
    }

    SetStatus("SHA256-Prüfsumme wird geprüft...", isError: false);
    using var http = CreateHttpClient();
    var checksumsText = await http.GetStringAsync(_update.ChecksumsDownloadUrl, ct);
    var expectedHash = FindExpectedHash(checksumsText, _setupFileName);

    if (string.IsNullOrWhiteSpace(expectedHash))
    {
      SetStatus("Keine passende SHA256-Zeile gefunden. Setup wird ohne Prüfsummenvergleich gestartet.", isError: false);
      return;
    }

    var actualHash = await ComputeSha256Async(setupPath, ct);
    if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
      throw new InvalidOperationException("SHA256-Prüfsumme stimmt nicht überein.");

    SetStatus("SHA256-Prüfsumme ist gültig.", isError: false);
  }

  private static HttpClient CreateHttpClient()
  {
    var http = new HttpClient();
    var version = Assembly.GetExecutingAssembly()
      .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
      .InformationalVersion;

    http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("YTMPresence", string.IsNullOrWhiteSpace(version) ? "dev" : version));
    return http;
  }

  private static string? FindExpectedHash(string checksumsText, string fileName)
  {
    using var reader = new StringReader(checksumsText);

    while (reader.ReadLine() is { } line)
    {
      var trimmed = line.Trim();
      if (string.IsNullOrWhiteSpace(trimmed))
        continue;

      var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length < 2)
        continue;

      if (string.Equals(parts[^1], fileName, StringComparison.OrdinalIgnoreCase))
        return parts[0];
    }

    return null;
  }

  private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
  {
    await using var stream = File.OpenRead(path);
    var hash = await SHA256.HashDataAsync(stream, ct);
    return Convert.ToHexString(hash).ToLowerInvariant();
  }

  private static string GetSetupFileName(UpdateCheckResult update)
  {
    if (!string.IsNullOrWhiteSpace(update.SetupAssetName))
      return Path.GetFileName(update.SetupAssetName);

    if (!string.IsNullOrWhiteSpace(update.SetupDownloadUrl) &&
        Uri.TryCreate(update.SetupDownloadUrl, UriKind.Absolute, out var uri))
    {
      var fileName = Path.GetFileName(uri.LocalPath);
      if (!string.IsNullOrWhiteSpace(fileName))
        return fileName;
    }

    return $"YTMPresence-{update.LatestVersion}-setup.exe";
  }

  private void UpdateProgress(long downloadedBytes, long? totalBytes)
  {
    if (totalBytes is > 0)
    {
      var percent = Math.Clamp(downloadedBytes * 100.0 / totalBytes.Value, 0, 100);
      DownloadProgress.Value = percent;
      ProgressText.Text = $"{percent:0}% · {FormatBytes(downloadedBytes)} von {FormatBytes(totalBytes.Value)}";
      return;
    }

    ProgressText.Text = $"{FormatBytes(downloadedBytes)} heruntergeladen";
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
    InstallButton.IsEnabled = !string.IsNullOrWhiteSpace(_update.SetupDownloadUrl);
    DownloadOnlyButton.IsEnabled = !string.IsNullOrWhiteSpace(_update.SetupDownloadUrl);
    OpenReleaseButton.IsEnabled = true;
    CancelButton.Content = "Schließen";
  }

  private void SetStatus(string text, bool isError)
  {
    StatusText.Text = text;
    StatusText.Foreground = isError
        ? System.Windows.Media.Brushes.Firebrick
        : System.Windows.Media.Brushes.DarkGreen;
  }
}
