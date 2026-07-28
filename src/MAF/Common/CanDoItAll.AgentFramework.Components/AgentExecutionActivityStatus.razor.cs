using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Components;

public partial class AgentExecutionActivityStatus : IAsyncDisposable
{
    private const int MaximumDisplayedMessageLength = 240;
    private const string InitialStatusMessage = "Reading agent activity.";
    private const string UnavailableStatusMessage = "Live agent activity is no longer available.";
    private const string ProfileChangedStatusMessage = "Live agent activity ended because the database profile changed.";
    private CancellationTokenSource? readerCancellation;
    private Task readerPump = Task.CompletedTask;
    private AgentExecutionActivityStreamId? observedStreamId;
    private AgentExecutionActivity? latestActivity;
    private long readerGeneration;
    private bool hasSequenceGap;
    private bool isUnavailable;
    private bool isProfileChanged;
    private bool isDisposed;

    [Parameter, EditorRequired]
    public required AgentExecutionActivityStreamId StreamId { get; set; }

    [Inject]
    public IAgentExecutionActivityReader ActivityReader { get; set; } = default!;

    [Inject]
    public ILogger<AgentExecutionActivityStatus> Logger { get; set; } = default!;

    private bool HasSequenceGap => hasSequenceGap;

    private string StatusLabel
        => isUnavailable
            ? "Updates unavailable"
            : latestActivity is null
                ? "Starting agent"
                : ResolvePhaseLabel(latestActivity.Phase);

    private string StatusTone
        => isUnavailable
            ? "warning"
            : latestActivity is null
                ? "info"
                : ResolvePhaseTone(latestActivity.Phase);

    private string StatusMessage
        => isUnavailable
            ? isProfileChanged
                ? ProfileChangedStatusMessage
                : UnavailableStatusMessage
            : latestActivity is null
                ? InitialStatusMessage
                : NormalizeDisplayedMessage(latestActivity.Message);

    protected override async Task OnParametersSetAsync()
    {
        if (isDisposed || observedStreamId == StreamId)
        {
            return;
        }

        await StopReaderAsync();
        if (isDisposed)
        {
            return;
        }

        observedStreamId = StreamId;
        latestActivity = null;
        hasSequenceGap = false;
        isUnavailable = false;
        isProfileChanged = false;
        var generation = Interlocked.Increment(ref readerGeneration);
        var cancellation = new CancellationTokenSource();
        readerCancellation = cancellation;

        try
        {
            var reader = ActivityReader.OpenReader(
                StreamId,
                StreamSequence.Beginning);
            readerPump = PumpReaderAsync(
                reader,
                generation,
                cancellation.Token);
        }
        catch (Exception exception)
        {
            cancellation.Dispose();
            readerCancellation = null;
            isUnavailable = true;
            LogReaderFailure(exception);
        }
    }

