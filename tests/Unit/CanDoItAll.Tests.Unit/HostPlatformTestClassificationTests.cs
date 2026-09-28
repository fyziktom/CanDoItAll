using System.Text.RegularExpressions;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Infrastructure;

// CI runs every Components and Integration test on Linux, but only the host-covered classes on Windows and macOS
// (see "Platform Split" in docs/testing.md). A class whose own source reaches the operating system must therefore
// declare a host-covered category, or its platform-specific behavior would be verified on Linux alone.
public sealed class HostPlatformTestClassificationTests
{
    private static readonly string[] HostCoveredCategories = ["HostPlatform", "UnixPortabilityCore", "UnixRuntimePortability"];

    private static readonly string[] ClassifiedProjects =
    [
        Path.Combine("tests", "Components", "CanDoItAll.Tests.Components"),
        Path.Combine("tests", "Integration", "CanDoItAll.Tests.Integration")
    ];

    private static readonly (string Name, Regex Pattern)[] HostInteractions =
    [
        ("os-branch", new(@"OperatingSystem\.Is(?:Windows|Linux|MacOS|FreeBSD)|RuntimeInformation\.IsOSPlatform|OSPlatform\.|Environment\.OSVersion")),
        ("process", new(@"ProcessStartInfo|\bnew\s+Process\b|Process\.(?:Start|GetProcess)|LocalWorkspaceProcessHost|\.Kill\s*\(")),
        ("filesystem", new(@"\b(?:File|Directory)\.\w+\s*\(|\bFileStream\b|\bFileInfo\b|\bDirectoryInfo\b|CreateTempSubdirectory|Path\.GetTemp(?:Path|FileName)")),
        ("links-permissions", new(@"SymbolicLink|ReparsePoint|ResolveLinkTarget|UnixFileMode|FileAttributes\.")),
        ("secrets-protection", new(@"DataProtection|ProtectedData|Keychain|SecretService|DPAPI")),
        ("environment", new(@"Environment\.(?:GetFolderPath|SpecialFolder|SetEnvironmentVariable|GetEnvironmentVariable|NewLine|ProcessPath|CurrentDirectory)")),
        ("native-path", new(@"[""'][A-Za-z]:\\\\|Path\.DirectorySeparatorChar|Path\.AltDirectorySeparatorChar"))
    ];

    private static readonly Regex TestAttribute = new(@"\[(?:Fact|Theory|SkippableFact|SkippableTheory)\b");
    private static readonly Regex Namespace = new(@"^\s*namespace\s+([\w.]+)", RegexOptions.Multiline);
    private static readonly Regex TypeDeclaration = new(
        @"^([ \t]*)(?:(?:public|internal|private|protected|sealed|static|abstract|partial)\s+)*(?:class|record|struct)\s+(\w+)",
        RegexOptions.Multiline);
    private static readonly Regex CategoryTrait = new(@"Trait\(\s*""Category""\s*,\s*""(\w+)""\s*\)");

    [Fact]
    public void Components_and_integration_classes_that_reach_the_host_run_on_every_platform()
    {
        string repositoryRoot = TestRepositoryRoot.Find();
        SourceFile[] sources = ClassifiedProjects.SelectMany(project =>
                Directory.EnumerateFiles(Path.Combine(repositoryRoot, project), "*.cs", SearchOption.AllDirectories)
                    .Where(path => !IsBuildOutput(path))
                    .Select(path => new SourceFile(Path.GetRelativePath(repositoryRoot, path), File.ReadAllText(path))))
            .ToArray();

        IReadOnlyList<string> violations = FindUnclassified(sources);

        Assert.True(
            violations.Count == 0,
            "These test classes reach the host operating system but run only on Linux. Add " +
            "[Trait(\"Category\", \"HostPlatform\")] to each class (see docs/testing.md):\n" + string.Join("\n", violations));
    }

