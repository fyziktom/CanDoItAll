namespace CanDoItAll.Modules.Processes;

public enum ProcessDefinitionLintSeverity
{
    Info,
    Warning,
    Error
}

public sealed record ProcessDefinitionLintIssue(
    string Code,
    ProcessDefinitionLintSeverity Severity,
    string Message,
    Guid? StepId,
    string StepTitle);

public sealed record ProcessDefinitionLintResult(IReadOnlyList<ProcessDefinitionLintIssue> Issues)
{
    public bool HasWarningsOrErrors => Issues.Any(issue => issue.Severity is ProcessDefinitionLintSeverity.Warning or ProcessDefinitionLintSeverity.Error);

    public string BuildDryRunSummary()
    {
        if (Issues.Count == 0)
        {
            return "Process definition lint dry-run found no execution-boundary, artifact, branch, role, workflow, or subprocess warnings.";
        }

        return string.Join(
            Environment.NewLine,
            Issues.Select(issue =>
                $"{issue.Severity}: {issue.Code}: {issue.StepTitle}: {issue.Message}"));
    }
}

public static class ProcessDefinitionLinter
{
    public static ProcessDefinitionLintResult Analyze(ProcessDefinitionEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var issues = new List<ProcessDefinitionLintIssue>();
        var rolesById = model.Roles
            .Where(role => role.Id.HasValue)
            .ToDictionary(role => role.Id!.Value);

        foreach (var step in model.Steps)
        {
            AddBoundaryIssues(issues, step);
            AddWorkflowArtifactIssues(issues, step, rolesById);
            AddSubprocessArtifactIssues(issues, step);
            AddBranchOutcomeIssues(issues, step);
            AddArtifactContractIssues(issues, step);
        }

        return new ProcessDefinitionLintResult(issues);
    }

    private static void AddBoundaryIssues(
        List<ProcessDefinitionLintIssue> issues,
        ProcessStepEditorModel step)
    {
        var text = NormalizeText(string.Join(
            " ",
            step.Title,
            step.Notes,
            step.InputContractSummary,
            step.OutputContractSummary,
            step.EvidenceContractSummary,
            string.Join(" ", step.ArtifactExpectations.Select(item => $"{item.ArtifactKind} {item.Title} {item.ValidationRequirementSummary}"))));
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var isAnalysisBoundary = step.StepKind is ProcessStepKind.Start or ProcessStepKind.Decision or ProcessStepKind.Approval or ProcessStepKind.Review ||
            ContainsAny(text, "architecture", "design", "scope", "planning", "analysis", "decision", "approval", "review");
        var hasImplementationDemand = ContainsAny(text, "implement", "implementation", "build", "create", "generate", "scaffold", "repair", "fix", "mutate");
        var hasProductDeliverable = step.ArtifactExpectations.Any(item => item.ArtifactKind == ProcessArtifactKind.Deliverable);
        if (isAnalysisBoundary && hasImplementationDemand && hasProductDeliverable)
        {
            AddIssue(
                issues,
                step,
                "processes.lint.step-boundary-ambiguous",
                ProcessDefinitionLintSeverity.Warning,
                "This step reads like an analysis/review boundary but also demands product implementation deliverables. Split architecture/decision work from product mutation or make the mutation step explicit.");
        }
    }

    private static void AddWorkflowArtifactIssues(
        List<ProcessDefinitionLintIssue> issues,
        ProcessStepEditorModel step,
        IReadOnlyDictionary<Guid, ProcessRoleEditorModel> rolesById)
    {
        var hasWorkflowExecutor = step.RoleAssignments.Any(assignment =>
            assignment.RoleRequirementId.HasValue &&
            rolesById.TryGetValue(assignment.RoleRequirementId.Value, out var role) &&
            (ProcessExecutorKindNames.IsWorkflow(role.PreferredExecutorKind) ||
             role.PreferredWorkflowDefinitionId.HasValue));
        if (!hasWorkflowExecutor)
        {
            return;
        }

        var requiredArtifacts = step.ArtifactExpectations.Where(item => item.IsRequired).ToList();
        if (requiredArtifacts.Count == 0)
        {
            AddIssue(
                issues,
                step,
                "processes.lint.workflow-artifact-contract-missing",
                ProcessDefinitionLintSeverity.Warning,
                "Workflow-backed steps should declare required process artifacts so workflow completion cannot bypass process-owned finalization.");
            return;
        }

        if (requiredArtifacts.Any(item => string.IsNullOrWhiteSpace(item.ValidationRequirementSummary)))
        {
            AddIssue(
                issues,
                step,
                "processes.lint.workflow-artifact-validation-weak",
                ProcessDefinitionLintSeverity.Warning,
                "Workflow-backed required artifacts need validation requirements so projected workflow output can be checked against the process contract.");
        }
    }

