using CanDoItAll.Processes.Drivers.Abstractions.Permissions;

namespace CanDoItAll.Modules.Processes;

internal enum ProcessExecutionCapableDriverSurface
{
    CommandExecution = 1,
    PackageRestore = 2,
    FileAccess = 3,
    WorkspaceWrite = 4,
    StorageWrite = 5,
    NetworkHttpCall = 6,
    OfficeGraphCall = 7,
    CrmMutation = 8,
    ProviderRepair = 9,
    FinalizerApplication = 10,
    TransitionMutation = 11,
    ClaimMutation = 12,
    RetryScheduling = 13,
    ProcessMutation = 14
}

internal enum ProcessExecutionCapableDriverApprovalStatus
{
    NotApproved = 0,
    Approved = 1
}

internal enum ProcessExecutionCapableDriverGateDecision
{
    Blocked = 0,
    ApprovedForFutureExecution = 1
}

internal enum ProcessExecutionCapableDriverFutureGateRequirement
{
    SourceBackedApprovalBundle = 1,
    LifecycleOwnership = 2,
    CancellationTimeoutFailureHandoff = 3,
    ImmutableAuditPersistence = 4,
    SandboxBoundary = 5,
    AuthorizationApprovalRevocation = 6,
    PublicApiCompatibility = 7,
    MaliciousCorpus = 8,
    RedTeamProof = 9
}

internal enum ProcessExecutionCapableDriverAuthorizationGap
{
    ApprovalGrantMissing = 1,
    RevocationCheckMissing = 2,
    EmergencyStopActiveOrUnknown = 3
}

internal sealed record ProcessExecutionCapableDriverSandboxPolicy(
    ProcessExecutionCapableDriverApprovalStatus ApprovalStatus,
    bool DryRunOnly,
    IReadOnlyList<ProcessExecutionCapableDriverSurface> AllowListedSurfaces)
{
    public IReadOnlyList<ProcessExecutionCapableDriverSurface> AllowListedSurfaces { get; init; } =
        AllowListedSurfaces ?? throw new ArgumentNullException(nameof(AllowListedSurfaces));

    public static ProcessExecutionCapableDriverSandboxPolicy DefaultBlockedDryRun { get; } = new(
        ProcessExecutionCapableDriverApprovalStatus.NotApproved,
        DryRunOnly: true,
        []);

    public bool NoMutationPerformed => true;

    public bool AllowsProcessMutation => false;

    public bool AllowsTransitionMutation => false;

    public bool AllowsFinalizerMutation => false;

    public bool Allows(ProcessExecutionCapableDriverSurface surface)
    {
        return ApprovalStatus == ProcessExecutionCapableDriverApprovalStatus.Approved &&
            !DryRunOnly &&
            AllowListedSurfaces.Contains(surface);
    }
}

internal sealed record ProcessExecutionCapableDriverAuthorizationEvidence(
    bool ApprovalGrantPresent,
    bool RevocationCheckPassed,
    bool EmergencyStopClear)
{
    public static ProcessExecutionCapableDriverAuthorizationEvidence None { get; } = new(
        ApprovalGrantPresent: false,
        RevocationCheckPassed: false,
        EmergencyStopClear: false);

    public bool IsSatisfied => ApprovalGrantPresent && RevocationCheckPassed && EmergencyStopClear;

    public IReadOnlyList<ProcessExecutionCapableDriverAuthorizationGap> MissingGaps()
    {
        var gaps = new List<ProcessExecutionCapableDriverAuthorizationGap>();
        AddIfMissing(ApprovalGrantPresent, ProcessExecutionCapableDriverAuthorizationGap.ApprovalGrantMissing);
        AddIfMissing(RevocationCheckPassed, ProcessExecutionCapableDriverAuthorizationGap.RevocationCheckMissing);
        AddIfMissing(EmergencyStopClear, ProcessExecutionCapableDriverAuthorizationGap.EmergencyStopActiveOrUnknown);
        return gaps;

        void AddIfMissing(bool satisfied, ProcessExecutionCapableDriverAuthorizationGap gap)
        {
            if (!satisfied)
            {
                gaps.Add(gap);
            }
        }
    }
}

