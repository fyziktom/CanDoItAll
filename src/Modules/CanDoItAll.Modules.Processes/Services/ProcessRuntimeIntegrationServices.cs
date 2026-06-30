using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal sealed class StandardProcessLaunchDriverCatalogProvider(
    IProcessExecutionAdapter executionAdapter) : IProcessLaunchDriverCatalogProvider
{
    public ValueTask<ProcessLaunchDriverCatalog> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(new ProcessLaunchDriverCatalog(
            new ProcessDriverCatalog(StandardProcessAdapterDriverPackageFactory.CreateLayeredPackages(executionAdapter)),
            StandardProcessAdapterDescriptors.WorkflowAdapter.Strategy.StrategyId,
            StandardProcessAdapterDescriptors.WorkflowAdapter.CapabilityTags));
    }
}

internal sealed class StandardProcessRuntimeStrategyFactoryResolver(
    IProcessExecutionAdapter executionAdapter) : IProcessRuntimeStrategyFactoryResolver
{
    public ValueTask<IProcessStrategyFactory> ResolveAsync(
        ProcessStrategyBindingSnapshot binding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        cancellationToken.ThrowIfCancellationRequested();

        if (binding.StrategyId != executionAdapter.Descriptor.Strategy.StrategyId)
        {
            throw new InvalidOperationException(
                $"No process strategy factory is registered for strategy '{binding.StrategyId}'.");
        }

        return ValueTask.FromResult<IProcessStrategyFactory>(new StandardProcessAdapterStrategyFactory(executionAdapter));
    }
}

internal static class ProcessProviderReadinessRules
{
    public static bool CanExecuteGovernedProcessStep(
        ProviderProfile provider,
        IProviderProfileService providerProfileService)
    {
        var featureMatrix = providerProfileService.ResolveFeatureMatrix(provider);
        return featureMatrix.SupportsStructuredOutput || featureMatrix.SupportsFunctionTools;
    }
}

