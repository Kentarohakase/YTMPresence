using YTMPresence.Core.Updates;

namespace YTMPresence.Core.Tests;

public class UpdateUrlValidatorTests
{
  private static readonly UpdateUrlValidator Production = new(allowLoopbackHttp: false);
  private static readonly UpdateUrlValidator LoopbackTestMode = new(allowLoopbackHttp: true);

  [Theory]
  [InlineData("https://api.github.com/repos/Kentarohakase/YTMPresence/releases/latest")]
  [InlineData("https://github.com/Kentarohakase/YTMPresence/releases/download/v1.0.0/YTMPresence-1.0.0-win-x64-setup.exe")]
  [InlineData("https://release-assets.githubusercontent.com/github-production-release-asset/123?sig=abc")]
  [InlineData("https://objects.githubusercontent.com/some/asset")]
  public void GitHubHttpsUrls_AreAllowed(string url)
  {
    var uri = Production.Validate(url, "Test");
    Assert.Equal("https", uri.Scheme);
  }

  [Theory]
  [InlineData("http://github.com/Kentarohakase/YTMPresence/releases")]
  [InlineData("http://api.github.com/repos/x")]
  public void HttpInProduction_IsRejected(string url)
  {
    Assert.Throws<UpdateSecurityException>(() => Production.Validate(url, "Test"));
  }

  [Theory]
  [InlineData("http://127.0.0.1:18111/releases/latest")]
  [InlineData("http://localhost:8080/assets/setup.exe")]
  [InlineData("http://[::1]:9000/x")]
  public void LoopbackHttp_RequiresExplicitTestMode(string url)
  {
    Assert.Throws<UpdateSecurityException>(() => Production.Validate(url, "Test"));
    Assert.NotNull(LoopbackTestMode.Validate(url, "Test"));
  }

  [Theory]
  [InlineData("https://evil.example/setup.exe")]
  [InlineData("https://github.com.evil.example/setup.exe")]
  [InlineData("https://evilgithubusercontent.com/setup.exe")]
  [InlineData("https://githubusercontent.com/setup.exe")]
  public void UnexpectedHosts_AreRejected(string url)
  {
    Assert.Throws<UpdateSecurityException>(() => Production.Validate(url, "Test"));
  }

  [Theory]
  [InlineData("https://raw.githubusercontent.com/owner/repo/main/setup.exe")]
  [InlineData("https://gist.githubusercontent.com/owner/id/raw/setup.exe")]
  [InlineData("https://user-images.githubusercontent.com/1/setup.exe")]
  public void UnneededGitHubusercontentServices_AreRejected(string url)
  {
    Assert.Throws<UpdateSecurityException>(() => Production.Validate(url, "Test"));
  }

  [Theory]
  [InlineData("https://140.82.121.3/download")]
  [InlineData("https://10.0.0.5/internal")]
  [InlineData("https://172.16.1.1/internal")]
  [InlineData("https://192.168.1.10/internal")]
  [InlineData("https://169.254.1.1/link-local")]
  [InlineData("https://[fe80::1]/link-local")]
  [InlineData("https://[fd00::1]/ula")]
  public void IpLiteralTargets_AreRejectedInProduction(string url)
  {
    Assert.Throws<UpdateSecurityException>(() => Production.Validate(url, "Test"));
  }

  [Theory]
  [InlineData("http://10.0.0.5/internal")]
  [InlineData("http://192.168.1.10/internal")]
  [InlineData("http://169.254.1.1/link-local")]
  public void PrivateAddresses_StayRejectedInTestMode(string url)
  {
    // Der Testmodus öffnet nur Loopback, keine privaten oder link-local Bereiche.
    Assert.Throws<UpdateSecurityException>(() => LoopbackTestMode.Validate(url, "Test"));
  }

  [Theory]
  [InlineData(@"file:///C:/Windows/evil.exe")]
  [InlineData(@"\\server\share\evil.exe")]
  [InlineData("ftp://github.com/x")]
  public void FileUncAndOtherSchemes_AreRejected(string url)
  {
    Assert.Throws<UpdateSecurityException>(() => Production.Validate(url, "Test"));
    Assert.Throws<UpdateSecurityException>(() => LoopbackTestMode.Validate(url, "Test"));
  }

  [Fact]
  public void UserInfo_IsRejected()
  {
    Assert.Throws<UpdateSecurityException>(() => Production.Validate("https://user:pass@github.com/x", "Test"));
  }

  [Fact]
  public void NonDefaultHttpsPort_IsRejected()
  {
    Assert.Throws<UpdateSecurityException>(() => Production.Validate("https://github.com:8443/x", "Test"));
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("not a url")]
  [InlineData("/relative/path")]
  public void EmptyOrRelativeUrls_AreRejected(string url)
  {
    Assert.Throws<UpdateSecurityException>(() => Production.Validate(url, "Test"));
  }

  [Fact]
  public void LoopbackHttps_IsAlsoOnlyAllowedInTestMode()
  {
    Assert.Throws<UpdateSecurityException>(() => Production.Validate("https://127.0.0.1:18111/x", "Test"));
    Assert.NotNull(LoopbackTestMode.Validate("https://127.0.0.1:18111/x", "Test"));
  }

  [Fact]
  public void TryValidate_DoesNotThrow()
  {
    Assert.True(Production.TryValidate("https://github.com/x", "Test", out var uri));
    Assert.NotNull(uri);

    Assert.False(Production.TryValidate("https://evil.example/x", "Test", out var rejected));
    Assert.Null(rejected);
  }
}