internal sealed record ProcessExecutionCapableDriverApprovalEvidence(
    bool SourceBackedApprovalBundle,
    bool LifecycleOwnership,
    bool CancellationTimeoutFailureHandoff,
    bool ImmutableAuditPersistence,
    bool SandboxBoundary,
    bool AuthorizationApprovalRevocation,
    bool PublicApiCompatibility,
    bool MaliciousCorpus,
    bool RedTeamProof,
    ProcessExecutionCapableDriverAuthorizationEvidence? AuthorizationEvidence = null)
{
    public static ProcessExecutionCapableDriverApprovalEvidence None { get; } = new(
        SourceBackedApprovalBundle: false,
        LifecycleOwnership: false,
        CancellationTimeoutFailureHandoff: false,
        ImmutableAuditPersistence: false,
        SandboxBoundary: false,
        AuthorizationApprovalRevocation: false,
        PublicApiCompatibility: false,
        MaliciousCorpus: false,
        RedTeamProof: false);

    public ProcessExecutionCapableDriverAuthorizationEvidence EffectiveAuthorizationEvidence =>
        AuthorizationEvidence ?? ProcessExecutionCapableDriverAuthorizationEvidence.None;

    public IReadOnlyList<ProcessExecutionCapableDriverFutureGateRequirement> MissingRequirements()
    {
        var missing = new List<ProcessExecutionCapableDriverFutureGateRequirement>();
        AddIfMissing(SourceBackedApprovalBundle, ProcessExecutionCapableDriverFutureGateRequirement.SourceBackedApprovalBundle);
        AddIfMissing(LifecycleOwnership, ProcessExecutionCapableDriverFutureGateRequirement.LifecycleOwnership);
        AddIfMissing(CancellationTimeoutFailureHandoff, ProcessExecutionCapableDriverFutureGateRequirement.CancellationTimeoutFailureHandoff);
        AddIfMissing(ImmutableAuditPersistence, ProcessExecutionCapableDriverFutureGateRequirement.ImmutableAuditPersistence);
        AddIfMissing(SandboxBoundary, ProcessExecutionCapableDriverFutureGateRequirement.SandboxBoundary);
        AddIfMissing(
            AuthorizationApprovalRevocation && EffectiveAuthorizationEvidence.IsSatisfied,
            ProcessExecutionCapableDriverFutureGateRequirement.AuthorizationApprovalRevocation);
        AddIfMissing(PublicApiCompatibility, ProcessExecutionCapableDriverFutureGateRequirement.PublicApiCompatibility);
        AddIfMissing(MaliciousCorpus, ProcessExecutionCapableDriverFutureGateRequirement.MaliciousCorpus);
        AddIfMissing(RedTeamProof, ProcessExecutionCapableDriverFutureGateRequirement.RedTeamProof);

        return missing;

        void AddIfMissing(bool satisfied, ProcessExecutionCapableDriverFutureGateRequirement requirement)
        {
            if (!satisfied)
            {
                missing.Add(requirement);
            }
        }
    }
}

internal sealed record ProcessExecutionCapableDriverSurfaceRule(
    ProcessExecutionCapableDriverSurface Surface,
    ProcessDriverOperation? DriverOperation,
    string Description)
{
    public bool DeniedByDefault => true;
}

