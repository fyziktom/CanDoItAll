using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public static class AgentExecutionActivityLimits
{
    public const int MaximumMessageLength = 2048;
    public const int MaximumErrorCodeLength = 128;
}

public static class AgentExecutionActivityFailureCodes
{
    public const string UnhandledExecutionFailure = "agent-execution-unhandled";
    public const string OutputValidationFailure = "agent-output-validation";
}

[JsonConverter(typeof(AgentExecutionOperationIdJsonConverter))]
public readonly record struct AgentExecutionOperationId
{
    public AgentExecutionOperationId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Agent execution operation id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static AgentExecutionOperationId New()
    {
        return new AgentExecutionOperationId(Guid.NewGuid());
    }

    public override string ToString()
    {
        return Value.ToString("N");
    }
}

public sealed class AgentExecutionOperationIdJsonConverter : JsonConverter<AgentExecutionOperationId>
{
    public override AgentExecutionOperationId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String ||
            !reader.TryGetGuid(out var value) ||
            value == Guid.Empty)
        {
            throw new JsonException("Agent execution operation id must be a non-empty GUID string.");
        }

        return new AgentExecutionOperationId(value);
    }

    public override void Write(
        Utf8JsonWriter writer,
        AgentExecutionOperationId value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed record AgentExecutionActivityWorkspaceIdentity
{
    public AgentExecutionActivityWorkspaceIdentity(
        Guid databaseProfileId,
        WorkspaceScopeDescriptor workspaceScope,
        DatabaseProfileGeneration databaseProfileGeneration)
    {
        if (databaseProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "Database profile id cannot be empty.",
                nameof(databaseProfileId));
        }

        ArgumentNullException.ThrowIfNull(workspaceScope);

        DatabaseProfileId = databaseProfileId;
        WorkspaceScope = workspaceScope;
        DatabaseProfileGeneration = databaseProfileGeneration;
    }

    public Guid DatabaseProfileId { get; }

    public WorkspaceScopeDescriptor WorkspaceScope { get; }

    public DatabaseProfileGeneration DatabaseProfileGeneration { get; }

    public static AgentExecutionActivityWorkspaceIdentity CreateHostLifetime(
        WorkspaceScopeDescriptor workspaceScope)
    {
        return new(
            Guid.NewGuid(),
            workspaceScope,
            new DatabaseProfileGeneration(0));
    }

    public AgentExecutionActivityStreamId CreateStreamId(
        AgentExecutionOperationId operationId)
    {
        return new(
            DatabaseProfileId,
            WorkspaceScope,
            DatabaseProfileGeneration,
            operationId);
    }
}

public sealed record AgentExecutionActivityStreamId
{
    public AgentExecutionActivityStreamId(
        Guid databaseProfileId,
        WorkspaceScopeDescriptor workspaceScope,
        DatabaseProfileGeneration databaseProfileGeneration,
        AgentExecutionOperationId operationId)
    {
        if (databaseProfileId == Guid.Empty)
        {
            throw new ArgumentException("Database profile id cannot be empty.", nameof(databaseProfileId));
        }

        ArgumentNullException.ThrowIfNull(workspaceScope);
        if (operationId.Value == Guid.Empty)
        {
            throw new ArgumentException("Agent execution operation id cannot be empty.", nameof(operationId));
        }

        DatabaseProfileId = databaseProfileId;
        WorkspaceScope = workspaceScope;
        DatabaseProfileGeneration = databaseProfileGeneration;
        OperationId = operationId;
    }

    public Guid DatabaseProfileId { get; }

    public WorkspaceScopeDescriptor WorkspaceScope { get; }

    public DatabaseProfileGeneration DatabaseProfileGeneration { get; }

    public AgentExecutionOperationId OperationId { get; }
}

public enum AgentExecutionActivityPhase
{
    Accepted,
    CapturingContext,
    ResolvingPreparation,
    ResolvingProvider,
    ResolvingSession,
    CreatingExecution,
    PreparingInput,
    PreparingCapabilities,
    PreparingRuntime,
    WaitingForProvider,
    Streaming,
    UsingTool,
    AwaitingApproval,
    PersistingResult,
    Completed,
    Failed,
    Cancelled
}

