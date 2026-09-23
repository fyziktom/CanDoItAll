using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentToolFailure
{
    string ErrorCode { get; }

    string SafeMessage { get; }

    bool IsSafeToExpose { get; }

    bool CanRetryWithCorrectedInput { get; }
}

public interface IAgentToolFailureEffectEvidence : IAgentToolFailure
{
    AgentToolEffectState EffectState { get; }
}

public sealed record AgentToolFailureResult(
    bool Succeeded,
    string ErrorCode,
    string Message,
    bool CanRetryWithCorrectedInput) : IAgentToolInvocationResultEvidence
{
    public AgentToolEffectState EffectState { get; init; } = AgentToolEffectState.Unknown;

    AgentToolInvocationOutcome IAgentToolInvocationResultEvidence.Outcome =>
        Succeeded
            ? AgentToolInvocationOutcome.Succeeded
            : AgentToolInvocationOutcome.Failed;

    string IAgentToolInvocationResultEvidence.FailureCode => ErrorCode;

    string IAgentToolInvocationResultEvidence.SafeMessage => Message;
}

/// <summary>
/// A tool rejected its input before performing any effect. Tools may raise this only while nothing has been
/// stored, written or dispatched yet, because the failure tells the agent that a corrected retry is safe and the
/// runtime records the attempt as a proven no-effect rejection rather than an uncertain mutation.
/// </summary>
public sealed class AgentToolInputValidationException : InvalidOperationException, IAgentToolFailureEffectEvidence
{
    public const string FailureCode = "InvalidToolInput";

    private AgentToolInputValidationException(string safeMessage)
        : base(NormalizeSafeMessage(safeMessage))
    {
    }

    public string ErrorCode => FailureCode;

    public string SafeMessage => Message;

    public bool IsSafeToExpose => true;

    public bool CanRetryWithCorrectedInput => true;

    public AgentToolEffectState EffectState => AgentToolEffectState.None;

    public static AgentToolInputValidationException Create(string safeMessage)
        => new(safeMessage);

    private static string NormalizeSafeMessage(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        return safeMessage.Trim();
    }
}

/// <summary>
/// A tool refused to proceed because its target already exists or otherwise conflicts with the request. The
/// conflict is detected before anything is written, so the attempt is recorded as a proven no-effect rejection.
/// </summary>
public sealed class AgentToolConflictException : InvalidOperationException, IAgentToolFailureEffectEvidence
{
    public const string FailureCode = "ToolConflict";

    private AgentToolConflictException(string safeMessage)
        : base(NormalizeSafeMessage(safeMessage))
    {
    }

    public string ErrorCode => FailureCode;

    public string SafeMessage => Message;

    public bool IsSafeToExpose => true;

    public bool CanRetryWithCorrectedInput => true;

    public AgentToolEffectState EffectState => AgentToolEffectState.None;

    public static AgentToolConflictException Create(string safeMessage)
        => new(safeMessage);

    private static string NormalizeSafeMessage(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        return safeMessage.Trim();
    }
}

public enum WorkspaceReadOnlyAncestorMutationOperation
{
    CopyOrReplace,
    Move,
    MoveOrReplace,
    ExtractInto
}

/// <summary>
/// A workspace tool refused an operation. Access guards run before the operation starts, so their denials carry a
/// proven no-effect state; an access failure observed while the operation was already running keeps an unknown
/// effect state because part of the requested tree may have changed.
/// </summary>
public sealed class WorkspaceToolAccessDeniedException : InvalidOperationException, IAgentToolFailureEffectEvidence
{
    public const string FailureCode = "WorkspaceAccessDenied";

    private WorkspaceToolAccessDeniedException(
        string safeMessage,
        bool canRetryWithCorrectedInput,
        AgentToolEffectState effectState = AgentToolEffectState.None)
        : base(NormalizeSafeMessage(safeMessage))
    {
        CanRetryWithCorrectedInput = canRetryWithCorrectedInput;
        EffectState = effectState;
    }

    public string ErrorCode => FailureCode;

    public string SafeMessage => Message;

    public bool IsSafeToExpose => true;

    public bool CanRetryWithCorrectedInput { get; }

    public AgentToolEffectState EffectState { get; }

    public static WorkspaceToolAccessDeniedException FileReadDisabled()
        => new(
            "This agent is not allowed to read workspace files. Ask the operator to enable workspace file access.",
            canRetryWithCorrectedInput: false);

    public static WorkspaceToolAccessDeniedException FileWriteDisabled()
        => new(
            "This agent is not allowed to write workspace files. Ask the operator to enable workspace file writes.",
            canRetryWithCorrectedInput: false);

