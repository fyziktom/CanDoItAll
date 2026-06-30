using System.Xml.Linq;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowFoundationHardeningCheckpointTests
{
    private static readonly string[] ForbiddenFoundationReferences =
    [
        "CanDoItAll.AgentFramework.Maf",
        "CanDoItAll.Modules.AgentFramework",
        "CanDoItAll.Modules.Plugins",
        "CanDoItAll.Plugins.Abstractions",
        "CanDoItAll.AgentFramework.Persistence",
        "CanDoItAll.Web"
    ];

    private const string ObjectTypeName = "object";

    [Fact]
    public void FoundationProjectsUseAllowedDependencyGraph()
    {
        var rules = new[]
        {
            new ProjectDependencyRule(
                "CanDoItAll.AgentFramework.Workflows.Abstractions",
                ["CanDoItAll.AgentFramework.Models"],
                []),
            new ProjectDependencyRule(
                "CanDoItAll.AgentFramework.Workflows.Builder",
                [
                    "CanDoItAll.AgentFramework.Models",
                    "CanDoItAll.AgentFramework.Workflows.Abstractions"
                ],
                []),
            new ProjectDependencyRule(
                "CanDoItAll.AgentFramework.Workflows.Core",
                [
                    "CanDoItAll.AgentFramework.Core",
                    "CanDoItAll.AgentFramework.Models",
                    "CanDoItAll.AgentFramework.WorkflowExecutors.Core",
                    "CanDoItAll.AgentFramework.Workflows.Abstractions",
                    "CanDoItAll.AgentFramework.Workflows.Runtime",
                    "CanDoItAll.SharedKernel"
                ],
                ["Microsoft.Extensions.DependencyInjection.Abstractions"]),
            new ProjectDependencyRule(
                "CanDoItAll.AgentFramework.Workflows.Runtime",
                [
                    "CanDoItAll.AgentFramework.Core",
                    "CanDoItAll.AgentFramework.Models",
                    "CanDoItAll.AgentFramework.WorkflowExecutors.Core",
                    "CanDoItAll.AgentFramework.Workflows.Abstractions"
                ],
                ["Microsoft.Extensions.DependencyInjection.Abstractions"])
        };

        foreach (var rule in rules)
        {
            var project = XDocument.Load(GetProjectPath(rule.ProjectName));
            var projectReferences = ReadReferences(project, "ProjectReference")
                .Select(Path.GetFileNameWithoutExtension)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var packageReferences = ReadReferences(project, "PackageReference")
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(rule.AllowedProjectReferences.Order(StringComparer.Ordinal), projectReferences);
            Assert.Equal(rule.AllowedPackageReferences.Order(StringComparer.Ordinal), packageReferences);
        }
    }

    [Fact]
    public void FoundationProjectsRejectForbiddenDownstreamReferences()
    {
        var projectNames = new[]
        {
            "CanDoItAll.AgentFramework.Workflows.Abstractions",
            "CanDoItAll.AgentFramework.Workflows.Builder",
            "CanDoItAll.AgentFramework.Workflows.Core",
            "CanDoItAll.AgentFramework.Workflows.Runtime"
        };

        foreach (var projectName in projectNames)
        {
            var project = XDocument.Load(GetProjectPath(projectName));
            var references = ReadReferences(project, "ProjectReference")
                .Concat(ReadReferences(project, "PackageReference"))
                .ToArray();

            foreach (var forbiddenReference in ForbiddenFoundationReferences)
            {
                Assert.DoesNotContain(
                    references,
                    reference => reference.Contains(forbiddenReference, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void LargeMovedImplementationFilesHaveSinglePublicOwner()
    {
        var reviewedFiles = new[]
        {
            new PublicOwnerRule("CanDoItAll.AgentFramework.Workflows.Core", "WorkflowCatalogServices.cs", ["InMemoryWorkflowCatalogService"]),
            new PublicOwnerRule("CanDoItAll.AgentFramework.Workflows.Core", "WorkflowDefinitionValidator.cs", ["WorkflowDefinitionValidator"]),
            new PublicOwnerRule("CanDoItAll.AgentFramework.Workflows.Runtime", "WorkflowRuntimeManager.cs", ["WorkflowRuntimeManager"]),
            new PublicOwnerRule("CanDoItAll.AgentFramework.Workflows.Runtime", "WorkflowArtifactContentStores.cs", ["FileWorkflowArtifactContentStore"])
        };

        foreach (var rule in reviewedFiles)
        {
            var source = File.ReadAllText(GetSourcePath(rule.ProjectName, rule.FileName));
            var publicTypes = FindPublicTypeNames(source);

            Assert.Equal(rule.ExpectedPublicTypes.Order(StringComparer.Ordinal), publicTypes.Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void FoundationImplementationFilesStayWithinCheckpointLineBudget()
    {
        var implementationDirectories = new[]
        {
            Path.Combine(FindRepositoryRoot(), "src", "MAF", "Workflows", "CanDoItAll.AgentFramework.Workflows.Core"),
            Path.Combine(FindRepositoryRoot(), "src", "MAF", "Workflows", "CanDoItAll.AgentFramework.Workflows.Runtime")
        };
        var excludedContractFiles = new HashSet<string>(StringComparer.Ordinal)
        {
            "WorkflowContracts.cs"
        };
        var oversizedFiles = implementationDirectories
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs"))
            .Where(path => !excludedContractFiles.Contains(Path.GetFileName(path)))
            .Select(path => new
            {
                Path = Path.GetRelativePath(FindRepositoryRoot(), path),
                Lines = File.ReadLines(path).Count()
            })
            .Where(item => item.Lines > 750)
            .ToArray();

        Assert.Empty(oversizedFiles);
    }

    [Fact]
    public void FoundationDiagnosticsRemainTypedRepairableAndRedacted()
    {
        var root = FindRepositoryRoot();
        var coreDiagnosticMapper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MAF",
            "Workflows",
            "CanDoItAll.AgentFramework.Workflows.Core",
            "WorkflowFailureDiagnosticsMapper.cs"));
        var runtimeDiagnosticMapper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MAF",
            "Workflows",
            "CanDoItAll.AgentFramework.Workflows.Runtime",
            "WorkflowRuntimeFailureDiagnostics.cs"));
        var failureContract = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MAF",
            "Workflows",
            "CanDoItAll.AgentFramework.Workflows.Abstractions",
            "WorkflowFailureDiagnostics.cs"));
        var eventPayloads = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MAF",
            "Workflows",
            "CanDoItAll.AgentFramework.Workflows.Runtime",
            "WorkflowEventPayloads.cs"));

        Assert.Contains("WorkflowFailureDiagnosticEnvelope", failureContract, StringComparison.Ordinal);
        Assert.Contains("RepairHint", failureContract, StringComparison.Ordinal);
        Assert.Contains("RedactedTechnicalDetail", failureContract, StringComparison.Ordinal);
        Assert.Contains("ExceptionDataKey", coreDiagnosticMapper, StringComparison.Ordinal);
        Assert.Contains("ExceptionDataKey", runtimeDiagnosticMapper, StringComparison.Ordinal);
        Assert.Contains("WorkflowFailureDiagnosticEnvelope", coreDiagnosticMapper, StringComparison.Ordinal);
        Assert.Contains("WorkflowFailureDiagnosticEnvelope", runtimeDiagnosticMapper, StringComparison.Ordinal);
        Assert.Contains("WorkflowExecutorRedaction.RedactText", runtimeDiagnosticMapper, StringComparison.Ordinal);
        Assert.Contains("WorkflowExecutorRedaction.RedactJson", eventPayloads, StringComparison.Ordinal);
    }

    [Fact]
    public void FoundationCodeDoesNotUseLooseObjectDiagnosticPayloadsOrGenericErrors()
    {
        var source = string.Join(
            Environment.NewLine,
            EnumerateFoundationSourceFiles().Select(File.ReadAllText));
        var forbiddenSnippets = new[]
        {
            $"Dictionary<string, {ObjectTypeName}",
            $"IDictionary<string, {ObjectTypeName}",
            $"IReadOnlyDictionary<string, {ObjectTypeName}",
            "generic error",
            "unknown error",
            "something went wrong"
        };

        foreach (var forbiddenSnippet in forbiddenSnippets)
        {
            Assert.DoesNotContain(forbiddenSnippet, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyList<string> ReadReferences(XDocument project, string itemName)
        => project
            .Descendants(itemName)
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

    private static IReadOnlyList<string> FindPublicTypeNames(string source)
    {
        var publicTypes = new List<string>();
        foreach (var line in source.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("public ", StringComparison.Ordinal))
            {
                continue;
            }

            var tokens = trimmed.Split(
                [' ', '\t', '(', ':'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var typeKeywordIndex = Array.FindIndex(tokens, token =>
                token is "class" or "record" or "interface" or "enum");
            if (typeKeywordIndex < 0 || typeKeywordIndex + 1 >= tokens.Length)
            {
                continue;
            }

            publicTypes.Add(tokens[typeKeywordIndex + 1]);
        }

        return publicTypes;
    }

    private static IEnumerable<string> EnumerateFoundationSourceFiles()
    {
        var root = FindRepositoryRoot();
        var projectNames = new[]
        {
            "CanDoItAll.AgentFramework.Workflows.Abstractions",
            "CanDoItAll.AgentFramework.Workflows.Builder",
            "CanDoItAll.AgentFramework.Workflows.Core",
            "CanDoItAll.AgentFramework.Workflows.Runtime"
        };

        return projectNames.SelectMany(projectName =>
            Directory.EnumerateFiles(GetProjectDirectory(root, projectName), "*.cs"));
    }

    private static string GetProjectPath(string projectName)
    {
        var root = FindRepositoryRoot();
        return Path.Combine(GetProjectDirectory(root, projectName), $"{projectName}.csproj");
    }

    private static string GetSourcePath(string projectName, string fileName)
    {
        var root = FindRepositoryRoot();
        return Path.Combine(GetProjectDirectory(root, projectName), fileName);
    }

    private static string GetProjectDirectory(string root, string projectName)
        => Path.Combine(root, "src", "MAF", "Workflows", projectName);

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

    private sealed record ProjectDependencyRule(
        string ProjectName,
        IReadOnlyList<string> AllowedProjectReferences,
        IReadOnlyList<string> AllowedPackageReferences);

    private sealed record PublicOwnerRule(
        string ProjectName,
        string FileName,
        IReadOnlyList<string> ExpectedPublicTypes);
}
