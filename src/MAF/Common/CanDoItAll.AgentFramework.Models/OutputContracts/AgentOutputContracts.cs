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

/// <summary>
/// How a typed final answer must be submitted, as a string: <c>Disabled</c> (no finalizer tool), <c>Shadow</c> (the
/// finalizer tool is offered and its result checked without failing the run), <c>Required</c> (the answer must be
/// submitted through the finalizer tool; otherwise the run fails unless the policy allows recovery).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AgentFinalizerMode>))]
public enum AgentFinalizerMode
{
    Disabled,
    Shadow,
    Required
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

public static class AgentJsonSchemaOutputContractVersions
{
    public const string Current = "1.0";
    public const string Kind = "json-schema";
}

/// <summary>
/// JSON Schema that the final answer of an agent execution run must satisfy, sent as <c>structuredOutput</c> when a
/// run is started. The contract is checked before the run starts; an unsupported contract is rejected with HTTP 400
/// and an <c>agents.structured-output-*</c> code. When the run finishes, the answer is validated and the result is
/// returned as <c>structuredOutput</c> of the run result.
/// </summary>
/// <param name="Kind">Contract kind; must be <c>json-schema</c>.</param>
/// <param name="Version">Contract version; must be <c>1.0</c>.</param>
/// <param name="Name">
/// Name of the output: a letter followed by at most 63 ASCII letters, digits, underscores or hyphens.
/// </param>
/// <param name="Schema">
/// The JSON Schema, a JSON object of at most 64 KiB (UTF-8) with at most 16 nesting levels, 512 schema nodes and 128
/// properties per object; the root must declare type <c>object</c>. Supported keywords: <c>$schema</c>, <c>$id</c>,
/// <c>title</c>, <c>description</c>, <c>type</c>, <c>properties</c>, <c>required</c>, <c>additionalProperties</c>,
/// <c>items</c>, <c>enum</c> (1 to 128 values), <c>const</c>, <c>minimum</c>, <c>maximum</c>,
/// <c>exclusiveMinimum</c>, <c>exclusiveMaximum</c>, <c>minLength</c>, <c>maxLength</c>, <c>pattern</c> (at most 256
/// characters), <c>minItems</c>, <c>maxItems</c>, <c>uniqueItems</c>, <c>minProperties</c> and <c>maxProperties</c>.
/// Supported types: <c>object</c>, <c>array</c>, <c>string</c>, <c>number</c>, <c>integer</c>, <c>boolean</c> and
/// <c>null</c>.
/// </param>
/// <param name="Strict">
/// True (the default) requires every object schema to set <c>additionalProperties</c> to false and to list every
/// declared property in <c>required</c>; use a type that includes <c>null</c> for optional values.
/// </param>
public sealed record AgentJsonSchemaOutputContract(
    string Kind,
    string Version,
    string Name,
    JsonElement Schema,
    bool Strict = true);

/// <summary>
/// Result of validating a model answer against a JSON Schema output contract, as a string: <c>Valid</c>,
/// <c>ProviderRefusal</c> (the model refused), <c>MalformedJson</c> (not one complete JSON value, or larger than
/// 1 MiB) or <c>SchemaValidationFailed</c>. Server-sent event streams write it as camel-case text, for example
/// <c>schemaValidationFailed</c>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AgentJsonSchemaOutputValidationStatus>))]
public enum AgentJsonSchemaOutputValidationStatus
{
    Valid,
    ProviderRefusal,
    MalformedJson,
    SchemaValidationFailed
}

public sealed record AgentJsonSchemaOutputValidationError(
    string Code,
    string Message,
    string Path = "$");

public sealed record AgentJsonSchemaOutputResult(
    JsonElement? Data,
    string RawOutput,
    string Schema,
    string SchemaHash,
    AgentJsonSchemaOutputValidationStatus ValidationStatus,
    IReadOnlyList<AgentJsonSchemaOutputValidationError> ValidationErrors);

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
    public string ContractKey => string.IsNullOrWhiteSpace(SchemaName)
        ? OutputType.FullName ?? OutputType.Name
        : SchemaName;

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

public static class AgentStructuredOutputContracts
{
    public const string ProcessStepOutcomeResultKey = "process_step_outcome_result";
    public const string CodeReviewResultKey = "code_review_result";
    public const string ArchitectureReviewResultKey = "architecture_review_result";
    public const string ImplementationPlanResultKey = "implementation_plan_result";
    public const string TestPlanResultKey = "test_plan_result";
    public const string ToolExecutionDecisionResultKey = "tool_execution_decision_result";
    public const string ProcessStatePatchKey = "process_state_patch";
    public const string HumanEscalationRequestKey = "human_escalation_request";

