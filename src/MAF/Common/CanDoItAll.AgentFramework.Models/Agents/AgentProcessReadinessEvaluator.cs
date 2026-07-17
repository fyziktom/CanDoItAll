using System.Security.Cryptography;
using System.Text;
using AccessCapabilityIdentity = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityIdentity;
using AccessCapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;

namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentProcessRoleReadinessRequest(
    string StepKey,
    string StepTitle,
    string RoleKey,
    string RoleResourceKey,
    string RoleDisplayName,
    IReadOnlyList<string> AllowedOperations,
    string OperationTargetScope,
    IReadOnlyList<string>? RequiredRuntimeToolNames = null,
    IReadOnlyList<AccessCapabilityIdentity>? RequiredCapabilities = null,
    IReadOnlyList<string>? PreferredSpecializationTags = null);

public sealed record AgentProcessRoleReadinessResult(
    bool HasRoleFit,
    bool IsExecutionReady,
    int Score,
    string MatchSummary,
    string ReadinessSummary,
    string ReadinessHash,
    IReadOnlyList<AgentProcessReadinessFinding> Findings);

public sealed record AgentProcessReadinessFinding(
    AgentProcessReadinessFindingSeverity Severity,
    string Code,
    string Message);

public enum AgentProcessReadinessFindingSeverity
{
    Info,
    Warning,
    Error
}

public static class AgentProcessReadinessEvaluator
{
    private const int SemanticRoleMatchMinimumScore = 6;
    private const int PrimaryMetadataMatchBonus = 3;

    public static AgentProcessRoleReadinessResult Evaluate(
        AgentDefinition agent,
        AgentProcessRoleReadinessRequest request)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(request);

        var roleIdentityTokens = ResolveRoleIdentityTokens(request);
        var roleTokens = ResolveRoleTokens(request);
        var roleFamilyEligibilityTokens = ResolveRoleFamilyEligibilityTokens(request, roleIdentityTokens, roleTokens);
        var agentTerms = CollectAgentTerms(agent);
        var primaryTerms = CollectPrimaryAgentTerms(agent);
        var roleFamilyTerms = CollectRoleFamilyAgentTerms(agent);
        var exactMatch = CalculateBestExactRoleMatch(agent, ResolveMatchKeys(request));
        var findings = new List<AgentProcessReadinessFinding>();
        var roleFamilyFit = HasRequiredRoleFamilySignal(roleFamilyEligibilityTokens, roleFamilyTerms, primaryTerms);
        var roleFit = (exactMatch.Score > 0 || roleFamilyFit) && roleFamilyFit;
        var score = exactMatch.Score > 0 ? 1_000 + exactMatch.Score : 0;
        var matchedTokens = new List<string>();
        var specializationTerms = CollectAgentSpecializationTerms(agent);
        var matchedSpecializationTags = ResolvePreferredSpecializationTags(request)
            .Where(tag => TokenMatches(specializationTerms, tag))
            .ToArray();

        if (!roleFit)
        {
            findings.Add(new AgentProcessReadinessFinding(
                AgentProcessReadinessFindingSeverity.Error,
                "agent.readiness.role-family-mismatch",
                $"Agent '{agent.Name}' is not a role-family match for role '{ResolveRoleLabel(request)}'."));
        }
        else if (exactMatch.Score == 0)
        {
            foreach (var roleToken in roleTokens)
            {
                if (!TokenMatches(agentTerms, roleToken))
                {
                    continue;
                }

                matchedTokens.Add(roleToken);
                score += ScoreRoleToken(roleToken);
                if (TokenMatches(primaryTerms, roleToken))
                {
                    score += PrimaryMetadataMatchBonus;
                }
            }

            score += ScoreWorkloadFit(agent.Workload, roleTokens);
            if (score < SemanticRoleMatchMinimumScore || matchedTokens.Count == 0)
            {
                roleFit = false;
                findings.Add(new AgentProcessReadinessFinding(
                    AgentProcessReadinessFindingSeverity.Error,
                    "agent.readiness.role-signal-too-weak",
                    $"Agent '{agent.Name}' has no strong role signal for role '{ResolveRoleLabel(request)}'."));
            }
        }

        AddToolReadinessFindings(agent, request, roleTokens, findings);
        AddRequiredCapabilityReadinessFindings(agent, request, findings);
        score += matchedSpecializationTags.Length * 50;

        var hasErrors = findings.Any(finding => finding.Severity == AgentProcessReadinessFindingSeverity.Error);
        var roleMatchSummary = exactMatch.Score > 0
            ? $"exact role metadata for '{exactMatch.MatchKey}'"
            : matchedTokens.Count == 0
                ? "no role metadata match"
                : $"semantic role match on {string.Join(", ", matchedTokens)}";
        var matchSummary = matchedSpecializationTags.Length == 0
            ? roleMatchSummary
            : $"{roleMatchSummary} plus preferred specialization {string.Join(", ", matchedSpecializationTags)}";
        var readinessSummary = hasErrors
            ? string.Join(" ", findings
                .Where(finding => finding.Severity == AgentProcessReadinessFindingSeverity.Error)
                .Select(finding => finding.Message))
            : "Agent role family, workspace tool readiness, and project-structure tool readiness satisfy the step operation contract.";
        var readinessHash = ComputeHash(string.Join(
            "|",
            agent.Id.ToString("D"),
            request.StepKey,
            request.RoleKey,
            request.RoleResourceKey,
            request.OperationTargetScope,
            string.Join(",", request.AllowedOperations.OrderBy(item => item, StringComparer.Ordinal)),
            string.Join(",", NormalizeRequiredRuntimeToolNames(request.RequiredRuntimeToolNames)),
            string.Join(",", NormalizeRequiredCapabilities(request.RequiredCapabilities).Select(FormatCapabilityIdentity)),
            string.Join(",", ResolvePreferredSpecializationTags(request)),
            readinessSummary));

