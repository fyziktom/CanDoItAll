using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.AgentFramework.Memory;

public sealed class MemoryWorkflowExecutor : IWorkflowExecutor
{
    private readonly MemoryWorkflowQueryOperation query;
    private readonly MemoryWorkflowStatusOperation status;

    public MemoryWorkflowExecutor(
        IMemoryOperationHandler operationHandler,
        TimeProvider timeProvider)
    {
        var requests = new MemoryWorkflowRequestFactory(timeProvider);
        query = new MemoryWorkflowQueryOperation(operationHandler, requests);
        status = new MemoryWorkflowStatusOperation(operationHandler, requests);
    }

    public WorkflowExecutorDescriptor Descriptor => MemoryWorkflowExecutorDescriptors.MemoryOperation;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);
        var settingsRead = MemoryWorkflowSettingsReader.Read(context.SettingsJson);
        if (settingsRead.IsUnsupported)
        {
            return WorkflowExecutorJson.Result(
                context,
                MemoryMafToolResultShaper.RejectedQuery(
                    MemoryToolResultStatus.UnsupportedOperation,
                    $"Unsupported memory workflow operation '{settingsRead.UnsupportedOperation}'."));
        }

        var settings = settingsRead.Settings!;
        var result = settings.Operation switch
        {
            MemoryWorkflowOperation.ContextQuery =>
                await query.ExecuteAsync(context, input, settings, cancellationToken).ConfigureAwait(false),
            MemoryWorkflowOperation.OperationStatus =>
                await status.ExecuteAsync(context, settings, cancellationToken).ConfigureAwait(false),
            _ => MemoryMafToolResultShaper.RejectedQuery(
                MemoryToolResultStatus.UnsupportedOperation,
                $"Unsupported memory workflow operation '{settings.Operation}'.")
        };
        return WorkflowExecutorJson.Result(context, result);
    }
}
