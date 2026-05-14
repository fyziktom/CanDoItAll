using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IWorkflowExecutorCatalog
{
    IReadOnlyList<WorkflowExecutorDescriptor> ListExecutors();

    bool TryGetExecutor(WorkflowExecutorId executorId, out WorkflowExecutorDescriptor descriptor);

    WorkflowExecutorDescriptor GetRequiredExecutor(WorkflowExecutorId executorId);
}

public interface IWorkflowExecutor
{
    WorkflowExecutorDescriptor Descriptor { get; }

    ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowExecutorInvoker
{
    ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowLlmComponentInvoker
{
    ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowDefinition definition,
        WorkflowNode node,
        LlmCallComponent component,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowExecutorExecutionContext(
    WorkflowDefinition Definition,
    WorkflowNode Node,
    WorkflowExecutorDescriptor Descriptor,
    string SettingsJson,
    WorkflowExecutorExecutionPolicy Policy)
{
    public WorkflowRunId? RunId { get; init; }

    public string PluginConnectionId { get; init; } = string.Empty;

    public string RedactedSettingsSummary { get; init; } = string.Empty;
}

public sealed class WorkflowExecutorCatalog : IWorkflowExecutorCatalog
{
    private readonly IReadOnlyList<WorkflowExecutorDescriptor> descriptors;
    private readonly IReadOnlyDictionary<WorkflowExecutorId, WorkflowExecutorDescriptor> descriptorsById;

    public WorkflowExecutorCatalog(IEnumerable<IWorkflowExecutor> executors)
    {
        ArgumentNullException.ThrowIfNull(executors);

        var resolvedDescriptors = executors
            .Select(executor => executor.Descriptor)
            .ToArray();
        var duplicateIds = resolvedDescriptors
            .GroupBy(descriptor => descriptor.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.Value)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException($"Workflow executor catalog contains duplicate executor id(s): {string.Join(", ", duplicateIds)}.");
        }

        descriptors = resolvedDescriptors
            .OrderBy(descriptor => descriptor.Category)
            .ThenBy(descriptor => descriptor.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        descriptorsById = resolvedDescriptors.ToDictionary(descriptor => descriptor.Id);
    }

    public IReadOnlyList<WorkflowExecutorDescriptor> ListExecutors() => descriptors;

    public bool TryGetExecutor(WorkflowExecutorId executorId, out WorkflowExecutorDescriptor descriptor)
        => descriptorsById.TryGetValue(executorId, out descriptor!);

    public WorkflowExecutorDescriptor GetRequiredExecutor(WorkflowExecutorId executorId)
    {
        if (TryGetExecutor(executorId, out var descriptor))
        {
            return descriptor;
        }

        throw new InvalidOperationException($"Workflow executor '{executorId}' is not registered.");
    }
}

public sealed class WorkflowExecutorInvoker(
    IWorkflowExecutorCatalog catalog,
    IEnumerable<IWorkflowExecutor> executors,
    IWorkflowExecutorExecutionObserver? executionObserver = null) : IWorkflowExecutorInvoker
{
    private readonly IReadOnlyDictionary<WorkflowExecutorId, IWorkflowExecutor> executorsById = BuildExecutorsById(executors);
    private readonly IWorkflowExecutorExecutionObserver executionObserver = executionObserver ?? new NullWorkflowExecutorExecutionObserver();

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(input);

        var executorId = node.Settings.ExecutorId
            ?? throw new InvalidOperationException($"Workflow executor node '{node.Id}' does not specify an executor id.");

        var descriptor = catalog.GetRequiredExecutor(executorId);
        if (!descriptor.CanExecute)
        {
            throw new WorkflowExecutorUnavailableException(
                node.Id,
                executorId,
                descriptor.Availability,
                $"Workflow executor '{executorId}' is not runnable for node '{node.Id}': {descriptor.Availability.Message}");
        }

        if (!executorsById.TryGetValue(executorId, out var executor))
        {
            throw new InvalidOperationException($"Workflow executor '{executorId}' has a descriptor but no implementation.");
        }

        var policy = node.Settings.ExecutionPolicy ?? descriptor.DefaultPolicy;
        WorkflowExecutorPolicyLimits.ThrowIfInvalid(policy, node.Id, executorId);
        var settingsJson = string.IsNullOrWhiteSpace(node.Settings.ExecutorSettingsJson)
            ? descriptor.DefaultSettingsJson
            : node.Settings.ExecutorSettingsJson;
        var redactedSettingsSummary = WorkflowExecutorRedaction.RedactSettingsJson(settingsJson);
        var pluginConnectionId = WorkflowExecutorRedaction.ReadStringProperty(settingsJson, "connectionId");
        var context = new WorkflowExecutorExecutionContext(
            definition,
            node,
            descriptor,
            settingsJson,
            policy)
        {
            RunId = WorkflowExecutorExecutionAuditScope.CurrentRunId,
            PluginConnectionId = pluginConnectionId,
            RedactedSettingsSummary = redactedSettingsSummary
        };

        Exception? lastException = null;
        var maxAttemptIndex = policy.MaxRetryAttempts;
        for (var attemptIndex = 0; attemptIndex <= maxAttemptIndex; attemptIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(policy.TimeoutSeconds));
            await RecordExecutionAuditAsync(
                context,
                WorkflowExecutorExecutionAuditStatus.Started,
                attemptIndex,
                "Workflow executor invocation started.",
                payloadCharacters: null,
                cancellationToken);

            try
            {
                var result = await executor.ExecuteAsync(context, input, timeoutSource.Token);
                if (result.NodeId != node.Id)
                {
                        throw new InvalidOperationException($"Workflow executor '{executorId}' returned result for node '{result.NodeId}' while executing node '{node.Id}'.");
                }

                WorkflowExecutorPayloadPolicy.ThrowIfPluginPayloadTooLarge(descriptor, node.Id, result.PayloadJson);
                await RecordExecutionAuditAsync(
                    context,
                    WorkflowExecutorExecutionAuditStatus.Completed,
                    attemptIndex,
                    "Workflow executor invocation completed.",
                    result.PayloadJson.Length,
                    cancellationToken);
                return result;
            }
            catch (WorkflowExecutorPayloadTooLargeException exception)
            {
                lastException = exception;
                await RecordExecutionAuditAsync(
                    context,
                    WorkflowExecutorExecutionAuditStatus.Failed,
                    attemptIndex,
                    exception.Message,
                    exception.PayloadCharacters,
                    cancellationToken);
                break;
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested)
            {
                lastException = new TimeoutException($"Workflow executor '{executorId}' timed out after {policy.TimeoutSeconds} second(s) on node '{node.Id}'.", exception);
                await RecordExecutionAuditAsync(
                    context,
                    WorkflowExecutorExecutionAuditStatus.Failed,
                    attemptIndex,
                    lastException.Message,
                    payloadCharacters: null,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not WorkflowExecutorInvocationException)
            {
                lastException = WorkflowExecutorRedaction.SanitizeException(exception);
                await RecordExecutionAuditAsync(
                    context,
                    WorkflowExecutorExecutionAuditStatus.Failed,
                    attemptIndex,
                    lastException?.Message ?? "Workflow executor invocation failed.",
                    payloadCharacters: null,
                    cancellationToken);
            }

            if (attemptIndex >= maxAttemptIndex)
            {
                break;
            }

            if (policy.RetryDelayMilliseconds > 0)
            {
                await Task.Delay(policy.RetryDelayMilliseconds, cancellationToken);
            }
        }

        throw new WorkflowExecutorInvocationException(
            node.Id,
            executorId,
            policy.MaxRetryAttempts + 1,
            policy.TimeoutSeconds,
            $"Workflow executor '{executorId}' failed on node '{node.Id}' after {policy.MaxRetryAttempts + 1} attempt(s).",
            lastException);
    }

    private async ValueTask RecordExecutionAuditAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowExecutorExecutionAuditStatus status,
        int attemptIndex,
        string message,
        int? payloadCharacters,
        CancellationToken cancellationToken)
    {
        var record = new WorkflowExecutorExecutionAuditRecord(
            context.Definition.Id,
            context.Definition.VersionId,
            context.RunId,
            context.Node.Id,
            context.Descriptor.Id,
            context.Descriptor.Source.Kind,
            context.Descriptor.Source.PluginId,
            context.PluginConnectionId,
            status,
            attemptIndex + 1,
            context.Policy.MaxRetryAttempts + 1,
            context.Policy.TimeoutSeconds,
            context.Policy.CaptureOutputArtifact,
            context.RedactedSettingsSummary,
            WorkflowExecutorRedaction.RedactText(message),
            payloadCharacters,
            DateTimeOffset.UtcNow);

        await executionObserver.RecordAsync(record, cancellationToken);
    }

    private static IReadOnlyDictionary<WorkflowExecutorId, IWorkflowExecutor> BuildExecutorsById(
        IEnumerable<IWorkflowExecutor> executors)
    {
        ArgumentNullException.ThrowIfNull(executors);

        var groupedExecutors = executors
            .GroupBy(executor => executor.Descriptor.Id)
            .ToArray();
        var duplicateIds = groupedExecutors
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.Value)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException($"Workflow executor invoker contains duplicate executor implementation id(s): {string.Join(", ", duplicateIds)}.");
        }

