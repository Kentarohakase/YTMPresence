using System.Security.Cryptography;

namespace YTMPresence.Core;

public static class SecurityTokenHelper
{
  /// <summary>
  /// Erzeugt ein starkes Shared Secret (256-bit), Base64Url ohne Padding.
  /// </summary>
  public static string GenerateSecureToken()
  {
    Span<byte> bytes = stackalloc byte[32];
    RandomNumberGenerator.Fill(bytes);

    var b64 = Convert.ToBase64String(bytes.ToArray());
    return b64.Replace("+", "-").Replace("/", "_").TrimEnd('=');
  }
}