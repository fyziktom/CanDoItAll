namespace CanDoItAll.Tests.Playwright;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WebGlSandboxCollection : ICollectionFixture<WebGlSandboxPlaywrightFixture>
{
    public const string Name = "WebGlSandbox";
}
