using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;

namespace CanDoItAll.Processes.Application;

public interface IProcessLaunchArtifactInitializer
{
    Task InitializeAsync(
        ProcessLaunchArtifactInitializationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessLaunchArtifactInitializationRequest(
    ProcessRunId RunId,
    ProcessDefinitionId DefinitionId,
    ProcessInstancePlanId PlanId,
    string DefinitionKey,
    Guid? ProjectId,
    string ManagedArtifactRoot);
