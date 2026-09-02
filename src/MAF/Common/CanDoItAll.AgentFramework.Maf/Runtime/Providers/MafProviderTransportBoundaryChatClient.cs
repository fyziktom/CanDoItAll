using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafProviderTransportException : Exception
{
    internal const string DisposalFailureTypeDataKey =
        "CanDoItAll.ProviderTransportDisposalFailureType";

    public MafProviderTransportException(
        Guid providerProfileId,
        string model,
        Exception innerException)
        : this(
            providerProfileId,
            string.Empty,
            providerKind: null,
            transport: null,
            model,
            innerException)
    {
    }

    public MafProviderTransportException(
        ProviderProfile provider,
        string model,
        Exception innerException)
        : this(
            provider.Id,
            provider.Name,
            provider.Kind,
            provider.Transport,
            model,
            innerException)
    {
    }

    private MafProviderTransportException(
        Guid providerProfileId,
        string providerName,
        ProviderKind? providerKind,
        ProviderTransportKind? transport,
        string model,
        Exception innerException)
        : base("Provider runtime failed at the transport boundary.", innerException)
    {
        ProviderProfileId = providerProfileId;
        ProviderName = providerName;
        ProviderKind = providerKind;
        Transport = transport;
        Model = model;
    }

    public Guid ProviderProfileId { get; }

    public string ProviderName { get; }

    public ProviderKind? ProviderKind { get; }

    public ProviderTransportKind? Transport { get; }

    public string Model { get; }

    internal static string ResolveDiagnosticFailureType(Exception exception)
    {
        var innerException = (exception as MafProviderTransportException)?.InnerException;
        return innerException is ProviderFailureBoundaryException
            {
                DiagnosticFailureType: { Length: > 0 } diagnosticFailureType
            }
                ? diagnosticFailureType
                : innerException?.GetType().FullName ??
                  exception.GetType().FullName ??
                  exception.GetType().Name;
    }
}

internal sealed class MafProviderTransportBoundaryChatClient : DelegatingChatClient
{
    private const string DispatchLeaseDisposalFailureTypeDataKey =
        "CanDoItAll.ProviderDispatchLeaseDisposalFailureType";
    private const string WatchdogCancellationFailureTypeDataKey =
        "CanDoItAll.ProviderWatchdogCancellationFailureType";
    private const string DeferredCleanupDataKey =
        "CanDoItAll.ProviderTransportCleanupDeferred";
    private static readonly TimeSpan DefaultStreamingCleanupTimeout = TimeSpan.FromSeconds(5);
    private static readonly ConcurrentDictionary<long, Task> DeferredCleanups = new();
    private static long deferredCleanupId;
    private readonly ProviderProfile provider;
    private readonly string model;
    private readonly IMafProviderStreamingDispatchGate dispatchGate;
    private readonly Func<ProviderProfile, TimeSpan> resolveStreamingIdleTimeout;
    private readonly Func<ProviderProfile, TimeSpan> resolveStreamingAbsoluteTimeout;
    private readonly Func<ProviderProfile, TimeSpan> resolveStreamingCleanupTimeout;
    private readonly ILogger<MafProviderTransportBoundaryChatClient> logger;

    public MafProviderTransportBoundaryChatClient(
        IChatClient innerClient,
        ProviderProfile provider,
        string model)
        : this(
            innerClient,
            provider,
            model,
            NoOpMafProviderStreamingDispatchGate.Instance,
            MafProviderRuntimeSettings.ResolveStreamingIdleTimeout,
            MafProviderRuntimeSettings.ResolveStreamingAbsoluteTimeout,
            _ => DefaultStreamingCleanupTimeout,
            NullLogger<MafProviderTransportBoundaryChatClient>.Instance)
    {
    }

