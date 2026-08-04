using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

public sealed class MafProviderUpdatePumpTests
{
    [Fact]
    public async Task PumpAsync_processes_updates_and_disposes_the_provider_enumerator()
    {
        var updates = new[]
        {
            CreateUpdate("first"),
            CreateUpdate("second")
        };
        var stream = new ControllableProviderUpdateStream(updates);
        var handled = new List<AgentResponseUpdate>();
        var pump = new MafProviderUpdatePump();

        var result = await pump.PumpAsync<string>(
            stream,
            CreateContext(),
            update =>
            {
                handled.Add(update);
                return Task.FromResult<string?>(null);
            },
            () => Task.FromResult<string?>(null),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(updates, handled);
        Assert.Equal(1, stream.EnumeratorAcquisitionCount);
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task PumpAsync_classifies_advance_failure_and_preserves_usage_evidence()
    {
        var context = CreateContext(
            usageSourcePhase: ProviderUsageSourcePhases.FinalizerRecovery,
            runtimeSessionKey: "runtime-session-001");
        var advanceFailure = new MafProviderTransportException(
            context.Provider,
            context.Model,
            new IOException("Provider stream advance failed."));
        var stream = new ControllableProviderUpdateStream([], advanceFailure: advanceFailure);
        var pump = new MafProviderUpdatePump();

        var exception = await Assert.ThrowsAsync<AgentRuntimeUsageException>(() =>
            pump.PumpAsync<string>(
                stream,
                context,
                _ => Task.FromResult<string?>(null),
                () => Task.FromResult<string?>(null),
                CancellationToken.None));

        Assert.Same(advanceFailure, exception.InnerException);
        Assert.Equal(AgentRuntimeFailureOrigin.Provider, exception.FailureOrigin);
        Assert.Equal(context.EntryAgentRequestCompatibilityEvidence, exception.EntryAgentRequestCompatibilityEvidence);
        Assert.Equal(context.SnapshotToolInvocationTraces(), exception.ToolInvocationTraces);
        var identity = Assert.IsType<AgentRuntimeProviderFailureIdentity>(exception.ProviderFailureIdentity);
        Assert.Equal(context.Provider.Id, identity.ProviderProfileId);
        Assert.Equal(context.Provider.Name, identity.ProviderName);
        Assert.Equal(context.Provider.Kind, identity.ProviderKind);
        Assert.Equal(context.Provider.Transport, identity.Transport);
        Assert.Equal(context.Model, identity.Model);
        var usage = Assert.Single(exception.UsageObservations);
        Assert.Equal(context.Provider.Id, usage.ProviderProfileId);
        Assert.Equal(context.Provider.Name, usage.ProviderName);
        Assert.Equal(context.Model, usage.Model);
        Assert.Equal(ProviderUsageSourcePhases.FinalizerRecovery, usage.SourcePhase);
        Assert.Equal(ProviderUsageObservationStatus.MissingAfterProviderActivity, usage.UsageStatus);
        Assert.Equal("runtime-session-001", usage.RuntimeSessionKey);
        Assert.Contains(nameof(IOException), usage.DiagnosticsJson, StringComparison.Ordinal);
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task PumpAsync_propagates_disposal_failure_when_stream_completed_successfully()
    {
        var context = CreateContext();
        var disposalFailure = new MafProviderTransportException(
            context.Provider,
            context.Model,
            new IOException("Provider enumerator disposal failed."));
        var stream = new ControllableProviderUpdateStream([], disposalFailure: disposalFailure);
        var pump = new MafProviderUpdatePump();

        var exception = await Assert.ThrowsAsync<MafProviderTransportException>(() =>
            pump.PumpAsync<string>(
                stream,
                context,
                _ => Task.FromResult<string?>(null),
                () => Task.FromResult<string?>(null),
                CancellationToken.None));

        Assert.Same(disposalFailure, exception);
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task PumpAsync_preserves_advance_failure_when_enumerator_disposal_also_fails()
    {
        var context = CreateContext();
        var advanceFailure = new MafProviderTransportException(
            context.Provider,
            context.Model,
            new IOException("Provider stream advance failed."));
        var disposalFailure = new MafProviderTransportException(
            context.Provider,
            context.Model,
            new UnauthorizedAccessException("Provider enumerator disposal failed."));
        var stream = new ControllableProviderUpdateStream(
            [],
            advanceFailure,
            disposalFailure);
        var pump = new MafProviderUpdatePump();

        var exception = await Assert.ThrowsAsync<AgentRuntimeUsageException>(() =>
            pump.PumpAsync<string>(
                stream,
                context,
                _ => Task.FromResult<string?>(null),
                () => Task.FromResult<string?>(null),
                CancellationToken.None));

        Assert.Same(advanceFailure, exception.InnerException);
        Assert.Equal(AgentRuntimeFailureOrigin.Provider, exception.FailureOrigin);
        Assert.Equal(
            typeof(UnauthorizedAccessException).FullName,
            exception.Data[MafProviderTransportException.DisposalFailureTypeDataKey]);
        Assert.Equal(1, stream.DisposeCount);
    }

    private static MafProviderUpdatePumpContext CreateContext(
        string usageSourcePhase = ProviderUsageSourcePhases.AgentRuntime,
        string runtimeSessionKey = "runtime-session")
    {
        var provider = new ProviderProfile(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Unit Provider",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            "unit-model",
            ProviderTransportKind.Responses,
            true,
            true,
            true,
            false,
            true,
            "{}",
            string.Empty,
            "Not checked",
            null,
            []);
        var compatibilityEvidence = new ProviderRequestCompatibilityEvidence(
            ProviderRequestCompatibilityEvidence.CurrentSchemaVersion,
            provider.Kind,
            provider.Id,
            provider.Transport,
            provider.DefaultModel,
            provider.DefaultModel,
            ProviderInvocationFeatures.None,
            RequestedEffort: null,
            EffectiveEffort: null,
            ProviderRequestCompatibilityDisposition.Preserved,
            ProviderModelParameterAdjustment.None);
        IReadOnlyList<AgentToolInvocationTrace> toolInvocationTraces =
        [
            new AgentToolInvocationTrace(
                "project_structure_read",
                ToolInvocationClassification.Read,
                Sequence: 1,
                StartedAtUtc: DateTimeOffset.Parse("2026-08-04T00:00:00Z"),
                CompletedAtUtc: DateTimeOffset.Parse("2026-08-04T00:00:01Z"),
                Succeeded: true,
                FailureMessage: string.Empty)
        ];

        return new MafProviderUpdatePumpContext(
            provider,
            provider.DefaultModel,
            new TestAgentSession(),
            runtimeSessionKey,
            UsageUpdates: [],
            usageSourcePhase,
            () => toolInvocationTraces,
            compatibilityEvidence);
    }

    private static AgentResponseUpdate CreateUpdate(string text)
        => new(ChatRole.Assistant, [new TextContent(text)]);

    private sealed class TestAgentSession : AgentSession;

    private sealed class ControllableProviderUpdateStream(
        IReadOnlyList<AgentResponseUpdate> updates,
        Exception? advanceFailure = null,
        Exception? disposalFailure = null) :
        IAsyncEnumerable<AgentResponseUpdate>,
        IAsyncEnumerator<AgentResponseUpdate>
    {
        private int index = -1;

        public int EnumeratorAcquisitionCount { get; private set; }

        public int DisposeCount { get; private set; }

        public AgentResponseUpdate Current => updates[index];

        public IAsyncEnumerator<AgentResponseUpdate> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnumeratorAcquisitionCount++;
            return this;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            if (advanceFailure is not null)
            {
                return ValueTask.FromException<bool>(advanceFailure);
            }

            index++;
            return ValueTask.FromResult(index < updates.Count);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return disposalFailure is null
                ? ValueTask.CompletedTask
                : ValueTask.FromException(disposalFailure);
        }
    }
}
