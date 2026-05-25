using CanDoItAll.Modules.Factory;

namespace CanDoItAll.Tests.Unit;

public sealed class PromptLibraryPackLoaderTests
{
    [Fact]
    public void Embedded_prompt_library_pack_is_available_to_loader()
    {
        var resources = typeof(PromptLibraryPackLoader)
            .Assembly
            .GetManifestResourceNames()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("CanDoItAll.Modules.Factory.SeedAssets.PromptLibrary.manifest.json", resources);
        Assert.Contains("CanDoItAll.Modules.Factory.SeedAssets.PromptLibrary.group-catalog.json", resources);
        Assert.Contains("CanDoItAll.Modules.Factory.SeedAssets.PromptLibrary.prompt-component-library.json", resources);
        Assert.Contains("CanDoItAll.Modules.Factory.SeedAssets.PromptLibrary.factory-prompt-flow-templates.seed.json", resources);
        Assert.Contains("CanDoItAll.Modules.Factory.SeedAssets.PromptLibrary.factory-prompt-blueprints.seed.json", resources);

        var pack = new PromptLibraryPackLoader().Load();

        Assert.Equal(12, pack.Groups.Count);
        Assert.Equal(111, pack.Components.Count);
        Assert.Equal(10, pack.Flows.Count);
        Assert.Equal(13, pack.Blueprints.Count);
        Assert.Contains(pack.Components, item => item.Key == "role-architecture-lead");
        Assert.Contains(pack.Flows, item => item.Key == "architecture-review-plan-implement-validate");
        Assert.Contains(pack.Blueprints, item => item.Key == "architecture-spec");
    }
}
