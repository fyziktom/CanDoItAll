using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowExecutorInvoker(
    IWorkflowExecutorCatalog catalog,
    IEnumerable<IWorkflowExecutor> executors,
    IWorkflowExecutorExecutionObserver? executionObserver = null,
    IWorkflowExecutorApprovalGate? approvalGate = null) : IWorkflowExecutorInvoker
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

        if (node.Settings.ExecutorId is not { } executorId)
        {
            throw WorkflowExecutorFailureDiagnosticMapper.CreateMissingExecutorIdException(definition, node);
        }

        if (!catalog.TryGetExecutor(executorId, out var descriptor))
        {
            throw WorkflowExecutorFailureDiagnosticMapper.CreateExecutorNotRegisteredException(definition, node, executorId);
        }

        if (!descriptor.CanExecute)
        {
            throw WorkflowExecutorFailureDiagnosticMapper.CreateUnavailableException(definition, node, descriptor);
        }

        if (!executorsById.TryGetValue(executorId, out var executor))
        {
            throw WorkflowExecutorFailureDiagnosticMapper.CreateMissingImplementationException(definition, node, descriptor);
        }

        var policy = node.Settings.ExecutionPolicy ?? descriptor.DefaultPolicy;
        WorkflowExecutorPolicyLimits.ThrowIfInvalid(policy, node.Id, executorId);
        WorkflowExecutorSideEffectPolicy.ThrowIfUnsafeRetryPolicy(descriptor, policy, node.Id);
        var settingsJson = string.IsNullOrWhiteSpace(node.Settings.ExecutorSettingsJson)
            ? descriptor.DefaultSettingsJson
            : node.Settings.ExecutorSettingsJson;
        var redactedSettingsSummary = WorkflowExecutorRedaction.RedactSettingsJson(settingsJson);
        var pluginConnectionId = WorkflowExecutorRedaction.ReadStringProperty(settingsJson, "connectionId");
        await EnforceApprovalPolicyAsync(
            definition,
            node,
            descriptor,
            redactedSettingsSummary,
            cancellationToken);
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
                lastException = WorkflowExecutorFailureDiagnosticMapper.Attach(
                    exception,
                    WorkflowExecutorFailureDiagnosticMapper.FromExecutionFailure(definition, node, descriptor, exception, policy));
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
                var timeoutException = new TimeoutException($"Workflow executor '{executorId}' timed out after {policy.TimeoutSeconds} second(s) on node '{node.Id}'.", exception);
                lastException = WorkflowExecutorFailureDiagnosticMapper.Attach(
                    timeoutException,
                    WorkflowExecutorFailureDiagnosticMapper.FromExecutionFailure(definition, node, descriptor, timeoutException, policy));
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
                lastException = WorkflowExecutorFailureDiagnosticMapper.Attach(
                    WorkflowExecutorRedaction.SanitizeException(exception)!,
                    WorkflowExecutorFailureDiagnosticMapper.FromExecutionFailure(definition, node, descriptor, exception, policy));
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

        throw WorkflowExecutorFailureDiagnosticMapper.CreateInvocationException(
            definition,
            node,
            descriptor,
            policy,
            lastException);
    }

    private async ValueTask EnforceApprovalPolicyAsync(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowExecutorDescriptor descriptor,
        string redactedSettingsSummary,
        CancellationToken cancellationToken)
    {
        if (!descriptor.PermissionPolicy.RequiresApproval)
        {
            return;
        }

        if (approvalGate is null)
        {
            throw WorkflowExecutorFailureDiagnosticMapper.CreateApprovalGateMissingException(definition, node, descriptor);
        }

        var decision = await approvalGate.RequestApprovalAsync(
            new WorkflowExecutorApprovalRequest(
                definition,
                node,
                descriptor,
                redactedSettingsSummary),
            cancellationToken);
        if (!decision.Approved)
        {
            throw WorkflowExecutorFailureDiagnosticMapper.CreateApprovalDeniedException(
                definition,
                node,
                descriptor,
                decision.Message);
        }
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
            context.Descriptor.Source.PackageId,
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
