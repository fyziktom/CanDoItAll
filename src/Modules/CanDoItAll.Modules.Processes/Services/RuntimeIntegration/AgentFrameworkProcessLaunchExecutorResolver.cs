using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
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
using AccessCapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;
using CapabilityOperationClassification = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityOperationClassification;
using RuntimeToolName = CanDoItAll.AgentFramework.Capabilities.Abstractions.RuntimeToolName;

namespace CanDoItAll.Modules.Processes;


internal sealed class AgentFrameworkProcessLaunchExecutorResolver(
    IAgentReferenceDataProvider agentReferenceDataProvider,
    ProcessMockAgentCatalogService processMockAgentCatalogService,
    IProviderProfileService providerProfileService,
    IWorkflowCatalogService workflowCatalog) : IProcessLaunchExecutorResolver
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
            if (ProcessLaunchExecutorKinds.IsWorkflow(requestedExecutorKind))
            {
                await ResolveWorkflowBindingAsync(
                    planStep.StepKey,
                    roleKey,
                    role,
                    profileAssignment,
                    executorOverride,
                    bindings,
                    findings,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (ResolveWorkflowBinding(role, profileAssignment, executorOverride) is not null)
            {
                findings.Add(new ProcessLaunchReadinessFinding(
                    ProcessLaunchReadinessSeverity.Error,
                    "process.launch.workflow_binding_executor_mismatch",
                    $"Step '{planStep.StepKey}' role '{roleKey}' declares a workflow binding but executor kind '{requestedExecutorKind}' is not Workflow.",
                    planStep.StepKey,
                    roleKey));
                continue;
            }

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

            if (!AddCapabilityScopeReadinessFindings(findings, planStep.StepKey, roleKey, templateStep.CapabilityScope))
            {
                continue;
            }

            if (!ValidateStepOperationContract(templateStep, roleKey, findings, planStep.StepKey))
            {
                continue;
            }

            var readinessRequest = CreateReadinessRequest(planStep.StepKey, templateStep, roleKey, role, request.Variables);

            var candidate = executorOverride is null
                ? SelectAgent(readinessRequest, agents, providerById, providerProfileService)
                : ResolveOverrideAgent(executorOverride, agents, providerById, providerProfileService, findings, planStep.StepKey, roleKey);
            if (candidate is null)
            {
                if (executorOverride is null)
                {
                    var addedReadinessFailure = AddBestRoleFitReadinessFailure(
                        findings,
                        readinessRequest,
                        agents,
                        providerById,
                        providerProfileService,
                        planStep.StepKey,
                        roleKey);

                    if (!addedReadinessFailure)
                    {
                        findings.Add(new ProcessLaunchReadinessFinding(
                            ProcessLaunchReadinessSeverity.Error,
                            "process.launch.agent_missing",
                            FormatMissingAgentMessage(roleQuery, planStep.StepKey),
                            planStep.StepKey,
                            roleKey));
                    }
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

        if (findings.All(finding => finding.Severity != ProcessLaunchReadinessSeverity.Error))
        {
            findings.Add(new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Info,
                "process.launch.readiness_ok",
                "All executable steps have explicit runnable executor bindings."));
        }

        return new ProcessLaunchExecutorResolution(bindings, findings);
    }

    private async ValueTask ResolveWorkflowBindingAsync(
        string stepKey,
        string roleKey,
        ProcessTemplateDefinitionRoleUsageDocument? role,
        ProcessTemplateLiveRunAssignmentDocument? profileAssignment,
        ProcessLaunchExecutorOverride? executorOverride,
        ICollection<ProcessLaunchExecutorBinding> bindings,
        ICollection<ProcessLaunchReadinessFinding> findings,
        CancellationToken cancellationToken)
    {
        var workflowBinding = ResolveWorkflowBinding(role, profileAssignment, executorOverride);
        if (workflowBinding is null)
        {
            findings.Add(new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Error,
                "process.launch.workflow_selection_required",
                $"Step '{stepKey}' role '{roleKey}' requires one explicitly selected workflow id.",
                stepKey,
                roleKey));
            return;
        }

        var workflowId = new WorkflowId(workflowBinding.WorkflowId.Value);
        WorkflowDefinitionDetail? detail;
        if (workflowBinding.WorkflowVersionId is { } workflowVersionId)
        {
            detail = await workflowCatalog.GetDefinitionAsync(
                workflowId,
                new WorkflowVersionId(workflowVersionId.Value),
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            detail = await workflowCatalog.GetLatestDefinitionByStatusAsync(
                workflowId,
                WorkflowLifecycleStatus.Active,
                cancellationToken).ConfigureAwait(false);
        }

        if (detail is null)
        {
            findings.Add(new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Error,
                "process.launch.workflow_not_found",
                workflowBinding.WorkflowVersionId is { } versionId
                    ? $"Workflow '{workflowBinding.WorkflowId.Value:D}' version '{versionId.Value:D}' was not found."
                    : $"Workflow '{workflowBinding.WorkflowId.Value:D}' does not have an Active version.",
                stepKey,
                roleKey));
            return;
        }

        var definition = detail.Definition;
        var exactVersionMatches = workflowBinding.WorkflowVersionId is not { } selectedVersionId ||
            definition.VersionId.Value == selectedVersionId.Value;
        if (definition.Id != workflowId ||
            !exactVersionMatches ||
            definition.Status != WorkflowLifecycleStatus.Active ||
            !detail.Validation.Succeeded)
        {
            findings.Add(new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Error,
                "process.launch.workflow_not_runnable",
                $"Workflow '{workflowBinding.WorkflowId.Value:D}' resolved to version '{definition.VersionId.Value:D}', but the selected definition is not an Active validated production workflow.",
                stepKey,
                roleKey));
            return;
        }

        bindings.Add(new ProcessLaunchExecutorBinding(
            stepKey,
            roleKey,
            ProcessLaunchExecutorKinds.Workflow,
            workflowBinding.WorkflowId.Value.ToString("D"),
            definition.Name,
            ComputeWorkflowReadinessHash(definition, detail.Validation),
            ResolveWorkflowAssignmentReason(profileAssignment, executorOverride, definition))
        {
            WorkflowBinding = workflowBinding
        });
    }

    private static ProcessWorkflowExecutorBinding? ResolveWorkflowBinding(
        ProcessTemplateDefinitionRoleUsageDocument? role,
        ProcessTemplateLiveRunAssignmentDocument? profileAssignment,
        ProcessLaunchExecutorOverride? executorOverride)
        => executorOverride?.WorkflowBinding ??
           profileAssignment?.WorkflowBinding ??
           role?.WorkflowBinding;

    private static string ResolveWorkflowAssignmentReason(
        ProcessTemplateLiveRunAssignmentDocument? profileAssignment,
        ProcessLaunchExecutorOverride? executorOverride,
        WorkflowDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(executorOverride?.AssignmentReason))
        {
            return executorOverride.AssignmentReason.Trim();
        }

        if (!string.IsNullOrWhiteSpace(profileAssignment?.BindingReason))
        {
            return profileAssignment.BindingReason.Trim();
        }

        return $"Resolved explicit Active workflow '{definition.Name}' version '{definition.VersionId.Value:D}'.";
    }

    private static string ComputeWorkflowReadinessHash(
        WorkflowDefinition definition,
        WorkflowValidationResult validation)
    {
        var source = string.Join(
            ':',
            definition.Id.Value.ToString("N"),
            definition.VersionId.Value.ToString("N"),
            definition.Status,
            validation.Succeeded,
            string.Join(',', validation.Issues.Select(issue => issue.Code).OrderBy(code => code)));
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }

    private static bool AddCapabilityScopeReadinessFindings(
        List<ProcessLaunchReadinessFinding> findings,
        string stepKey,
        string roleKey,
        ProcessCapabilityScope? capabilityScope)
    {
        var normalized = ProcessCapabilityScope.Normalize(capabilityScope);
        var hasErrors = false;
        foreach (var directive in normalized.Directives)
        {
            if (!TryValidateCapabilityScopeTarget(directive.Target, out var targetSummary, out var error))
            {
                findings.Add(new ProcessLaunchReadinessFinding(
                    ProcessLaunchReadinessSeverity.Error,
                    "process.launch.capability_scope_invalid",
                    $"Step '{stepKey}' role '{roleKey}' has invalid capability-scope directive: {error}",
                    stepKey,
                    roleKey));
                hasErrors = true;
                continue;
            }

            var code = directive.Kind switch
            {
                ProcessCapabilityScopeDirectiveKind.Allow => "process.launch.capability_allowed",
                ProcessCapabilityScopeDirectiveKind.AllowOnly => "process.launch.capability_allow_only_scope",
                ProcessCapabilityScopeDirectiveKind.Deny => "process.launch.capability_suppressed",
                ProcessCapabilityScopeDirectiveKind.Require => "process.launch.capability_required",
                _ => "process.launch.capability_scope_directive"
            };
            var message = directive.Kind switch
            {
                ProcessCapabilityScopeDirectiveKind.AllowOnly =>
                    $"Step '{stepKey}' limits agent context to capability scope '{targetSummary}'.",
                ProcessCapabilityScopeDirectiveKind.Deny =>
                    $"Step '{stepKey}' suppresses capability scope '{targetSummary}'.",
                ProcessCapabilityScopeDirectiveKind.Require =>
                    $"Step '{stepKey}' requires capability scope '{targetSummary}'.",
                _ =>
                    $"Step '{stepKey}' allows capability scope '{targetSummary}'."
            };
            findings.Add(new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Info,
                code,
                AppendReason(message, directive.Reason),
                stepKey,
                roleKey));
        }

        foreach (var receipt in normalized.RequiredReceipts)
        {
            findings.Add(new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Info,
                "process.launch.required_tool_receipt",
                AppendReason(
                    $"Step '{stepKey}' requires tool receipt '{receipt.Key}' for {FormatRequiredReceiptSelector(receipt)}.",
                    receipt.Reason),
                stepKey,
                roleKey));
        }

        foreach (var fragment in normalized.InstructionFragments)
        {
            findings.Add(new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Info,
                "process.launch.scoped_instruction_fragment",
                $"Step '{stepKey}' adds scoped instruction fragment '{fragment.Key}'.",
                stepKey,
                roleKey));
        }

        return !hasErrors;
    }

    private static bool TryValidateCapabilityScopeTarget(
        ProcessCapabilityScopeTarget target,
        out string summary,
        out string error)
    {
        summary = string.Empty;
        error = string.Empty;
        if (target.Kind == ProcessCapabilityScopeTargetKind.Unspecified)
        {
            error = "target kind is required.";
            return false;
        }

        if (target.Kind != ProcessCapabilityScopeTargetKind.All &&
            string.IsNullOrWhiteSpace(target.Value))
        {
            error = $"target value is required for '{target.Kind}'.";
            return false;
        }

        if (target.Kind == ProcessCapabilityScopeTargetKind.CapabilityKind &&
            !Enum.TryParse<AccessCapabilityKind>(target.Value.Trim(), ignoreCase: true, out _))
        {
            error = $"target value '{target.Value}' is not a valid capability kind.";
            return false;
        }

        if (target.Kind == ProcessCapabilityScopeTargetKind.CapabilityIdentity &&
            !Enum.TryParse<AccessCapabilityKind>(target.Value.Trim(), ignoreCase: true, out _))
        {
            error = $"capability identity kind '{target.Value}' is not valid.";
            return false;
        }

        if (target.Kind == ProcessCapabilityScopeTargetKind.CapabilityIdentity &&
            string.IsNullOrWhiteSpace(target.SecondaryValue))
        {
            error = "capability identity target requires capability key in secondary value.";
            return false;
        }

        if (target.Kind == ProcessCapabilityScopeTargetKind.RuntimeToolName &&
            !RuntimeToolName.TryCreate(target.Value.Trim().Replace('-', '_'), out _))
        {
            error = $"target value '{target.Value}' is not a valid runtime tool name.";
            return false;
        }

        if (target.Kind == ProcessCapabilityScopeTargetKind.McpToolName &&
            string.IsNullOrWhiteSpace(target.SecondaryValue))
        {
            error = "MCP tool target requires server key and tool name.";
            return false;
        }

        if (target.Kind == ProcessCapabilityScopeTargetKind.OperationClassification &&
            !Enum.TryParse<CapabilityOperationClassification>(target.Value.Trim(), ignoreCase: true, out _))
        {
            error = $"target value '{target.Value}' is not a valid operation classification.";
            return false;
        }

        summary = target.Kind == ProcessCapabilityScopeTargetKind.All
            ? "All"
            : string.IsNullOrWhiteSpace(target.SecondaryValue)
                ? $"{target.Kind}:{target.Value.Trim()}"
                : $"{target.Kind}:{target.Value.Trim()}/{target.SecondaryValue.Trim()}";
        return true;
    }

    private static string FormatRequiredReceiptSelector(ProcessRequiredToolReceipt receipt)
    {
        return receipt.Kind switch
        {
            ProcessRequiredToolReceiptKind.RuntimeToolName => $"runtime tool '{receipt.ToolName}'",
            ProcessRequiredToolReceiptKind.RuntimeToolProviderKey => $"runtime tool provider '{receipt.RuntimeToolProviderKey}'",
            ProcessRequiredToolReceiptKind.RuntimeToolNameWithProvider => $"runtime tool '{receipt.ToolName}' from provider '{receipt.RuntimeToolProviderKey}'",
            ProcessRequiredToolReceiptKind.McpToolName => string.IsNullOrWhiteSpace(receipt.McpServerKey)
                ? $"MCP tool '{receipt.ToolName}'"
                : $"MCP tool '{receipt.ToolName}' on server '{receipt.McpServerKey}'",
            _ => "runtime tool receipt"
        };
    }

    private static string AppendReason(string message, string reason)
        => string.IsNullOrWhiteSpace(reason)
            ? message
            : $"{message} Reason: {reason.Trim()}";

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

    private static bool AddBestRoleFitReadinessFailure(
        List<ProcessLaunchReadinessFinding> findings,
        AgentProcessRoleReadinessRequest readinessRequest,
        IReadOnlyList<AgentDefinition> agents,
        IReadOnlyDictionary<Guid, ProviderProfile> providerById,
        IProviderProfileService providerProfileService,
        string stepKey,
        string roleKey)
    {
        var bestFailure = agents
            .Where(agent => !agent.IsTemplate && agent.Status == AgentLifecycleStatus.Active)
            .Select(agent => ResolveReadinessFailure(agent, readinessRequest, providerById, providerProfileService))
            .OfType<AgentReadinessFailure>()
            .OrderByDescending(failure => failure.Readiness.Score)
            .ThenBy(failure => failure.Agent.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (bestFailure is null)
        {
            return false;
        }

        AddReadinessFindings(findings, bestFailure.Readiness, stepKey, roleKey);
        return true;
    }

    private static AgentReadinessFailure? ResolveReadinessFailure(
        AgentDefinition agent,
        AgentProcessRoleReadinessRequest readinessRequest,
        IReadOnlyDictionary<Guid, ProviderProfile> providerById,
        IProviderProfileService providerProfileService)
    {
        if (agent.ProviderProfileId is not { } providerId ||
            !providerById.TryGetValue(providerId, out var provider) ||
            !provider.IsEnabled ||
            !ProcessProviderReadinessRules.CanExecuteGovernedProcessStep(provider, providerProfileService))
        {
            return null;
        }

        var readiness = AgentProcessReadinessEvaluator.Evaluate(agent, readinessRequest);
        return readiness.HasRoleFit &&
               !readiness.IsExecutionReady &&
               readiness.Findings.Any(finding => finding.Severity == AgentProcessReadinessFindingSeverity.Error)
            ? new AgentReadinessFailure(agent, readiness)
            : null;
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
        ProcessTemplateDefinitionRoleUsageDocument? role,
        IReadOnlyDictionary<string, string> variables)
    {
        return new AgentProcessRoleReadinessRequest(
            stepKey,
            templateStep.Title,
            roleKey,
            role?.RoleResourceKey ?? string.Empty,
            role?.DisplayName ?? roleKey,
            NormalizeOperations(templateStep.AllowedOperations),
            NormalizeOptional(templateStep.OperationTargetScope),
            ResolveLaunchReadinessRequiredRuntimeToolNames(
                variables,
                stepKey,
                templateStep.CapabilityScope,
                templateStep.ExecutionContract?.RequiredRuntimeToolNames),
            AgentFrameworkProcessCapabilityScopeTranslator.Translate(templateStep.CapabilityScope).RequiredCapabilities,
            ProcessExecutorSpecializationPolicy.Resolve(variables)
                .Concat(templateStep.ExecutorPreferredSpecializationTags)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static IReadOnlyList<string> ResolveLaunchReadinessRequiredRuntimeToolNames(
        IReadOnlyDictionary<string, string> variables,
        string stepKey,
        ProcessCapabilityScope capabilityScope,
        IReadOnlyList<string>? templateRequiredRuntimeToolNames)
    {
        var launchContextToolNames = ResolveLaunchRequiredRuntimeToolNames(variables, stepKey)
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return launchContextToolNames
            .Concat(ProcessRequiredRuntimeToolNames.NormalizeRuntimeToolNameCandidates(templateRequiredRuntimeToolNames))
            .Concat(ProcessRequiredRuntimeToolNames.FromUnconditionalCapabilityScope(capabilityScope, launchContextToolNames))
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(toolName => toolName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveLaunchRequiredRuntimeToolNames(
        IReadOnlyDictionary<string, string> variables,
        string stepKey)
    {
        if (variables.TryGetValue(ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts, out var direct) &&
            !string.IsNullOrWhiteSpace(direct))
        {
            return ProcessRequiredRuntimeToolNames.FromUnconditionalProductCompletionRequiredToolReceipts(direct);
        }

        if (!variables.TryGetValue(ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep, out var byStep) ||
            string.IsNullOrWhiteSpace(byStep) ||
            string.IsNullOrWhiteSpace(stepKey))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(byStep);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, stepKey, StringComparison.OrdinalIgnoreCase))
                {
                    return ProcessRequiredRuntimeToolNames.FromUnconditionalProductCompletionRequiredToolReceipts(property.Value);
                }
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return [];
    }

    private static bool ValidateStepOperationContract(
        ProcessTemplateDefinitionStepDocument templateStep,
        string roleKey,
        List<ProcessLaunchReadinessFinding> findings,
        string stepKey)
    {
        var operations = NormalizeOperations(templateStep.AllowedOperations);
        var targetScope = NormalizeOptional(templateStep.OperationTargetScope);
        var valid = true;

        if (IsSubprocessStep(templateStep))
        {
            if (!operations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase))
            {
                AddStructuralContractFinding(
                    findings,
                    stepKey,
                    roleKey,
                    $"Subprocess step '{stepKey}' must allow {ProcessOperationContractNames.ExecuteExternalAction} so the child process can be launched.");
                valid = false;
            }

            if (!string.Equals(targetScope, ProcessOperationContractNames.ExternalActionControlled, StringComparison.OrdinalIgnoreCase))
            {
                AddStructuralContractFinding(
                    findings,
                    stepKey,
                    roleKey,
                    $"Subprocess step '{stepKey}' must use {ProcessOperationContractNames.ExternalActionControlled} target scope.");
                valid = false;
            }
        }

        return valid;
    }

    private static void AddStructuralContractFinding(
        List<ProcessLaunchReadinessFinding> findings,
        string stepKey,
        string roleKey,
        string message)
    {
        findings.Add(new ProcessLaunchReadinessFinding(
            ProcessLaunchReadinessSeverity.Error,
            "process.launch.step_operation_contract_invalid",
            message,
            stepKey,
            roleKey));
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

    private static bool IsSubprocessStep(ProcessTemplateDefinitionStepDocument step)
    {
        return string.Equals(step.StepKind, ProcessTemplateStepKinds.Subprocess, StringComparison.OrdinalIgnoreCase) ||
               !string.IsNullOrWhiteSpace(step.SubprocessProcessKey);
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

    private sealed record AgentReadinessFailure(
        AgentDefinition Agent,
        AgentProcessRoleReadinessResult Readiness);

    private sealed record AgentProviderCandidate(
        AgentDefinition Agent,
        ProviderProfile Provider,
        int MatchScore,
        string MatchSummary,
        string ReadinessHash,
        string ReadinessSummary);
}

