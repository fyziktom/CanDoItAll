namespace CanDoItAll.Tests.Components.Memory;

[Trait("Category", "HostPlatform")]
public sealed class MemoryUiRefactoringCheckpointTests
{
    [Fact]
    public void MemoryProvidersPage_RemainsBoundedAndProviderAgnostic()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagePath = Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CanDoItAll.Modules.Memory",
            "Pages",
            "MemoryProvidersPage.razor");
        var source = File.ReadAllText(pagePath);


        Assert.Contains("MemoryWorkspaceSurface", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RenderProviderUiSurface(", source);
        Assert.DoesNotContain("RenderOperationRow(", source);
        Assert.DoesNotContain("RenderFeedbackRow(", source);
        Assert.DoesNotContain("RenderEventRow(", source);
        Assert.DoesNotContain("CanDoItAll.Modules.CognitiveMemory", source);
        Assert.DoesNotContain("CognitiveMemory", source);
        Assert.DoesNotContain("Qdrant", source);
    }

    [Fact]
    public void MemoryUiCheckpoint_UsesExtractedProviderAndLedgerComponents()
    {
        var repositoryRoot = FindRepositoryRoot();
        var componentRoot = Path.Combine(
            repositoryRoot,
            "src",
            "UI",
            "CanDoItAll.Memory.UI",
            "Components");
        var expectedComponents = new[]
        {
            "MemoryProviderListPanel.razor",
            "MemoryProviderUiSurfaceHost.razor",
            "MemoryOperationLedgerRow.razor",
            "MemoryFeedbackLedgerRow.razor",
            "MemoryEventInboxRow.razor"
        };

        foreach (var component in expectedComponents)
        {
            Assert.True(File.Exists(Path.Combine(componentRoot, component)), $"{component} should be extracted from the generic Memory page.");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
