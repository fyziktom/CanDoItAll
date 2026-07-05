using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureProcessSubprocessLaunchCoordinator(
    ProjectStructureProcessNodeService processNodeService) : IProcessSubprocessLaunchCoordinator
{
    public async ValueTask<ProcessSubprocessLaunchCoordinatorResult?> TryLaunchAsync(
        ProcessSubprocessLaunchCoordinatorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.DefinitionKey) ||
            !ProcessRuntimeLaunchVariables.TryReadProjectId(
                request.ParentAssignment.LaunchVariables,
                out var projectId))
        {
            return null;
        }

        var launch = await processNodeService
            .StartSubprocessAsync(
                projectId,
                request.ParentAssignment.RunId.ToString(),
                request.ParentAssignment.StepInstanceId.ToString(),
                new ProjectStructureProcessSubprocessLaunchInput(
                    request.DefinitionKey,
                    Execute: true,
                    IncludeLaunchPlan: true,
                    RequestedBy: "process-runtime-subprocess-coordinator"),
                BuildAgentContext(request.ParentAssignment.LaunchVariables),
                cancellationToken)
            .ConfigureAwait(false);

        return new ProcessSubprocessLaunchCoordinatorResult(
            launch.DefinitionKey,
            launch.RunId.HasValue ? new ProcessRunId(launch.RunId.Value) : null,
            launch.Stage,
            launch.ParentDeferredOutcomeJson,
            launch.ExpectedChildEvidenceRefs,
            launch.Warnings);
    }

    private static ProjectStructureAgentContext BuildAgentContext(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        return new ProjectStructureAgentContext(
            ResolveLaunchVariable(launchVariables, "AgentId", "process-runtime"),
            ResolveLaunchVariable(launchVariables, "AgentName", "Process runtime"),
            ResolveLaunchVariable(launchVariables, "MachineName", Environment.MachineName),
            ResolveLaunchVariable(launchVariables, "RepositoryRoot", string.Empty),
            ResolveLaunchVariable(launchVariables, "BranchName", string.Empty),
            ResolveLaunchVariable(launchVariables, "SessionId", string.Empty));
    }

    private static string ResolveLaunchVariable(
        IReadOnlyDictionary<string, string> launchVariables,
        string key,
        string defaultValue)
    {
        return launchVariables.TryGetValue(key, out var value) &&
               !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : defaultValue;
    }
}
