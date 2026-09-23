using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public delegate Task<ProviderBatchDispatchOutcome<TResult>> ProviderBatchItemDispatcher<TPayload, TResult>(
    ProviderBatchItemDispatchContext<TPayload> context,
    CancellationToken cancellationToken);

public sealed record ProviderBatchItemDispatchContext<TPayload>(
    ProviderBatchDispatchPlan Plan,
    ProviderBatchDispatchAssignment Assignment,
    ProviderBatchInput<TPayload> Input,
    ProviderRuntimeDispatchContext<TPayload> RuntimeContext) {
    public CanDoItAll.AgentFramework.ProviderHistory.HistoryInvocationContext History { get; init; } =
        ProviderBatchHistoryContext.Create(Plan.JobId, Input.InputId);
}

public interface IProviderBatchJobBalancer
{
    Task<ProviderBatchDispatchPlan> CreatePlanAsync<TPayload>(
        ProviderBatchJobRequest<TPayload> request,
        CancellationToken cancellationToken = default);

    Task<ProviderBatchJobResult<TResult>> ExecuteAsync<TPayload, TResult>(
        ProviderBatchJobRequest<TPayload> request,
        ProviderBatchItemDispatcher<TPayload, TResult> dispatchAsync,
        CancellationToken cancellationToken = default);
}

public sealed class ProviderBatchPlanningException(
    string message,
    IReadOnlyList<ProviderBatchProviderRejection> rejections) : InvalidOperationException(message)
{
    public IReadOnlyList<ProviderBatchProviderRejection> Rejections { get; } = rejections;
}

public sealed class ProviderBatchJobBalancer : IProviderBatchJobBalancer
{
    private static readonly IProviderBatchJobCheckpointStore NoCheckpointStore = new NullProviderBatchJobCheckpointStore();

    private readonly IProviderRuntimePool runtimePool;
    private readonly IProviderBatchJobCheckpointStore checkpointStore;

    public ProviderBatchJobBalancer(IProviderRuntimePool runtimePool)
        : this(runtimePool, NoCheckpointStore)
    {
    }

    public ProviderBatchJobBalancer(
        IProviderRuntimePool runtimePool,
        IProviderBatchJobCheckpointStore checkpointStore)
    {
        this.runtimePool = runtimePool ?? throw new ArgumentNullException(nameof(runtimePool));
        this.checkpointStore = checkpointStore ?? throw new ArgumentNullException(nameof(checkpointStore));
    }

