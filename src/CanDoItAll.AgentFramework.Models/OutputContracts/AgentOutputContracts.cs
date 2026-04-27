using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

[JsonConverter(typeof(JsonStringEnumConverter<AgentStepOutcome>))]
public enum AgentStepOutcome
{
    Completed,
    NeedsHumanInput,
    NeedsMoreData,
    Failed,
    Blocked
}

[JsonConverter(typeof(JsonStringEnumConverter<AgentOutputValidationSeverity>))]
public enum AgentOutputValidationSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

[JsonConverter(typeof(JsonStringEnumConverter<AgentExecutionFailureKind>))]
public enum AgentExecutionFailureKind
{
    MalformedOutput,
    ValidationFailed,
    RepairLimitExceeded,
    ToolFinalizerMissing,
    PolicyRejected,
    RuntimeException
}

[JsonConverter(typeof(JsonStringEnumConverter<CodeReviewStatus>))]
public enum CodeReviewStatus
{
    Passed,
    NeedsChanges,
    Failed,
    Blocked
}

[JsonConverter(typeof(JsonStringEnumConverter<ArchitectureReviewStatus>))]
public enum ArchitectureReviewStatus
{
    Approved,
    NeedsChanges,
    Rejected,
    Blocked
}

[JsonConverter(typeof(JsonStringEnumConverter<TestPlanStatus>))]
public enum TestPlanStatus
{
    Ready,
    NeedsChanges,
    Blocked
}

[JsonConverter(typeof(JsonStringEnumConverter<AgentToolExecutionDecision>))]
public enum AgentToolExecutionDecision
{
    Allow,
    Deny,
    NeedsHumanApproval
}

[JsonConverter(typeof(JsonStringEnumConverter<ProcessPatchOperationKind>))]
public enum ProcessPatchOperationKind
{
    Add,
    Replace,
    Remove
}

[JsonConverter(typeof(JsonStringEnumConverter<ProcessStepOutcomeStatus>))]
public enum ProcessStepOutcomeStatus
{
    Completed,
    Blocked,
    Failed,
    WaitingApproval,
    Refused
}

public sealed record AgentStructuredOutputContract
{
    public AgentStructuredOutputContract(
        Type outputType,
        string schemaName = "",
        string schemaDescription = "")
    {
        ArgumentNullException.ThrowIfNull(outputType);
        if (!IsSupportedTopLevelObject(outputType))
        {
            throw new ArgumentException(
                $"Structured agent output type '{outputType.FullName}' must be an object DTO, not a primitive, string, enum, array, collection, or weak JSON container.",
                nameof(outputType));
        }

        OutputType = outputType;
        SchemaName = schemaName;
        SchemaDescription = schemaDescription;
    }

    public Type OutputType { get; }
    public string SchemaName { get; }
    public string SchemaDescription { get; }

    public static AgentStructuredOutputContract For<TOutput>(
        string schemaName = "",
        string schemaDescription = "")
        => new(typeof(TOutput), schemaName, schemaDescription);

    private static bool IsSupportedTopLevelObject(Type outputType)
    {
        if (outputType == typeof(string) ||
            outputType == typeof(object) ||
            outputType == typeof(JsonElement) ||
            outputType == typeof(JsonDocument) ||
            outputType.IsPrimitive ||
            outputType.IsEnum ||
            outputType.IsArray)
        {
            return false;
        }

        if (typeof(IEnumerable).IsAssignableFrom(outputType))
        {
            return false;
        }

        return outputType.IsClass;
    }
}

public sealed class AgentStepResult<TPayload>
{
    public required string AgentId { get; init; }
    public required string ProcessInstanceId { get; init; }
    public required string StepId { get; init; }
    public required AgentStepOutcome Outcome { get; init; }
    public required TPayload Payload { get; init; }
    public required IReadOnlyList<string> EvidenceRefs { get; init; }
    public required IReadOnlyList<string> NextActions { get; init; }
    public string? HumanReadableSummaryMarkdown { get; init; }
}

public sealed class AgentOutputEnvelope<TPayload>
{
    public required string AgentId { get; init; }
    public required string ProcessInstanceId { get; init; }
    public required string StepId { get; init; }
    public required string ContractName { get; init; }
    public required TPayload Payload { get; init; }
    public required IReadOnlyList<string> EvidenceRefs { get; init; }
    public required IReadOnlyList<string> NextActions { get; init; }
    public string? RawOutputHash { get; init; }
    public string? HumanReadableSummaryMarkdown { get; init; }
}

public sealed class AgentOutputValidationResult
{
    public required bool IsValid { get; init; }
    public required IReadOnlyList<AgentOutputValidationError> Errors { get; init; }

