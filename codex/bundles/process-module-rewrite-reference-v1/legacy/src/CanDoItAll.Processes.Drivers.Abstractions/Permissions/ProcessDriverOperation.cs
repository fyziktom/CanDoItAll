namespace CanDoItAll.Processes.Drivers.Abstractions.Permissions;

public enum ProcessDriverOperation
{
    InspectExistingEvidence = 1,
    ReturnDiagnostics = 2,
    ReadProcessFacts = 3,
    ExplainDenial = 4,
    MutateProcessState = 100,
    ExecuteCommand = 101,
    RestorePackage = 102,
    WriteArtifact = 103,
    WriteWorkspaceStorage = 104,
    CallOfficeGraph = 105,
    MutateEmailCategory = 106,
    CreateTask = 107,
    MutateBusinessRecord = 108,
    ApplyTransition = 109,
    ClaimDispatch = 110,
    ApplyFinalizer = 111,
    ScheduleRetry = 112
}
