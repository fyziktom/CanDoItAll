using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService
{
    private IReadOnlyList<ProviderUsageObservation> BuildUsageObservations(
        ExecutionRunRecord run,
        AgentDefinition agent,
        ProviderProfile provider,
        AgentRunMetric metric,
        AgentRuntimeResponse runtimeResponse)
    {
        var sourceObservations = runtimeResponse.UsageObservations.ToList();
        if (!HasRuntimeUsageObservation(sourceObservations))
        {
            sourceObservations.Insert(
                0,
                CreateObservationFromMetric(metric, provider, ProviderUsageObservationStatus.ObservedFromMetric, ProviderUsageSourcePhases.AgentRuntime));
        }

        return sourceObservations
            .Select(observation => EnrichUsageObservation(run, agent, provider, observation))
            .Select(observation => AttachRuntimeContextDiagnostics(observation, runtimeResponse))
            .Select(observation => PriceUsageObservation(observation, provider))
            .ToList();
    }

    private IReadOnlyList<ProviderUsageObservation> BuildRuntimeResponseUsageObservations(
        ExecutionRunRecord run,
        AgentDefinition agent,
        ProviderProfile provider,
        AgentRunMetric metric,
        AgentRuntimeResponse runtimeResponse)
    {
        var sourceObservations = runtimeResponse.UsageObservations.ToList();
        if (!HasRuntimeUsageObservation(sourceObservations))
        {
            sourceObservations.Insert(0, CreateObservationFromRuntimeResponse(metric, provider, runtimeResponse));
        }

        return sourceObservations
            .Select(observation => EnrichUsageObservation(run, agent, provider, observation))
            .Select(observation => AttachRuntimeContextDiagnostics(observation, runtimeResponse))
            .Select(observation => PriceUsageObservation(observation, provider))
            .ToList();
    }

    private IReadOnlyList<ProviderUsageObservation> BuildFailureUsageObservations(
        ExecutionRunRecord run,
        AgentDefinition agent,
        ProviderProfile provider,
        AgentRunMetric failureMetric,
        Exception exception)
    {
        if (exception is AgentRuntimeUsageException usageException &&
            usageException.UsageObservations.Count > 0)
        {
            var observations = usageException.UsageObservations
                .Where(observation => !IsSupersededSyntheticFailureObservation(
                    observation,
                    usageException.ProviderFailureIdentity))
                .Select(observation => EnrichUsageObservation(run, agent, provider, observation))
                .Select(observation => PriceUsageObservation(observation, provider))
                .ToList();
            if (usageException.ProviderFailureIdentity is not null)
            {
                observations.Add(CreateProviderFailureObservation(
                    run,
                    agent,
                    provider,
                    usageException.ProviderFailureIdentity));
            }

            return observations;
        }

        return
        [
            PriceUsageObservation(
                EnrichUsageObservation(
                    run,
                    agent,
                    provider,
                    CreateObservationFromMetric(
                        failureMetric,
                        provider,
                        ProviderUsageObservationStatus.EstimatedFromMetric,
                        ProviderUsageSourcePhases.LegacyAgentRunMetric)),
                provider)
        ];
    }

    private static bool IsSupersededSyntheticFailureObservation(
        ProviderUsageObservation observation,
        AgentRuntimeProviderFailureIdentity? identity)
    {
        return identity is not null &&
               observation.ProviderProfileId != identity.ProviderProfileId &&
               observation.UsageStatus == ProviderUsageObservationStatus.MissingAfterProviderActivity &&
               observation.InputTokens == 0 &&
               observation.CachedInputTokens == 0 &&
               observation.OutputTokens == 0 &&
               observation.ReasoningTokens == 0 &&
               observation.TotalTokens == 0;
    }

    private IReadOnlyList<ProviderUsageObservation> BuildRepairUsageObservations(
        ExecutionRunRecord run,
        AgentDefinition agent,
        ProviderProfile provider,
        AgentOutputRepairAttemptResult repair)
    {
        return repair.UsageObservations
            .Select(observation => EnrichUsageObservation(run, agent, provider, observation with
            {
                SourcePhase = string.IsNullOrWhiteSpace(observation.SourcePhase)
                    ? ProviderUsageSourcePhases.StructuredOutputRepair
                    : observation.SourcePhase
            }))
            .Select(observation => PriceUsageObservation(observation, provider))
            .ToList();
    }

    private static AgentRuntimeResponse AppendRepairUsageObservations(
        AgentRuntimeResponse response,
        AgentOutputRepairAttemptResult repair)
    {
        if (repair.UsageObservations.Count == 0)
        {
            return response;
        }

        return response with
        {
            UsageObservations = response.UsageObservations
                .Concat(repair.UsageObservations.Select(observation => observation with
                {
                    SourcePhase = string.IsNullOrWhiteSpace(observation.SourcePhase)
                        ? ProviderUsageSourcePhases.StructuredOutputRepair
                        : observation.SourcePhase
                }))
                .ToList()
        };
    }

    private static bool HasRuntimeUsageObservation(IReadOnlyList<ProviderUsageObservation> observations)
    {
        return observations.Any(observation =>
            !string.Equals(observation.SourcePhase, ProviderUsageSourcePhases.StructuredOutputRepair, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(observation.SourcePhase, ProviderUsageSourcePhases.InputAttachmentAnalysis, StringComparison.OrdinalIgnoreCase));
    }

    private static ProviderUsageObservation CreateObservationFromMetric(
        AgentRunMetric metric,
        ProviderProfile provider,
        ProviderUsageObservationStatus status,
        string sourcePhase)
    {
        var totalTokens = Math.Max(0, metric.InputTokens) + Math.Max(0, metric.OutputTokens);
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: metric.CreatedAtUtc,
            ProviderName: metric.ProviderName,
            ProviderKind: provider.Kind,
            Model: metric.Model,
            TransportKind: provider.Transport,
            SourcePhase: sourcePhase,
            UsageStatus: status,
            InputTokens: Math.Max(0, metric.InputTokens),
            CachedInputTokens: Math.Clamp(metric.CachedInputTokens, 0, Math.Max(0, metric.InputTokens)),
            OutputTokens: Math.Max(0, metric.OutputTokens),
            ReasoningTokens: 0,
            TotalTokens: totalTokens,
            ToolCallCount: Math.Max(0, metric.ToolCalls))
        {
            ProviderProfileId = provider.Id,
            ExecutionRunId = metric.ExecutionRunId == Guid.Empty ? null : metric.ExecutionRunId,
            AgentId = metric.AgentId,
            ChatSessionId = metric.ChatSessionId,
            CalculatedCostUsd = metric.CostUsd > 0m ? metric.CostUsd : null,
            DiagnosticsJson = status == ProviderUsageObservationStatus.EstimatedFromMetric
                ? """{"source":"legacy metric prompt estimate"}"""
                : """{"source":"legacy metric bridge"}"""
        };
    }

    private static ProviderUsageObservation CreateObservationFromRuntimeResponse(
        AgentRunMetric metric,
        ProviderProfile provider,
        AgentRuntimeResponse runtimeResponse)
    {
        var inputTokens = Math.Max(0, runtimeResponse.InputTokens);
        var cachedInputTokens = Math.Clamp(runtimeResponse.CachedInputTokens, 0, inputTokens);
        var outputTokens = Math.Max(0, runtimeResponse.OutputTokens);
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: metric.CreatedAtUtc,
            ProviderName: metric.ProviderName,
            ProviderKind: provider.Kind,
            Model: metric.Model,
            TransportKind: provider.Transport,
            SourcePhase: ProviderUsageSourcePhases.AgentRuntime,
            UsageStatus: ProviderUsageObservationStatus.ObservedFromMetric,
            InputTokens: inputTokens,
            CachedInputTokens: cachedInputTokens,
            OutputTokens: outputTokens,
            ReasoningTokens: 0,
            TotalTokens: inputTokens + outputTokens,
            ToolCallCount: Math.Max(0, runtimeResponse.ToolCalls))
        {
            ProviderProfileId = provider.Id,
            ExecutionRunId = metric.ExecutionRunId == Guid.Empty ? null : metric.ExecutionRunId,
            AgentId = metric.AgentId,
            ChatSessionId = metric.ChatSessionId,
            DiagnosticsJson = """{"source":"runtime response aggregate"}"""
        };
    }

    private ProviderUsageObservation EnrichUsageObservation(
        ExecutionRunRecord run,
        AgentDefinition agent,
        ProviderProfile provider,
        ProviderUsageObservation observation)
    {
        var preserveObservationProvider = observation.ProviderProfileId.HasValue &&
                                          observation.ProviderProfileId.Value != provider.Id;
        return observation with
        {
            Id = observation.Id == Guid.Empty ? Guid.NewGuid() : observation.Id,
            ProviderProfileId = observation.ProviderProfileId ?? provider.Id,
            ProviderName = preserveObservationProvider
                ? observation.ProviderName
                : provider.Name,
            ProviderKind = preserveObservationProvider
                ? observation.ProviderKind
                : provider.Kind,
            Model = preserveObservationProvider
                ? observation.Model
                : string.IsNullOrWhiteSpace(observation.Model)
                    ? ResolveEffectiveManagedSeedModel(agent, provider)
                    : observation.Model,
            TransportKind = preserveObservationProvider
                ? observation.TransportKind
                : provider.Transport,
            ExecutionRunId = run.Id,
            AgentId = agent.Id,
            ChatSessionId = run.ChatSessionId,
            RuntimeSessionKey = string.IsNullOrWhiteSpace(observation.RuntimeSessionKey) ? run.RuntimeSessionKey : observation.RuntimeSessionKey,
            ProcessRunId = string.IsNullOrWhiteSpace(observation.ProcessRunId) ? run.ProcessRunId : observation.ProcessRunId,
            ProcessStepId = string.IsNullOrWhiteSpace(observation.ProcessStepId) ? run.ProcessStepId : observation.ProcessStepId,
            CorrelationId = string.IsNullOrWhiteSpace(observation.CorrelationId) ? run.CorrelationId : observation.CorrelationId
        };
    }

    private static ProviderUsageObservation PriceUsageObservation(
        ProviderUsageObservation observation,
        ProviderProfile provider)
    {
        if (observation.ProviderProfileId.HasValue &&
            observation.ProviderProfileId.Value != provider.Id)
        {
            return observation;
        }

        return ProviderPricingCalculator.TryResolveObservationCost(observation, [provider], out var costUsd)
            ? observation with { CalculatedCostUsd = costUsd }
            : observation;
    }

    private static ProviderUsageObservation CreateProviderFailureObservation(
        ExecutionRunRecord run,
        AgentDefinition agent,
        ProviderProfile provider,
        AgentRuntimeProviderFailureIdentity identity)
    {
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ProviderName: provider.Name,
            ProviderKind: provider.Kind,
            Model: identity.Model,
            TransportKind: provider.Transport,
            SourcePhase: ProviderUsageSourcePhases.AgentRuntime,
            UsageStatus: ProviderUsageObservationStatus.MissingAfterProviderActivity,
            InputTokens: 0,
            CachedInputTokens: 0,
            OutputTokens: 0,
            ReasoningTokens: 0,
            TotalTokens: 0,
            ToolCallCount: 0)
        {
            ProviderProfileId = provider.Id,
            ExecutionRunId = run.Id,
            AgentId = agent.Id,
            ChatSessionId = run.ChatSessionId,
            RuntimeSessionKey = run.RuntimeSessionKey,
            ProcessRunId = run.ProcessRunId,
            ProcessStepId = run.ProcessStepId,
            CorrelationId = run.CorrelationId,
            DiagnosticsJson = """{"diagnostic":"provider failure identity captured without attributable token usage"}"""
        };
    }

    private static ProviderUsageObservation AttachRuntimeContextDiagnostics(
        ProviderUsageObservation observation,
        AgentRuntimeResponse runtimeResponse)
    {
        if (runtimeResponse.ContextAssemblyManifest is null &&
            runtimeResponse.ContextContributionTraces.Count == 0)
        {
            return observation;
        }

        var diagnostics = ParseDiagnosticsObject(observation.DiagnosticsJson);
        if (runtimeResponse.ContextAssemblyManifest is not null)
        {
            diagnostics["contextAssemblyManifest"] = JsonSerializer.SerializeToNode(
                runtimeResponse.ContextAssemblyManifest,
                AgentOutputJson.SerializerOptions);
        }

        if (runtimeResponse.ContextContributionTraces.Count > 0)
        {
            diagnostics["contextContributionTraces"] = JsonSerializer.SerializeToNode(
                runtimeResponse.ContextContributionTraces,
                AgentOutputJson.SerializerOptions);
        }

        return observation with
        {
            DiagnosticsJson = diagnostics.ToJsonString(AgentOutputJson.SerializerOptions)
        };
    }

    private static JsonObject ParseDiagnosticsObject(string diagnosticsJson)
    {
        if (string.IsNullOrWhiteSpace(diagnosticsJson))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(diagnosticsJson) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return new JsonObject
            {
                ["legacyDiagnostics"] = diagnosticsJson
            };
        }
    }
}
