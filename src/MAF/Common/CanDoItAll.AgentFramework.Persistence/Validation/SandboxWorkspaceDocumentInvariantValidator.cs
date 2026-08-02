using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal static class SandboxWorkspaceDocumentInvariantValidator
{
    public static void Validate(SandboxWorkspaceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        EnsureUniqueIds(document.Agents.Select(item => item.Id), "Agent");
        EnsureUniqueIds(document.Providers.Select(item => item.Id), "Provider profile");
        EnsureUniqueIds(document.Capabilities.Select(item => item.Id), "Capability");
        EnsureUniqueCapabilityIdentities(document.Capabilities);

        var runIds = document.ExecutionRuns
            .Select(item => item.Id)
            .ToHashSet();
        var runsById = document.ExecutionRuns
            .ToDictionary(item => item.Id);
        var agentIds = document.Agents
            .Select(item => item.Id)
            .ToHashSet();
        var providerIds = document.Providers
            .Select(item => item.Id)
            .ToHashSet();
        var capabilityIds = document.Capabilities
            .Select(item => item.Id)
            .ToHashSet();
        var capabilitiesById = document.Capabilities
            .ToDictionary(item => item.Id);
        var sessionIds = document.ChatSessions
            .Select(item => item.Id)
            .ToHashSet();
        var sessionAgentIds = document.ChatSessions
            .ToDictionary(item => item.Id, item => item.AgentId);

        foreach (var agent in document.Agents)
        {
            if (agent.ProviderProfileId.HasValue && !providerIds.Contains(agent.ProviderProfileId.Value))
            {
                throw new InvalidOperationException(
                    $"Agent '{agent.Id:N}' references missing provider profile '{agent.ProviderProfileId.Value:N}'.");
            }

            var assignedCapabilityIds = new HashSet<Guid>();
            foreach (var capability in agent.Capabilities)
            {
                if (!assignedCapabilityIds.Add(capability.CapabilityId))
                {
                    throw new InvalidOperationException(
                        $"Agent '{agent.Id:N}' contains duplicate capability assignment '{capability.CapabilityId:N}'.");
                }

                if (!capabilityIds.Contains(capability.CapabilityId) ||
                    !capabilitiesById.TryGetValue(capability.CapabilityId, out var catalogCapability))
                {
                    throw new InvalidOperationException(
                        $"Agent '{agent.Id:N}' references missing capability '{capability.CapabilityId:N}'.");
                }

                if (capability.Kind != catalogCapability.Kind ||
                    !string.Equals(
                        capability.CapabilityKey,
                        catalogCapability.Key,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Agent '{agent.Id:N}' capability assignment '{capability.CapabilityId:N}' does not match " +
                        $"catalog identity '{catalogCapability.Kind}:{catalogCapability.Key}'.");
                }
            }
        }

        foreach (var session in document.ChatSessions)
        {
            if (agentIds.Contains(session.AgentId))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Chat session '{session.Id:N}' references missing agent '{session.AgentId:N}'.");
        }

        foreach (var session in document.ChatSessions.Where(item => item.LatestExecutionRunId.HasValue))
        {
            var latestRunId = session.LatestExecutionRunId!.Value;
            if (!runsById.TryGetValue(latestRunId, out var latestRun))
            {
                throw new InvalidOperationException(
                    $"Chat session '{session.Id:N}' references missing latest execution run '{latestRunId:N}'.");
            }

            if (latestRun.ChatSessionId != session.Id)
            {
                throw new InvalidOperationException(
                    $"Chat session '{session.Id:N}' points to execution run '{latestRunId:N}' that is not linked back to the session.");
            }

            if (latestRun.AgentId != session.AgentId)
            {
                throw new InvalidOperationException(
                    $"Chat session '{session.Id:N}' points to execution run '{latestRunId:N}' with a different agent id.");
            }
        }

        foreach (var run in document.ExecutionRuns.Where(item => item.ChatSessionId.HasValue))
        {
            if (!agentIds.Contains(run.AgentId))
            {
                throw new InvalidOperationException(
                    $"Execution run '{run.Id:N}' references missing agent '{run.AgentId:N}'.");
            }

            var sessionId = run.ChatSessionId!.Value;
            if (!sessionIds.Contains(sessionId))
            {
                throw new InvalidOperationException(
                    $"Execution run '{run.Id:N}' references missing chat session '{sessionId:N}'.");
            }

            if (sessionAgentIds.TryGetValue(sessionId, out var sessionAgentId)
                && sessionAgentId != run.AgentId)
            {
                throw new InvalidOperationException(
                    $"Execution run '{run.Id:N}' is linked to chat session '{sessionId:N}' with a different agent id.");
            }
        }

        foreach (var run in document.ExecutionRuns.Where(item => !item.ChatSessionId.HasValue))
        {
            if (agentIds.Contains(run.AgentId))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Execution run '{run.Id:N}' references missing agent '{run.AgentId:N}'.");
        }

        foreach (var logEntry in document.ExecutionLog.Where(item => item.ExecutionRunId != Guid.Empty))
        {
            if (!agentIds.Contains(logEntry.AgentId))
            {
                throw new InvalidOperationException(
                    $"Execution log entry '{logEntry.Id:N}' references missing agent '{logEntry.AgentId:N}'.");
            }

            if (!runIds.Contains(logEntry.ExecutionRunId))
            {
                throw new InvalidOperationException(
                    $"Execution log entry '{logEntry.Id:N}' references missing execution run '{logEntry.ExecutionRunId:N}'.");
            }
        }

        foreach (var logEntry in document.ExecutionLog.Where(item => item.ExecutionRunId == Guid.Empty))
        {
            if (agentIds.Contains(logEntry.AgentId))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Execution log entry '{logEntry.Id:N}' references missing agent '{logEntry.AgentId:N}'.");
        }

        foreach (var logEntry in document.ExecutionLog.Where(item => item.ChatSessionId.HasValue))
        {
            if (!sessionIds.Contains(logEntry.ChatSessionId!.Value))
            {
                throw new InvalidOperationException(
                    $"Execution log entry '{logEntry.Id:N}' references missing chat session '{logEntry.ChatSessionId.Value:N}'.");
            }
        }

        foreach (var metric in document.Metrics.Where(item => item.ExecutionRunId != Guid.Empty))
        {
            if (!agentIds.Contains(metric.AgentId))
            {
                throw new InvalidOperationException(
                    $"Execution metric '{metric.Id:N}' references missing agent '{metric.AgentId:N}'.");
            }

            if (!runIds.Contains(metric.ExecutionRunId))
            {
                throw new InvalidOperationException(
                    $"Execution metric '{metric.Id:N}' references missing execution run '{metric.ExecutionRunId:N}'.");
            }
        }

        foreach (var metric in document.Metrics.Where(item => item.ExecutionRunId == Guid.Empty))
        {
            if (agentIds.Contains(metric.AgentId))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Execution metric '{metric.Id:N}' references missing agent '{metric.AgentId:N}'.");
        }

        foreach (var metric in document.Metrics.Where(item => item.ChatSessionId.HasValue))
        {
            if (!sessionIds.Contains(metric.ChatSessionId!.Value))
            {
                throw new InvalidOperationException(
                    $"Execution metric '{metric.Id:N}' references missing chat session '{metric.ChatSessionId.Value:N}'.");
            }
        }

        foreach (var observation in document.ProviderUsageObservations)
        {
            if (observation.ExecutionRunId is not { } executionRunId)
            {
                continue;
            }

            if (observation.AgentId.HasValue && !agentIds.Contains(observation.AgentId.Value))
            {
                throw new InvalidOperationException(
                    $"Provider usage observation '{observation.Id:N}' references missing agent '{observation.AgentId.Value:N}'.");
            }

            if (!runIds.Contains(executionRunId))
            {
                throw new InvalidOperationException(
                    $"Provider usage observation '{observation.Id:N}' references missing execution run '{executionRunId:N}'.");
            }
        }

        foreach (var observation in document.ProviderUsageObservations.Where(item => item.ChatSessionId.HasValue))
        {
            if (!sessionIds.Contains(observation.ChatSessionId!.Value))
            {
                throw new InvalidOperationException(
                    $"Provider usage observation '{observation.Id:N}' references missing chat session '{observation.ChatSessionId.Value:N}'.");
            }
        }

        foreach (var memoryRecord in document.Memory)
        {
            if (agentIds.Contains(memoryRecord.AgentId))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Memory record '{memoryRecord.Id:N}' references missing agent '{memoryRecord.AgentId:N}'.");
        }

        var teamIds = new HashSet<Guid>();
        var teamNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var team in document.AgentTeams ?? [])
        {
            if (team.Id == Guid.Empty)
            {
                throw new InvalidOperationException("Agent team id is required.");
            }

            if (!teamIds.Add(team.Id))
            {
                throw new InvalidOperationException($"Agent team '{team.Id:N}' is duplicated.");
            }

            if (string.IsNullOrWhiteSpace(team.Name))
            {
                throw new InvalidOperationException($"Agent team '{team.Id:N}' must have a name.");
            }

            if (!teamNames.Add(team.Name.Trim()))
            {
                throw new InvalidOperationException($"Agent team name '{team.Name}' is duplicated.");
            }

            var memberIds = new HashSet<Guid>();
            foreach (var agentId in team.AgentIds ?? [])
            {
                if (!memberIds.Add(agentId))
                {
                    throw new InvalidOperationException(
                        $"Agent team '{team.Id:N}' contains duplicate agent '{agentId:N}'.");
                }

                if (agentIds.Contains(agentId))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Agent team '{team.Id:N}' references missing agent '{agentId:N}'.");
            }
        }

        EnsureAllRunsExist(document.ExecutionApprovals.Select(item => (item.ExecutionRunId, $"Execution approval '{item.ApprovalId}'")), runIds);
        EnsureAllRunsExist(document.ExecutionArtifacts.Select(item => (item.ExecutionRunId, $"Execution artifact '{item.Id:N}'")), runIds);
        EnsureAllRunsExist(document.ExecutionWorkflowCheckpoints.Select(item => (item.ExecutionRunId, $"Execution checkpoint '{item.Id:N}'")), runIds);
        EnsureAllRunsExist(document.ToolExecutionReceipts.Select(item => (item.ExecutionRunId, $"Tool execution receipt '{item.Id:N}'")), runIds);
        EnsureAllRunsExist(
            document.ProviderUsageObservations
                .Where(item => item.ExecutionRunId.HasValue)
                .Select(item => (item.ExecutionRunId!.Value, $"Provider usage observation '{item.Id:N}'")),
            runIds);
    }

    private static void EnsureAllRunsExist(
        IEnumerable<(Guid ExecutionRunId, string Label)> items,
        IReadOnlySet<Guid> runIds)
    {
        foreach (var item in items)
        {
            if (runIds.Contains(item.ExecutionRunId))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"{item.Label} references missing execution run '{item.ExecutionRunId:N}'.");
        }
    }

    private static void EnsureUniqueIds(IEnumerable<Guid> ids, string label)
    {
        var duplicate = ids
            .GroupBy(id => id)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"{label} id '{duplicate.Key:N}' is duplicated.");
        }
    }

    private static void EnsureUniqueCapabilityIdentities(
        IReadOnlyList<CapabilityCatalogItem> capabilities)
    {
        var duplicate = capabilities
            .GroupBy(capability => new CapabilityIdentity(
                capability.Kind,
                capability.Key.Trim().ToUpperInvariant()))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Capability identity '{duplicate.Key.Kind}:{duplicate.First().Key}' is duplicated.");
        }
    }

    private readonly record struct CapabilityIdentity(
        CapabilityKind Kind,
        string NormalizedKey);
}
