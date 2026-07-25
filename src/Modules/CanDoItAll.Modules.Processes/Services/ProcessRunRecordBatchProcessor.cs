using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRunRecordBatchProcessor(
    IProcessRunRecordStore store,
    ProcessRunRecordBackfillProcessor backfillProcessor,
    ProcessRunRecordAssembler assembler,
    IProcessRunNarrativeGenerator narrativeGenerator,
    TimeProvider timeProvider,
    IOptions<ProcessRunRecordProcessingOptions> options,
    ILogger<ProcessRunRecordBatchProcessor> logger)
{
    private readonly ProcessRunRecordProcessingOptions processingOptions = options.Value;

    public async Task<ProcessRunRecordProcessingResult> ProcessNextBatchAsync(
        CancellationToken cancellationToken = default)
    {
        if (!processingOptions.Enabled)
        {
            return ProcessRunRecordProcessingResult.Empty;
        }

        var backfillResult = await backfillProcessor
            .RunBatchAsync(processingOptions.BatchSize, cancellationToken)
            .ConfigureAwait(false);
        var factsCompleted = await ProcessFactsAsync(cancellationToken).ConfigureAwait(false);
        var narrativesCompleted = await ProcessNarrativesAsync(cancellationToken).ConfigureAwait(false);
        return new ProcessRunRecordProcessingResult(
            backfillResult.InsertedOrRevisedCount,
            factsCompleted,
            narrativesCompleted);
    }

    private async Task<int> ProcessFactsAsync(CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow();
        var claims = await store
            .ClaimFactsAsync(CreateClaimRequest(nowUtc), cancellationToken)
            .ConfigureAwait(false);
        var completedCount = 0;
        foreach (var claim in claims)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = await store
                .GetAsync(claim.RunId, includeSuperseded: false, cancellationToken)
                .ConfigureAwait(false);
            if (current is null || current.Summary.SourceGlobalSequence != claim.SourceGlobalSequence)
            {
                logger.LogWarning(
                    "Skipped stale process run facts claim. RunId={RunId} SourceGlobalSequence={SourceGlobalSequence}.",
                    claim.RunId,
                    claim.SourceGlobalSequence);
                continue;
            }

            try
            {
                var completion = await assembler
                    .AssembleAsync(claim, current, cancellationToken)
                    .ConfigureAwait(false);
                if (await store.CompleteFactsAsync(completion, cancellationToken).ConfigureAwait(false))
                {
                    completedCount++;
                }
                else
                {
                    logger.LogWarning(
                        "Process run facts completion lost its lease or source revision. RunId={RunId} SourceGlobalSequence={SourceGlobalSequence}.",
                        claim.RunId,
                        claim.SourceGlobalSequence);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await FailStageAsync(
                        claim.RunId,
                        claim.SourceGlobalSequence,
                        claim.ClaimToken,
                        claim.AttemptCount,
                        ProcessRunRecordStage.Facts,
                        exception.GetType().Name,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return completedCount;
    }

    private async Task<int> ProcessNarrativesAsync(CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow();
        var claims = await store
            .ClaimNarrativesAsync(CreateClaimRequest(nowUtc), cancellationToken)
            .ConfigureAwait(false);
        var completedCount = 0;
        foreach (var claim in claims)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = await store
                .GetAsync(claim.RunId, includeSuperseded: false, cancellationToken)
                .ConfigureAwait(false);
            if (current is null ||
                current.Summary.SourceGlobalSequence != claim.SourceGlobalSequence ||
                current.Summary.FactsStatus != ProcessRunFactsStatus.Completed)
            {
                logger.LogWarning(
                    "Skipped stale process run narrative claim. RunId={RunId} SourceGlobalSequence={SourceGlobalSequence}.",
                    claim.RunId,
                    claim.SourceGlobalSequence);
                continue;
            }

            try
            {
                var narrative = await narrativeGenerator
                    .GenerateAsync(current, cancellationToken)
                    .ConfigureAwait(false);
                var completion = new ProcessRunNarrativeCompletion(
                    claim.RunId,
                    claim.SourceGlobalSequence,
                    claim.ClaimToken,
                    narrative,
                    timeProvider.GetUtcNow());
                if (await store.CompleteNarrativeAsync(completion, cancellationToken).ConfigureAwait(false))
                {
                    completedCount++;
                }
                else
                {
                    logger.LogWarning(
                        "Process run narrative completion lost its lease or source revision. RunId={RunId} SourceGlobalSequence={SourceGlobalSequence}.",
                        claim.RunId,
                        claim.SourceGlobalSequence);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (ProcessRunNarrativeGenerationDeferredException exception)
            {
                await DeferNarrativeAsync(claim, exception, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                await FailStageAsync(
                        claim.RunId,
                        claim.SourceGlobalSequence,
                        claim.ClaimToken,
                        claim.AttemptCount,
                        ProcessRunRecordStage.Narrative,
                        exception.GetType().Name,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return completedCount;
    }

    private async Task DeferNarrativeAsync(
        ProcessRunNarrativeClaim claim,
        ProcessRunNarrativeGenerationDeferredException exception,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow();
        var retryAtUtc = nowUtc.Add(processingOptions.RetryBaseDelay);
        var diagnosticReference =
            $"process-run:{claim.RunId}:narrative:{claim.SourceGlobalSequence}";
        var deferred = await store
            .FailNarrativeAsync(
                new ProcessRunStageFailure(
                    claim.RunId,
                    claim.SourceGlobalSequence,
                    claim.ClaimToken,
                    exception.GetType().Name,
                    diagnosticReference,
                    nowUtc,
                    retryAtUtc,
                    ConsumesAttempt: false),
                cancellationToken)
            .ConfigureAwait(false);
        logger.LogInformation(
            "Process run narrative generation was deferred because the same-source execution is still active. RunId={RunId} SourceGlobalSequence={SourceGlobalSequence} ExecutionRunId={ExecutionRunId} ExecutionState={ExecutionState} RetryAtUtc={RetryAtUtc} DeferralRecorded={DeferralRecorded}.",
            claim.RunId,
            claim.SourceGlobalSequence,
            exception.ExecutionRunId,
            exception.ExecutionState,
            retryAtUtc,
            deferred);
    }

    private async Task FailStageAsync(
        ProcessRunId runId,
        long sourceGlobalSequence,
        ProcessRunRecordClaimToken claimToken,
        int attemptCount,
        ProcessRunRecordStage stage,
        string errorClass,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow();
        var diagnosticReference =
            $"process-run:{runId}:{stage.ToString().ToLowerInvariant()}:{sourceGlobalSequence}";
        var failure = new ProcessRunStageFailure(
            runId,
            sourceGlobalSequence,
            claimToken,
            errorClass,
            diagnosticReference,
            nowUtc,
            ResolveNextAttemptAtUtc(nowUtc, attemptCount),
            ConsumesAttempt: true);
        var recorded = stage switch
        {
            ProcessRunRecordStage.Facts =>
                await store.FailFactsAsync(failure, cancellationToken).ConfigureAwait(false),
            ProcessRunRecordStage.Narrative =>
                await store.FailNarrativeAsync(failure, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Process run record stage is not supported.")
        };
        logger.LogWarning(
            "Process run record stage failed. RunId={RunId} SourceGlobalSequence={SourceGlobalSequence} Stage={Stage} AttemptCount={AttemptCount} ErrorClass={ErrorClass} DiagnosticReference={DiagnosticReference} FailureRecorded={FailureRecorded}.",
            runId,
            sourceGlobalSequence,
            stage,
            attemptCount,
            errorClass,
            diagnosticReference,
            recorded);
    }

    private ProcessRunRecordClaimRequest CreateClaimRequest(DateTimeOffset nowUtc)
    {
        return new ProcessRunRecordClaimRequest(
            nowUtc,
            processingOptions.LeaseDuration,
            processingOptions.BatchSize);
    }

    private DateTimeOffset? ResolveNextAttemptAtUtc(DateTimeOffset nowUtc, int attemptCount)
    {
        if (attemptCount >= processingOptions.MaximumAttempts)
        {
            return null;
        }

        var exponent = Math.Clamp(attemptCount - 1, 0, 20);
        var multiplier = 1L << exponent;
        var maximumMultiplier = Math.Max(
            1,
            processingOptions.RetryMaximumDelay.Ticks / processingOptions.RetryBaseDelay.Ticks);
        var delayTicks = processingOptions.RetryBaseDelay.Ticks * Math.Min(multiplier, maximumMultiplier);
        return nowUtc.AddTicks(delayTicks);
    }
}

internal enum ProcessRunRecordStage
{
    Facts,
    Narrative
}

internal sealed record ProcessRunRecordProcessingResult(
    int BackfilledCount,
    int FactsCompletedCount,
    int NarrativesCompletedCount)
{
    public static ProcessRunRecordProcessingResult Empty { get; } = new(0, 0, 0);

    public int ProcessedCount => BackfilledCount + FactsCompletedCount + NarrativesCompletedCount;
}
