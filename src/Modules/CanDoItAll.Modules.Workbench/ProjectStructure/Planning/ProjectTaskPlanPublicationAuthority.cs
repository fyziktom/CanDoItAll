using System.Collections.Immutable;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal interface IProjectTaskPlanPublicationAuthority {
    Task<IProjectTaskPlanHeldAuthority> AcquireAsync(ProjectTaskPlanSource original, ProjectWriteAdmission target, CancellationToken cancellationToken);
}

internal interface IProjectTaskPlanHeldAuthority : IAsyncDisposable {
    ImmutableArray<ProjectWriteAdmission> AdditionalProjects { get; }
    Task RequireCurrentAsync(CancellationToken cancellationToken);
    Task RequireEnlistedAsync(CancellationToken cancellationToken);
}

internal interface IProjectTaskPlanReceiptAccess {
    Task<bool> CanReadAsync(ProjectTaskPlanSource original, ProjectWriteAdmission target, CancellationToken cancellationToken);
}

internal interface IProjectTaskPlanActiveWorkAuthority : IProjectTaskPlanHeldAuthority {
    void RequireActiveWork(ProjectTaskPlanSource original, DecisionDataPolicy policy);
}

internal sealed record ProjectTaskPlanInvocation(ProjectTaskPlanCaller Caller, IProjectTaskPlanPublicationAuthority Authority,
    IProjectTaskPlanReceiptAccess ReceiptAccess);
internal sealed record ProjectTaskPlanReceiptInvocation(ProjectTaskPlanCaller Caller, IProjectTaskPlanReceiptAccess ReceiptAccess);

internal sealed class ProjectTaskPlanAuthorityException(ProjectTaskPlanRefusal reason)
    : InvalidOperationException("The original task-plan authority no longer permits this publication.") {
    internal ProjectTaskPlanRefusal Reason { get; } = reason is ProjectTaskPlanRefusal.AuthorityDenied or ProjectTaskPlanRefusal.SourceChanged or ProjectTaskPlanRefusal.BudgetElapsed
        ? reason : throw new ArgumentOutOfRangeException(nameof(reason));
}
