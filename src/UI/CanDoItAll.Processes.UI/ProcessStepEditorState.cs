using System.Globalization;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Processes.Projections;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Processes.UI;

public sealed class ProcessStepEditorState {

    public ProcessDefinitionStepEditorProjection StepEditor { get; set; } = default!;

    public ProcessWorkspaceShellScope Scope { get; set; } = default!;

    public bool Disabled { get; set; }

    public EventCallback<ProcessDefinitionStepEditorCommand> ExecuteCommand { get; set; }

    public ProcessDefinitionStepEditorVersionToken? SyncedVersionToken { get; set; }
    public ProcessDefinitionStepKey? SelectedStepKey { get; set; }
    public string? CommandNotice { get; set; }
    public string StepTitle { get; set; } = string.Empty;
    public string StepSubtitle { get; set; } = string.Empty;
    public string StepNotes { get; set; } = string.Empty;
    public ProcessDefinitionStepKind StepKind { get; set; } = ProcessDefinitionStepKind.Work;
    public string TargetLeadHoursInput { get; set; } = "0";
    public int TargetLeadHours => int.Parse(TargetLeadHoursInput, NumberStyles.Integer, CultureInfo.InvariantCulture);
    public bool AllowsManualSkip { get; set; }
    public bool AllowsSafeRefusal { get; set; }
    public bool RequiresApproval { get; set; }
    public bool RequiresDecisionRecord { get; set; }
    public ProcessDefinitionStepTargetScopeKind OperationTargetScope { get; set; } = ProcessDefinitionStepTargetScopeKind.Unspecified;
    public HashSet<ProcessDefinitionStepOperationKind> SelectedOperations { get; set; } = [];
    public string InputContractSummary { get; set; } = string.Empty;
    public string OutputContractSummary { get; set; } = string.Empty;
    public string EvidenceContractSummary { get; set; } = string.Empty;
    public string DecisionRightsSummary { get; set; } = string.Empty;
    public string ExceptionPolicySummary { get; set; } = string.Empty;
    public string SubprocessProcessKey { get; set; } = string.Empty;
    public string SubprocessSnapshotName { get; set; } = string.Empty;
    public List<ProcessDefinitionBranchOutcomeProjection> BranchOutcomes { get; set; } = [];
    public List<ProcessDefinitionStepRoleBindingProjection> RoleBindings { get; set; } = [];
    public List<ProcessDefinitionArtifactExpectationProjection> ArtifactExpectations { get; set; } = [];

    public static readonly ProcessDefinitionStepKind[] StepKindOptions =
    [
        ProcessDefinitionStepKind.Start,
        ProcessDefinitionStepKind.Work,
        ProcessDefinitionStepKind.Decision,
        ProcessDefinitionStepKind.Review,
        ProcessDefinitionStepKind.Approval,
        ProcessDefinitionStepKind.Delivery,
        ProcessDefinitionStepKind.Subprocess,
        ProcessDefinitionStepKind.End
    ];

    public static readonly ProcessDefinitionStepTargetScopeKind[] TargetScopeOptions =
    [
        ProcessDefinitionStepTargetScopeKind.Unspecified,
        ProcessDefinitionStepTargetScopeKind.ManagedProcessArtifactsOnly,
        ProcessDefinitionStepTargetScopeKind.ManagedOutputProduct,
        ProcessDefinitionStepTargetScopeKind.ExternalArtifactDestination,
        ProcessDefinitionStepTargetScopeKind.ExternalProductTargetReadOnly,
        ProcessDefinitionStepTargetScopeKind.ExternalProductTargetMutable,
        ProcessDefinitionStepTargetScopeKind.ExternalActionControlled
    ];