    internal MafProviderTransportBoundaryChatClient(
        IChatClient innerClient,
        ProviderProfile provider,
        string model,
        IMafProviderStreamingDispatchGate dispatchGate,
        Func<ProviderProfile, TimeSpan>? resolveStreamingIdleTimeout = null,
        Func<ProviderProfile, TimeSpan>? resolveStreamingAbsoluteTimeout = null,
        Func<ProviderProfile, TimeSpan>? resolveStreamingCleanupTimeout = null,
        ILogger<MafProviderTransportBoundaryChatClient>? logger = null)
        : base(innerClient)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        this.model = string.IsNullOrWhiteSpace(model)
            ? throw new ArgumentException("A provider model is required.", nameof(model))
            : model;
        this.dispatchGate = dispatchGate ?? throw new ArgumentNullException(nameof(dispatchGate));
        this.resolveStreamingIdleTimeout = resolveStreamingIdleTimeout ??
                                           MafProviderRuntimeSettings.ResolveStreamingIdleTimeout;
        this.resolveStreamingAbsoluteTimeout = resolveStreamingAbsoluteTimeout ??
                                               MafProviderRuntimeSettings.ResolveStreamingAbsoluteTimeout;
        this.resolveStreamingCleanupTimeout = resolveStreamingCleanupTimeout ??
                                              (_ => DefaultStreamingCleanupTimeout);
        this.logger = logger ?? NullLogger<MafProviderTransportBoundaryChatClient>.Instance;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var dispatchLease = await dispatchGate.EnterAsync(
            provider,
            model,
            cancellationToken).ConfigureAwait(false);
        Exception? primaryFailure = null;
        ChatResponse? response = null;
        try
        {
            try
            {
                response = await base
                    .GetResponseAsync(messages, options, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                primaryFailure = exception;
            }
            catch (MafProviderTransportException exception)
            {
                primaryFailure = CreateTransportException(exception);
            }
            catch (Exception exception)
            {
                primaryFailure = CreateTransportException(exception);
            }
        }
        finally
        {
            var disposalFailure = await CaptureDispatchLeaseDisposalFailureAsync(dispatchLease)
                .ConfigureAwait(false);
            ThrowPrimaryOrDisposalFailure(primaryFailure, disposalFailure, isProviderDisposal: false);
        }

        return response!;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var idleTimeout = resolveStreamingIdleTimeout(provider);
        ValidateStreamingTimeout(idleTimeout, "idle");
        var absoluteTimeout = resolveStreamingAbsoluteTimeout(provider);
        ValidateStreamingTimeout(absoluteTimeout, "absolute");
        var cleanupTimeout = resolveStreamingCleanupTimeout(provider);
        ValidateStreamingTimeout(cleanupTimeout, "cleanup");

        var dispatchLease = await dispatchGate.EnterAsync(
            provider,
            model,
            cancellationToken).ConfigureAwait(false);
        using var transportCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IAsyncEnumerator<ChatResponseUpdate>? enumerator = null;
        Task<bool>? inFlightMoveNext = null;
        Exception? primaryFailure = null;
        var streamStartedAt = Stopwatch.GetTimestamp();
        var lastSemanticProgressAt = streamStartedAt;
        try
        {
            try
            {
                enumerator = base
                    .GetStreamingResponseAsync(messages, options, transportCancellation.Token)
                    .GetAsyncEnumerator(transportCancellation.Token);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                primaryFailure = exception;
            }
            catch (MafProviderTransportException exception)
            {
                primaryFailure = CreateTransportException(exception);
            }
            catch (Exception exception)
            {
                primaryFailure = CreateTransportException(exception);
            }

            while (primaryFailure is null && enumerator is not null)
            {
                var watchdog = ResolveWatchdogDeadline(
                    streamStartedAt,
                    lastSemanticProgressAt,
                    idleTimeout,
                    absoluteTimeout);
                if (watchdog.Remaining <= TimeSpan.Zero)
                {
                    primaryFailure = await CreateWatchdogFailureAsync(
                        transportCancellation,
                        watchdog.IsAbsoluteDeadline,
                        innerException: null).ConfigureAwait(false);
                    break;
                }

                bool hasNext;
                try
                {
                    inFlightMoveNext = enumerator
                        .MoveNextAsync()
                        .AsTask();
                    hasNext = await inFlightMoveNext
                        .WaitAsync(watchdog.Remaining, cancellationToken)
                        .ConfigureAwait(false);
                    inFlightMoveNext = null;
                }
                catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
                {
                    primaryFailure = exception;
                    break;
                }
                catch (TimeoutException exception)
                {
                    primaryFailure = await CreateWatchdogFailureAsync(
                        transportCancellation,
                        watchdog.IsAbsoluteDeadline,
                        exception).ConfigureAwait(false);
                    break;
                }
                catch (MafProviderTransportException exception)
                {
                    inFlightMoveNext = null;
                    primaryFailure = CreateTransportException(exception);
                    break;
                }
                catch (Exception exception)
                {
                    inFlightMoveNext = null;
                    primaryFailure = CreateTransportException(exception);
                    break;
                }

                if (!hasNext)
                {
                    break;
                }

                var update = enumerator.Current;
                if (HasSemanticProgress(update))
                {
                    lastSemanticProgressAt = Stopwatch.GetTimestamp();
                }

                yield return update;
            }
        }
        finally
        {
            if (inFlightMoveNext is { IsCompleted: false })
            {
                var cleanupGrace = cleanupTimeout < TimeSpan.FromMilliseconds(100)
                    ? cleanupTimeout
                    : TimeSpan.FromMilliseconds(100);
                await Task.WhenAny(
                    inFlightMoveNext,
                    Task.Delay(cleanupGrace)).ConfigureAwait(false);
            }

            if (enumerator is not null &&
                inFlightMoveNext is { IsCompleted: false })
            {
                primaryFailure ??= CreateTransportException(
                    new InvalidOperationException(
                        "Provider streaming cleanup was deferred with an active transport operation."));
                primaryFailure.Data[DeferredCleanupDataKey] = true;
                TrackDeferredCleanup(CompleteDeferredCleanupAsync(
                    inFlightMoveNext,
                    enumerator,
                    dispatchLease,
                    cleanupTimeout,
                    provider.Id,
                    model,
                    logger));
                ExceptionDispatchInfo.Capture(primaryFailure).Throw();
            }

            if (inFlightMoveNext is not null)
            {
                var inFlightFailure = await CaptureCompletedMoveNextFailureAsync(inFlightMoveNext)
                    .ConfigureAwait(false);
                AttachSecondaryFailure(
                    primaryFailure!,
                    inFlightFailure,
                    MafProviderTransportException.DisposalFailureTypeDataKey,
                    isProviderDisposal: true);
            }

            var providerDisposalFailure = enumerator is null
                ? null
                : await CaptureProviderEnumeratorDisposalFailureAsync(
                    enumerator,
                    cancellationToken,
                    cleanupTimeout).ConfigureAwait(false);
            var dispatchDisposalFailure = await CaptureDispatchLeaseDisposalFailureAsync(dispatchLease)
                .ConfigureAwait(false);

            if (primaryFailure is not null)
            {
                AttachSecondaryFailure(
                    primaryFailure,
                    providerDisposalFailure,
                    MafProviderTransportException.DisposalFailureTypeDataKey,
                    isProviderDisposal: true);
                AttachSecondaryFailure(
                    primaryFailure,
                    dispatchDisposalFailure,
                    DispatchLeaseDisposalFailureTypeDataKey,
                    isProviderDisposal: false);
                ExceptionDispatchInfo.Capture(primaryFailure).Throw();
            }

            if (providerDisposalFailure is not null)
            {
                AttachSecondaryFailure(
                    providerDisposalFailure,
                    dispatchDisposalFailure,
                    DispatchLeaseDisposalFailureTypeDataKey,
                    isProviderDisposal: false);
                ExceptionDispatchInfo.Capture(providerDisposalFailure).Throw();
            }

            if (dispatchDisposalFailure is not null)
            {
                ExceptionDispatchInfo.Capture(dispatchDisposalFailure).Throw();
            }
        }
    }

