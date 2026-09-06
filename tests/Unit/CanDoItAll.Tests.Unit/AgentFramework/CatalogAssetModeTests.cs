using CanDoItAll.AgentFramework.UiSandbox;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class CatalogAssetModeTests {
    [Fact]
    public void Default_asset_mode_matches_the_built_theme() {
        CatalogAssets.ValidateRequestedMode(null);
        CatalogAssets.ValidateRequestedMode(CatalogAssets.Mode.ToString());
        Assert.Equal(CatalogAssets.Mode == CatalogAssetMode.Fast ? "css/catalog-fast.css" : "css/output.css", CatalogAssets.ThemePath);
    }

    [Fact]
    public void Runtime_configuration_cannot_misrepresent_the_built_asset_mode() {
        var other = CatalogAssets.Mode == CatalogAssetMode.Fast ? CatalogAssetMode.Parity : CatalogAssetMode.Fast;
        Assert.Throws<InvalidOperationException>(() => CatalogAssets.ValidateRequestedMode(other.ToString()));
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("9")]
    [InlineData("")]
    public void Unsupported_asset_mode_fails_explicitly(string requested) {
        Assert.Throws<InvalidOperationException>(() => CatalogAssets.ValidateRequestedMode(requested));
    }
}
