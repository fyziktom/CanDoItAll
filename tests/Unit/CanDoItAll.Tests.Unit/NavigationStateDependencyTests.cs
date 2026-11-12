using System.Text.RegularExpressions;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Architecture;

// The URL-state library is a leaf used by routed pages only: renderers, foundation and runtime layers stay unaware
// of page URLs, and the library itself never reaches into a product project.
public sealed class NavigationStateDependencyTests {
    private const string Library = "CanDoItAll.AppComponents.Navigation";
    private static readonly Regex ProjectReference = new("<ProjectReference\\s+Include=\"([^\"]+)\"", RegexOptions.Compiled);

    [Fact]
    public void The_navigation_library_references_no_project() {
        var project = Path.Combine(TestRepositoryRoot.Find(), "src", "UI", Library, Library + ".csproj");

        Assert.Empty(ProjectReference.Matches(File.ReadAllText(project)));
    }

    [Theory]
    [InlineData("UI")]
    [InlineData("Foundation")]
    [InlineData("MAF")]
    [InlineData("Integration")]
    [InlineData("Memory")]
    [InlineData("Processes")]
    public void Layers_below_the_routed_pages_never_reference_the_navigation_library(string layer) {
        var root = Path.Combine(TestRepositoryRoot.Find(), "src", layer);
        var violations = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(project => !string.Equals(Path.GetFileNameWithoutExtension(project), Library, StringComparison.Ordinal))
            .Where(project => ProjectReference.Matches(File.ReadAllText(project))
                .Any(match => match.Groups[1].Value.Contains(Library + ".csproj", StringComparison.Ordinal)))
            .Select(project => Path.GetRelativePath(root, project))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }
}