    private static void AddSubprocessArtifactIssues(
        List<ProcessDefinitionLintIssue> issues,
        ProcessStepEditorModel step)
    {
        if (step.StepKind != ProcessStepKind.Subprocess)
        {
            return;
        }

        if (step.ArtifactExpectations.Any(item => item.IsRequired))
        {
            AddIssue(
                issues,
                step,
                "processes.lint.subprocess-parent-artifact-mapping-review",
                ProcessDefinitionLintSeverity.Warning,
                "Subprocess parent required artifacts depend on child artifact projection. Verify child output contracts produce matching artifacts; source-less projection is not satisfying evidence.");
        }
    }

    private static void AddBranchOutcomeIssues(
        List<ProcessDefinitionLintIssue> issues,
        ProcessStepEditorModel step)
    {
        var text = NormalizeText(string.Join(" ", step.Title, step.Notes, step.DecisionRightsSummary, step.OutputContractSummary));
        var needsNegativeDispositionRoute = ContainsAny(text, "approve", "approval", "no-go", "nogo", "reject", "repair", "rework", "escalate", "escalation");
        if (needsNegativeDispositionRoute && step.BranchOutcomes.Count == 0)
        {
            AddIssue(
                issues,
                step,
                "processes.lint.branch-outcome-missing",
                ProcessDefinitionLintSeverity.Warning,
                "This governed decision/review step mentions approval, no-go, repair, or escalation but has no branch outcomes for disposition routing.");
        }

        var ambiguousOutcomes = step.BranchOutcomes
            .Where(outcome => string.IsNullOrWhiteSpace(outcome.Key) && IsAmbiguousBranchOutcome(outcome))
            .ToList();
        if (ambiguousOutcomes.Count > 0)
        {
            AddIssue(
                issues,
                step,
                "processes.lint.branch-outcome-ambiguous",
                ProcessDefinitionLintSeverity.Warning,
                "Branch outcomes should use clear keys/titles such as approved, repair-required, no-go, or escalation so disposition routing is deterministic.");
        }
    }

    private static void AddArtifactContractIssues(
        List<ProcessDefinitionLintIssue> issues,
        ProcessStepEditorModel step)
    {
        foreach (var artifact in step.ArtifactExpectations)
        {
            var text = NormalizeText($"{artifact.Title} {artifact.ValidationRequirementSummary} {artifact.AllowedFutureUsageSummary}");
            if (artifact.ArtifactKind == ProcessArtifactKind.Decision &&
                ContainsAny(text, "decision log", "legal log", "approval log") &&
                ContainsAny(text, "runtime", "browser proof", "test output", "build output"))
            {
                AddIssue(
                    issues,
                    step,
                    "processes.lint.decision-log-runtime-proof-conflict",
                    ProcessDefinitionLintSeverity.Warning,
                    "Decision-log artifacts should not be mixed with runtime proof requirements. Use a separate evidence artifact for runtime proof.");
            }
        }
    }

    private static bool IsAmbiguousBranchOutcome(ProcessStepBranchOutcomeEditorModel outcome)
    {
        var text = NormalizeText($"{outcome.Title} {outcome.Description}");
        return string.IsNullOrWhiteSpace(text) ||
               text is "yes" or "no" or "ok" or "done" ||
               ContainsAny(text, "maybe", "other", "path");
    }

    private static void AddIssue(
        List<ProcessDefinitionLintIssue> issues,
        ProcessStepEditorModel step,
        string code,
        ProcessDefinitionLintSeverity severity,
        string message)
    {
        issues.Add(new ProcessDefinitionLintIssue(
            code,
            severity,
            message,
            step.Id,
            string.IsNullOrWhiteSpace(step.Title) ? "(untitled step)" : step.Title.Trim()));
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        return tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}