    public static WorkspaceToolAccessDeniedException ExternalTargetReadOnly(string path)
        => new(
            $"External workspace path '{NormalizePathForMessage(path)}' is read-only for this run. Use a read operation or choose a writable grounded external target.",
            canRetryWithCorrectedInput: true);

    public static WorkspaceToolAccessDeniedException ExternalTargetNotAuthorized(string path)
        => new(
            $"External workspace path '{NormalizePathForMessage(path)}' is not in this run's allowed external workspace roots. Retry with a grounded external-target alias from the current run.",
            canRetryWithCorrectedInput: true);

    public static WorkspaceToolAccessDeniedException RecursiveDeleteReadOnlyAncestor(string path)
        => new(
            $"Refusing to recursively delete external workspace path '{NormalizePathForMessage(path)}' because it is an ancestor of a read-only external target for this run. Delete only an explicitly writable descendant.",
            canRetryWithCorrectedInput: true);

    public static WorkspaceToolAccessDeniedException GroundedTargetRootDelete(string path)
        => new(
            $"Refusing to delete grounded external target root '{NormalizePathForMessage(path)}'. Repair the scaffold in place or delete only explicit generated evidence files.",
            canRetryWithCorrectedInput: true);

    public static WorkspaceToolAccessDeniedException ProtectedProductDirectoryDelete(string path)
        => new(
            $"Refusing to recursively delete protected external product directory '{NormalizePathForMessage(path)}'. Repair source and test files in place instead.",
            canRetryWithCorrectedInput: true);

    public static WorkspaceToolAccessDeniedException ReadOnlyAncestorMutation(
        WorkspaceReadOnlyAncestorMutationOperation operation,
        string path)
    {
        var operationText = operation switch
        {
            WorkspaceReadOnlyAncestorMutationOperation.CopyOrReplace => "copy or replace",
            WorkspaceReadOnlyAncestorMutationOperation.Move => "move",
            WorkspaceReadOnlyAncestorMutationOperation.MoveOrReplace => "move or replace",
            WorkspaceReadOnlyAncestorMutationOperation.ExtractInto => "extract into",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown read-only ancestor mutation operation.")
        };
        return new(
            $"Refusing to {operationText} external workspace path '{NormalizePathForMessage(path)}' because it is an ancestor of a read-only external target for this run. Choose an explicitly writable destination.",
            canRetryWithCorrectedInput: true);
    }

    public static WorkspaceToolAccessDeniedException InaccessiblePath(string path)
        => new(
            $"Workspace path '{NormalizePathForMessage(path)}' could not be fully inspected because access to part of the requested tree was denied. Narrow the path or ask the operator to grant access, then retry.",
            canRetryWithCorrectedInput: true,
            AgentToolEffectState.Unknown);

    public static WorkspaceToolAccessDeniedException InaccessiblePaths(
        string firstPath,
        string secondPath)
        => new(
            $"Workspace paths '{NormalizePathForMessage(firstPath)}' and '{NormalizePathForMessage(secondPath)}' could not be fully accessed for the requested operation. Narrow the paths or ask the operator to grant access, then retry.",
            canRetryWithCorrectedInput: true,
            AgentToolEffectState.Unknown);

    /// <summary>A read-only workspace tool could not inspect part of the requested tree; a read changes nothing.</summary>
    public static WorkspaceToolAccessDeniedException InaccessibleReadPath(string path)
        => new(
            $"Workspace path '{NormalizePathForMessage(path)}' could not be fully read because access to part of the requested tree was denied. Narrow the path or ask the operator to grant access, then retry.",
            canRetryWithCorrectedInput: true,
            AgentToolEffectState.None);

    /// <summary>A read-only workspace tool could not read one of two requested paths; a read changes nothing.</summary>
    public static WorkspaceToolAccessDeniedException InaccessibleReadPaths(
        string firstPath,
        string secondPath)
        => new(
            $"Workspace paths '{NormalizePathForMessage(firstPath)}' and '{NormalizePathForMessage(secondPath)}' could not be fully read. Narrow the paths or ask the operator to grant access, then retry.",
            canRetryWithCorrectedInput: true,
            AgentToolEffectState.None);

    private static string NormalizeSafeMessage(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        return safeMessage.Trim();
    }

    private static string NormalizePathForMessage(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var alias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(path);
        if (!string.IsNullOrWhiteSpace(alias))
        {
            return alias;
        }

        return Path.IsPathRooted(path)
            ? "external-target/unresolved"
            : path.Trim().Replace('\\', '/');
    }
}
