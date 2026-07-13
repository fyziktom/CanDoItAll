using System.Globalization;
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

namespace CanDoItAll.Modules.Processes;


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
                : assignment.OperationTargetScope.Trim(),
            ResolveRepairReadinessRequiredRuntimeToolNames(assignment));
    }

    private static IReadOnlyList<string> ResolveRepairReadinessRequiredRuntimeToolNames(ProcessRuntimeStepAssignment assignment)
    {
        var launchContextToolNames = ResolveRequiredRuntimeToolNames(assignment.LaunchVariables, assignment.StepKey)
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return launchContextToolNames
            .Concat(ProcessRequiredRuntimeToolNames.FromCapabilityScope(assignment.CapabilityScope, launchContextToolNames))
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(toolName => toolName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveRequiredRuntimeToolNames(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey)
    {
        var direct = ResolveAssignmentLaunchVariable(launchVariables, ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return ProcessRequiredRuntimeToolNames.FromProductCompletionRequiredToolReceipts(direct);
        }

        var byStep = ResolveAssignmentLaunchVariable(launchVariables, ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep);
        if (string.IsNullOrWhiteSpace(byStep) ||
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
                    return ProcessRequiredRuntimeToolNames.FromProductCompletionRequiredToolReceipts(property.Value);
                }
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return [];
    }

    private static string ResolveAssignmentLaunchVariable(
        IReadOnlyDictionary<string, string> launchVariables,
        string key)
    {
        if (launchVariables.TryGetValue(key, out var exact) &&
            !string.IsNullOrWhiteSpace(exact))
        {
            return exact.Trim();
        }

        return launchVariables
            .Where(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value?.Trim() ?? string.Empty)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
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

