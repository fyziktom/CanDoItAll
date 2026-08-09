using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.AgentFramework.Core;

public static class ExecutionInvocationMetadata
{
    public const string MaxStructuredOutputRepairAttemptsMetadataKey = "agentMaxStructuredOutputRepairAttempts";
    public const string RequireStructuredOutputValidationMetadataKey = "agentRequireStructuredOutputValidation";
    public const string AllowRequiredFinalizerStructuredOutputRecoveryMetadataKey = "agentAllowRequiredFinalizerStructuredOutputRecovery";
    public const string AllowedExternalTargetAliasesMetadataKey = "agentAllowedExternalTargetAliases";
    public const string ReadOnlyExternalTargetAliasesMetadataKey = "agentReadOnlyExternalTargetAliases";
    public const string ExternalTargetRootBindingsMetadataKey = "agentExternalTargetRootBindings";
    public const string AllowedManagedArtifactReadRefsMetadataKey = "agentAllowedManagedArtifactReadRefs";
    public const string ProcessCooperationModeMetadataKey = "agentProcessCooperationMode";
    public const string ProcessWorkspaceToolProfileMetadataKey = "agentProcessWorkspaceToolProfile";
    public const string ProcessCooperationSummaryMetadataKey = "agentProcessCooperationSummary";
    public const string ProcessBrowserToolsAllowedMetadataKey = "agentProcessBrowserToolsAllowed";
    public const string ProcessStepExecutionBoundaryMetadataKey = "agentProcessStepExecutionBoundary";
    public const string ProcessStepAllowedOperationsMetadataKey = "agentProcessStepAllowedOperations";
    public const string ProcessStepTargetScopeMetadataKey = "agentProcessStepTargetScope";
    public const string ProcessStepAllowsProductMutationMetadataKey = "agentProcessStepAllowsProductMutation";
    public const string ProcessStepRequiresProductMutationBeforeManagedOutputMetadataKey = "agentProcessStepRequiresProductMutationBeforeManagedOutput";
    public const string ProcessProductMutationToolNamesMetadataKey = "agentProcessProductMutationToolNames";
    public const string ProcessProductMutationRequiredBranchOutcomeKeysMetadataKey = "agentProcessProductMutationRequiredBranchOutcomeKeys";
    public const string ProcessGroundedTargetAliasLedgerMetadataKey = "agentProcessGroundedTargetAliasLedger";
    public const string ContextWorkspaceScopeMetadataKey = "agentContextWorkspaceScope";
    public const string ProjectStructureLaunchAgentMetadataKey = "agentProjectStructureLaunchAgent";
    public const string ProjectStructureProcessNodeContextMetadataKey = "agentProjectStructureProcessNodeContext";
    public const string RuntimeToolProvidersEnabledMetadataKey = "agentRuntimeToolProvidersEnabled";
    public const string WorkspaceToolsEnabledMetadataKey = "agentWorkspaceToolsEnabled";
    public const string ToolCapabilitiesEnabledMetadataKey = "agentToolCapabilitiesEnabled";
    public const string RuntimeCapabilityScopeOverrideMetadataKey = "agentRuntimeCapabilityScopeOverride";
    public const string TransientContextRequiredMetadataKey = "agentTransientContextRequired";
    public const string TransientContextDigestMetadataKey = "agentTransientContextDigest";
    public const int DefaultGovernedRepairAttempts = 1;
    public const int MaxRepairAttempts = 2;
    private const string ContextWorkspaceScopeKindPropertyName = "kind";
    private const string ContextWorkspaceScopeKeyPropertyName = "key";
    private const string ProjectStructureLaunchAgentIdPropertyName = "agentId";
    private const string ProjectStructureLaunchAgentNamePropertyName = "agentName";
    private const string ProjectStructureLaunchAgentMachineNamePropertyName = "machineName";
    private const string ProjectStructureLaunchAgentRepositoryRootPropertyName = "repositoryRoot";
    private const string ProjectStructureLaunchAgentBranchNamePropertyName = "branchName";
    private const string ProjectStructureLaunchAgentSessionIdPropertyName = "sessionId";
    private const string CurrentProcessRunNodeIdPropertyName = "currentProcessRunNodeId";
    private const string ProcessRunNodeIdPropertyName = "processRunNodeId";
    private const string ParentProcessRunNodeIdPropertyName = "parentProcessRunNodeId";
    private const string TargetProcessRunNodeIdPropertyName = "targetProcessRunNodeId";