    public async Task<ProviderBatchDispatchPlan> CreatePlanAsync<TPayload>(
        ProviderBatchJobRequest<TPayload> request,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = ValidateRequest(request);
        var policy = ValidatePolicy(normalizedRequest.Policy);

        return await CreatePlanAsync(normalizedRequest, policy, new HashSet<Guid>(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProviderBatchJobResult<TResult>> ExecuteAsync<TPayload, TResult>(
        ProviderBatchJobRequest<TPayload> request,
        ProviderBatchItemDispatcher<TPayload, TResult> dispatchAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dispatchAsync);

        var normalizedRequest = ValidateRequest(request);
        var policy = ValidatePolicy(normalizedRequest.Policy);
        var checkpoints = await LoadCheckpointsAsync(normalizedRequest.JobId, policy, cancellationToken).ConfigureAwait(false);
        var completedByInputId = checkpoints
            .Where(checkpoint => checkpoint.Status == ProviderBatchItemStatus.Succeeded)
            .ToDictionary(checkpoint => checkpoint.InputId);
        var completedIds = completedByInputId.Keys.ToHashSet();
        var pendingInputs = normalizedRequest.Inputs
            .Where(input => !completedIds.Contains(input.InputId))
            .ToList();
        var pendingRequest = normalizedRequest with { Inputs = pendingInputs };
        var plan = await CreatePlanAsync(pendingRequest, policy, completedIds, cancellationToken).ConfigureAwait(false);
        var recoveredResults = normalizedRequest.Inputs
            .Where(input => completedByInputId.ContainsKey(input.InputId))
            .Select(input => ProviderBatchJobItemResult<TResult>.Recovered(
                BoxInput(input),
                completedByInputId[input.InputId]))
            .ToList();

        if (pendingInputs.Count == 0)
        {
            return new ProviderBatchJobResult<TResult>(
                normalizedRequest.JobId,
                plan,
                recoveredResults.OrderBy(item => item.Sequence).ToList(),
                plan.Rejections);
        }

        using var failFastCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var totalGate = new SemaphoreSlim(policy.MaxTotalParallelism, policy.MaxTotalParallelism);
        var laneGates = plan.Lanes.ToDictionary(
            lane => lane.LaneKey,
            lane => new SemaphoreSlim(lane.PlannedParallelism, lane.PlannedParallelism));

        try
        {
            var selectionsByProviderId = normalizedRequest.Providers.ToDictionary(selection => selection.Provider.Id);
            var inputsById = pendingInputs.ToDictionary(input => input.InputId);
            var executionTasks = plan.Assignments
                .Select(assignment => ExecuteAssignmentAsync(
                    normalizedRequest,
                    policy,
                    plan,
                    assignment,
                    inputsById[assignment.InputId],
                    selectionsByProviderId[assignment.ProviderProfileId],
                    totalGate,
                    laneGates[assignment.LaneKey],
                    dispatchAsync,
                    failFastCancellation))
                .ToArray();
            var executedResults = await Task.WhenAll(executionTasks).ConfigureAwait(false);
            var allResults = recoveredResults
                .Concat(executedResults)
                .OrderBy(item => item.Sequence)
                .ToList();

            return new ProviderBatchJobResult<TResult>(
                normalizedRequest.JobId,
                plan,
                allResults,
                plan.Rejections);
        }
        finally
        {
            foreach (var gate in laneGates.Values)
            {
                gate.Dispose();
            }
        }
    }

    private async Task<ProviderBatchDispatchPlan> CreatePlanAsync<TPayload>(
        ProviderBatchJobRequest<TPayload> request,
        ProviderBatchExecutionPolicy policy,
        IReadOnlySet<Guid> completedInputIds,
        CancellationToken cancellationToken)
    {
        var lanes = new List<ProviderBatchDispatchLane>();
        var rejections = new List<ProviderBatchProviderRejection>();

        foreach (var selection in request.Providers.OrderBy(selection => selection.Provider.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryCreateStaticLane(selection, request, policy, out var laneInput, out var rejection))
            {
                rejections.Add(rejection);
                continue;
            }

            var handle = await runtimePool.GetRequiredAsync(selection.Provider.Id, cancellationToken).ConfigureAwait(false);
            if (handle.Descriptor.ProviderKind != selection.Provider.Kind)
            {
                rejections.Add(CreateRejection(
                    selection.Provider,
                    ProviderBatchRejectionCodes.RuntimeMismatch,
                    "Provider runtime descriptor kind does not match the selected provider kind."));
                continue;
            }

            if (!handle.ProviderFactory.Supports(selection.Provider.Kind, request.Capability))
            {
                rejections.Add(CreateRejection(
                    selection.Provider,
                    ProviderBatchRejectionCodes.CapabilityUnsupported,
                    $"Provider does not support capability '{request.Capability}'."));
                continue;
            }

            var query = new ProviderDispatchQuery(
                selection.Provider,
                request.Capability,
                request.Operation,
                laneInput.Model);
            var limits = handle.ProviderFactory.GetDispatchLimits(query);
            lanes.Add(CreateLane(selection.Provider, laneInput.Model, limits, policy, selection.MaxParallelism));
        }

        if (request.Inputs.Count > 0 && lanes.Count == 0)
        {
            throw new ProviderBatchPlanningException(
                "No eligible provider profiles are available for the provider batch job.",
                rejections);
        }

        var assignments = AssignInputs(request, lanes, completedInputIds);
        return new ProviderBatchDispatchPlan(
            request.JobId,
            request.Capability,
            request.Operation,
            request.Inputs.Count,
            lanes,
            assignments,
            rejections);
    }

    private async Task<ProviderBatchJobItemResult<TResult>> ExecuteAssignmentAsync<TPayload, TResult>(
        ProviderBatchJobRequest<TPayload> request,
        ProviderBatchExecutionPolicy policy,
        ProviderBatchDispatchPlan plan,
        ProviderBatchDispatchAssignment assignment,
        ProviderBatchInput<TPayload> input,
        ProviderBatchProviderSelection selection,
        SemaphoreSlim totalGate,
        SemaphoreSlim laneGate,
        ProviderBatchItemDispatcher<TPayload, TResult> dispatchAsync,
        CancellationTokenSource failFastCancellation)
    {
        var cancellationToken = failFastCancellation.Token;
        var boxedInput = BoxInput(input);

        try
        {
            await totalGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            await laneGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            return await RecordCancelledAsync<TResult>(request.JobId, assignment, boxedInput, 0, exception.Message, CancellationToken.None)
                .ConfigureAwait(false);
        }

        try
        {
            return await ExecuteWithRetryAsync(
                request,
                policy,
                plan,
                assignment,
                input,
                boxedInput,
                selection,
                dispatchAsync,
                failFastCancellation).ConfigureAwait(false);
        }
        finally
        {
            laneGate.Release();
            totalGate.Release();
        }
    }

    private async Task<ProviderBatchJobItemResult<TResult>> ExecuteWithRetryAsync<TPayload, TResult>(
        ProviderBatchJobRequest<TPayload> request,
        ProviderBatchExecutionPolicy policy,
        ProviderBatchDispatchPlan plan,
        ProviderBatchDispatchAssignment assignment,
        ProviderBatchInput<TPayload> input,
        ProviderBatchInput<object?> boxedInput,
        ProviderBatchProviderSelection selection,
        ProviderBatchItemDispatcher<TPayload, TResult> dispatchAsync,
        CancellationTokenSource failFastCancellation)
    {
        var attempt = 0;
        Exception? lastException = null;
        var history = ProviderBatchHistoryContext.Create(request.JobId, input.InputId);

        while (attempt < policy.MaxAttempts)
        {
            attempt++;
            var cancellationToken = failFastCancellation.Token;
            await RecordCheckpointAsync(
                CreateCheckpoint(
                    request.JobId,
                    assignment,
                    ProviderBatchItemStatus.Running,
                    attempt,
                    string.Empty,
                    string.Empty,
                    string.Empty),
                cancellationToken).ConfigureAwait(false);

            try
            {
                var handle = await runtimePool.GetRequiredAsync(assignment.ProviderProfileId, cancellationToken).ConfigureAwait(false);
                var query = new ProviderDispatchQuery(
                    selection.Provider,
                    request.Capability,
                    request.Operation,
                    assignment.Model);
                var outcome = await handle.DispatchAsync(
                    new ProviderRuntimeDispatchRequest<TPayload>(query, input.Payload, input.InputId),
                    (runtimeContext, token) =>
                    {
                        var context = new ProviderBatchItemDispatchContext<TPayload>(
                            plan,
                            assignment,
                            input,
                            runtimeContext) { History = history };
                        return dispatchAsync(context, token);
                    },
                    cancellationToken).ConfigureAwait(false);
                await RecordCheckpointAsync(
                    CreateCheckpoint(
                        request.JobId,
                        assignment,
                        ProviderBatchItemStatus.Succeeded,
                        attempt,
                        outcome.ResultReference,
                        string.Empty,
                        string.Empty),
                    cancellationToken).ConfigureAwait(false);

                return ProviderBatchJobItemResult<TResult>.Succeeded(boxedInput, assignment, attempt, outcome);
            }
            catch (OperationCanceledException exception)
            {
                return await RecordCancelledAsync<TResult>(request.JobId, assignment, boxedInput, attempt, exception.Message, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                lastException = exception;
                await RecordCheckpointAsync(
                    CreateCheckpoint(
                        request.JobId,
                        assignment,
                        ProviderBatchItemStatus.Failed,
                        attempt,
                        string.Empty,
                        exception.GetType().Name,
                        exception.Message),
                    CancellationToken.None).ConfigureAwait(false);

                if (!ProviderBatchRetryPolicy.CanRetry(exception))
                {
                    break;
                }
                if (attempt < policy.MaxAttempts)
                {
                    continue;
                }
            }
        }

        var errorCode = lastException?.GetType().Name ?? "ProviderBatch.UnknownFailure";
        var errorMessage = lastException?.Message ?? "Provider batch item failed.";
        if (policy.FailurePolicy == ProviderBatchFailurePolicy.FailFast)
        {
            await failFastCancellation.CancelAsync().ConfigureAwait(false);
        }

        return ProviderBatchJobItemResult<TResult>.Failed(boxedInput, assignment, attempt, errorCode, errorMessage);
    }

    private async Task<ProviderBatchJobItemResult<TResult>> RecordCancelledAsync<TResult>(
        Guid jobId,
        ProviderBatchDispatchAssignment assignment,
        ProviderBatchInput<object?> input,
        int attempt,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        await RecordCheckpointAsync(
            CreateCheckpoint(
                jobId,
                assignment,
                ProviderBatchItemStatus.Cancelled,
                attempt,
                string.Empty,
                "ProviderBatch.Cancelled",
                errorMessage),
            cancellationToken).ConfigureAwait(false);

        return ProviderBatchJobItemResult<TResult>.Cancelled(input, assignment, attempt, errorMessage);
    }

    private async Task<IReadOnlyList<ProviderBatchItemCheckpoint>> LoadCheckpointsAsync(
        Guid jobId,
        ProviderBatchExecutionPolicy policy,
        CancellationToken cancellationToken)
    {
        if (policy.PersistenceMode != ProviderBatchPersistenceMode.Checkpointed)
        {
            return [];
        }

        if (ReferenceEquals(checkpointStore, NoCheckpointStore))
        {
            throw new InvalidOperationException("Checkpointed provider batch jobs require an IProviderBatchJobCheckpointStore implementation.");
        }

        return await checkpointStore.GetItemCheckpointsAsync(jobId, cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordCheckpointAsync(
        ProviderBatchItemCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        if (ReferenceEquals(checkpointStore, NoCheckpointStore))
        {
            return;
        }

        await checkpointStore.UpsertItemCheckpointAsync(checkpoint, cancellationToken).ConfigureAwait(false);
    }

    private static ProviderBatchJobRequest<TPayload> ValidateRequest<TPayload>(
        ProviderBatchJobRequest<TPayload> request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.JobId == Guid.Empty)
        {
            throw new ArgumentException("Provider batch job id is required.", nameof(request));
        }

        if (request.Inputs is null || request.Inputs.Count == 0)
        {
            throw new ArgumentException("Provider batch job requires at least one input.", nameof(request));
        }

        if (request.Providers is null || request.Providers.Count == 0)
        {
            throw new ArgumentException("Provider batch job requires at least one provider selection.", nameof(request));
        }

        var duplicateInput = request.Inputs
            .GroupBy(input => input.InputId)
            .FirstOrDefault(group => group.Key == Guid.Empty || group.Count() > 1);
        if (duplicateInput is not null)
        {
            throw new ArgumentException("Provider batch inputs require unique non-empty input ids.", nameof(request));
        }

        var duplicateSequence = request.Inputs
            .GroupBy(input => input.Sequence)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSequence is not null)
        {
            throw new ArgumentException("Provider batch inputs require unique sequence values.", nameof(request));
        }

        return request;
    }

    private static ProviderBatchExecutionPolicy ValidatePolicy(
        ProviderBatchExecutionPolicy? policy)
    {
        var value = policy ?? new ProviderBatchExecutionPolicy();
        if (value.MaxTotalParallelism < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(policy), "Provider batch max total parallelism must be at least one.");
        }

        if (value.MaxPerProviderParallelism < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(policy), "Provider batch max per-provider parallelism must be at least one.");
        }

        if (value.MaxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(policy), "Provider batch max attempts must be at least one.");
        }

        return value;
    }

