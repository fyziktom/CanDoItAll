using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workspace;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed class StorageSelectionProjectionTests {
    [Theory]
    [InlineData("ftp://user:private-sentinel@example.test/archive?token=private-sentinel#private-sentinel", StorageConnectionMode.Remote, "ftp://example.test/archive")]
    [InlineData("https://example.test/root?password=private-sentinel", StorageConnectionMode.Remote, "https://example.test/root")]
    [InlineData("example.test/root?token=private-sentinel", StorageConnectionMode.Remote, "example.test/root")]
    [InlineData("user:private-sentinel@example.test", StorageConnectionMode.Remote, "Endpoint unavailable")]
    [InlineData("/owned/catalogs", StorageConnectionMode.Local, "/owned/catalogs")]
    public void Projection_keeps_only_display_metadata_and_removes_endpoint_credentials(string endpoint, StorageConnectionMode mode, string expected) {
        var id = Guid.NewGuid();
        var summary = new StorageCatalogSummary(id, "Archive", StorageProviderKind.Ftp, mode, endpoint, 7, false, true, true,
            StorageCapability.Read | StorageCapability.Write, StorageHealthStatus.Unavailable, DateTimeOffset.UtcNow, "private-sentinel");
        var item = WorkspaceStorageCatalogSelectionSource.Project(summary);
        Assert.Equal(id, item.Id);
        Assert.Equal("Archive", item.Name);
        Assert.Equal(expected, item.EndpointOrRoot);
        Assert.Equal(7, item.DisplayOrder);
        Assert.False(item.IsEnabled);
        Assert.True(item.IsSystemDefault);
        Assert.True(item.IsReadOnly);
        Assert.Equal("Unavailable", item.Health);
        Assert.DoesNotContain("private-sentinel", System.Text.Json.JsonSerializer.Serialize(item), StringComparison.Ordinal);
    }
}