    public static string Build(
        string? metadataJson,
        ExecutionInvocationPolicy? policy)
    {
        var metadata = ParseObject(metadataJson);
        if (policy is null)
        {
            return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
        }

        if (policy.FinalizerMode.HasValue)
        {
            metadata[AgentFinalizerPolicies.FinalizerModeMetadataKey] = AgentFinalizerPolicies.FormatMode(policy.FinalizerMode.Value);
        }

        if (policy.MaxStructuredOutputRepairAttempts.HasValue)
        {
            metadata[MaxStructuredOutputRepairAttemptsMetadataKey] = ClampRepairAttempts(policy.MaxStructuredOutputRepairAttempts.Value);
        }

        metadata[RequireStructuredOutputValidationMetadataKey] = policy.RequireStructuredOutputValidation;
        metadata[AllowRequiredFinalizerStructuredOutputRecoveryMetadataKey] =
            policy.AllowRequiredFinalizerStructuredOutputRecovery;
        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static string ApplyProcessCooperation(
        string? metadataJson,
        AgentProcessCooperationMetadata cooperationMetadata)
    {
        ArgumentNullException.ThrowIfNull(cooperationMetadata);

        var metadata = ParseObject(metadataJson);
        metadata[ProcessCooperationModeMetadataKey] = cooperationMetadata.CooperationMode.ToString();
        metadata[ProcessWorkspaceToolProfileMetadataKey] = AgentWorkspaceToolAccessProfiles.GetProfileKey(cooperationMetadata.WorkspaceToolProfile);
        metadata[ProcessCooperationSummaryMetadataKey] = cooperationMetadata.Summary.Trim();
        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static string ApplyContextWorkspaceScope(
        string? metadataJson,
        WorkspaceScopeDescriptor? scope)
    {
        var metadata = ParseObject(metadataJson);
        if (scope is null || scope.IsDefaultSandbox)
        {
            return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
        }

        metadata[ContextWorkspaceScopeMetadataKey] = new JsonObject
        {
            [ContextWorkspaceScopeKindPropertyName] = scope.Kind.ToString(),
            [ContextWorkspaceScopeKeyPropertyName] = scope.Key
        };
        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static string ApplyRuntimeToolProvidersEnabled(
        string? metadataJson,
        bool enabled)
    {
        var metadata = ParseObject(metadataJson);
        metadata[RuntimeToolProvidersEnabledMetadataKey] = enabled;
        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static string ApplyWorkspaceToolsEnabled(
        string? metadataJson,
        bool enabled)
    {
        var metadata = ParseObject(metadataJson);
        metadata[WorkspaceToolsEnabledMetadataKey] = enabled;
        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static string ApplyToolCapabilitiesEnabled(
        string? metadataJson,
        bool enabled)
    {
        var metadata = ParseObject(metadataJson);
        metadata[ToolCapabilitiesEnabledMetadataKey] = enabled;
        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static string ApplyRuntimeCapabilityScopeOverride(
        string? metadataJson,
        AgentRuntimeCapabilityScopeOverride? scopeOverride)
    {
        var metadata = ParseObject(metadataJson);
        if (scopeOverride is null || scopeOverride.IsEmpty)
        {
            metadata.Remove(RuntimeCapabilityScopeOverrideMetadataKey);
            return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
        }

        metadata[RuntimeCapabilityScopeOverrideMetadataKey] = JsonSerializer.SerializeToNode(
            scopeOverride,
            AgentOutputJson.SerializerOptions);
        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static string ApplyTransientContextRequirement(
        string? metadataJson,
        string digest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(digest);
        var normalizedDigest = digest.Trim();
        if (normalizedDigest.Length != 64 ||
            normalizedDigest.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "A transient context digest must be a SHA-256 hexadecimal value.",
                nameof(digest));
        }

        var metadata = ParseObject(metadataJson);
        metadata[TransientContextRequiredMetadataKey] = true;
        metadata[TransientContextDigestMetadataKey] = normalizedDigest.ToLowerInvariant();
        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static string ApplyProjectStructureLaunchAgent(
        string? metadataJson,
        ProjectStructureAgentIdentityDescriptor? launchAgent)
    {
        var metadata = ParseObject(metadataJson);
        if (launchAgent?.HasLeaseOwnerIdentity != true)
        {
            return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
        }

        metadata[ProjectStructureLaunchAgentMetadataKey] = new JsonObject
        {
            [ProjectStructureLaunchAgentIdPropertyName] = launchAgent.AgentId.Trim(),
            [ProjectStructureLaunchAgentNamePropertyName] = launchAgent.AgentName.Trim(),
            [ProjectStructureLaunchAgentMachineNamePropertyName] = launchAgent.MachineName.Trim(),
            [ProjectStructureLaunchAgentRepositoryRootPropertyName] = launchAgent.RepositoryRoot.Trim(),
            [ProjectStructureLaunchAgentBranchNamePropertyName] = launchAgent.BranchName.Trim(),
            [ProjectStructureLaunchAgentSessionIdPropertyName] = launchAgent.SessionId.Trim()
        };
        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static string ApplyProjectStructureProcessNodeContext(
        string? metadataJson,
        ProjectStructureProcessNodeContextDescriptor? context)
    {
        var metadata = ParseObject(metadataJson);
        if (context?.HasAnyProcessRunNode != true)
        {
            return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
        }

        metadata[ProjectStructureProcessNodeContextMetadataKey] = new JsonObject
        {
            [CurrentProcessRunNodeIdPropertyName] = context.CurrentProcessRunNodeId.Trim(),
            [ProcessRunNodeIdPropertyName] = context.ProcessRunNodeId.Trim(),
            [ParentProcessRunNodeIdPropertyName] = context.ParentProcessRunNodeId.Trim(),
            [TargetProcessRunNodeIdPropertyName] = context.TargetProcessRunNodeId.Trim()
        };
        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static string GroundPromptExternalTargetAliases(
        string? metadataJson,
        string? prompt,
        AgentWorkspaceToolAccessSettings? workspaceToolAccess,
        IExternalTargetPathRegistry externalTargetRegistry)
    {
        ArgumentNullException.ThrowIfNull(externalTargetRegistry);
        var metadata = ParseObject(metadataJson);
        var accessSettings = AgentWorkspaceToolAccessMetadata.Normalize(workspaceToolAccess ?? new AgentWorkspaceToolAccessSettings());
        if ((!accessSettings.CanReadFiles && !accessSettings.CanWriteFiles) ||
            string.IsNullOrWhiteSpace(prompt))
        {
            return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
        }

        if (metadata[AllowedExternalTargetAliasesMetadataKey] is JsonArray)
        {
            MergeExternalTargetAliases(
                metadata,
                AllowedExternalTargetAliasesMetadataKey,
                [],
                externalTargetRegistry);
        }

        if (metadata[ReadOnlyExternalTargetAliasesMetadataKey] is JsonArray)
        {
            MergeExternalTargetAliases(
                metadata,
                ReadOnlyExternalTargetAliasesMetadataKey,
                [],
                externalTargetRegistry);
        }

        var aliases = ExtractPromptExternalTargetAliases(prompt, externalTargetRegistry);
        if (aliases.Count > 0)
        {
            var targetMetadataKey = accessSettings.CanWriteFiles &&
                                    !HasProcessBoundaryMetadata(metadata)
                ? AllowedExternalTargetAliasesMetadataKey
                : ReadOnlyExternalTargetAliasesMetadataKey;
            MergeExternalTargetAliases(
                metadata,
                targetMetadataKey,
                aliases,
                externalTargetRegistry);
        }

        var groundedAliases = ReadExternalTargetAliases(metadata, AllowedExternalTargetAliasesMetadataKey)
            .Concat(ReadExternalTargetAliases(metadata, ReadOnlyExternalTargetAliasesMetadataKey))
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .ToArray();
        MergeExternalTargetRootBindings(
            metadata,
            externalTargetRegistry.ExportBindings(groundedAliases));
        RemoveWritableCoveredReadOnlyExternalTargetAliases(metadata);

        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static string ApplyReadOnlyExternalTargetAliases(
        string? metadataJson,
        IEnumerable<string>? pathsOrAliases)
    {
        var metadata = ParseObject(metadataJson);
        var aliases = pathsOrAliases?
            .Select(AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias)
            .Where(static alias => !string.IsNullOrWhiteSpace(alias))
            .Cast<string>()
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .Order(StringComparer.Ordinal)
            .ToArray() ?? [];
        if (aliases.Length == 0)
        {
            return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
        }

        MergeExternalTargetAliases(
            metadata,
            ReadOnlyExternalTargetAliasesMetadataKey,
            aliases);
        RemoveWritableCoveredReadOnlyExternalTargetAliases(metadata);
        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static string ApplyExternalTargetRootBindings(
        string? metadataJson,
        IReadOnlyList<ExternalTargetRootBinding>? bindings)
    {
        var metadata = ParseObject(metadataJson);
        if (bindings is { Count: > 0 })
        {
            if (bindings.Any(binding => !IsValidExternalTargetRootBinding(binding)))
            {
                throw new InvalidOperationException("An external-target root binding is malformed.");
            }

            MergeExternalTargetRootBindings(metadata, bindings);
        }

        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static string ProtectForUntrustedOutput(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return "{}";
        }

        JsonObject metadata;
        try
        {
            metadata = JsonNode.Parse(metadataJson) as JsonObject
                ?? throw new InvalidOperationException("Execution metadata must be a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Execution metadata is malformed.", exception);
        }

        metadata.Remove(ExternalTargetRootBindingsMetadataKey);
        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static IReadOnlyList<string> ExtractPromptExternalTargetAliases(
        string? prompt,
        IExternalTargetPathRegistry externalTargetRegistry)
    {
        ArgumentNullException.ThrowIfNull(externalTargetRegistry);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return [];
        }

        var aliases = new List<string>();
        for (var index = 0; index < prompt.Length; index++)
        {
            if (!IsPathCandidateStart(prompt, index))
            {
                continue;
            }

            var candidate = ReadPathCandidate(prompt, index, out var nextIndex);
            var alias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
                candidate,
                externalTargetRegistry);
            if (!string.IsNullOrWhiteSpace(alias))
            {
                aliases.Add(alias);
            }

            index = Math.Max(index, nextIndex - 1);
        }

        return aliases
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    public static int ResolveMaxStructuredOutputRepairAttempts(
        ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        var configured = TryReadInt32(run.MetadataJson, MaxStructuredOutputRepairAttemptsMetadataKey);
        if (configured.HasValue)
        {
            return ClampRepairAttempts(configured.Value);
        }

        return IsGovernedMachineCriticalRun(run)
            ? DefaultGovernedRepairAttempts
            : 0;
    }

    public static bool ResolveRequireStructuredOutputValidation(
        ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return TryReadBoolean(run.MetadataJson, RequireStructuredOutputValidationMetadataKey) ?? true;
    }

    public static bool ResolveAllowRequiredFinalizerStructuredOutputRecovery(
        ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return TryReadBoolean(
                   run.MetadataJson,
                   AllowRequiredFinalizerStructuredOutputRecoveryMetadataKey) ??
               false;
    }

    public static IReadOnlyList<string> ResolveAllowedExternalTargetAliases(
        ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return ResolveExternalTargetAliases(run, AllowedExternalTargetAliasesMetadataKey);
    }

    public static IReadOnlyList<string> ResolveReadOnlyExternalTargetAliases(
        ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return ResolveExternalTargetAliases(run, ReadOnlyExternalTargetAliasesMetadataKey);
    }

    public static IReadOnlyList<ExternalTargetRootBinding> ResolveExternalTargetRootBindings(
        ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (string.IsNullOrWhiteSpace(run.MetadataJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(run.MetadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Execution metadata must be a JSON object.");
            }

            if (!document.RootElement.TryGetProperty(ExternalTargetRootBindingsMetadataKey, out var value))
            {
                return [];
            }

            if (value.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("External-target root binding metadata must be a JSON array.");
            }

            return ValidateExternalTargetRootBindings(
                value.Deserialize<ExternalTargetRootBinding[]>(AgentOutputJson.SerializerOptions) ?? []);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "External-target root binding metadata is malformed.",
                exception);
        }
    }

    public static IReadOnlyList<string> ResolveAllowedManagedArtifactReadRefs(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!IsTrustedGovernedProcessRun(run))
        {
            return [];
        }

        return ResolveStringArray(run.MetadataJson, AllowedManagedArtifactReadRefsMetadataKey)
            .Select(WorkspaceScopeDescriptor.NormalizeRelativePath)
            .Where(path =>
                WorkspaceProcessRunArtifactPath.TryResolveRunId(path, out var runId, out var artifactSuffix) &&
                Guid.TryParse(runId, out _) &&
                !string.IsNullOrWhiteSpace(artifactSuffix))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(256)
            .ToArray();
    }

    public static AgentProcessCooperationMode? ResolveProcessCooperationMode(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!IsTrustedGovernedProcessRun(run))
        {
            return null;
        }

        var value = TryReadString(run.MetadataJson, ProcessCooperationModeMetadataKey);
        return Enum.TryParse<AgentProcessCooperationMode>(value, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    public static AgentWorkspaceToolProfileKind? ResolveProcessWorkspaceToolProfile(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!IsTrustedGovernedProcessRun(run))
        {
            return null;
        }

        var value = TryReadString(run.MetadataJson, ProcessWorkspaceToolProfileMetadataKey);
        return AgentWorkspaceToolAccessProfiles.TryParseProfileKey(value, out var parsed)
            ? parsed
            : null;
    }

    public static string ResolveProcessCooperationSummary(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return IsTrustedGovernedProcessRun(run)
            ? TryReadString(run.MetadataJson, ProcessCooperationSummaryMetadataKey)
            : string.Empty;
    }

    public static bool ResolveProcessBrowserToolsAllowed(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return !IsTrustedGovernedProcessRun(run) ||
               TryReadBoolean(run.MetadataJson, ProcessBrowserToolsAllowedMetadataKey) == true;
    }

    public static bool ResolveRuntimeToolProvidersEnabled(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return TryReadBoolean(run.MetadataJson, RuntimeToolProvidersEnabledMetadataKey) != false;
    }

    public static bool ResolveWorkspaceToolsEnabled(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return TryReadBoolean(run.MetadataJson, WorkspaceToolsEnabledMetadataKey) != false;
    }

    public static bool ResolveToolCapabilitiesEnabled(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return TryReadBoolean(run.MetadataJson, ToolCapabilitiesEnabledMetadataKey) != false;
    }

    public static bool RequiresTransientContext(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return TryReadBoolean(run.MetadataJson, TransientContextRequiredMetadataKey) == true;
    }

    public static string ResolveTransientContextDigest(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return TryReadString(run.MetadataJson, TransientContextDigestMetadataKey);
    }

    public static bool ResolveProcessAllowsProductMutation(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!IsTrustedGovernedProcessRun(run))
        {
            return true;
        }

        return ResolveProcessAllowsProductMutation(ParseObject(run.MetadataJson));
    }

    public static bool ResolveProcessRequiresProductMutationBeforeManagedOutput(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return IsTrustedGovernedProcessRun(run) &&
               TryReadBoolean(
                   run.MetadataJson,
                   ProcessStepRequiresProductMutationBeforeManagedOutputMetadataKey) == true;
    }

    public static IReadOnlyList<string> ResolveProcessProductMutationToolNames(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!IsTrustedGovernedProcessRun(run))
        {
            return [];
        }

        return ResolveStringArray(run.MetadataJson, ProcessProductMutationToolNamesMetadataKey)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> ResolveProcessProductMutationRequiredBranchOutcomeKeys(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!IsTrustedGovernedProcessRun(run))
        {
            return [];
        }

        return ResolveStringArray(run.MetadataJson, ProcessProductMutationRequiredBranchOutcomeKeysMetadataKey)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> ResolveProcessStepAllowedOperations(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!IsTrustedGovernedProcessRun(run))
        {
            return [];
        }

        return ResolveStringArray(run.MetadataJson, ProcessStepAllowedOperationsMetadataKey)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string ResolveProcessStepTargetScope(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return IsTrustedGovernedProcessRun(run)
            ? TryReadString(run.MetadataJson, ProcessStepTargetScopeMetadataKey)
            : string.Empty;
    }

    public static WorkspaceScopeDescriptor? ResolveContextWorkspaceScope(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return IsTrustedGovernedProcessRun(run)
            ? ResolveContextWorkspaceScope(run.MetadataJson)
            : null;
    }

    internal static RecordedContextWorkspaceScopeResolution
        ResolveRecordedContextWorkspaceScopeForReporting(
        ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return ResolveRecordedContextWorkspaceScope(run.MetadataJson);
    }

    public static ProjectStructureAgentIdentityDescriptor? ResolveProjectStructureLaunchAgent(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return IsTrustedGovernedProcessRun(run)
            ? ResolveProjectStructureLaunchAgent(run.MetadataJson)
            : null;
    }

    public static ProjectStructureProcessNodeContextDescriptor? ResolveProjectStructureProcessNodeContext(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return IsTrustedGovernedProcessRun(run)
            ? ResolveProjectStructureProcessNodeContext(run.MetadataJson)
            : null;
    }

    public static AgentRuntimeCapabilityScopeOverride? ResolveRuntimeCapabilityScopeOverride(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!IsTrustedGovernedProcessRun(run))
        {
            return null;
        }

        var scope = ReadTrustedRuntimeCapabilityScopeOverride(
            run.MetadataJson,
            RuntimeCapabilityScopeOverrideMetadataKey);
        return scope is { IsEmpty: false } ? scope : null;
    }

    private static JsonObject ParseObject(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(metadataJson) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static int ClampRepairAttempts(int value)
    {
        return Math.Clamp(value, 0, MaxRepairAttempts);
    }

    private static int? TryReadInt32(
        string? metadataJson,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty(propertyName, out var value) &&
                   value.TryGetInt32(out var parsed)
                ? parsed
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool? TryReadBoolean(
        string? metadataJson,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string TryReadString(
        string? metadataJson,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty(propertyName, out var value) &&
                   value.ValueKind == JsonValueKind.String
                ? value.GetString()?.Trim() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static AgentRuntimeCapabilityScopeOverride? ReadTrustedRuntimeCapabilityScopeOverride(
        string? metadataJson,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Trusted governed process metadata must be a JSON object.");
            }

            if (!document.RootElement.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.Deserialize<AgentRuntimeCapabilityScopeOverride>(AgentOutputJson.SerializerOptions)
                ?? throw new InvalidOperationException("Trusted governed process capability scope metadata deserialized to null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Trusted governed process capability scope metadata is malformed.",
                exception);
        }
    }

    private static bool ResolveProcessAllowsProductMutation(JsonObject metadata)
    {
        if (metadata[ProcessStepAllowsProductMutationMetadataKey] is JsonValue value &&
            value.TryGetValue<bool>(out var allowsProductMutation))
        {
            return allowsProductMutation;
        }

        if (metadata[ProcessStepAllowedOperationsMetadataKey] is JsonArray operations &&
            operations
                .Select(item => item?.GetValue<string>())
                .Any(item => string.Equals(item, "MutateProductTarget", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (metadata[ProcessStepTargetScopeMetadataKey] is JsonValue scopeValue &&
            scopeValue.TryGetValue<string>(out var scope))
        {
            return scope.Contains("Mutable", StringComparison.OrdinalIgnoreCase);
        }

        if (metadata[ProcessStepExecutionBoundaryMetadataKey] is JsonValue boundaryValue &&
            boundaryValue.TryGetValue<string>(out var boundary))
        {
            return string.Equals(boundary, "ProductMutation", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(boundary, "Recovery", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool HasProcessBoundaryMetadata(JsonObject metadata)
    {
        return metadata.ContainsKey(ProcessStepAllowsProductMutationMetadataKey) ||
               metadata.ContainsKey(ProcessStepRequiresProductMutationBeforeManagedOutputMetadataKey) ||
               metadata.ContainsKey(ProcessProductMutationRequiredBranchOutcomeKeysMetadataKey) ||
               metadata.ContainsKey(ProcessStepAllowedOperationsMetadataKey) ||
               metadata.ContainsKey(ProcessStepTargetScopeMetadataKey) ||
               metadata.ContainsKey(ProcessStepExecutionBoundaryMetadataKey);
    }

    private static IReadOnlyList<string> ResolveExternalTargetAliases(
        ExecutionRunRecord run,
        string metadataKey)
    {
        if (string.IsNullOrWhiteSpace(run.MetadataJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(run.MetadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(metadataKey, out var value) ||
                value.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return value
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .Where(item => item.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
                .Distinct(ExternalTargetAliasCodec.EqualityComparer)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> ResolveStringArray(
        string? metadataJson,
        string metadataKey)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(metadataKey, out var value) ||
                value.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return value
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static WorkspaceScopeDescriptor? ResolveContextWorkspaceScope(string? metadataJson)
        => ResolveRecordedContextWorkspaceScope(metadataJson).Scope;

    private static RecordedContextWorkspaceScopeResolution
        ResolveRecordedContextWorkspaceScope(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return RecordedContextWorkspaceScopeResolution.Missing;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return RecordedContextWorkspaceScopeResolution.Invalid;
            }

            if (!document.RootElement.TryGetProperty(
                    ContextWorkspaceScopeMetadataKey,
                    out var scopeElement))
            {
                return RecordedContextWorkspaceScopeResolution.Missing;
            }

            if (scopeElement.ValueKind != JsonValueKind.Object ||
                !scopeElement.TryGetProperty(ContextWorkspaceScopeKindPropertyName, out var kindElement) ||
                kindElement.ValueKind != JsonValueKind.String)
            {
                return RecordedContextWorkspaceScopeResolution.Invalid;
            }

            var kindValue = kindElement.GetString();
            if (!Enum.TryParse<WorkspaceScopeKind>(kindValue, ignoreCase: true, out var kind))
            {
                return RecordedContextWorkspaceScopeResolution.Invalid;
            }

            var key = scopeElement.TryGetProperty(ContextWorkspaceScopeKeyPropertyName, out var keyElement) &&
                      keyElement.ValueKind == JsonValueKind.String
                ? keyElement.GetString()
                : null;

            var scope = kind == WorkspaceScopeKind.Sandbox && string.IsNullOrWhiteSpace(key)
                ? WorkspaceScopeDescriptor.Sandbox
                : new WorkspaceScopeDescriptor(kind, key);
            return RecordedContextWorkspaceScopeResolution.Resolved(scope);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return RecordedContextWorkspaceScopeResolution.Invalid;
        }
    }

    private static ProjectStructureAgentIdentityDescriptor? ResolveProjectStructureLaunchAgent(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(ProjectStructureLaunchAgentMetadataKey, out var agentElement) ||
                agentElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var descriptor = new ProjectStructureAgentIdentityDescriptor(
                ReadObjectString(agentElement, ProjectStructureLaunchAgentIdPropertyName),
                ReadObjectString(agentElement, ProjectStructureLaunchAgentNamePropertyName),
                ReadObjectString(agentElement, ProjectStructureLaunchAgentMachineNamePropertyName),
                ReadObjectString(agentElement, ProjectStructureLaunchAgentRepositoryRootPropertyName),
                ReadObjectString(agentElement, ProjectStructureLaunchAgentBranchNamePropertyName),
                ReadObjectString(agentElement, ProjectStructureLaunchAgentSessionIdPropertyName));
            return descriptor.HasLeaseOwnerIdentity ? descriptor : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ProjectStructureProcessNodeContextDescriptor? ResolveProjectStructureProcessNodeContext(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(ProjectStructureProcessNodeContextMetadataKey, out var contextElement) ||
                contextElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var descriptor = new ProjectStructureProcessNodeContextDescriptor(
                ReadObjectString(contextElement, CurrentProcessRunNodeIdPropertyName),
                ReadObjectString(contextElement, ProcessRunNodeIdPropertyName),
                ReadObjectString(contextElement, ParentProcessRunNodeIdPropertyName),
                ReadObjectString(contextElement, TargetProcessRunNodeIdPropertyName));
            return descriptor.HasAnyProcessRunNode ? descriptor : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ReadObjectString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : string.Empty;
    }

    private static void MergeExternalTargetAliases(
        JsonObject metadata,
        string metadataKey,
        IReadOnlyList<string> aliases,
        IExternalTargetPathRegistry? externalTargetRegistry = null)
    {
        var mergedAliases = new List<string>();
        if (metadata[metadataKey] is JsonArray existingAliases)
        {
            mergedAliases.AddRange(existingAliases
                .Select(item => item?.GetValue<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => externalTargetRegistry is null
                    ? AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(item)
                    : AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
                        item,
                        externalTargetRegistry))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>());
        }

        mergedAliases.AddRange(aliases);
        metadata[metadataKey] = new JsonArray(
            mergedAliases
                .Distinct(ExternalTargetAliasCodec.EqualityComparer)
                .OrderBy(item => item, StringComparer.Ordinal)
                .Select(alias => JsonValue.Create(alias))
                .ToArray());
    }

    private static void MergeExternalTargetRootBindings(
        JsonObject metadata,
        IReadOnlyList<ExternalTargetRootBinding> bindings)
    {
        if (bindings.Count == 0)
        {
            return;
        }

        List<ExternalTargetRootBinding> mergedBindings;
        try
        {
            mergedBindings = metadata[ExternalTargetRootBindingsMetadataKey] is { } existingNode
                ? ValidateExternalTargetRootBindings(
                    existingNode.Deserialize<ExternalTargetRootBinding[]>(AgentOutputJson.SerializerOptions) ?? [])
                    .ToList()
                : [];
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "External-target root binding metadata is malformed.",
                exception);
        }

        foreach (var binding in ValidateExternalTargetRootBindings(bindings))
        {
            var existing = mergedBindings.FirstOrDefault(candidate =>
                string.Equals(candidate.RootId, binding.RootId, StringComparison.OrdinalIgnoreCase));
            if (existing is not null && existing != binding)
            {
                throw new InvalidOperationException(
                    $"Conflicting external-target root bindings use identity '{binding.RootId}'.");
            }

            if (existing is null)
            {
                mergedBindings.Add(binding);
            }
        }

        metadata[ExternalTargetRootBindingsMetadataKey] = JsonSerializer.SerializeToNode(
            mergedBindings.OrderBy(binding => binding.RootId, StringComparer.OrdinalIgnoreCase),
            AgentOutputJson.SerializerOptions);
    }

    private static bool IsValidExternalTargetRootBinding(ExternalTargetRootBinding? binding)
    {
        return binding is not null &&
               binding.RootId.Length == ExternalTargetAliasCodec.RootIdLength &&
               binding.RootId.All(Uri.IsHexDigit) &&
               !string.IsNullOrWhiteSpace(binding.HostPlatform) &&
               !string.IsNullOrWhiteSpace(binding.ProtectedRootToken);
    }

    private static IReadOnlyList<ExternalTargetRootBinding> ValidateExternalTargetRootBindings(
        IEnumerable<ExternalTargetRootBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        var normalized = bindings.ToArray();
        if (normalized.Any(binding => !IsValidExternalTargetRootBinding(binding)))
        {
            throw new InvalidOperationException("An external-target root binding is malformed.");
        }

        return normalized
            .GroupBy(binding => binding.RootId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Distinct().Count() == 1
                ? group.First()
                : throw new InvalidOperationException(
                    $"Conflicting external-target root bindings use identity '{group.Key}'."))
            .OrderBy(binding => binding.RootId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void RemoveWritableCoveredReadOnlyExternalTargetAliases(JsonObject metadata)
    {
        var writableAliases = ReadExternalTargetAliases(metadata, AllowedExternalTargetAliasesMetadataKey);
        if (writableAliases.Count == 0 ||
            metadata[ReadOnlyExternalTargetAliasesMetadataKey] is not JsonArray)
        {
            return;
        }

        var readOnlyAliases = ReadExternalTargetAliases(metadata, ReadOnlyExternalTargetAliasesMetadataKey);
        var filteredAliases = readOnlyAliases
            .Where(readOnlyAlias => !IsExternalTargetAliasCoveredByAny(readOnlyAlias, writableAliases))
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        if (filteredAliases.Length == 0)
        {
            metadata.Remove(ReadOnlyExternalTargetAliasesMetadataKey);
            return;
        }

        metadata[ReadOnlyExternalTargetAliasesMetadataKey] = new JsonArray(
            filteredAliases
                .Select(alias => JsonValue.Create(alias))
                .ToArray());
    }

    private static IReadOnlyList<string> ReadExternalTargetAliases(
        JsonObject metadata,
        string metadataKey)
    {
        if (metadata[metadataKey] is not JsonArray aliases)
        {
            return [];
        }

        return aliases
            .Select(item => item?.GetValue<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(item))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .ToArray();
    }

    private static bool IsExternalTargetAliasCoveredByAny(
        string alias,
        IReadOnlyList<string> roots)
    {
        return roots.Any(root => ExternalTargetAliasCodec.IsAliasWithinRoot(alias, root));
    }

    private static bool IsPathCandidateStart(string value, int index)
    {
        if (index < 0 || index >= value.Length)
        {
            return false;
        }

        if (index + 2 < value.Length &&
            char.IsLetter(value[index]) &&
            value[index + 1] == ':' &&
            (value[index + 2] == '\\' || value[index + 2] == '/'))
        {
            return index == 0 || !char.IsLetterOrDigit(value[index - 1]);
        }

        return value.AsSpan(index).StartsWith("external-target/", StringComparison.OrdinalIgnoreCase) &&
               (index == 0 || !char.IsLetterOrDigit(value[index - 1]));
    }

    private static string ReadPathCandidate(string value, int startIndex, out int nextIndex)
    {
        var terminator = startIndex > 0 && IsQuote(value[startIndex - 1])
            ? value[startIndex - 1]
            : '\0';

        var index = startIndex;
        while (index < value.Length)
        {
            var current = value[index];
            if (terminator != '\0')
            {
                if (current == terminator)
                {
                    break;
                }
            }
            else if (char.IsWhiteSpace(current) ||
                     current is '"' or '\'' or '`' or '<' or '>' or '|' or '?' or '*' or ',' or ';' or ')' or ']' or '}')
            {
                break;
            }

            index++;
        }

        nextIndex = index;
        return value[startIndex..index].Trim().TrimEnd('.', ',', ';', ':', ')', ']', '}');
    }

    private static bool IsQuote(char value)
        => value is '"' or '\'' or '`';

    private static bool IsGovernedMachineCriticalRun(ExecutionRunRecord run)
    {
        return string.Equals(run.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
               !string.IsNullOrWhiteSpace(run.ProcessRunId) ||
               !string.IsNullOrWhiteSpace(run.ProcessStepId);
    }

    private static bool IsTrustedGovernedProcessRun(ExecutionRunRecord run)
    {
        return string.Equals(run.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(run.RequestedByKind, "system", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(run.ProcessRunId) &&
               !string.IsNullOrWhiteSpace(run.ProcessStepId);
    }
}

internal readonly record struct RecordedContextWorkspaceScopeResolution(
    bool IsPresent,
    bool IsValid,
    WorkspaceScopeDescriptor? Scope)
{
    public static RecordedContextWorkspaceScopeResolution Missing { get; } =
        new(IsPresent: false, IsValid: true, Scope: null);

    public static RecordedContextWorkspaceScopeResolution Invalid { get; } =
        new(IsPresent: true, IsValid: false, Scope: null);

    public static RecordedContextWorkspaceScopeResolution Resolved(
        WorkspaceScopeDescriptor scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return new RecordedContextWorkspaceScopeResolution(
            IsPresent: true,
            IsValid: true,
            scope);
    }
}