internal static class ProcessExecutionCapableDriverSurfaceMatrix
{
    public static IReadOnlyList<ProcessExecutionCapableDriverSurfaceRule> DefaultDeniedRules { get; } =
    [
        new(ProcessExecutionCapableDriverSurface.CommandExecution, ProcessDriverOperation.ExecuteCommand, "Shell or local command execution."),
        new(ProcessExecutionCapableDriverSurface.PackageRestore, ProcessDriverOperation.RestorePackage, "Package restore or dependency acquisition."),
        new(ProcessExecutionCapableDriverSurface.FileAccess, ProcessDriverOperation.WriteArtifact, "File read/write access outside supplied evidence."),
        new(ProcessExecutionCapableDriverSurface.WorkspaceWrite, ProcessDriverOperation.WriteWorkspaceStorage, "Workspace file mutation."),
        new(ProcessExecutionCapableDriverSurface.StorageWrite, ProcessDriverOperation.WriteWorkspaceStorage, "Managed storage mutation."),
        new(ProcessExecutionCapableDriverSurface.NetworkHttpCall, null, "Outbound network or HTTP call."),
        new(ProcessExecutionCapableDriverSurface.OfficeGraphCall, ProcessDriverOperation.CallOfficeGraph, "Office or Graph API call."),
        new(ProcessExecutionCapableDriverSurface.CrmMutation, ProcessDriverOperation.MutateBusinessRecord, "CRM or business record mutation."),
        new(ProcessExecutionCapableDriverSurface.ProviderRepair, null, "Provider repair or fallback execution."),
        new(ProcessExecutionCapableDriverSurface.FinalizerApplication, ProcessDriverOperation.ApplyFinalizer, "Process finalizer application."),
        new(ProcessExecutionCapableDriverSurface.TransitionMutation, ProcessDriverOperation.ApplyTransition, "Process transition mutation."),
        new(ProcessExecutionCapableDriverSurface.ClaimMutation, ProcessDriverOperation.ClaimDispatch, "Dispatch claim mutation."),
        new(ProcessExecutionCapableDriverSurface.RetryScheduling, ProcessDriverOperation.ScheduleRetry, "Retry scheduling mutation."),
        new(ProcessExecutionCapableDriverSurface.ProcessMutation, ProcessDriverOperation.MutateProcessState, "Process state mutation.")
    ];

    public static IReadOnlyList<ProcessExecutionCapableDriverSurface> ResolveSurfacesForOperations(
        IReadOnlyList<ProcessDriverOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var operationSet = operations.ToHashSet();
        return DefaultDeniedRules
            .Where(rule => rule.DriverOperation.HasValue && operationSet.Contains(rule.DriverOperation.Value))
            .Select(rule => rule.Surface)
            .Distinct()
            .ToArray();
    }
}

internal sealed class ProcessExecutionCapableDriverFutureGate
{
    public ProcessExecutionCapableDriverGateResult Evaluate(
        ProcessExecutionCapableDriverSandboxPolicy requestedPolicy,
        ProcessExecutionCapableDriverApprovalEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(requestedPolicy);
        ArgumentNullException.ThrowIfNull(evidence);

        var missingRequirements = evidence.MissingRequirements();
        var approved = requestedPolicy.ApprovalStatus == ProcessExecutionCapableDriverApprovalStatus.Approved &&
            !requestedPolicy.DryRunOnly &&
            missingRequirements.Count == 0;

        return new ProcessExecutionCapableDriverGateResult(
            approved
                ? ProcessExecutionCapableDriverGateDecision.ApprovedForFutureExecution
                : ProcessExecutionCapableDriverGateDecision.Blocked,
            missingRequirements,
            approved
                ? requestedPolicy
                : ProcessExecutionCapableDriverSandboxPolicy.DefaultBlockedDryRun);
    }
}

internal sealed record ProcessExecutionCapableDriverGateResult(
    ProcessExecutionCapableDriverGateDecision Decision,
    IReadOnlyList<ProcessExecutionCapableDriverFutureGateRequirement> MissingRequirements,
    ProcessExecutionCapableDriverSandboxPolicy EffectivePolicy)
{
    public bool NoMutationPerformed => true;

    public bool AllowsProcessMutation => false;

    public bool AllowsTransitionMutation => false;

    public bool AllowsFinalizerMutation => false;

    public bool Allows(ProcessExecutionCapableDriverSurface surface)
    {
        return Decision == ProcessExecutionCapableDriverGateDecision.ApprovedForFutureExecution &&
            EffectivePolicy.Allows(surface);
    }
}
