using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CanDoItAll.Processes.Templates;

public sealed class ProcessTemplatePackLoader
{
    private const string ManifestFileName = "manifest.json";
    private const string DefinitionFileName = "definition.json";
    private const string SharedDirectoryName = "shared";
    private const string RolesDirectoryName = "roles";
    private static readonly string RoleTemplatesRelativePath = Path.Combine("toolbox", "role-templates.json");

    private readonly string? configuredPackRoot;
    private readonly Lazy<ProcessTemplatePack> pack;

    public ProcessTemplatePackLoader(string? packRoot = null)
    {
        configuredPackRoot = packRoot;
        pack = new Lazy<ProcessTemplatePack>(LoadCore, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ProcessTemplatePack Load() => pack.Value;

    public static string FindPackRoot(string? packRoot = null) => ResolvePackRoot(packRoot);

    private ProcessTemplatePack LoadCore()
    {
        var root = ResolvePackRoot(configuredPackRoot);
        var manifestPath = Path.Combine(root, ManifestFileName);
        var manifest = ReadJson(manifestPath, ProcessTemplateJsonContext.Default.ProcessTemplatePackManifest);
        var definitions = new List<ProcessTemplateDefinitionSummary>(manifest.Processes.Count);
        var roleTemplateActions = LoadRoleTemplateActions(root);

        foreach (var entry in manifest.Processes.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Require(entry.RelativePath, "process relative path", manifestPath);
            var definitionPath = Path.GetFullPath(Path.Combine(root, relativePath, DefinitionFileName));
            var definition = ReadJson(definitionPath, ProcessTemplateJsonContext.Default.ProcessTemplateDefinitionDocument);
            var key = Require(definition.Key, "definition key", definitionPath);
            if (!string.Equals(key, Require(entry.Key, "process key", manifestPath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Process template '{definitionPath}' key '{key}' does not match manifest key '{entry.Key}'.");
            }

            definitions.Add(new ProcessTemplateDefinitionSummary(
                key,
                relativePath,
                Require(definition.DisplayName, "definition display name", definitionPath),
                Require(definition.Summary, "definition summary", definitionPath),
                NormalizeOptional(definition.Criticality, "Unspecified"),
                NormalizeOptional(definition.OperatingMode, "Unspecified"),
                NormalizeOptional(definition.AutonomyLevel, "Unspecified"),
                File.GetLastWriteTimeUtc(definitionPath),
                new ProcessTemplateDefinitionAuthoringDefaults(
                    NormalizeOptional(definition.ValueStatement, string.Empty),
                    NormalizeOptional(definition.CustomerName, string.Empty),
                    NormalizeOptional(definition.OwnerName, string.Empty),
                    NormalizeOptional(definition.InterfaceContractSummary, string.Empty),
                    NormalizeOptional(definition.ManagerOverrideSummary, string.Empty),
                    NormalizeOptional(definition.GovernanceNotes, string.Empty),
                    NormalizeOptional(definition.ChangeSummary, string.Empty),
                    NormalizeOptional(definition.GovernancePolicySummary, string.Empty),
                    NormalizeOptional(definition.ConstitutionRuleSummary, string.Empty),
                    NormalizeOptional(definition.OperatingModeSummary, string.Empty),
                    NormalizeOptional(definition.SimulationReadinessSummary, string.Empty),
                    definition.Steps.Count,
                    definition.RoleUsages.Count(role => role.IsRequired),
                    definition.Steps.Sum(step => step.ArtifactExpectations.Count(artifact => artifact.IsRequired))),
                BuildRoleAuthoringDefaults(root, relativePath, definition, roleTemplateActions),
                ProcessTemplateStepSummaryBuilder.Build(definition),
                ProcessTemplateCanvasSummaryBuilder.Build(root, definition)));
        }

        return new ProcessTemplatePack(root, manifest, definitions);
    }

    private static IReadOnlyList<ProcessTemplateRoleTemplateActionSummary> LoadRoleTemplateActions(string root)
    {
        var path = Path.Combine(root, RoleTemplatesRelativePath);
        if (!File.Exists(path))
        {
            return [];
        }

        var documents = ReadJson(path, ProcessTemplateJsonContext.Default.ProcessTemplateRoleTemplateActionDocumentArray);
        return documents
            .Select(document => new ProcessTemplateRoleTemplateActionSummary(
                Require(document.ActionId, "role template action id", path),
                Require(document.Label, "role template label", path),
                NormalizeOptional(document.Summary, string.Empty),
                NormalizeOptional(document.TemplateRoleKey, string.Empty),
                NormalizeOptional(document.KeyPrefix, "role"),
                NormalizeOptional(document.DisplayNameTemplate, "Role {ordinal}"),
                NormalizeOptional(document.PreferredExecutorKind, "person"),
                document.DefaultAllocationPercent))
            .ToArray();
    }

    private static ProcessTemplateDefinitionRoleAuthoringDefaults BuildRoleAuthoringDefaults(
        string root,
        string definitionRelativePath,
        ProcessTemplateDefinitionDocument definition,
        IReadOnlyList<ProcessTemplateRoleTemplateActionSummary> roleTemplateActions)
    {
        var roles = definition.RoleUsages
            .Select((role, index) => CreateRoleSummary(root, definitionRelativePath, role, index))
            .ToArray();
        var roleNames = roles.ToDictionary(role => role.Key, role => role.DisplayName, StringComparer.OrdinalIgnoreCase);
        var stepRoleBindings = definition.Steps
            .SelectMany(step => step.RoleAssignments.Select(assignment => CreateStepRoleBinding(step, assignment, roleNames)))
            .ToArray();

        return new ProcessTemplateDefinitionRoleAuthoringDefaults(
            roles,
            roleTemplateActions,
            stepRoleBindings);
    }

    private static ProcessTemplateDefinitionRoleSummary CreateRoleSummary(
        string root,
        string definitionRelativePath,
        ProcessTemplateDefinitionRoleUsageDocument usage,
        int index)
    {
        var usageKey = NormalizeOptional(usage.Key, string.Empty);
        var resourceKey = NormalizeOptional(usage.RoleResourceKey, usageKey);
        var resource = string.IsNullOrWhiteSpace(resourceKey)
            ? null
            : TryLoadRoleResource(root, definitionRelativePath, resourceKey);
        var key = NormalizeOptional(usageKey, NormalizeOptional(resource?.Key, $"role-{index + 1}"));
        var displayName = NormalizeOptional(usage.DisplayName, NormalizeOptional(resource?.DisplayName, $"Role {index + 1}"));
        var summary = NormalizeOptional(usage.Notes, NormalizeOptional(resource?.Summary, string.Empty));
        var roleTemplateSourceKey = NormalizeOptional(
            usage.RoleTemplateSourceKey,
            NormalizeOptional(resource?.RoleTemplateSourceKey, string.IsNullOrWhiteSpace(resourceKey) ? string.Empty : $"process-role-template/{resourceKey}"));

        return new ProcessTemplateDefinitionRoleSummary(
            key,
            resourceKey,
            displayName,
            summary,
            NormalizeOptional(usage.Purpose, NormalizeOptional(resource?.Purpose, string.Empty)),
            NormalizeOptional(usage.StaffingIntent, NormalizeOptional(resource?.StaffingIntent, string.Empty)),
            NormalizeOptional(usage.PreferredExecutorKind, NormalizeOptional(resource?.PreferredExecutorKind, "person")),
            NormalizeOptional(usage.PreferredProjectAssignmentRole, NormalizeOptional(resource?.PreferredProjectAssignmentRole, string.Empty)),
            usage.IsRequired,
            usage.AllowsFallback,
            usage.RequiresExplicitApproval,
            usage.DefaultAllocationPercent,
            roleTemplateSourceKey,
            NormalizeOptional(usage.RoleTemplateSnapshotName, NormalizeOptional(resource?.RoleTemplateSnapshotName, string.Empty)),
            NormalizeOptional(usage.SnapshotSummary, NormalizeOptional(resource?.SnapshotSummary, summary)),
            string.IsNullOrWhiteSpace(roleTemplateSourceKey)
                ? "Local role without template source."
                : $"Resolved from {roleTemplateSourceKey}.",
            usage.CanvasX,
            usage.CanvasY);
    }

    private static ProcessTemplateRoleResourceDocument? TryLoadRoleResource(
        string root,
        string definitionRelativePath,
        string roleResourceKey)
    {
        var localPath = Path.Combine(root, definitionRelativePath, RolesDirectoryName, $"{roleResourceKey}.json");
        if (File.Exists(localPath))
        {
            return ReadJson(localPath, ProcessTemplateJsonContext.Default.ProcessTemplateRoleResourceDocument);
        }

        var sharedPath = Path.Combine(root, SharedDirectoryName, RolesDirectoryName, $"{roleResourceKey}.json");
        return File.Exists(sharedPath)
            ? ReadJson(sharedPath, ProcessTemplateJsonContext.Default.ProcessTemplateRoleResourceDocument)
            : null;
    }

    private static ProcessTemplateDefinitionStepRoleBindingSummary CreateStepRoleBinding(
        ProcessTemplateDefinitionStepDocument step,
        ProcessTemplateDefinitionStepRoleAssignmentDocument assignment,
        IReadOnlyDictionary<string, string> roleNames)
    {
        var roleKey = NormalizeOptional(assignment.RoleKey, string.Empty);
        roleNames.TryGetValue(roleKey, out var roleDisplayName);
        return new ProcessTemplateDefinitionStepRoleBindingSummary(
            NormalizeOptional(step.Key, "step"),
            NormalizeOptional(step.Title, NormalizeOptional(step.Key, "Step")),
            roleKey,
            NormalizeOptional(roleDisplayName, roleKey),
            NormalizeOptional(assignment.ResponsibilityKind, "Responsible"),
            assignment.IsRequired,
            assignment.FallbackOrder,
            NormalizeOptional(assignment.RebindPolicySummary, string.Empty));
    }

    private static T ReadJson<T>(
        string path,
        JsonTypeInfo<T> jsonTypeInfo)
        where T : class
    {
        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize(stream, jsonTypeInfo)
                   ?? throw new InvalidOperationException($"Process template JSON file '{path}' was empty.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidOperationException(
                $"Process template JSON file '{path}' could not be loaded: {exception.Message}",
                exception);
        }
    }

    private static string ResolvePackRoot(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            var normalizedExplicitRoot = Path.GetFullPath(explicitRoot);
            if (File.Exists(Path.Combine(normalizedExplicitRoot, ManifestFileName)))
            {
                return normalizedExplicitRoot;
            }

            if (File.Exists(normalizedExplicitRoot) &&
                string.Equals(Path.GetFileName(normalizedExplicitRoot), ManifestFileName, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(normalizedExplicitRoot)!;
            }
        }

        var relativeManifestPath = Path.Combine(
            ProcessTemplatePackOptions.TemplatesRootDirectoryName,
            ProcessTemplatePackOptions.ProcessesDirectoryName,
            ManifestFileName);
        var discoveredRoot = FindContainingDirectory(
            relativeManifestPath,
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        if (!string.IsNullOrWhiteSpace(discoveredRoot))
        {
            return discoveredRoot;
        }

        throw new InvalidOperationException(
            $"Unable to locate {ProcessTemplatePackOptions.DefaultRelativePackRoot}/{ManifestFileName} from the current execution root. " +
            "Configure a process template pack root when the template pack lives outside the repository default layout.");
    }

    private static string? FindContainingDirectory(string relativeFilePath, params string?[] startPaths)
    {
        foreach (var startPath in startPaths
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Select(path => Path.GetFullPath(path!))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var current = new DirectoryInfo(startPath);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, relativeFilePath);
                if (File.Exists(candidate))
                {
                    return Path.GetDirectoryName(candidate);
                }

                current = current.Parent;
            }
        }

        return null;
    }

    private static string Require(string? value, string description, string context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Process template {description} is missing in '{context}'.");
        }

        return value.Trim();
    }

    private static string NormalizeOptional(string? value, string defaultValue)
        => string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();

}

public static class ProcessTemplatePackOptions
{
    public const string TemplatesRootDirectoryName = "Templates";
    public const string ProcessesDirectoryName = "Processes";
    public static readonly string DefaultRelativePackRoot = Path.Combine(TemplatesRootDirectoryName, ProcessesDirectoryName);
}

public sealed record ProcessTemplatePack(
    string RootPath,
    ProcessTemplatePackManifest Manifest,
    IReadOnlyList<ProcessTemplateDefinitionSummary> Definitions);

public sealed record ProcessTemplateDefinitionSummary(
    string Key,
    string RelativePath,
    string DisplayName,
    string Summary,
    string Criticality,
    string OperatingMode,
    string AutonomyLevel,
    DateTimeOffset UpdatedAtUtc,
    ProcessTemplateDefinitionAuthoringDefaults AuthoringDefaults,
    ProcessTemplateDefinitionRoleAuthoringDefaults RoleAuthoringDefaults,
    ProcessTemplateDefinitionStepAuthoringDefaults StepAuthoringDefaults,
    ProcessTemplateDefinitionCanvasAuthoringDefaults CanvasAuthoringDefaults);

public sealed record ProcessTemplateDefinitionAuthoringDefaults(
    string ValueStatement,
    string CustomerName,
    string OwnerName,
    string InterfaceContractSummary,
    string ManagerOverrideSummary,
    string GovernanceNotes,
    string ChangeSummary,
    string GovernancePolicySummary,
    string ConstitutionRuleSummary,
    string OperatingModeSummary,
    string SimulationReadinessSummary,
    int StepCount,
    int RequiredRoleCount,
    int RequiredArtifactExpectationCount);

public sealed record ProcessTemplateDefinitionRoleAuthoringDefaults(
    IReadOnlyList<ProcessTemplateDefinitionRoleSummary> Roles,
    IReadOnlyList<ProcessTemplateRoleTemplateActionSummary> TemplateActions,
    IReadOnlyList<ProcessTemplateDefinitionStepRoleBindingSummary> StepRoleBindings);

public sealed record ProcessTemplateDefinitionRoleSummary(
    string Key,
    string RoleResourceKey,
    string DisplayName,
    string Summary,
    string Purpose,
    string StaffingIntent,
    string PreferredExecutorKind,
    string PreferredProjectAssignmentRole,
    bool IsRequired,
    bool AllowsFallback,
    bool RequiresExplicitApproval,
    int DefaultAllocationPercent,
    string RoleTemplateSourceKey,
    string RoleTemplateSnapshotName,
    string SnapshotSummary,
    string OverrideSummary,
    double CanvasX,
    double CanvasY);

public sealed record ProcessTemplateRoleTemplateActionSummary(
    string ActionId,
    string Label,
    string Summary,
    string TemplateRoleKey,
    string KeyPrefix,
    string DisplayNameTemplate,
    string PreferredExecutorKind,
    int DefaultAllocationPercent);

public sealed record ProcessTemplateDefinitionStepRoleBindingSummary(
    string StepKey,
    string StepTitle,
    string RoleKey,
    string RoleDisplayName,
    string ResponsibilityKind,
    bool IsRequired,
    int FallbackOrder,
    string RebindPolicySummary);

public sealed record ProcessTemplateDefinitionStepAuthoringDefaults(
    IReadOnlyList<ProcessTemplateDefinitionStepAuthoringSummary> Steps);

public sealed record ProcessTemplateDefinitionStepAuthoringSummary(
    int Order,
    string Key,
    string Title,
    string Subtitle,
    string Notes,
    string StepKind,
    int TargetLeadHours,
    bool AllowsManualSkip,
    bool AllowsSafeRefusal,
    bool RequiresApproval,
    bool RequiresDecisionRecord,
    string DecisionRoleKey,
    string InputContractSummary,
    string OutputContractSummary,
    string EvidenceContractSummary,
    string DecisionRightsSummary,
    string ExceptionPolicySummary,
    IReadOnlyList<string> AllowedOperations,
    string OperationTargetScope,
    string SubprocessProcessKey,
    string SubprocessDefinitionSnapshotName,
    IReadOnlyList<ProcessTemplateDefinitionStepBranchOutcomeSummary> BranchOutcomes,
    IReadOnlyList<ProcessTemplateDefinitionStepRoleBindingSummary> RoleBindings,
    IReadOnlyList<ProcessTemplateDefinitionStepArtifactExpectationSummary> ArtifactExpectations);

public sealed record ProcessTemplateDefinitionStepBranchOutcomeSummary(
    string Key,
    string Title,
    string Description,
    string RouteTargetKind,
    string RouteTargetStepKey,
    string RouteTargetArtifactExpectationKey,
    bool IsBackwardRoute,
    int LoopBudgetMaximumRepeats,
    string LoopFingerprintPolicyKey,
    string LoopEscalationTargetKind);

public sealed record ProcessTemplateDefinitionStepArtifactExpectationSummary(
    string Key,
    string TemplateKey,
    string Title,
    string ArtifactKind,
    bool IsRequired,
    string TrustRequirement,
    string SensitivityLevel,
    int RetentionDays,
    string WorkflowOutputId,
    string WorkflowOutputName,
    string WorkflowOutputKind,
    Guid? SubprocessChildArtifactExpectationId,
    string SubprocessChildStepKey,
    string SubprocessChildArtifactTitle,
    string AllowedFutureUsageSummary,
    string ValidationRequirementSummary);

public sealed class ProcessTemplatePackManifest
{
    public string PackKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public DateTimeOffset GeneratedAtUtc { get; set; }