    [Fact]
    public void The_classifier_requires_class_level_coverage_for_host_interaction_only()
    {
        const string fileScoped = """
            namespace Sample.Tests;

            internal sealed class Helper { }

            public sealed class WritesFilesTests
            {
                private sealed class Nested { }

                [Fact]
                public void Writes() => File.WriteAllText("x", "y");
            }
            """;
        const string tagged = """
            namespace Sample.Tests
            {
                [Trait("Category", "HostPlatform")]
                public sealed class TaggedTests
                {
                    [Fact]
                    public void Starts() => Process.Start("tool");
                }
            }
            """;
        const string methodLevelOnly = """
            namespace Sample.Tests;

            public sealed partial class SplitTests
            {
                [Fact]
                [Trait("Category", "UnixPortabilityCore")]
                public void UsesTemp() => Directory.CreateTempSubdirectory();
            }
            """;
        const string otherPartTagged = """
            namespace Sample.Tests;

            [Trait("Category", "UnixPortabilityCore")]
            public sealed partial class SplitTests
            {
            }
            """;
        const string neutral = """
            namespace Sample.Tests;

            public sealed class PureTests
            {
                [Fact]
                public void Adds() => Assert.Equal(2, 1 + 1);
            }
            """;

        IReadOnlyList<string> violations = FindUnclassified(
        [
            new("WritesFilesTests.cs", fileScoped),
            new("TaggedTests.cs", tagged),
            new("SplitTests.cs", methodLevelOnly),
            new("PureTests.cs", neutral)
        ]);

        Assert.Equal(2, violations.Count);
        Assert.Contains(violations, violation => violation.StartsWith("Sample.Tests.WritesFilesTests (filesystem)", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.StartsWith("Sample.Tests.SplitTests (filesystem)", StringComparison.Ordinal));
        Assert.Empty(FindUnclassified([new("SplitTests.cs", methodLevelOnly), new("SplitTests.Tagged.cs", otherPartTagged)]));
    }

    private static IReadOnlyList<string> FindUnclassified(IReadOnlyCollection<SourceFile> sources)
    {
        var classes = new Dictionary<string, TestClassEvidence>(StringComparer.Ordinal);
        foreach (SourceFile source in sources)
        {
            Match firstTest = TestAttribute.Match(source.Text);
            Match declaration = firstTest.Success ? FindTestClass(source.Text, firstTest.Index) : Match.Empty;
            if (!declaration.Success)
            {
                continue;
            }

            Match ns = Namespace.Match(source.Text);
            string name = ns.Success ? $"{ns.Groups[1].Value}.{declaration.Groups[2].Value}" : declaration.Groups[2].Value;
            if (!classes.TryGetValue(name, out TestClassEvidence? evidence))
            {
                evidence = new TestClassEvidence();
                classes.Add(name, evidence);
            }

            evidence.Files.Add(source.RelativePath);
            evidence.Interactions.UnionWith(HostInteractions.Where(item => item.Pattern.IsMatch(source.Text)).Select(item => item.Name));
            evidence.Categories.UnionWith(ClassLevelCategories(source.Text, declaration.Index));
        }

        // A partial part without tests can still carry the class-level category.
        foreach (SourceFile source in sources)
        {
            Match ns = Namespace.Match(source.Text);
            foreach (Match declaration in TypeDeclaration.Matches(source.Text))
            {
                string name = ns.Success ? $"{ns.Groups[1].Value}.{declaration.Groups[2].Value}" : declaration.Groups[2].Value;
                if (classes.TryGetValue(name, out TestClassEvidence? evidence) && !evidence.Files.Contains(source.RelativePath))
                {
                    evidence.Categories.UnionWith(ClassLevelCategories(source.Text, declaration.Index));
                }
            }
        }

        return classes
            .Where(item => item.Value.Interactions.Count > 0 && !item.Value.Categories.Overlaps(HostCoveredCategories))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => $"{item.Key} ({string.Join(", ", item.Value.Interactions.Order(StringComparer.Ordinal))}) in {string.Join(", ", item.Value.Files.Order(StringComparer.Ordinal))}")
            .ToArray();
    }

    // The test class is the outermost type declared before the first test attribute, which skips both helper
    // types declared ahead of it and helper types nested inside it.
    private static Match FindTestClass(string text, int firstTestIndex)
    {
        Match[] declarations = TypeDeclaration.Matches(text).Where(match => match.Index < firstTestIndex).ToArray();
        if (declarations.Length == 0)
        {
            return Match.Empty;
        }

        int outermost = declarations.Min(match => match.Groups[1].Value.Length);
        return declarations.Last(match => match.Groups[1].Value.Length == outermost);
    }

    private static IEnumerable<string> ClassLevelCategories(string text, int declarationIndex)
    {
        string[] precedingLines = text[..declarationIndex].TrimEnd().Split('\n');
        return precedingLines
            .Reverse()
            .TakeWhile(line => line.TrimStart().StartsWith('['))
            .SelectMany(line => CategoryTrait.Matches(line).Select(match => match.Groups[1].Value));
    }

    private static bool IsBuildOutput(string path)
        => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj");

    private sealed record SourceFile(string RelativePath, string Text);

    private sealed class TestClassEvidence
    {
        public HashSet<string> Files { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Interactions { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Categories { get; } = new(StringComparer.Ordinal);
    }
}
