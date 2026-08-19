namespace CanDoItAll.Tests.Unit.LlmChats;

public sealed class SimpleChatArchitectureBoundaryTests
{
    private static readonly string[] ForbiddenCoreFragments =
    [
        "EntityFramework",
        "Microsoft.AspNetCore",
        "CanDoItAll.Modules",
        "CanDoItAll.AgentFramework.Llm.SimpleChats.Application",
        "CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence",
        "CanDoItAll.AgentFramework.Llm.SimpleChats.Components"
    ];

    [Fact]
    public void CoreHasNoOuterDependencies()
    {
        var root = FindRepositoryRoot();
        var projectDirectory = Path.Combine(
            root,
            "src",
            "MAF",
            "SimpleChats",
            "CanDoItAll.AgentFramework.Llm.SimpleChats.Core");

        AssertDirectoryDoesNotContain(projectDirectory, ForbiddenCoreFragments);
    }

    [Fact]
    public void ApplicationUsesOnlyCoreAndPorts()
    {
        var root = FindRepositoryRoot();
        var projectDirectory = Path.Combine(
            root,
            "src",
            "MAF",
            "SimpleChats",
            "CanDoItAll.AgentFramework.Llm.SimpleChats.Application");
        var forbidden = new[]
        {
            "EntityFramework",
            "Microsoft.AspNetCore",
            "CanDoItAll.Modules",
            "CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime",
            "CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence",
            "CanDoItAll.AgentFramework.Llm.SimpleChats.Components"
        };

        AssertDirectoryDoesNotContain(projectDirectory, forbidden);
    }

    [Fact]
    public void RuntimeHasNoEfReference()
    {
        var root = FindRepositoryRoot();
        var projectDirectory = Path.Combine(
            root,
            "src",
            "MAF",
            "SimpleChats",
            "CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime");

        AssertDirectoryDoesNotContain(
            projectDirectory,
            ["EntityFramework", "AppDbContext", "CanDoItAll.Modules", "CanDoItAll.Infrastructure.Persistence"]);
    }

    private static void AssertDirectoryDoesNotContain(string directory, IReadOnlyList<string> forbidden)
    {
        var sources = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var source in sources)
        {
            var content = File.ReadAllText(source);
            foreach (var fragment in forbidden)
            {
                Assert.DoesNotContain(fragment, content, StringComparison.Ordinal);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the CanDoItAll repository root.");
    }
}