    public static readonly ProcessDefinitionStepOperationKind[] OperationOptions =
    [
        ProcessDefinitionStepOperationKind.ReadProcessContext,
        ProcessDefinitionStepOperationKind.ReadProjectStructure,
        ProcessDefinitionStepOperationKind.ReadUpstreamArtifacts,
        ProcessDefinitionStepOperationKind.WriteManagedProcessArtifacts,
        ProcessDefinitionStepOperationKind.WriteExternalArtifactDestination,
        ProcessDefinitionStepOperationKind.MutateProductTarget,
        ProcessDefinitionStepOperationKind.RunValidation,
        ProcessDefinitionStepOperationKind.LaunchRuntime,
        ProcessDefinitionStepOperationKind.CaptureRuntimeProof,
        ProcessDefinitionStepOperationKind.ExecuteExternalAction,
        ProcessDefinitionStepOperationKind.RecoverArtifactsOnly,
        ProcessDefinitionStepOperationKind.EscalateOrDecide
    ];

    public static readonly ProcessDefinitionRouteTargetKind[] RouteTargetOptions =
    [
        ProcessDefinitionRouteTargetKind.NextStep,
        ProcessDefinitionRouteTargetKind.SpecificStep,
        ProcessDefinitionRouteTargetKind.PreviousStep,
        ProcessDefinitionRouteTargetKind.SubprocessStart,
        ProcessDefinitionRouteTargetKind.SubprocessResume,
        ProcessDefinitionRouteTargetKind.WaitForArtifact,
        ProcessDefinitionRouteTargetKind.WaitForUser,
        ProcessDefinitionRouteTargetKind.Escalate,
        ProcessDefinitionRouteTargetKind.CompleteRun,
        ProcessDefinitionRouteTargetKind.FailRun,
        ProcessDefinitionRouteTargetKind.CancelRun
    ];

    public static readonly ProcessDefinitionArtifactKind[] ArtifactKindOptions =
    [
        ProcessDefinitionArtifactKind.Brief,
        ProcessDefinitionArtifactKind.Checklist,
        ProcessDefinitionArtifactKind.Dataset,
        ProcessDefinitionArtifactKind.Decision,
        ProcessDefinitionArtifactKind.DecisionRecord,
        ProcessDefinitionArtifactKind.Deliverable,
        ProcessDefinitionArtifactKind.Evidence,
        ProcessDefinitionArtifactKind.Prompt,
        ProcessDefinitionArtifactKind.Report,
        ProcessDefinitionArtifactKind.Transcript
    ];

    public void Observe() {
        if (definitionKey != StepEditor.DefinitionKey || observedScope != Scope) {
            definitionKey = StepEditor.DefinitionKey;
            observedScope = Scope;
            SelectedStepKey = StepEditor.SelectedStepKey;
            baseline = null;
            submission = null;
            SyncedVersionToken = null;
            HasConflict = false;
        }
        CommandNotice = StepEditor.LastCommandReceipt?.Summary;
        if (SyncedVersionToken == StepEditor.VersionToken) {
            return;
        }
        if (IsDirty) {
            HasConflict = true;
            return;
        }
        SyncedVersionToken = StepEditor.VersionToken;
        SelectedStepKey ??= StepEditor.SelectedStepKey;
        SyncSelectedStep();
    }

    public void SelectStep(ProcessDefinitionStepKey stepKey) {
        if (SelectedStepKey == stepKey) {
            return;
        }
        submission = null;
        SelectedStepKey = stepKey;
        SyncedVersionToken = StepEditor.VersionToken;
        HasConflict = false;
        SyncSelectedStep();
    }

