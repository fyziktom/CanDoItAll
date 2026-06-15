using System.Xml.Linq;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessModuleBoundaryTests
{
    private static readonly string[] OrderedProcessBoundaryProjects =
    [
        "CanDoItAll.Processes.Contracts",
        "CanDoItAll.Processes.Abstractions",
        "CanDoItAll.Processes.Core",
        "CanDoItAll.Processes.Drivers.Abstractions",
        "CanDoItAll.Processes.Projections",
        "CanDoItAll.Git",
        "CanDoItAll.Processes.Templates",
        "CanDoItAll.Processes.Builder",
        "CanDoItAll.Processes.Runtime",
        "CanDoItAll.Processes.Persistence",
        "CanDoItAll.Processes.Application",
        "CanDoItAll.Components.Git",
        "CanDoItAll.Modules.Processes"
    ];

    private static readonly Dictionary<string, string[]> AllowedProcessReferences = new(StringComparer.Ordinal)
    {
        ["CanDoItAll.Processes.Contracts"] = [],
        ["CanDoItAll.Processes.Abstractions"] = ["CanDoItAll.Processes.Contracts"],
        ["CanDoItAll.Processes.Core"] = ["CanDoItAll.Processes.Contracts", "CanDoItAll.Processes.Abstractions"],
        ["CanDoItAll.Processes.Drivers.Abstractions"] = ["CanDoItAll.Processes.Contracts", "CanDoItAll.Processes.Abstractions", "CanDoItAll.Processes.Core"],
        ["CanDoItAll.Processes.Projections"] = ["CanDoItAll.Processes.Contracts", "CanDoItAll.Processes.Abstractions", "CanDoItAll.Processes.Core"],
        ["CanDoItAll.Git"] = [],
        ["CanDoItAll.Processes.Templates"] = ["CanDoItAll.Processes.Contracts", "CanDoItAll.Processes.Abstractions", "CanDoItAll.Processes.Core"],
        ["CanDoItAll.Processes.Builder"] = ["CanDoItAll.Processes.Contracts", "CanDoItAll.Processes.Abstractions", "CanDoItAll.Processes.Core", "CanDoItAll.Processes.Templates", "CanDoItAll.Processes.Drivers.Abstractions"],
        ["CanDoItAll.Processes.Runtime"] = ["CanDoItAll.Processes.Contracts", "CanDoItAll.Processes.Abstractions", "CanDoItAll.Processes.Core", "CanDoItAll.Processes.Builder", "CanDoItAll.Processes.Drivers.Abstractions"],
        ["CanDoItAll.Processes.Persistence"] = ["CanDoItAll.Processes.Contracts", "CanDoItAll.Processes.Abstractions", "CanDoItAll.Processes.Core", "CanDoItAll.Processes.Runtime", "CanDoItAll.Processes.Projections"],
        ["CanDoItAll.Processes.Application"] = ["CanDoItAll.Processes.Builder", "CanDoItAll.Processes.Runtime", "CanDoItAll.Processes.Persistence", "CanDoItAll.Processes.Templates", "CanDoItAll.Processes.Projections", "CanDoItAll.Git", "CanDoItAll.Processes.Drivers.Abstractions"],
        ["CanDoItAll.Components.Git"] = ["CanDoItAll.Git"],
        ["CanDoItAll.Modules.Processes"] = ["CanDoItAll.Processes.Application", "CanDoItAll.Processes.Projections", "CanDoItAll.Components.Git"]
    };

    [Fact]
    public void Process_boundary_projects_appear_in_solution_order()
    {
        var root = FindRepositoryRoot();
        var solution = XDocument.Load(Path.Combine(root, "CanDoItAll.slnx"));
        var paths = solution.Descendants("Project")
            .Select(element => element.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToArray();

        var indexes = OrderedProcessBoundaryProjects.ToDictionary(
            project => project,
            project => Array.FindIndex(paths, path => path.EndsWith($"/{project}/{project}.csproj", StringComparison.Ordinal)));

        var missing = indexes
            .Where(item => item.Value < 0)
            .Select(item => item.Key)
            .ToArray();
        Assert.True(missing.Length == 0, "Missing process boundary projects from solution: " + string.Join(", ", missing));

        for (var index = 1; index < OrderedProcessBoundaryProjects.Length; index++)
        {
            var previous = OrderedProcessBoundaryProjects[index - 1];
            var current = OrderedProcessBoundaryProjects[index];
            Assert.True(
                indexes[previous] < indexes[current],
                $"{previous} must appear before {current} in the solution.");
        }
    }

    [Fact]
    public void Process_boundary_projects_only_reference_allowed_inner_layers()
    {
        var root = FindRepositoryRoot();
        var knownProjects = OrderedProcessBoundaryProjects.ToHashSet(StringComparer.Ordinal);

        foreach (var project in OrderedProcessBoundaryProjects)
        {
            var projectFile = Path.Combine(root, "src", project, $"{project}.csproj");
            var document = XDocument.Load(projectFile);
            var actualReferences = document.Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value)
                .Where(include => !string.IsNullOrWhiteSpace(include))
                .Select(include => Path.GetFileNameWithoutExtension(include))
                .Where(reference => reference is not null && knownProjects.Contains(reference))
                .Cast<string>()
                .Order(StringComparer.Ordinal)
                .ToArray();
            var allowedReferences = AllowedProcessReferences[project]
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(allowedReferences, actualReferences);
        }
    }

    [Fact]
    public void Process_generic_projects_do_not_contain_domain_specific_vocabulary()
    {
        var root = FindRepositoryRoot();
        var genericProjects = new[]
        {
            "CanDoItAll.Processes.Contracts",
            "CanDoItAll.Processes.Abstractions",
            "CanDoItAll.Processes.Core",
            "CanDoItAll.Processes.Drivers.Abstractions",
            "CanDoItAll.Processes.Builder",
            "CanDoItAll.Processes.Runtime"
        };
        var blockedTerms = new[]
        {
            "Blazor",
            "Razor",
            "DbContext",
            "EntityFramework",
            "ProjectStructure",
            "Workbench",
            "SchedulerPlanner",
            "CrmHr",
            "Gmail",
            "Office365",
            "Docker",
            "Tetris",
            "Invoice",
            "Recipe",
            "Radzen"
        };

        var findings = genericProjects
            .Select(project => Path.Combine(root, "src", project))
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => FindTermMatches(root, path, blockedTerms))
            .Take(20)
            .ToArray();

        Assert.True(
            findings.Length == 0,
            "Generic process boundary projects must stay domain-agnostic: " + string.Join(", ", findings));
    }

    [Fact]
    public void Deprecated_process_runtime_symbols_are_absent_from_active_sources()
    {
        var root = FindRepositoryRoot();
        var symbols = new[]
        {
            string.Concat("Process", "RunAutomationDispatchService"),
            string.Concat("Processes", "Service.StartRunAsync"),
            string.Concat("Process", "ObservationService"),
            string.Concat("Process", "ObservationCache"),
            string.Concat("Process", "BranchOutcomeRouting"),
            string.Concat("Process", "RecoveryRouter"),
            string.Concat("Agent", "RecoveryModels"),
            string.Concat("Process", "StepRun"),
            string.Concat("Process", "ArtifactRecord"),
            string.Concat("Process", "JournalEntry"),
            string.Concat("Process", "DriverVerificationGateway"),
            string.Concat("current-module", ".import-envelope"),
            string.Concat("current-module", ".compatibility-report")
        };

        var searchRoots = new[]
        {
            Path.Combine(root, "src"),
            Path.Combine(root, "tests"),
            Path.Combine(root, "tools"),
            Path.Combine(root, "Templates")
        };

        var findings = searchRoots
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            .Where(IsScannableSourceOrProjectFile)
            .Where(path => !IsAllowedHistoricalInput(root, path))
            .SelectMany(path => FindTermMatches(root, path, symbols))
            .Take(20)
            .ToArray();

        Assert.True(
            findings.Length == 0,
            "Deprecated process runtime symbols must remain only in archived or historical inputs: " + string.Join(", ", findings));
    }

    [Fact]
    public void Concrete_process_driver_projects_are_not_active()
    {
        var root = FindRepositoryRoot();
        var concreteDriverProjects = Directory.EnumerateDirectories(Path.Combine(root, "src"), "CanDoItAll.Processes.Drivers.*")
            .Select(Path.GetFileName)
            .Where(name => !string.Equals(name, "CanDoItAll.Processes.Drivers.Abstractions", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            concreteDriverProjects.Length == 0,
            "Concrete process driver projects must stay out of the active tree until their rebuild phases: " + string.Join(", ", concreteDriverProjects));
    }

    private static IEnumerable<string> FindTermMatches(string root, string path, IReadOnlyList<string> terms)
    {
        var text = File.ReadAllText(path);
        foreach (var term in terms)
        {
            if (text.Contains(term, StringComparison.Ordinal))
            {
                yield return $"{Path.GetRelativePath(root, path)} contains {term}";
            }
        }
    }

    private static bool IsScannableSourceOrProjectFile(string path)
    {
        if (path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
            path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
            path.Contains($"{Path.DirectorySeparatorChar}.codex{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
            path.Contains($"{Path.DirectorySeparatorChar}.codex-tmp{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
            path.Contains($"{Path.DirectorySeparatorChar}.artifacts{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedHistoricalInput(string root, string path)
    {
        var relativePath = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
        return relativePath.StartsWith("src/CanDoItAll.Migrations.PostgreSql/Migrations/", StringComparison.Ordinal) ||
               relativePath.StartsWith("Templates/Processes/", StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