    public static AgentStructuredOutputContract ProcessStepOutcomeResult { get; } =
        AgentStructuredOutputContract.For<ProcessStepOutcomeResult>(
            ProcessStepOutcomeResultKey,
            "Validated machine contract for process step completion, branch selection, acceptance-criterion proof, next actions, and display-only markdown summary.");

    public static AgentStructuredOutputContract CodeReviewResult { get; } =
        AgentStructuredOutputContract.For<CodeReviewResult>(
            CodeReviewResultKey,
            "Validated machine contract for code-review pass, failure, findings, required actions, and evidence references.");

    public static AgentStructuredOutputContract ArchitectureReviewResult { get; } =
        AgentStructuredOutputContract.For<ArchitectureReviewResult>(
            ArchitectureReviewResultKey,
            "Validated machine contract for architecture approval, rejection, boundary concerns, required actions, and evidence references.");

    public static AgentStructuredOutputContract ImplementationPlanResult { get; } =
        AgentStructuredOutputContract.For<ImplementationPlanResult>(
            ImplementationPlanResultKey,
            "Validated machine contract for implementation tasks, risks, owned paths, and validation steps.");

    public static AgentStructuredOutputContract TestPlanResult { get; } =
        AgentStructuredOutputContract.For<TestPlanResult>(
            TestPlanResultKey,
            "Validated machine contract for test plan readiness, test cases, coverage gaps, and evidence references.");

    public static AgentStructuredOutputContract ToolExecutionDecisionResult { get; } =
        AgentStructuredOutputContract.For<ToolExecutionDecisionResult>(
            ToolExecutionDecisionResultKey,
            "Validated machine contract for tool execution allow, deny, or human-approval decisions.");

    public static AgentStructuredOutputContract ProcessStatePatch { get; } =
        AgentStructuredOutputContract.For<ProcessStatePatch>(
            ProcessStatePatchKey,
            "Validated machine contract for governed process-state patch operations.");

    public static AgentStructuredOutputContract HumanEscalationRequest { get; } =
        AgentStructuredOutputContract.For<HumanEscalationRequest>(
            HumanEscalationRequestKey,
            "Validated machine contract for human escalation requests.");

    public static IReadOnlyList<AgentStructuredOutputContract> All { get; } =
    [
        ProcessStepOutcomeResult,
        CodeReviewResult,
        ArchitectureReviewResult,
        ImplementationPlanResult,
        TestPlanResult,
        ToolExecutionDecisionResult,
        ProcessStatePatch,
        HumanEscalationRequest
    ];

    private static readonly IReadOnlyDictionary<string, AgentStructuredOutputContract> KnownContracts =
        CreateKnownContracts();

    public static bool TryResolve(
        string? contractKey,
        out AgentStructuredOutputContract contract)
    {
        if (!string.IsNullOrWhiteSpace(contractKey) &&
            KnownContracts.TryGetValue(contractKey.Trim(), out var resolved))
        {
            contract = resolved;
            return true;
        }

        contract = default!;
        return false;
    }

    private static IReadOnlyDictionary<string, AgentStructuredOutputContract> CreateKnownContracts()
    {
        var contracts = new Dictionary<string, AgentStructuredOutputContract>(StringComparer.OrdinalIgnoreCase);
        foreach (var contract in All)
        {
            contracts[contract.ContractKey] = contract;
            contracts[contract.OutputType.FullName!] = contract;
            contracts[contract.OutputType.AssemblyQualifiedName!] = contract;
        }

        return contracts;
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
    public string? SchemaName { get; init; }
    public string? SchemaDescription { get; init; }
    public string? InvalidRawOutputHash { get; init; }
    public int AttemptNumber { get; init; }
    public int MaxAttempts { get; init; }
}

public sealed class AgentOutputRepairAttemptResult
{
    public required bool Succeeded { get; init; }
    public required string RepairedRawOutput { get; init; }
    public required IReadOnlyList<AgentOutputValidationError> RemainingErrors { get; init; }
    public string FailureMessage { get; init; } = string.Empty;
    public IReadOnlyList<ProviderUsageObservation> UsageObservations { get; init; } = [];
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
    public IReadOnlyList<ProcessAcceptanceCriterionEvidence> AcceptanceCriteriaEvidence { get; init; } = [];
    public IReadOnlyList<string> NextActions { get; init; } = [];
    public string? HumanReadableSummaryMarkdown { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<ProcessAcceptanceCriterionEvidenceStatus>))]
public enum ProcessAcceptanceCriterionEvidenceStatus
{
    Passed,
    Failed,
    NotVerified
}

public sealed class ProcessAcceptanceCriterionEvidence
{
    public required string CriterionId { get; init; }
    public required ProcessAcceptanceCriterionEvidenceStatus Status { get; init; }
    public required string Summary { get; init; }
    public IReadOnlyList<string> EvidenceRefs { get; init; } = [];
}