    public void SyncSelectedStep() {
        var draft = SelectedStepKey is { } stepKey
            ? ResolveDraft(stepKey)
            : StepEditor.SelectedStep;
        if (draft is null) {
            return;
        }

        SelectedStepKey = draft.Basic.StepKey;
        StepTitle = draft.Basic.Title;
        StepSubtitle = draft.Basic.Subtitle;
        StepNotes = draft.Basic.Notes;
        StepKind = draft.Basic.StepKind == ProcessDefinitionStepKind.Unspecified
            ? ProcessDefinitionStepKind.Work
            : draft.Basic.StepKind;
        TargetLeadHoursInput = draft.Basic.TargetLeadHours.ToString(CultureInfo.InvariantCulture);
        AllowsManualSkip = draft.Basic.AllowsManualSkip;
        AllowsSafeRefusal = draft.Basic.AllowsSafeRefusal;
        RequiresApproval = draft.Basic.RequiresApproval;
        RequiresDecisionRecord = draft.Basic.RequiresDecisionRecord;
        OperationTargetScope = draft.OperationContract.TargetScope;
        SelectedOperations = draft.OperationContract.AllowedOperations.ToHashSet();
        InputContractSummary = draft.Contracts.InputContractSummary;
        OutputContractSummary = draft.Contracts.OutputContractSummary;
        EvidenceContractSummary = draft.Contracts.EvidenceContractSummary;
        DecisionRightsSummary = draft.Contracts.DecisionRightsSummary;
        ExceptionPolicySummary = draft.Contracts.ExceptionPolicySummary;
        BranchOutcomes = draft.BranchOutcomes.ToList();
        RoleBindings = draft.RoleBindings.ToList();
        ArtifactExpectations = draft.ArtifactExpectations.ToList();
        SubprocessProcessKey = draft.SubprocessMapping.ProcessKey;
        SubprocessSnapshotName = draft.SubprocessMapping.DefinitionSnapshotName;
        BranchNumberInputs.Clear();
        ArtifactNumberInputs.Clear();
        baseline = CaptureInputs();
    }

    public ProcessDefinitionStepDraftProjection? ResolveDraft(ProcessDefinitionStepKey stepKey)
        => StepEditor.SelectedStep?.Basic.StepKey == stepKey
            ? StepEditor.SelectedStep
            : StepEditor.StepDrafts.FirstOrDefault(step => step.Basic.StepKey == stepKey);

    public async Task ExecuteAsync(ProcessDefinitionStepCommandKind commandKind) {
        if (executing || Disabled || HasInputErrors || SelectedStepKey is null) {
            return;
        }

        var command = new ProcessDefinitionStepEditorCommand(
            Scope,
            StepEditor.DefinitionKey,
            commandKind,
            SyncedVersionToken ?? StepEditor.VersionToken,
            CreateDraft());
        var submitted = CaptureInputs();
        submission = submitted;
        executing = true;
        try {
            await ExecuteCommand.InvokeAsync(command);
        } finally {
            executing = false;
            if (ReferenceEquals(submission, submitted)) {
                submission = null;
            }
        }
    }

    public ProcessDefinitionStepDraftProjection CreateDraft() {
        var stepKey = SelectedStepKey ?? StepEditor.SelectedStep?.Basic.StepKey ?? new ProcessDefinitionStepKey("step-draft");
        return new ProcessDefinitionStepDraftProjection(
            new ProcessDefinitionStepBasicDraftProjection(
                stepKey,
                StepTitle,
                StepSubtitle,
                StepNotes,
                StepKind,
                TargetLeadHours,
                AllowsManualSkip,
                AllowsSafeRefusal,
                RequiresApproval,
                RequiresDecisionRecord,
                StepEditor.SelectedStep?.Basic.DecisionRoleKey),
            new ProcessDefinitionStepOperationContractProjection(
                OperationTargetScope,
                SelectedOperations.OrderBy(operation => operation.ToString(), StringComparer.Ordinal).ToArray()),
            new ProcessDefinitionStepContractsProjection(
                InputContractSummary,
                OutputContractSummary,
                EvidenceContractSummary,
                DecisionRightsSummary,
                ExceptionPolicySummary),
            BranchOutcomes.ToArray(),
            RoleBindings.ToArray(),
            ArtifactExpectations.ToArray(),
            new ProcessDefinitionSubprocessMappingProjection(
                SubprocessProcessKey,
                SubprocessSnapshotName,
                ArtifactExpectations
                    .Where(artifact => !string.IsNullOrWhiteSpace(artifact.SubprocessChildStepKey) ||
                                       !string.IsNullOrWhiteSpace(artifact.SubprocessChildArtifactTitle) ||
                                       artifact.SubprocessChildArtifactExpectationId.HasValue)
                    .ToArray()));
    }