    public static AgentOutputValidationResult Success()
        => new()
        {
            IsValid = true,
            Errors = []
        };

    public static AgentOutputValidationResult Failure(params AgentOutputValidationError[] errors)
        => new()
        {
            IsValid = false,
            Errors = errors
        };
}

public sealed class AgentOutputValidationError
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? Path { get; init; }
    public AgentOutputValidationSeverity Severity { get; init; } = AgentOutputValidationSeverity.Error;
}

public sealed class AgentOutputRepairRequest
{
    public required string ContractName { get; init; }
    public required string InvalidRawOutput { get; init; }
    public required IReadOnlyList<AgentOutputValidationError> ValidationErrors { get; init; }
    public string? SchemaDescription { get; init; }
}

public sealed class AgentOutputRepairResult<TOutput>
{
    public required bool Succeeded { get; init; }
    public TOutput? Output { get; init; }
    public required IReadOnlyList<AgentOutputValidationError> RemainingErrors { get; init; }
    public int Attempts { get; init; }
}

public sealed class AgentExecutionFailure
{
    public required AgentExecutionFailureKind Kind { get; init; }
    public required string Message { get; init; }
    public required IReadOnlyList<AgentOutputValidationError> ValidationErrors { get; init; }
    public string? RawOutputHash { get; init; }
    public int RepairAttempts { get; init; }
}

public sealed class HumanEscalationRequest
{
    public required string Reason { get; init; }
    public required string RequestedRole { get; init; }
    public required IReadOnlyList<AgentOutputValidationError> ValidationErrors { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? StepId { get; init; }
    public string? HumanReadableSummaryMarkdown { get; init; }
}

public sealed class ProcessStatePatch
{
    public required IReadOnlyList<ProcessPatchOperation> Operations { get; init; }
}

public sealed class ProcessPatchOperation
{
    public required ProcessPatchOperationKind Op { get; init; }
    public required string Path { get; init; }
    public JsonElement? Value { get; init; }
    public required string Reason { get; init; }
}

public sealed class CodeReviewResult
{
    public required CodeReviewStatus Status { get; init; }
    public required IReadOnlyList<CodeReviewFinding> Findings { get; init; }
    public required IReadOnlyList<string> RequiredActions { get; init; }
    public required IReadOnlyList<string> EvidenceRefs { get; init; }
    public string? HumanReadableSummaryMarkdown { get; init; }
}

public sealed class CodeReviewFinding
{
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required string FilePath { get; init; }
    public int? StartLine { get; init; }
    public int? EndLine { get; init; }
    public string? Severity { get; init; }
}

public sealed class ImplementationPlanResult
{
    public required IReadOnlyList<ImplementationTask> Tasks { get; init; }
    public required IReadOnlyList<string> Risks { get; init; }
    public required IReadOnlyList<string> EvidenceRefs { get; init; }
    public string? HumanReadableSummaryMarkdown { get; init; }
}

public sealed class ImplementationTask
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<string> OwnedPaths { get; init; }
    public required IReadOnlyList<string> ValidationSteps { get; init; }
}

public sealed class ArchitectureReviewResult
{
    public required ArchitectureReviewStatus Status { get; init; }
    public required IReadOnlyList<string> BoundaryConcerns { get; init; }
    public required IReadOnlyList<string> RequiredActions { get; init; }
    public required IReadOnlyList<string> EvidenceRefs { get; init; }
    public string? HumanReadableSummaryMarkdown { get; init; }
}

public sealed class TestPlanResult
{
    public required TestPlanStatus Status { get; init; }
    public required IReadOnlyList<string> TestCases { get; init; }
    public required IReadOnlyList<string> CoverageGaps { get; init; }
    public required IReadOnlyList<string> EvidenceRefs { get; init; }
    public string? HumanReadableSummaryMarkdown { get; init; }
}

public sealed class ToolExecutionDecisionResult
{
    public required AgentToolExecutionDecision Decision { get; init; }
    public required string ToolName { get; init; }
    public required string Reason { get; init; }
    public required IReadOnlyList<string> EvidenceRefs { get; init; }
    public HumanEscalationRequest? Escalation { get; init; }
}

public sealed class ProcessStepOutcomeResult
{
    public required ProcessStepOutcomeStatus Status { get; init; }
    public required string Reason { get; init; }
    public string BranchOutcomeKey { get; init; } = string.Empty;
    public string BranchOutcomeTitle { get; init; } = string.Empty;
    public IReadOnlyList<string> EvidenceRefs { get; init; } = [];
    public IReadOnlyList<string> NextActions { get; init; } = [];
    public string? HumanReadableSummaryMarkdown { get; init; }
}