    private static bool TryCreateStaticLane<TPayload>(
        ProviderBatchProviderSelection selection,
        ProviderBatchJobRequest<TPayload> request,
        ProviderBatchExecutionPolicy policy,
        out ProviderBatchLaneInput laneInput,
        out ProviderBatchProviderRejection rejection)
    {
        var provider = selection.Provider;
        laneInput = default;
        rejection = default!;

        if (!provider.IsEnabled)
        {
            rejection = CreateRejection(
                provider,
                ProviderBatchRejectionCodes.ProviderDisabled,
                "Provider profile is disabled.");
            return false;
        }

        if (selection.RequireHealthy && !IsHealthy(provider.HealthStatus))
        {
            rejection = CreateRejection(
                provider,
                ProviderBatchRejectionCodes.ProviderUnhealthy,
                $"Provider health status '{provider.HealthStatus}' is not eligible.");
            return false;
        }

        var model = ResolveModel(selection, request.Model);
        if (string.IsNullOrWhiteSpace(model))
        {
            rejection = CreateRejection(
                provider,
                ProviderBatchRejectionCodes.ModelMissing,
                "No model was selected for this provider.");
            return false;
        }

        if (!IsModelCompatible(provider, model))
        {
            rejection = CreateRejection(
                provider,
                ProviderBatchRejectionCodes.ModelMismatch,
                $"Model '{model}' is not listed for provider '{provider.Name}'.");
            return false;
        }

        laneInput = new ProviderBatchLaneInput(model, policy.MaxPerProviderParallelism);
        return true;
    }

