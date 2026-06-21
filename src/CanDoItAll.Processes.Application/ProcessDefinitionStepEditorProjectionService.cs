using System.Globalization;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessDefinitionStepEditorProjectionService
{
    private const string StepTitleRequiredCode = "processes.definition.step.basic.title-required";
    private const string OperationScopeRequiredCode = "processes.definition.step.operation.scope-required";
    private const string OperationRequiredCode = "processes.definition.step.operation.allowed-operation-recommended";
    private const string BackwardRouteBudgetRequiredCode = "processes.definition.step.routing.backward-loop-budget-required";
    private const string InvalidLoopBudgetCode = "processes.definition.step.routing.invalid-loop-budget";
    private const string SubprocessMappingRequiredCode = "processes.definition.step.subprocess.mapping-required";
    private const string VersionConflictCode = "processes.definition.step.version-conflict";
    private const string SubprocessStepRequiredCode = "processes.definition.step.subprocess.step-kind-required";

    private readonly ProcessTemplatePackLoader templatePackLoader;
    private readonly IProcessProjectionClock clock;
    private readonly Dictionary<ProcessDefinitionStepEditorStateKey, ProcessDefinitionStepEditorSnapshot> snapshots = [];

    public ProcessDefinitionStepEditorProjectionService(IProcessProjectionClock clock)
        : this(new ProcessTemplatePackLoader(), clock)
    {
    }

    public ProcessDefinitionStepEditorProjectionService(
        ProcessTemplatePackLoader templatePackLoader,
        IProcessProjectionClock clock)
    {
        this.templatePackLoader = templatePackLoader ?? throw new ArgumentNullException(nameof(templatePackLoader));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<ProcessDefinitionStepEditorProjection> GetEditorAsync(
        ProcessWorkspaceShellScope scope,
        ProcessDefinitionCatalogItemKey definitionKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateScope(scope);

        var stateKey = ProcessDefinitionStepEditorStateKey.From(scope, definitionKey);
        if (snapshots.TryGetValue(stateKey, out var snapshot))
        {
            return Task.FromResult(CreateProjection(snapshot, lastReceipt: null));
        }

        var pack = templatePackLoader.Load();
        var template = FindTemplateDefinition(pack, definitionKey);
        return Task.FromResult(CreateProjection(CreateTemplateSnapshot(scope, pack, template), lastReceipt: null));
    }

    public Task<ProcessDefinitionStepEditorCommandResult> ExecuteCommandAsync(
        ProcessDefinitionStepEditorCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Scope);
        ArgumentNullException.ThrowIfNull(command.Draft);
        ValidateScope(command.Scope);

        var stateKey = ProcessDefinitionStepEditorStateKey.From(command.Scope, command.DefinitionKey);
        var pack = templatePackLoader.Load();
        var baseline = snapshots.TryGetValue(stateKey, out var existing)
            ? existing
            : CreateTemplateSnapshot(command.Scope, pack, FindTemplateDefinition(pack, command.DefinitionKey));
        var observedAtUtc = clock.GetUtcNow();
        var versionLint = CreateVersionLint(command.ExpectedVersionToken, baseline.VersionToken);
        if (versionLint.HasBlockingIssues)
        {
            return Task.FromResult(CreateRejectedResult(
                baseline with { Lint = versionLint },
                command.CommandKind,
                versionLint,
                observedAtUtc,
                "Step command was rejected because the step editor projection changed before submission."));
        }

        var result = command.CommandKind switch
        {
            ProcessDefinitionStepCommandKind.SaveStep => ExecuteSaveStep(stateKey, baseline, command, observedAtUtc),
            ProcessDefinitionStepCommandKind.AddBranchOutcome => ExecuteAddBranchOutcome(stateKey, baseline, command, observedAtUtc),
            ProcessDefinitionStepCommandKind.AddArtifactExpectation => ExecuteAddArtifactExpectation(stateKey, baseline, command, observedAtUtc),
            ProcessDefinitionStepCommandKind.MapSubprocess => ExecuteMapSubprocess(stateKey, baseline, command, observedAtUtc),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.CommandKind, "Unknown step editor command.")
        };

        return Task.FromResult(result);
    }

    private ProcessDefinitionStepEditorCommandResult ExecuteSaveStep(
        ProcessDefinitionStepEditorStateKey stateKey,
        ProcessDefinitionStepEditorSnapshot baseline,
        ProcessDefinitionStepEditorCommand command,
        DateTimeOffset observedAtUtc)
    {
        var draft = NormalizeDraft(command.Draft);
        var lint = LintDraft(draft, requireCommandCompleteness: true);
        if (lint.HasBlockingIssues)
        {
            return CreateRejectedResult(
                baseline with { SelectedStepKey = draft.Basic.StepKey, Lint = lint },
                command.CommandKind,
                lint,
                observedAtUtc,
                "Step was not saved because blocking step lint issues remain.");
        }

        var stored = StoreDraft(stateKey, baseline, draft, command.CommandKind, lint);
        return CreateAcceptedResult(
            stored,
            command.CommandKind,
            lint,
            observedAtUtc,
            $"Step '{draft.Basic.Title}' saved.");
    }

    private ProcessDefinitionStepEditorCommandResult ExecuteAddBranchOutcome(
        ProcessDefinitionStepEditorStateKey stateKey,
        ProcessDefinitionStepEditorSnapshot baseline,
        ProcessDefinitionStepEditorCommand command,
        DateTimeOffset observedAtUtc)
    {
        var draft = NormalizeDraft(command.Draft);
        var outcomeKey = BuildUniqueOutcomeKey(draft);
        var addedOutcome = new ProcessDefinitionBranchOutcomeProjection(
            outcomeKey,
            $"Outcome {draft.BranchOutcomes.Count + 1}",
            "Route outcome.",
            new ProcessDefinitionRouteTargetProjection(
                ProcessDefinitionRouteTargetKind.NextStep,
                StepKey: null,
                ArtifactExpectationKey: null,
                "Next step"),
            IsBackwardRoute: false,
            new ProcessDefinitionLoopBudgetProjection(
                IsRequired: false,
                MaximumRepeats: 0,
                FingerprintPolicyKey: string.Empty,
                ProcessDefinitionRouteTargetKind.Escalate));
        draft = draft with
        {
            BranchOutcomes = [.. draft.BranchOutcomes, addedOutcome]
        };
        var lint = LintDraft(draft, requireCommandCompleteness: false);
        var stored = StoreDraft(stateKey, baseline, draft, command.CommandKind, lint);
        return CreateAcceptedResult(
            stored,
            command.CommandKind,
            lint,
            observedAtUtc,
            $"Branch outcome '{addedOutcome.Title}' added.");
    }

    private ProcessDefinitionStepEditorCommandResult ExecuteAddArtifactExpectation(
        ProcessDefinitionStepEditorStateKey stateKey,
        ProcessDefinitionStepEditorSnapshot baseline,
        ProcessDefinitionStepEditorCommand command,
        DateTimeOffset observedAtUtc)
    {
        var draft = NormalizeDraft(command.Draft);
        var artifactKey = BuildUniqueArtifactKey(draft);
        var addedArtifact = new ProcessDefinitionArtifactExpectationProjection(
            artifactKey,
            TemplateKey: artifactKey.Value,
            $"Artifact {draft.ArtifactExpectations.Count + 1}",
            ProcessDefinitionArtifactKind.Evidence,
            IsRequired: true,
            ProcessDefinitionArtifactTrustRequirement.ReviewRequired,
            ProcessDefinitionArtifactSensitivityLevel.Internal,
            RetentionDays: 365,
            WorkflowOutputId: string.Empty,
            WorkflowOutputName: string.Empty,
            ProcessDefinitionWorkflowOutputKind.Unspecified,
            SubprocessChildArtifactExpectationId: null,
            SubprocessChildStepKey: string.Empty,
            SubprocessChildArtifactTitle: string.Empty,
            AllowedFutureUsageSummary: "Reusable by downstream process steps.",
            ValidationRequirementSummary: "Must identify producer, validation status, and artifact lineage.");
        draft = draft with
        {
            ArtifactExpectations = [.. draft.ArtifactExpectations, addedArtifact]
        };
        var lint = LintDraft(draft, requireCommandCompleteness: false);
        var stored = StoreDraft(stateKey, baseline, draft, command.CommandKind, lint);
        return CreateAcceptedResult(
            stored,
            command.CommandKind,
            lint,
            observedAtUtc,
            $"Artifact expectation '{addedArtifact.Title}' added.");
    }

    private ProcessDefinitionStepEditorCommandResult ExecuteMapSubprocess(
        ProcessDefinitionStepEditorStateKey stateKey,
        ProcessDefinitionStepEditorSnapshot baseline,
        ProcessDefinitionStepEditorCommand command,
        DateTimeOffset observedAtUtc)
    {
        var draft = NormalizeDraft(command.Draft);
        if (draft.Basic.StepKind != ProcessDefinitionStepKind.Subprocess)
        {
            var stepKindLint = new ProcessDefinitionStepLintProjection(
            [
                new ProcessDefinitionStepLintIssueProjection(
                    SubprocessStepRequiredCode,
                    ProcessDefinitionStepLintSeverity.Error,
                    ProcessDefinitionStepLintSection.Subprocess,
                    "Only subprocess steps can map a subprocess definition.",
                    "Change the step kind to Subprocess before mapping a child definition.")
            ]);
            return CreateRejectedResult(
                baseline with { SelectedStepKey = draft.Basic.StepKey, Lint = stepKindLint },
                command.CommandKind,
                stepKindLint,
                observedAtUtc,
                "Subprocess mapping was not applied because the selected step is not a subprocess step.");
        }

        var subprocess = string.IsNullOrWhiteSpace(draft.SubprocessMapping.ProcessKey)
            ? ResolveDefaultSubprocessOption(baseline)
            : draft.SubprocessMapping.ProcessKey;
        draft = draft with
        {
            SubprocessMapping = draft.SubprocessMapping with
            {
                ProcessKey = subprocess,
                DefinitionSnapshotName = ResolveSubprocessSnapshotName(baseline, subprocess, draft.SubprocessMapping.DefinitionSnapshotName),
                ChildArtifactMappings = draft.ArtifactExpectations
                    .Where(artifact => !string.IsNullOrWhiteSpace(artifact.SubprocessChildStepKey) ||
                                       !string.IsNullOrWhiteSpace(artifact.SubprocessChildArtifactTitle) ||
                                       artifact.SubprocessChildArtifactExpectationId.HasValue)
                    .ToArray()
            }
        };
        var lint = LintDraft(draft, requireCommandCompleteness: true);
        if (lint.HasBlockingIssues)
        {
            return CreateRejectedResult(
                baseline with { SelectedStepKey = draft.Basic.StepKey, Lint = lint },
                command.CommandKind,
                lint,
                observedAtUtc,
                "Subprocess mapping was not applied because blocking step lint issues remain.");
        }

        var stored = StoreDraft(stateKey, baseline, draft, command.CommandKind, lint);
        return CreateAcceptedResult(
            stored,
            command.CommandKind,
            lint,
            observedAtUtc,
            $"Subprocess '{draft.SubprocessMapping.ProcessKey}' mapped.");
    }

    private ProcessDefinitionStepEditorSnapshot StoreDraft(
        ProcessDefinitionStepEditorStateKey stateKey,
        ProcessDefinitionStepEditorSnapshot baseline,
        ProcessDefinitionStepDraftProjection draft,
        ProcessDefinitionStepCommandKind commandKind,
        ProcessDefinitionStepLintProjection lint)
    {
        var steps = baseline.Steps
            .Where(step => step.Basic.StepKey != draft.Basic.StepKey)
            .Append(draft)
            .OrderBy(step => ResolveStepOrder(baseline.Steps, step.Basic.StepKey))
            .ThenBy(step => step.Basic.StepKey.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var stored = baseline with
        {
            VersionToken = CreateVersionToken(commandKind),
            SelectedStepKey = draft.Basic.StepKey,
            Steps = steps,
            Lint = lint
        };
        snapshots[stateKey] = stored;
        return stored;
    }

    private ProcessDefinitionStepEditorProjection CreateProjection(
        ProcessDefinitionStepEditorSnapshot snapshot,
        ProcessDefinitionStepCommandReceipt? lastReceipt)
    {
        var selectedStep = snapshot.SelectedStepKey is { } selectedStepKey
            ? snapshot.Steps.FirstOrDefault(step => step.Basic.StepKey == selectedStepKey)
            : snapshot.Steps.FirstOrDefault();
        var selectedKey = selectedStep?.Basic.StepKey;

        return new ProcessDefinitionStepEditorProjection(
            snapshot.DefinitionKey,
            snapshot.VersionToken,
            selectedKey,
            snapshot.Steps
                .Select((step, index) => new ProcessDefinitionStepListItemProjection(
                    step.Basic.StepKey,
                    step.Basic.Title,
                    step.Basic.Subtitle,
                    step.Basic.StepKind,
                    index,
                    selectedKey == step.Basic.StepKey))
                .ToArray(),
            snapshot.Steps,
            selectedStep,
            snapshot.SubprocessOptions,
            CreateCommands(selectedStep),
            snapshot.Lint,
            lastReceipt);
    }

    private ProcessDefinitionStepEditorSnapshot CreateTemplateSnapshot(
        ProcessWorkspaceShellScope scope,
        ProcessTemplatePack pack,
        ProcessTemplateDefinitionSummary template)
    {
        var roleNames = template.RoleAuthoringDefaults.Roles
            .ToDictionary(role => role.Key, role => role.DisplayName, StringComparer.OrdinalIgnoreCase);
        var steps = template.StepAuthoringDefaults.Steps
            .Select(step => CreateStepDraft(step, roleNames))
            .ToArray();
        var selectedStep = steps.FirstOrDefault();
        return new ProcessDefinitionStepEditorSnapshot(
            scope,
            new ProcessDefinitionCatalogItemKey(template.Key),
            new ProcessDefinitionStepEditorVersionToken($"template:{template.Key}:steps"),
            selectedStep?.Basic.StepKey,
            steps,
            pack.Definitions
                .Where(definition => !string.Equals(definition.Key, template.Key, StringComparison.OrdinalIgnoreCase))
                .Select(definition => new ProcessDefinitionSubprocessOptionProjection(
                    new ProcessDefinitionCatalogItemKey(definition.Key),
                    definition.DisplayName,
                    definition.Summary))
                .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            selectedStep is null
                ? new ProcessDefinitionStepLintProjection([])
                : LintDraft(selectedStep, requireCommandCompleteness: false));
    }

    private static ProcessDefinitionStepDraftProjection CreateStepDraft(
        ProcessTemplateDefinitionStepAuthoringSummary step,
        IReadOnlyDictionary<string, string> roleNames)
    {
        var stepKey = new ProcessDefinitionStepKey(step.Key);
        var artifactExpectations = step.ArtifactExpectations
            .Select(CreateArtifactExpectation)
            .ToArray();
        return new ProcessDefinitionStepDraftProjection(
            new ProcessDefinitionStepBasicDraftProjection(
                stepKey,
                step.Title,
                step.Subtitle,
                step.Notes,
                ParseStepKind(step.StepKind),
                step.TargetLeadHours,
                step.AllowsManualSkip,
                step.AllowsSafeRefusal,
                step.RequiresApproval,
                step.RequiresDecisionRecord,
                string.IsNullOrWhiteSpace(step.DecisionRoleKey) ? null : new ProcessDefinitionRoleKey(step.DecisionRoleKey)),
            new ProcessDefinitionStepOperationContractProjection(
                ParseTargetScope(step.OperationTargetScope),
                step.AllowedOperations
                    .Select(ParseOperationKind)
                    .Where(operation => operation != ProcessDefinitionStepOperationKind.Unspecified)
                    .Distinct()
                    .ToArray()),
            new ProcessDefinitionStepContractsProjection(
                step.InputContractSummary,
                step.OutputContractSummary,
                step.EvidenceContractSummary,
                step.DecisionRightsSummary,
                step.ExceptionPolicySummary),
            step.BranchOutcomes
                .Select(CreateBranchOutcome)
                .ToArray(),
            step.RoleBindings
                .Where(binding => !string.IsNullOrWhiteSpace(binding.RoleKey))
                .Select(binding => CreateRoleBinding(binding, roleNames))
                .ToArray(),
            artifactExpectations,
            new ProcessDefinitionSubprocessMappingProjection(
                step.SubprocessProcessKey,
                step.SubprocessDefinitionSnapshotName,
                artifactExpectations
                    .Where(artifact => !string.IsNullOrWhiteSpace(artifact.SubprocessChildStepKey) ||
                                       !string.IsNullOrWhiteSpace(artifact.SubprocessChildArtifactTitle) ||
                                       artifact.SubprocessChildArtifactExpectationId.HasValue)
                    .ToArray()));
    }

    private static ProcessDefinitionBranchOutcomeProjection CreateBranchOutcome(
        ProcessTemplateDefinitionStepBranchOutcomeSummary outcome)
    {
        var routeKind = ParseRouteTargetKind(outcome.RouteTargetKind);
        ProcessDefinitionArtifactExpectationKey? artifactExpectationKey = string.IsNullOrWhiteSpace(outcome.RouteTargetArtifactExpectationKey)
            ? null
            : new ProcessDefinitionArtifactExpectationKey(outcome.RouteTargetArtifactExpectationKey);
        return new ProcessDefinitionBranchOutcomeProjection(
            new ProcessDefinitionBranchOutcomeKey(outcome.Key),
            outcome.Title,
            outcome.Description,
            new ProcessDefinitionRouteTargetProjection(
                routeKind,
                string.IsNullOrWhiteSpace(outcome.RouteTargetStepKey) ? null : new ProcessDefinitionStepKey(outcome.RouteTargetStepKey),
                artifactExpectationKey,
                ResolveRouteSummary(routeKind, outcome.RouteTargetStepKey, outcome.RouteTargetArtifactExpectationKey)),
            outcome.IsBackwardRoute || routeKind == ProcessDefinitionRouteTargetKind.PreviousStep,
            new ProcessDefinitionLoopBudgetProjection(
                outcome.IsBackwardRoute || routeKind == ProcessDefinitionRouteTargetKind.PreviousStep,
                outcome.LoopBudgetMaximumRepeats,
                outcome.LoopFingerprintPolicyKey,
                ParseRouteTargetKind(outcome.LoopEscalationTargetKind)));
    }

    private static ProcessDefinitionStepRoleBindingProjection CreateRoleBinding(
        ProcessTemplateDefinitionStepRoleBindingSummary binding,
        IReadOnlyDictionary<string, string> roleNames)
    {
        roleNames.TryGetValue(binding.RoleKey, out var displayName);
        return new ProcessDefinitionStepRoleBindingProjection(
            new ProcessDefinitionStepKey(binding.StepKey),
            binding.StepTitle,
            new ProcessDefinitionRoleKey(binding.RoleKey),
            string.IsNullOrWhiteSpace(displayName) ? binding.RoleDisplayName : displayName,
            ParseResponsibilityKind(binding.ResponsibilityKind),
            binding.IsRequired,
            binding.FallbackOrder,
            binding.RebindPolicySummary);
    }

    private static ProcessDefinitionArtifactExpectationProjection CreateArtifactExpectation(
        ProcessTemplateDefinitionStepArtifactExpectationSummary artifact)
        => new(
            new ProcessDefinitionArtifactExpectationKey(artifact.Key),
            artifact.TemplateKey,
            artifact.Title,
            ParseArtifactKind(artifact.ArtifactKind),
            artifact.IsRequired,
            ParseTrustRequirement(artifact.TrustRequirement),
            ParseSensitivityLevel(artifact.SensitivityLevel),
            artifact.RetentionDays,
            artifact.WorkflowOutputId,
            artifact.WorkflowOutputName,
            ParseWorkflowOutputKind(artifact.WorkflowOutputKind),
            artifact.SubprocessChildArtifactExpectationId,
            artifact.SubprocessChildStepKey,
            artifact.SubprocessChildArtifactTitle,
            artifact.AllowedFutureUsageSummary,
            artifact.ValidationRequirementSummary);

    private static ProcessDefinitionStepDraftProjection NormalizeDraft(ProcessDefinitionStepDraftProjection draft)
        => draft with
        {
            Basic = draft.Basic with
            {
                Title = NormalizeOptional(draft.Basic.Title, string.Empty),
                Subtitle = NormalizeOptional(draft.Basic.Subtitle, string.Empty),
                Notes = NormalizeOptional(draft.Basic.Notes, string.Empty),
                TargetLeadHours = Math.Max(0, draft.Basic.TargetLeadHours)
            },
            OperationContract = draft.OperationContract with
            {
                AllowedOperations = draft.OperationContract.AllowedOperations
                    .Where(operation => operation != ProcessDefinitionStepOperationKind.Unspecified)
                    .Distinct()
                    .ToArray()
            },
            Contracts = draft.Contracts with
            {
                InputContractSummary = NormalizeOptional(draft.Contracts.InputContractSummary, string.Empty),
                OutputContractSummary = NormalizeOptional(draft.Contracts.OutputContractSummary, string.Empty),
                EvidenceContractSummary = NormalizeOptional(draft.Contracts.EvidenceContractSummary, string.Empty),
                DecisionRightsSummary = NormalizeOptional(draft.Contracts.DecisionRightsSummary, string.Empty),
                ExceptionPolicySummary = NormalizeOptional(draft.Contracts.ExceptionPolicySummary, string.Empty)
            },
            BranchOutcomes = draft.BranchOutcomes
                .Select(NormalizeBranchOutcome)
                .ToArray(),
            ArtifactExpectations = draft.ArtifactExpectations
                .Select(NormalizeArtifactExpectation)
                .ToArray(),
            SubprocessMapping = draft.SubprocessMapping with
            {
                ProcessKey = NormalizeOptional(draft.SubprocessMapping.ProcessKey, string.Empty),
                DefinitionSnapshotName = NormalizeOptional(draft.SubprocessMapping.DefinitionSnapshotName, string.Empty)
            }
        };

    private static ProcessDefinitionBranchOutcomeProjection NormalizeBranchOutcome(
        ProcessDefinitionBranchOutcomeProjection outcome)
    {
        var isBackward = outcome.IsBackwardRoute || outcome.RouteTarget.Kind == ProcessDefinitionRouteTargetKind.PreviousStep;
        return outcome with
        {
            Title = NormalizeOptional(outcome.Title, outcome.OutcomeKey.Value),
            Description = NormalizeOptional(outcome.Description, string.Empty),
            IsBackwardRoute = isBackward,
            LoopBudget = outcome.LoopBudget with
            {
                IsRequired = isBackward,
                MaximumRepeats = Math.Max(0, outcome.LoopBudget.MaximumRepeats),
                FingerprintPolicyKey = NormalizeOptional(outcome.LoopBudget.FingerprintPolicyKey, string.Empty)
            }
        };
    }

    private static ProcessDefinitionArtifactExpectationProjection NormalizeArtifactExpectation(
        ProcessDefinitionArtifactExpectationProjection artifact)
        => artifact with
        {
            TemplateKey = NormalizeOptional(artifact.TemplateKey, artifact.ArtifactKey.Value),
            Title = NormalizeOptional(artifact.Title, artifact.ArtifactKey.Value),
            RetentionDays = Math.Max(0, artifact.RetentionDays),
            WorkflowOutputId = NormalizeOptional(artifact.WorkflowOutputId, string.Empty),
            WorkflowOutputName = NormalizeOptional(artifact.WorkflowOutputName, string.Empty),
            SubprocessChildStepKey = NormalizeOptional(artifact.SubprocessChildStepKey, string.Empty),
            SubprocessChildArtifactTitle = NormalizeOptional(artifact.SubprocessChildArtifactTitle, string.Empty),
            AllowedFutureUsageSummary = NormalizeOptional(artifact.AllowedFutureUsageSummary, string.Empty),
            ValidationRequirementSummary = NormalizeOptional(artifact.ValidationRequirementSummary, string.Empty)
        };

    private static ProcessDefinitionStepLintProjection LintDraft(
        ProcessDefinitionStepDraftProjection draft,
        bool requireCommandCompleteness)
    {
        var issues = new List<ProcessDefinitionStepLintIssueProjection>();
        if (string.IsNullOrWhiteSpace(draft.Basic.Title))
        {
            issues.Add(new ProcessDefinitionStepLintIssueProjection(
                StepTitleRequiredCode,
                ProcessDefinitionStepLintSeverity.Error,
                ProcessDefinitionStepLintSection.Basic,
                "Step title is required.",
                "Enter a stable title before saving the step."));
        }

        if (draft.OperationContract.TargetScope == ProcessDefinitionStepTargetScopeKind.Unspecified)
        {
            issues.Add(new ProcessDefinitionStepLintIssueProjection(
                OperationScopeRequiredCode,
                requireCommandCompleteness ? ProcessDefinitionStepLintSeverity.Error : ProcessDefinitionStepLintSeverity.Warning,
                ProcessDefinitionStepLintSection.OperationContract,
                "Operation target scope is not explicit.",
                "Choose the narrowest target scope that covers the allowed operations."));
        }

        if (draft.OperationContract.AllowedOperations.Count == 0)
        {
            issues.Add(new ProcessDefinitionStepLintIssueProjection(
                OperationRequiredCode,
                ProcessDefinitionStepLintSeverity.Warning,
                ProcessDefinitionStepLintSection.OperationContract,
                "No allowed operation is selected.",
                "Select explicit operations when the step executes agent, workflow, artifact, or external actions."));
        }

        foreach (var outcome in draft.BranchOutcomes)
        {
            if (outcome.IsBackwardRoute && outcome.LoopBudget.MaximumRepeats == 0)
            {
                issues.Add(new ProcessDefinitionStepLintIssueProjection(
                    BackwardRouteBudgetRequiredCode,
                    requireCommandCompleteness ? ProcessDefinitionStepLintSeverity.Error : ProcessDefinitionStepLintSeverity.Warning,
                    ProcessDefinitionStepLintSection.Routing,
                    $"Backward route '{outcome.Title}' does not declare a loop budget.",
                    "Set a positive maximum repeat count and a fingerprint policy for backward routes."));
            }

            if (outcome.LoopBudget.MaximumRepeats < 0)
            {
                issues.Add(new ProcessDefinitionStepLintIssueProjection(
                    InvalidLoopBudgetCode,
                    ProcessDefinitionStepLintSeverity.Error,
                    ProcessDefinitionStepLintSection.Routing,
                    $"Route '{outcome.Title}' has an invalid loop budget.",
                    "Loop budgets must be zero for forward routes or a positive value for backward routes."));
            }
        }

        if (draft.Basic.StepKind == ProcessDefinitionStepKind.Subprocess &&
            string.IsNullOrWhiteSpace(draft.SubprocessMapping.ProcessKey))
        {
            issues.Add(new ProcessDefinitionStepLintIssueProjection(
                SubprocessMappingRequiredCode,
                requireCommandCompleteness ? ProcessDefinitionStepLintSeverity.Error : ProcessDefinitionStepLintSeverity.Warning,
                ProcessDefinitionStepLintSection.Subprocess,
                "Subprocess step is not mapped to a child definition.",
                "Choose the child process definition that this step starts or resumes."));
        }

        return new ProcessDefinitionStepLintProjection(issues);
    }

    private static ProcessDefinitionStepLintProjection CreateVersionLint(
        ProcessDefinitionStepEditorVersionToken? expected,
        ProcessDefinitionStepEditorVersionToken actual)
    {
        if (expected is null || expected == actual)
        {
            return new ProcessDefinitionStepLintProjection([]);
        }

        return new ProcessDefinitionStepLintProjection(
        [
            new ProcessDefinitionStepLintIssueProjection(
                VersionConflictCode,
                ProcessDefinitionStepLintSeverity.Error,
                ProcessDefinitionStepLintSection.Basic,
                "Step editor projection changed before submission.",
                "Reload the step editor and apply the change against the latest version token.")
        ]);
    }

    private ProcessDefinitionStepEditorCommandResult CreateAcceptedResult(
        ProcessDefinitionStepEditorSnapshot snapshot,
        ProcessDefinitionStepCommandKind commandKind,
        ProcessDefinitionStepLintProjection lint,
        DateTimeOffset observedAtUtc,
        string summary)
    {
        var receipt = new ProcessDefinitionStepCommandReceipt(
            Guid.NewGuid(),
            commandKind,
            ProcessDefinitionStepCommandStatus.Accepted,
            snapshot.VersionToken,
            observedAtUtc,
            summary,
            lint.Issues);
        return new ProcessDefinitionStepEditorCommandResult(receipt, CreateProjection(snapshot, receipt));
    }

    private ProcessDefinitionStepEditorCommandResult CreateRejectedResult(
        ProcessDefinitionStepEditorSnapshot snapshot,
        ProcessDefinitionStepCommandKind commandKind,
        ProcessDefinitionStepLintProjection lint,
        DateTimeOffset observedAtUtc,
        string summary)
    {
        var receipt = new ProcessDefinitionStepCommandReceipt(
            Guid.NewGuid(),
            commandKind,
            ProcessDefinitionStepCommandStatus.Rejected,
            snapshot.VersionToken,
            observedAtUtc,
            summary,
            lint.Issues);
        return new ProcessDefinitionStepEditorCommandResult(receipt, CreateProjection(snapshot, receipt));
    }

    private ProcessDefinitionStepEditorVersionToken CreateVersionToken(ProcessDefinitionStepCommandKind commandKind)
        => new($"{commandKind.ToString().ToLowerInvariant()}:{clock.GetUtcNow():yyyyMMddHHmmss}:{Guid.NewGuid():N}");

    private static IReadOnlyList<ProcessDefinitionStepCommandProjection> CreateCommands(
        ProcessDefinitionStepDraftProjection? selectedStep)
        =>
        [
            new(ProcessDefinitionStepCommandKind.SaveStep, "Save step", "save", selectedStep is not null, selectedStep is null ? "Select a step first." : null),
            new(ProcessDefinitionStepCommandKind.AddBranchOutcome, "Add route", "alt_route", selectedStep is not null, selectedStep is null ? "Select a step first." : null),
            new(ProcessDefinitionStepCommandKind.AddArtifactExpectation, "Add artifact", "inventory_2", selectedStep is not null, selectedStep is null ? "Select a step first." : null),
            new(ProcessDefinitionStepCommandKind.MapSubprocess, "Map subprocess", "account_tree", selectedStep?.Basic.StepKind == ProcessDefinitionStepKind.Subprocess, selectedStep?.Basic.StepKind == ProcessDefinitionStepKind.Subprocess ? null : "Only subprocess steps can map a child definition.")
        ];

    private static ProcessTemplateDefinitionSummary FindTemplateDefinition(
        ProcessTemplatePack pack,
        ProcessDefinitionCatalogItemKey definitionKey)
        => pack.Definitions.FirstOrDefault(definition =>
            string.Equals(definition.Key, definitionKey.Value, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Process definition '{definitionKey.Value}' is not available in the template pack.");

    private static ProcessDefinitionBranchOutcomeKey BuildUniqueOutcomeKey(
        ProcessDefinitionStepDraftProjection draft)
    {
        var used = draft.BranchOutcomes
            .Select(outcome => outcome.OutcomeKey.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseKey = $"{draft.Basic.StepKey.Value}-route";
        var candidate = baseKey;
        for (var index = 1; used.Contains(candidate); index++)
        {
            candidate = $"{baseKey}-{index.ToString(CultureInfo.InvariantCulture)}";
        }

        return new ProcessDefinitionBranchOutcomeKey(candidate);
    }

    private static ProcessDefinitionArtifactExpectationKey BuildUniqueArtifactKey(
        ProcessDefinitionStepDraftProjection draft)
    {
        var used = draft.ArtifactExpectations
            .Select(artifact => artifact.ArtifactKey.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseKey = $"{draft.Basic.StepKey.Value}-artifact";
        var candidate = baseKey;
        for (var index = 1; used.Contains(candidate); index++)
        {
            candidate = $"{baseKey}-{index.ToString(CultureInfo.InvariantCulture)}";
        }

        return new ProcessDefinitionArtifactExpectationKey(candidate);
    }

    private static int ResolveStepOrder(
        IReadOnlyList<ProcessDefinitionStepDraftProjection> steps,
        ProcessDefinitionStepKey stepKey)
    {
        var index = steps
            .Select((step, stepIndex) => new { step.Basic.StepKey, Index = stepIndex })
            .FirstOrDefault(item => item.StepKey == stepKey)?.Index;
        return index ?? int.MaxValue;
    }

    private static string ResolveDefaultSubprocessOption(ProcessDefinitionStepEditorSnapshot baseline)
        => baseline.SubprocessOptions.FirstOrDefault()?.DefinitionKey.Value ?? string.Empty;

    private static string ResolveSubprocessSnapshotName(
        ProcessDefinitionStepEditorSnapshot baseline,
        string processKey,
        string requestedSnapshotName)
    {
        if (!string.IsNullOrWhiteSpace(requestedSnapshotName))
        {
            return requestedSnapshotName.Trim();
        }

        return baseline.SubprocessOptions
            .FirstOrDefault(option => string.Equals(option.DefinitionKey.Value, processKey, StringComparison.OrdinalIgnoreCase))
            ?.DisplayName ?? string.Empty;
    }

    private static string ResolveRouteSummary(
        ProcessDefinitionRouteTargetKind kind,
        string stepKey,
        string artifactExpectationKey)
        => kind switch
        {
            ProcessDefinitionRouteTargetKind.SpecificStep or ProcessDefinitionRouteTargetKind.SubprocessStart or ProcessDefinitionRouteTargetKind.SubprocessResume
                when !string.IsNullOrWhiteSpace(stepKey) => $"{kind} -> {stepKey}",
            ProcessDefinitionRouteTargetKind.WaitForArtifact when !string.IsNullOrWhiteSpace(artifactExpectationKey) => $"Wait for {artifactExpectationKey}",
            _ => kind.ToString()
        };

    private static ProcessDefinitionStepKind ParseStepKind(string value)
        => ParseEnum(value, ProcessDefinitionStepKind.Unspecified);

    private static ProcessDefinitionStepOperationKind ParseOperationKind(string value)
        => ParseEnum(value, ProcessDefinitionStepOperationKind.Unspecified);

    private static ProcessDefinitionStepTargetScopeKind ParseTargetScope(string value)
        => ParseEnum(value, ProcessDefinitionStepTargetScopeKind.Unspecified);

    private static ProcessDefinitionRouteTargetKind ParseRouteTargetKind(string value)
        => ParseEnum(value, ProcessDefinitionRouteTargetKind.NextStep);

    private static ProcessDefinitionArtifactKind ParseArtifactKind(string value)
        => ParseEnum(value, ProcessDefinitionArtifactKind.Unspecified);

    private static ProcessDefinitionArtifactTrustRequirement ParseTrustRequirement(string value)
        => ParseEnum(value, ProcessDefinitionArtifactTrustRequirement.Unspecified);

    private static ProcessDefinitionArtifactSensitivityLevel ParseSensitivityLevel(string value)
        => ParseEnum(value, ProcessDefinitionArtifactSensitivityLevel.Unspecified);

    private static ProcessDefinitionWorkflowOutputKind ParseWorkflowOutputKind(string value)
        => ParseEnum(value, ProcessDefinitionWorkflowOutputKind.Unspecified);

    private static ProcessStepRoleResponsibilityKind ParseResponsibilityKind(string value)
        => ParseEnum(value, ProcessStepRoleResponsibilityKind.Responsible);

    private static TEnum ParseEnum<TEnum>(string value, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        var normalized = NormalizeEnumToken(value);
        foreach (var enumValue in Enum.GetValues<TEnum>())
        {
            if (NormalizeEnumToken(enumValue.ToString()) == normalized)
            {
                return enumValue;
            }
        }

        return defaultValue;
    }

    private static string NormalizeEnumToken(string value)
        => new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static string NormalizeOptional(string? value, string defaultValue)
        => string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();

    private static void ValidateScope(ProcessWorkspaceShellScope scope)
    {
        if (scope.Kind == ProcessWorkspaceScopeKind.Project && scope.ProjectId is null)
        {
            throw new ArgumentException("Project-scoped step editor command requires a project id.", nameof(scope));
        }

        if (scope.Kind == ProcessWorkspaceScopeKind.Global && scope.ProjectId is not null)
        {
            throw new ArgumentException("Global step editor command cannot carry a project id.", nameof(scope));
        }
    }

    private readonly record struct ProcessDefinitionStepEditorStateKey(
        ProcessWorkspaceScopeKind ScopeKind,
        Guid? ProjectId,
        ProcessDefinitionCatalogItemKey DefinitionKey)
    {
        public static ProcessDefinitionStepEditorStateKey From(
            ProcessWorkspaceShellScope scope,
            ProcessDefinitionCatalogItemKey definitionKey)
            => new(scope.Kind, scope.ProjectId, definitionKey);
    }

    private sealed record ProcessDefinitionStepEditorSnapshot(
        ProcessWorkspaceShellScope Scope,
        ProcessDefinitionCatalogItemKey DefinitionKey,
        ProcessDefinitionStepEditorVersionToken VersionToken,
        ProcessDefinitionStepKey? SelectedStepKey,
        IReadOnlyList<ProcessDefinitionStepDraftProjection> Steps,
        IReadOnlyList<ProcessDefinitionSubprocessOptionProjection> SubprocessOptions,
        ProcessDefinitionStepLintProjection Lint);
}