    private static void TrackDeferredCleanup(Task cleanup)
    {
        long cleanupId;
        do
        {
            cleanupId = Interlocked.Increment(ref deferredCleanupId);
        }
        while (!DeferredCleanups.TryAdd(cleanupId, cleanup));

        _ = cleanup.ContinueWith(
            static (completedCleanup, state) =>
            {
                _ = completedCleanup.Exception;
                DeferredCleanups.TryRemove((long)state!, out _);
            },
            cleanupId,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static async Task CompleteDeferredCleanupAsync(
        Task<bool> inFlightMoveNext,
        IAsyncEnumerator<ChatResponseUpdate> enumerator,
        IAsyncDisposable dispatchLease,
        TimeSpan cleanupTimeout,
        Guid providerProfileId,
        string model,
        ILogger logger)
    {
        try
        {
            await inFlightMoveNext.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                "Deferred provider MoveNext completed with failure type {FailureType}. ProviderProfileId={ProviderProfileId}, Model={Model}.",
                exception.GetType().FullName,
                providerProfileId,
                model);
        }

        Task? disposal = null;
        try
        {
            disposal = enumerator.DisposeAsync().AsTask();
            await disposal
                .WaitAsync(cleanupTimeout)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (disposal is { IsCompleted: false })
            {
                TrackDeferredCleanup(ObserveLateDisposalAsync(
                    disposal,
                    providerProfileId,
                    model,
                    logger));
            }

            logger.LogWarning(
                "Deferred provider stream disposal failed with type {FailureType}. ProviderProfileId={ProviderProfileId}, Model={Model}.",
                exception.GetType().FullName,
                providerProfileId,
                model);
        }

        try
        {
            await dispatchLease.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Deferred provider dispatch lease disposal failed with type {FailureType}. ProviderProfileId={ProviderProfileId}, Model={Model}.",
                exception.GetType().FullName,
                providerProfileId,
                model);
        }
    }

