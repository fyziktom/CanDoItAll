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
        "CanDoItAll.Processes.Drivers.Standard",
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
        ["CanDoItAll.Processes.Drivers.Standard"] = ["CanDoItAll.Processes.Drivers.Abstractions"],
        ["CanDoItAll.Processes.Projections"] = ["CanDoItAll.Processes.Contracts", "CanDoItAll.Processes.Abstractions", "CanDoItAll.Processes.Core"],
        ["CanDoItAll.Git"] = [],
        ["CanDoItAll.Processes.Templates"] = ["CanDoItAll.Processes.Contracts", "CanDoItAll.Processes.Abstractions", "CanDoItAll.Processes.Core"],
        ["CanDoItAll.Processes.Builder"] = ["CanDoItAll.Processes.Contracts", "CanDoItAll.Processes.Abstractions", "CanDoItAll.Processes.Core", "CanDoItAll.Processes.Templates", "CanDoItAll.Processes.Drivers.Abstractions"],
        ["CanDoItAll.Processes.Runtime"] = ["CanDoItAll.Processes.Contracts", "CanDoItAll.Processes.Abstractions", "CanDoItAll.Processes.Core", "CanDoItAll.Processes.Builder", "CanDoItAll.Processes.Drivers.Abstractions"],
        ["CanDoItAll.Processes.Persistence"] = ["CanDoItAll.Processes.Contracts", "CanDoItAll.Processes.Abstractions", "CanDoItAll.Processes.Core", "CanDoItAll.Processes.Builder", "CanDoItAll.Processes.Runtime", "CanDoItAll.Processes.Projections"],
        ["CanDoItAll.Processes.Application"] = ["CanDoItAll.Processes.Builder", "CanDoItAll.Processes.Runtime", "CanDoItAll.Processes.Templates", "CanDoItAll.Processes.Projections", "CanDoItAll.Git", "CanDoItAll.Processes.Drivers.Abstractions"],
        ["CanDoItAll.Components.Git"] = ["CanDoItAll.Git"],
        ["CanDoItAll.Modules.Processes"] = ["CanDoItAll.Processes.Application", "CanDoItAll.Processes.Builder", "CanDoItAll.Processes.Drivers.Abstractions", "CanDoItAll.Processes.Drivers.Standard", "CanDoItAll.Processes.Persistence", "CanDoItAll.Processes.Projections", "CanDoItAll.Processes.Runtime", "CanDoItAll.Processes.Templates", "CanDoItAll.Components.Git"]
    };

    [Fact]
    public void Process_boundary_projects_appear_in_solution_under_logical_folders()
    {
        var root = FindRepositoryRoot();
        var mismatches = OrderedProcessBoundaryProjects
            .Select(project => (
                Project: project,
                ExpectedPath: GetExpectedSolutionProjectPath(project),
                ActualPath: GetSolutionProjectPath(root, project)))
            .Where(item => !string.Equals(item.ExpectedPath, item.ActualPath, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            mismatches.Length == 0,
            "Process boundary projects must appear under their logical solution folders: " +
            string.Join(", ", mismatches.Select(item => $"{item.Project} expected {item.ExpectedPath} but found {item.ActualPath}")));
    }

    [Fact]
    public void Process_boundary_projects_only_reference_allowed_inner_layers()
    {
        var root = FindRepositoryRoot();
        var knownProjects = OrderedProcessBoundaryProjects.ToHashSet(StringComparer.Ordinal);

        foreach (var project in OrderedProcessBoundaryProjects)
        {
            var projectFile = GetSolutionProjectFile(root, project);
            var document = XDocument.Load(projectFile);
            var actualReferences = document.Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value)
                .OfType<string>()
                .Where(include => !string.IsNullOrWhiteSpace(include))
                .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
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
            "ProjectContext",
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
            .Select(project => Path.GetDirectoryName(GetSolutionProjectFile(root, project))!)
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
    public void Process_launch_application_service_does_not_embed_adapter_specific_step_briefs()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "src", "Processes", "CanDoItAll.Processes.Application", "ProcessLaunchApplicationService.cs");
        var blockedTerms = new[]
        {
            "project_structure_process_subprocess_launch",
            "process_step_outcome_result",
            "Do not write evidence under output/",
            "BranchName, RepositoryRoot, SessionId",
            "ChildManagedArtifactRoot",
            "Project id:",
            "Project node id:"
        };

        var findings = FindTermMatches(root, path, blockedTerms).ToArray();

        Assert.True(
            findings.Length == 0,
            "Generic launch orchestration must delegate adapter-specific step brief text: " + string.Join(", ", findings));
    }

    [Fact]
    public void Workbench_launch_preparation_uses_only_the_application_facing_contract()
    {
        var root = FindRepositoryRoot();
        var removedContributorPath = Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "ProjectStructure",
            "ProjectStructureProcessLaunchVariableContributor.cs");
        Assert.False(
            File.Exists(removedContributorPath),
            "Workbench must not retain a concrete .NET launch-variable contributor.");

        var launchPreparationPaths = new[]
        {
            Path.Combine(root, "src", "Modules", "CanDoItAll.Modules.Workbench", "ProjectStructure", "ProjectStructureProcessLaunchSourceSnapshotMapper.cs"),
            Path.Combine(root, "src", "Modules", "CanDoItAll.Modules.Workbench", "Pages", "ProjectStructurePage.Processes.cs"),
            Path.Combine(root, "src", "Modules", "CanDoItAll.Modules.Workbench", "Services", "WorkbenchModuleServiceCollectionExtensions.cs")
        };
        var blockedTerms = new[]
        {
            "DotNet",
            "Blazor",
            "workspace_dotnet",
            "AgentWorkspaceToolAccessMetadata",
            "IProjectStructureProcessLaunchVariableContributor",
            "ProjectStructureProcessLaunchVariableContext",
            "dotnet-solution-setup",
            "software-delivery",
            "CanDoItAll.Processes.Runtime"
        };

        var findings = launchPreparationPaths
            .SelectMany(path => FindTermMatches(root, path, blockedTerms))
            .ToArray();

        Assert.True(
            findings.Length == 0,
            "Workbench launch preparation must map neutral source facts and delegate domain contribution: " + string.Join(", ", findings));
    }

    [Fact]
    public void Workbench_process_node_service_does_not_synthesize_dotnet_launch_context()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "ProjectStructure",
            "ProjectStructureProcessNodeService.cs");
        var blockedTerms = new[]
        {
            "DotNet",
            ".sln",
            ".csproj"
        };

        var findings = FindTermMatches(root, path, blockedTerms).ToArray();

        Assert.True(
            findings.Length == 0,
            "Workbench process orchestration must retain only generic external-target context and delegate technology-specific paths to drivers: " + string.Join(", ", findings));
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
    public void Only_approved_concrete_process_driver_projects_are_active()
    {
        var root = FindRepositoryRoot();
        var allowedConcreteDriverProjects = new[]
        {
            "CanDoItAll.Processes.Drivers.Standard"
        }.ToHashSet(StringComparer.Ordinal);
        var concreteDriverProjects = Directory.EnumerateDirectories(Path.Combine(root, "src", "Processes", "Drivers"), "CanDoItAll.Processes.Drivers.*")
            .Select(Path.GetFileName)
            .Where(name => !string.Equals(name, "CanDoItAll.Processes.Drivers.Abstractions", StringComparison.Ordinal))
            .Where(name => name is not null && !allowedConcreteDriverProjects.Contains(name))
            .ToArray();

        Assert.True(
            concreteDriverProjects.Length == 0,
            "Unexpected concrete process driver projects are active: " + string.Join(", ", concreteDriverProjects));
    }

    private static IEnumerable<string> FindTermMatches(string root, string path, IReadOnlyList<string> terms)
    {
        var text = File.ReadAllText(path);
        foreach (var term in terms)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    text,
                    $"(?<![A-Za-z0-9_]){System.Text.RegularExpressions.Regex.Escape(term)}(?![A-Za-z0-9_])",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant))
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
        return relativePath.StartsWith("src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/", StringComparison.Ordinal) ||
               relativePath.StartsWith("Templates/Processes/", StringComparison.Ordinal);
    }

    private static string GetExpectedSolutionProjectPath(string project)
        => project switch
        {
            "CanDoItAll.Git" => "src/Foundation/CanDoItAll.Git/CanDoItAll.Git.csproj",
            "CanDoItAll.Components.Git" => "src/UI/CanDoItAll.Components.Git/CanDoItAll.Components.Git.csproj",
            "CanDoItAll.Modules.Processes" => "src/Modules/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj",
            _ when project.StartsWith("CanDoItAll.Processes.Drivers.", StringComparison.Ordinal) =>
                $"src/Processes/Drivers/{project}/{project}.csproj",
            _ when project.StartsWith("CanDoItAll.Processes.", StringComparison.Ordinal) =>
                $"src/Processes/{project}/{project}.csproj",
            _ => throw new InvalidOperationException($"No expected solution path is registered for {project}.")
        };

    private static string GetSolutionProjectPath(string root, string project)
    {
        var solution = XDocument.Load(Path.Combine(root, "CanDoItAll.slnx"));
        var projectPaths = solution.Descendants("Project")
            .Select(element => element.Attribute("Path")?.Value)
            .OfType<string>()
            .Where(path => path.EndsWith($"/{project}/{project}.csproj", StringComparison.Ordinal))
            .ToArray();

        return Assert.Single(projectPaths);
    }

    private static string GetSolutionProjectFile(string root, string project)
        => Path.Combine(root, GetSolutionProjectPath(root, project).Replace('/', Path.DirectorySeparatorChar));

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
