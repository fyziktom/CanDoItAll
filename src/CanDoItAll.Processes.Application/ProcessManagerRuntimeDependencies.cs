using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessManagerRuntimeDependencies(
    IProcessDiagnosticEvidenceStore Diagnostics,
    IProcessIncidentStore Incidents,
    IProcessManagerQueue Queue,
    IProcessRecoveryPolicy RecoveryPolicy,
    IProcessRecoveryRequestStore RecoveryRequests,
    IProcessBranchDecisionStore BranchDecisions,
    IProcessLoopBudgetLedger LoopBudgets,
    IProcessSubprocessMessageStore SubprocessMessages,
    IProcessManagerDecisionStore Decisions);
