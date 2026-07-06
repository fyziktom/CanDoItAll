namespace CanDoItAll.Tests.Components;

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
        var lineCount = File.ReadLines(pagePath).Count();

        Assert.True(lineCount <= 1300, $"MemoryProvidersPage.razor has {lineCount} lines; extract bounded UI components before native UI migration.");
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
            "Modules",
            "CanDoItAll.Modules.Memory",
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