        return new AgentProcessRoleReadinessResult(
            roleFit,
            !hasErrors,
            Math.Max(0, score),
            matchSummary,
            readinessSummary,
            readinessHash,
            findings);
    }

    public static IReadOnlyList<string> ResolveWorkspaceToolNames(AgentDefinition agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var settings = AgentWorkspaceToolAccessMetadata.Read(agent.ConfigurationJson);
        var names = new List<string>();
        AddIf(names, settings.CanReadFiles, "workspace-read-files");
        AddIf(names, settings.CanWriteFiles, "workspace-write-files");
        AddIf(names, settings.CanRunValidationCommands, "workspace-dotnet-validation");
        AddIf(names, settings.CanRunLocalScripts, "workspace-local-scripts");
        AddIf(names, settings.CanScaffoldProjects, "workspace-dotnet-scaffold");
        AddIf(names, settings.CanManageWorkspacePaths, "workspace-manage-paths");
        AddIf(names, settings.CanTransformArtifacts, "workspace-artifact-transform");
        return names;
    }

    private static void AddToolReadinessFindings(
        AgentDefinition agent,
        AgentProcessRoleReadinessRequest request,
        IReadOnlyList<string> roleTokens,
        List<AgentProcessReadinessFinding> findings)
    {
        var settings = AgentWorkspaceToolAccessMetadata.Read(agent.ConfigurationJson);
        var normalized = AgentWorkspaceToolAccessMetadata.Normalize(settings);
        var requiresTools = request.AllowedOperations.Count > 0;
        if (requiresTools && !agent.Permissions.CanUseTools)
        {
            findings.Add(new AgentProcessReadinessFinding(
                AgentProcessReadinessFindingSeverity.Error,
                "agent.readiness.tools-disabled",
                $"Agent '{agent.Name}' cannot use tools, but step '{request.StepKey}' requires process operation tools."));
        }

        if (RequiresReadFiles(request) && !normalized.CanReadFiles)
        {
            findings.Add(MissingTool(agent, request, "agent.readiness.workspace-read-files-missing", "read workspace files"));
        }

        if (RequiresWriteFiles(request) && !normalized.CanWriteFiles)
        {
            findings.Add(MissingTool(agent, request, "agent.readiness.workspace-write-files-missing", "write workspace files"));
        }

        if (RequiresValidationCommands(request, roleTokens) && !normalized.CanRunValidationCommands)
        {
            findings.Add(MissingTool(agent, request, "agent.readiness.workspace-validation-missing", "run .NET validation/runtime commands"));
        }

        if (RequiresEngineeringScaffoldTools(request, roleTokens) && !normalized.CanScaffoldProjects)
        {
            findings.Add(MissingTool(agent, request, "agent.readiness.workspace-scaffold-missing", "scaffold .NET projects"));
        }

        AddRequiredRuntimeToolReadinessFindings(agent, request, normalized, findings);
    }

    private static void AddRequiredRuntimeToolReadinessFindings(
        AgentDefinition agent,
        AgentProcessRoleReadinessRequest request,
        AgentWorkspaceToolAccessSettings normalized,
        List<AgentProcessReadinessFinding> findings)
    {
        foreach (var requiredToolName in NormalizeRequiredRuntimeToolNames(request.RequiredRuntimeToolNames))
        {
            if (IsProjectRuntimeToolName(requiredToolName))
            {
                AddRequiredProjectStructureToolReadinessFindings(agent, request, requiredToolName, findings);
                continue;
            }

            if (IsBrowserRuntimeToolName(requiredToolName))
            {
                AddRequiredBrowserToolReadinessFindings(agent, request, requiredToolName, findings);
                continue;
            }

            if (!AgentWorkspaceToolAccessMetadata.TryResolveWorkspaceToolPermission(requiredToolName, out var permission))
            {
                if (requiredToolName.StartsWith("workspace_", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new AgentProcessReadinessFinding(
                        AgentProcessReadinessFindingSeverity.Error,
                        "agent.readiness.required-workspace-tool-unknown",
                        $"Step '{request.StepKey}' requires workspace tool '{requiredToolName}', but readiness cannot map that tool to a workspace permission."));
                }

                continue;
            }

            if (HasWorkspacePermission(normalized, permission))
            {
                if (HasRequiredWorkspaceRuntimeToolCapability(agent, requiredToolName))
                {
                    continue;
                }

                findings.Add(new AgentProcessReadinessFinding(
                    AgentProcessReadinessFindingSeverity.Error,
                    "agent.readiness.required-tool-capability-missing",
                    $"Step '{request.StepKey}' requires workspace tool '{requiredToolName}', but agent '{agent.Name}' does not have the matching capability assignment."));
                continue;
            }

            var summary = FormatWorkspaceToolPermission(permission);
            findings.Add(MissingTool(
                agent,
                request,
                $"agent.readiness.required-tool-{summary.Code}-missing",
                $"{summary.Description} for required runtime tool '{requiredToolName}'"));
        }
    }

    private static void AddRequiredCapabilityReadinessFindings(
        AgentDefinition agent,
        AgentProcessRoleReadinessRequest request,
        List<AgentProcessReadinessFinding> findings)
    {
        var workspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(
            AgentWorkspaceToolAccessMetadata.Read(agent.ConfigurationJson));
        foreach (var requiredCapability in NormalizeRequiredCapabilities(request.RequiredCapabilities))
        {
            if (HasRequiredCapability(agent, workspaceToolAccess, requiredCapability))
            {
                continue;
            }

            findings.Add(new AgentProcessReadinessFinding(
                AgentProcessReadinessFindingSeverity.Error,
                "agent.readiness.required-capability-missing",
                $"Step '{request.StepKey}' requires {FormatCapabilityKind(requiredCapability.Kind)} capability '{requiredCapability.Key.Value}', but agent '{agent.Name}' does not expose it in the process step capability scope."));
        }
    }

    private static bool HasRequiredCapability(
        AgentDefinition agent,
        AgentWorkspaceToolAccessSettings workspaceToolAccess,
        AccessCapabilityIdentity requiredCapability)
    {
        if (requiredCapability.Kind == AccessCapabilityKind.Tool &&
            TryResolveRuntimeToolNameFromCapabilityKey(requiredCapability.Key.Value, out var runtimeToolName) &&
            AgentWorkspaceToolAccessMetadata.TryResolveWorkspaceToolPermission(runtimeToolName, out var permission))
        {
            return HasWorkspacePermission(workspaceToolAccess, permission);
        }

        return TryMapCapabilityKind(requiredCapability.Kind, out var modelKind) &&
               agent.Capabilities.Any(capability =>
                   capability.Kind == modelKind &&
                   CapabilityKeyMatches(capability.CapabilityKey, requiredCapability.Key.Value));
    }

    private static bool TryResolveRuntimeToolNameFromCapabilityKey(
        string capabilityKey,
        out string runtimeToolName)
    {
        runtimeToolName = string.IsNullOrWhiteSpace(capabilityKey)
            ? string.Empty
            : capabilityKey.Trim().Replace('-', '_');
        return !string.IsNullOrWhiteSpace(runtimeToolName);
    }

    private static bool TryMapCapabilityKind(
        AccessCapabilityKind accessKind,
        out CapabilityKind modelKind)
    {
        modelKind = default;
        return accessKind switch
        {
            AccessCapabilityKind.Skill => SetMappedKind(CapabilityKind.Skill, out modelKind),
            AccessCapabilityKind.Tool => SetMappedKind(CapabilityKind.Tool, out modelKind),
            AccessCapabilityKind.McpServer => SetMappedKind(CapabilityKind.McpServer, out modelKind),
            AccessCapabilityKind.Plugin => SetMappedKind(CapabilityKind.Plugin, out modelKind),
            AccessCapabilityKind.Rag => SetMappedKind(CapabilityKind.Rag, out modelKind),
            AccessCapabilityKind.AiContext => SetMappedKind(CapabilityKind.AiContext, out modelKind),
            AccessCapabilityKind.Memory => SetMappedKind(CapabilityKind.Memory, out modelKind),
            _ => false
        };
    }

    private static bool SetMappedKind(
        CapabilityKind value,
        out CapabilityKind mapped)
    {
        mapped = value;
        return true;
    }

    private static bool CapabilityKeyMatches(
        string assignedKey,
        string requiredKey)
    {
        if (string.IsNullOrWhiteSpace(assignedKey) ||
            string.IsNullOrWhiteSpace(requiredKey))
        {
            return false;
        }

        return string.Equals(assignedKey.Trim(), requiredKey.Trim(), StringComparison.OrdinalIgnoreCase) ||
               string.Equals(assignedKey.Trim().Replace('_', '-'), requiredKey.Trim().Replace('_', '-'), StringComparison.OrdinalIgnoreCase);
    }

    private static void AddRequiredBrowserToolReadinessFindings(
        AgentDefinition agent,
        AgentProcessRoleReadinessRequest request,
        string requiredToolName,
        List<AgentProcessReadinessFinding> findings)
    {
        if (HasRequiredBrowserRuntimeToolCapability(agent, requiredToolName))
        {
            return;
        }

        findings.Add(new AgentProcessReadinessFinding(
            AgentProcessReadinessFindingSeverity.Error,
            "agent.readiness.required-browser-tool-missing",
            $"Step '{request.StepKey}' requires browser runtime tool '{requiredToolName}', but agent '{agent.Name}' does not have a Playwright/browser MCP capability assignment that can expose it."));
    }

    private static void AddRequiredProjectStructureToolReadinessFindings(
        AgentDefinition agent,
        AgentProcessRoleReadinessRequest request,
        string requiredToolName,
        List<AgentProcessReadinessFinding> findings)
    {
        var access = AgentProjectStructureAccessMetadata.Normalize(
            AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson));

        if (ProjectCreationRuntimeToolNames.Contains(requiredToolName))
        {
            if (!access.CanCreateProjects)
            {
                findings.Add(new AgentProcessReadinessFinding(
                    AgentProcessReadinessFindingSeverity.Error,
                    "agent.readiness.required-project-create-missing",
                    $"Step '{request.StepKey}' requires standalone project creation tool '{requiredToolName}', but agent '{agent.Name}' does not have project creation access."));
            }

            return;
        }

        if (SubprojectCreationRuntimeToolNames.Contains(requiredToolName))
        {
            if (!access.CanCreateSubprojects)
            {
                findings.Add(new AgentProcessReadinessFinding(
                    AgentProcessReadinessFindingSeverity.Error,
                    "agent.readiness.required-subproject-create-missing",
                    $"Step '{request.StepKey}' requires subproject creation tool '{requiredToolName}', but agent '{agent.Name}' does not have subproject creation access."));
            }

            return;
        }

        if (SubprojectStructureMutationRuntimeToolNames.Contains(requiredToolName))
        {
            if (!access.CanCreateSubprojects || (!access.CanWrite && !access.CanWriteNonTaskStructure))
            {
                findings.Add(new AgentProcessReadinessFinding(
                    AgentProcessReadinessFindingSeverity.Error,
                    "agent.readiness.required-subproject-structure-write-missing",
                    $"Step '{request.StepKey}' requires subproject creation plus project-structure write access for tool '{requiredToolName}', but agent '{agent.Name}' does not have both permissions."));
            }

            return;
        }

        if (ProjectTaskMutationRuntimeToolNames.Contains(requiredToolName))
        {
            if (!access.CanWrite && !access.CanWriteTasks)
            {
                findings.Add(new AgentProcessReadinessFinding(
                    AgentProcessReadinessFindingSeverity.Error,
                    "agent.readiness.required-project-task-write-missing",
                    $"Step '{request.StepKey}' requires project-task mutation tool '{requiredToolName}', but agent '{agent.Name}' does not have project-task write access."));
            }

            return;
        }

        if (ProjectStructureBroadMutationRuntimeToolNames.Contains(requiredToolName))
        {
            if (!access.CanWrite)
            {
                findings.Add(new AgentProcessReadinessFinding(
                    AgentProcessReadinessFindingSeverity.Error,
                    "agent.readiness.required-project-structure-full-write-missing",
                    $"Step '{request.StepKey}' requires unrestricted project-structure mutation tool '{requiredToolName}', but agent '{agent.Name}' does not have full project-structure write access."));
            }

            return;
        }

        if (ProjectStructureMutationRuntimeToolNames.Contains(requiredToolName))
        {
            if (!access.CanWrite && !access.CanWriteNonTaskStructure)
            {
                findings.Add(new AgentProcessReadinessFinding(
                    AgentProcessReadinessFindingSeverity.Error,
                    "agent.readiness.required-project-structure-write-missing",
                    $"Step '{request.StepKey}' requires project-structure mutation tool '{requiredToolName}', but agent '{agent.Name}' does not have project-structure write access."));
            }

            return;
        }

        if (ProjectStructureReadRuntimeToolNames.Contains(requiredToolName))
        {
            if (!access.CanRead)
            {
                findings.Add(new AgentProcessReadinessFinding(
                    AgentProcessReadinessFindingSeverity.Error,
                    "agent.readiness.required-project-structure-read-missing",
                    $"Step '{request.StepKey}' requires project-structure read tool '{requiredToolName}', but agent '{agent.Name}' does not have project-structure read access."));
            }

            return;
        }

        findings.Add(new AgentProcessReadinessFinding(
            AgentProcessReadinessFindingSeverity.Error,
            "agent.readiness.required-project-structure-tool-unknown",
            $"Step '{request.StepKey}' requires project-structure tool '{requiredToolName}', but readiness cannot classify the tool as read-only or mutating."));
    }

    private static bool HasRequiredWorkspaceRuntimeToolCapability(AgentDefinition agent, string requiredToolName)
    {
        if (string.IsNullOrWhiteSpace(requiredToolName) ||
            !requiredToolName.StartsWith("workspace_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedToolName = requiredToolName.Trim().Replace('-', '_');
        var normalizedCapabilityKey = normalizedToolName.Replace('_', '-');
        return agent.Capabilities.Any(capability =>
            string.Equals(capability.CapabilityKey, normalizedCapabilityKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(capability.CapabilityKey.Replace('-', '_'), normalizedToolName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasRequiredBrowserRuntimeToolCapability(AgentDefinition agent, string requiredToolName)
    {
        var normalizedToolName = requiredToolName.Trim().Replace('-', '_');
        var normalizedToolKey = normalizedToolName.Replace('_', '-');
        return agent.Capabilities.Any(capability =>
            capability.Kind switch
            {
                CapabilityKind.McpServer => IsBrowserMcpServerCapability(capability.CapabilityKey),
                CapabilityKind.Tool => CapabilityKeyMatchesTool(capability.CapabilityKey, normalizedToolName, normalizedToolKey),
                _ => false
            });
    }

    private static bool IsBrowserMcpServerCapability(string capabilityKey)
    {
        return capabilityKey.Contains("playwright", StringComparison.OrdinalIgnoreCase) ||
               capabilityKey.Contains("browser-mcp", StringComparison.OrdinalIgnoreCase) ||
               capabilityKey.Contains("browser_mcp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool CapabilityKeyMatchesTool(
        string capabilityKey,
        string normalizedToolName,
        string normalizedToolKey)
    {
        var keyWithUnderscores = capabilityKey.Replace('-', '_');
        return string.Equals(capabilityKey, normalizedToolKey, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(keyWithUnderscores, normalizedToolName, StringComparison.OrdinalIgnoreCase) ||
               keyWithUnderscores.EndsWith($"_{normalizedToolName}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBrowserRuntimeToolName(string toolName)
        => toolName.StartsWith("browser_", StringComparison.OrdinalIgnoreCase);

    private static bool IsProjectRuntimeToolName(string toolName)
    {
        return toolName.StartsWith("project_structure_", StringComparison.OrdinalIgnoreCase) ||
               toolName.StartsWith("project_task_", StringComparison.OrdinalIgnoreCase) ||
               toolName.StartsWith("project_plan_", StringComparison.OrdinalIgnoreCase);
    }

    private static AgentProcessReadinessFinding MissingTool(
        AgentDefinition agent,
        AgentProcessRoleReadinessRequest request,
        string code,
        string capability)
    {
        return new AgentProcessReadinessFinding(
            AgentProcessReadinessFindingSeverity.Error,
            code,
            $"Agent '{agent.Name}' cannot {capability}, but step '{request.StepKey}' requires it.");
    }

    private static bool RequiresReadFiles(AgentProcessRoleReadinessRequest request)
    {
        return request.AllowedOperations.Count > 0 ||
               IsTargetScope(request, ProcessOperationContractNames.ExternalProductTargetReadOnly) ||
               IsTargetScope(request, ProcessOperationContractNames.ExternalProductTargetMutable) ||
               IsTargetScope(request, ProcessOperationContractNames.ManagedOutputProduct);
    }

    private static bool RequiresWriteFiles(AgentProcessRoleReadinessRequest request)
    {
        return HasOperation(request, ProcessOperationContractNames.WriteManagedProcessArtifacts) ||
               HasOperation(request, ProcessOperationContractNames.WriteExternalArtifactDestination) ||
               HasOperation(request, ProcessOperationContractNames.MutateProductTarget) ||
               IsTargetScope(request, ProcessOperationContractNames.ExternalProductTargetMutable) ||
               IsTargetScope(request, ProcessOperationContractNames.ManagedOutputProduct);
    }

    private static bool RequiresValidationCommands(
        AgentProcessRoleReadinessRequest request,
        IReadOnlyList<string> roleTokens)
    {
        return HasOperation(request, ProcessOperationContractNames.RunValidation) ||
               ((HasOperation(request, ProcessOperationContractNames.LaunchRuntime) ||
                 HasOperation(request, ProcessOperationContractNames.CaptureRuntimeProof)) &&
                (roleTokens.Any(IsEngineeringRoleToken) ||
                 roleTokens.Any(IsQualityRoleToken) ||
                 IsTargetScope(request, ProcessOperationContractNames.ExternalProductTargetMutable) ||
                 IsTargetScope(request, ProcessOperationContractNames.ExternalProductTargetReadOnly) ||
                 IsTargetScope(request, ProcessOperationContractNames.ManagedOutputProduct)));
    }

    private static bool RequiresEngineeringScaffoldTools(
        AgentProcessRoleReadinessRequest request,
        IReadOnlyList<string> roleTokens)
    {
        return HasOperation(request, ProcessOperationContractNames.MutateProductTarget) &&
               (roleTokens.Any(IsEngineeringRoleToken) ||
                roleTokens.Any(IsTechnologyRoleToken));
    }

    private static bool HasOperation(AgentProcessRoleReadinessRequest request, string operation)
    {
        return request.AllowedOperations.Contains(operation, StringComparer.Ordinal);
    }

    private static bool HasWorkspacePermission(
        AgentWorkspaceToolAccessSettings normalized,
        AgentWorkspaceToolPermissionKind permission)
    {
        return permission switch
        {
            AgentWorkspaceToolPermissionKind.ReadFiles => normalized.CanReadFiles,
            AgentWorkspaceToolPermissionKind.WriteFiles => normalized.CanWriteFiles,
            AgentWorkspaceToolPermissionKind.ManagePaths => normalized.CanManageWorkspacePaths,
            AgentWorkspaceToolPermissionKind.RunValidationCommands => normalized.CanRunValidationCommands,
            AgentWorkspaceToolPermissionKind.ScaffoldProjects => normalized.CanScaffoldProjects,
            AgentWorkspaceToolPermissionKind.RunLocalScripts => normalized.CanRunLocalScripts,
            AgentWorkspaceToolPermissionKind.TransformArtifacts => normalized.CanTransformArtifacts,
            _ => false
        };
    }

    private static (string Code, string Description) FormatWorkspaceToolPermission(AgentWorkspaceToolPermissionKind permission)
    {
        return permission switch
        {
            AgentWorkspaceToolPermissionKind.ReadFiles => ("read-files", "read workspace files"),
            AgentWorkspaceToolPermissionKind.WriteFiles => ("write-files", "write workspace files"),
            AgentWorkspaceToolPermissionKind.ManagePaths => ("manage-paths", "manage workspace paths"),
            AgentWorkspaceToolPermissionKind.RunValidationCommands => ("validation", "run workspace validation commands"),
            AgentWorkspaceToolPermissionKind.ScaffoldProjects => ("scaffold", "scaffold workspace projects"),
            AgentWorkspaceToolPermissionKind.RunLocalScripts => ("local-scripts", "run workspace local scripts"),
            AgentWorkspaceToolPermissionKind.TransformArtifacts => ("artifact-transform", "transform or analyze workspace artifacts"),
            _ => ("workspace-permission", "use workspace tools")
        };
    }

    private static IReadOnlyList<string> NormalizeRequiredRuntimeToolNames(IReadOnlyList<string>? requiredRuntimeToolNames)
    {
        return requiredRuntimeToolNames?
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .Select(toolName => toolName.Trim().Replace('-', '_'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(toolName => toolName, StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
    }

    private static IReadOnlyList<AccessCapabilityIdentity> NormalizeRequiredCapabilities(
        IReadOnlyList<AccessCapabilityIdentity>? requiredCapabilities)
    {
        return requiredCapabilities?
            .Where(item => !string.IsNullOrWhiteSpace(item.Key.Value))
            .Distinct()
            .OrderBy(FormatCapabilityIdentity, StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
    }

    private static string FormatCapabilityIdentity(AccessCapabilityIdentity identity)
        => $"{FormatCapabilityKind(identity.Kind)}:{identity.Key.Value}";

    private static string FormatCapabilityKind(AccessCapabilityKind kind)
        => kind.ToString();

    private static bool IsTargetScope(AgentProcessRoleReadinessRequest request, string targetScope)
    {
        return string.Equals(request.OperationTargetScope, targetScope, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ResolveRoleTokens(AgentProcessRoleReadinessRequest request)
    {
        return ResolveRoleIdentityTokens(request)
            .Concat(ExtractTokens(request.StepTitle))
            .Where(token => !IgnoredRoleTokens.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolvePreferredSpecializationTags(
        AgentProcessRoleReadinessRequest request)
        => (request.PreferredSpecializationTags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .SelectMany(ExtractTokens)
            .Where(tag => !IgnoredRoleTokens.Contains(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<string> ResolveRoleIdentityTokens(AgentProcessRoleReadinessRequest request)
    {
        return ResolveRoleIdentityKeys(request)
            .SelectMany(ExtractTokens)
            .Where(token => !IgnoredRoleTokens.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveRoleIdentityKeys(AgentProcessRoleReadinessRequest request)
    {
        return new[]
            {
                request.RoleKey,
                request.RoleResourceKey
            }
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveRoleFamilyEligibilityTokens(
        AgentProcessRoleReadinessRequest request,
        IReadOnlyList<string> roleIdentityTokens,
        IReadOnlyList<string> roleTokens)
    {
        var tokens = new List<string>(roleIdentityTokens);
        if (RequiresProductEngineeringRole(request, roleTokens))
        {
            tokens.AddRange(roleTokens.Where(token =>
                IsEngineeringRoleToken(token) ||
                IsTechnologyRoleToken(token)));
        }

        return tokens
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool RequiresProductEngineeringRole(
        AgentProcessRoleReadinessRequest request,
        IReadOnlyList<string> roleTokens)
    {
        return (HasOperation(request, ProcessOperationContractNames.MutateProductTarget) ||
                IsTargetScope(request, ProcessOperationContractNames.ExternalProductTargetMutable)) &&
               (roleTokens.Any(IsEngineeringRoleToken) ||
                roleTokens.Any(IsTechnologyRoleToken));
    }

    private static IReadOnlyList<string> ResolveMatchKeys(AgentProcessRoleReadinessRequest request)
    {
        return new[]
            {
                request.RoleKey,
                request.RoleResourceKey,
                request.RoleDisplayName
            }
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveRoleLabel(AgentProcessRoleReadinessRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.RoleDisplayName))
        {
            return request.RoleDisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.RoleKey))
        {
            return request.RoleKey.Trim();
        }

        return request.StepKey.Trim();
    }

    private static HashSet<string> CollectAgentTerms(AgentDefinition agent)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddTerms(terms, agent.Name);
        AddTerms(terms, agent.RoleTitle);
        AddTerms(terms, agent.Summary);
        AddTerms(terms, agent.Workload.ToString());
        AddTerms(terms, AgentWorkspaceToolAccessProfiles.GetProfileKey(
            AgentWorkspaceToolAccessMetadata.Read(agent.ConfigurationJson).Profile));

        foreach (var tag in agent.Tags)
        {
            AddTerms(terms, tag);
            var normalizedTag = Normalize(tag);
            if (!string.IsNullOrWhiteSpace(normalizedTag))
            {
                terms.Add(normalizedTag);
            }
        }

        foreach (var capability in agent.Capabilities)
        {
            AddTerms(terms, capability.CapabilityKey);
        }

        return terms;
    }

    private static HashSet<string> CollectPrimaryAgentTerms(AgentDefinition agent)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddTerms(terms, agent.Name);
        AddTerms(terms, agent.RoleTitle);
        return terms;
    }

    private static HashSet<string> CollectRoleFamilyAgentTerms(AgentDefinition agent)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddTerms(terms, agent.Name);
        AddTerms(terms, agent.RoleTitle);
        AddTerms(terms, agent.Workload.ToString());

        foreach (var tag in agent.Tags)
        {
            AddTerms(terms, tag);
            var normalizedTag = Normalize(tag);
            if (!string.IsNullOrWhiteSpace(normalizedTag))
            {
                terms.Add(normalizedTag);
            }
        }

        return terms;
    }

    private static HashSet<string> CollectAgentSpecializationTerms(AgentDefinition agent)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddTerms(terms, agent.Name);
        AddTerms(terms, agent.RoleTitle);
        foreach (var tag in agent.Tags)
        {
            AddTerms(terms, tag);
            var normalizedTag = Normalize(tag);
            if (!string.IsNullOrWhiteSpace(normalizedTag))
            {
                terms.Add(normalizedTag);
            }
        }

        return terms;
    }

    private static ExactRoleMatch CalculateBestExactRoleMatch(
        AgentDefinition agent,
        IReadOnlyList<string> matchKeys)
    {
        var bestMatch = ExactRoleMatch.NoMatch;
        foreach (var matchKey in matchKeys)
        {
            var exactScore = CalculateExactRoleScore(agent, matchKey);
            if (exactScore > bestMatch.Score)
            {
                bestMatch = new ExactRoleMatch(matchKey, exactScore);
            }
        }

        return bestMatch;
    }

    private static int CalculateExactRoleScore(AgentDefinition agent, string roleKey)
    {
        var score = 0;
        if (agent.Tags.Contains($"process-mock-role:{roleKey}", StringComparer.OrdinalIgnoreCase))
        {
            score += 30;
        }

        if (agent.Tags.Contains(roleKey, StringComparer.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (ContainsNormalized(agent.RoleTitle, roleKey) || ContainsNormalized(agent.Name, roleKey))
        {
            score += 10;
        }

        var roleTokens = ExtractTokens(roleKey)
            .Where(token => !IgnoredRoleTokens.Contains(token))
            .ToArray();
        if (roleTokens.Length >= 2)
        {
            var primaryTerms = CollectPrimaryAgentTerms(agent);
            if (roleTokens.All(primaryTerms.Contains))
            {
                score += 8;
            }
        }

        return score;
    }

    private static bool HasRequiredRoleFamilySignal(
        IReadOnlyList<string> roleTokens,
        IReadOnlySet<string> terms,
        IReadOnlySet<string> primaryTerms)
    {
        var hasRoleFamilyRequirement = false;

        if (roleTokens.Any(IsArchitectureRoleToken))
        {
            hasRoleFamilyRequirement = true;
            if (!MatchesAnyTerm(primaryTerms, ArchitectureRoleAliases))
            {
                return false;
            }
        }

        if (roleTokens.Any(IsEngineeringRoleToken))
        {
            hasRoleFamilyRequirement = true;
            if (!MatchesAnyTerm(primaryTerms, EngineeringRoleAliases))
            {
                return false;
            }
        }

        if (roleTokens.Any(IsQualityRoleToken))
        {
            hasRoleFamilyRequirement = true;
            if (!MatchesAnyTerm(primaryTerms, QualityRoleAliases))
            {
                return false;
            }
        }

        if (roleTokens.Any(IsDeliveryRoleToken))
        {
            hasRoleFamilyRequirement = true;
            if (!MatchesAnyTerm(primaryTerms, DeliveryRoleAliases))
            {
                return false;
            }
        }

        if (roleTokens.Any(IsSecurityRoleToken))
        {
            hasRoleFamilyRequirement = true;
            if (!MatchesAnyTerm(primaryTerms, SecurityRoleAliases))
            {
                return false;
            }
        }

        return hasRoleFamilyRequirement ||
            roleTokens.Any(roleToken => TokenMatches(terms, roleToken));
    }

    private static bool TokenMatches(
        IReadOnlySet<string> terms,
        string roleToken)
    {
        foreach (var alias in ExpandRoleTokenAliases(roleToken))
        {
            if (terms.Contains(alias))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesAnyTerm(
        IReadOnlySet<string> terms,
        IReadOnlyList<string> aliases)
    {
        return aliases.Any(terms.Contains);
    }

    private static int ScoreRoleToken(string roleToken)
    {
        if (TechnologyRoleTokens.Contains(roleToken))
        {
            return 4;
        }

        if (SecondaryRoleTokens.Contains(roleToken))
        {
            return 3;
        }

        return 6;
    }

    private static int ScoreWorkloadFit(
        AgentWorkloadKind workload,
        IReadOnlyList<string> roleTokens)
    {
        if (roleTokens.Any(IsEngineeringRoleToken) && workload == AgentWorkloadKind.Programming)
        {
            return 5;
        }

        if (roleTokens.Any(IsQualityRoleToken) && workload == AgentWorkloadKind.Qa)
        {
            return 5;
        }

        if (roleTokens.Any(IsDeliveryRoleToken) && workload == AgentWorkloadKind.Management)
        {
            return 5;
        }

        return 0;
    }

    private static IReadOnlyList<string> ExpandRoleTokenAliases(string roleToken)
    {
        return roleToken switch
        {
            "architect" or "architecture" or "solution" => ArchitectureRoleAliases,
            "engineer" or "developer" or "implementation" or "programming" => EngineeringRoleAliases,
            "qa" or "quality" or "test" or "tester" or "validation" or "validate" => QualityRoleAliases,
            "delivery" or "release" => DeliveryTokenAliases,
            "manager" => ManagerTokenAliases,
            "security" or "secure" => SecurityRoleAliases,
            "product" => ProductRoleAliases,
            "owner" => OwnerRoleAliases,
            "lead" => LeadRoleAliases,
            "blazor" => BlazorRoleAliases,
            "dotnet" or "net" => DotNetRoleAliases,
            "pwa" or "wasm" => PwaRoleAliases,
            _ => [roleToken]
        };
    }

    private static bool IsArchitectureRoleToken(string token)
    {
        return ArchitectureTriggerTokens.Contains(token);
    }

    private static bool IsEngineeringRoleToken(string token)
    {
        return EngineeringTriggerTokens.Contains(token);
    }

    private static bool IsQualityRoleToken(string token)
    {
        return QualityTriggerTokens.Contains(token);
    }

    private static bool IsDeliveryRoleToken(string token)
    {
        return DeliveryTriggerTokens.Contains(token);
    }

    private static bool IsSecurityRoleToken(string token)
    {
        return SecurityTriggerTokens.Contains(token);
    }

    private static bool IsTechnologyRoleToken(string token)
    {
        return TechnologyRoleTokens.Contains(token);
    }

    private static IReadOnlyList<string> ExtractTokens(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var tokens = new List<string>();
        var builder = new StringBuilder();
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            AddBuiltToken(tokens, builder);
        }

        AddBuiltToken(tokens, builder);
        return tokens
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddBuiltToken(
        List<string> tokens,
        StringBuilder builder)
    {
        if (builder.Length == 0)
        {
            return;
        }

        tokens.Add(builder.ToString());
        builder.Clear();
    }

    private static void AddTerms(HashSet<string> terms, string value)
    {
        foreach (var token in ExtractTokens(value))
        {
            terms.Add(token);
        }
    }

    private static void AddIf(List<string> values, bool condition, string value)
    {
        if (condition)
        {
            values.Add(value);
        }
    }

    private static bool ContainsNormalized(
        string value,
        string token)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var normalizedValue = Normalize(value);
        var normalizedToken = Normalize(token);
        return normalizedValue.Contains(normalizedToken, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        return new string(value.Where(character => char.IsLetterOrDigit(character)).ToArray());
    }

    private static string ComputeHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static readonly HashSet<string> IgnoredRoleTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "a",
        "add",
        "agent",
        "an",
        "and",
        "application",
        "app",
        "bounded",
        "change",
        "code",
        "create",
        "focused",
        "feature",
        "function",
        "in",
        "of",
        "or",
        "project",
        "process",
        "role",
        "runtime",
        "step",
        "subprocess",
        "the",
        "through",
        "to",
        "with"
    };

    private static readonly HashSet<string> TechnologyRoleTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "blazor",
        "dotnet",
        "net",
        "pwa",
        "wasm"
    };

    private static readonly HashSet<string> SecondaryRoleTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "lead"
    };

    private static readonly HashSet<string> ArchitectureTriggerTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "architect",
        "architecture",
        "design",
        "solution"
    };

    private static readonly HashSet<string> EngineeringTriggerTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "developer",
        "engineer",
        "implement",
        "implementation",
        "programming",
        "scaffold"
    };

    private static readonly HashSet<string> QualityTriggerTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "qa",
        "quality",
        "test",
        "tester",
        "validate",
        "validation"
    };

    private static readonly HashSet<string> DeliveryTriggerTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "delivery",
        "manager",
        "release"
    };

    private static readonly HashSet<string> SecurityTriggerTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "security",
        "secure"
    };

    private static readonly string[] ArchitectureRoleAliases =
    [
        "architect",
        "architecture",
        "design",
        "solution"
    ];

    private static readonly string[] EngineeringRoleAliases =
    [
        "application",
        "coder",
        "developer",
        "development",
        "engineer",
        "frontend",
        "fullstack",
        "implementation",
        "programmer",
        "programming",
        "software"
    ];

    private static readonly string[] QualityRoleAliases =
    [
        "browser",
        "qa",
        "quality",
        "review",
        "reviewer",
        "test",
        "tester",
        "validate",
        "validation"
    ];

    private static readonly string[] DeliveryRoleAliases =
    [
        "coordination",
        "coordinator",
        "delivery",
        "evidence",
        "governance",
        "manager",
        "release"
    ];

    private static readonly string[] SecurityRoleAliases =
    [
        "secure",
        "security",
        "threat"
    ];

    private static readonly string[] DeliveryTokenAliases =
    [
        "delivery",
        "evidence",
        "governance",
        "release",
        "writeback"
    ];

    private static readonly string[] ManagerTokenAliases =
    [
        "coordination",
        "coordinator",
        "manager"
    ];

    private static readonly string[] ProductRoleAliases =
    [
        "business",
        "planning",
        "product",
        "requirements",
        "scope",
        "strategy"
    ];

    private static readonly string[] OwnerRoleAliases =
    [
        "business",
        "owner",
        "planning",
        "product",
        "requirements",
        "scope",
        "strategy"
    ];

    private static readonly string[] LeadRoleAliases =
    [
        "lead",
        "manager",
        "review",
        "reviewer"
    ];

    private static readonly string[] BlazorRoleAliases =
    [
        "blazor",
        "development",
        "dotnet",
        "frontend",
        "programming",
        "razor",
        "software",
        "wasm",
        "webassembly"
    ];

    private static readonly string[] DotNetRoleAliases =
    [
        "csharp",
        "development",
        "dotnet",
        "net",
        "programming",
        "software"
    ];

    private static readonly string[] PwaRoleAliases =
    [
        "frontend",
        "pwa",
        "wasm",
        "webassembly"
    ];

    private static readonly HashSet<string> ProjectStructureReadRuntimeToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "project_structure_projects_list",
        "project_structure_hierarchy_get",
        "project_structure_read",
        "project_structure_node_catalog",
        "project_structure_checklist",
        "project_structure_dependencies_query",
        "project_plan_summary_get",
        "project_structure_asset_get",
        "project_structure_asset_content_get",
        "project_structure_node_workflow_add_options",
        "project_structure_node_workflow_status_get",
        "project_structure_knowledge_query",
        "project_structure_analytics_query",
        "project_structure_lease_get"
    };

    private static readonly HashSet<string> ProjectCreationRuntimeToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "project_structure_project_create"
    };

    private static readonly HashSet<string> SubprojectCreationRuntimeToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "project_structure_subproject_create"
    };

    private static readonly HashSet<string> SubprojectStructureMutationRuntimeToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "project_structure_subproject_link",
        "project_structure_nodes_to_new_subproject"
    };

    private static readonly HashSet<string> ProjectStructureMutationRuntimeToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "project_structure_project_update",
        "project_structure_dependency_link",
        "project_structure_dependency_unlink",
        "project_structure_node_create",
        "project_structure_node_update",
        "project_structure_node_type_update",
        "project_structure_node_metadata_update",
        "project_structure_nodes_status_update",
        "project_structure_node_status_update",
        "project_structure_nodes_progress_update",
        "project_structure_node_progress_update",
        "project_structure_nodes_marker_update",
        "project_structure_node_marker_update",
        "project_structure_nodes_priority_update",
        "project_structure_node_priority_update",
        "project_structure_node_move",
        "project_structure_node_recompose",
        "project_structure_node_reparent",
        "project_structure_node_descendants_to_project_move",
        "project_structure_node_command_execute",
        "project_structure_node_process_definition_link",
        "project_structure_node_process_start",
        "project_structure_process_subprocess_launch",
        "project_structure_node_workflow_definition_create",
        "project_structure_node_workflow_start",
        "project_structure_node_delete",
        "project_structure_nodes_delete",
        "project_structure_approval_request",
        "project_structure_asset_create",
        "project_structure_asset_create_revision",
        "project_structure_link_create",
        "project_structure_link_unlink",
        "project_structure_project_lease_acquire",
        "project_structure_repo_branch_lease_acquire",
        "project_structure_lease_renew",
        "project_structure_lease_release"
    };

    private static readonly HashSet<string> ProjectTaskMutationRuntimeToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "project_task_create",
        "project_task_update"
    };

    private static readonly HashSet<string> ProjectStructureBroadMutationRuntimeToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "project_structure_import"
    };

    private sealed record ExactRoleMatch(
        string MatchKey,
        int Score)
    {
        public static ExactRoleMatch NoMatch { get; } = new(string.Empty, 0);
    }
}