internal sealed class AgentFrameworkProcessLaunchExecutorResolver(
    IAgentReferenceDataProvider agentReferenceDataProvider,
    ProcessMockAgentCatalogService processMockAgentCatalogService,
    IProviderProfileService providerProfileService) : IProcessLaunchExecutorResolver
{
    public async ValueTask<ProcessLaunchExecutorResolution> ResolveAsync(
        ProcessLaunchExecutorResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await processMockAgentCatalogService.EnsureCatalogAsync(cancellationToken).ConfigureAwait(false);

        var referenceData = await agentReferenceDataProvider
            .GetAsync(AgentReferenceDataRequest.AgentsAndProviders(), cancellationToken)
            .ConfigureAwait(false);
        var agents = referenceData.Agents;
        var providerById = referenceData.ProviderById;
        var templateStepByKey = request.Definition.Steps.ToDictionary(step => step.Key, StringComparer.OrdinalIgnoreCase);
        var roleByKey = request.Definition.RoleUsages.ToDictionary(role => role.Key, StringComparer.OrdinalIgnoreCase);
        var profileAssignmentByStep = request.LiveRunProfile?.Assignments
            .ToDictionary(assignment => assignment.StepKey, StringComparer.OrdinalIgnoreCase) ??
            new Dictionary<string, ProcessTemplateLiveRunAssignmentDocument>(StringComparer.OrdinalIgnoreCase);
        var overrideByStepKey = request.ExecutorOverrides
            .Where(item => !string.IsNullOrWhiteSpace(item.StepKey))
            .GroupBy(item => item.StepKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last(),
                StringComparer.OrdinalIgnoreCase);
        var bindings = new List<ProcessLaunchExecutorBinding>();
        var findings = new List<ProcessLaunchReadinessFinding>();

        foreach (var planStep in request.Plan.Steps.Where(step => step.IsExecutable))
        {
            if (!templateStepByKey.TryGetValue(planStep.StepKey, out var templateStep))
            {
                findings.Add(new ProcessLaunchReadinessFinding(
                    ProcessLaunchReadinessSeverity.Error,
                    "process.launch.step_template_missing",
                    $"Step '{planStep.StepKey}' has no source template step.",
                    planStep.StepKey));
                continue;
            }

            profileAssignmentByStep.TryGetValue(planStep.StepKey, out var profileAssignment);
            overrideByStepKey.TryGetValue(planStep.StepKey, out var executorOverride);
            var roleKey = ResolveRoleKey(templateStep, profileAssignment);
            var role = roleByKey.GetValueOrDefault(roleKey);
            var roleQuery = ResolveRoleQuery(roleKey, role);
            var requestedExecutorKind = ResolveExecutorKind(profileAssignment, role, executorOverride);
            if (!ProcessLaunchExecutorKinds.CanResolveAsAgent(requestedExecutorKind))
            {
                findings.Add(new ProcessLaunchReadinessFinding(
                    ProcessLaunchReadinessSeverity.Error,
                    "process.launch.executor_kind_unsupported",
                    $"Step '{planStep.StepKey}' role '{roleKey}' requested unsupported executor kind '{requestedExecutorKind}'.",
                    planStep.StepKey,
                    roleKey));
                continue;
            }

            var readinessRequest = CreateReadinessRequest(planStep.StepKey, templateStep, roleKey, role);
            var candidate = executorOverride is null
                ? SelectAgent(readinessRequest, agents, providerById, providerProfileService)
                : ResolveOverrideAgent(executorOverride, agents, providerById, providerProfileService, findings, planStep.StepKey, roleKey);
            if (candidate is null)
            {
                if (executorOverride is null)
                {
                    findings.Add(new ProcessLaunchReadinessFinding(
                        ProcessLaunchReadinessSeverity.Error,
                        "process.launch.agent_missing",
                        FormatMissingAgentMessage(roleQuery, planStep.StepKey),
                        planStep.StepKey,
                        roleKey));
                }

                continue;
            }

            var readiness = AgentProcessReadinessEvaluator.Evaluate(candidate.Agent, readinessRequest);
            if (!readiness.IsExecutionReady)
            {
                AddReadinessFindings(findings, readiness, planStep.StepKey, roleKey);
                continue;
            }

            bindings.Add(new ProcessLaunchExecutorBinding(
                planStep.StepKey,
                roleKey,
                ProcessLaunchExecutorKinds.Agent,
                candidate.Agent.Id.ToString("D"),
                candidate.Agent.Name,
                readiness.ReadinessHash,
                ResolveAssignmentReason(profileAssignment, executorOverride, candidate, roleQuery, requestedExecutorKind)));
        }

        if (findings.Count == 0)
        {
            findings.Add(new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Info,
                "process.launch.readiness_ok",
                "All executable steps have active agent bindings with enabled governed-output-capable providers."));
        }

        return new ProcessLaunchExecutorResolution(bindings, findings);
    }

    private static string ResolveRoleKey(
        ProcessTemplateDefinitionStepDocument step,
        ProcessTemplateLiveRunAssignmentDocument? assignment)
    {
        if (!string.IsNullOrWhiteSpace(assignment?.RoleKey))
        {
            return assignment.RoleKey.Trim();
        }

        return step.RoleAssignments
            .OrderBy(role => role.FallbackOrder)
            .Select(role => role.RoleKey)
            .FirstOrDefault(roleKey => !string.IsNullOrWhiteSpace(roleKey)) ?? string.Empty;
    }

    private static string ResolveExecutorKind(
        ProcessTemplateLiveRunAssignmentDocument? assignment,
        ProcessTemplateDefinitionRoleUsageDocument? role,
        ProcessLaunchExecutorOverride? executorOverride)
    {
        if (!string.IsNullOrWhiteSpace(executorOverride?.ExecutorKind))
        {
            return executorOverride.ExecutorKind.Trim();
        }

        if (!string.IsNullOrWhiteSpace(assignment?.ExecutorKind))
        {
            return assignment.ExecutorKind.Trim();
        }

        if (!string.IsNullOrWhiteSpace(role?.PreferredExecutorKind))
        {
            return role.PreferredExecutorKind.Trim();
        }

        return ProcessLaunchExecutorKinds.Agent;
    }

    private static ProcessRoleQuery ResolveRoleQuery(
        string roleKey,
        ProcessTemplateDefinitionRoleUsageDocument? role)
    {
        var matchKeys = new[]
            {
                roleKey,
                role?.RoleResourceKey
            }
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ProcessRoleQuery(roleKey, matchKeys.Length == 0 ? [roleKey] : matchKeys);
    }

    private static string FormatMissingAgentMessage(
        ProcessRoleQuery roleQuery,
        string stepKey)
    {
        var aliases = roleQuery.MatchKeys
            .Where(key => !string.Equals(key, roleQuery.BindingRoleKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var aliasSummary = aliases.Length == 0
            ? string.Empty
            : $" Shared role aliases checked: {string.Join(", ", aliases)}.";

        return $"No active agent with an enabled governed-output-capable provider is available for role '{roleQuery.BindingRoleKey}' on step '{stepKey}'.{aliasSummary}";
    }

    private static string ResolveAssignmentReason(
        ProcessTemplateLiveRunAssignmentDocument? profileAssignment,
        ProcessLaunchExecutorOverride? executorOverride,
        AgentProviderCandidate candidate,
        ProcessRoleQuery roleQuery,
        string requestedExecutorKind)
    {
        if (!string.IsNullOrWhiteSpace(executorOverride?.AssignmentReason))
        {
            return $"{executorOverride.AssignmentReason.Trim()} {candidate.ReadinessSummary}";
        }

        if (!string.IsNullOrWhiteSpace(profileAssignment?.BindingReason))
        {
            return $"{profileAssignment.BindingReason.Trim()} {candidate.ReadinessSummary}";
        }

        if (string.Equals(requestedExecutorKind, ProcessLaunchExecutorKinds.Agent, StringComparison.OrdinalIgnoreCase))
        {
            return $"Resolved active agent '{candidate.Agent.Name}' by role '{roleQuery.BindingRoleKey}' using {candidate.MatchSummary}. {candidate.ReadinessSummary}";
        }

        return $"Resolved active agent '{candidate.Agent.Name}' by hybrid executor intent '{requestedExecutorKind}' for role '{roleQuery.BindingRoleKey}' using {candidate.MatchSummary}. {candidate.ReadinessSummary}";
    }

    private static AgentProviderCandidate? SelectAgent(
        AgentProcessRoleReadinessRequest readinessRequest,
        IReadOnlyList<AgentDefinition> agents,
        IReadOnlyDictionary<Guid, ProviderProfile> providerById,
        IProviderProfileService providerProfileService)
    {
        var matches = new List<AgentRoleMatch>();

        foreach (var agent in agents
            .Where(agent => !agent.IsTemplate && agent.Status == AgentLifecycleStatus.Active)
            .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (agent.ProviderProfileId is not { } providerId ||
                !providerById.TryGetValue(providerId, out var provider) ||
                !provider.IsEnabled ||
                !ProcessProviderReadinessRules.CanExecuteGovernedProcessStep(provider, providerProfileService))
            {
                continue;
            }

            var readiness = AgentProcessReadinessEvaluator.Evaluate(agent, readinessRequest);
            if (readiness.IsExecutionReady && readiness.HasRoleFit)
            {
                matches.Add(new AgentRoleMatch(
                    agent,
                    provider,
                    readiness.Score,
                    readiness.MatchSummary,
                    readiness.ReadinessHash,
                    readiness.ReadinessSummary));
            }
        }

        var bestMatch = matches
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Agent.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return bestMatch is null
            ? null
            : new AgentProviderCandidate(
                bestMatch.Agent,
                bestMatch.Provider,
                bestMatch.Score,
                bestMatch.Summary,
                bestMatch.ReadinessHash,
                bestMatch.ReadinessSummary);
    }

    private static AgentProviderCandidate? ResolveOverrideAgent(
        ProcessLaunchExecutorOverride executorOverride,
        IReadOnlyList<AgentDefinition> agents,
        IReadOnlyDictionary<Guid, ProviderProfile> providerById,
        IProviderProfileService providerProfileService,
        List<ProcessLaunchReadinessFinding> findings,
        string stepKey,
        string roleKey)
    {
        if (!Guid.TryParse(executorOverride.ExecutorId, out var agentId) || agentId == Guid.Empty)
        {
            findings.Add(new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Error,
                "process.launch.override_agent_invalid",
                $"Step '{stepKey}' role '{roleKey}' selected invalid agent id '{executorOverride.ExecutorId}'.",
                stepKey,
                roleKey));
            return null;
        }

        var agent = agents.FirstOrDefault(candidate => candidate.Id == agentId);
        if (agent is null || agent.IsTemplate || agent.Status != AgentLifecycleStatus.Active)
        {
            findings.Add(new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Error,
                "process.launch.override_agent_unavailable",
                $"Step '{stepKey}' role '{roleKey}' selected unavailable agent '{executorOverride.ExecutorDisplayName}'.",
                stepKey,
                roleKey));
            return null;
        }

        if (agent.ProviderProfileId is not { } providerId ||
            !providerById.TryGetValue(providerId, out var provider) ||
            !provider.IsEnabled ||
            !ProcessProviderReadinessRules.CanExecuteGovernedProcessStep(provider, providerProfileService))
        {
            findings.Add(new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Error,
                "process.launch.override_provider_unavailable",
                $"Step '{stepKey}' role '{roleKey}' selected agent '{agent.Name}' without an enabled governed-output-capable provider.",
                stepKey,
                roleKey));
            return null;
        }

        return new AgentProviderCandidate(
            agent,
            provider,
            0,
            "manual launch review selection",
            string.Empty,
            string.Empty);
    }

    private static AgentProcessRoleReadinessRequest CreateReadinessRequest(
        string stepKey,
        ProcessTemplateDefinitionStepDocument templateStep,
        string roleKey,
        ProcessTemplateDefinitionRoleUsageDocument? role)
    {
        return new AgentProcessRoleReadinessRequest(
            stepKey,
            templateStep.Title,
            roleKey,
            role?.RoleResourceKey ?? string.Empty,
            role?.DisplayName ?? roleKey,
            NormalizeOperations(templateStep.AllowedOperations),
            NormalizeOptional(templateStep.OperationTargetScope));
    }

    private static void AddReadinessFindings(
        List<ProcessLaunchReadinessFinding> findings,
        AgentProcessRoleReadinessResult readiness,
        string stepKey,
        string roleKey)
    {
        foreach (var finding in readiness.Findings.Where(finding => finding.Severity == AgentProcessReadinessFindingSeverity.Error))
        {
            findings.Add(new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Error,
                finding.Code,
                finding.Message,
                stepKey,
                roleKey));
        }
    }

    private static IReadOnlyList<string> NormalizeOperations(IEnumerable<string> operations)
    {
        return operations
            .Where(operation => !string.IsNullOrWhiteSpace(operation))
            .Select(operation => operation.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(operation => operation, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private sealed record AgentRoleMatch(
        AgentDefinition Agent,
        ProviderProfile Provider,
        int Score,
        string Summary,
        string ReadinessHash,
        string ReadinessSummary);

    private sealed record ProcessRoleQuery(
        string BindingRoleKey,
        IReadOnlyList<string> MatchKeys);

    private sealed record AgentProviderCandidate(
        AgentDefinition Agent,
        ProviderProfile Provider,
        int MatchScore,
        string MatchSummary,
        string ReadinessHash,
        string ReadinessSummary);
}

internal sealed class AgentFrameworkProcessRuntimeStepAssignmentRepairService(
    IAgentReferenceDataProvider agentReferenceDataProvider,
    IProviderProfileService providerProfileService) : IProcessRuntimeStepAssignmentRepairService
{
    public async ValueTask<ProcessRuntimeStepAssignmentRepairResult> RepairAsync(
        ProcessRuntimeStepAssignment assignment,
        string operatorReason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var readinessRequest = CreateReadinessRequest(assignment);
        var referenceData = await agentReferenceDataProvider
            .GetAsync(AgentReferenceDataRequest.AgentsAndProviders(), cancellationToken)
            .ConfigureAwait(false);
        var agents = referenceData.Agents;
        var providerById = referenceData.ProviderById;
        var currentAgent = ResolveCurrentAgent(assignment, agents);
        if (currentAgent is not null)
        {
            var currentReadiness = AgentProcessReadinessEvaluator.Evaluate(currentAgent, readinessRequest);
            if (currentReadiness.IsExecutionReady && currentReadiness.HasRoleFit)
            {
                return new ProcessRuntimeStepAssignmentRepairResult(assignment, false, string.Empty);
            }
        }

        var candidate = SelectAgent(readinessRequest, agents, providerById);
        if (candidate is null)
        {
            return new ProcessRuntimeStepAssignmentRepairResult(assignment, false, string.Empty);
        }

        if (string.Equals(assignment.ExecutorId, candidate.Agent.Id.ToString("D"), StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessRuntimeStepAssignmentRepairResult(assignment, false, string.Empty);
        }

        var previousExecutor = string.IsNullOrWhiteSpace(assignment.ExecutorDisplayName)
            ? assignment.ExecutorId
            : assignment.ExecutorDisplayName.Trim();
        var summary =
            $"Reassigned step '{assignment.StepKey}' from '{previousExecutor}' to '{candidate.Agent.Name}' because the previous executor no longer satisfies role/tool readiness for role '{ResolveRoleLabel(assignment)}'. New match: {candidate.MatchSummary}. {candidate.ReadinessSummary}";
        var repaired = assignment with
        {
            ExecutorKind = ProcessLaunchExecutorKinds.Agent,
            ExecutorId = candidate.Agent.Id.ToString("D"),
            ExecutorDisplayName = candidate.Agent.Name,
            ReadinessHash = candidate.ReadinessHash,
            AssignmentReason = string.IsNullOrWhiteSpace(assignment.AssignmentReason)
                ? summary
                : $"{assignment.AssignmentReason.Trim()} {summary}"
        };

        return new ProcessRuntimeStepAssignmentRepairResult(repaired, true, summary);
    }

    private RepairCandidate? SelectAgent(
        AgentProcessRoleReadinessRequest readinessRequest,
        IReadOnlyList<AgentDefinition> agents,
        IReadOnlyDictionary<Guid, ProviderProfile> providerById)
    {
        var matches = new List<RepairCandidate>();

        foreach (var agent in agents
            .Where(agent => !agent.IsTemplate && agent.Status == AgentLifecycleStatus.Active)
            .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (agent.ProviderProfileId is not { } providerId ||
                !providerById.TryGetValue(providerId, out var provider) ||
                !provider.IsEnabled ||
                !ProcessProviderReadinessRules.CanExecuteGovernedProcessStep(provider, providerProfileService))
            {
                continue;
            }

            var readiness = AgentProcessReadinessEvaluator.Evaluate(agent, readinessRequest);
            if (!readiness.IsExecutionReady || !readiness.HasRoleFit)
            {
                continue;
            }

            matches.Add(new RepairCandidate(
                agent,
                readiness.Score,
                readiness.MatchSummary,
                readiness.ReadinessHash,
                readiness.ReadinessSummary));
        }

        return matches
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Agent.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static AgentDefinition? ResolveCurrentAgent(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<AgentDefinition> agents)
    {
        return Guid.TryParse(assignment.ExecutorId, out var agentId)
            ? agents.FirstOrDefault(agent => agent.Id == agentId)
            : null;
    }

    private static AgentProcessRoleReadinessRequest CreateReadinessRequest(ProcessRuntimeStepAssignment assignment)
    {
        return new AgentProcessRoleReadinessRequest(
            assignment.StepKey,
            assignment.StepKey,
            assignment.RoleKey,
            assignment.RoleResourceKey,
            assignment.RoleDisplayName,
            NormalizeOperations(assignment.AllowedOperations),
            string.IsNullOrWhiteSpace(assignment.OperationTargetScope)
                ? string.Empty
                : assignment.OperationTargetScope.Trim());
    }

    private static IReadOnlyList<string> NormalizeOperations(IReadOnlyList<string> operations)
    {
        return operations
            .Where(operation => !string.IsNullOrWhiteSpace(operation))
            .Select(operation => operation.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(operation => operation, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveRoleLabel(ProcessRuntimeStepAssignment assignment)
    {
        if (!string.IsNullOrWhiteSpace(assignment.RoleDisplayName))
        {
            return assignment.RoleDisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(assignment.RoleKey))
        {
            return assignment.RoleKey.Trim();
        }

        return assignment.StepKey.Trim();
    }

    private sealed record RepairCandidate(
        AgentDefinition Agent,
        int Score,
        string MatchSummary,
        string ReadinessHash,
        string ReadinessSummary);
}

internal sealed class AgentFrameworkProcessStepBriefBuilder : IProcessStepBriefBuilder
{
    private const string SubprocessLaunchToolName = "project_structure_process_subprocess_launch";
    private readonly GenericProcessStepBriefBuilder genericBuilder = new();

    public string Build(ProcessStepBriefBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var genericBrief = genericBuilder.Build(request);
        var subprocessGuidance = BuildSubprocessGuidance(request.Step);
        var dependencyArtifactGuidance = BuildDependencyArtifactGuidance(request);
        var ownOutputBootstrapGuidance = BuildOwnOutputBootstrapGuidance(request);
        var projectStructureContextGuidance = BuildProjectStructureContextGuidance(request);

        return $"""
        {genericBrief}

        AgentFramework execution contract:
        This is a tool-backed process step, not a chat-only response. First use the available workspace, project-structure, subprocess, runtime, validation, or browser tools needed by the step contract. Only after the required evidence exists, submit the final process_step_outcome_result through the required finalizer tool. If the runtime explicitly asks for a JSON fallback, return one JSON object matching that same contract.
        Use Status Completed when the step is done, Blocked when required input or tools are missing, Failed for unrecoverable execution failure, or WaitingApproval when a human approval is required.
        If branch outcomes are listed, set BranchOutcomeKey to exactly one listed outcome key.

        AgentFramework manager escalation rule:
        If you are blocked by a missing or denied tool, permission, right, capability, workspace boundary, approval path, or policy contract, make the first nextActions entry a manager action request. Include the assigned agent name, step key, process run id, denied tool or right, allowed operations, operation target scope, and whether the manager should grant the right to this agent or reassign the step to an agent that already has it. Include the exact policy or tool denial text in Reason and in HumanReadableSummaryMarkdown; do not only say that you cannot proceed.

        Project-scoped launch context:
        Project id: {request.LaunchRequest.ProjectId?.ToString("D") ?? "not scoped"}
        Project node id: {request.LaunchRequest.ProjectNodeId ?? "not scoped"}

        AgentFramework project-structure context source:
        {projectStructureContextGuidance}

        AgentFramework evidence write rule:
        Write process step summaries, proof, screenshots, logs, and handoff notes under the managed artifact root or a child path. Managed artifact refs are workspace-managed relative paths; use them exactly as shown and never convert them to external-target paths. Include the written managed artifact paths from this brief in evidenceRefs; if a workspace tool echoes a longer scoped storage path for the same artifact, keep the managed relative ref in evidenceRefs and use the scoped echo only as extra context. Do not write evidence under output/ unless this step is explicitly mutating a managed product output path.
        If Produced artifact slots are listed, the first workspace mutation for that produced output must be workspace_write_file or workspace_append_file to the listed Primary write ref. Do not list, search, stat, or read this run's managed artifact root to discover your own missing output before that write. For intake, planning, scope, architecture, review, governance, or summary steps with no required upstream slot, write a managed Markdown artifact with assumptions and known gaps instead of blocking on optional context. Do not finalize Completed with an empty evidenceRefs array.

        AgentFramework own-output bootstrap:
        {ownOutputBootstrapGuidance}

        AgentFramework dependency artifact refs:
        {dependencyArtifactGuidance}

        AgentFramework upstream artifact read rule:
        When Required upstream artifact slots or AgentFramework dependency artifact refs list managed refs, call workspace_stat_path or workspace_read_file on those exact refs before using project-structure hierarchy as fallback context. Project-structure nodes may summarize a run, but upstream process artifacts are read through workspace file tools. Do not abbreviate, ellipsize, shorten, or guess managed refs; copy the full ref from this brief into the workspace tool call. Do not return Blocked for missing intake, design, implementation, QA, screenshot, runtime, or release evidence until every listed managed ref for the needed slot has a current failed workspace file-tool receipt.

        Project-structure evidence hygiene:
        Do not create project-structure nodes for every subprocess, intermediate screenshot, log, or step detail. Keep subprocess detail in managed artifacts and live-process history. For multi-team app delivery, the visible project structure should contain one root process run plus only the durable handoff nodes the process asks for: the final accepted screenshot ImageAsset, one run-app proof node, one run-tests proof node, and one manager summary node describing what was built, how it works, and current validation state.

        AgentFramework subprocess adapter guidance:
        {subprocessGuidance}
        """;
    }

    private static string BuildProjectStructureContextGuidance(ProcessStepBriefBuildRequest request)
    {
        var lines = new List<string>();
        var canReadProjectStructure = request.Step.AllowedOperations.Contains(
            ProcessOperationContractNames.ReadProjectStructure,
            StringComparer.OrdinalIgnoreCase);

        if (TryResolveLaunchVariable(request.LaunchVariables, "ProjectStructureContextSummary", out _))
        {
            lines.Add("ProjectStructureContextSummary in Launch variables is the current project-structure context for this run; treat it as authoritative project-structure evidence when no richer project-structure tool result is required by the step.");
        }
        else if (canReadProjectStructure)
        {
            lines.Add("This step may read project structure, but no ProjectStructureContextSummary launch variable was supplied; use available project-structure tools or the supplied launch variables instead of inventing a managed file path.");
        }

        if (TryResolveLaunchVariable(request.LaunchVariables, "DotNetScaffoldContract", out _))
        {
            lines.Add("DotNetScaffoldContract and DotNet* launch variables are typed project-structure facts for .NET subprocesses; use them for app type, product root, scaffold layout, test project, and target framework decisions.");
        }

        if (TryResolveLaunchVariable(request.LaunchVariables, "ProductRoot", out _) ||
            TryResolveLaunchVariable(request.LaunchVariables, "OutputRoot", out _) ||
            TryResolveLaunchVariable(request.LaunchVariables, "ExternalTargetRoot", out _))
        {
            var aliases = ResolveLaunchExternalTargetAliases(request.LaunchVariables);
            var aliasSummary = aliases.Count == 0
                ? "No normalized external-target alias was resolved from launch variables; use only managed artifact refs unless a tool result supplies a grounded alias."
                : $"Grounded external-target aliases for structured workspace tool path arguments: {string.Join("; ", aliases)}.";
            lines.Add($"ProductRoot, OutputRoot, and ExternalTargetRoot launch variables identify the product target. {aliasSummary} Do not call workspace_read_file, workspace_stat_path, workspace_list_files, workspace_search, workspace_copy_path, workspace_analyze_image, or other structured workspace path tools with native absolute ProductRoot or OutputRoot paths. If a workspace-tool denial supplies a replacement external-target alias, retry the same structured workspace tool with that alias before returning Blocked.");
        }

        if (TryResolveLaunchVariable(request.LaunchVariables, "ParentProcessRunId", out _) ||
            TryResolveLaunchVariable(request.LaunchVariables, "SubprocessDefinitionKey", out _))
        {
            lines.Add("For subprocess runs, parent launch variables are copied into the child run; ParentProcessRunId, ParentProcessStepKey, and SubprocessDefinitionKey are metadata, not managed artifact refs.");
        }

        if (lines.Count == 0)
        {
            lines.Add("No project-structure launch summary was supplied for this step.");
        }

        lines.Add($"Do not call workspace_read_file on artifacts/process-runs/{request.RunId}/project-structure.json or any other invented project-structure snapshot path unless that exact file is listed in Required upstream artifact slots or AgentFramework dependency artifact refs. Project-structure context is not materialized as a managed JSON file by default.");
        lines.Add("If a durable project-structure summary is useful for this step, write the relevant facts into the step's primary managed artifact instead of treating a missing snapshot file as a blocker.");

        return string.Join(Environment.NewLine, lines);
    }

    private static bool TryResolveLaunchVariable(
        IReadOnlyDictionary<string, string> launchVariables,
        string key,
        out string value)
    {
        value = string.Empty;
        if (!launchVariables.TryGetValue(key, out var candidate) ||
            string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        value = candidate.Trim();
        return true;
    }

    private static IReadOnlyList<string> ResolveLaunchExternalTargetAliases(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        return launchVariables
            .Where(item => TrustedExternalTargetVariableNames.Contains(item.Key))
            .Select(item => AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(item.Value))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Where(item => item.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static readonly HashSet<string> TrustedExternalTargetVariableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ExternalTargetAlias",
        "ExternalTargetRoot",
        "OutputFolder",
        "OutputRoot",
        "OutputRootAlias",
        "ProductRoot",
        "ProductRootAlias",
        "WorkspaceAlias"
    };

    private static string BuildOwnOutputBootstrapGuidance(ProcessStepBriefBuildRequest request)
    {
        if (request.ProducedSlots.Count == 0)
        {
            return "No produced artifact slots; no own-output bootstrap is required.";
        }

        var primaryWriteRef = BuildManagedStepArtifactPath(request.ManagedArtifactRoot, request.Step.Key);
        if (request.RequiredSlots.Count == 0)
        {
            return $"""
            This step has produced artifact slots and no required upstream artifact slots. It is an evidence producer. Do not return Blocked for missing upstream artifacts, insufficient evidence, missing prior logs, or absent screenshots before creating your own managed artifact. Your first evidence action must be workspace_write_file or workspace_append_file to the exact primary write ref below.
            Primary own-output write ref: {primaryWriteRef}
            Completion rule: after writing that artifact, return Completed with evidenceRefs containing the exact primary own-output write ref. If optional project context is missing, include assumptions and known gaps inside the artifact instead of blocking. Do not read or stat ProductRoot, OutputRoot, ExternalTargetRoot, or their external-target aliases looking for a same-named own-output packet before writing this managed artifact; own process outputs are generated under managed artifact refs, not discovered from the product target. Do not require build, test, runtime, screenshot, deployment, approval, or downstream handoff evidence that belongs to later steps before completing this producer step. Blocked is valid only when you cannot create the primary managed artifact or the step contract's own immediate inputs are contradictory.
            """;
        }

        return $"""
        This step has required upstream artifact slots and produced artifact slots. Read required upstream refs first, then create or update your own primary managed artifact before returning Completed.
        Primary own-output write ref: {primaryWriteRef}
        """;
    }

    private static string BuildDependencyArtifactGuidance(ProcessStepBriefBuildRequest request)
    {
        var dependencyStepKeys = ResolveDependencyStepKeys(request.Step);
        if (dependencyStepKeys.Count == 0)
        {
            return "No direct dependency step artifact refs.";
        }

        var stepsByKey = request.Definition.Steps.ToDictionary(step => step.Key, StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();
        foreach (var dependencyStepKey in dependencyStepKeys)
        {
            stepsByKey.TryGetValue(dependencyStepKey, out var dependencyStep);
            var title = string.IsNullOrWhiteSpace(dependencyStep?.Title)
                ? dependencyStepKey
                : dependencyStep.Title.Trim();
            lines.Add($"""
            - Dependency step: {dependencyStepKey} - {title}
              Primary completed-step artifact ref: {BuildManagedStepArtifactPath(request.ManagedArtifactRoot, dependencyStepKey)}
              Dependency step artifact root: {BuildManagedStepArtifactRoot(request.ManagedArtifactRoot, dependencyStepKey)}
              Runtime rule: before listing, searching, or using project-structure fallback context, call workspace_stat_path or workspace_read_file on the exact primary ref above. If the primary ref is missing, inspect the listed dependency step artifact root before blocking.
            """);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<string> ResolveDependencyStepKeys(ProcessTemplateDefinitionStepDocument step)
    {
        var keys = new List<string>();
        foreach (var dependency in step.Dependencies)
        {
            if (!string.IsNullOrWhiteSpace(dependency.DependsOnStepKey))
            {
                keys.Add(dependency.DependsOnStepKey.Trim());
            }
        }

        if (!string.IsNullOrWhiteSpace(step.DependsOnStepKey))
        {
            keys.Add(step.DependsOnStepKey.Trim());
        }

        return keys
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildManagedStepArtifactPath(string artifactRoot, string stepKey)
        => $"{artifactRoot}/steps/{SanitizeManagedArtifactPathSegment(stepKey)}.md";

    private static string BuildManagedStepArtifactRoot(string artifactRoot, string stepKey)
        => $"{artifactRoot}/{SanitizeManagedArtifactPathSegment(stepKey)}/";

    private static string SanitizeManagedArtifactPathSegment(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? "step"
            : value.Trim();
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }

        return builder.Length == 0 ? "step" : builder.ToString();
    }

    private static string BuildSubprocessGuidance(ProcessTemplateDefinitionStepDocument step)
    {
        var isSubprocessStep = string.Equals(step.StepKind, ProcessTemplateStepKinds.Subprocess, StringComparison.OrdinalIgnoreCase) ||
                               !string.IsNullOrWhiteSpace(step.SubprocessProcessKey);
        if (!isSubprocessStep)
        {
            return "No subprocess mapping.";
        }

        var hasSubprocessKey = !string.IsNullOrWhiteSpace(step.SubprocessProcessKey);
        var subprocessKey = hasSubprocessKey
            ? step.SubprocessProcessKey.Trim()
            : "not mapped";
        var snapshotName = string.IsNullOrWhiteSpace(step.SubprocessDefinitionSnapshotName)
            ? "not supplied"
            : step.SubprocessDefinitionSnapshotName.Trim();
        var launchInstruction = !hasSubprocessKey
            ? "This step is marked as a subprocess but has no child process definition key. Return Blocked unless upstream evidence already supplies the missing child run."
            : $"Use {SubprocessLaunchToolName} with DefinitionKey \"{subprocessKey}\" when {ProcessOperationContractNames.ExecuteExternalAction} is allowed. Do not mark Completed until the child run receipt and required child evidence are available. If required evidence is missing from a stopped child run and launch is allowed, call the launch tool again to create or reuse a non-stopped child run. Return Blocked only for a concrete missing tool, input, policy, environment, or irrecoverable evidence problem.";

        return $"""
        - Child process definition key: {subprocessKey}
        - Child definition snapshot name: {snapshotName}
        - Governed launch tool: {SubprocessLaunchToolName}
        - Completion rule: {launchInstruction}
        - Live-run profile rule: leave LiveRunProfileKey empty unless the launch variables explicitly provide a valid process live-run profile key for this child definition. BranchName, RepositoryRoot, SessionId, parent DefinitionKey, and child DefinitionKey are not live-run profile keys.
        - Scope rule: use the parent step's assigned project node. Leave ParentProjectNodeId empty unless the parent launch context has no project node. Do not pass ProcessRunNodeId as ParentProjectNodeId.
        - Retry rule: repeated launch-tool calls for the same parent run, parent step, project node, and child definition return the existing child run instead of creating another child.
        - Stopped-child rule: a Completed, Failed, Cancelled, or Blocked child run is not an active wait. Inspect stopped-child evidence, then either complete from valid evidence or relaunch when required evidence is missing and launch is allowed. Do not return Blocked only because a stopped child run exists.
        - Active-child defer rule: when the launch tool result has RunId and Stage Running, call submit_process_step_outcome with ParentDeferredOutcomeJson exactly if that field is present. Do not inspect child evidence or write a hand-authored blocked finalizer while the child run is active; the process runtime will defer the parent step until the child run stops.
        - Evidence rule: the launch tool result includes ChildManagedArtifactRoot, ChildStepsArtifactRoot, ChildLiveProcessesRoute, ExpectedChildEvidenceRefs, ParentDeferredOutcomeInstruction, and ParentDeferredOutcomeJson. Treat artifacts under ChildManagedArtifactRoot as the child evidence bundle; do not require child evidence to be copied into the parent run root. ExpectedChildEvidenceRefs are preferred lookup candidates after the child run is stopped, not an all-or-nothing checklist while it is still active; if one expected ref is missing after the child stops, inspect sibling files under ChildManagedArtifactRoot and child step directories before blocking.
        """;
    }
}

internal sealed partial class AgentFrameworkProcessExecutionAdapter(
    ICanDoItAllAgentWorkspaceFactory workspaceFactory,
    IAgentReferenceDataProvider agentReferenceDataProvider,
    IProcessRuntimeStepAssignmentStore assignmentStore,
    IProcessRuntimeStateStore stateStore,
    IWorkspaceFileService workspaceFiles) : IProcessExecutionAdapter
{
    public ProcessExecutionAdapterDescriptor Descriptor => StandardProcessAdapterDescriptors.WorkflowAdapter;

    public async ValueTask<ProcessExecutionAdapterResult> ExecuteAsync(
        ProcessExecutionAdapterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.StepId is not { } stepId)
        {
            return Failed("process.adapter.step_missing", "The process execution adapter requires a concrete step id.", "step-missing");
        }

        var assignment = await assignmentStore.LoadAsync(request.RunId, stepId, cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            return Failed("process.adapter.assignment_missing", $"No runtime assignment exists for step '{stepId}'.", stepId.ToString());
        }

        if (await TryResolveExistingPendingChildRunAsync(
                assignment,
                assignmentStore,
                stateStore,
                cancellationToken).ConfigureAwait(false) is { } existingPendingChildRunId)
        {
            throw new ProcessRuntimeDispatchDeferredException(
                $"Step '{assignment.StepKey}' is waiting for active child process run '{existingPendingChildRunId}'.",
                existingPendingChildRunId);
        }

        if (!string.Equals(assignment.ExecutorKind, ProcessLaunchExecutorKinds.Agent, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(assignment.ExecutorId, out var agentId))
        {
            return Failed(
                "process.adapter.executor_invalid",
                $"Step '{assignment.StepKey}' has invalid executor binding '{assignment.ExecutorKind}:{assignment.ExecutorId}'.",
                assignment.ExecutorId);
        }

        var referenceData = await agentReferenceDataProvider
            .GetAsync(new AgentReferenceDataRequest(AgentReferenceDataSections.Agents), cancellationToken)
            .ConfigureAwait(false);
        var agent = referenceData.Agents.FirstOrDefault(candidate => candidate.Id == agentId);
        if (agent is null)
        {
            return NeedsManager(
                "process.adapter.executor_agent_missing",
                $"Step '{assignment.StepKey}' is assigned to missing agent '{agentId}'.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{agentId}");
        }

        var readiness = AgentProcessReadinessEvaluator.Evaluate(agent, CreateRuntimeReadinessRequest(assignment));
        if (!readiness.IsExecutionReady || !readiness.HasRoleFit)
        {
            return NeedsManager(
                "process.adapter.executor_readiness_failed",
                $"Step '{assignment.StepKey}' cannot run with assigned agent '{agent.Name}': {readiness.ReadinessSummary}",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{agentId}:{readiness.ReadinessHash}");
        }

        try
        {
            var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
            var metadataJson = BuildProcessExecutionMetadata(assignment);
            var result = await workspaceService
                .ExecuteRunAsync(
                    new ExecutionRunRequest(
                        agentId,
                        assignment.Prompt,
                        Context: new ExecutionInvocationContext(
                            SourceKind: ProcessMockAgentCatalog.ProcessSourceKind,
                            SourceId: assignment.StepKey,
                            CorrelationId: request.RunId.ToString(),
                            CausationId: stepId.ToString(),
                            RequestedBy: "process-runtime",
                            RequestedByKind: "system",
                            MetadataJson: metadataJson,
                            ProcessRunId: request.RunId.ToString(),
                            ProcessStepId: stepId.ToString(),
                            Policy: new ExecutionInvocationPolicy(
                                MaxStructuredOutputRepairAttempts: ExecutionInvocationMetadata.DefaultGovernedRepairAttempts)),
                        AutoApprovePendingToolCalls: true,
                        StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult),
                    cancellationToken)
                .ConfigureAwait(false);

            if (await TryResolveExistingPendingChildRunAsync(
                    assignment,
                    assignmentStore,
                    stateStore,
                    cancellationToken).ConfigureAwait(false) is { } pendingChildRunAfterExecution)
            {
                throw CreatePendingChildRunDeferredException(assignment, pendingChildRunAfterExecution);
            }

            var validation = AgentOutputJson.DeserializeAndValidate(
                result.ResponseText,
                new ProcessStepOutcomeValidator());

            if (!validation.Succeeded || validation.Output is null)
            {
                return Failed(
                    "process.adapter.output_invalid",
                    FormatValidationErrors(validation.Validation.Errors),
                    validation.RawOutputHash);
            }

            if (await TryResolvePendingChildRunAsync(
                    assignment,
                    validation.Output,
                    stateStore,
                    cancellationToken).ConfigureAwait(false) is { } pendingChildRunId)
            {
                throw CreatePendingChildRunDeferredException(assignment, pendingChildRunId);
            }

            var executionDetail = await workspaceService
                .GetExecutionRunDetailAsync(result.ExecutionRunId, cancellationToken)
                .ConfigureAwait(false);

            var materialization = MaterializeManagedOutcomeArtifactIfNeeded(
                assignment,
                validation.Output,
                result.ExecutionRunId,
                executionDetail.ToolReceipts);
            if (materialization.Issue is { } materializationIssue)
            {
                return NeedsManagerForCompletionIssue(assignment, validation.RawOutputHash, materializationIssue);
            }

            return ToAdapterResult(
                assignment,
                materialization.Output,
                validation.RawOutputHash,
                materialization.ToolReceipts);
        }
        catch (ProcessRuntimeDispatchDeferredException)
        {
            throw;
        }
        catch (AgentExecutionCancelledException exception)
        {
            return Canceled(
                "process.adapter.agent_execution_cancelled",
                $"Agent execution was cancelled for step '{assignment.StepKey}': {exception.Message}",
                $"{exception.ExecutionRunId:N}:{exception.ProcessRunId}:{exception.Message}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (await TryResolveExistingPendingChildRunAsync(
                    assignment,
                    assignmentStore,
                    stateStore,
                    CancellationToken.None).ConfigureAwait(false) is { } pendingChildRunId)
            {
                throw CreatePendingChildRunDeferredException(assignment, pendingChildRunId);
            }

            if (TryBuildRetryableAgentOutputContractIssue(assignment, exception, out var outputContractIssue))
            {
                return NeedsManagerForCompletionIssue(
                    assignment,
                    ComputeHash(exception.GetType().FullName + ":" + exception.Message),
                    outputContractIssue);
            }

            return Failed(
                "process.adapter.agent_execution_failed",
                $"Agent execution failed for step '{assignment.StepKey}': {exception.Message}",
                ComputeHash(exception.GetType().FullName + ":" + exception.Message));
        }
    }

    private ManagedOutcomeArtifactMaterialization MaterializeManagedOutcomeArtifactIfNeeded(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        Guid executionRunId,
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
    {
        var isSelfEvidenceBlocker = IsPureManagedArtifactSelfEvidenceBlocker(assignment, output);
        if (output.Status != ProcessStepOutcomeStatus.Completed &&
            !isSelfEvidenceBlocker)
        {
            return ManagedOutcomeArtifactMaterialization.Unchanged(output, toolReceipts);
        }

        if (assignment.ProducedArtifactSlotIds.Count == 0)
        {
            return ManagedOutcomeArtifactMaterialization.Unchanged(output, toolReceipts);
        }

        var hasManagedEvidence = HasAllManagedArtifactEvidence(assignment, output.EvidenceRefs);
        var hasWriteReceipt = HasManagedArtifactWriteReceipt(assignment, toolReceipts);
        var primaryRef = BuildManagedStepArtifactPath(assignment);
        IReadOnlyList<ToolExecutionReceiptRecord> effectiveReceipts = toolReceipts;
        if (!hasWriteReceipt)
        {
            var writeResult = workspaceFiles.WriteTextFile(
                primaryRef,
                BuildManagedOutcomeArtifactContent(assignment, output, primaryRef),
                overwrite: true);
            if (!writeResult.Succeeded)
            {
                return ManagedOutcomeArtifactMaterialization.Failed(
                    output,
                    toolReceipts,
                    new ProcessCompletionIssue(
                        "process.adapter.managed_artifact_materialization_failed",
                        $"Step '{assignment.StepKey}' produced a valid structured outcome, but the runtime could not persist the primary managed artifact '{primaryRef}': {writeResult.Message}",
                        $"{assignment.RunId}:{assignment.StepInstanceId}:managed-artifact-materialization-failed:{primaryRef}:{writeResult.Message}",
                        assignment.ProducedArtifactSlotIds,
                        ProcessDiagnosticRetrySafety.SafeToRetry,
                        ProcessDiagnosticIdempotencyClassification.Idempotent));
            }

            effectiveReceipts = toolReceipts
                .Append(CreateManagedOutcomeArtifactReceipt(executionRunId, primaryRef, writeResult.Message))
                .ToArray();
        }
        else
        {
            var appendResult = workspaceFiles.AppendTextFile(
                primaryRef,
                BuildManagedOutcomeArtifactAppendixContent(assignment, output, primaryRef));
            if (!appendResult.Succeeded)
            {
                return ManagedOutcomeArtifactMaterialization.Failed(
                    output,
                    toolReceipts,
                    new ProcessCompletionIssue(
                        "process.adapter.managed_artifact_outcome_append_failed",
                        $"Step '{assignment.StepKey}' produced a valid structured outcome, but the runtime could not append the validated outcome to primary managed artifact '{primaryRef}': {appendResult.Message}",
                        $"{assignment.RunId}:{assignment.StepInstanceId}:managed-artifact-outcome-append-failed:{primaryRef}:{appendResult.Message}",
                        assignment.ProducedArtifactSlotIds,
                        ProcessDiagnosticRetrySafety.SafeToRetry,
                        ProcessDiagnosticIdempotencyClassification.Idempotent));
            }

            effectiveReceipts = toolReceipts
                .Append(CreateManagedOutcomeArtifactReceipt(
                    executionRunId,
                    primaryRef,
                    appendResult.Message,
                    "workspace_append_file"))
                .ToArray();
        }

        var effectiveOutput = isSelfEvidenceBlocker
            ? CopyAsCompletedWithEvidenceRef(output, primaryRef)
            : hasManagedEvidence
                ? output
                : CopyWithEvidenceRef(output, primaryRef);
        return ManagedOutcomeArtifactMaterialization.Succeeded(effectiveOutput, effectiveReceipts);
    }

    private static bool IsPureManagedArtifactSelfEvidenceBlocker(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Blocked ||
            assignment.ProducedArtifactSlotIds.Count == 0 ||
            assignment.RequiredArtifactSlotIds.Count > 0)
        {
            return false;
        }

        var operations = NormalizeOperations(assignment.AllowedOperations);
        if (operations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase) ||
            operations.Contains(ProcessOperationContractNames.LaunchRuntime, StringComparer.OrdinalIgnoreCase) ||
            operations.Contains(ProcessOperationContractNames.CaptureRuntimeProof, StringComparer.OrdinalIgnoreCase) ||
            operations.Contains(ProcessOperationContractNames.RunValidation, StringComparer.OrdinalIgnoreCase) ||
            operations.Contains(ProcessOperationContractNames.MutateProductTarget, StringComparer.OrdinalIgnoreCase) ||
            AllowsProductMutation(operations, assignment.OperationTargetScope))
        {
            return false;
        }

        var text = string.Join(
            " ",
            EnumerateOutcomeText(output).Where(value => !string.IsNullOrWhiteSpace(value)));
        if (LooksLikeRightsOrToolBoundary(text))
        {
            return false;
        }

        if (LooksLikeMissingOwnPrimaryManagedArtifact(assignment, text))
        {
            return true;
        }

        return ContainsAny(
            text,
            "concrete current-run evidence",
            "current run evidence",
            "current-run evidence",
            "managed artifact evidence",
            "managed artifact ref",
            "own-output write ref",
            "evidence reference");
    }

    private static bool LooksLikeMissingOwnPrimaryManagedArtifact(
        ProcessRuntimeStepAssignment assignment,
        string text)
    {
        if (!ContainsAny(
                text,
                "does not exist",
                "not found",
                "failed to find",
                "could not find",
                "cannot find",
                "missing file"))
        {
            return false;
        }

        var normalizedText = text.Replace('\\', '/');
        var primaryRef = BuildManagedStepArtifactPath(assignment);
        if (normalizedText.Contains(primaryRef, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var processRunSuffix = $"process-runs/{assignment.RunId.Value:D}/steps/{SanitizeManagedArtifactPathSegment(assignment.StepKey)}.md";
        return normalizedText.Contains(processRunSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAllManagedArtifactEvidence(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<string> evidenceRefs)
    {
        var normalizedEvidenceRefs = evidenceRefs
            .Where(evidenceRef => !string.IsNullOrWhiteSpace(evidenceRef))
            .Select(NormalizeManagedArtifactRef)
            .Where(evidenceRef => evidenceRef.Length > 0)
            .ToArray();
        return assignment.ProducedArtifactSlotIds.All(slotId =>
            HasManagedArtifactEvidence(assignment, slotId, normalizedEvidenceRefs));
    }

    private static ProcessStepOutcomeResult CopyWithEvidenceRef(
        ProcessStepOutcomeResult output,
        string evidenceRef)
    {
        var evidenceRefs = output.EvidenceRefs
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Append(evidenceRef)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new ProcessStepOutcomeResult
        {
            Status = output.Status,
            Reason = output.Reason,
            BranchOutcomeKey = output.BranchOutcomeKey,
            BranchOutcomeTitle = output.BranchOutcomeTitle,
            EvidenceRefs = evidenceRefs,
            NextActions = output.NextActions,
            HumanReadableSummaryMarkdown = output.HumanReadableSummaryMarkdown
        };
    }

    private static ProcessStepOutcomeResult CopyAsCompletedWithEvidenceRef(
        ProcessStepOutcomeResult output,
        string evidenceRef)
    {
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? "The agent reported a self-evidence blocker for a pure managed-artifact producer step."
            : output.Reason.Trim();
        return new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = $"Runtime materialized the pure managed-artifact producer outcome after the agent reported a self-evidence blocker. Original reason: {originalReason}",
            BranchOutcomeKey = output.BranchOutcomeKey,
            BranchOutcomeTitle = output.BranchOutcomeTitle,
            EvidenceRefs = [evidenceRef],
            NextActions = [],
            HumanReadableSummaryMarkdown = output.HumanReadableSummaryMarkdown
        };
    }

    private static string BuildManagedOutcomeArtifactContent(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        string primaryRef)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {assignment.StepKey} Process Step Outcome");
        builder.AppendLine();
        builder.AppendLine("Runtime persisted this managed artifact from the validated structured process step outcome.");
        builder.AppendLine();
        builder.AppendLine($"- Run id: {assignment.RunId.Value:D}");
        builder.AppendLine($"- Step id: {assignment.StepInstanceId.Value:D}");
        builder.AppendLine($"- Step key: {assignment.StepKey}");
        builder.AppendLine($"- Executor: {assignment.ExecutorDisplayName}");
        builder.AppendLine($"- Status: {output.Status}");
        builder.AppendLine($"- Primary managed ref: {primaryRef}");
        builder.AppendLine($"- Persisted at UTC: {DateTimeOffset.UtcNow:u}");
        builder.AppendLine();
        builder.AppendLine("## Reason");
        builder.AppendLine();
        builder.AppendLine(output.Reason.Trim());
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey) ||
            !string.IsNullOrWhiteSpace(output.BranchOutcomeTitle))
        {
            builder.AppendLine("## Branch Outcome");
            builder.AppendLine();
            if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey))
            {
                builder.AppendLine($"- Key: {output.BranchOutcomeKey.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(output.BranchOutcomeTitle))
            {
                builder.AppendLine($"- Title: {output.BranchOutcomeTitle.Trim()}");
            }

            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(output.HumanReadableSummaryMarkdown))
        {
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.AppendLine(output.HumanReadableSummaryMarkdown.Trim());
            builder.AppendLine();
        }

        AppendList(builder, "Agent Evidence Refs", output.EvidenceRefs);
        AppendList(builder, "Next Actions", output.NextActions);
        return builder.ToString();
    }

    private static string BuildManagedOutcomeArtifactAppendixContent(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        string primaryRef)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("## Runtime Validated Structured Outcome");
        builder.AppendLine();
        builder.AppendLine("The process runtime appended this section after validating the structured process step outcome.");
        builder.AppendLine();
        builder.AppendLine($"- Run id: {assignment.RunId.Value:D}");
        builder.AppendLine($"- Step id: {assignment.StepInstanceId.Value:D}");
        builder.AppendLine($"- Step key: {assignment.StepKey}");
        builder.AppendLine($"- Executor: {assignment.ExecutorDisplayName}");
        builder.AppendLine($"- Status: {output.Status}");
        builder.AppendLine($"- Primary managed ref: {primaryRef}");
        builder.AppendLine($"- Appended at UTC: {DateTimeOffset.UtcNow:u}");
        builder.AppendLine();
        builder.AppendLine("### Reason");
        builder.AppendLine();
        builder.AppendLine(output.Reason.Trim());
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey) ||
            !string.IsNullOrWhiteSpace(output.BranchOutcomeTitle))
        {
            builder.AppendLine("### Branch Outcome");
            builder.AppendLine();
            if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey))
            {
                builder.AppendLine($"- Key: {output.BranchOutcomeKey.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(output.BranchOutcomeTitle))
            {
                builder.AppendLine($"- Title: {output.BranchOutcomeTitle.Trim()}");
            }

            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(output.HumanReadableSummaryMarkdown))
        {
            builder.AppendLine("### Summary");
            builder.AppendLine();
            builder.AppendLine(output.HumanReadableSummaryMarkdown.Trim());
            builder.AppendLine();
        }

        AppendList(builder, "Agent Evidence Refs", output.EvidenceRefs);
        AppendList(builder, "Next Actions", output.NextActions);
        return builder.ToString();
    }

    private static void AppendList(
        StringBuilder builder,
        string heading,
        IReadOnlyList<string> values)
    {
        var items = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
        if (items.Length == 0)
        {
            return;
        }

        builder.AppendLine($"## {heading}");
        builder.AppendLine();
        foreach (var item in items)
        {
            builder.AppendLine($"- {item}");
        }

        builder.AppendLine();
    }

    private static ToolExecutionReceiptRecord CreateManagedOutcomeArtifactReceipt(
        Guid executionRunId,
        string primaryRef,
        string writeMessage,
        string toolName = "workspace_write_file")
        => new(
            Guid.NewGuid(),
            executionRunId,
            "process-runtime",
            toolName,
            "ManagedProcessArtifact",
            "NotRequired",
            "Process runtime persisted validated structured step outcome.",
            primaryRef,
            ".",
            $"Succeeded: {writeMessage}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private sealed record ManagedOutcomeArtifactMaterialization(
        ProcessStepOutcomeResult Output,
        IReadOnlyList<ToolExecutionReceiptRecord> ToolReceipts,
        ProcessCompletionIssue? Issue)
    {
        public static ManagedOutcomeArtifactMaterialization Unchanged(
            ProcessStepOutcomeResult output,
            IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
            => new(output, toolReceipts, null);

        public static ManagedOutcomeArtifactMaterialization Succeeded(
            ProcessStepOutcomeResult output,
            IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
            => new(output, toolReceipts, null);

        public static ManagedOutcomeArtifactMaterialization Failed(
            ProcessStepOutcomeResult output,
            IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts,
            ProcessCompletionIssue issue)
            => new(output, toolReceipts, issue);
    }

    private static string BuildProcessExecutionMetadata(ProcessRuntimeStepAssignment assignment)
    {
        var allowedOperations = ResolveEffectiveOperations(assignment.StepKey, assignment.AllowedOperations);
        var targetScope = string.IsNullOrWhiteSpace(assignment.OperationTargetScope)
            ? string.Empty
            : assignment.OperationTargetScope.Trim();
        var allowsProductMutation = AllowsProductMutation(allowedOperations, targetScope);
        var allowsBrowserProof = AllowsBrowserRuntimeProof(allowedOperations);
        var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [ExecutionInvocationMetadata.ProcessBrowserToolsAllowedMetadataKey] = allowsBrowserProof,
            [ExecutionInvocationMetadata.ProcessStepAllowedOperationsMetadataKey] = allowedOperations,
            [ExecutionInvocationMetadata.ProcessStepTargetScopeMetadataKey] = targetScope,
            [ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey] = allowsProductMutation
        };
        var trustedAliases = ResolveTrustedExternalTargetAliases(assignment.LaunchVariables);
        if (trustedAliases.Count > 0 && ShouldGroundExternalTargetAliases(allowedOperations, targetScope))
        {
            metadata[allowsProductMutation
                ? ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey
                : ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey] = trustedAliases;
        }

        var metadataJson = ApplyLaunchContextMetadata(
            JsonSerializer.Serialize(metadata, AgentOutputJson.SerializerOptions),
            assignment.LaunchVariables);
        return ExecutionInvocationMetadata.Build(
            metadataJson,
            new ExecutionInvocationPolicy(
                FinalizerMode: AgentFinalizerMode.Required,
                MaxStructuredOutputRepairAttempts: ExecutionInvocationMetadata.DefaultGovernedRepairAttempts));
    }

    private static string ApplyLaunchContextMetadata(
        string metadataJson,
        IReadOnlyDictionary<string, string> launchVariables)
    {
        metadataJson = ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
            metadataJson,
            ResolveProjectWorkspaceScope(launchVariables));
        metadataJson = ExecutionInvocationMetadata.ApplyProjectStructureLaunchAgent(
            metadataJson,
            ResolveProjectStructureLaunchAgent(launchVariables));
        return ExecutionInvocationMetadata.ApplyProjectStructureProcessNodeContext(
            metadataJson,
            ResolveProjectStructureProcessNodeContext(launchVariables));
    }

    private static WorkspaceScopeDescriptor? ResolveProjectWorkspaceScope(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        return TryResolveLaunchGuid(launchVariables, ProcessLaunchVariableNames.ProjectId, out var projectId)
            ? WorkspaceScopeDescriptor.Project(projectId.ToString("D"))
            : null;
    }

    private static ProjectStructureAgentIdentityDescriptor? ResolveProjectStructureLaunchAgent(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        var descriptor = new ProjectStructureAgentIdentityDescriptor(
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.AgentId),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.AgentName),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.MachineName),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.RepositoryRoot),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.BranchName),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.SessionId));
        return descriptor.HasLeaseOwnerIdentity ? descriptor : null;
    }

    private static ProjectStructureProcessNodeContextDescriptor? ResolveProjectStructureProcessNodeContext(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        var descriptor = new ProjectStructureProcessNodeContextDescriptor(
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.CurrentProcessRunNodeId),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.ProcessRunNodeId),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.ParentProcessRunNodeId),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.TargetProcessRunNodeId));
        return descriptor.HasAnyProcessRunNode ? descriptor : null;
    }

    private static bool TryResolveLaunchGuid(
        IReadOnlyDictionary<string, string> launchVariables,
        string key,
        out Guid value)
    {
        value = Guid.Empty;
        return launchVariables.TryGetValue(key, out var rawValue) &&
               Guid.TryParse(rawValue, out value) &&
               value != Guid.Empty;
    }

    private static string ResolveLaunchVariable(
        IReadOnlyDictionary<string, string> launchVariables,
        string key)
    {
        return launchVariables.TryGetValue(key, out var value)
            ? value.Trim()
            : string.Empty;
    }

    private static IReadOnlyList<string> NormalizeOperations(IReadOnlyList<string> operations)
    {
        return operations
            .Where(operation => !string.IsNullOrWhiteSpace(operation))
            .Select(operation => operation.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(operation => operation, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveEffectiveOperations(
        string stepKey,
        IReadOnlyList<string> allowedOperations)
    {
        var normalized = NormalizeOperations(allowedOperations);
        if (!IsRuntimeProofCaptureStep(stepKey))
        {
            return normalized;
        }

        return NormalizeOperations(
        [
            .. normalized,
            ProcessOperationContractNames.LaunchRuntime,
            ProcessOperationContractNames.CaptureRuntimeProof
        ]);
    }

    private static bool IsRuntimeProofCaptureStep(string stepKey)
    {
        return stepKey.Contains("screenshot", StringComparison.OrdinalIgnoreCase) ||
            stepKey.Contains("browser-proof", StringComparison.OrdinalIgnoreCase) ||
            stepKey.Contains("runtime-proof", StringComparison.OrdinalIgnoreCase);
    }

    private static bool AllowsProductMutation(
        IReadOnlyList<string> allowedOperations,
        string targetScope)
    {
        return allowedOperations.Contains(ProcessOperationContractNames.MutateProductTarget, StringComparer.OrdinalIgnoreCase) ||
            string.Equals(targetScope, ProcessOperationContractNames.ExternalProductTargetMutable, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(targetScope, ProcessOperationContractNames.ManagedOutputProduct, StringComparison.OrdinalIgnoreCase);
    }

    private static bool AllowsBrowserRuntimeProof(IReadOnlyList<string> allowedOperations)
    {
        return allowedOperations.Contains(ProcessOperationContractNames.CaptureRuntimeProof, StringComparer.OrdinalIgnoreCase);
    }

    private static bool UsesExternalProductTarget(string targetScope)
    {
        return string.Equals(targetScope, ProcessOperationContractNames.ExternalProductTargetMutable, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(targetScope, ProcessOperationContractNames.ExternalProductTargetReadOnly, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldGroundExternalTargetAliases(
        IReadOnlyList<string> allowedOperations,
        string targetScope)
    {
        return UsesExternalProductTarget(targetScope) ||
            string.Equals(targetScope, ProcessOperationContractNames.ExternalActionControlled, StringComparison.OrdinalIgnoreCase) ||
            allowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ResolveTrustedExternalTargetAliases(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        return launchVariables
            .Where(item => TrustedExternalTargetVariableNames.Contains(item.Key))
            .Select(item => AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(item.Value))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Where(item => item.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static readonly HashSet<string> TrustedExternalTargetVariableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ExternalTargetAlias",
        "ExternalTargetRoot",
        "OutputFolder",
        "OutputRoot",
        "OutputRootAlias",
        "ProductRoot",
        "ProductRootAlias",
        "WorkspaceAlias"
    };

    internal static async ValueTask<ProcessRunId?> TryResolvePendingChildRunAsync(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IProcessRuntimeStateStore stateStore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(stateStore);

        if (output.Status is not (ProcessStepOutcomeStatus.Blocked or ProcessStepOutcomeStatus.WaitingApproval) ||
            !CanWaitOnControlledChildRun(assignment))
        {
            return null;
        }

        var currentState = await stateStore.LoadAsync(assignment.RunId, cancellationToken).ConfigureAwait(false);
        if (currentState is null)
        {
            return null;
        }

        foreach (var candidateRunId in ExtractReferencedRunIds(output))
        {
            var pendingRunId = await TryResolveNonTerminalProcessTreeRunAsync(
                assignment.RunId,
                currentState,
                candidateRunId,
                stateStore,
                cancellationToken).ConfigureAwait(false);
            if (pendingRunId is not null)
            {
                return pendingRunId;
            }
        }

        return null;
    }

    private static ProcessRuntimeDispatchDeferredException CreatePendingChildRunDeferredException(
        ProcessRuntimeStepAssignment assignment,
        ProcessRunId pendingChildRunId)
    {
        return new ProcessRuntimeDispatchDeferredException(
            $"Step '{assignment.StepKey}' is waiting for active child process run '{pendingChildRunId}'.",
            pendingChildRunId);
    }

    internal static async ValueTask<ProcessRunId?> TryResolveExistingPendingChildRunAsync(
        ProcessRuntimeStepAssignment assignment,
        IProcessRuntimeStepAssignmentStore assignmentStore,
        IProcessRuntimeStateStore stateStore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(assignmentStore);
        ArgumentNullException.ThrowIfNull(stateStore);

        if (!CanWaitOnControlledChildRun(assignment))
        {
            return null;
        }

        var currentState = await stateStore.LoadAsync(assignment.RunId, cancellationToken).ConfigureAwait(false);
        if (currentState is null)
        {
            return null;
        }

        var childAssignments = await assignmentStore
            .FindByLaunchVariablesAsync(
                ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                    assignment.RunId,
                    assignment.StepInstanceId),
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var candidateRunId in childAssignments
            .OrderByDescending(childAssignment => childAssignment.CreatedAtUtc)
            .Select(childAssignment => childAssignment.RunId)
            .Distinct())
        {
            var pendingRunId = await TryResolveNonTerminalProcessTreeRunAsync(
                assignment.RunId,
                currentState,
                candidateRunId,
                stateStore,
                cancellationToken).ConfigureAwait(false);
            if (pendingRunId is not null)
            {
                return pendingRunId;
            }
        }

        return null;
    }

    private static async ValueTask<ProcessRunId?> TryResolveNonTerminalProcessTreeRunAsync(
        ProcessRunId currentRunId,
        ProcessRuntimeStateSnapshot currentState,
        ProcessRunId candidateRunId,
        IProcessRuntimeStateStore stateStore,
        CancellationToken cancellationToken)
    {
        if (candidateRunId == currentRunId)
        {
            return null;
        }

        var candidateState = await stateStore.LoadAsync(candidateRunId, cancellationToken).ConfigureAwait(false);
        if (candidateState is null ||
            ProcessRuntimeChildRunParentQuery.IsStoppedChildStatus(candidateState.Status) ||
            !IsSameProcessTree(currentState, candidateState))
        {
            return null;
        }

        return candidateRunId;
    }

    private static bool CanWaitOnControlledChildRun(ProcessRuntimeStepAssignment assignment)
    {
        return assignment.AllowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase) ||
            string.Equals(assignment.OperationTargetScope, ProcessOperationContractNames.ExternalActionControlled, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameProcessTree(
        ProcessRuntimeStateSnapshot currentState,
        ProcessRuntimeStateSnapshot candidateState)
    {
        return candidateState.RootRunId == currentState.RootRunId ||
            candidateState.RootRunId == currentState.RunId ||
            candidateState.RunId == currentState.RootRunId;
    }

    private static IReadOnlyList<ProcessRunId> ExtractReferencedRunIds(ProcessStepOutcomeResult output)
    {
        var runIds = new List<ProcessRunId>();
        foreach (var text in EnumerateOutcomeText(output))
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (Match match in ProcessRunIdRegex().Matches(text))
            {
                if (Guid.TryParse(match.Value, out var runGuid))
                {
                    var runId = new ProcessRunId(runGuid);
                    if (!runIds.Contains(runId))
                    {
                        runIds.Add(runId);
                    }
                }
            }
        }

        return runIds;
    }

    private static IEnumerable<string?> EnumerateOutcomeText(ProcessStepOutcomeResult output)
    {
        yield return output.Reason;
        yield return output.BranchOutcomeKey;
        yield return output.BranchOutcomeTitle;
        yield return output.HumanReadableSummaryMarkdown;

        foreach (var evidenceRef in output.EvidenceRefs)
        {
            yield return evidenceRef;
        }

        foreach (var nextAction in output.NextActions)
        {
            yield return nextAction;
        }
    }

    private static class ProcessLaunchVariableNames
    {
        public const string AgentId = "AgentId";
        public const string AgentName = "AgentName";
        public const string BranchName = "BranchName";
        public const string CurrentProcessRunNodeId = "CurrentProcessRunNodeId";
        public const string MachineName = "MachineName";
        public const string ParentProcessRunNodeId = "ParentProcessRunNodeId";
        public const string ProjectId = "ProjectId";
        public const string ProcessRunNodeId = "ProcessRunNodeId";
        public const string RepositoryRoot = "RepositoryRoot";
        public const string SessionId = "SessionId";
        public const string TargetProcessRunNodeId = "TargetProcessRunNodeId";
    }

    internal static ProcessExecutionAdapterResult ToAdapterResult(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        string rawOutputHash,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts = null)
    {
        var outcome = output.Status switch
        {
            ProcessStepOutcomeStatus.Completed => StrategyOutcome.Succeeded,
            ProcessStepOutcomeStatus.Blocked or ProcessStepOutcomeStatus.WaitingApproval => StrategyOutcome.NeedsManager,
            ProcessStepOutcomeStatus.Refused => StrategyOutcome.Canceled,
            ProcessStepOutcomeStatus.Failed => StrategyOutcome.Failed,
            _ => StrategyOutcome.Failed
        };
        if (outcome == StrategyOutcome.Succeeded &&
            ValidateProductMutationCompletion(assignment, output) is { } productMutationIssue)
        {
            return NeedsManagerForCompletionIssue(assignment, rawOutputHash, productMutationIssue);
        }

        if (outcome == StrategyOutcome.Succeeded &&
            ValidateManagedArtifactCompletion(assignment, output) is { } managedArtifactIssue)
        {
            return NeedsManagerForCompletionIssue(assignment, rawOutputHash, managedArtifactIssue);
        }

        if (outcome == StrategyOutcome.Succeeded &&
            ValidateManagedArtifactWriteReceipt(assignment, toolReceipts) is { } managedArtifactWriteIssue)
        {
            return NeedsManagerForCompletionIssue(assignment, rawOutputHash, managedArtifactWriteIssue);
        }

        IReadOnlyList<ProducedArtifactRef> artifacts = outcome == StrategyOutcome.Succeeded
            ? assignment.ProducedArtifactSlotIds
                .Select(slotId => new ProducedArtifactRef(
                    ArtifactInstanceId.New(),
                    slotId,
                    ComputeHash($"{rawOutputHash}:{assignment.StepInstanceId}:{slotId}")))
                .ToArray()
            : [];
        IReadOnlyList<RequestedArtifactRef> requestedArtifacts = outcome == StrategyOutcome.NeedsManager
            ? assignment.RequiredArtifactSlotIds
                .Select(slotId => new RequestedArtifactRef(
                    slotId,
                    ComputeHash($"{rawOutputHash}:requested:{slotId}")))
                .ToArray()
            : [];
        var diagnostics = new List<ProcessExecutionAdapterDiagnostic>();
        var managerSignals = new List<ManagerSignal>();
        var userSafeSummary = output.Reason;
        if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey))
        {
            managerSignals.Add(new ManagerSignal(
                ProcessBranchSignalCodes.Outcome(output.BranchOutcomeKey),
                ComputeHash(output.BranchOutcomeKey),
                string.IsNullOrWhiteSpace(output.BranchOutcomeTitle)
                    ? $"Branch outcome selected: {output.BranchOutcomeKey}"
                    : output.BranchOutcomeTitle));
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            TryBuildAgentRightsManagerRequest(assignment, output, out var managerRequest))
        {
            var rightsHash = ComputeHash($"{rawOutputHash}:agent-rights:{managerRequest}");
            diagnostics.Add(new ProcessExecutionAdapterDiagnostic(
                new StrategyDiagnosticCode(AgentRightsManagerRequestCode),
                StrategyDiagnosticSensitivity.Normal,
                rightsHash,
                managerRequest,
                RestrictedEvidenceReference: null,
                ProcessDiagnosticRetrySafety.UnsafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Idempotent));
            managerSignals.Add(new ManagerSignal(
                new ManagerSignalCode(AgentRightsManagerRequestCode),
                rightsHash,
                managerRequest));
            userSafeSummary = string.IsNullOrWhiteSpace(userSafeSummary)
                ? managerRequest
                : $"{userSafeSummary}{Environment.NewLine}{Environment.NewLine}{managerRequest}";
        }

        return new ProcessExecutionAdapterResult(
            outcome,
            artifacts,
            requestedArtifacts,
            diagnostics,
            managerSignals,
            userSafeSummary,
            rawOutputHash);
    }

    private const string AgentRightsManagerRequestCode = "process.adapter.agent_rights_request";

    private static bool TryBuildAgentRightsManagerRequest(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        out string managerRequest)
    {
        managerRequest = string.Empty;
        var issueText = FirstNonEmpty(
            output.Reason,
            output.HumanReadableSummaryMarkdown ?? string.Empty,
            string.Join(" ", output.NextActions));
        if (!LooksLikeRightsOrToolBoundary(issueText))
        {
            return false;
        }

        var deniedToolOrRight = ResolveDeniedToolOrRight(issueText);
        var operations = NormalizeOperations(assignment.AllowedOperations);
        var operationsSummary = operations.Count == 0
            ? "none declared"
            : string.Join(", ", operations);
        var scope = string.IsNullOrWhiteSpace(assignment.OperationTargetScope)
            ? "unspecified"
            : assignment.OperationTargetScope.Trim();
        var executor = string.IsNullOrWhiteSpace(assignment.ExecutorDisplayName)
            ? assignment.ExecutorId
            : assignment.ExecutorDisplayName.Trim();
        var mutationSummary = AllowsProductMutation(operations, assignment.OperationTargetScope)
            ? "product mutation allowed"
            : "product mutation not allowed";

        managerRequest =
            $"Manager action required: step '{assignment.StepKey}' in run '{assignment.RunId}' is assigned to '{executor}' but reported a tool/right boundary problem for {deniedToolOrRight}. Grant the missing right/tool to this agent or reassign the step to an agent that already has it, then retry the step. Required operation contract: allowed operations [{operationsSummary}], target scope '{scope}', {mutationSummary}.";
        return true;
    }

    private static bool LooksLikeRightsOrToolBoundary(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsAny(
            text,
            "PolicyDenied",
            "blocked by policy",
            "missing tool",
            "tool is not part of the composed capability set",
            "not authorized to use tool",
            "permission",
            "permissions",
            "right",
            "rights",
            "capability",
            "access denied",
            "workspace boundary",
            "outside the current run boundary",
            "approval path",
            "denied tool");
    }

    private static string ResolveDeniedToolOrRight(string text)
    {
        var quotedTool = Regex.Match(text, @"Tool '([^']+)'", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (quotedTool.Success)
        {
            return $"tool '{quotedTool.Groups[1].Value}'";
        }

        return "the denied or unavailable tool/right named in the blocker";
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static ProcessCompletionIssue? ValidateProductMutationCompletion(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            !AllowsProductMutation(NormalizeOperations(assignment.AllowedOperations), assignment.OperationTargetScope))
        {
            return null;
        }

        if (output.EvidenceRefs.Count == 0 ||
            output.EvidenceRefs.All(string.IsNullOrWhiteSpace))
        {
            return new ProcessCompletionIssue(
                "process.adapter.product_output_evidence_missing",
                $"Step '{assignment.StepKey}' claimed completion for a product-mutating scope but returned no evidence references.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:evidence-missing",
                [],
                ProcessDiagnosticRetrySafety.SafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Idempotent);
        }

        if (!TryResolveInspectableProductRoot(assignment.LaunchVariables, out var productRoot))
        {
            return null;
        }

        var inspection = InspectProductRoot(productRoot);
        if (inspection.HasProductFiles)
        {
            return null;
        }

        return new ProcessCompletionIssue(
            "process.adapter.product_output_missing",
            inspection.Summary.Length == 0
                ? $"Step '{assignment.StepKey}' claimed completion but the configured product output root '{productRoot}' contains no product files."
                : $"Step '{assignment.StepKey}' claimed completion but the configured product output root '{productRoot}' is not usable: {inspection.Summary}",
            productRoot,
            [],
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static ProcessCompletionIssue? ValidateManagedArtifactCompletion(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            assignment.ProducedArtifactSlotIds.Count == 0)
        {
            return null;
        }

        var evidenceRefs = output.EvidenceRefs
            .Where(evidenceRef => !string.IsNullOrWhiteSpace(evidenceRef))
            .Select(NormalizeManagedArtifactRef)
            .Where(evidenceRef => evidenceRef.Length > 0)
            .ToArray();
        var missingSlotIds = assignment.ProducedArtifactSlotIds
            .Where(slotId => !HasManagedArtifactEvidence(assignment, slotId, evidenceRefs))
            .Distinct()
            .ToArray();
        if (missingSlotIds.Length == 0)
        {
            return null;
        }

        var expectedRefs = missingSlotIds
            .SelectMany(slotId => EnumerateManagedArtifactEvidenceRefs(assignment, slotId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new ProcessCompletionIssue(
            "process.adapter.produced_artifact_evidence_missing",
            $"Step '{assignment.StepKey}' claimed completion but did not return a managed artifact evidence ref for produced slot(s): {string.Join(", ", missingSlotIds)}. Expected one of: {string.Join("; ", expectedRefs)}",
            $"{assignment.RunId}:{assignment.StepInstanceId}:produced-artifact-evidence-missing:{string.Join(",", missingSlotIds)}:{string.Join("|", output.EvidenceRefs)}",
            missingSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static ProcessCompletionIssue? ValidateManagedArtifactWriteReceipt(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
    {
        if (assignment.ProducedArtifactSlotIds.Count == 0 ||
            toolReceipts is null)
        {
            return null;
        }

        var primaryRef = NormalizeManagedArtifactRef(BuildManagedStepArtifactPath(assignment));
        if (HasManagedArtifactWriteReceipt(toolReceipts, primaryRef))
        {
            return null;
        }

        return new ProcessCompletionIssue(
            "process.adapter.produced_artifact_write_receipt_missing",
            $"Step '{assignment.StepKey}' claimed completion but did not produce a successful workspace_write_file or workspace_append_file receipt for primary managed artifact '{BuildManagedStepArtifactPath(assignment)}'.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:produced-artifact-write-receipt-missing:{string.Join("|", toolReceipts.Select(receipt => $"{receipt.ToolName}:{receipt.RequestSummary}:{receipt.ExitSummary}"))}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static bool HasManagedArtifactWriteReceipt(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
        => HasManagedArtifactWriteReceipt(
            toolReceipts,
            NormalizeManagedArtifactRef(BuildManagedStepArtifactPath(assignment)));

    private static bool HasManagedArtifactWriteReceipt(
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts,
        string primaryRef)
        => toolReceipts.Any(receipt =>
            IsManagedArtifactWriteTool(receipt.ToolName) &&
            IsSuccessfulReceipt(receipt.ExitSummary) &&
            ReceiptTargetsManagedRef(receipt.RequestSummary, primaryRef));

    private static bool IsManagedArtifactWriteTool(string toolName)
        => string.Equals(toolName, "workspace_write_file", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(toolName, "workspace_append_file", StringComparison.OrdinalIgnoreCase);

    private static bool IsSuccessfulReceipt(string exitSummary)
        => exitSummary.StartsWith("Succeeded", StringComparison.OrdinalIgnoreCase);

    private static bool ReceiptTargetsManagedRef(string requestSummary, string expectedRef)
    {
        var normalizedRequest = NormalizeManagedArtifactRef(requestSummary);
        var expectedTail = expectedRef.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase)
            ? expectedRef["artifacts".Length..]
            : expectedRef;
        return string.Equals(normalizedRequest, expectedRef, StringComparison.OrdinalIgnoreCase) ||
               normalizedRequest.Contains(expectedRef, StringComparison.OrdinalIgnoreCase) ||
               normalizedRequest.Contains(expectedTail, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasManagedArtifactEvidence(
        ProcessRuntimeStepAssignment assignment,
        ArtifactSlotId slotId,
        IReadOnlyList<string> evidenceRefs)
    {
        if (evidenceRefs.Count == 0)
        {
            return false;
        }

        var stepPath = NormalizeManagedArtifactRef(BuildManagedStepArtifactPath(assignment));
        var slotRoot = NormalizeManagedArtifactRef(BuildManagedSlotArtifactRoot(assignment, slotId));
        var stepRoot = NormalizeManagedArtifactRef(BuildManagedStepArtifactRoot(assignment));
        var stepRootPrefix = stepRoot + "/";
        return evidenceRefs.Any(evidenceRef =>
            string.Equals(evidenceRef, stepPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(evidenceRef, slotRoot, StringComparison.OrdinalIgnoreCase) ||
            evidenceRef.StartsWith(stepRootPrefix, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> EnumerateManagedArtifactEvidenceRefs(
        ProcessRuntimeStepAssignment assignment,
        ArtifactSlotId slotId)
    {
        yield return BuildManagedStepArtifactPath(assignment);
        yield return BuildManagedSlotArtifactRoot(assignment, slotId);
        yield return BuildManagedStepArtifactRoot(assignment) + "/";
    }

    private static string BuildManagedStepArtifactPath(ProcessRuntimeStepAssignment assignment)
        => $"{BuildManagedArtifactRoot(assignment)}/steps/{SanitizeManagedArtifactPathSegment(assignment.StepKey)}.md";

    private static string BuildManagedSlotArtifactRoot(
        ProcessRuntimeStepAssignment assignment,
        ArtifactSlotId slotId)
        => $"{BuildManagedArtifactRoot(assignment)}/{slotId}";

    private static string BuildManagedStepArtifactRoot(ProcessRuntimeStepAssignment assignment)
        => $"{BuildManagedArtifactRoot(assignment)}/{SanitizeManagedArtifactPathSegment(assignment.StepKey)}";

    private static string BuildManagedArtifactRoot(ProcessRuntimeStepAssignment assignment)
        => $"artifacts/process-runs/{assignment.RunId.Value:D}";

    private static string NormalizeManagedArtifactRef(string value)
    {
        var normalized = value.Trim().Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        normalized = normalized.TrimEnd('/');
        if (normalized.StartsWith("artifacts/scopes/", StringComparison.OrdinalIgnoreCase))
        {
            var processRunsIndex = normalized.IndexOf("/process-runs/", StringComparison.OrdinalIgnoreCase);
            if (processRunsIndex >= 0)
            {
                return "artifacts" + normalized[processRunsIndex..];
            }
        }

        return normalized;
    }

    private static string SanitizeManagedArtifactPathSegment(string value)
    {
        var sanitized = ManagedArtifactPathSegmentInvalidCharactersRegex()
            .Replace(value.Trim(), "-")
            .Trim('-', '.', '_');
        return string.IsNullOrWhiteSpace(sanitized)
            ? "step"
            : sanitized;
    }

    private static ProcessExecutionAdapterResult NeedsManagerForCompletionIssue(
        ProcessRuntimeStepAssignment assignment,
        string rawOutputHash,
        ProcessCompletionIssue issue)
    {
        var requestedArtifactSlots = issue.RequestedArtifactSlotIds.Count > 0
            ? issue.RequestedArtifactSlotIds
            : assignment.ProducedArtifactSlotIds.Count > 0
                ? assignment.ProducedArtifactSlotIds
                : assignment.RequiredArtifactSlotIds;
        return new ProcessExecutionAdapterResult(
            StrategyOutcome.NeedsManager,
            [],
            requestedArtifactSlots
                .Select(slotId => new RequestedArtifactRef(
                    slotId,
                    ComputeHash($"{rawOutputHash}:requested:{slotId}:{issue.Code}")))
                .ToArray(),
            [
                new ProcessExecutionAdapterDiagnostic(
                    new StrategyDiagnosticCode(issue.Code),
                    StrategyDiagnosticSensitivity.Normal,
                    ComputeHash(issue.Evidence),
                    issue.Summary,
                    RestrictedEvidenceReference: null,
                    issue.RetrySafety,
                    issue.Idempotency)
            ],
            [
                new ManagerSignal(
                    new ManagerSignalCode(issue.Code),
                    ComputeHash($"{rawOutputHash}:manager:{issue.Code}:{issue.Evidence}"),
                    issue.Summary)
            ],
            issue.Summary,
            ComputeHash($"{rawOutputHash}:{issue.Code}:{issue.Evidence}"));
    }

    private static bool TryBuildRetryableAgentOutputContractIssue(
        ProcessRuntimeStepAssignment assignment,
        Exception exception,
        out ProcessCompletionIssue issue)
    {
        issue = null!;
        if (!LooksLikeAgentOutputContractFailure(exception))
        {
            return false;
        }

        var expectedRefs = assignment.ProducedArtifactSlotIds.Count > 0
            ? assignment.ProducedArtifactSlotIds
                .SelectMany(slotId => EnumerateManagedArtifactEvidenceRefs(assignment, slotId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [BuildManagedStepArtifactPath(assignment)];
        var expectedRefSummary = expectedRefs.Length == 0
            ? "the concrete current-run evidence ref required by the process step brief"
            : string.Join("; ", expectedRefs);
        issue = new ProcessCompletionIssue(
            "process.adapter.agent_output_contract_retryable",
            $"Agent execution for step '{assignment.StepKey}' did not produce a valid process-step finalizer outcome. Retry the step, create the required current-run evidence first, and only return Completed after submit_process_step_outcome evidenceRefs contains one of: {expectedRefSummary}. Runtime detail: {exception.Message}",
            $"{assignment.RunId}:{assignment.StepInstanceId}:agent-output-contract:{exception.GetType().FullName}:{exception.Message}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
        return true;
    }

    private static bool LooksLikeAgentOutputContractFailure(Exception exception)
    {
        var text = exception.ToString();
        return ContainsAny(
            text,
            "submit_process_step_outcome",
            "Required finalizer tool",
            "process_step_outcome_result",
            "ProcessStepOutcomeResult",
            "process.step_outcome",
            "agent.finalizer",
            "agent.output");
    }

    private static bool TryResolveInspectableProductRoot(
        IReadOnlyDictionary<string, string> launchVariables,
        out string productRoot)
    {
        productRoot = FirstNonEmpty(
            ResolveLaunchVariable(launchVariables, "OutputFolder"),
            ResolveLaunchVariable(launchVariables, "OutputRoot"),
            ResolveLaunchVariable(launchVariables, "ProductRoot"),
            ResolveLaunchVariable(launchVariables, "ExternalTargetRoot"));
        if (string.IsNullOrWhiteSpace(productRoot) ||
            productRoot.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase) ||
            !Path.IsPathFullyQualified(productRoot))
        {
            productRoot = string.Empty;
            return false;
        }

        productRoot = Path.GetFullPath(productRoot);
        return true;
    }

    private static ProductRootInspection InspectProductRoot(string productRoot)
    {
        try
        {
            if (!Directory.Exists(productRoot))
            {
                return new ProductRootInspection(false, "the directory does not exist");
            }

            return Directory
                .EnumerateFiles(productRoot, "*", SearchOption.AllDirectories)
                .Any(file => IsProductFile(productRoot, file))
                ? new ProductRootInspection(true, string.Empty)
                : new ProductRootInspection(false, "no product files were found");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or NotSupportedException)
        {
            return new ProductRootInspection(false, exception.Message);
        }
    }

    private static bool IsProductFile(string productRoot, string file)
    {
        var relativePath = Path.GetRelativePath(productRoot, file);
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Any(IsIgnoredProductPathSegment))
        {
            return false;
        }

        var fileName = Path.GetFileName(file);
        return !string.Equals(fileName, ".gitkeep", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(fileName, ".DS_Store", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(fileName, "Thumbs.db", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIgnoredProductPathSegment(string segment)
        => string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, ".vs", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, "node_modules", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, "packages", StringComparison.OrdinalIgnoreCase);

    private static ProcessExecutionAdapterResult Failed(
        string code,
        string summary,
        string evidence)
    {
        return new ProcessExecutionAdapterResult(
            StrategyOutcome.Failed,
            [],
            [],
            [
                new ProcessExecutionAdapterDiagnostic(
                    new StrategyDiagnosticCode(code),
                    StrategyDiagnosticSensitivity.Normal,
                    ComputeHash(evidence),
                    summary,
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.Unknown,
                    ProcessDiagnosticIdempotencyClassification.Unknown)
            ],
            [],
            summary,
            ComputeHash($"{code}:{evidence}"));
    }

    private static ProcessExecutionAdapterResult Canceled(
        string code,
        string summary,
        string evidence)
    {
        var evidenceHash = ComputeHash(evidence);
        return new ProcessExecutionAdapterResult(
            StrategyOutcome.Canceled,
            [],
            [],
            [
                new ProcessExecutionAdapterDiagnostic(
                    new StrategyDiagnosticCode(code),
                    StrategyDiagnosticSensitivity.Normal,
                    evidenceHash,
                    summary,
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.UnsafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [],
            summary,
            ComputeHash($"{code}:{evidence}"));
    }

    private static ProcessExecutionAdapterResult NeedsManager(
        string code,
        string summary,
        string evidence)
    {
        var evidenceHash = ComputeHash(evidence);
        return new ProcessExecutionAdapterResult(
            StrategyOutcome.NeedsManager,
            [],
            [],
            [
                new ProcessExecutionAdapterDiagnostic(
                    new StrategyDiagnosticCode(code),
                    StrategyDiagnosticSensitivity.Normal,
                    evidenceHash,
                    summary,
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.UnsafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [
                new ManagerSignal(
                    new ManagerSignalCode(code),
                    evidenceHash,
                    summary)
            ],
            summary,
            ComputeHash($"{code}:{evidence}"));
    }

    private static AgentProcessRoleReadinessRequest CreateRuntimeReadinessRequest(ProcessRuntimeStepAssignment assignment)
    {
        return new AgentProcessRoleReadinessRequest(
            assignment.StepKey,
            assignment.StepKey,
            assignment.RoleKey,
            assignment.RoleResourceKey,
            assignment.RoleDisplayName,
            NormalizeOperations(assignment.AllowedOperations),
            assignment.OperationTargetScope);
    }

    private static string FormatValidationErrors(IReadOnlyList<AgentOutputValidationError> errors)
    {
        if (errors.Count == 0)
        {
            return "Agent output did not satisfy the process step outcome contract.";
        }

        return string.Join("; ", errors.Select(error => $"{error.Code}: {error.Message}"));
    }

    private static string ComputeHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    [GeneratedRegex(
        @"(?<![0-9a-fA-F])[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}(?![0-9a-fA-F])",
        RegexOptions.CultureInvariant)]
    private static partial Regex ProcessRunIdRegex();

    [GeneratedRegex("[^A-Za-z0-9._-]+", RegexOptions.CultureInvariant)]
    private static partial Regex ManagedArtifactPathSegmentInvalidCharactersRegex();

    private sealed record ProcessCompletionIssue(
        string Code,
        string Summary,
        string Evidence,
        IReadOnlyList<ArtifactSlotId> RequestedArtifactSlotIds,
        ProcessDiagnosticRetrySafety RetrySafety,
        ProcessDiagnosticIdempotencyClassification Idempotency);

    private sealed record ProductRootInspection(
        bool HasProductFiles,
        string Summary);
}

internal sealed class AgentFrameworkProcessExecutionObservationReader(
    IAgentReferenceDataProvider agentReferenceDataProvider,
    IAgentFrameworkWorkspaceService workspaceService) : IProcessExecutionObservationReader
{
    public async ValueTask<IReadOnlyList<ProcessExecutionObservation>> ListAsync(
        ProcessExecutionObservationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.RunIds.Count == 0)
        {
            return [];
        }

        var referenceData = await agentReferenceDataProvider
            .GetAsync(new AgentReferenceDataRequest(AgentReferenceDataSections.Agents), cancellationToken)
            .ConfigureAwait(false);
        var agentNameById = referenceData.Agents.ToDictionary(agent => agent.Id, agent => agent.Name);
        var agentAvatarById = referenceData.Agents.ToDictionary(agent => agent.Id, agent => agent.AvatarImageUrl ?? string.Empty);
        var requestedRunIds = query.RunIds.ToHashSet();
        var executionRuns = await ListExecutionRunsAsync(query, cancellationToken).ConfigureAwait(false);
        var observations = new List<ProcessExecutionObservation>();

        foreach (var executionRun in executionRuns
                     .OrderByDescending(run => run.UpdatedAtUtc)
                     .GroupBy(
                         run => run.ProcessRunId,
                         StringComparer.OrdinalIgnoreCase)
                     .SelectMany(group => group.Take(Math.Max(1, query.TakePerRun))))
        {
            if (!TryParseProcessIdentity(executionRun, out var processRunId, out var stepInstanceId) ||
                !requestedRunIds.Contains(processRunId))
            {
                continue;
            }

            ExecutionRunDetail? detail = null;
            try
            {
                detail = await workspaceService.GetExecutionRunDetailAsync(executionRun.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
            }

            var detailRun = detail?.Run ?? executionRun;
            var agentName = agentNameById.GetValueOrDefault(detailRun.AgentId);
            var agentAvatarImageUrl = agentAvatarById.GetValueOrDefault(detailRun.AgentId) ?? string.Empty;
            observations.Add(new ProcessExecutionObservation(
                detailRun.Id,
                processRunId,
                stepInstanceId,
                detailRun.AgentId,
                FirstNonEmpty(agentName, detailRun.RequestedBy, detailRun.AgentId.ToString("D")),
                detailRun.ProviderName,
                detailRun.Model,
                detailRun.State.ToString(),
                detailRun.Outcome?.ToString() ?? string.Empty,
                detailRun.CreatedAtUtc,
                detailRun.UpdatedAtUtc,
                detailRun.StartedAtUtc,
                detailRun.CompletedAtUtc,
                detailRun.InputSummary,
                detailRun.ResultSummary,
                MapActivities(detail),
                MapTools(detail),
                MapArtifacts(detail),
                ResolveLastError(detail))
            {
                AgentAvatarImageUrl = agentAvatarImageUrl
            });
        }

        return observations
            .OrderByDescending(observation => observation.UpdatedAtUtc)
            .ToArray();
    }

    private async Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
        ProcessExecutionObservationQuery query,
        CancellationToken cancellationToken)
    {
        var executionRuns = new List<ExecutionRunRecord>();
        foreach (var runId in query.RunIds.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var runRecords = await workspaceService.ListExecutionRunsAsync(
                new ExecutionRunQuery(
                    Take: Math.Max(1, query.TakePerRun),
                    ProcessRunId: runId.ToString(),
                    UpdatedFromUtc: query.FromUtc,
                    UpdatedToUtc: query.ToUtc),
                cancellationToken).ConfigureAwait(false);
            executionRuns.AddRange(runRecords);
        }

        return executionRuns;
    }

    private static IReadOnlyList<ProcessExecutionActivityObservation> MapActivities(ExecutionRunDetail? detail)
        => detail?.ExecutionLog
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .Take(8)
            .OrderBy(entry => entry.CreatedAtUtc)
            .Select(entry => new ProcessExecutionActivityObservation(
                entry.CreatedAtUtc,
                entry.State.ToString(),
                entry.Phase,
                entry.Message))
            .ToArray() ?? [];

    private static IReadOnlyList<ProcessExecutionToolObservation> MapTools(ExecutionRunDetail? detail)
        => detail?.ToolReceipts
            .OrderByDescending(tool => tool.CompletedAtUtc)
            .Take(8)
            .OrderBy(tool => tool.StartedAtUtc)
            .Select(tool => new ProcessExecutionToolObservation(
                tool.ToolName,
                tool.RuntimeToolProviderKey,
                tool.RequestSummary,
                tool.ExitSummary,
                tool.StartedAtUtc,
                tool.CompletedAtUtc))
            .ToArray() ?? [];

    private static IReadOnlyList<ProcessExecutionArtifactObservation> MapArtifacts(ExecutionRunDetail? detail)
        => detail?.Artifacts
            .OrderByDescending(artifact => artifact.CreatedAtUtc)
            .Take(8)
            .OrderBy(artifact => artifact.CreatedAtUtc)
            .Select(artifact => new ProcessExecutionArtifactObservation(
                artifact.ArtifactKind,
                artifact.DisplayName,
                artifact.RelativePath,
                artifact.Summary,
                artifact.CreatedAtUtc))
            .ToArray() ?? [];

    private static string ResolveLastError(ExecutionRunDetail? detail)
    {
        if (detail is null)
        {
            return string.Empty;
        }

        return detail.ExecutionLog
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .FirstOrDefault(entry =>
                entry.State == ExecutionState.Failed ||
                entry.Phase.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                entry.Message.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                entry.Message.Contains("exception", StringComparison.OrdinalIgnoreCase))
            ?.Message ?? string.Empty;
    }

    private static bool TryParseProcessIdentity(
        ExecutionRunRecord executionRun,
        out ProcessRunId runId,
        out ProcessStepInstanceId stepInstanceId)
    {
        runId = default;
        stepInstanceId = default;

        return Guid.TryParse(executionRun.ProcessRunId, out var parsedRunId) &&
               parsedRunId != Guid.Empty &&
               Guid.TryParse(executionRun.ProcessStepId, out var parsedStepId) &&
               parsedStepId != Guid.Empty &&
               TryCreateProcessRunId(parsedRunId, out runId) &&
               TryCreateStepInstanceId(parsedStepId, out stepInstanceId);
    }

    private static bool TryCreateProcessRunId(Guid value, out ProcessRunId runId)
    {
        try
        {
            runId = new ProcessRunId(value);
            return true;
        }
        catch (ArgumentException)
        {
            runId = default;
            return false;
        }
    }

    private static bool TryCreateStepInstanceId(Guid value, out ProcessStepInstanceId stepInstanceId)
    {
        try
        {
            stepInstanceId = new ProcessStepInstanceId(value);
            return true;
        }
        catch (ArgumentException)
        {
            stepInstanceId = default;
            return false;
        }
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}

internal sealed class AgentFrameworkProcessRuntimeUsageTelemetryReader(
    IAgentReferenceDataProvider agentReferenceDataProvider,
    IAgentFrameworkWorkspaceService workspaceService) : IProcessRuntimeUsageTelemetryReader
{
    private const int ContextEstimatedInputTokenWarningThreshold = 128_000;
    private const int ContextToolSchemaTokenWarningThreshold = 32_000;
    private const int ContextToolCountWarningThreshold = 64;
    private const int UsageExecutionRunBatchTake = 5_000;

    public async ValueTask<IReadOnlyList<ProcessRuntimeUsageObservation>> ListAsync(
        ProcessRuntimeUsageTelemetryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.RunIds.Count == 0)
        {
            return [];
        }

        var runIdSet = query.RunIds.ToHashSet();
        var referenceData = await agentReferenceDataProvider
            .GetAsync(new AgentReferenceDataRequest(AgentReferenceDataSections.Providers), cancellationToken)
            .ConfigureAwait(false);
        var providers = referenceData.Providers;
        var executionRuns = await ListExecutionRunsAsync(query, cancellationToken).ConfigureAwait(false);
        var observations = new List<ProcessRuntimeUsageObservation>();

        foreach (var executionRun in executionRuns
                     .OrderByDescending(run => run.UpdatedAtUtc))
        {
            if (!TryCreateProcessRunId(executionRun.ProcessRunId, out var executionProcessRunId) ||
                !runIdSet.Contains(executionProcessRunId))
            {
                continue;
            }

            ExecutionRunDetail detail;
            try
            {
                detail = await workspaceService.GetExecutionRunDetailAsync(executionRun.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            foreach (var usageObservation in detail.UsageObservations)
            {
                if (usageObservation.CreatedAtUtc < query.FromUtc ||
                    usageObservation.CreatedAtUtc > query.ToUtc ||
                    !TryResolveProcessRunId(usageObservation, detail.Run, out var processRunId) ||
                    !runIdSet.Contains(processRunId))
                {
                    continue;
                }

                observations.Add(MapUsageObservation(usageObservation, detail.Run, processRunId, providers));
            }
        }

        return observations
            .OrderBy(observation => observation.CreatedAtUtc)
            .ThenBy(observation => observation.UsageObservationId)
            .ToArray();
    }

    private async Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
        ProcessRuntimeUsageTelemetryQuery query,
        CancellationToken cancellationToken)
    {
        var runIds = query.RunIds
            .Select(runId => runId.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (runIds.Count == 0)
        {
            return [];
        }

        var requestedTake = Math.Max(query.TakePerRun * runIds.Count, runIds.Count);
        var take = Math.Clamp(
            requestedTake,
            runIds.Count,
            Math.Max(runIds.Count, UsageExecutionRunBatchTake));
        var executionRuns = await workspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                Take: take,
                UpdatedFromUtc: query.FromUtc,
                UpdatedToUtc: query.ToUtc),
            cancellationToken).ConfigureAwait(false);
        return executionRuns
            .Where(run => runIds.Contains(run.ProcessRunId))
            .ToArray();
    }

    private static ProcessRuntimeUsageObservation MapUsageObservation(
        ProviderUsageObservation usageObservation,
        ExecutionRunRecord? executionRun,
        ProcessRunId processRunId,
        IReadOnlyList<ProviderProfile> providers)
    {
        var isKnownUsage = ProviderPricingCalculator.IsKnownUsageStatus(usageObservation.UsageStatus);
        var actualCostUsd = isKnownUsage &&
            ProviderPricingCalculator.TryResolveObservationCost(usageObservation, providers, out var knownCostUsd)
                ? knownCostUsd
                : 0m;
        var estimatedCostUsd = usageObservation.UsageStatus == ProviderUsageObservationStatus.EstimatedFromMetric
            ? ResolveEstimatedCost(usageObservation, providers)
            : 0m;
        var contextSummary = ResolveRuntimeContextSummary(usageObservation.DiagnosticsJson);

        return new ProcessRuntimeUsageObservation(
            usageObservation.Id,
            usageObservation.ExecutionRunId ?? executionRun?.Id ?? Guid.Empty,
            processRunId,
            TryResolveStepInstanceId(usageObservation, executionRun),
            usageObservation.CreatedAtUtc,
            usageObservation.ProviderName,
            usageObservation.Model,
            usageObservation.SourcePhase,
            usageObservation.UsageStatus.ToString(),
            isKnownUsage,
            Math.Max(0, usageObservation.InputTokens),
            Math.Clamp(usageObservation.CachedInputTokens, 0, Math.Max(0, usageObservation.InputTokens)),
            Math.Max(0, usageObservation.OutputTokens),
            Math.Max(0, usageObservation.ReasoningTokens),
            Math.Max(0, usageObservation.TotalTokens),
            decimal.Round(estimatedCostUsd, 6, MidpointRounding.AwayFromZero),
            decimal.Round(actualCostUsd, 6, MidpointRounding.AwayFromZero))
        {
            ContextEstimatedInputTokens = contextSummary.EstimatedInputTokens,
            ContextInputMessageCount = contextSummary.InputMessageCount,
            ContextToolCount = contextSummary.ToolCount,
            ContextToolSchemaEstimatedTokens = contextSummary.ToolSchemaEstimatedTokens,
            ContextSourceCount = contextSummary.SourceCount,
            ContextBudgetExceeded = HasRuntimeContextBudgetWarning(contextSummary),
            ContextBudgetWarning = ResolveRuntimeContextBudgetWarning(contextSummary),
            ContextDiagnosticsJson = contextSummary.DiagnosticsJson
        };
    }

    private static RuntimeContextUsageSummary ResolveRuntimeContextSummary(string diagnosticsJson)
    {
        if (string.IsNullOrWhiteSpace(diagnosticsJson))
        {
            return RuntimeContextUsageSummary.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(diagnosticsJson);
            if (!document.RootElement.TryGetProperty("contextAssemblyManifest", out var manifest) ||
                manifest.ValueKind != JsonValueKind.Object)
            {
                return RuntimeContextUsageSummary.Empty;
            }

            if (!manifest.TryGetProperty("totals", out var totals) ||
                totals.ValueKind != JsonValueKind.Object)
            {
                return RuntimeContextUsageSummary.Empty;
            }

            var sourceCount = manifest.TryGetProperty("sources", out var sources) && sources.ValueKind == JsonValueKind.Array
                ? sources.GetArrayLength()
                : 0;
            return new RuntimeContextUsageSummary(
                ReadInt32(totals, "estimatedInputTokens"),
                ReadInt32(totals, "inputMessageCount"),
                ReadInt32(totals, "toolCount"),
                ReadInt32(totals, "toolSchemaEstimatedTokens"),
                sourceCount,
                manifest.GetRawText());
        }
        catch (JsonException)
        {
            return RuntimeContextUsageSummary.Empty;
        }
    }

    private static int ReadInt32(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? Math.Max(0, value)
            : 0;

    private static bool HasRuntimeContextBudgetWarning(RuntimeContextUsageSummary contextSummary)
        => contextSummary.EstimatedInputTokens >= ContextEstimatedInputTokenWarningThreshold ||
           contextSummary.ToolSchemaEstimatedTokens >= ContextToolSchemaTokenWarningThreshold ||
           contextSummary.ToolCount >= ContextToolCountWarningThreshold;

    private static string ResolveRuntimeContextBudgetWarning(RuntimeContextUsageSummary contextSummary)
    {
        if (!HasRuntimeContextBudgetWarning(contextSummary))
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            "Agent context request shape is above the diagnostic warning threshold.",
            $"EstimatedInputTokens={contextSummary.EstimatedInputTokens}.",
            $"ToolCount={contextSummary.ToolCount}.",
            $"ToolSchemaEstimatedTokens={contextSummary.ToolSchemaEstimatedTokens}.",
            $"SourceCount={contextSummary.SourceCount}.");
    }

    private static decimal ResolveEstimatedCost(
        ProviderUsageObservation usageObservation,
        IReadOnlyList<ProviderProfile> providers)
    {
        if (usageObservation.ProviderCostUsd is > 0m)
        {
            return usageObservation.ProviderCostUsd.Value;
        }

        if (usageObservation.CalculatedCostUsd is > 0m)
        {
            return usageObservation.CalculatedCostUsd.Value;
        }

        var provider = providers.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, usageObservation.ProviderName, StringComparison.OrdinalIgnoreCase));
        if (provider is not null &&
            ProviderPricingCalculator.TryCalculate(
                provider.Name,
                usageObservation.Model,
                usageObservation.InputTokens,
                usageObservation.CachedInputTokens,
                ProviderPricingCalculator.ResolveBillableOutputTokens(
                    usageObservation.InputTokens,
                    usageObservation.OutputTokens,
                    usageObservation.TotalTokens),
                provider.ModelPrices,
                out var cost))
        {
            return cost.TotalUsd;
        }

        return 0m;
    }

    private static bool TryResolveProcessRunId(
        ProviderUsageObservation usageObservation,
        ExecutionRunRecord? executionRun,
        out ProcessRunId processRunId)
    {
        if (TryCreateProcessRunId(usageObservation.ProcessRunId, out processRunId))
        {
            return true;
        }

        return executionRun is not null &&
               TryCreateProcessRunId(executionRun.ProcessRunId, out processRunId);
    }

    private static ProcessStepInstanceId? TryResolveStepInstanceId(
        ProviderUsageObservation usageObservation,
        ExecutionRunRecord? executionRun)
    {
        if (TryCreateStepInstanceId(usageObservation.ProcessStepId, out var observationStepId))
        {
            return observationStepId;
        }

        return executionRun is not null &&
               TryCreateStepInstanceId(executionRun.ProcessStepId, out var executionStepId)
            ? executionStepId
            : null;
    }

    private static bool TryCreateProcessRunId(string value, out ProcessRunId processRunId)
    {
        processRunId = default;
        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
        {
            return false;
        }

        try
        {
            processRunId = new ProcessRunId(parsed);
            return true;
        }
        catch (ArgumentException)
        {
            processRunId = default;
            return false;
        }
    }

    private static bool TryCreateStepInstanceId(string value, out ProcessStepInstanceId stepInstanceId)
    {
        stepInstanceId = default;
        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
        {
            return false;
        }

        try
        {
            stepInstanceId = new ProcessStepInstanceId(parsed);
            return true;
        }
        catch (ArgumentException)
        {
            stepInstanceId = default;
            return false;
        }
    }

    private sealed record RuntimeContextUsageSummary(
        int EstimatedInputTokens,
        int InputMessageCount,
        int ToolCount,
        int ToolSchemaEstimatedTokens,
        int SourceCount,
        string DiagnosticsJson)
    {
        public static RuntimeContextUsageSummary Empty { get; } = new(
            EstimatedInputTokens: 0,
            InputMessageCount: 0,
            ToolCount: 0,
            ToolSchemaEstimatedTokens: 0,
            SourceCount: 0,
            DiagnosticsJson: string.Empty);
    }
}

internal sealed class AgentFrameworkProcessExecutionClaimRecoveryCoordinator(
    IProcessProjectionClock clock,
    IProcessRuntimeStateStore stateStore,
    IProcessRuntimeStepAssignmentStore assignmentStore,
    IProcessRuntimeUnitOfWork unitOfWork,
    IProcessRuntimeDispatchQueue dispatchQueue,
    ProcessRuntimeProjectionCatchupService projectionCatchupService,
    ILogger<AgentFrameworkProcessExecutionClaimRecoveryCoordinator> logger)
{
    private const int MaximumConcurrencyRetries = 3;
    private static readonly TimeSpan ConcurrencyRetryDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan RecoveredExecutionClaimAssociationWindow = TimeSpan.FromMinutes(2);

    public async Task<bool> ReleaseRecoveredExecutionClaimAsync(
        Guid executionRunId,
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId,
        string requestedBy,
        CancellationToken cancellationToken = default,
        DateTimeOffset? recoveredExecutionCreatedAtUtc = null)
    {
        var normalizedRequestedBy = NormalizeRequestedBy(requestedBy);
        for (var attempt = 1; attempt <= MaximumConcurrencyRetries; attempt++)
        {
            try
            {
                return await TryReleaseRecoveredExecutionClaimAsync(
                    executionRunId,
                    runId,
                    stepInstanceId,
                    normalizedRequestedBy,
                    recoveredExecutionCreatedAtUtc,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt < MaximumConcurrencyRetries)
            {
                await Task.Delay(ConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    public static bool IsRecoverableExecutionFailure(ExecutionState state, RunOutcome? outcome)
        => state == ExecutionState.Failed &&
           outcome is RunOutcome.Cancelled or RunOutcome.Failed;

    public static bool IsRecoverableExecutionCompletion(ExecutionState state, RunOutcome? outcome)
        => state == ExecutionState.Completed &&
           outcome == RunOutcome.Succeeded;

    public async Task<bool> SubmitRecoveredExecutionResultAsync(
        ExecutionRunRecord executionRun,
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId,
        string requestedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executionRun);

        var normalizedRequestedBy = NormalizeRequestedBy(requestedBy);
        for (var attempt = 1; attempt <= MaximumConcurrencyRetries; attempt++)
        {
            try
            {
                return await TrySubmitRecoveredExecutionResultAsync(
                    executionRun,
                    runId,
                    stepInstanceId,
                    normalizedRequestedBy,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt < MaximumConcurrencyRetries)
            {
                await Task.Delay(ConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    private async Task<bool> TryReleaseRecoveredExecutionClaimAsync(
        Guid executionRunId,
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId,
        string requestedBy,
        DateTimeOffset? recoveredExecutionCreatedAtUtc,
        CancellationToken cancellationToken)
    {
        var state = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false);
        if (state is null || ProcessRuntimeTerminalStates.IsRunTerminal(state.Status))
        {
            return false;
        }

        var step = state.Steps.FirstOrDefault(candidate => candidate.StepInstanceId == stepInstanceId);
        if (step is null ||
            step.ActiveClaimToken is not { } claimToken ||
            step.Status is not (ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running))
        {
            return false;
        }

        var claim = state.Claims.FirstOrDefault(candidate =>
            candidate.StepInstanceId == stepInstanceId &&
            candidate.ClaimToken == claimToken &&
            candidate.Status is DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed);
        if (claim is null)
        {
            return false;
        }

        if (recoveredExecutionCreatedAtUtc is { } executionCreatedAtUtc &&
            !CanAssociateClaimWithRecoveredExecution(claim.CreatedAtUtc, executionCreatedAtUtc))
        {
            logger.LogInformation(
                "Skipping process claim recovery release for execution run {ExecutionRunId} because claim {ClaimToken} was created after the recovered execution association window. ProcessRunId={RunId} StepInstanceId={StepInstanceId} ClaimCreatedAtUtc={ClaimCreatedAtUtc} ExecutionCreatedAtUtc={ExecutionCreatedAtUtc}",
                executionRunId,
                claim.ClaimToken,
                runId.Value,
                stepInstanceId.Value,
                claim.CreatedAtUtc,
                executionCreatedAtUtc);
            return false;
        }

        var engine = new ProcessRuntimeEngine(unitOfWork);
        var releaseCommit = await engine.ReleaseClaimAsync(
            state,
            CreateContext(requestedBy),
            new ReleaseDispatchClaimCommand(stepInstanceId, claim.OwnerId, claim.ClaimToken),
            cancellationToken).ConfigureAwait(false);
        if (!releaseCommit.Succeeded)
        {
            logger.LogWarning(
                "Process execution recovery could not release claim {ClaimToken} for run {RunId}, step {StepInstanceId}. Diagnostics={Diagnostics}",
                claim.ClaimToken,
                runId.Value,
                stepInstanceId.Value,
                string.Join("; ", releaseCommit.Diagnostics.Select(diagnostic => diagnostic.Message)));
            return false;
        }

        await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);
        await dispatchQueue.EnqueueAsync(
            new ProcessRuntimeDispatchQueueRequest(runId, requestedBy),
            cancellationToken).ConfigureAwait(false);

        var recoverySource = executionRunId == Guid.Empty
            ? "missing AgentFramework execution run"
            : $"interrupted execution run {executionRunId:D}";
        logger.LogInformation(
            "Process execution recovery released claim {ClaimToken} for {RecoverySource}. ProcessRunId={RunId} StepInstanceId={StepInstanceId}",
            claim.ClaimToken,
            recoverySource,
            runId.Value,
            stepInstanceId.Value);
        return true;
    }

    private async Task<bool> TrySubmitRecoveredExecutionResultAsync(
        ExecutionRunRecord executionRun,
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId,
        string requestedBy,
        CancellationToken cancellationToken)
    {
        if (!IsRecoverableExecutionCompletion(executionRun.State, executionRun.Outcome))
        {
            return false;
        }

        var assignment = await assignmentStore.LoadAsync(runId, stepInstanceId, cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            logger.LogWarning(
                "Completed AgentFramework execution run {ExecutionRunId} could not be reconciled because process assignment {RunId}/{StepInstanceId} was not found.",
                executionRun.Id,
                runId.Value,
                stepInstanceId.Value);
            return false;
        }

        var validation = AgentOutputJson.DeserializeAndValidate(
            executionRun.ResultSummary,
            new ProcessStepOutcomeValidator());
        if (!validation.Succeeded || validation.Output is null)
        {
            logger.LogWarning(
                "Completed AgentFramework execution run {ExecutionRunId} could not be reconciled because its process step output was invalid. RawOutputHash={RawOutputHash} Errors={Errors}",
                executionRun.Id,
                validation.RawOutputHash,
                string.Join("; ", validation.Validation.Errors.Select(error => $"{error.Code}: {error.Message}")));
            return false;
        }

        var state = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false);
        if (state is null || ProcessRuntimeTerminalStates.IsRunTerminal(state.Status))
        {
            return false;
        }

        var step = state.Steps.FirstOrDefault(candidate => candidate.StepInstanceId == stepInstanceId);
        if (step is null ||
            step.ActiveClaimToken is not { } claimToken ||
            step.Status is not (ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running))
        {
            return false;
        }

        var claim = state.Claims.FirstOrDefault(candidate =>
            candidate.StepInstanceId == stepInstanceId &&
            candidate.ClaimToken == claimToken &&
            candidate.Status is DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed);
        if (claim is null)
        {
            return false;
        }

        if (!CanAssociateClaimWithRecoveredExecution(claim.CreatedAtUtc, executionRun.CreatedAtUtc))
        {
            logger.LogInformation(
                "Skipping recovered AgentFramework execution result {ExecutionRunId} because claim {ClaimToken} was created after the recovered execution association window. ProcessRunId={RunId} StepInstanceId={StepInstanceId} ClaimCreatedAtUtc={ClaimCreatedAtUtc} ExecutionCreatedAtUtc={ExecutionCreatedAtUtc}",
                executionRun.Id,
                claim.ClaimToken,
                runId.Value,
                stepInstanceId.Value,
                claim.CreatedAtUtc,
                executionRun.CreatedAtUtc);
            return false;
        }

        var context = CreateContext(
            requestedBy,
            NormalizeRecoveredResultTimestamp(executionRun.CompletedAtUtc ?? executionRun.UpdatedAtUtc, claim.ExpiresAtUtc));
        var adapterResult = AgentFrameworkProcessExecutionAdapter.ToAdapterResult(
            assignment,
            validation.Output,
            validation.RawOutputHash);
        var result = CreateRecoveredStrategyResult(executionRun, adapterResult);
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var commit = await engine.SubmitStrategyResultAsync(
            state,
            context,
            new SubmitStrategyResultCommand(
                stepInstanceId,
                claim.OwnerId,
                claim.ClaimToken,
                new StrategyResultIdempotencyKey(result.IdempotencyKey),
                result),
            cancellationToken).ConfigureAwait(false);
        if (!commit.Succeeded)
        {
            logger.LogWarning(
                "Completed AgentFramework execution run {ExecutionRunId} could not submit process result for run {RunId}, step {StepInstanceId}. Diagnostics={Diagnostics}",
                executionRun.Id,
                runId.Value,
                stepInstanceId.Value,
                string.Join("; ", commit.Diagnostics.Select(diagnostic => diagnostic.Message)));
            return false;
        }

        await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);
        await dispatchQueue.EnqueueAsync(
            new ProcessRuntimeDispatchQueueRequest(runId, requestedBy),
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Recovered completed AgentFramework execution run {ExecutionRunId} into process run {RunId}, step {StepInstanceId}. Outcome={Outcome}",
            executionRun.Id,
            runId.Value,
            stepInstanceId.Value,
            adapterResult.Outcome);
        return true;
    }

    private static StrategyResultEnvelope CreateRecoveredStrategyResult(
        ExecutionRunRecord executionRun,
        ProcessExecutionAdapterResult adapterResult)
    {
        var strategy = StandardProcessAdapterDescriptors.WorkflowAdapter.Strategy;
        var idempotencyKey = CreateDeterministicGuid($"agent-framework-process-result:{executionRun.Id:N}");
        return new StrategyResultEnvelope(
            strategy.StrategyId,
            strategy.StrategyVersion,
            idempotencyKey,
            adapterResult.Outcome,
            adapterResult.ProducedArtifacts,
            adapterResult.RequestedArtifacts,
            adapterResult.Diagnostics
                .Select(diagnostic => new StrategyDiagnosticRef(
                    diagnostic.Code,
                    diagnostic.Sensitivity,
                    diagnostic.EvidenceHash,
                    diagnostic.SafeSummary,
                    diagnostic.RestrictedEvidenceReference,
                    diagnostic.RetrySafety,
                    diagnostic.Idempotency))
                .ToArray(),
            adapterResult.ManagerSignals,
            adapterResult.ResultHash);
    }

    private static DateTimeOffset NormalizeRecoveredResultTimestamp(
        DateTimeOffset executionCompletedAtUtc,
        DateTimeOffset claimExpiresAtUtc)
    {
        var completedAtUtc = NormalizeUtc(executionCompletedAtUtc);
        var expiresAtUtc = NormalizeUtc(claimExpiresAtUtc);
        return completedAtUtc < expiresAtUtc
            ? completedAtUtc
            : expiresAtUtc.AddTicks(-1);
    }

    private RuntimeCommandContext CreateContext(
        string requestedBy,
        DateTimeOffset occurredAtUtc)
    {
        return new RuntimeCommandContext(
            RuntimeCommandId.New(),
            new ProcessEventActor(ProcessEventActorKind.System, new ProcessActorId(requestedBy)),
            new ProcessCorrelationId($"{requestedBy}-{Guid.NewGuid():N}"),
            NormalizeUtc(occurredAtUtc));
    }

    private RuntimeCommandContext CreateContext(string requestedBy)
    {
        return CreateContext(requestedBy, clock.GetUtcNow());
    }

    private static Guid CreateDeterministicGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private static string NormalizeRequestedBy(string requestedBy)
        => string.IsNullOrWhiteSpace(requestedBy)
            ? "agent-execution-recovery"
            : requestedBy.Trim();

    internal static bool CanAssociateClaimWithRecoveredExecution(
        DateTimeOffset claimCreatedAtUtc,
        DateTimeOffset executionCreatedAtUtc)
        => NormalizeUtc(claimCreatedAtUtc) <= NormalizeUtc(executionCreatedAtUtc).Add(RecoveredExecutionClaimAssociationWindow);

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value)
        => value.Offset == TimeSpan.Zero ? value : value.ToUniversalTime();
}

internal sealed class AgentFrameworkProcessExecutionRecoveryObserver(
    AgentFrameworkProcessExecutionClaimRecoveryCoordinator recoveryCoordinator,
    IAgentFrameworkWorkspaceService workspaceService,
    ILogger<AgentFrameworkProcessExecutionRecoveryObserver> logger) : IAgentExecutionRecoveryObserver
{
    private const string RecoveryRequestedBy = "agent-execution-recovery";
    private const int ProcessStepExecutionTake = 25;

    public async Task OnExecutionRecoveredAsync(
        AgentExecutionRecoveryObservation observation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (!AgentFrameworkProcessExecutionClaimRecoveryCoordinator.IsRecoverableExecutionFailure(observation.State, observation.Outcome) ||
            !Guid.TryParse(observation.ProcessRunId, out var processRunGuid) ||
            !Guid.TryParse(observation.ProcessStepId, out var processStepGuid))
        {
            return;
        }

        var processStepExecutions = await workspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                Take: ProcessStepExecutionTake,
                ProcessRunId: observation.ProcessRunId,
                ProcessStepId: observation.ProcessStepId),
            cancellationToken).ConfigureAwait(false);
        var recoveredExecution = processStepExecutions
            .FirstOrDefault(run => run.Id == observation.ExecutionRunId);
        if (recoveredExecution is null)
        {
            logger.LogWarning(
                "Skipping process claim release for recovered AgentFramework execution {ExecutionRunId} because the execution run record was not found. ProcessRunId={ProcessRunId} ProcessStepId={ProcessStepId}",
                observation.ExecutionRunId,
                observation.ProcessRunId,
                observation.ProcessStepId);
            return;
        }

        if (HasNewerActiveExecutionRun(processStepExecutions, recoveredExecution))
        {
            logger.LogInformation(
                "Skipping process claim release for recovered AgentFramework execution {ExecutionRunId} because a newer active execution exists for the same process step. ProcessRunId={ProcessRunId} ProcessStepId={ProcessStepId}",
                observation.ExecutionRunId,
                observation.ProcessRunId,
                observation.ProcessStepId);
            return;
        }

        await recoveryCoordinator.ReleaseRecoveredExecutionClaimAsync(
            observation.ExecutionRunId,
            new ProcessRunId(processRunGuid),
            new ProcessStepInstanceId(processStepGuid),
            RecoveryRequestedBy,
            cancellationToken,
            recoveredExecutionCreatedAtUtc: recoveredExecution.CreatedAtUtc).ConfigureAwait(false);
    }

    internal static bool HasNewerActiveExecutionRun(
        IReadOnlyList<ExecutionRunRecord> processStepExecutions,
        ExecutionRunRecord recoveredExecution)
    {
        ArgumentNullException.ThrowIfNull(processStepExecutions);
        ArgumentNullException.ThrowIfNull(recoveredExecution);

        var recoveredCreatedAtUtc = NormalizeUtc(recoveredExecution.CreatedAtUtc);
        return processStepExecutions.Any(run =>
            run.Id != recoveredExecution.Id &&
            run.State is not ExecutionState.Completed and not ExecutionState.Failed &&
            NormalizeUtc(run.CreatedAtUtc) > recoveredCreatedAtUtc &&
            string.Equals(run.ProcessRunId, recoveredExecution.ProcessRunId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(run.ProcessStepId, recoveredExecution.ProcessStepId, StringComparison.OrdinalIgnoreCase));
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value)
        => value.Offset == TimeSpan.Zero ? value : value.ToUniversalTime();
}

internal sealed class AgentFrameworkProcessRuntimeCancellationObserver(
    IAgentExecutionCancellationRegistry cancellationRegistry,
    ISandboxWorkspaceExecutionRunStore executionRunStore,
    ILogger<AgentFrameworkProcessRuntimeCancellationObserver> logger) : IProcessRuntimeRunCancellationObserver
{
    private const string CancellationPhase = "process-cancellation";
    private const string CancellationSummary = "Execution run cancelled because the owning process run was cancelled.";

    public async ValueTask<ProcessRuntimeRunCancellationObservationResult> OnRunsCancelledAsync(
        ProcessRuntimeRunCancellationObservation observation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var processRunIds = observation.CancelledRunIds
            .Select(runId => runId.Value.ToString("D"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (processRunIds.Length == 0)
        {
            return ProcessRuntimeRunCancellationObservationResult.Empty;
        }

        var signaledCount = cancellationRegistry.RequestCancellationByProcessRunIds(
            processRunIds,
            observation.RequestedBy,
            observation.Reason);
        var repairedCount = await MarkExecutionRunRecordsCancelledAsync(
            processRunIds,
            observation.CancelledAtUtc,
            cancellationToken).ConfigureAwait(false);
        var diagnostics = new List<string>();
        if (signaledCount > 0)
        {
            diagnostics.Add($"Signaled cancellation to {signaledCount} active AgentFramework execution run(s).");
        }

        if (repairedCount > 0)
        {
            diagnostics.Add($"Marked {repairedCount} AgentFramework execution run record(s) cancelled for the cancelled process run(s).");
        }

        if (signaledCount > 0 || repairedCount > 0)
        {
            logger.LogInformation(
                "Process cancellation signaled {SignaledCount} active AgentFramework execution run(s) and marked {RepairedCount} record(s) cancelled. ProcessRunIds={ProcessRunIds}",
                signaledCount,
                repairedCount,
                string.Join(", ", processRunIds));
        }

        return diagnostics.Count == 0
            ? ProcessRuntimeRunCancellationObservationResult.Empty
            : new ProcessRuntimeRunCancellationObservationResult(diagnostics);
    }

    private async Task<int> MarkExecutionRunRecordsCancelledAsync(
        IReadOnlyList<string> processRunIds,
        DateTimeOffset cancelledAtUtc,
        CancellationToken cancellationToken)
    {
        var processRunIdSet = processRunIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var executionRuns = await executionRunStore.ListExecutionRunsAsync(cancellationToken).ConfigureAwait(false);
        var activeRuns = executionRuns
            .Where(run =>
                processRunIdSet.Contains(run.ProcessRunId) &&
                IsActiveExecutionRun(run))
            .OrderBy(run => run.CreatedAtUtc)
            .ToArray();
        var repairedCount = 0;
        foreach (var executionRun in activeRuns)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var detail = await executionRunStore.GetExecutionRunDetailAsync(executionRun.Id, cancellationToken).ConfigureAwait(false);
            if (detail is null || !IsActiveExecutionRun(detail.Run))
            {
                continue;
            }

            var cancelledRun = detail.Run with
            {
                State = ExecutionState.Failed,
                Outcome = RunOutcome.Cancelled,
                ResultSummary = CancellationSummary,
                UpdatedAtUtc = cancelledAtUtc,
                CompletedAtUtc = cancelledAtUtc,
                RuntimeSessionKey = string.Empty,
                SerializedSessionStateJson = null,
                PendingApprovals = [],
                Revision = detail.Run.Revision + 1L
            };
            var cancelledSession = detail.ChatSession is null
                ? null
                : detail.ChatSession with
                {
                    UpdatedAtUtc = cancelledAtUtc,
                    Compatibility = null,
                    LatestExecutionRunId = cancelledRun.Id
                };
            var cancelledDetail = new ExecutionRunDetail(
                cancelledRun,
                cancelledSession,
                AppendCancellationLog(detail.ExecutionLog, cancelledRun, cancelledAtUtc),
                detail.Metrics)
            {
                UsageObservations = detail.UsageObservations,
                Approvals = detail.Approvals,
                Artifacts = detail.Artifacts,
                Checkpoints = detail.Checkpoints,
                ToolReceipts = detail.ToolReceipts
            };

            await executionRunStore.SaveExecutionRunDetailAsync(cancelledDetail, cancellationToken).ConfigureAwait(false);
            repairedCount++;
        }

        return repairedCount;
    }

    private static bool IsActiveExecutionRun(ExecutionRunRecord run)
    {
        return run.State is not ExecutionState.Completed and not ExecutionState.Failed;
    }

    private static IReadOnlyList<ExecutionLogEntry> AppendCancellationLog(
        IReadOnlyList<ExecutionLogEntry> executionLog,
        ExecutionRunRecord run,
        DateTimeOffset cancelledAtUtc)
    {
        var entry = new ExecutionLogEntry(
            Id: Guid.NewGuid(),
            AgentId: run.AgentId,
            ChatSessionId: run.ChatSessionId,
            CreatedAtUtc: cancelledAtUtc,
            State: ExecutionState.Failed,
            Phase: CancellationPhase,
            Message: CancellationSummary)
        {
            ExecutionRunId = run.Id
        };

        var entries = new List<ExecutionLogEntry>(executionLog.Count + 1)
        {
            entry
        };
        entries.AddRange(executionLog.Where(item => item.Id != entry.Id));
        return entries;
    }
}

internal sealed class AgentFrameworkProcessExecutionClaimRecoveryReconciler(
    ProcessPersistenceDbContext dbContext,
    IAgentFrameworkWorkspaceService workspaceService,
    AgentFrameworkProcessExecutionClaimRecoveryCoordinator recoveryCoordinator,
    IOptions<ProcessRuntimeDispatchQueueOptions> options,
    IProcessProjectionClock clock)
{
    private const int ExecutionTakePerClaim = 10;
    private const int MaxCandidateClaims = 250;
    private const string RecoveryRequestedBy = "agent-execution-reconciliation";
    private static readonly TimeSpan ExecutionCreationSkew = TimeSpan.FromSeconds(30);

    public async Task<int> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var candidates = await LoadActiveClaimCandidatesAsync(dbContext, cancellationToken).ConfigureAwait(false);
        var recoveredCount = 0;
        foreach (var candidate in candidates)
        {
            var executionRuns = await LoadExecutionRunsForClaimAsync(candidate, cancellationToken).ConfigureAwait(false);
            var executionRun = SelectRecoverableExecution(executionRuns, candidate);
            if (executionRun is null)
            {
                if (ShouldReleaseClaimWithoutExecution(
                        executionRuns,
                        candidate,
                        clock.GetUtcNow(),
                        options.Value.ActiveClaimWithoutExecutionRunStaleAfter))
                {
                    var released = await recoveryCoordinator.ReleaseRecoveredExecutionClaimAsync(
                        Guid.Empty,
                        new ProcessRunId(candidate.RunId),
                        new ProcessStepInstanceId(candidate.StepInstanceId),
                        RecoveryRequestedBy,
                        cancellationToken).ConfigureAwait(false);
                    if (released)
                    {
                        recoveredCount++;
                    }
                }

                continue;
            }

            var runId = new ProcessRunId(candidate.RunId);
            var stepInstanceId = new ProcessStepInstanceId(candidate.StepInstanceId);
            var recovered = AgentFrameworkProcessExecutionClaimRecoveryCoordinator.IsRecoverableExecutionCompletion(
                executionRun.State,
                executionRun.Outcome)
                ? await recoveryCoordinator.SubmitRecoveredExecutionResultAsync(
                    executionRun,
                    runId,
                    stepInstanceId,
                    RecoveryRequestedBy,
                    cancellationToken).ConfigureAwait(false)
                : await recoveryCoordinator.ReleaseRecoveredExecutionClaimAsync(
                    executionRun.Id,
                    runId,
                    stepInstanceId,
                    RecoveryRequestedBy,
                    cancellationToken,
                    recoveredExecutionCreatedAtUtc: executionRun.CreatedAtUtc).ConfigureAwait(false);

            if (recovered)
            {
                recoveredCount++;
            }
        }

        return recoveredCount;
    }

    internal static async Task<IReadOnlyList<ActiveProcessClaimCandidate>> LoadActiveClaimCandidatesAsync(
        ProcessPersistenceDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        return await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state => state.Status == ProcessRuntimeStatus.Active)
            .Join(
                dbContext.RuntimeSteps.AsNoTracking(),
                state => state.RunId,
                step => step.RunId,
                (state, step) => new { state.RunId, Step = step })
            .Where(item =>
                item.Step.ActiveClaimToken != null &&
                (item.Step.Status == ProcessRuntimeStepStatus.Claimed ||
                 item.Step.Status == ProcessRuntimeStepStatus.Running))
            .Join(
                dbContext.DispatchClaims.AsNoTracking(),
                item => item.RunId,
                claim => claim.RunId,
                (item, claim) => new { item.RunId, item.Step, Claim = claim })
            .Where(item =>
                item.Step.ActiveClaimToken == item.Claim.ClaimToken &&
                (item.Claim.Status == DispatchClaimStatus.Claimed ||
                 item.Claim.Status == DispatchClaimStatus.LeaseRenewed ||
                 item.Claim.Status == DispatchClaimStatus.Reclaimed))
            .OrderBy(item => item.Claim.ExpiresAtUtc)
            .Select(item => new ActiveProcessClaimCandidate(
                item.RunId,
                item.Step.StepInstanceId,
                item.Claim.ClaimToken,
                item.Claim.OwnerId,
                item.Claim.CreatedAtUtc,
                item.Claim.ExpiresAtUtc))
            .Take(MaxCandidateClaims)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    internal static ExecutionRunRecord? SelectRecoverableExecution(
        IReadOnlyList<ExecutionRunRecord> executionRuns,
        ActiveProcessClaimCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(executionRuns);

        var runId = candidate.RunId.ToString("D");
        var stepId = candidate.StepInstanceId.ToString("D");
        var earliestCreatedAtUtc = candidate.CreatedAtUtc - ExecutionCreationSkew;
        var latestExecution = executionRuns
            .Where(executionRun =>
                executionRun.CreatedAtUtc >= earliestCreatedAtUtc &&
                string.Equals(executionRun.ProcessRunId, runId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(executionRun.ProcessStepId, stepId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(executionRun => executionRun.CreatedAtUtc)
            .ThenByDescending(executionRun => executionRun.UpdatedAtUtc)
            .FirstOrDefault();

        return latestExecution is not null &&
               (AgentFrameworkProcessExecutionClaimRecoveryCoordinator.IsRecoverableExecutionFailure(
                    latestExecution.State,
                    latestExecution.Outcome) ||
                AgentFrameworkProcessExecutionClaimRecoveryCoordinator.IsRecoverableExecutionCompletion(
                    latestExecution.State,
                    latestExecution.Outcome))
            ? latestExecution
            : null;
    }

    internal static bool ShouldReleaseClaimWithoutExecution(
        IReadOnlyList<ExecutionRunRecord> executionRuns,
        ActiveProcessClaimCandidate candidate,
        DateTimeOffset nowUtc,
        TimeSpan staleAfter)
    {
        ArgumentNullException.ThrowIfNull(executionRuns);

        if (staleAfter <= TimeSpan.Zero)
        {
            return false;
        }

        if (NormalizeUtc(candidate.CreatedAtUtc).Add(staleAfter) > NormalizeUtc(nowUtc))
        {
            return false;
        }

        return !executionRuns.Any(executionRun => IsMatchingClaimExecution(executionRun, candidate));
    }

    private async Task<IReadOnlyList<ExecutionRunRecord>> LoadExecutionRunsForClaimAsync(
        ActiveProcessClaimCandidate candidate,
        CancellationToken cancellationToken)
    {
        return await workspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                Take: ExecutionTakePerClaim,
                ProcessRunId: candidate.RunId.ToString("D"),
                ProcessStepId: candidate.StepInstanceId.ToString("D"),
                CreatedFromUtc: candidate.CreatedAtUtc - ExecutionCreationSkew),
            cancellationToken).ConfigureAwait(false);
    }

    private static bool IsMatchingClaimExecution(
        ExecutionRunRecord executionRun,
        ActiveProcessClaimCandidate candidate)
    {
        var earliestCreatedAtUtc = NormalizeUtc(candidate.CreatedAtUtc - ExecutionCreationSkew);
        return NormalizeUtc(executionRun.CreatedAtUtc) >= earliestCreatedAtUtc &&
               string.Equals(executionRun.ProcessRunId, candidate.RunId.ToString("D"), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(executionRun.ProcessStepId, candidate.StepInstanceId.ToString("D"), StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value)
        => value.Offset == TimeSpan.Zero ? value : value.ToUniversalTime();

    internal sealed record ActiveProcessClaimCandidate(
        Guid RunId,
        Guid StepInstanceId,
        Guid ClaimToken,
        string OwnerId,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset ExpiresAtUtc);

}

internal sealed class AgentFrameworkProcessExecutionClaimRecoveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ProcessRuntimeDispatchQueueOptions> options,
    ILogger<AgentFrameworkProcessExecutionClaimRecoveryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ReconciliationInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.EnableRecovery)
        {
            return;
        }

        try
        {
            await Task.Delay(StartupDelay, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var reconciler = scope.ServiceProvider.GetRequiredService<AgentFrameworkProcessExecutionClaimRecoveryReconciler>();
                var recoveredCount = await reconciler.ReconcileAsync(stoppingToken).ConfigureAwait(false);
                if (recoveredCount > 0)
                {
                    logger.LogInformation(
                        "Process execution claim recovery reconciled {RecoveredCount} interrupted claim(s).",
                        recoveredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Process execution claim recovery reconciliation failed.");
            }

            try
            {
                await Task.Delay(ReconciliationInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
