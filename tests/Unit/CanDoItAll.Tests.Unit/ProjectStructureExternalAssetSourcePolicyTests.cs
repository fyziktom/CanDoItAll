using System.Net;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureExternalAssetSourcePolicyTests
{
    [Theory]
    [InlineData("ftp://assets.example.com/report.pdf")]
    [InlineData("https://user:secret@assets.example.com/report.pdf")]
    [InlineData("http://localhost/report.pdf")]
    [InlineData("http://api.localhost/report.pdf")]
    [InlineData("http://assets.local/report.pdf")]
    [InlineData("http://127.0.0.1/report.pdf")]
    [InlineData("http://10.0.0.1/report.pdf")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://[::1]/report.pdf")]
    [InlineData("http://[fc00::1]/report.pdf")]
    [InlineData("http://[fe80::1]/report.pdf")]
    public void Uri_policy_rejects_non_public_sources(string sourceUrl)
    {
        var exception = Assert.Throws<ProjectStructureExternalAssetSourcePolicyException>(
            () => ProjectStructureExternalAssetSourcePolicy.ValidateUri(new Uri(sourceUrl)));

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    [Theory]
    [InlineData("https://assets.example.com/report.pdf?version=2")]
    [InlineData("http://8.8.8.8/report.pdf")]
    [InlineData("https://[2606:4700:4700::1111]/report.pdf")]
    public void Uri_policy_accepts_public_http_sources(string sourceUrl)
    {
        ProjectStructureExternalAssetSourcePolicy.ValidateUri(new Uri(sourceUrl));
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("10.0.0.1")]
    [InlineData("100.64.0.1")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("fc00::1")]
    [InlineData("fd00::1")]
    [InlineData("fe80::1")]
    [InlineData("fec0::1")]
    [InlineData("ff02::1")]
    public void Address_policy_rejects_non_public_addresses(string address)
    {
        Assert.False(ProjectStructureExternalAssetSourcePolicy.IsPublicAddress(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    public void Address_policy_accepts_public_addresses(string address)
    {
        Assert.True(ProjectStructureExternalAssetSourcePolicy.IsPublicAddress(IPAddress.Parse(address)));
    }

    [Fact]
    public void Address_policy_rejects_mixed_public_and_private_dns_answers()
    {
        Assert.Throws<ProjectStructureExternalAssetSourcePolicyException>(() =>
            ProjectStructureExternalAssetSourcePolicy.EnsurePublicAddresses(
                [IPAddress.Parse("1.1.1.1"), IPAddress.Parse("10.0.0.1")]));
    }

    [Fact]
    public void Named_client_handler_disables_redirects_proxy_and_cookies()
    {
        using var handler = ProjectStructureExternalAssetSourceHttpClient.CreatePrimaryHandler();

        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
        Assert.False(handler.UseCookies);
        Assert.NotNull(handler.ConnectCallback);
    }

    [Fact]
    public void Source_display_omits_query_userinfo_and_fragment()
    {
        var sourceUri = new Uri(
            "https://download-user:download-secret@assets.example.com/files/report.pdf?signature=super-secret#page-2");

        var display = ProjectStructureExternalAssetSourcePolicy.FormatForDisplay(sourceUri);

        Assert.Equal("https://assets.example.com/files/report.pdf", display);
        Assert.DoesNotContain("download-secret", display, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", display, StringComparison.Ordinal);
        Assert.DoesNotContain("page-2", display, StringComparison.Ordinal);
    }
}
