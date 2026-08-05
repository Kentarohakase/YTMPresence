using System.Net;

namespace YTMPresence.Core.Updates;

/// <summary>
/// Validiert Update-URLs (Ausgangs-URL, jeden Redirect und die finale URL).
///
/// Produktionsmodus: ausschließlich HTTPS auf Port 443 zu einer kleinen
/// Allowlist von GitHub-Hosts. IP-Literale, Loopback, private und
/// link-local Adressen, UNC-/file-Ziele und Userinfo werden abgelehnt.
///
/// Loopback-Testmodus (nur über explizite Konfiguration aktivierbar):
/// erlaubt zusätzlich HTTP/HTTPS auf Loopback-Adressen mit beliebigem Port,
/// damit der lokale QA-Update-Server nutzbar bleibt. Andere Hosts bleiben
/// auch im Testmodus auf die GitHub-Allowlist beschränkt.
///
/// Bewusste Grenze: Es findet kein eigenes DNS-Pinning statt. Die sehr kleine
/// Host-Allowlist und die normale TLS-Zertifikatsprüfung binden die Verbindung
/// an GitHub; eine kompromittierte lokale Trust-/DNS-Umgebung bleibt außerhalb
/// dieses Validators. SHA256 aus demselben Release ist keine Release-Signatur.
/// </summary>
public sealed class UpdateUrlValidator
{
  // Aus dem realen Ablauf ermittelt (Stand 2026-07):
  //   api.github.com  -> Release-Metadaten
  //   github.com      -> browser_download_url der Assets
  //   302             -> release-assets.githubusercontent.com (Asset-CDN)
  // objects.githubusercontent.com ist ein historischer Asset-CDN-Host. Andere
  // githubusercontent.com-Dienste sind bewusst nicht freigegeben.
  private static readonly string[] ExactAllowedHosts =
  [
    "api.github.com",
    "github.com",
    "release-assets.githubusercontent.com",
    "objects.githubusercontent.com"
  ];

  public UpdateUrlValidator(bool allowLoopbackHttp = false)
  {
    AllowLoopbackHttp = allowLoopbackHttp;
  }

  /// <summary>Explizit aktivierter Testmodus für lokale Update-Server.</summary>
  public bool AllowLoopbackHttp { get; }

  public static UpdateUrlValidator ForSettings(AppSettings settings)
    => new(settings.AllowLoopbackUpdateSource);

  /// <summary>
  /// Prüft eine URL und liefert die geparste <see cref="Uri"/> zurück.
  /// Wirft <see cref="UpdateSecurityException"/> bei jedem Verstoß.
  /// </summary>
  public Uri Validate(string? url, string context)
  {
    if (string.IsNullOrWhiteSpace(url))
      throw new UpdateSecurityException($"{context}: URL ist leer.");

    if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
      throw new UpdateSecurityException($"{context}: URL ist keine gültige absolute Adresse.");

    return Validate(uri, context);
  }

  public Uri Validate(Uri uri, string context)
  {
    if (!uri.IsAbsoluteUri)
      throw new UpdateSecurityException($"{context}: URL ist nicht absolut.");

    // file:, ftp:, UNC (\\server\share wird von Uri als file: geparst) usw.
    var isHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    var isHttp = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
    if (!isHttps && !isHttp)
      throw new UpdateSecurityException($"{context}: Schema '{uri.Scheme}' ist nicht erlaubt (nur HTTPS).");

    if (!string.IsNullOrEmpty(uri.UserInfo))
      throw new UpdateSecurityException($"{context}: URLs mit Benutzerdaten sind nicht erlaubt.");

    if (uri.IsUnc || string.IsNullOrEmpty(uri.Host))
      throw new UpdateSecurityException($"{context}: URL enthält keinen gültigen Host.");

    if (IsLoopbackHost(uri.Host))
    {
      // Loopback ist ausschließlich im expliziten Testmodus zulässig,
      // dort mit beliebigem Port und auch als HTTP.
      if (!AllowLoopbackHttp)
        throw new UpdateSecurityException($"{context}: Loopback-Adressen sind für Updates nicht erlaubt.");

      return uri;
    }

    if (!isHttps)
      throw new UpdateSecurityException($"{context}: Nur HTTPS ist als Updatequelle erlaubt.");

    if (IPAddress.TryParse(TrimIpv6Brackets(uri.Host), out var address))
    {
      // Direkte IP-Ziele (inkl. privater und link-local Bereiche) sind nie erlaubt;
      // die Allowlist besteht ausschließlich aus DNS-Namen.
      var kind = DescribeAddress(address);
      throw new UpdateSecurityException($"{context}: Direkte IP-Adressen sind nicht erlaubt ({kind}).");
    }

    if (!uri.IsDefaultPort)
      throw new UpdateSecurityException($"{context}: Port {uri.Port} ist nicht erlaubt (nur 443).");

    if (!IsAllowedHost(uri.Host))
      throw new UpdateSecurityException($"{context}: Host '{uri.Host}' ist nicht als Updatequelle erlaubt.");

    return uri;
  }

  public bool TryValidate(string? url, string context, out Uri? uri)
  {
    try
    {
      uri = Validate(url, context);
      return true;
    }
    catch (UpdateSecurityException)
    {
      uri = null;
      return false;
    }
  }

  private static bool IsAllowedHost(string host)
  {
    foreach (var allowed in ExactAllowedHosts)
    {
      if (string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase))
        return true;
    }

    return false;
  }

  private static bool IsLoopbackHost(string host)
  {
    if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
      return true;

    return IPAddress.TryParse(TrimIpv6Brackets(host), out var address) && IPAddress.IsLoopback(address);
  }

  private static string TrimIpv6Brackets(string host)
    => host.Length >= 2 && host.StartsWith('[') && host.EndsWith(']') ? host[1..^1] : host;

  private static string DescribeAddress(IPAddress address)
  {
    if (IPAddress.IsLoopback(address)) return "Loopback";
    if (IsPrivate(address)) return "privater Bereich";
    if (IsLinkLocal(address)) return "link-local";
    return "öffentliche IP";
  }

  private static bool IsPrivate(IPAddress address)
  {
    if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
    {
      var b = address.GetAddressBytes();
      return b[0] == 10 ||
             (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
             (b[0] == 192 && b[1] == 168);
    }

    // fc00::/7 (Unique Local Addresses)
    var bytes = address.GetAddressBytes();
    return bytes.Length == 16 && (bytes[0] & 0xFE) == 0xFC;
  }

  private static bool IsLinkLocal(IPAddress address)
  {
    if (address.IsIPv6LinkLocal)
      return true;

    if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
    {
      var b = address.GetAddressBytes();
      return b[0] == 169 && b[1] == 254;
    }

    return false;
  }
}
