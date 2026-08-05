using YTMPresence.Core;

namespace YTMPresence.Core.Tests;

public class SecurityTokenHelperTests
{
  [Fact]
  public void GenerateSecureToken_IsNotEmpty()
  {
    var token = SecurityTokenHelper.GenerateSecureToken();
    Assert.False(string.IsNullOrWhiteSpace(token));
  }

  [Fact]
  public void GenerateSecureToken_IsBase64UrlWithoutPadding()
  {
    var token = SecurityTokenHelper.GenerateSecureToken();

    // 32 Bytes -> 43 Zeichen Base64Url ohne Padding
    Assert.Equal(43, token.Length);
    Assert.DoesNotContain('+', token);
    Assert.DoesNotContain('/', token);
    Assert.DoesNotContain('=', token);
  }

  [Fact]
  public void GenerateSecureToken_ReturnsUniqueValues()
  {
    var tokens = Enumerable.Range(0, 50)
      .Select(_ => SecurityTokenHelper.GenerateSecureToken())
      .ToHashSet();

    Assert.Equal(50, tokens.Count);
  }
}
