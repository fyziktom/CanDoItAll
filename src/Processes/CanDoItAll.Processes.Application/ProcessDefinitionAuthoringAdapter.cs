using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessDefinitionAuthoringAdapter(ProcessAuthoringWorkspace workspace, ProcessTemplatePackLoader templates, IProcessProjectionClock clock) {
    public async Task<ProcessDefinitionEditorProjection> ReadAsync(ProcessWorkspaceShellScope scope, ProcessDefinitionCatalogItemKey key, CancellationToken cancellationToken) {
        var session = await workspace.ReadAsync(scope, key, cancellationToken);
        return await ProjectAsync(session, cancellationToken);
    }

    public async Task<ProcessDefinitionEditorCommandResult> ExecuteAsync(ProcessDefinitionEditorCommand submitted, CancellationToken cancellationToken) {
        var (command, fingerprint) = ProcessAuthoringRequests.Capture(submitted);
        if (await workspace.RecoverAsync(command.Scope, command.DefinitionKey, command.ExpectedVersionToken?.Value,
                command.OperationId, fingerprint, cancellationToken) is { } replay) {
            return await ResultAsync(command, workspace.FromReceipt(command.Scope, replay), replay.Outcome, cancellationToken);
        }
        var baseline = await workspace.ReadAsync(command.Scope, command.DefinitionKey, cancellationToken);
        if (!workspace.Matches(baseline, command.ExpectedVersionToken?.Value)) {
            return await ResultAsync(command, baseline, ProcessAuthoringOutcome.Conflict, cancellationToken);
        }
        var engine = Engine(baseline);
        var normalized = await engine.ExecuteCommandAsync(command, cancellationToken);
        if (normalized.Receipt.Status != ProcessDefinitionEditorCommandStatus.Accepted) {
            return normalized with { Projection = normalized.Projection with { Observation = baseline.Observation } };
        }
        var content = ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(baseline.Content));
        Patch(content.Definition, normalized.Projection);
        var lifecycle = command.CommandKind switch {
            ProcessDefinitionEditorCommandKind.Publish => ProcessAuthoringLifecycle.Published,
            ProcessDefinitionEditorCommandKind.Archive => ProcessAuthoringLifecycle.Archived,
            ProcessDefinitionEditorCommandKind.Delete => ProcessAuthoringLifecycle.Deleted,
            _ => ProcessAuthoringLifecycle.Draft
        };
        if (lifecycle == ProcessAuthoringLifecycle.Deleted) {
            content = await workspace.InheritedContentAsync(baseline, cancellationToken);
        }
        var saved = await workspace.CommitAsync(baseline, command.OperationId, fingerprint, content, lifecycle,
            command.CommandKind == ProcessDefinitionEditorCommandKind.Publish, null, cancellationToken);
        return await ResultAsync(command, workspace.FromReceipt(command.Scope, saved), saved.Outcome, cancellationToken);
    }

    private async Task<ProcessDefinitionEditorCommandResult> ResultAsync(ProcessDefinitionEditorCommand command,
        ProcessAuthoringSession session, ProcessAuthoringOutcome outcome, CancellationToken cancellationToken) {
        var projection = await ProjectAsync(session, cancellationToken);
        ProcessDefinitionEditorCommandReceipt receipt = new(command.OperationId, command.CommandKind,
            outcome == ProcessAuthoringOutcome.Accepted ? ProcessDefinitionEditorCommandStatus.Accepted : ProcessDefinitionEditorCommandStatus.Rejected,
            projection.VersionToken, session.CommittedAtUtc ?? clock.GetUtcNow(), outcome == ProcessAuthoringOutcome.Accepted
                ? "The definition was committed to its captured workspace." : "The definition changed. Review the current revision before submitting this draft again.", []);
        return new(receipt, projection with { LastCommandReceipt = receipt });
    }

    private async Task<ProcessDefinitionEditorProjection> ProjectAsync(ProcessAuthoringSession session, CancellationToken cancellationToken)
        => (await Engine(session).GetEditorAsync(session.Scope, new(session.Address.DefinitionKey), cancellationToken)) with { Observation = session.Observation };

    private ProcessDefinitionEditorProjectionService Engine(ProcessAuthoringSession session) {
        var engine = new ProcessDefinitionEditorProjectionService(templates, clock);
        engine.InitializeAuthored(session);
        return engine;
    }

    private static void Patch(ProcessTemplateDefinitionDocument target, ProcessDefinitionEditorProjection patch) {
        target.DisplayName = patch.Identity.Name;
        target.CustomerName = patch.Identity.CustomerName;
        target.OwnerName = patch.Identity.OwnerName;
        target.Summary = patch.Identity.Summary;
        target.ValueStatement = patch.Identity.ValueStatement;
        target.Criticality = patch.Governance.Criticality.ToString();
        target.AutonomyLevel = patch.Governance.AutonomyLevel.ToString();
        target.OperatingMode = patch.Governance.OperatingMode.ToString();
        target.ManagerOverrideSummary = patch.Governance.ManagerOverrideSummary;
        target.GovernanceNotes = patch.Governance.GovernanceNotes;
        target.ChangeSummary = patch.Governance.ChangeSummary;
        target.GovernancePolicySummary = patch.Governance.GovernancePolicySummary;
        target.InterfaceContractSummary = patch.Contracts.InterfaceContractSummary;
        target.ConstitutionRuleSummary = patch.Contracts.ConstitutionRuleSummary;
        target.OperatingModeSummary = patch.Contracts.OperatingModeSummary;
        target.SimulationReadinessSummary = patch.Simulation.SimulationReadinessSummary;
    }
}
