using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal static class SandboxWorkspaceDocumentInvariantValidator
{
    public static void Validate(SandboxWorkspaceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

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

            foreach (var capability in agent.Capabilities)
            {
                if (capabilityIds.Contains(capability.CapabilityId))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Agent '{agent.Id:N}' references missing capability '{capability.CapabilityId:N}'.");
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

        foreach (var memoryRecord in document.Memory)
        {
            if (agentIds.Contains(memoryRecord.AgentId))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Memory record '{memoryRecord.Id:N}' references missing agent '{memoryRecord.AgentId:N}'.");
        }

        EnsureAllRunsExist(document.ExecutionApprovals.Select(item => (item.ExecutionRunId, $"Execution approval '{item.ApprovalId}'")), runIds);
        EnsureAllRunsExist(document.ExecutionArtifacts.Select(item => (item.ExecutionRunId, $"Execution artifact '{item.Id:N}'")), runIds);
        EnsureAllRunsExist(document.ExecutionWorkflowCheckpoints.Select(item => (item.ExecutionRunId, $"Execution checkpoint '{item.Id:N}'")), runIds);
        EnsureAllRunsExist(document.ToolExecutionReceipts.Select(item => (item.ExecutionRunId, $"Tool execution receipt '{item.Id:N}'")), runIds);
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
}