    public List<ProcessTemplateManifestProcessEntry> Processes { get; set; } = [];
}

public sealed class ProcessTemplateManifestProcessEntry
{
    public string Key { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;
}

public sealed class ProcessTemplateDefinitionDocument
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Criticality { get; set; } = string.Empty;

    public string OperatingMode { get; set; } = string.Empty;

    public string AutonomyLevel { get; set; } = string.Empty;

    public string ValueStatement { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string OwnerName { get; set; } = string.Empty;

    public string InterfaceContractSummary { get; set; } = string.Empty;

    public string ManagerOverrideSummary { get; set; } = string.Empty;

    public string GovernanceNotes { get; set; } = string.Empty;

    public string ChangeSummary { get; set; } = string.Empty;

    public string GovernancePolicySummary { get; set; } = string.Empty;

    public string ConstitutionRuleSummary { get; set; } = string.Empty;

    public string OperatingModeSummary { get; set; } = string.Empty;

    public string SimulationReadinessSummary { get; set; } = string.Empty;

    public List<ProcessTemplateDefinitionRoleUsageDocument> RoleUsages { get; set; } = [];

    public List<ProcessTemplateDefinitionStepDocument> Steps { get; set; } = [];
}

public sealed class ProcessTemplateDefinitionRoleUsageDocument
{
    public string Key { get; set; } = string.Empty;