        return groupedExecutors.ToDictionary(group => group.Key, group => group.Single());
    }
}

public sealed class WorkflowExecutorInvocationException : InvalidOperationException
{
    public WorkflowExecutorInvocationException(
        WorkflowNodeId nodeId,
        WorkflowExecutorId executorId,
        int attemptCount,
        int timeoutSeconds,
        string message,
        Exception? innerException)
        : base(message, innerException)
    {
        NodeId = nodeId;
        ExecutorId = executorId;
        AttemptCount = attemptCount;
        TimeoutSeconds = timeoutSeconds;
    }

    public WorkflowNodeId NodeId { get; }

    public WorkflowExecutorId ExecutorId { get; }

    public int AttemptCount { get; }

    public int TimeoutSeconds { get; }
}

public sealed class WorkflowExecutorUnavailableException : InvalidOperationException
{
    public WorkflowExecutorUnavailableException(
        WorkflowNodeId nodeId,
        WorkflowExecutorId executorId,
        WorkflowExecutorAvailabilityDescriptor availability,
        string message)
        : base(message)
    {
        NodeId = nodeId;
        ExecutorId = executorId;
        Availability = availability;
    }

    public WorkflowNodeId NodeId { get; }

    public WorkflowExecutorId ExecutorId { get; }

    public WorkflowExecutorAvailabilityDescriptor Availability { get; }
}