    private static ProviderBatchDispatchLane CreateLane(
        ProviderProfile provider,
        string model,
        ProviderDispatchLimits limits,
        ProviderBatchExecutionPolicy policy,
        int? selectionMaxParallelism)
    {
        var limitParallelism = limits.SupportsBatching
            ? Math.Max(1, limits.MaxBatchSize * limits.MaxInFlightBatches)
            : Math.Max(1, limits.MaxInFlightBatches);
        var requestedParallelism = selectionMaxParallelism is > 0
            ? Math.Min(policy.MaxPerProviderParallelism, selectionMaxParallelism.Value)
            : policy.MaxPerProviderParallelism;
        var plannedParallelism = Math.Max(1, Math.Min(requestedParallelism, limitParallelism));
        var laneKey = string.Join(
            "|",
            provider.Id.ToString("N"),
            provider.Kind,
            model,
            limits.SupportsBatching ? "batched" : "direct");

        return new ProviderBatchDispatchLane(
            laneKey,
            provider.Id,
            provider.Kind,
            provider.Name,
            model,
            limits,
            plannedParallelism);
    }

    private static IReadOnlyList<ProviderBatchDispatchAssignment> AssignInputs<TPayload>(
        ProviderBatchJobRequest<TPayload> request,
        IReadOnlyList<ProviderBatchDispatchLane> lanes,
        IReadOnlySet<Guid> completedInputIds)
    {
        var assignmentCounts = lanes.ToDictionary(lane => lane.LaneKey, _ => 0);
        var assignments = new List<ProviderBatchDispatchAssignment>();

        foreach (var input in request.Inputs
                     .Where(input => !completedInputIds.Contains(input.InputId))
                     .OrderBy(input => input.Sequence))
        {
            var lane = lanes
                .OrderBy(lane => (double)assignmentCounts[lane.LaneKey] / lane.PlannedParallelism)
                .ThenBy(lane => lane.ProviderName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(lane => lane.ProviderProfileId)
                .First();
            assignmentCounts[lane.LaneKey]++;
            assignments.Add(new ProviderBatchDispatchAssignment(
                input.InputId,
                input.Sequence,
                input.SourceReference,
                lane.LaneKey,
                lane.ProviderProfileId,
                lane.ProviderKind,
                lane.ProviderName,
                lane.Model,
                PlannedAttempt: 1));
        }

        return assignments;
    }

    private static ProviderBatchItemCheckpoint CreateCheckpoint(
        Guid jobId,
        ProviderBatchDispatchAssignment assignment,
        ProviderBatchItemStatus status,
        int attemptCount,
        string resultReference,
        string errorCode,
        string errorMessage)
    {
        return new ProviderBatchItemCheckpoint(
            jobId,
            assignment.InputId,
            assignment.Sequence,
            status,
            assignment.ProviderProfileId,
            assignment.ProviderKind,
            assignment.ProviderName,
            assignment.Model,
            attemptCount,
            resultReference,
            errorCode,
            errorMessage,
            DateTimeOffset.UtcNow);
    }

    private static ProviderBatchProviderRejection CreateRejection(
        ProviderProfile provider,
        string reasonCode,
        string message)
    {
        return new ProviderBatchProviderRejection(
            provider.Id,
            provider.Name,
            provider.Kind,
            reasonCode,
            message);
    }

    private static ProviderBatchInput<object?> BoxInput<TPayload>(
        ProviderBatchInput<TPayload> input)
    {
        return new ProviderBatchInput<object?>(
            input.InputId,
            input.Sequence,
            input.SourceReference,
            input.Payload);
    }

    private static string ResolveModel(
        ProviderBatchProviderSelection selection,
        string requestModel)
    {
        if (!string.IsNullOrWhiteSpace(selection.Model))
        {
            return selection.Model.Trim();
        }

        if (!string.IsNullOrWhiteSpace(requestModel))
        {
            return requestModel.Trim();
        }

        if (!string.IsNullOrWhiteSpace(selection.Provider.DefaultModel))
        {
            return selection.Provider.DefaultModel.Trim();
        }

        return string.Empty;
    }

    private static bool IsModelCompatible(
        ProviderProfile provider,
        string model)
    {
        return provider.SuggestedModels.Count == 0 ||
               provider.SuggestedModels.Any(candidate => string.Equals(candidate, model, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsHealthy(string healthStatus)
    {
        return string.Equals(healthStatus, "Healthy", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(healthStatus, "OK", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(healthStatus, "Available", StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct ProviderBatchLaneInput(
        string Model,
        int MaxPerProviderParallelism);

    private sealed class NullProviderBatchJobCheckpointStore : IProviderBatchJobCheckpointStore
    {
        public Task<IReadOnlyList<ProviderBatchItemCheckpoint>> GetItemCheckpointsAsync(
            Guid jobId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProviderBatchItemCheckpoint>>([]);
        }

        public Task UpsertItemCheckpointAsync(
            ProviderBatchItemCheckpoint checkpoint,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
