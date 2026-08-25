using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Extensions.AI;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafProviderTransportBoundaryChatClientTests
{
    [Fact]
    public async Task Non_streaming_transport_failure_is_marked_with_provider_identity()
    {
        var provider = CreateProvider();
        var transportFailure = new HttpRequestException("Provider endpoint failed.");
        using var client = new MafProviderTransportBoundaryChatClient(
            new ThrowingChatClient(nonStreamingException: transportFailure),
            provider,
            provider.DefaultModel);

        var exception = await Assert.ThrowsAsync<MafProviderTransportException>(() =>
            client.GetResponseAsync(CreateMessages()));

        Assert.Equal(provider.Id, exception.ProviderProfileId);
        Assert.Equal(provider.DefaultModel, exception.Model);
        Assert.Same(transportFailure, exception.InnerException);

        var sourceSecretId = Guid.Parse(
            "4cf1b375-b3c5-4f80-84ae-d23f4897ccbf");
        var sourceProvider = provider with
        {
            BaseUrl = "http://10.23.45.67:43123/openai/v1",
            CredentialBinding = new ProviderCredentialBinding(
                sourceSecretId,
                ProviderCredentialPurpose.SourceAccessToken,
                ProviderCredentialConsumerKind.Source,
                Guid.Parse("5c745ad6-4ccd-4921-a966-e64c3ace17c1")),
            ModelSelectionConstraint = new ProviderModelSelectionConstraint(
                [provider.DefaultModel])
        };
        const string remoteMarker = "raw-private-provider-failure";
        using var sourceClient = new MafProviderTransportBoundaryChatClient(
            new ThrowingChatClient(
                nonStreamingException: new HttpRequestException(
                    $"{remoteMarker} at {sourceProvider.BaseUrl}; secret={sourceSecretId:D}.")),
            sourceProvider,
            sourceProvider.DefaultModel);

        var sourceException = await Assert.ThrowsAsync<
            MafProviderTransportException>(() =>
            sourceClient.GetResponseAsync(CreateMessages()));

        var boundary = Assert.IsType<ProviderFailureBoundaryException>(
            sourceException.InnerException);
        Assert.Equal(ProviderFailureOperation.RuntimeRequest,
            boundary.Operation);
        Assert.Null(boundary.InnerException);
        Assert.Contains(
            ProviderFailureDisclosurePolicy.SanitizedRuntimeFailureMessage,
            sourceException.ToString(),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            sourceProvider.BaseUrl,
            sourceException.ToString(),
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            sourceSecretId.ToString("D"),
            sourceException.ToString(),
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            remoteMarker,
            sourceException.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Streaming_transport_failure_is_marked_at_enumerator_advance()
    {
        var provider = CreateProvider();
        var transportFailure = new HttpRequestException("Provider stream failed.");
        using var client = new MafProviderTransportBoundaryChatClient(
            new ThrowingChatClient(streamingException: transportFailure),
            provider,
            provider.DefaultModel);

        var exception = await Assert.ThrowsAsync<MafProviderTransportException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(CreateMessages()))
            {
            }
        });

        Assert.Equal(provider.Id, exception.ProviderProfileId);
        Assert.Equal(provider.DefaultModel, exception.Model);
        Assert.Same(transportFailure, exception.InnerException);
    }

    [Fact]
    public async Task Requested_cancellation_is_not_reclassified_as_provider_failure()
    {
        var provider = CreateProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var client = new MafProviderTransportBoundaryChatClient(
            new ThrowingChatClient(
                nonStreamingException: new OperationCanceledException(cancellation.Token)),
            provider,
            provider.DefaultModel);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetResponseAsync(CreateMessages(), cancellationToken: cancellation.Token));

        Assert.IsNotType<MafProviderTransportException>(exception);
    }

    [Fact]
    public async Task Internal_transport_cancellation_is_marked_when_caller_did_not_cancel()
    {
        var provider = CreateProvider();
        using var client = new MafProviderTransportBoundaryChatClient(
            new ThrowingChatClient(
                streamingException: new OperationCanceledException("Transport timeout.")),
            provider,
            provider.DefaultModel);

        var exception = await Assert.ThrowsAsync<MafProviderTransportException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(CreateMessages()))
            {
            }
        });

        Assert.IsType<OperationCanceledException>(exception.InnerException);
    }

    [Fact]
    public async Task Streaming_disposal_failure_is_marked_as_provider_transport_failure()
    {
        var provider = CreateProvider();
        var disposalFailure = new IOException("Transport stream disposal failed.");
        using var client = new MafProviderTransportBoundaryChatClient(
            new ThrowingChatClient(streamingDisposalException: disposalFailure),
            provider,
            provider.DefaultModel);

        var exception = await Assert.ThrowsAsync<MafProviderTransportException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(CreateMessages()))
            {
                break;
            }
        });

        Assert.Same(disposalFailure, exception.InnerException);
        Assert.Equal(
            AgentRuntimeFailureOrigin.Provider,
            MafRuntimeFailureOriginClassifier.ResolveOutsideProviderBoundary(exception));
    }

    [Fact]
    public async Task Disposal_failure_does_not_mask_requested_cancellation()
    {
        var provider = CreateProvider();
        using var cancellation = new CancellationTokenSource();
        using var client = new MafProviderTransportBoundaryChatClient(
            new ThrowingChatClient(
                streamingException: new OperationCanceledException(cancellation.Token),
                streamingDisposalException: new IOException("Secondary disposal failure."),
                onStreamingMoveNext: cancellation.Cancel),
            provider,
            provider.DefaultModel);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(
                               CreateMessages(),
                               cancellationToken: cancellation.Token))
            {
            }
        });

        Assert.IsNotType<MafProviderTransportException>(exception);
        Assert.Equal(
            typeof(IOException).FullName,
            exception.Data["CanDoItAll.ProviderTransportDisposalFailureType"]);
    }

    [Fact]
    public async Task Streaming_idle_watchdog_marks_provider_failure_and_cancels_raw_transport()
    {
        var provider = CreateProvider();
        var stream = new HangingTransportAsyncEnumerable(hangDuringDisposal: false);
        using var client = CreateBoundaryClient(
            new StreamingChatClient(stream),
            provider,
            idleTimeout: TimeSpan.FromMilliseconds(40),
            absoluteTimeout: TimeSpan.FromSeconds(1));

        var exception = await Assert.ThrowsAsync<MafProviderTransportException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(CreateMessages()))
            {
            }
        });

        Assert.IsType<TimeoutException>(exception.InnerException);
        Assert.True(stream.TransportCancellationWasRequested);
        Assert.Equal(provider.Id, exception.ProviderProfileId);
    }

    [Fact]
    public async Task Empty_heartbeats_do_not_reset_streaming_idle_watchdog()
    {
        var provider = CreateProvider();
        var stream = new HeartbeatAsyncEnumerable(
            TimeSpan.FromMilliseconds(10),
            semanticProgress: false,
            updateLimit: null);
        using var client = CreateBoundaryClient(
            new StreamingChatClient(stream),
            provider,
            idleTimeout: TimeSpan.FromMilliseconds(60),
            absoluteTimeout: TimeSpan.FromSeconds(1));

        var exception = await Assert.ThrowsAsync<MafProviderTransportException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(CreateMessages()))
            {
            }
        });

        Assert.IsType<TimeoutException>(exception.InnerException);
        Assert.True(stream.TransportCancellationWasRequested);
    }

    [Fact]
    public async Task Semantic_updates_reset_streaming_idle_watchdog()
    {
        var provider = CreateProvider();
        var stream = new HeartbeatAsyncEnumerable(
            TimeSpan.FromMilliseconds(250),
            semanticProgress: true,
            updateLimit: 9);
        using var client = CreateBoundaryClient(
            new StreamingChatClient(stream),
            provider,
            idleTimeout: TimeSpan.FromSeconds(2),
            absoluteTimeout: TimeSpan.FromSeconds(8));
        var updates = new List<ChatResponseUpdate>();

        await foreach (var update in client.GetStreamingResponseAsync(CreateMessages()))
        {
            updates.Add(update);
        }

        Assert.Equal(9, updates.Count);
        Assert.False(stream.TransportCancellationWasRequested);
    }

    [Fact]
    public async Task Absolute_watchdog_expires_while_semantic_updates_continue()
    {
        var provider = CreateProvider();
        var stream = new HeartbeatAsyncEnumerable(
            TimeSpan.FromMilliseconds(10),
            semanticProgress: true,
            updateLimit: null);
        using var client = CreateBoundaryClient(
            new StreamingChatClient(stream),
            provider,
            idleTimeout: TimeSpan.FromSeconds(1),
            absoluteTimeout: TimeSpan.FromMilliseconds(70));

        var exception = await Assert.ThrowsAsync<MafProviderTransportException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(CreateMessages()))
            {
            }
        });

        Assert.IsType<TimeoutException>(exception.InnerException);
        Assert.True(stream.TransportCancellationWasRequested);
    }

    [Fact]
    public async Task Hung_provider_disposal_cannot_mask_watchdog_or_hold_dispatch_lane()
    {
        var provider = CreateProvider();
        var gate = new TrackingDispatchGate();
        var hangingStream = new HangingTransportAsyncEnumerable(hangDuringDisposal: true);
        using var stalledClient = CreateBoundaryClient(
            new StreamingChatClient(hangingStream),
            provider,
            gate,
            idleTimeout: TimeSpan.FromMilliseconds(30),
            absoluteTimeout: TimeSpan.FromSeconds(1),
            cleanupTimeout: TimeSpan.FromMilliseconds(30));

        var exception = await Assert.ThrowsAsync<MafProviderTransportException>(async () =>
        {
            await foreach (var _ in stalledClient.GetStreamingResponseAsync(CreateMessages()))
            {
            }
        }).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.IsType<TimeoutException>(exception.InnerException);
        Assert.Equal(
            typeof(TimeoutException).FullName,
            exception.Data[MafProviderTransportException.DisposalFailureTypeDataKey]);
        Assert.Equal(0, gate.ActiveLeaseCount);

        using var succeedingClient = CreateBoundaryClient(
            new DelayedChatClient(TimeSpan.Zero),
            provider,
            gate);
        var response = await succeedingClient
            .GetResponseAsync(CreateMessages())
            .WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal("completed", response.Text);
        Assert.Equal(2, gate.EntryCount);
    }

    [Fact]
    public async Task Dispatch_gate_serializes_actual_provider_requests()
    {
        var provider = CreateProvider();
        var gate = new TrackingDispatchGate();
        using var client = CreateBoundaryClient(
            new DelayedChatClient(TimeSpan.FromMilliseconds(60)),
            provider,
            gate);

        await Task.WhenAll(
            client.GetResponseAsync(CreateMessages()),
            client.GetResponseAsync(CreateMessages()));

        Assert.Equal(2, gate.EntryCount);
        Assert.Equal(1, gate.MaximumActiveLeaseCount);
        Assert.Equal(0, gate.ActiveLeaseCount);
    }

    [Fact]
    public async Task Non_cooperative_timed_out_transport_retains_lane_until_raw_move_next_ends()
    {
        var provider = CreateProvider();
        var gate = new TrackingDispatchGate();
        var zombieStream = new NonCooperativeAsyncEnumerable();
        using var stalledClient = CreateBoundaryClient(
            new StreamingChatClient(zombieStream),
            provider,
            gate,
            idleTimeout: TimeSpan.FromMilliseconds(30),
            absoluteTimeout: TimeSpan.FromSeconds(1),
            cleanupTimeout: TimeSpan.FromMilliseconds(30));

        var timeout = await Assert.ThrowsAsync<MafProviderTransportException>(async () =>
        {
            await foreach (var _ in stalledClient.GetStreamingResponseAsync(CreateMessages()))
            {
            }
        }).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.IsType<TimeoutException>(timeout.InnerException);
        Assert.True(zombieStream.TransportCancellationWasRequested);
        Assert.Equal(1, gate.ActiveLeaseCount);

        using var succeedingClient = CreateBoundaryClient(
            new DelayedChatClient(TimeSpan.Zero),
            provider,
            gate);
        var secondRequest = succeedingClient.GetResponseAsync(CreateMessages());
        await Task.Delay(80);

        Assert.False(secondRequest.IsCompleted);
        Assert.Equal(1, gate.EntryCount);

        zombieStream.CompleteMoveNext();
        var response = await secondRequest.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal("completed", response.Text);
        Assert.Equal(2, gate.EntryCount);
        Assert.Equal(0, gate.ActiveLeaseCount);
        Assert.Equal(1, zombieStream.DisposeCount);
    }

    private static MafProviderTransportBoundaryChatClient CreateBoundaryClient(
        IChatClient innerClient,
        ProviderProfile provider,
        IMafProviderStreamingDispatchGate? gate = null,
        TimeSpan? idleTimeout = null,
        TimeSpan? absoluteTimeout = null,
        TimeSpan? cleanupTimeout = null)
        => new(
            innerClient,
            provider,
            provider.DefaultModel,
            gate ?? NoOpMafProviderStreamingDispatchGate.Instance,
            _ => idleTimeout ?? TimeSpan.FromSeconds(1),
            _ => absoluteTimeout ?? TimeSpan.FromSeconds(2),
            _ => cleanupTimeout ?? TimeSpan.FromSeconds(1));

    private static IReadOnlyList<ChatMessage> CreateMessages()
        => [new ChatMessage(ChatRole.User, "test")];

    private static ProviderProfile CreateProvider()
        => new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Transport boundary test provider",
            ProviderKind.OpenAi,
            "https://openai.test/v1",
            "UNUSED_TEST_KEY",
            "unit-model",
            ProviderTransportKind.Responses,
            true,
            true,
            true,
            false,
            true,
            "{}",
            string.Empty,
            "ok",
            null,
            []);

    private sealed class ThrowingChatClient(
        Exception? nonStreamingException = null,
        Exception? streamingException = null,
        Exception? streamingDisposalException = null,
        Action? onStreamingMoveNext = null) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromException<ChatResponse>(
                nonStreamingException ??
                new InvalidOperationException("A non-streaming exception was not configured."));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => new ThrowingAsyncEnumerable(
                streamingException,
                streamingDisposalException,
                onStreamingMoveNext);

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this)
                ? this
                : null;

        public void Dispose()
        {
        }
    }

    private sealed class ThrowingAsyncEnumerable(
        Exception? moveNextException,
        Exception? disposalException,
        Action? onMoveNext = null) :
        IAsyncEnumerable<ChatResponseUpdate>,
        IAsyncEnumerator<ChatResponseUpdate>
    {
        private bool advanced;

        public ChatResponseUpdate Current { get; } = new(
            ChatRole.Assistant,
            [new TextContent("unused")]);

        public IAsyncEnumerator<ChatResponseUpdate> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
            => this;

        public ValueTask<bool> MoveNextAsync()
        {
            onMoveNext?.Invoke();

            if (moveNextException is not null)
            {
                return ValueTask.FromException<bool>(moveNextException);
            }

            if (advanced)
            {
                return ValueTask.FromResult(false);
            }

            advanced = true;
            return ValueTask.FromResult(true);
        }

        public ValueTask DisposeAsync()
            => disposalException is null
                ? ValueTask.CompletedTask
                : ValueTask.FromException(disposalException);
    }

    private sealed class StreamingChatClient(IAsyncEnumerable<ChatResponseUpdate> updates) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => updates;

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this)
                ? this
                : null;

        public void Dispose()
        {
        }
    }

    private sealed class DelayedChatClient(TimeSpan delay) : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(delay, cancellationToken);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed"));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this)
                ? this
                : null;

        public void Dispose()
        {
        }
    }

    private sealed class HangingTransportAsyncEnumerable(bool hangDuringDisposal) :
        IAsyncEnumerable<ChatResponseUpdate>,
        IAsyncEnumerator<ChatResponseUpdate>
    {
        private readonly TaskCompletionSource<bool> moveNext =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource disposal =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenRegistration cancellationRegistration;

        public ChatResponseUpdate Current => throw new InvalidOperationException();

        public bool TransportCancellationWasRequested { get; private set; }

        public IAsyncEnumerator<ChatResponseUpdate> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
        {
            cancellationRegistration = cancellationToken.Register(() =>
            {
                TransportCancellationWasRequested = true;
                moveNext.TrySetCanceled(cancellationToken);
            });
            return this;
        }

        public ValueTask<bool> MoveNextAsync()
            => new(moveNext.Task);

        public async ValueTask DisposeAsync()
        {
            cancellationRegistration.Dispose();
            if (hangDuringDisposal)
            {
                await disposal.Task;
            }
        }
    }

    private sealed class HeartbeatAsyncEnumerable(
        TimeSpan delay,
        bool semanticProgress,
        int? updateLimit) :
        IAsyncEnumerable<ChatResponseUpdate>,
        IAsyncEnumerator<ChatResponseUpdate>
    {
        private CancellationToken cancellationToken;
        private CancellationTokenRegistration cancellationRegistration;
        private int updateCount;

        public ChatResponseUpdate Current { get; private set; } = new(ChatRole.Assistant, []);

        public bool TransportCancellationWasRequested { get; private set; }

        public IAsyncEnumerator<ChatResponseUpdate> GetAsyncEnumerator(
            CancellationToken requestedCancellationToken = default)
        {
            cancellationToken = requestedCancellationToken;
            cancellationRegistration = cancellationToken.Register(
                () => TransportCancellationWasRequested = true);
            return this;
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            if (updateLimit is not null && updateCount >= updateLimit.Value)
            {
                return false;
            }

            await Task.Delay(delay, cancellationToken);
            updateCount++;
            Current = semanticProgress
                ? new ChatResponseUpdate(
                    ChatRole.Assistant,
                    [new TextContent($"update {updateCount}")])
                : new ChatResponseUpdate(ChatRole.Assistant, []);
            return true;
        }

        public ValueTask DisposeAsync()
        {
            cancellationRegistration.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NonCooperativeAsyncEnumerable :
        IAsyncEnumerable<ChatResponseUpdate>,
        IAsyncEnumerator<ChatResponseUpdate>
    {
        private readonly TaskCompletionSource<bool> moveNext =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenRegistration cancellationRegistration;
        private int disposeCount;

        public ChatResponseUpdate Current => throw new InvalidOperationException();

        public int DisposeCount => Volatile.Read(ref disposeCount);

        public bool TransportCancellationWasRequested { get; private set; }

        public IAsyncEnumerator<ChatResponseUpdate> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
        {
            cancellationRegistration = cancellationToken.Register(
                () => TransportCancellationWasRequested = true);
            return this;
        }

        public ValueTask<bool> MoveNextAsync()
            => new(moveNext.Task);

        public ValueTask DisposeAsync()
        {
            cancellationRegistration.Dispose();
            Interlocked.Increment(ref disposeCount);
            return ValueTask.CompletedTask;
        }

        public void CompleteMoveNext()
            => moveNext.TrySetResult(false);
    }

    private sealed class TrackingDispatchGate : IMafProviderStreamingDispatchGate
    {
        private readonly SemaphoreSlim semaphore = new(1, 1);
        private int activeLeaseCount;
        private int entryCount;
        private int maximumActiveLeaseCount;

        public int ActiveLeaseCount => Volatile.Read(ref activeLeaseCount);

        public int EntryCount => Volatile.Read(ref entryCount);

        public int MaximumActiveLeaseCount => Volatile.Read(ref maximumActiveLeaseCount);

        public async ValueTask<IAsyncDisposable> EnterAsync(
            ProviderProfile provider,
            string model,
            CancellationToken cancellationToken = default)
        {
            await semaphore.WaitAsync(cancellationToken);
            var active = Interlocked.Increment(ref activeLeaseCount);
            Interlocked.Increment(ref entryCount);
            UpdateMaximum(active);
            return new Lease(this);
        }

        private void UpdateMaximum(int candidate)
        {
            while (true)
            {
                var current = Volatile.Read(ref maximumActiveLeaseCount);
                if (candidate <= current ||
                    Interlocked.CompareExchange(ref maximumActiveLeaseCount, candidate, current) == current)
                {
                    return;
                }
            }
        }

        private sealed class Lease(TrackingDispatchGate owner) : IAsyncDisposable
        {
            private int disposed;

            public ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0)
                {
                    Interlocked.Decrement(ref owner.activeLeaseCount);
                    owner.semaphore.Release();
                }

                return ValueTask.CompletedTask;
            }
        }
    }
}
