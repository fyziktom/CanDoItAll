using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessDefinitionEditorProjectionService
{
    private const string NameRequiredCode = "processes.definition.identity.name-required";
    private const string SummaryRequiredCode = "processes.definition.identity.summary-required";
    private const string OwnerRecommendedCode = "processes.definition.identity.owner-recommended";
    private const string GovernancePolicyRequiredCode = "processes.definition.governance.policy-required";
    private const string ContractRequiredCode = "processes.definition.contracts.interface-required";
    private const string SimulationRequiredCode = "processes.definition.simulation.readiness-required";
    private const string ArchiveTemplateDefaultCode = "processes.definition.archive.template-default";
    private const string DeleteTemplateDefaultCode = "processes.definition.delete.template-default";
    private const string VersionConflictCode = "processes.definition.version-conflict";

    private readonly ProcessTemplatePackLoader templatePackLoader;
    private readonly IProcessProjectionClock clock;
    private readonly Dictionary<ProcessDefinitionEditorStateKey, ProcessDefinitionEditorSnapshot> snapshots = [];

    public ProcessDefinitionEditorProjectionService(IProcessProjectionClock clock)
        : this(new ProcessTemplatePackLoader(), clock)
    {
    }

    public ProcessDefinitionEditorProjectionService(
        ProcessTemplatePackLoader templatePackLoader,
        IProcessProjectionClock clock)
    {
        this.templatePackLoader = templatePackLoader ?? throw new ArgumentNullException(nameof(templatePackLoader));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<ProcessDefinitionEditorProjection> GetEditorAsync(
        ProcessWorkspaceShellScope scope,
        ProcessDefinitionCatalogItemKey definitionKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateScope(scope);

        var stateKey = ProcessDefinitionEditorStateKey.From(scope, definitionKey);
        if (snapshots.TryGetValue(stateKey, out var snapshot))
        {
            return Task.FromResult(CreateProjection(snapshot, lastReceipt: null));
        }

        var template = FindTemplateDefinition(definitionKey);
        return Task.FromResult(CreateProjection(CreateTemplateSnapshot(scope, template), lastReceipt: null));
    }

    public Task<ProcessDefinitionEditorCommandResult> ExecuteCommandAsync(
        ProcessDefinitionEditorCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Scope);
        ArgumentNullException.ThrowIfNull(command.Draft);
        ValidateScope(command.Scope);

        if (command.DefinitionKey != command.Draft.DefinitionKey)
        {
            throw new ArgumentException("Definition editor command key must match the draft key.", nameof(command));
        }

        var stateKey = ProcessDefinitionEditorStateKey.From(command.Scope, command.DefinitionKey);
        var baseline = snapshots.TryGetValue(stateKey, out var existing)
            ? existing
            : CreateTemplateSnapshot(command.Scope, FindTemplateDefinition(command.DefinitionKey));
        var submitted = NormalizeDraft(command.Scope, command.Draft, baseline.Status, baseline.VersionToken);
        var lint = Lint(submitted, strict: command.CommandKind == ProcessDefinitionEditorCommandKind.Publish);
        var versionConflict = command.ExpectedVersionToken is { } expected &&
            expected != baseline.VersionToken;
        if (versionConflict)
        {
            lint = AddIssue(
                lint,
                new ProcessDefinitionEditorLintIssueProjection(
                    VersionConflictCode,
                    ProcessDefinitionEditorLintSeverity.Error,
                    ProcessDefinitionEditorLintSection.Identity,
                    "The definition editor projection changed before this command was submitted.",
                    "Reload the definition and apply the edit again."));
        }

        var observedAtUtc = clock.GetUtcNow();
        var result = command.CommandKind switch
        {
            ProcessDefinitionEditorCommandKind.SaveDraft => ExecuteSaveDraft(stateKey, submitted, lint, observedAtUtc),
            ProcessDefinitionEditorCommandKind.Publish => ExecutePublish(stateKey, submitted, lint, observedAtUtc),
            ProcessDefinitionEditorCommandKind.Archive => ExecuteArchive(stateKey, baseline, submitted, lint, observedAtUtc),
            ProcessDefinitionEditorCommandKind.Delete => ExecuteDelete(stateKey, command.Scope, baseline, submitted, lint, observedAtUtc),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.CommandKind, "Unknown definition editor command.")
        };

        return Task.FromResult(result);
    }

    private ProcessDefinitionEditorCommandResult ExecuteSaveDraft(
        ProcessDefinitionEditorStateKey stateKey,
        ProcessDefinitionEditorSnapshot submitted,
        ProcessDefinitionEditorLintProjection lint,
        DateTimeOffset observedAtUtc)
    {
        if (lint.HasBlockingIssues)
        {
            return CreateRejectedResult(
                submitted,
                ProcessDefinitionEditorCommandKind.SaveDraft,
                lint,
                observedAtUtc,
                "Draft was not saved because blocking definition lint issues remain.");
        }

        var stored = submitted with
        {
            Status = ProcessDefinitionAuthoringStatus.Draft,
            VersionToken = CreateVersionToken(ProcessDefinitionEditorCommandKind.SaveDraft),
            Lint = lint
        };
        snapshots[stateKey] = stored;
        return CreateAcceptedResult(
            stored,
            ProcessDefinitionEditorCommandKind.SaveDraft,
            lint,
            observedAtUtc,
            $"Draft saved for '{stored.Draft.Identity.Name}'.");
    }

    private ProcessDefinitionEditorCommandResult ExecutePublish(
        ProcessDefinitionEditorStateKey stateKey,
        ProcessDefinitionEditorSnapshot submitted,
        ProcessDefinitionEditorLintProjection lint,
        DateTimeOffset observedAtUtc)
    {
        if (lint.HasBlockingIssues)
        {
            return CreateRejectedResult(
                submitted,
                ProcessDefinitionEditorCommandKind.Publish,
                lint,
                observedAtUtc,
                "Definition was not published because blocking lint issues remain.");
        }

        var stored = submitted with
        {
            Status = ProcessDefinitionAuthoringStatus.Published,
            VersionToken = CreateVersionToken(ProcessDefinitionEditorCommandKind.Publish),
            Lint = lint
        };
        snapshots[stateKey] = stored;
        return CreateAcceptedResult(
            stored,
            ProcessDefinitionEditorCommandKind.Publish,
            lint,
            observedAtUtc,
            $"Definition '{stored.Draft.Identity.Name}' published from the authoring projection.");
    }

    private ProcessDefinitionEditorCommandResult ExecuteArchive(
        ProcessDefinitionEditorStateKey stateKey,
        ProcessDefinitionEditorSnapshot baseline,
        ProcessDefinitionEditorSnapshot submitted,
        ProcessDefinitionEditorLintProjection lint,
        DateTimeOffset observedAtUtc)
    {
        if (baseline.Status == ProcessDefinitionAuthoringStatus.TemplateDefault)
        {
            var rejected = AddSingleIssue(
                submitted,
                ArchiveTemplateDefaultCode,
                "Template defaults cannot be archived directly.",
                "Save a draft first, then archive the project-scoped draft.");
            return CreateRejectedResult(
                rejected,
                ProcessDefinitionEditorCommandKind.Archive,
                rejected.Lint,
                observedAtUtc,
                "Archive was rejected because the selected definition is still a template default.");
        }

        if (lint.HasBlockingIssues)
        {
            return CreateRejectedResult(
                submitted,
                ProcessDefinitionEditorCommandKind.Archive,
                lint,
                observedAtUtc,
                "Archive was rejected because blocking definition lint issues remain.");
        }

        var stored = submitted with
        {
            Status = ProcessDefinitionAuthoringStatus.Archived,
            VersionToken = CreateVersionToken(ProcessDefinitionEditorCommandKind.Archive),
            Lint = lint
        };
        snapshots[stateKey] = stored;
        return CreateAcceptedResult(
            stored,
            ProcessDefinitionEditorCommandKind.Archive,
            lint,
            observedAtUtc,
            $"Definition '{stored.Draft.Identity.Name}' archived in the authoring projection.");
    }

    private ProcessDefinitionEditorCommandResult ExecuteDelete(
        ProcessDefinitionEditorStateKey stateKey,
        ProcessWorkspaceShellScope scope,
        ProcessDefinitionEditorSnapshot baseline,
        ProcessDefinitionEditorSnapshot submitted,
        ProcessDefinitionEditorLintProjection lint,
        DateTimeOffset observedAtUtc)
    {
        if (baseline.Status == ProcessDefinitionAuthoringStatus.TemplateDefault)
        {
            var rejected = AddSingleIssue(
                submitted,
                DeleteTemplateDefaultCode,
                "Template defaults cannot be deleted directly.",
                "Save a draft first, then delete the project-scoped draft.");
            return CreateRejectedResult(
                rejected,
                ProcessDefinitionEditorCommandKind.Delete,
                rejected.Lint,
                observedAtUtc,
                "Delete was rejected because the selected definition is still a template default.");
        }

        if (lint.HasBlockingIssues)
        {
            return CreateRejectedResult(
                submitted,
                ProcessDefinitionEditorCommandKind.Delete,
                lint,
                observedAtUtc,
                "Delete was rejected because blocking definition lint issues remain.");
        }

        snapshots.Remove(stateKey);
        var template = FindTemplateDefinition(submitted.Draft.DefinitionKey);
        var restored = CreateTemplateSnapshot(scope, template);
        return CreateAcceptedResult(
            restored,
            ProcessDefinitionEditorCommandKind.Delete,
            lint,
            observedAtUtc,
            $"Definition draft '{submitted.Draft.Identity.Name}' deleted; template default remains available.");
    }

    private ProcessDefinitionEditorCommandResult CreateAcceptedResult(
        ProcessDefinitionEditorSnapshot snapshot,
        ProcessDefinitionEditorCommandKind commandKind,
        ProcessDefinitionEditorLintProjection lint,
        DateTimeOffset observedAtUtc,
        string summary)
    {
        var receipt = new ProcessDefinitionEditorCommandReceipt(
            Guid.NewGuid(),
            commandKind,
            ProcessDefinitionEditorCommandStatus.Accepted,
            snapshot.VersionToken,
            observedAtUtc,
            summary,
            lint.Issues);
        return new ProcessDefinitionEditorCommandResult(receipt, CreateProjection(snapshot, receipt));
    }

    private ProcessDefinitionEditorCommandResult CreateRejectedResult(
        ProcessDefinitionEditorSnapshot snapshot,
        ProcessDefinitionEditorCommandKind commandKind,
        ProcessDefinitionEditorLintProjection lint,
        DateTimeOffset observedAtUtc,
        string summary)
    {
        var receipt = new ProcessDefinitionEditorCommandReceipt(
            Guid.NewGuid(),
            commandKind,
            ProcessDefinitionEditorCommandStatus.Rejected,
            snapshot.VersionToken,
            observedAtUtc,
            summary,
            lint.Issues);
        return new ProcessDefinitionEditorCommandResult(receipt, CreateProjection(snapshot with { Lint = lint }, receipt));
    }

    private ProcessDefinitionEditorProjection CreateProjection(
        ProcessDefinitionEditorSnapshot snapshot,
        ProcessDefinitionEditorCommandReceipt? lastReceipt)
        => new(
            snapshot.Draft.DefinitionKey,
            snapshot.VersionToken,
            snapshot.Status,
            snapshot.Draft.Identity with { ScopeLabel = ResolveScopeLabel(snapshot.Scope) },
            snapshot.Draft.Governance with { WorkingStatus = snapshot.Status },
            snapshot.Draft.Contracts,
            snapshot.Draft.Simulation,
            snapshot.Lint,
            CreateCommands(),
            lastReceipt);

    private ProcessDefinitionEditorSnapshot CreateTemplateSnapshot(
        ProcessWorkspaceShellScope scope,
        ProcessTemplateDefinitionSummary template)
    {
        var draft = new ProcessDefinitionEditorDraftProjection(
            new ProcessDefinitionCatalogItemKey(template.Key),
            new ProcessDefinitionEditorIdentityProjection(
                template.DisplayName,
                ResolveScopeLabel(scope),
                template.AuthoringDefaults.CustomerName,
                template.AuthoringDefaults.OwnerName,
                template.Summary,
                template.AuthoringDefaults.ValueStatement),
            new ProcessDefinitionEditorGovernanceProjection(
                ParseEnum(template.Criticality, ProcessDefinitionCriticalityLevel.Unspecified),
                ParseEnum(template.AutonomyLevel, ProcessDefinitionAutonomyLevel.Unspecified),
                ParseEnum(template.OperatingMode, ProcessDefinitionOperatingModeKind.Unspecified),
                ProcessDefinitionAuthoringStatus.TemplateDefault,
                template.AuthoringDefaults.ManagerOverrideSummary,
                template.AuthoringDefaults.GovernanceNotes,
                template.AuthoringDefaults.ChangeSummary,
                template.AuthoringDefaults.GovernancePolicySummary),
            new ProcessDefinitionEditorContractProjection(
                template.AuthoringDefaults.InterfaceContractSummary,
                template.AuthoringDefaults.ConstitutionRuleSummary,
                template.AuthoringDefaults.OperatingModeSummary),
            new ProcessDefinitionEditorSimulationProjection(
                template.AuthoringDefaults.SimulationReadinessSummary,
                template.AuthoringDefaults.StepCount,
                template.AuthoringDefaults.RequiredRoleCount,
                template.AuthoringDefaults.RequiredArtifactExpectationCount,
                !string.IsNullOrWhiteSpace(template.AuthoringDefaults.SimulationReadinessSummary) &&
                template.AuthoringDefaults.StepCount > 0));

        var snapshot = new ProcessDefinitionEditorSnapshot(
            scope,
            ProcessDefinitionAuthoringStatus.TemplateDefault,
            new ProcessDefinitionEditorVersionToken($"template:{template.Key}:{template.UpdatedAtUtc.UtcTicks}"),
            draft,
            LintDraft(draft, strict: false));
        return snapshot;
    }

    private ProcessDefinitionEditorSnapshot NormalizeDraft(
        ProcessWorkspaceShellScope scope,
        ProcessDefinitionEditorDraftProjection draft,
        ProcessDefinitionAuthoringStatus status,
        ProcessDefinitionEditorVersionToken versionToken)
    {
        var normalizedDraft = draft with
        {
            Identity = draft.Identity with
            {
                Name = NormalizeText(draft.Identity.Name),
                CustomerName = NormalizeText(draft.Identity.CustomerName),
                OwnerName = NormalizeText(draft.Identity.OwnerName),
                Summary = NormalizeText(draft.Identity.Summary),
                ValueStatement = NormalizeText(draft.Identity.ValueStatement)
            },
            Governance = draft.Governance with
            {
                WorkingStatus = status,
                ManagerOverrideSummary = NormalizeText(draft.Governance.ManagerOverrideSummary),
                GovernanceNotes = NormalizeText(draft.Governance.GovernanceNotes),
                ChangeSummary = NormalizeText(draft.Governance.ChangeSummary),
                GovernancePolicySummary = NormalizeText(draft.Governance.GovernancePolicySummary)
            },
            Contracts = draft.Contracts with
            {
                InterfaceContractSummary = NormalizeText(draft.Contracts.InterfaceContractSummary),
                ConstitutionRuleSummary = NormalizeText(draft.Contracts.ConstitutionRuleSummary),
                OperatingModeSummary = NormalizeText(draft.Contracts.OperatingModeSummary)
            },
            Simulation = draft.Simulation with
            {
                SimulationReadinessSummary = NormalizeText(draft.Simulation.SimulationReadinessSummary),
                IsReadyForSimulation = !string.IsNullOrWhiteSpace(draft.Simulation.SimulationReadinessSummary) &&
                    draft.Simulation.StepCount > 0
            }
        };

        return new ProcessDefinitionEditorSnapshot(
            scope,
            status,
            versionToken,
            normalizedDraft,
            LintDraft(normalizedDraft, strict: false));
    }

    private static ProcessDefinitionEditorLintProjection Lint(
        ProcessDefinitionEditorSnapshot snapshot,
        bool strict)
        => LintDraft(snapshot.Draft, strict);

    private static ProcessDefinitionEditorLintProjection LintDraft(
        ProcessDefinitionEditorDraftProjection draft,
        bool strict)
    {
        var issues = new List<ProcessDefinitionEditorLintIssueProjection>();

        if (string.IsNullOrWhiteSpace(draft.Identity.Name))
        {
            issues.Add(new ProcessDefinitionEditorLintIssueProjection(
                NameRequiredCode,
                ProcessDefinitionEditorLintSeverity.Error,
                ProcessDefinitionEditorLintSection.Identity,
                "Definition name is required.",
                "Enter a stable, user-facing definition name."));
        }

        if (string.IsNullOrWhiteSpace(draft.Identity.Summary))
        {
            issues.Add(new ProcessDefinitionEditorLintIssueProjection(
                SummaryRequiredCode,
                ProcessDefinitionEditorLintSeverity.Error,
                ProcessDefinitionEditorLintSection.Identity,
                "Definition summary is required.",
                "Summarize the process intent and boundary."));
        }

        if (string.IsNullOrWhiteSpace(draft.Identity.OwnerName))
        {
            issues.Add(new ProcessDefinitionEditorLintIssueProjection(
                OwnerRecommendedCode,
                strict ? ProcessDefinitionEditorLintSeverity.Error : ProcessDefinitionEditorLintSeverity.Warning,
                ProcessDefinitionEditorLintSection.Identity,
                "Definition owner is not set.",
                "Set an accountable owner before publication."));
        }

        if (string.IsNullOrWhiteSpace(draft.Governance.GovernancePolicySummary))
        {
            issues.Add(new ProcessDefinitionEditorLintIssueProjection(
                GovernancePolicyRequiredCode,
                strict ? ProcessDefinitionEditorLintSeverity.Error : ProcessDefinitionEditorLintSeverity.Warning,
                ProcessDefinitionEditorLintSection.Governance,
                "Governance policy summary is empty.",
                "State the rule that blocks unsafe or incomplete execution."));
        }

        if (string.IsNullOrWhiteSpace(draft.Contracts.InterfaceContractSummary))
        {
            issues.Add(new ProcessDefinitionEditorLintIssueProjection(
                ContractRequiredCode,
                strict ? ProcessDefinitionEditorLintSeverity.Error : ProcessDefinitionEditorLintSeverity.Warning,
                ProcessDefinitionEditorLintSection.Contracts,
                "Interface contract summary is empty.",
                "Describe the inputs, outputs, and evidence contract."));
        }

        if (string.IsNullOrWhiteSpace(draft.Simulation.SimulationReadinessSummary) ||
            draft.Simulation.StepCount == 0)
        {
            issues.Add(new ProcessDefinitionEditorLintIssueProjection(
                SimulationRequiredCode,
                strict ? ProcessDefinitionEditorLintSeverity.Error : ProcessDefinitionEditorLintSeverity.Warning,
                ProcessDefinitionEditorLintSection.Simulation,
                "Simulation readiness is incomplete.",
                "Describe the rehearsal path and ensure the definition has at least one step."));
        }

        return new ProcessDefinitionEditorLintProjection(issues);
    }

    private ProcessTemplateDefinitionSummary FindTemplateDefinition(ProcessDefinitionCatalogItemKey definitionKey)
    {
        var pack = templatePackLoader.Load();
        return pack.Definitions.FirstOrDefault(definition =>
            string.Equals(definition.Key, definitionKey.Value, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Process definition '{definitionKey.Value}' is not available in the template pack.");
    }

    private static ProcessDefinitionEditorLintProjection AddIssue(
        ProcessDefinitionEditorLintProjection lint,
        ProcessDefinitionEditorLintIssueProjection issue)
        => new([.. lint.Issues, issue]);

    private static ProcessDefinitionEditorSnapshot AddSingleIssue(
        ProcessDefinitionEditorSnapshot snapshot,
        string code,
        string message,
        string suggestion)
        => snapshot with
        {
            Lint = AddIssue(
                snapshot.Lint,
                new ProcessDefinitionEditorLintIssueProjection(
                    code,
                    ProcessDefinitionEditorLintSeverity.Error,
                    ProcessDefinitionEditorLintSection.Governance,
                    message,
                    suggestion))
        };

    private static IReadOnlyList<ProcessDefinitionEditorCommandProjection> CreateCommands()
        =>
        [
            new(ProcessDefinitionEditorCommandKind.SaveDraft, "Save draft", "save", IsEnabled: true, DisabledReason: null),
            new(ProcessDefinitionEditorCommandKind.Publish, "Publish", "publish", IsEnabled: true, DisabledReason: null),
            new(ProcessDefinitionEditorCommandKind.Archive, "Archive", "archive", IsEnabled: true, DisabledReason: null),
            new(ProcessDefinitionEditorCommandKind.Delete, "Delete", "delete", IsEnabled: true, DisabledReason: null)
        ];

    private ProcessDefinitionEditorVersionToken CreateVersionToken(ProcessDefinitionEditorCommandKind commandKind)
        => new($"{commandKind.ToString().ToLowerInvariant()}:{clock.GetUtcNow():yyyyMMddHHmmss}:{Guid.NewGuid():N}");

    private static string ResolveScopeLabel(ProcessWorkspaceShellScope scope)
        => scope.Kind == ProcessWorkspaceScopeKind.Project
            ? $"Project {scope.ProjectId:D}"
            : "Global";

    private static TEnum ParseEnum<TEnum>(
        string value,
        TEnum defaultValue)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : defaultValue;

    private static string NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static void ValidateScope(ProcessWorkspaceShellScope scope)
    {
        if (scope.Kind == ProcessWorkspaceScopeKind.Project && scope.ProjectId is null)
        {
            throw new ArgumentException("Project-scoped definition editor command requires a project id.", nameof(scope));
        }

        if (scope.Kind == ProcessWorkspaceScopeKind.Global && scope.ProjectId is not null)
        {
            throw new ArgumentException("Global definition editor command cannot carry a project id.", nameof(scope));
        }
    }

    private readonly record struct ProcessDefinitionEditorStateKey(
        ProcessWorkspaceScopeKind ScopeKind,
        Guid? ProjectId,
        ProcessDefinitionCatalogItemKey DefinitionKey)
    {
        public static ProcessDefinitionEditorStateKey From(
            ProcessWorkspaceShellScope scope,
            ProcessDefinitionCatalogItemKey definitionKey)
            => new(scope.Kind, scope.ProjectId, definitionKey);
    }

    private sealed record ProcessDefinitionEditorSnapshot(
        ProcessWorkspaceShellScope Scope,
        ProcessDefinitionAuthoringStatus Status,
        ProcessDefinitionEditorVersionToken VersionToken,
        ProcessDefinitionEditorDraftProjection Draft,
        ProcessDefinitionEditorLintProjection Lint);
}
