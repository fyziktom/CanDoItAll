using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    public Task<Result<Guid>> StartRunFromTriggerAsync(
        ProcessRunTriggerStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = ValidateTriggerStartRequest(request);
        if (errors.Count > 0)
        {
            return Task.FromResult(Result<Guid>.Failure(errors));
        }

        return StartRunAsync(
            new ProcessRunStartRequest
            {
                ProcessDefinitionId = request.ProcessDefinitionId,
                ProjectId = request.ProjectId,
                RunName = request.RunName,
                OperatingMode = request.OperatingMode,
                TriggerReason = BuildTriggerStartReason(request),
                ProjectStructureContext = request.ProjectStructureContext,
                LintMode = request.LintMode
            },
            cancellationToken);
    }

    private static IReadOnlyList<Error> ValidateTriggerStartRequest(ProcessRunTriggerStartRequest request)
    {
        var errors = new List<Error>();
        if (!Enum.IsDefined(typeof(ProcessRunTriggerSourceKind), request.TriggerSourceKind))
        {
            errors.Add(Error.Validation(
                "Process trigger source kind is not supported.",
                "processes.run.trigger-source-kind-unsupported"));
        }

        if (string.IsNullOrWhiteSpace(request.RequestedBy))
        {
            errors.Add(Error.Validation(
                "Process trigger requester is required.",
                "processes.run.trigger-requested-by-required"));
        }

        if (RequiresTriggerSourceId(request.TriggerSourceKind) &&
            (!request.TriggerSourceId.HasValue || request.TriggerSourceId.Value == Guid.Empty))
        {
            errors.Add(Error.Validation(
                "Process trigger source id is required for scheduler and workflow starts.",
                "processes.run.trigger-source-id-required"));
        }

        return errors;
    }

    private static bool RequiresTriggerSourceId(ProcessRunTriggerSourceKind triggerSourceKind)
    {
        return triggerSourceKind is ProcessRunTriggerSourceKind.SchedulerPlan
            or ProcessRunTriggerSourceKind.WorkflowRun;
    }

    private static string BuildTriggerStartReason(ProcessRunTriggerStartRequest request)
    {
        var baseReason = string.IsNullOrWhiteSpace(request.TriggerReason)
            ? "Process run started from a manual trigger path."
            : request.TriggerReason.Trim();
        var sourceSummary = BuildTriggerSourceSummary(request);
        return $"{baseReason} Trigger source: {sourceSummary}. Requested by {request.RequestedBy.Trim()}.";
    }

    private static string BuildTriggerSourceSummary(ProcessRunTriggerStartRequest request)
    {
        var sourceName = string.IsNullOrWhiteSpace(request.TriggerSourceName)
            ? string.Empty
            : $" '{request.TriggerSourceName.Trim()}'";

        return request.TriggerSourceId.HasValue && request.TriggerSourceId.Value != Guid.Empty
            ? $"{request.TriggerSourceKind}{sourceName} ({request.TriggerSourceId.Value:D})"
            : $"{request.TriggerSourceKind}{sourceName}";
    }
}