    public void ToggleOperation(
        ProcessDefinitionStepOperationKind operation,
        ChangeEventArgs args) {
        if (ParseBool(args)) {
            SelectedOperations.Add(operation);
            return;
        }

        SelectedOperations.Remove(operation);
    }

    public void UpdateBranchTitle(ProcessDefinitionBranchOutcomeKey key, ChangeEventArgs args) {
        var index = BranchOutcomes.FindIndex(row => row.OutcomeKey == key);
        if (index < 0) {
            return;
        }
        var branch = BranchOutcomes[index];
        BranchOutcomes[index] = branch with { Title = ReadChangeValue(args) };
    }

    public void UpdateBranchRouteTarget(ProcessDefinitionBranchOutcomeKey key, ChangeEventArgs args) {
        var index = BranchOutcomes.FindIndex(row => row.OutcomeKey == key);
        if (index < 0) {
            return;
        }
        var branch = BranchOutcomes[index];
        var targetKind = ParseRouteTargetKind(args);
        BranchOutcomes[index] = branch with {
            RouteTarget = branch.RouteTarget with {
                Kind = targetKind,
                Summary = targetKind.ToString()
            },
            IsBackwardRoute = targetKind == ProcessDefinitionRouteTargetKind.PreviousStep,
            LoopBudget = branch.LoopBudget with {
                IsRequired = targetKind == ProcessDefinitionRouteTargetKind.PreviousStep
            }
        };
    }

    public void UpdateBranchLoopBudget(ProcessDefinitionBranchOutcomeKey key, ChangeEventArgs args) {
        var index = BranchOutcomes.FindIndex(row => row.OutcomeKey == key);
        if (index < 0) {
            return;
        }
        var input = ReadChangeValue(args);
        BranchNumberInputs[key] = input;
        if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || number < 0) {
            return;
        }
        var branch = BranchOutcomes[index];
        BranchOutcomes[index] = branch with {
            LoopBudget = branch.LoopBudget with {
                MaximumRepeats = number
            }
        };
    }

    public void UpdateArtifactTitle(ProcessDefinitionArtifactExpectationKey key, ChangeEventArgs args) {
        var index = ArtifactExpectations.FindIndex(row => row.ArtifactKey == key);
        if (index < 0) {
            return;
        }
        var artifact = ArtifactExpectations[index];
        ArtifactExpectations[index] = artifact with { Title = ReadChangeValue(args) };
    }

    public void UpdateArtifactKind(ProcessDefinitionArtifactExpectationKey key, ChangeEventArgs args) {
        var index = ArtifactExpectations.FindIndex(row => row.ArtifactKey == key);
        if (index < 0) {
            return;
        }
        var artifact = ArtifactExpectations[index];
        ArtifactExpectations[index] = artifact with { ArtifactKind = ParseArtifactKind(args) };
    }

    public void UpdateArtifactRetention(ProcessDefinitionArtifactExpectationKey key, ChangeEventArgs args) {
        var index = ArtifactExpectations.FindIndex(row => row.ArtifactKey == key);
        if (index < 0) {
            return;
        }
        var input = ReadChangeValue(args);
        ArtifactNumberInputs[key] = input;
        if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || number < 0) {
            return;
        }
        var artifact = ArtifactExpectations[index];
        ArtifactExpectations[index] = artifact with { RetentionDays = number };
    }

    public static string ReadChangeValue(ChangeEventArgs args)
        => args.Value?.ToString() ?? string.Empty;

    public static bool ParseBool(ChangeEventArgs args)
        => args.Value is bool value && value;

