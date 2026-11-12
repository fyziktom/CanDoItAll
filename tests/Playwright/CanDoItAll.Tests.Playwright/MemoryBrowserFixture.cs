namespace CanDoItAll.Tests.Playwright;

[CollectionDefinition(Name)]
public sealed class MemoryBrowserCollection : ICollectionFixture<MemoryBrowserFixture> {
    public const string Name = "Memory owned browser host";
}

public sealed class MemoryBrowserFixture : IAsyncLifetime {
    public PlaywrightAppFixture App { get; } = new() {
        RuntimeConfiguration = new Dictionary<string, string?> {
            ["Memory:Providers:DeterministicMock:Enabled"] = "true",
            ["Memory:Providers:Http:Enabled"] = "true"
        }
    };
    public Task InitializeAsync() => App.InitializeAsync();
    public Task DisposeAsync() => App.DisposeAsync();
}