    public string RoleResourceKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string StaffingIntent { get; set; } = string.Empty;

    public string PreferredExecutorKind { get; set; } = string.Empty;

    public string PreferredProjectAssignmentRole { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public bool AllowsFallback { get; set; }

    public bool RequiresExplicitApproval { get; set; }

    public int DefaultAllocationPercent { get; set; }

    public string RoleTemplateSourceKey { get; set; } = string.Empty;

    public string RoleTemplateSnapshotName { get; set; } = string.Empty;

    public string SnapshotSummary { get; set; } = string.Empty;

    public double CanvasX { get; set; }

    public double CanvasY { get; set; }

    public string Notes { get; set; } = string.Empty;
}

public sealed class ProcessTemplateDefinitionStepDocument
{
    public int Order { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string StepKind { get; set; } = string.Empty;

    public int TargetLeadHours { get; set; }

    public bool AllowsManualSkip { get; set; }

    public bool AllowsSafeRefusal { get; set; }

    public bool RequiresApproval { get; set; }

    public bool RequiresDecisionRecord { get; set; }

    public string InputContractSummary { get; set; } = string.Empty;

    public string OutputContractSummary { get; set; } = string.Empty;

    public string EvidenceContractSummary { get; set; } = string.Empty;

    public string DecisionRightsSummary { get; set; } = string.Empty;

    public string ExceptionPolicySummary { get; set; } = string.Empty;

    public List<string> AllowedOperations { get; set; } = [];

    public string OperationTargetScope { get; set; } = string.Empty;

    public string DependsOnStepKey { get; set; } = string.Empty;

    public string DependsOnBranchOutcomeKey { get; set; } = string.Empty;

    public string DecisionRoleKey { get; set; } = string.Empty;

    public string SubprocessProcessKey { get; set; } = string.Empty;

    public string SubprocessDefinitionSnapshotName { get; set; } = string.Empty;

    public double CanvasX { get; set; }

    public double CanvasY { get; set; }

    public double BranchCanvasX { get; set; }

    public double BranchCanvasY { get; set; }

    public List<ProcessTemplateDefinitionStepDependencyDocument> Dependencies { get; set; } = [];

    public List<ProcessTemplateDefinitionStepRoleAssignmentDocument> RoleAssignments { get; set; } = [];

    public List<ProcessTemplateDefinitionArtifactExpectationDocument> ArtifactExpectations { get; set; } = [];

    public List<ProcessTemplateDefinitionStepBranchOutcomeDocument> BranchOutcomes { get; set; } = [];
}

public sealed class ProcessTemplateDefinitionStepDependencyDocument
{
    public string DependsOnStepKey { get; set; } = string.Empty;

