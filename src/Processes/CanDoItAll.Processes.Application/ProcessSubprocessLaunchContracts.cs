using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public interface IProcessSubprocessLaunchCoordinator
{
    ValueTask<ProcessSubprocessLaunchCoordinatorResult?> TryLaunchAsync(
        ProcessSubprocessLaunchCoordinatorRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessSubprocessLaunchCoordinatorRequest(
    ProcessRuntimeStepAssignment ParentAssignment,
    string DefinitionKey);

public sealed record ProcessSubprocessLaunchCoordinatorResult(
    string DefinitionKey,
    ProcessRunId? ChildRunId,
    string Stage,
    string ParentDeferredOutcomeJson,
    IReadOnlyList<string> ExpectedChildEvidenceRefs,
    IReadOnlyList<string> Warnings);
