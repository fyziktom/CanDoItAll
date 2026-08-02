using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetProductBaselineLaunchVariableContributor(
    IWorkspaceFileService workspaceFiles) : IProcessLaunchVariableContributor
{
    internal const string DriverKey = "dotnet.product-baseline";
    internal const string VariableName = "DotNetProductBaselineContract";
    internal const string Schema = "dotnet.product-baseline/v1";

    private const int MaximumCandidatesPerKind = 200;
    private const int MaximumContractSolutionSamples = 4;
    private const int MaximumContractProjectSamples = 8;
    private const int MaximumContractDuplicateNameSamples = 4;
    private const int MaximumContractPathCharacters = 240;
    private const int MaximumContractNameCharacters = 120;
    private const int MaximumContractFrameworkSamples = 4;
    private const int MaximumContractFrameworkCharacters = 48;
    internal const int MaximumSerializedContractCharacters = 8000;
    private const int MaximumProjectCharacters = 100000;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex TargetFrameworkPattern = new(
        @"<TargetFrameworks?>(?<value>[^<]+)</TargetFrameworks?>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public void Enrich(
        ProcessLaunchPreparationContext context,
        IDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(variables);

        if (!context.DriverActivations.Any(activation =>
                string.Equals(activation.DriverKey, DriverKey, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var targetAlias = ResolveTargetAlias(variables);
        if (string.IsNullOrWhiteSpace(targetAlias))
        {
            variables[VariableName] = SerializeContract(
                "unavailable",
                discoveryComplete: false,
                [],
                [],
                []);
            return;
        }

        var rootStat = workspaceFiles.StatPath(targetAlias);
        var rootMissing = rootStat.IsKnownMissing() || rootStat.Succeeded && !rootStat.Exists;
        if (!rootStat.Succeeded ||
            !rootStat.Exists ||
            !string.Equals(rootStat.PathKind, "directory", StringComparison.OrdinalIgnoreCase))
        {
            var rootStatus = rootMissing
                ? "not-found"
                : "unavailable";
            variables[VariableName] = SerializeContract(
                rootStatus,
                discoveryComplete: string.Equals(
                    rootStatus,
                    "not-found",
                    StringComparison.Ordinal),
                [],
                [],
                []);
            return;
        }

        var solutionListing = MergeListings(
            workspaceFiles.ListFiles(targetAlias, "*.sln", MaximumCandidatesPerKind),
            workspaceFiles.ListFiles(targetAlias, "*.slnx", MaximumCandidatesPerKind));
        var projectListing = workspaceFiles.ListFiles(
            targetAlias,
            "*.csproj",
            MaximumCandidatesPerKind);
        var solutionFiles = ResolveRelativeFiles(solutionListing, targetAlias);
        var projectFiles = ResolveRelativeFiles(projectListing, targetAlias);
        var sampledProjectPaths = projectFiles
            .Where(IsBoundedContractPath)
            .Take(MaximumContractProjectSamples)
            .ToArray();
        var projects = sampledProjectPaths
            .Select(path => InspectProject(targetAlias, path))
            .OrderBy(project => project.File, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var discoveryComplete =
            solutionListing.Succeeded &&
            projectListing.Succeeded &&
            !solutionListing.IsTruncated &&
            !projectListing.IsTruncated;
        var hasDiscoveredTopology = solutionFiles.Count > 0 || projectFiles.Count > 0;
        var status = discoveryComplete
            ? solutionFiles.Count > 0 && projectFiles.Count > 0
                ? "discovered"
                : !hasDiscoveredTopology
                    ? "not-found"
                    : "partial"
            : hasDiscoveredTopology || solutionListing.Succeeded || projectListing.Succeeded
                ? "partial"
                : "unavailable";
        variables[VariableName] = SerializeContract(
            status,
            discoveryComplete,
            solutionFiles,
            projectFiles,
            projects);
    }

    private DotNetProductBaselineProject InspectProject(
        string targetAlias,
        string relativePath)
    {
        var read = workspaceFiles.ReadTextFile(
            CombineAlias(targetAlias, relativePath),
            MaximumProjectCharacters);
        if (!read.Succeeded || read.IsTruncated)
        {
            return new DotNetProductBaselineProject(
                relativePath,
                Path.GetFileNameWithoutExtension(relativePath),
                [],
                IsLikelyTestProject(relativePath, string.Empty),
                InspectionComplete: false);
        }

        var discoveredTargetFrameworks = TargetFrameworkPattern
            .Matches(read.Content)
            .SelectMany(match => match.Groups["value"].Value.Split(
                ';',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var targetFrameworks = discoveredTargetFrameworks
            .Where(framework =>
                framework.Length <= MaximumContractFrameworkCharacters)
            .Take(MaximumContractFrameworkSamples)
            .ToArray();
        return new DotNetProductBaselineProject(
            relativePath,
            Path.GetFileNameWithoutExtension(relativePath),
            targetFrameworks,
            IsLikelyTestProject(relativePath, read.Content),
            discoveredTargetFrameworks.Length == targetFrameworks.Length);
    }

    private static WorkspaceFileListResult MergeListings(
        WorkspaceFileListResult first,
        WorkspaceFileListResult second)
        => new(
            first.Succeeded && second.Succeeded,
            string.Join(" ", first.Message, second.Message),
            first.Receipt,
            first.RootPath,
            "*.sln;*.slnx",
            first.Entries
                .Concat(second.Entries)
                .GroupBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray(),
            first.IsTruncated || second.IsTruncated);

    private static IReadOnlyList<string> ResolveRelativeFiles(
        WorkspaceFileListResult listing,
        string targetAlias)
    {
        var normalizedRoot = NormalizePath(targetAlias).TrimEnd('/');
        return listing.Entries
            .Where(entry => string.Equals(entry.PathKind, "file", StringComparison.OrdinalIgnoreCase))
            .Select(entry => NormalizePath(entry.RelativePath))
            .Select(path => path.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase)
                ? path[(normalizedRoot.Length + 1)..]
                : path)
            .Where(IsSafeRelativeProductPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string SerializeContract(
        string status,
        bool discoveryComplete,
        IReadOnlyList<string> solutionFiles,
        IReadOnlyList<string> projectFiles,
        IReadOnlyList<DotNetProductBaselineProject> projects)
    {
        var duplicateProjectNames = projectFiles
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var solutionSamples = solutionFiles
            .Where(IsBoundedContractPath)
            .Take(MaximumContractSolutionSamples)
            .ToArray();
        var duplicateProjectNameSamples = duplicateProjectNames
            .Where(name => name.Length <= MaximumContractNameCharacters)
            .Take(MaximumContractDuplicateNameSamples)
            .ToArray();
        var contract = JsonSerializer.Serialize(
            new DotNetProductBaselineContract(
                Schema,
                status,
                discoveryComplete,
                solutionFiles.Count,
                projectFiles.Count,
                solutionSamples.Length == solutionFiles.Count &&
                    projects.Count == projectFiles.Count,
                projects.Count == projectFiles.Count &&
                    projects.All(project => project.InspectionComplete),
                solutionSamples,
                projects,
                duplicateProjectNames.Length,
                duplicateProjectNameSamples.Length == duplicateProjectNames.Length,
                duplicateProjectNameSamples),
            SerializerOptions);
        if (contract.Length > MaximumSerializedContractCharacters)
        {
            throw new InvalidOperationException(
                $"The bounded .NET product baseline contract exceeded {MaximumSerializedContractCharacters} characters.");
        }

        return contract;
    }

    private static bool IsBoundedContractPath(string path)
        => path.Length <= MaximumContractPathCharacters &&
           Path.GetFileNameWithoutExtension(path).Length <= MaximumContractNameCharacters;

    private static string ResolveTargetAlias(IDictionary<string, string> variables)
        => FirstNonEmpty(
            ResolveVariable(variables, "ProductRootAlias"),
            ResolveVariable(variables, "ExternalTargetRoot"),
            ResolveVariable(variables, "OutputRootAlias"),
            AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
                FirstNonEmpty(
                    ResolveVariable(variables, "ProductRoot"),
                    ResolveVariable(variables, "OutputRoot"))) ?? string.Empty);

    private static string CombineAlias(string rootAlias, string relativePath)
        => $"{NormalizePath(rootAlias).TrimEnd('/')}/{NormalizePath(relativePath).TrimStart('/')}";

    private static bool IsLikelyTestProject(string relativePath, string content)
        => relativePath.Contains("/test", StringComparison.OrdinalIgnoreCase) ||
           Path.GetFileNameWithoutExtension(relativePath).EndsWith(
               ".Tests",
               StringComparison.OrdinalIgnoreCase) ||
           content.Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase) ||
           content.Contains("<IsTestProject>true</IsTestProject>", StringComparison.OrdinalIgnoreCase);

    private static bool IsSafeRelativeProductPath(string path)
        => !string.IsNullOrWhiteSpace(path) &&
           !Path.IsPathRooted(path) &&
           !path.StartsWith("../", StringComparison.Ordinal) &&
           !path.Contains("/../", StringComparison.Ordinal) &&
           !path.Contains(':');

    private static string NormalizePath(string value)
        => value.Trim().Replace('\\', '/');

    private static string ResolveVariable(IDictionary<string, string> variables, string key)
        => variables.TryGetValue(key, out var value)
            ? value?.Trim() ?? string.Empty
            : string.Empty;

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}

internal sealed record DotNetProductBaselineContract(
    string Schema,
    string Status,
    bool DiscoveryComplete,
    int SolutionFileCount,
    int ProjectFileCount,
    bool TopologySampleComplete,
    bool MetadataInspectionComplete,
    IReadOnlyList<string> SolutionFiles,
    IReadOnlyList<DotNetProductBaselineProject> Projects,
    int DuplicateProjectNameCount,
    bool DuplicateProjectNameSampleComplete,
    IReadOnlyList<string> DuplicateProjectNames);

internal sealed record DotNetProductBaselineProject(
    string File,
    string Name,
    IReadOnlyList<string> TargetFrameworks,
    bool IsTestProject,
    bool InspectionComplete);