public static ProcessDefinitionStepKind ParseStepKind(ChangeEventArgs args)
        => Enum.TryParse<ProcessDefinitionStepKind>(ReadChangeValue(args), out var parsed)
            ? parsed
            : ProcessDefinitionStepKind.Work;

    public static ProcessDefinitionStepTargetScopeKind ParseTargetScope(ChangeEventArgs args)
        => Enum.TryParse<ProcessDefinitionStepTargetScopeKind>(ReadChangeValue(args), out var parsed)
            ? parsed
            : ProcessDefinitionStepTargetScopeKind.Unspecified;

    public static ProcessDefinitionRouteTargetKind ParseRouteTargetKind(ChangeEventArgs args)
        => Enum.TryParse<ProcessDefinitionRouteTargetKind>(ReadChangeValue(args), out var parsed)
            ? parsed
            : ProcessDefinitionRouteTargetKind.NextStep;

    public static ProcessDefinitionArtifactKind ParseArtifactKind(ChangeEventArgs args)
        => Enum.TryParse<ProcessDefinitionArtifactKind>(ReadChangeValue(args), out var parsed)
            ? parsed
            : ProcessDefinitionArtifactKind.Unspecified;

    public static string ResolveStepIcon(ProcessDefinitionStepKind kind)
        => kind switch {
            ProcessDefinitionStepKind.Decision => "alt_route",
            ProcessDefinitionStepKind.Review => "rate_review",
            ProcessDefinitionStepKind.Approval => "verified",
            ProcessDefinitionStepKind.Subprocess => "account_tree",
            ProcessDefinitionStepKind.End => "flag",
            _ => "task_alt"
        };

    public static string ResolveLintTone(ProcessDefinitionStepLintProjection lint) {
        if (lint.HasBlockingIssues) {
            return "danger";
        }

        return lint.HasWarningsOrErrors ? "warning" : "success";
    }

    public static string ResolveLintText(ProcessDefinitionStepLintProjection lint) {
        if (lint.HasBlockingIssues) {
            return $"{lint.Issues.Count(issue => issue.Severity == ProcessDefinitionStepLintSeverity.Error)} blocking";
        }

        return lint.HasWarningsOrErrors ? $"{lint.Issues.Count} step lint issue(s)" : "Step lint clear";
    }

    public static string ResolveLintIssueTone(ProcessDefinitionStepLintSeverity severity)
        => severity switch {
            ProcessDefinitionStepLintSeverity.Error => "danger",
            ProcessDefinitionStepLintSeverity.Warning => "warning",
            ProcessDefinitionStepLintSeverity.Info => "info",
            _ => "neutral"
        };

    public static ButtonStyle ResolveCommandButtonStyle(ProcessDefinitionStepCommandKind kind)
        => kind switch {
            ProcessDefinitionStepCommandKind.SaveStep => ButtonStyle.Success,
            ProcessDefinitionStepCommandKind.AddBranchOutcome => ButtonStyle.Light,
            ProcessDefinitionStepCommandKind.AddArtifactExpectation => ButtonStyle.Info,
            ProcessDefinitionStepCommandKind.MapSubprocess => ButtonStyle.Light,
            _ => ButtonStyle.Light
        };

    public static string BuildCommandTestId(ProcessDefinitionStepCommandKind kind)
        => kind switch {
            ProcessDefinitionStepCommandKind.SaveStep => "processes-step-save",
            ProcessDefinitionStepCommandKind.AddBranchOutcome => "processes-step-command-add-branch-outcome",
            ProcessDefinitionStepCommandKind.AddArtifactExpectation => "processes-step-command-add-artifact-expectation",
            ProcessDefinitionStepCommandKind.MapSubprocess => "processes-step-command-map-subprocess",
            _ => $"processes-step-command-{kind.ToString().ToLowerInvariant()}"
        };

    public static string BuildStepItemTestId(ProcessDefinitionStepKey stepKey)
        => $"processes-step-{NormalizeTestId(stepKey.Value)}";

    public static string BuildOperationTestId(ProcessDefinitionStepOperationKind operation)
        => $"processes-step-operation-{NormalizeTestId(operation.ToString())}";

    public static string BuildBranchTestId(ProcessDefinitionBranchOutcomeKey key)
        => $"processes-step-branch-{NormalizeTestId(key.Value)}";

    public static string BuildBranchTitleTestId(ProcessDefinitionBranchOutcomeKey key)
        => $"processes-step-branch-title-{NormalizeTestId(key.Value)}";

    public static string BuildBranchRouteTargetTestId(ProcessDefinitionBranchOutcomeKey key)
        => $"processes-step-branch-route-target-{NormalizeTestId(key.Value)}";

    public static string BuildBranchLoopBudgetTestId(ProcessDefinitionBranchOutcomeKey key)
        => $"processes-step-branch-loop-budget-{NormalizeTestId(key.Value)}";

    public static string BuildArtifactTestId(ProcessDefinitionArtifactExpectationKey key)
        => $"processes-step-artifact-{NormalizeTestId(key.Value)}";

    public static string BuildArtifactTitleTestId(ProcessDefinitionArtifactExpectationKey key)
        => $"processes-step-artifact-title-{NormalizeTestId(key.Value)}";

    public static string BuildArtifactKindTestId(ProcessDefinitionArtifactExpectationKey key)
        => $"processes-step-artifact-kind-{NormalizeTestId(key.Value)}";

    public static string BuildArtifactRetentionTestId(ProcessDefinitionArtifactExpectationKey key)
        => $"processes-step-artifact-retention-{NormalizeTestId(key.Value)}";

    public static string NormalizeTestId(string value) {
        var normalized = new string(value
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray());
        return normalized.Trim('-');
    }

    public string? NumericInputError => !int.TryParse(TargetLeadHoursInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0 || value > 2147483647
        ? "Lead hours must be a non-negative whole number." : null;

    public Dictionary<ProcessDefinitionBranchOutcomeKey, string> BranchNumberInputs { get; } = [];
    public string BranchNumber(ProcessDefinitionBranchOutcomeProjection row)
        => BranchNumberInputs.GetValueOrDefault(row.OutcomeKey, row.LoopBudget.MaximumRepeats.ToString(CultureInfo.InvariantCulture));

    public Dictionary<ProcessDefinitionArtifactExpectationKey, string> ArtifactNumberInputs { get; } = [];
    public string ArtifactNumber(ProcessDefinitionArtifactExpectationProjection row)
        => ArtifactNumberInputs.GetValueOrDefault(row.ArtifactKey, row.RetentionDays.ToString(CultureInfo.InvariantCulture));

    public bool HasInputErrors => NumericInputError is not null || BranchNumberInputs.Values.Concat(ArtifactNumberInputs.Values)
        .Any(text => !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || number < 0);

    private ProcessWorkspaceShellScope? observedScope;
    private Inputs? baseline;
    private Inputs? submission;
    private bool executing;
    private ProcessDefinitionCatalogItemKey? definitionKey;
    public bool HasConflict { get; private set; }
    public bool IsDirty => HasInputErrors || baseline is not null && !CaptureInputs().Matches(baseline);

    private Inputs CaptureInputs() => new(
            StepTitle,
            StepSubtitle,
            StepNotes,
            StepKind,
            TargetLeadHoursInput,
            AllowsManualSkip,
            AllowsSafeRefusal,
            RequiresApproval,
            RequiresDecisionRecord,
            OperationTargetScope,
            SelectedOperations.ToArray(),
            InputContractSummary,
            OutputContractSummary,
            EvidenceContractSummary,
            DecisionRightsSummary,
            ExceptionPolicySummary,
            SubprocessProcessKey,
            SubprocessSnapshotName,
            BranchOutcomes.ToArray(),
            RoleBindings.ToArray(),
            ArtifactExpectations.ToArray());

    private sealed record Inputs(
        string StepTitle,
        string StepSubtitle,
        string StepNotes,
        ProcessDefinitionStepKind StepKind,
        string TargetLeadHoursInput,
        bool AllowsManualSkip,
        bool AllowsSafeRefusal,
        bool RequiresApproval,
        bool RequiresDecisionRecord,
        ProcessDefinitionStepTargetScopeKind OperationTargetScope,
        IReadOnlyList<ProcessDefinitionStepOperationKind> SelectedOperations,
        string InputContractSummary,
        string OutputContractSummary,
        string EvidenceContractSummary,
        string DecisionRightsSummary,
        string ExceptionPolicySummary,
        string SubprocessProcessKey,
        string SubprocessSnapshotName,
        IReadOnlyList<ProcessDefinitionBranchOutcomeProjection> BranchOutcomes,
        IReadOnlyList<ProcessDefinitionStepRoleBindingProjection> RoleBindings,
        IReadOnlyList<ProcessDefinitionArtifactExpectationProjection> ArtifactExpectations) {
        public bool Matches(Inputs other) => StepTitle == other.StepTitle &&
            StepSubtitle == other.StepSubtitle &&
            StepNotes == other.StepNotes &&
            StepKind == other.StepKind &&
            TargetLeadHoursInput == other.TargetLeadHoursInput &&
            AllowsManualSkip == other.AllowsManualSkip &&
            AllowsSafeRefusal == other.AllowsSafeRefusal &&
            RequiresApproval == other.RequiresApproval &&
            RequiresDecisionRecord == other.RequiresDecisionRecord &&
            OperationTargetScope == other.OperationTargetScope &&
            SelectedOperations.SequenceEqual(other.SelectedOperations) &&
            InputContractSummary == other.InputContractSummary &&
            OutputContractSummary == other.OutputContractSummary &&
            EvidenceContractSummary == other.EvidenceContractSummary &&
            DecisionRightsSummary == other.DecisionRightsSummary &&
            ExceptionPolicySummary == other.ExceptionPolicySummary &&
            SubprocessProcessKey == other.SubprocessProcessKey &&
            SubprocessSnapshotName == other.SubprocessSnapshotName &&
            BranchOutcomes.SequenceEqual(other.BranchOutcomes) &&
            RoleBindings.SequenceEqual(other.RoleBindings) &&
            ArtifactExpectations.SequenceEqual(other.ArtifactExpectations);
    }

    public void Accept(ProcessDefinitionStepEditorProjection projection) {
        if (submission is not { } submitted || definitionKey != projection.DefinitionKey) {
            return;
        }
        var current = CaptureInputs();
        var branchInputs = BranchNumberInputs.ToArray();
        var artifactInputs = ArtifactNumberInputs.ToArray();
        submission = null;
        StepEditor = projection;
        SyncedVersionToken = projection.VersionToken;
        SyncSelectedStep();
        StepTitle = current.StepTitle == submitted.StepTitle ? StepTitle : current.StepTitle;
        StepSubtitle = current.StepSubtitle == submitted.StepSubtitle ? StepSubtitle : current.StepSubtitle;
        StepNotes = current.StepNotes == submitted.StepNotes ? StepNotes : current.StepNotes;
        StepKind = current.StepKind == submitted.StepKind ? StepKind : current.StepKind;
        TargetLeadHoursInput = current.TargetLeadHoursInput == submitted.TargetLeadHoursInput ? TargetLeadHoursInput : current.TargetLeadHoursInput;
        AllowsManualSkip = current.AllowsManualSkip == submitted.AllowsManualSkip ? AllowsManualSkip : current.AllowsManualSkip;
        AllowsSafeRefusal = current.AllowsSafeRefusal == submitted.AllowsSafeRefusal ? AllowsSafeRefusal : current.AllowsSafeRefusal;
        RequiresApproval = current.RequiresApproval == submitted.RequiresApproval ? RequiresApproval : current.RequiresApproval;
        RequiresDecisionRecord = current.RequiresDecisionRecord == submitted.RequiresDecisionRecord ? RequiresDecisionRecord : current.RequiresDecisionRecord;
        OperationTargetScope = current.OperationTargetScope == submitted.OperationTargetScope ? OperationTargetScope : current.OperationTargetScope;
        SelectedOperations = current.SelectedOperations.SequenceEqual(submitted.SelectedOperations) ? SelectedOperations : current.SelectedOperations.ToHashSet();
        InputContractSummary = current.InputContractSummary == submitted.InputContractSummary ? InputContractSummary : current.InputContractSummary;
        OutputContractSummary = current.OutputContractSummary == submitted.OutputContractSummary ? OutputContractSummary : current.OutputContractSummary;
        EvidenceContractSummary = current.EvidenceContractSummary == submitted.EvidenceContractSummary ? EvidenceContractSummary : current.EvidenceContractSummary;
        DecisionRightsSummary = current.DecisionRightsSummary == submitted.DecisionRightsSummary ? DecisionRightsSummary : current.DecisionRightsSummary;
        ExceptionPolicySummary = current.ExceptionPolicySummary == submitted.ExceptionPolicySummary ? ExceptionPolicySummary : current.ExceptionPolicySummary;
        SubprocessProcessKey = current.SubprocessProcessKey == submitted.SubprocessProcessKey ? SubprocessProcessKey : current.SubprocessProcessKey;
        SubprocessSnapshotName = current.SubprocessSnapshotName == submitted.SubprocessSnapshotName ? SubprocessSnapshotName : current.SubprocessSnapshotName;
        BranchOutcomes = MergeRows(BranchOutcomes, submitted.BranchOutcomes, current.BranchOutcomes, row => row.OutcomeKey);
        RoleBindings = MergeRows(RoleBindings, submitted.RoleBindings, current.RoleBindings, row => (row.StepKey, row.RoleKey, row.ResponsibilityKind));
        ArtifactExpectations = MergeRows(ArtifactExpectations, submitted.ArtifactExpectations, current.ArtifactExpectations, row => row.ArtifactKey);
        foreach (var (key, value) in branchInputs) {
            if (BranchOutcomes.Any(row => row.OutcomeKey == key)) {
                BranchNumberInputs[key] = value;
            }
        }
        foreach (var (key, value) in artifactInputs) {
            if (ArtifactExpectations.Any(row => row.ArtifactKey == key)) {
                ArtifactNumberInputs[key] = value;
            }
        }
        HasConflict = false;
    }

    public void Discard() {
        SyncedVersionToken = StepEditor.VersionToken;
        SyncSelectedStep();
        HasConflict = false;
    }

    private static List<T> MergeRows<T, TKey>(IReadOnlyList<T> accepted, IReadOnlyList<T> submitted, IReadOnlyList<T> current, Func<T, TKey> key)
        where TKey : notnull {
        var sent = submitted.ToDictionary(key);
        var edited = current.ToDictionary(key);
        return accepted.Where(row => !sent.ContainsKey(key(row)) || edited.ContainsKey(key(row)))
            .Select(row => edited.TryGetValue(key(row), out var local) && sent.TryGetValue(key(row), out var original)
                && !EqualityComparer<T>.Default.Equals(local, original) ? local : row)
            .Concat(current.Where(row => !sent.ContainsKey(key(row)) && !accepted.Any(saved => EqualityComparer<TKey>.Default.Equals(key(saved), key(row)))))
            .ToList();
    }
}
