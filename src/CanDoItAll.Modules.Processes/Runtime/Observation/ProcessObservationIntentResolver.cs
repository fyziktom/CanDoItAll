namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessObservationIntentResolver(
    ProcessesService processesService) : IProcessObservationIntentResolver
{
    public async Task<ProcessObservationIntentPlan> ResolveAsync(
        ProcessObservationIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (intent.FocusKind is ProcessObservationFocusKind.AgentExecution or
            ProcessObservationFocusKind.Escalation or
            ProcessObservationFocusKind.Outbox or
            ProcessObservationFocusKind.QualityReview or
            ProcessObservationFocusKind.Run or
            ProcessObservationFocusKind.Stage or
            ProcessObservationFocusKind.Timeline or
            ProcessObservationFocusKind.Dashboard)
        {
            return await ResolveReadOnlyIntentAsync(intent, cancellationToken);
        }

        return ProcessObservationIntentPlan.Unsupported("The requested process observation focus is not supported.");
    }

    private async Task<ProcessObservationIntentPlan> ResolveReadOnlyIntentAsync(
        ProcessObservationIntent intent,
        CancellationToken cancellationToken)
    {
        if (intent.ProcessRunId.HasValue)
        {
            return new ProcessObservationIntentPlan(
                ProcessObservationIntentResolutionStatus.Resolved,
                intent.FocusKind,
                intent.ProcessDefinitionId,
                intent.ProcessRunId,
                intent.StepRunId,
                [BuildDescriptor(intent, intent.ProcessRunId.Value, intent.StepRunId)],
                "Resolved to the requested process run observation.");
        }

        if (!intent.ProcessDefinitionId.HasValue)
        {
            return ProcessObservationIntentPlan.Ambiguous("Select a process definition or run before focusing the process dashboard.");
        }

        var runs = await processesService.ListRunsAsync(
            intent.ProcessDefinitionId,
            intent.ProjectId,
            cancellationToken);
        if (runs.Count == 0)
        {
            return ProcessObservationIntentPlan.Ambiguous("The selected process definition has no runtime runs to observe.");
        }

        var activeRuns = new List<ProcessRunListItem>();
        ProcessRunListItem? latestRun = null;
        foreach (var runItem in runs)
        {
            if (latestRun is null || runItem.UpdatedAtUtc > latestRun.UpdatedAtUtc)
            {
                latestRun = runItem;
            }

            if (runItem.Status == ProcessRunStatus.Active)
            {
                activeRuns.Add(runItem);
            }
        }

        var candidateRuns = activeRuns.Count == 0
            ? [latestRun!]
            : activeRuns;
        if (!TryResolveCandidateRun(candidateRuns, intent.SearchText, out var selectedRun, out var message))
        {
            return ProcessObservationIntentPlan.Ambiguous(message);
        }

        return new ProcessObservationIntentPlan(
            ProcessObservationIntentResolutionStatus.Resolved,
            intent.FocusKind,
            intent.ProcessDefinitionId,
            selectedRun.Id,
            intent.StepRunId,
            [BuildDescriptor(intent, selectedRun.Id, intent.StepRunId)],
            "Resolved to the most relevant process run observation.");
    }

    private static bool TryResolveCandidateRun(
        IReadOnlyList<ProcessRunListItem> candidateRuns,
        string? searchText,
        out ProcessRunListItem run,
        out string message)
    {
        if (candidateRuns.Count == 1)
        {
            run = candidateRuns[0];
            message = string.Empty;
            return true;
        }

        if (string.IsNullOrWhiteSpace(searchText))
        {
            run = default!;
            message = "Multiple active process runs match. Select a run before opening focused details.";
            return false;
        }

        var trimmedSearch = searchText.Trim();
        var matchingRuns = new List<ProcessRunListItem>();
        foreach (var item in candidateRuns)
        {
            if (item.Name.Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase) ||
                item.Id.ToString("N").Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase))
            {
                matchingRuns.Add(item);
            }
        }

        if (matchingRuns.Count == 1)
        {
            run = matchingRuns[0];
            message = string.Empty;
            return true;
        }

        run = default!;
        message = matchingRuns.Count == 0
            ? "No active process run matched the requested observation text."
            : "Multiple active process runs matched the requested observation text.";
        return false;
    }

    private static ProcessObservationDialogDescriptor BuildDescriptor(
        ProcessObservationIntent intent,
        Guid runId,
        Guid? stepRunId)
    {
        var kind = intent.FocusKind switch
        {
            ProcessObservationFocusKind.Stage or ProcessObservationFocusKind.QualityReview when stepRunId.HasValue =>
                ProcessObservationDialogKind.StageDetails,
            ProcessObservationFocusKind.Timeline => ProcessObservationDialogKind.Timeline,
            ProcessObservationFocusKind.AgentExecution => ProcessObservationDialogKind.AgentExecution,
            ProcessObservationFocusKind.Escalation => ProcessObservationDialogKind.Escalation,
            ProcessObservationFocusKind.Outbox => ProcessObservationDialogKind.Outbox,
            _ => ProcessObservationDialogKind.RunSteps
        };

        return new ProcessObservationDialogDescriptor(
            kind,
            intent.FocusKind,
            runId,
            stepRunId,
            ResolveDescriptorTitle(intent.FocusKind),
            string.IsNullOrWhiteSpace(intent.SearchText)
                ? "Read-only process observation"
                : intent.SearchText.Trim());
    }

    private static string ResolveDescriptorTitle(ProcessObservationFocusKind focusKind)
    {
        return focusKind switch
        {
            ProcessObservationFocusKind.QualityReview => "Quality review details",
            ProcessObservationFocusKind.Stage => "Stage details",
            ProcessObservationFocusKind.Timeline => "Runtime timeline",
            ProcessObservationFocusKind.AgentExecution => "Agent execution details",
            ProcessObservationFocusKind.Escalation => "Escalation details",
            ProcessObservationFocusKind.Outbox => "Outbox details",
            _ => "Run details"
        };
    }
}