public enum AgentExecutionActivityTerminalOutcome
{
    Succeeded,
    Failed,
    Cancelled,
    Suspended
}

public sealed record AgentExecutionActivityContextIdentity
{
    public AgentExecutionActivityContextIdentity(
        AgentChatContextSource source,
        long version)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (version < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                version,
                "A context version cannot be negative.");
        }

        Source = source;
        Version = version;
    }

    public AgentChatContextSource Source { get; }

    public long Version { get; }
}

public sealed record AgentExecutionActivity
{
    public AgentExecutionActivity(
        AgentExecutionActivityPhase phase,
        DateTimeOffset occurredAtUtc,
        Guid? agentId,
        string message,
        Guid? chatSessionId = null,
        Guid? executionRunId = null,
        AgentExecutionActivityTerminalOutcome? terminalOutcome = null,
        string? errorCode = null,
        AgentExecutionActivityContextIdentity? context = null)
    {
        if (!Enum.IsDefined(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }

        ValidateOptionalId(agentId, nameof(agentId));
        ValidateOptionalId(chatSessionId, nameof(chatSessionId));
        ValidateOptionalId(executionRunId, nameof(executionRunId));

        Phase = phase;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        AgentId = agentId;
        Message = NormalizeRequiredText(
            message,
            AgentExecutionActivityLimits.MaximumMessageLength,
            nameof(message));
        ChatSessionId = chatSessionId;
        ExecutionRunId = executionRunId;
        TerminalOutcome = terminalOutcome;
        Context = context;
        ErrorCode = NormalizeOptionalText(
            errorCode,
            AgentExecutionActivityLimits.MaximumErrorCodeLength,
            nameof(errorCode));

        ValidateTerminalState();
    }

    public AgentExecutionActivityPhase Phase { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public Guid? AgentId { get; }

    public string Message { get; }

    public Guid? ChatSessionId { get; }

    public Guid? ExecutionRunId { get; }

    public AgentExecutionActivityTerminalOutcome? TerminalOutcome { get; }

    public AgentExecutionActivityContextIdentity? Context { get; }

    public string? ErrorCode { get; }

    [JsonIgnore]
    public bool IsTerminal => TerminalOutcome.HasValue;

    private static void ValidateOptionalId(Guid? value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Optional identifiers cannot be empty.", parameterName);
        }
    }

    private static string NormalizeRequiredText(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Text cannot exceed {maximumLength} characters.",
                parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Text cannot exceed {maximumLength} characters.",
                parameterName);
        }

        return normalized;
    }

    private void ValidateTerminalState()
    {
        var expectedPhase = TerminalOutcome switch
        {
            AgentExecutionActivityTerminalOutcome.Succeeded => AgentExecutionActivityPhase.Completed,
            AgentExecutionActivityTerminalOutcome.Failed => AgentExecutionActivityPhase.Failed,
            AgentExecutionActivityTerminalOutcome.Cancelled => AgentExecutionActivityPhase.Cancelled,
            AgentExecutionActivityTerminalOutcome.Suspended => AgentExecutionActivityPhase.AwaitingApproval,
            null => (AgentExecutionActivityPhase?)null,
            _ => throw new ArgumentOutOfRangeException(nameof(TerminalOutcome))
        };

        if (expectedPhase.HasValue && Phase != expectedPhase.Value)
        {
            throw new ArgumentException(
                $"Terminal outcome '{TerminalOutcome}' requires phase '{expectedPhase.Value}'.",
                nameof(Phase));
        }

        if (!expectedPhase.HasValue &&
            Phase is AgentExecutionActivityPhase.Completed or
                AgentExecutionActivityPhase.Failed or
                AgentExecutionActivityPhase.Cancelled)
        {
            throw new ArgumentException(
                $"Phase '{Phase}' requires a terminal outcome.",
                nameof(Phase));
        }

        if (ErrorCode is not null && TerminalOutcome != AgentExecutionActivityTerminalOutcome.Failed)
        {
            throw new ArgumentException(
                "An error code is valid only for failed terminal activity.",
                nameof(ErrorCode));
        }
    }
}