    public string DependsOnBranchOutcomeKey { get; set; } = string.Empty;
}

public sealed class ProcessTemplateDefinitionStepBranchOutcomeDocument
{
    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string RouteTargetKind { get; set; } = string.Empty;

    public string RouteTargetStepKey { get; set; } = string.Empty;

    public string RouteTargetArtifactExpectationKey { get; set; } = string.Empty;

    public bool IsBackwardRoute { get; set; }

    public int LoopBudgetMaximumRepeats { get; set; }

    public string LoopFingerprintPolicyKey { get; set; } = string.Empty;

    public string LoopEscalationTargetKind { get; set; } = string.Empty;
}

public sealed class ProcessTemplateDefinitionStepRoleAssignmentDocument
{
    public string RoleKey { get; set; } = string.Empty;

    public string ResponsibilityKind { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public int FallbackOrder { get; set; }

    public string RebindPolicySummary { get; set; } = string.Empty;
}

public sealed class ProcessTemplateDefinitionArtifactExpectationDocument
{
    public string Key { get; set; } = string.Empty;

    public string TemplateKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string ArtifactKind { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public string TrustRequirement { get; set; } = string.Empty;

    public string SensitivityLevel { get; set; } = string.Empty;

    public int RetentionDays { get; set; }

    public string WorkflowOutputId { get; set; } = string.Empty;

    public string WorkflowOutputName { get; set; } = string.Empty;

    public string WorkflowOutputKind { get; set; } = string.Empty;

    public Guid? SubprocessChildArtifactExpectationId { get; set; }

    public string SubprocessChildStepKey { get; set; } = string.Empty;

    public string SubprocessChildArtifactTitle { get; set; } = string.Empty;

    public string AllowedFutureUsageSummary { get; set; } = string.Empty;

    public string ValidationRequirementSummary { get; set; } = string.Empty;
}

public sealed class ProcessTemplateRoleResourceDocument
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string StaffingIntent { get; set; } = string.Empty;

    public string PreferredExecutorKind { get; set; } = string.Empty;

    public string PreferredProjectAssignmentRole { get; set; } = string.Empty;

    public string RoleTemplateSourceKey { get; set; } = string.Empty;

    public string RoleTemplateSnapshotName { get; set; } = string.Empty;

    public string SnapshotSummary { get; set; } = string.Empty;
}

public sealed class ProcessTemplateRoleTemplateActionDocument
{
    public string ActionId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string TemplateRoleKey { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = string.Empty;

    public string DisplayNameTemplate { get; set; } = string.Empty;

    public string PreferredExecutorKind { get; set; } = string.Empty;

    public int DefaultAllocationPercent { get; set; }
}

public sealed class ProcessTemplateStepTemplateActionDocument
{
    public string ActionId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public ProcessTemplateDefinitionStepDocument Template { get; set; } = new();
}
