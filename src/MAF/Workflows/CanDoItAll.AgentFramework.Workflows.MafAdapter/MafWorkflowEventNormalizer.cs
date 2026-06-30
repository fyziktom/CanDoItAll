using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.AgentFramework.Maf;

public interface IMafWorkflowEventNormalizer
{
    WorkflowEventRecord Normalize(
        WorkflowRunId runId,
        WorkflowEvent workflowEvent,
        MafWorkflowEventBindingIndex bindings,
        DateTimeOffset occurredAtUtc);
}

public sealed class MafWorkflowEventBindingIndex
{
    private readonly IReadOnlyDictionary<string, MafWorkflowEventBinding> bindingsByMafExecutorId;

    private MafWorkflowEventBindingIndex(
        IReadOnlyDictionary<string, MafWorkflowEventBinding> bindingsByMafExecutorId)
    {
        this.bindingsByMafExecutorId = bindingsByMafExecutorId;
    }

    public static MafWorkflowEventBindingIndex FromDefinition(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new MafWorkflowEventBindingIndex(definition.Graph.Nodes.ToDictionary(
            node => node.Id.Value,
            node => new MafWorkflowEventBinding(node.Id, node.Settings.ExecutorId),
            StringComparer.Ordinal));
    }

    public bool TryResolve(
        string? mafExecutorId,
        out MafWorkflowEventBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(mafExecutorId) &&
            bindingsByMafExecutorId.TryGetValue(mafExecutorId.Trim(), out binding))
        {
            return true;
        }

        binding = default;
        return false;
    }
}

public readonly record struct MafWorkflowEventBinding(
    WorkflowNodeId NodeId,
    WorkflowExecutorId? ExecutorId);

public sealed class MafWorkflowEventNormalizer : IMafWorkflowEventNormalizer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public WorkflowEventRecord Normalize(
        WorkflowRunId runId,
        WorkflowEvent workflowEvent,
        MafWorkflowEventBindingIndex bindings,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);
        ArgumentNullException.ThrowIfNull(bindings);

        var eventType = workflowEvent.GetType().Name;
        var sourceId = ResolveSourceId(workflowEvent);
        var nodeId = default(WorkflowNodeId?);
        var executorId = default(WorkflowExecutorId?);
        if (bindings.TryResolve(sourceId, out var binding))
        {
            nodeId = binding.NodeId;
            executorId = binding.ExecutorId;
        }

        var payloadJson = ResolveInlinePayloadJson(workflowEvent);
        var message = CreateMessage(workflowEvent, eventType, sourceId, nodeId);
        return new WorkflowEventRecord(
            Guid.NewGuid(),
            runId,
            MafWorkflowStatusMapper.MapEventKind(workflowEvent),
            nodeId,
            message,
            WorkflowEventPayloads.Serialize(
                WorkflowEventPayloadSource.MafNative,
                eventType,
                nodeId,
                executorId,
                inlineJson: payloadJson,
                reference: nodeId.HasValue ? string.Empty : sourceId ?? string.Empty),
            occurredAtUtc);
    }

    private static string CreateMessage(
        WorkflowEvent workflowEvent,
        string eventType,
        string? sourceId,
        WorkflowNodeId? nodeId)
    {
        return workflowEvent switch
        {
            WorkflowStartedEvent => "Workflow started.",
            WorkflowOutputEvent => nodeId.HasValue
                ? $"Workflow output emitted by node '{nodeId.Value}'."
                : "Workflow output emitted.",
            WorkflowWarningEvent => CreateWarningMessage(workflowEvent),
            WorkflowErrorEvent errorEvent => CreateErrorMessage(errorEvent),
            RequestInfoEvent => nodeId.HasValue
                ? $"Workflow external request emitted by node '{nodeId.Value}'."
                : "Workflow external request emitted.",
            SuperStepEvent superStepEvent => $"Workflow superstep {superStepEvent.StepNumber} completed.",
            ExecutorEvent => !string.IsNullOrWhiteSpace(sourceId)
                ? $"MAF executor '{sourceId}' emitted {eventType}."
                : $"MAF executor emitted {eventType}.",
            _ => $"MAF event '{eventType}' emitted."
        };
    }

    private static string CreateWarningMessage(WorkflowEvent workflowEvent)
    {
        var message = WorkflowExecutorRedaction.RedactText(TryReadStringProperty(workflowEvent, "Message"));
        return string.IsNullOrWhiteSpace(message)
            ? "Workflow warning emitted."
            : $"Workflow warning: {message}";
    }

    private static string CreateErrorMessage(WorkflowErrorEvent errorEvent)
    {
        var message = WorkflowExecutorRedaction.RedactText(errorEvent.Exception?.Message);
        return string.IsNullOrWhiteSpace(message)
            ? "Workflow error emitted."
            : $"Workflow error: {message}";
    }

    private static string ResolveSourceId(WorkflowEvent workflowEvent)
    {
        return workflowEvent switch
        {
            WorkflowOutputEvent outputEvent => FirstNonEmpty(
                outputEvent.ExecutorId,
                TryReadStringProperty(outputEvent, "SourceId")),
            ExecutorEvent executorEvent => executorEvent.ExecutorId,
            _ => FirstNonEmpty(
                TryReadStringProperty(workflowEvent, "ExecutorId"),
                TryReadStringProperty(workflowEvent, "SourceId"),
                TryReadStringProperty(workflowEvent, "Name"))
        };
    }

    private static string ResolveInlinePayloadJson(WorkflowEvent workflowEvent)
    {
        if (workflowEvent is WorkflowOutputEvent outputEvent)
        {
            if (outputEvent.Is<WorkflowNodeInput>(out var input))
            {
                return input.PayloadJson;
            }

            if (outputEvent.Is<string>(out var text))
            {
                return text;
            }
        }

        if (workflowEvent is WorkflowErrorEvent errorEvent && errorEvent.Exception is not null)
        {
            return JsonSerializer.Serialize(
                new
                {
                    exceptionType = errorEvent.Exception.GetType().FullName,
                    message = WorkflowExecutorRedaction.RedactText(errorEvent.Exception.Message)
                },
                JsonOptions);
        }

        foreach (var property in ResolveReadableProperties(workflowEvent, "Data"))
        {
            try
            {
                var value = property.GetValue(workflowEvent);
                if (value is WorkflowNodeInput input)
                {
                    return input.PayloadJson;
                }

                if (value is string text)
                {
                    return text;
                }
            }
            catch (Exception exception) when (exception is TargetInvocationException or InvalidOperationException)
            {
                continue;
            }
        }

        return string.Empty;
    }

    private static string TryReadStringProperty(
        object source,
        string propertyName)
    {
        foreach (var property in ResolveReadableProperties(source, propertyName))
        {
            try
            {
                var value = property.GetValue(source);
                if (value is string text)
                {
                    return text.Trim();
                }

                if (value is not null && property.PropertyType.IsValueType)
                {
                    return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
                }
            }
            catch (Exception exception) when (exception is TargetInvocationException or InvalidOperationException)
            {
                continue;
            }
        }

        return string.Empty;
    }

    private static IReadOnlyList<PropertyInfo> ResolveReadableProperties(
        object source,
        string propertyName)
        => source.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property =>
                string.Equals(property.Name, propertyName, StringComparison.Ordinal) &&
                property.GetIndexParameters().Length == 0 &&
                property.GetMethod is not null)
            .OrderBy(property => property.DeclaringType == source.GetType() ? 0 : 1)
            .ToArray();

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
