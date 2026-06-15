using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessExecutionAttemptContext(
    int AttemptNumber,
    int MaxExecutionAttempts,
    Guid? RecoverableExecutionRunId,
    Guid? AutomationChatSessionId,
    string? RecoveryDirective);

internal sealed record ProcessExecutionLoopState(
    int MaxExecutionAttempts,
    Guid? RecoverableExecutionRunId,
    Guid? AutomationChatSessionId,
    string? RecoveryDirective,
    ProcessAutomationExecutionRunDetail? LastDetail,
    ProcessStepRunStatus? LastCompletionStatus);
