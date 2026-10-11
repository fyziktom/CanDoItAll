using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessTemplateAuthoringAdapter(ProcessAuthoringWorkspace workspace, ProcessTemplatePackLoader templates, IProcessProjectionClock clock) {
    public async Task<ProcessTemplateCatalogProjection> ReadAsync(ProcessWorkspaceShellScope scope, ProcessDefinitionCatalogItemKey key,
        ProcessTemplateCatalogQueryProjection query, ProcessDefinitionStepEditorProjection? steps, CancellationToken cancellationToken)
        => await ProjectAsync(await workspace.ReadAsync(scope, key, cancellationToken), query, steps, cancellationToken);

    public async Task<ProcessTemplateImportCommandResult> ExecuteAsync(ProcessTemplateImportCommand submitted,
        ProcessDefinitionStepEditorProjection? steps, CancellationToken cancellationToken) {
        var (command, fingerprint) = ProcessAuthoringRequests.Capture(submitted);
        if (await workspace.RecoverAsync(command.Scope, command.TargetDefinitionKey, command.ExpectedVersionToken?.Value,
                command.OperationId, fingerprint, cancellationToken) is { } replay) {
            return await ResultAsync(command, workspace.FromReceipt(command.Scope, replay), replay.Outcome, steps, null, cancellationToken);
        }
        var baseline = await workspace.ReadAsync(command.Scope, command.TargetDefinitionKey, cancellationToken);
        if (!workspace.Matches(baseline, command.ExpectedVersionToken?.Value)) {
            return await ResultAsync(command, baseline, ProcessAuthoringOutcome.Conflict, steps, null, cancellationToken);
        }
        var normalized = await Engine(baseline).ExecuteCommandAsync(command with { ExpectedVersionToken = null }, steps, cancellationToken);
        if (normalized.Receipt.Status != ProcessTemplateImportCommandStatus.Accepted) {
            return normalized with { Projection = normalized.Projection with { VersionToken = new(baseline.Token), Observation = baseline.Observation } };
        }
        var imported = normalized.Projection.ImportedComponents.Single(item => item.ItemKey == command.ItemKey);
        var source = workspace.ReadTemplate(imported.SourceDefinitionKey);
        var content = ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(baseline.Content));
        try {
            content = ProcessAuthoringImportPatch.Apply(content, source, command, imported);
        } catch (InvalidOperationException exception) {
            return await ResultAsync(command, baseline, ProcessAuthoringOutcome.Conflict, steps, exception.Message, cancellationToken);
        }
        var saved = await workspace.CommitAsync(baseline, command.OperationId, fingerprint, content, ProcessAuthoringLifecycle.Draft,
            false, null, cancellationToken);
        return await ResultAsync(command, workspace.FromReceipt(command.Scope, saved), saved.Outcome, steps, null, cancellationToken);
    }

    private async Task<ProcessTemplateImportCommandResult> ResultAsync(ProcessTemplateImportCommand command,
        ProcessAuthoringSession session, ProcessAuthoringOutcome outcome, ProcessDefinitionStepEditorProjection? steps, string? reason, CancellationToken cancellationToken) {
        var projection = await ProjectAsync(session, command.Query, steps, cancellationToken);
        ProcessTemplateImportCommandReceipt receipt = new(command.OperationId, command.CommandKind,
            outcome == ProcessAuthoringOutcome.Accepted ? ProcessTemplateImportCommandStatus.Accepted : ProcessTemplateImportCommandStatus.Rejected,
            projection.VersionToken, session.CommittedAtUtc ?? clock.GetUtcNow(), reason ?? (outcome == ProcessAuthoringOutcome.Accepted ? "The template content was merged into the captured definition."
                : "The definition changed. Review the current revision before importing again."));
        return new(receipt, projection with { LastImportReceipt = receipt });
    }

    private async Task<ProcessTemplateCatalogProjection> ProjectAsync(ProcessAuthoringSession session, ProcessTemplateCatalogQueryProjection query,
        ProcessDefinitionStepEditorProjection? steps, CancellationToken cancellationToken)
        => (await Engine(session).GetCatalogAsync(session.Scope, new(session.Address.DefinitionKey), query, steps, cancellationToken))
            with { Observation = session.Observation, VersionToken = new(session.Token) };

    private ProcessTemplateCatalogProjectionService Engine(ProcessAuthoringSession session) {
        var engine = new ProcessTemplateCatalogProjectionService(templates, clock);
        engine.InitializeAuthored(session);
        return engine;
    }
}
