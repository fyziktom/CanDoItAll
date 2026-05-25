namespace CanDoItAll.Modules.Processes;

public enum ProcessDefinitionLintSeverity
{
    Info,
    Warning,
    Error
}

public enum ProcessDefinitionLintMode
{
    Advisory,
    Strict
}

public sealed record ProcessDefinitionLintIssue(
    string Code,
    ProcessDefinitionLintSeverity Severity,
    string Message,
    Guid? StepId,
    string StepTitle,
    string Suggestion = "");

public sealed record ProcessDefinitionLintResult(
    IReadOnlyList<ProcessDefinitionLintIssue> Issues,
    ProcessDefinitionLintMode Mode = ProcessDefinitionLintMode.Advisory)
{
    public static ProcessDefinitionLintResult Empty { get; } = new([]);

    public bool HasErrors => Issues.Any(issue => issue.Severity == ProcessDefinitionLintSeverity.Error);

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
            {
                var suggestion = string.IsNullOrWhiteSpace(issue.Suggestion)
                    ? string.Empty
                    : $" Suggested fix: {issue.Suggestion}";
                return $"{issue.Severity}: {issue.Code}: {issue.StepTitle}: {issue.Message}{suggestion}";
            }));
    }
}

public static class ProcessDefinitionLinter
{
    public static ProcessDefinitionLintResult Analyze(
        ProcessDefinitionEditorModel model,
        ProcessDefinitionLintMode mode = ProcessDefinitionLintMode.Advisory)
    {
        ArgumentNullException.ThrowIfNull(model);

        var issues = new List<ProcessDefinitionLintIssue>();
        var rolesById = model.Roles
            .Where(role => role.Id.HasValue)
            .ToDictionary(role => role.Id!.Value);

        foreach (var step in model.Steps)
        {
            AddBoundaryIssues(issues, step, mode);
            AddWorkflowArtifactIssues(issues, step, rolesById);
            AddSubprocessArtifactIssues(issues, step);
            AddBranchOutcomeIssues(issues, step, mode);
            AddArtifactContractIssues(issues, step);
        }

        return new ProcessDefinitionLintResult(issues, mode);
    }

    private static void AddBoundaryIssues(
        List<ProcessDefinitionLintIssue> issues,
        ProcessStepEditorModel step,
        ProcessDefinitionLintMode mode)
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
        var hasProductMutationTargetSignal = HasProductMutationTargetSignal(text);
        if (isAnalysisBoundary && hasImplementationDemand && hasProductMutationTargetSignal)
        {
            AddIssue(
                issues,
                step,
                "processes.lint.step-boundary-ambiguous",
                ProcessDefinitionLintSeverity.Warning,
                "This step reads like an analysis/review boundary but also demands product implementation deliverables. Split architecture/decision work from product mutation or make the mutation step explicit.",
                "Move product mutation into a separate Work step, or state an explicit operation contract such as allowed operation MutateProductTarget and target scope ExternalProductTargetMutable.");
        }

        var requiresExplicitOperationContract = hasImplementationDemand &&
            hasProductMutationTargetSignal;
        var hasTypedOperationContract = HasTypedOperationContract(step);
        var hasPartialTypedOperationContract = HasPartialTypedOperationContract(step);
        if (hasPartialTypedOperationContract)
        {
            AddIssue(
                issues,
                step,
                "processes.lint.step-operation-contract-partial",
                StrictSeverity(mode),
                "This step has only part of the persisted operation contract. Runtime boundaries need both allowed operations and target scope.",
                "Set both allowed operations and target scope in the step operation contract.");
        }

        if (!requiresExplicitOperationContract || hasTypedOperationContract || hasPartialTypedOperationContract)
        {
            return;
        }

        if (HasExplicitOperationContractText(text))
        {
            AddIssue(
                issues,
                step,
                "processes.lint.step-operation-contract-inferred",
                ProcessDefinitionLintSeverity.Warning,
                "This step relies on a text-inferred operation contract. Persisted allowed operations and target scope are required for a durable runtime boundary.",
                "Move the operation contract into the typed allowed operations and target scope fields.");
            return;
        }

