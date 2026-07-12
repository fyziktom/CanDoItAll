using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Processes.Contracts;

public enum ProcessWorkflowOutputMappingKind
{
    ProcessStepOutcome
}

[JsonConverter(typeof(ProcessWorkflowIdJsonConverter))]
public readonly record struct ProcessWorkflowId
{
    public ProcessWorkflowId(Guid value)
    {
        Value = ProcessWorkflowIdentifierValidation.Require(value, nameof(value));
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString("D");
}

[JsonConverter(typeof(ProcessWorkflowVersionIdJsonConverter))]
public readonly record struct ProcessWorkflowVersionId
{
    public ProcessWorkflowVersionId(Guid value)
    {
        Value = ProcessWorkflowIdentifierValidation.Require(value, nameof(value));
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString("D");
}

[JsonConverter(typeof(ProcessWorkflowRunIdJsonConverter))]
public readonly record struct ProcessWorkflowRunId
{
    public ProcessWorkflowRunId(Guid value)
    {
        Value = ProcessWorkflowIdentifierValidation.Require(value, nameof(value));
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString("D");
}

[JsonConverter(typeof(ProcessWorkflowAssignmentIdJsonConverter))]
public readonly record struct ProcessWorkflowAssignmentId
{
    public ProcessWorkflowAssignmentId(Guid value)
    {
        Value = ProcessWorkflowIdentifierValidation.Require(value, nameof(value));
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString("D");
}

public sealed record ProcessWorkflowExecutorBinding
{
    [JsonConstructor]
    public ProcessWorkflowExecutorBinding(
        ProcessWorkflowId workflowId,
        ProcessWorkflowVersionId? workflowVersionId = null,
        ProcessWorkflowOutputMappingKind outputMapping = ProcessWorkflowOutputMappingKind.ProcessStepOutcome)
    {
        if (workflowId.Value == Guid.Empty)
        {
            throw new ArgumentException("Process workflow id cannot be empty.", nameof(workflowId));
        }

        if (workflowVersionId.HasValue && workflowVersionId.Value.Value == Guid.Empty)
        {
            throw new ArgumentException("Process workflow version id cannot be empty.", nameof(workflowVersionId));
        }

        if (!Enum.IsDefined(outputMapping))
        {
            throw new ArgumentOutOfRangeException(nameof(outputMapping), outputMapping, "Process workflow output mapping is not defined.");
        }

        WorkflowId = workflowId;
        WorkflowVersionId = workflowVersionId;
        OutputMapping = outputMapping;
    }

    public ProcessWorkflowId WorkflowId { get; }

    public ProcessWorkflowVersionId? WorkflowVersionId { get; }

    public ProcessWorkflowOutputMappingKind OutputMapping { get; }
}

public sealed record WorkflowProcessAssignmentInputEnvelope
{
    public const string CurrentSchemaVersion = "CanDoItAll.ProcessWorkflowAssignmentInput/v1";

    [JsonConstructor]
    public WorkflowProcessAssignmentInputEnvelope(
        string schemaVersion,
        ProcessWorkflowRunId processRunId,
        ProcessWorkflowAssignmentId assignmentId,
        string stepKey,
        string roleKey,
        string prompt,
        string stepContractHash,
        IReadOnlyDictionary<string, string> launchVariables)
    {
        if (!string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unsupported process workflow input schema '{schemaVersion}'.", nameof(schemaVersion));
        }

        if (processRunId.Value == Guid.Empty)
        {
            throw new ArgumentException("Process run id cannot be empty.", nameof(processRunId));
        }

        if (assignmentId.Value == Guid.Empty)
        {
            throw new ArgumentException("Process assignment id cannot be empty.", nameof(assignmentId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(stepKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepContractHash);
        ArgumentNullException.ThrowIfNull(launchVariables);

        SchemaVersion = schemaVersion;
        ProcessRunId = processRunId;
        AssignmentId = assignmentId;
        StepKey = stepKey.Trim();
        RoleKey = roleKey.Trim();
        Prompt = prompt.Trim();
        StepContractHash = stepContractHash.Trim();
        LaunchVariables = launchVariables
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(
                item => item.Key.Trim(),
                item => item.Value?.Trim() ?? string.Empty,
                StringComparer.Ordinal);
    }

    public string SchemaVersion { get; }

    public ProcessWorkflowRunId ProcessRunId { get; }

    public ProcessWorkflowAssignmentId AssignmentId { get; }

    public string StepKey { get; }

    public string RoleKey { get; }

    public string Prompt { get; }

    public string StepContractHash { get; }

    public IReadOnlyDictionary<string, string> LaunchVariables { get; }
}

internal static class ProcessWorkflowIdentifierValidation
{
    public static Guid Require(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Process workflow identifier cannot be empty.", parameterName);
        }

        return value;
    }
}

public abstract class ProcessWorkflowGuidIdJsonConverter<T>(
    Func<Guid, T> factory,
    Func<T, Guid> valueAccessor) : JsonConverter<T>
{
    public override T Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || !reader.TryGetGuid(out var value))
        {
            throw new JsonException($"Expected a non-empty GUID string for '{typeof(T).Name}'.");
        }

        try
        {
            return factory(value);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException(exception.Message, exception);
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        T value,
        JsonSerializerOptions options)
        => writer.WriteStringValue(valueAccessor(value));
}

public sealed class ProcessWorkflowIdJsonConverter() :
    ProcessWorkflowGuidIdJsonConverter<ProcessWorkflowId>(
        static value => new ProcessWorkflowId(value),
        static value => value.Value);

public sealed class ProcessWorkflowVersionIdJsonConverter() :
    ProcessWorkflowGuidIdJsonConverter<ProcessWorkflowVersionId>(
        static value => new ProcessWorkflowVersionId(value),
        static value => value.Value);

public sealed class ProcessWorkflowRunIdJsonConverter() :
    ProcessWorkflowGuidIdJsonConverter<ProcessWorkflowRunId>(
        static value => new ProcessWorkflowRunId(value),
        static value => value.Value);

public sealed class ProcessWorkflowAssignmentIdJsonConverter() :
    ProcessWorkflowGuidIdJsonConverter<ProcessWorkflowAssignmentId>(
        static value => new ProcessWorkflowAssignmentId(value),
        static value => value.Value);
