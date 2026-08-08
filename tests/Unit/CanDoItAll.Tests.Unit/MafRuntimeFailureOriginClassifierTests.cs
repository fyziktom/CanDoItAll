using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.Tests.Unit;

public sealed class MafRuntimeFailureOriginClassifierTests
{
    [Fact]
    public void Recoverable_historical_tool_failure_does_not_reclassify_later_provider_failure()
    {
        var historicalFailure = CreateTrace(sequence: 1, succeeded: false);
        var failedBeforeAdvance =
            MafRuntimeFailureOriginClassifier.CountCompletedFailedTools(
                [historicalFailure]);

        var origin = MafRuntimeFailureOriginClassifier.ResolveProviderAdvanceFailure(
            failedBeforeAdvance,
            [historicalFailure],
            CreateProviderTransportException());

        Assert.Equal(AgentRuntimeFailureOrigin.Provider, origin);
    }

    [Fact]
    public void Tool_failure_completed_during_provider_advance_is_classified_as_tool_failure()
    {
        var failedBeforeAdvance =
            MafRuntimeFailureOriginClassifier.CountCompletedFailedTools([]);
        var currentFailure = CreateTrace(sequence: 1, succeeded: false);

        var origin = MafRuntimeFailureOriginClassifier.ResolveProviderAdvanceFailure(
            failedBeforeAdvance,
            [currentFailure],
            new InvalidOperationException("Tool invocation failed."));

        Assert.Equal(AgentRuntimeFailureOrigin.Tool, origin);
    }

    [Fact]
    public void New_failed_trace_with_colliding_participant_sequence_is_classified_as_tool_failure()
    {
        var historicalFailure = CreateTrace(sequence: 1, succeeded: false);
        var failedBeforeAdvance =
            MafRuntimeFailureOriginClassifier.CountCompletedFailedTools(
                [historicalFailure]);
        var currentFailure = CreateTrace(sequence: 1, succeeded: false);

        var origin = MafRuntimeFailureOriginClassifier.ResolveProviderAdvanceFailure(
            failedBeforeAdvance,
            [historicalFailure, currentFailure],
            new InvalidOperationException("Tool invocation failed."));

        Assert.Equal(AgentRuntimeFailureOrigin.Tool, origin);
    }

    [Fact]
    public void Unknown_failure_during_agent_stream_defaults_to_runtime()
    {
        var origin = MafRuntimeFailureOriginClassifier.ResolveProviderAdvanceFailure(
            failedToolCountBeforeAdvance: 0,
            tracesAfterAdvance: [],
            new InvalidOperationException("An AI context provider failed."));

        Assert.Equal(AgentRuntimeFailureOrigin.Runtime, origin);
    }

    [Fact]
    public void Typed_transport_failure_wins_over_recoverable_tool_failure_in_same_advance()
    {
        var origin = MafRuntimeFailureOriginClassifier.ResolveProviderAdvanceFailure(
            failedToolCountBeforeAdvance: 0,
            tracesAfterAdvance: [CreateTrace(sequence: 1, succeeded: false)],
            CreateProviderTransportException());

        Assert.Equal(AgentRuntimeFailureOrigin.Provider, origin);
    }

    [Fact]
    public void Explicit_outer_tool_boundary_wins_over_nested_provider_failure()
    {
        var nestedProviderFailure = CreateProviderTransportException();
        var outerToolFailure = new MafToolInvocationBoundaryException(
            "delegate_to_agent",
            nestedProviderFailure);

        var origin = MafRuntimeFailureOriginClassifier.ResolveProviderAdvanceFailure(
            failedToolCountBeforeAdvance: 0,
            tracesAfterAdvance: [CreateTrace(sequence: 1, succeeded: false)],
            outerToolFailure);

        Assert.Equal(AgentRuntimeFailureOrigin.Tool, origin);
        Assert.Equal(
            AgentRuntimeFailureOrigin.Tool,
            MafRuntimeFailureOriginClassifier.ResolveOutsideProviderBoundary(
                outerToolFailure));
    }

    [Fact]
    public void Failure_outside_provider_boundary_preserves_only_explicit_typed_origin()
    {
        var providerFailure = new AgentRuntimeUsageException(
            "Provider failed.",
            new InvalidOperationException("Transport failed."),
            [],
            failureOrigin: AgentRuntimeFailureOrigin.Provider);

        Assert.Equal(
            AgentRuntimeFailureOrigin.Provider,
            MafRuntimeFailureOriginClassifier.ResolveOutsideProviderBoundary(providerFailure));
        Assert.Equal(
            AgentRuntimeFailureOrigin.Runtime,
            MafRuntimeFailureOriginClassifier.ResolveOutsideProviderBoundary(
                new InvalidOperationException("Progress callback failed.")));
        Assert.Equal(
            AgentRuntimeFailureOrigin.Provider,
            MafRuntimeFailureOriginClassifier.ResolveOutsideProviderBoundary(
                CreateProviderTransportException()));
    }

    private static AgentToolInvocationTrace CreateTrace(int sequence, bool succeeded)
        => new(
            "project_structure_read",
            ToolInvocationClassification.Read,
            sequence,
            DateTimeOffset.UtcNow.AddSeconds(-1),
            DateTimeOffset.UtcNow,
            succeeded,
            succeeded ? string.Empty : "Tool invocation failed.");

    private static MafProviderTransportException CreateProviderTransportException()
        => new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "unit-model",
            new HttpRequestException("Provider request failed."));
}