        AddIssue(
            issues,
            step,
            "processes.lint.step-operation-contract-missing",
            StrictSeverity(mode),
            "This step can affect a product target but does not declare an explicit operation contract or target scope.",
            "Add explicit allowed operations and target scope in the step notes or contract, for example WriteManagedProcessArtifact for report-only work or MutateProductTarget with a grounded mutable product target.");
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
                "Workflow-backed steps should declare required process artifacts so workflow completion cannot bypass process-owned finalization.",
                "Add at least one required artifact expectation that maps the workflow output into the process artifact contract.");
            return;
        }

        if (requiredArtifacts.Any(item => string.IsNullOrWhiteSpace(item.ValidationRequirementSummary)))
        {
            AddIssue(
                issues,
                step,
                "processes.lint.workflow-artifact-validation-weak",
                ProcessDefinitionLintSeverity.Warning,
                "Workflow-backed required artifacts need validation requirements so projected workflow output can be checked against the process contract.",
                "Fill validation requirements with the expected output shape, producer, and acceptance criteria.");
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
                "Subprocess parent required artifacts depend on child artifact projection. Verify child output contracts produce matching artifacts; source-less projection is not satisfying evidence.",
                "Align child process artifact titles/kinds with the parent expectation and require source-run provenance.");
        }
    }

    private static void AddBranchOutcomeIssues(
        List<ProcessDefinitionLintIssue> issues,
        ProcessStepEditorModel step,
        ProcessDefinitionLintMode mode)
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
                "This governed decision/review step mentions approval, no-go, repair, or escalation but has no branch outcomes for disposition routing.",
                "Add explicit branch outcomes such as approved, repair-required, no-go, or escalation and route each outcome to the next step.");
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
                "Branch outcomes should use clear keys/titles such as approved, repair-required, no-go, or escalation so disposition routing is deterministic.",
                "Replace ambiguous branch labels with stable outcome keys and titles.");
        }

        var hasNegativeBranchOutcome = step.BranchOutcomes.Any(IsNegativeDispositionBranchOutcome);
        var producesRequiredArtifact = step.ArtifactExpectations.Any(artifact => artifact.IsRequired);
        if (hasNegativeBranchOutcome &&
            producesRequiredArtifact &&
            !HasArtifactRecoveryPolicyText(step))
        {
            AddIssue(
                issues,
                step,
                "processes.lint.artifact-recovery-policy-missing",
                StrictSeverity(mode),
                "This artifact-producing step has a negative disposition branch but no artifact recovery policy. Missing artifact production can be mistaken for a valid disposition.",
                "State how missing or invalid required artifacts are recovered or blocked before branch routing, or move the negative disposition to a separate review/approval step.");
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
                    "Decision-log artifacts should not be mixed with runtime proof requirements. Use a separate evidence artifact for runtime proof.",
                    "Split legal/approval decision logs from operational evidence or runtime proof artifacts.");
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

    private static bool IsNegativeDispositionBranchOutcome(ProcessStepBranchOutcomeEditorModel outcome)
    {
        var text = NormalizeText($"{outcome.Key} {outcome.Title} {outcome.Description}");
        return ContainsAny(text, "repair", "rework", "reject", "no-go", "nogo", "escalat", "blocked", "failed");
    }

    private static bool HasArtifactRecoveryPolicyText(ProcessStepEditorModel step)
    {
        var text = NormalizeText(string.Join(
            " ",
            step.Notes,
            step.ExceptionPolicySummary,
            step.EvidenceContractSummary,
            step.OutputContractSummary,
            string.Join(" ", step.ArtifactExpectations.Select(item => item.ValidationRequirementSummary))));
        return ContainsAny(text, "artifact recovery", "recover missing artifact", "missing artifact", "materialization", "required artifact failure", "block when artifact");
    }

    private static bool HasExplicitOperationContractText(string text)
    {
        return ContainsAny(
            text,
            "operation contract",
            "allowed operation",
            "allowed operations",
            "target scope",
            "writemanagedprocessartifact",
            "mutateproducttarget",
            "externalproducttargetmutable",
            "managedprocessartifactsonly");
    }

    private static bool HasTypedOperationContract(ProcessStepEditorModel step)
    {
        return step.AllowedOperations.Count > 0 &&
            step.OperationTargetScope.HasValue;
    }

    private static bool HasPartialTypedOperationContract(ProcessStepEditorModel step)
    {
        var hasAllowedOperations = step.AllowedOperations.Count > 0;
        return hasAllowedOperations != step.OperationTargetScope.HasValue;
    }

    private static bool HasProductMutationTargetSignal(string text)
    {
        return ContainsAny(
            text,
            "product root",
            "product file",
            "product files",
            "product target",
            "source file",
            "source files",
            "source root",
            "target app",
            "requested app",
            "web app",
            "console app",
            "app project",
            "project file",
            "solution file",
            "codebase",
            "repository",
            "runnable",
            "implementation change set",
            "implementation change",
            "deliverable files",
            ".csproj",
            ".sln",
            "blazor",
            ".net",
            "javascript",
            "typescript");
    }

    private static ProcessDefinitionLintSeverity StrictSeverity(ProcessDefinitionLintMode mode)
    {
        return mode == ProcessDefinitionLintMode.Strict
            ? ProcessDefinitionLintSeverity.Error
            : ProcessDefinitionLintSeverity.Warning;
    }

    private static void AddIssue(
        List<ProcessDefinitionLintIssue> issues,
        ProcessStepEditorModel step,
        string code,
        ProcessDefinitionLintSeverity severity,
        string message,
        string suggestion = "")
    {
        issues.Add(new ProcessDefinitionLintIssue(
            code,
            severity,
            message,
            step.Id,
            string.IsNullOrWhiteSpace(step.Title) ? "(untitled step)" : step.Title.Trim(),
            suggestion));
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