public static class WorkflowExecutorPolicyLimits
{
    public const int MinTimeoutSeconds = 1;
    public const int MaxTimeoutSeconds = 3600;
    public const int MinRetryAttempts = 0;
    public const int MaxRetryAttempts = 10;
    public const int MinRetryDelayMilliseconds = 0;
    public const int MaxRetryDelayMilliseconds = 600000;

    public static bool IsValid(WorkflowExecutorExecutionPolicy policy)
        => policy.TimeoutSeconds is >= MinTimeoutSeconds and <= MaxTimeoutSeconds &&
           policy.MaxRetryAttempts is >= MinRetryAttempts and <= MaxRetryAttempts &&
           policy.RetryDelayMilliseconds is >= MinRetryDelayMilliseconds and <= MaxRetryDelayMilliseconds;

    public static void ThrowIfInvalid(
        WorkflowExecutorExecutionPolicy policy,
        WorkflowNodeId nodeId,
        WorkflowExecutorId executorId)
    {
        if (IsValid(policy))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Workflow executor policy is invalid for executor '{executorId}' on node '{nodeId}'. " +
            $"TimeoutSeconds must be {MinTimeoutSeconds}-{MaxTimeoutSeconds}, MaxRetryAttempts must be {MinRetryAttempts}-{MaxRetryAttempts}, " +
            $"and RetryDelayMilliseconds must be {MinRetryDelayMilliseconds}-{MaxRetryDelayMilliseconds}.");
    }
}