    private static async ValueTask<Exception?> CaptureCompletedMoveNextFailureAsync(
        Task<bool> inFlightMoveNext)
    {
        try
        {
            await inFlightMoveNext.ConfigureAwait(false);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static void ValidateStreamingTimeout(TimeSpan timeout, string timeoutKind)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"The provider streaming {timeoutKind} timeout must be positive.");
        }
    }

    private static (TimeSpan Remaining, bool IsAbsoluteDeadline) ResolveWatchdogDeadline(
        long streamStartedAt,
        long lastSemanticProgressAt,
        TimeSpan semanticIdleTimeout,
        TimeSpan absoluteTimeout)
    {
        var semanticRemaining = semanticIdleTimeout - Stopwatch.GetElapsedTime(lastSemanticProgressAt);
        var absoluteRemaining = absoluteTimeout - Stopwatch.GetElapsedTime(streamStartedAt);
        return absoluteRemaining <= semanticRemaining
            ? (absoluteRemaining, true)
            : (semanticRemaining, false);
    }

    private async Task<MafProviderTransportException> CreateWatchdogFailureAsync(
        CancellationTokenSource transportCancellation,
        bool isAbsoluteDeadline,
        Exception? innerException)
    {
        Exception? cancellationFailure = null;
        try
        {
            await transportCancellation.CancelAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            cancellationFailure = exception;
        }

        var message = isAbsoluteDeadline
            ? "Provider streaming exceeded the configured absolute deadline."
            : "Provider streaming made no semantic progress before the configured idle deadline.";
        var timeout = innerException is null
            ? new TimeoutException(message)
            : new TimeoutException(message, innerException);
        if (cancellationFailure is not null)
        {
            timeout.Data[WatchdogCancellationFailureTypeDataKey] =
                cancellationFailure.GetType().FullName ?? cancellationFailure.GetType().Name;
        }

        return CreateTransportException(timeout);
    }

    private static bool HasSemanticProgress(ChatResponseUpdate update)
    {
        if (update.FinishReason is not null)
        {
            return true;
        }

        return update.Contents.Any(content => content switch
        {
            TextContent text => !string.IsNullOrWhiteSpace(text.Text),
            _ => true
        });
    }

    private async ValueTask<Exception?> CaptureProviderEnumeratorDisposalFailureAsync(
        IAsyncEnumerator<ChatResponseUpdate> enumerator,
        CancellationToken cancellationToken,
        TimeSpan cleanupTimeout)
    {
        Task? disposal = null;
        try
        {
            disposal = enumerator.DisposeAsync().AsTask();
            await disposal
                .WaitAsync(cleanupTimeout)
                .ConfigureAwait(false);
            return null;
        }
        catch (TimeoutException exception)
        {
            if (disposal is { IsCompleted: false })
            {
                TrackDeferredCleanup(ObserveLateDisposalAsync(
                    disposal,
                    provider.Id,
                    model,
                    logger));
            }

            return CreateTransportException(exception);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            return exception;
        }
        catch (MafProviderTransportException exception)
        {
            return CreateTransportException(exception);
        }
        catch (Exception exception)
        {
            return CreateTransportException(exception);
        }
    }

    private static async Task ObserveLateDisposalAsync(
        Task disposal,
        Guid providerProfileId,
        string model,
        ILogger logger)
    {
        try
        {
            await disposal.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Late provider stream disposal completed with failure type {FailureType}. ProviderProfileId={ProviderProfileId}, Model={Model}.",
                exception.GetType().FullName,
                providerProfileId,
                model);
        }
    }

    private async ValueTask<Exception?> CaptureDispatchLeaseDisposalFailureAsync(
        IAsyncDisposable dispatchLease)
    {
        try
        {
            await dispatchLease.DisposeAsync().ConfigureAwait(false);
            return null;
        }
        catch (Exception exception)
        {
            return CreateTransportException(exception);
        }
    }

    private static void ThrowPrimaryOrDisposalFailure(
        Exception? primaryFailure,
        Exception? disposalFailure,
        bool isProviderDisposal)
    {
        if (primaryFailure is not null)
        {
            AttachSecondaryFailure(
                primaryFailure,
                disposalFailure,
                DispatchLeaseDisposalFailureTypeDataKey,
                isProviderDisposal);
            ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        }

        if (disposalFailure is not null)
        {
            ExceptionDispatchInfo.Capture(disposalFailure).Throw();
        }
    }

    private static void AttachSecondaryFailure(
        Exception primaryFailure,
        Exception? secondaryFailure,
        string dataKey,
        bool isProviderDisposal)
    {
        if (secondaryFailure is null)
        {
            return;
        }

        primaryFailure.Data[dataKey] = isProviderDisposal
            ? MafProviderTransportException.ResolveDiagnosticFailureType(secondaryFailure)
            : secondaryFailure.GetType().FullName ?? secondaryFailure.GetType().Name;
    }

    private MafProviderTransportException CreateTransportException(
        Exception exception)
    {
        if (ProviderFailureDisclosurePolicy.RequiresSanitization(provider))
        {
            if (exception is MafProviderTransportException
                {
                    InnerException: ProviderFailureBoundaryException
                } sanitizedException)
            {
                return sanitizedException;
            }

            return new MafProviderTransportException(
                provider,
                model,
                ProviderFailureDisclosurePolicy.CreateBoundaryException(
                    provider,
                    ProviderFailureOperation.RuntimeRequest,
                    exception,
                    exception switch {
                        System.ClientModel.ClientResultException result => result.Status,
                        HttpRequestException { StatusCode: { } status } => (int)status,
                        _ => null
                    }));
        }

        return exception as MafProviderTransportException ??
            new MafProviderTransportException(provider, model, exception);
    }
}
