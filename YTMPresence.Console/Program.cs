using YTMPresence.Core;

var settingsPath = SettingsStore.GetDefaultSettingsPath();
var settings = SettingsStore.LoadOrCreateDefault(settingsPath);
SettingsStore.Save(settingsPath, settings);

using var stopCts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
  e.Cancel = true;
  stopCts.Cancel();
};

await using var server = new CompanionServer(settings);
await server.StartAsync();

Console.WriteLine($"Companion läuft: {settings.GetWebSocketEndpoint()}");
Console.WriteLine($"Settings: {settingsPath}");
Console.WriteLine("Strg+C zum Beenden.");

try
{
  await Task.Delay(Timeout.InfiniteTimeSpan, stopCts.Token);
}
catch (OperationCanceledException)
{
}