    private async Task PumpReaderAsync(
        ISequencedStreamReader<AgentExecutionActivity> reader,
        long generation,
        CancellationToken cancellationToken)
    {
        await using (reader)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = await reader
                        .ReadAsync(cancellationToken)
                        .ConfigureAwait(false);
                    var shouldContinue = await ApplyReadResultAsync(
                        result,
                        generation,
                        cancellationToken).ConfigureAwait(false);
                    if (!shouldContinue)
                    {
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (OperationCanceledException)
            {
                await SetUnavailableAsync(
                    generation,
                    profileChanged: true,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                LogReaderFailure(exception);
                await SetUnavailableAsync(
                    generation,
                    profileChanged: false,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> ApplyReadResultAsync(
        SequencedStreamReadResult<AgentExecutionActivity> result,
        long generation,
        CancellationToken cancellationToken)
    {
        switch (result)
        {
            case SequencedStreamEvents<AgentExecutionActivity> events:
            {
                var activity = events.Items[^1].Event;
                await ApplyStateAsync(
                    generation,
                    () => latestActivity = activity,
                    cancellationToken).ConfigureAwait(false);
                return !activity.IsTerminal;
            }
            case SequencedStreamGap<AgentExecutionActivity>:
                await ApplyStateAsync(
                    generation,
                    () => hasSequenceGap = true,
                    cancellationToken).ConfigureAwait(false);
                return true;
            case SequencedStreamCompleted<AgentExecutionActivity>:
                return false;
            case SequencedStreamEvicted<AgentExecutionActivity>:
            case SequencedStreamUnknown<AgentExecutionActivity>:
                await SetUnavailableAsync(
                    generation,
                    profileChanged: false,
                    cancellationToken).ConfigureAwait(false);
                return false;
            default:
                throw new InvalidOperationException(
                    "The agent activity reader returned an unknown result.");
        }
    }

    private Task SetUnavailableAsync(
        long generation,
        bool profileChanged,
        CancellationToken cancellationToken)
    {
        return ApplyStateAsync(
            generation,
            () =>
            {
                isUnavailable = true;
                isProfileChanged = profileChanged;
            },
            cancellationToken);
    }

    private async Task ApplyStateAsync(
        long generation,
        Action update,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested ||
            generation != Volatile.Read(ref readerGeneration) ||
            isDisposed)
        {
            return;
        }

        await InvokeAsync(() =>
        {
            if (cancellationToken.IsCancellationRequested ||
                generation != Volatile.Read(ref readerGeneration) ||
                isDisposed)
            {
                return;
            }

            update();
            StateHasChanged();
        });
    }

    private async Task StopReaderAsync()
    {
        var cancellation = Interlocked.Exchange(
            ref readerCancellation,
            null);
        var pump = readerPump;
        readerPump = Task.CompletedTask;
        Interlocked.Increment(ref readerGeneration);
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        try
        {
            await pump;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private void LogReaderFailure(Exception exception)
    {
        Logger.LogWarning(
            exception,
            "Agent activity feedback failed. OperationId={OperationId} DatabaseProfileId={DatabaseProfileId} ProfileGeneration={ProfileGeneration} FailureType={FailureType}.",
            StreamId.OperationId.Value,
            StreamId.DatabaseProfileId,
            StreamId.DatabaseProfileGeneration.Value,
            exception.GetType().Name);
    }

    private static string ResolvePhaseLabel(
        AgentExecutionActivityPhase phase)
    {
        return phase switch
        {
            AgentExecutionActivityPhase.Accepted => "Accepted",
            AgentExecutionActivityPhase.CapturingContext => "Capturing context",
            AgentExecutionActivityPhase.ResolvingPreparation => "Loading agent",
            AgentExecutionActivityPhase.ResolvingProvider => "Resolving provider",
            AgentExecutionActivityPhase.ResolvingSession => "Opening thread",
            AgentExecutionActivityPhase.CreatingExecution => "Creating run",
            AgentExecutionActivityPhase.PreparingInput => "Preparing input",
            AgentExecutionActivityPhase.PreparingCapabilities => "Initializing tools",
            AgentExecutionActivityPhase.PreparingRuntime => "Starting runtime",
            AgentExecutionActivityPhase.WaitingForProvider => "Waiting for model",
            AgentExecutionActivityPhase.Streaming => "Agent responding",
            AgentExecutionActivityPhase.UsingTool => "Using tool",
            AgentExecutionActivityPhase.AwaitingApproval => "Approval required",
            AgentExecutionActivityPhase.PersistingResult => "Saving result",
            AgentExecutionActivityPhase.Completed => "Completed",
            AgentExecutionActivityPhase.Failed => "Failed",
            AgentExecutionActivityPhase.Cancelled => "Cancelled",
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
        };
    }

    private static string ResolvePhaseTone(
        AgentExecutionActivityPhase phase)
    {
        return phase switch
        {
            AgentExecutionActivityPhase.Completed => "success",
            AgentExecutionActivityPhase.AwaitingApproval => "warning",
            AgentExecutionActivityPhase.Failed => "danger",
            AgentExecutionActivityPhase.Cancelled => "neutral",
            _ => "info"
        };
    }

    private static string NormalizeDisplayedMessage(string message)
    {
        var normalized = string.Join(
            ' ',
            message.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries));
        return normalized.Length <= MaximumDisplayedMessageLength
            ? normalized
            : $"{normalized[..(MaximumDisplayedMessageLength - 1)].TrimEnd()}…";
    }

    public async ValueTask DisposeAsync()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        observedStreamId = null;
        await StopReaderAsync();
    }
}
