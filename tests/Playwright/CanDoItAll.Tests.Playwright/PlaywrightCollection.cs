namespace CanDoItAll.Tests.Playwright;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PlaywrightCollection : ICollectionFixture<PlaywrightAppFixture>
{
    public const string Name = "Playwright";
}
