namespace YTMPresence.Core.Updates;

/// <summary>
/// Parst eine SHA256SUMS.txt im sha256sum-Format (&lt;hash&gt; &lt;whitespace&gt; [*]&lt;dateiname&gt;).
///
/// Die Zuordnung erfolgt über einen exakten, case-sensitiven Vergleich des
/// Dateinamens. Teilstring-Treffer, abweichende Groß-/Kleinschreibung oder
/// ähnliche Präfixe zählen bewusst nicht als Treffer.
/// </summary>
public static class ChecksumFileParser
{
  /// <summary>
  /// Liefert den erwarteten SHA256-Hash (lowercase hex) für <paramref name="fileName"/>.
  /// Wirft <see cref="UpdateSecurityException"/>, wenn keine Zeile passt, der Hash
  /// syntaktisch ungültig ist oder mehrere widersprüchliche Zeilen existieren.
  /// </summary>
  public static string GetRequiredSha256(string checksumsContent, string fileName)
  {
    if (string.IsNullOrWhiteSpace(fileName))
      throw new UpdateSecurityException("Checksum comparison: the expected file name is empty.");

    var matches = new List<string>();

    foreach (var rawLine in (checksumsContent ?? "").Split('\n'))
    {
      // Only remove the CR belonging to a CRLF line ending. In particular,
      // whitespace after the exact artifact name must not be normalized away.
      var line = rawLine.TrimEnd('\r');
      if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        continue;

      // Erstes Feld = Hash, danach Whitespace, optional '*' (Binärmodus), Rest = Dateiname.
      var separator = line.IndexOfAny([' ', '\t']);
      if (separator <= 0 || separator >= line.Length - 1)
        continue;

      var hashField = line[..separator];
      var nameField = line[(separator + 1)..].TrimStart(' ', '\t');
      if (nameField.StartsWith('*'))
        nameField = nameField[1..];

      // Exakter Vergleich: keine Contains-/Suffix-Logik, case-sensitiv.
      if (!string.Equals(nameField, fileName, StringComparison.Ordinal))
        continue;

      if (!IsValidSha256Hex(hashField))
        throw new UpdateSecurityException(
          $"Checksum comparison: the SHA256 line for '{fileName}' does not contain a valid hash.");

      matches.Add(hashField.ToLowerInvariant());
    }

    if (matches.Count == 0)
      throw new UpdateSecurityException(
        $"Checksum comparison: SHA256SUMS.txt has no line for '{fileName}'. The update is not run.");

    // Exactly one line is authoritative. Identical duplicates are rejected as
    // ambiguous, just like conflicting duplicates and the release verifier.
    if (matches.Count > 1)
      throw new UpdateSecurityException(
        $"Checksum comparison: SHA256SUMS.txt has several lines for '{fileName}'. The update is not run.");

    return matches[0];
  }

  public static bool IsValidSha256Hex(string? value)
  {
    if (value is null || value.Length != 64)
      return false;

    foreach (var c in value)
    {
      var isHex = c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
      if (!isHex)
        return false;
    }

    return true;
  }
}
