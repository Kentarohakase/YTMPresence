using YTMPresence.Core.Updates;

namespace YTMPresence.Core.Tests;

public class ChecksumFileParserTests
{
  private const string SetupName = "YTMPresence-1.0.0-win-x64-setup.exe";
  private const string HashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
  private const string HashB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

  [Fact]
  public void ValidLine_ReturnsLowercaseHash()
  {
    var content = $"{HashA.ToUpperInvariant()}  {SetupName}\n{HashB}  YTMPresence-extension.zip\n";
    Assert.Equal(HashA, ChecksumFileParser.GetRequiredSha256(content, SetupName));
  }

  [Fact]
  public void SingleSpaceAndBinaryMarker_AreAccepted()
  {
    Assert.Equal(HashA, ChecksumFileParser.GetRequiredSha256($"{HashA} {SetupName}", SetupName));
    Assert.Equal(HashA, ChecksumFileParser.GetRequiredSha256($"{HashA}  *{SetupName}", SetupName));
  }

  [Fact]
  public void ExtraWhitespaceBetweenFields_IsTolerated()
  {
    var content = $"{HashA}   {SetupName}\r\n";
    Assert.Equal(HashA, ChecksumFileParser.GetRequiredSha256(content, SetupName));
  }

  [Fact]
  public void LeadingOrTrailingWhitespaceTricks_DoNotMatch()
  {
    Assert.Throws<UpdateSecurityException>(() =>
      ChecksumFileParser.GetRequiredSha256($" {HashA}  {SetupName}", SetupName));
    Assert.Throws<UpdateSecurityException>(() =>
      ChecksumFileParser.GetRequiredSha256($"{HashA}  {SetupName} ", SetupName));
  }

  [Fact]
  public void MissingSetupLine_Throws()
  {
    var content = $"{HashA}  YTMPresence-extension.zip";
    var ex = Assert.Throws<UpdateSecurityException>(() => ChecksumFileParser.GetRequiredSha256(content, SetupName));
    Assert.Contains("has no line", ex.Message);
  }

  [Fact]
  public void EmptyContent_Throws()
  {
    Assert.Throws<UpdateSecurityException>(() => ChecksumFileParser.GetRequiredSha256("", SetupName));
  }

  [Fact]
  public void InvalidHashSyntax_Throws()
  {
    // 63 Zeichen bzw. Nicht-Hex-Zeichen
    var shortHash = HashA[..63];
    Assert.Throws<UpdateSecurityException>(() => ChecksumFileParser.GetRequiredSha256($"{shortHash}  {SetupName}", SetupName));

    var nonHex = "z" + HashA[..63];
    Assert.Throws<UpdateSecurityException>(() => ChecksumFileParser.GetRequiredSha256($"{nonHex}  {SetupName}", SetupName));
  }

  [Fact]
  public void DuplicateIdenticalLines_Throw()
  {
    var content = $"{HashA}  {SetupName}\n{HashA.ToUpperInvariant()}  {SetupName}";
    var ex = Assert.Throws<UpdateSecurityException>(() => ChecksumFileParser.GetRequiredSha256(content, SetupName));
    Assert.Contains("several lines", ex.Message);
  }

  [Fact]
  public void ConflictingLines_Throw()
  {
    var content = $"{HashA}  {SetupName}\n{HashB}  {SetupName}";
    var ex = Assert.Throws<UpdateSecurityException>(() => ChecksumFileParser.GetRequiredSha256(content, SetupName));
    Assert.Contains("several lines", ex.Message);
  }

  [Fact]
  public void DifferentCaseFileName_DoesNotMatch()
  {
    var content = $"{HashA}  {SetupName.ToUpperInvariant()}";
    Assert.Throws<UpdateSecurityException>(() => ChecksumFileParser.GetRequiredSha256(content, SetupName));
  }

  [Fact]
  public void SimilarPrefixOrSuffixNames_DoNotMatch()
  {
    var content =
      $"{HashA}  X{SetupName}\n" +
      $"{HashA}  {SetupName}.bak\n" +
      $"{HashA}  {SetupName[..^4]}.zip";
    Assert.Throws<UpdateSecurityException>(() => ChecksumFileParser.GetRequiredSha256(content, SetupName));
  }

  [Fact]
  public void ManipulatedFileNameWithInnerWhitespace_DoesNotMatch()
  {
    // Name mit eingebettetem Leerzeichen darf nicht als Setup-Zeile durchgehen.
    var content = $"{HashA}  {SetupName} extra";
    Assert.Throws<UpdateSecurityException>(() => ChecksumFileParser.GetRequiredSha256(content, SetupName));
  }

  [Fact]
  public void CommentAndEmptyLines_AreIgnored()
  {
    var content = $"# Kommentar\n\n{HashA}  {SetupName}\n";
    Assert.Equal(HashA, ChecksumFileParser.GetRequiredSha256(content, SetupName));
  }

  [Theory]
  [InlineData(null, false)]
  [InlineData("", false)]
  [InlineData("abc", false)]
  [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
  [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", true)]
  [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg", false)]
  public void IsValidSha256Hex_Works(string? value, bool expected)
  {
    Assert.Equal(expected, ChecksumFileParser.IsValidSha256Hex(value));
  }
}
