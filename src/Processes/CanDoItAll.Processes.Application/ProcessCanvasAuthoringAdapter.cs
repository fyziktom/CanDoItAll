using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessCanvasAuthoringAdapter(ProcessAuthoringWorkspace workspace, ProcessTemplatePackLoader templates, IProcessProjectionClock clock) {
    public async Task<ProcessDefinitionCanvasEditorProjection> ReadAsync(ProcessWorkspaceShellScope scope, ProcessDefinitionCatalogItemKey key, CancellationToken cancellationToken)
        => await ProjectAsync(await workspace.ReadAsync(scope, key, cancellationToken), cancellationToken);

    public async Task<ProcessDefinitionCanvasCommandResult> ExecuteAsync(ProcessDefinitionCanvasCommand submitted, CancellationToken cancellationToken) {
        var (command, fingerprint) = ProcessAuthoringRequests.Capture(submitted);
        if (await workspace.RecoverAsync(command.Scope, command.DefinitionKey, command.ExpectedVersionToken?.Value,
                command.OperationId, fingerprint, cancellationToken) is { } replay) {
            return await ResultAsync(command, workspace.FromReceipt(command.Scope, replay), replay.Outcome, cancellationToken, replay.Selection?.Canvas);
        }
        var baseline = await workspace.ReadAsync(command.Scope, command.DefinitionKey, cancellationToken);
        if (!workspace.Matches(baseline, command.ExpectedVersionToken?.Value)) {
            return await ResultAsync(command, baseline, ProcessAuthoringOutcome.Conflict, cancellationToken);
        }
        var engine = Engine(baseline);
        var before = await engine.GetCanvasAsync(command.Scope, command.DefinitionKey, cancellationToken);
        var normalized = await engine.ExecuteCommandAsync(command, cancellationToken);
        if (normalized.Receipt.Status != ProcessDefinitionCanvasCommandStatus.Accepted) {
            return normalized with { Projection = normalized.Projection with { Observation = baseline.Observation } };
        }
        var content = ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(baseline.Content));
        content = ProcessAuthoringCanvasPatch.Apply(content, command, before, normalized.Projection, templates);
        var saved = await workspace.CommitAsync(baseline, command.OperationId, fingerprint, content, ProcessAuthoringLifecycle.Draft,
            false, new(Canvas: normalized.Projection.Selection), cancellationToken);
        return await ResultAsync(command, workspace.FromReceipt(command.Scope, saved), saved.Outcome, cancellationToken, saved.Selection?.Canvas);
    }

    private async Task<ProcessDefinitionCanvasCommandResult> ResultAsync(ProcessDefinitionCanvasCommand command,
        ProcessAuthoringSession session, ProcessAuthoringOutcome outcome, CancellationToken cancellationToken, ProcessDefinitionCanvasSelectionProjection? selection = null) {
        var projection = await ProjectAsync(session, cancellationToken);
        if (selection is not null) {
            projection = projection with { Selection = selection };
        }
        ProcessDefinitionCanvasCommandReceipt receipt = new(command.OperationId, command.CommandKind,
            outcome == ProcessAuthoringOutcome.Accepted ? ProcessDefinitionCanvasCommandStatus.Accepted : ProcessDefinitionCanvasCommandStatus.Rejected,
            projection.VersionToken, session.CommittedAtUtc ?? clock.GetUtcNow(), outcome == ProcessAuthoringOutcome.Accepted
                ? command.CommandKind == ProcessDefinitionCanvasCommandKind.Recompose ? "The canvas was recomposed and committed." : "The canvas change was committed."
                : "The definition changed. Review the current revision before changing the canvas again.");
        return new(receipt, projection with { LastCommandReceipt = receipt });
    }

    private async Task<ProcessDefinitionCanvasEditorProjection> ProjectAsync(ProcessAuthoringSession session, CancellationToken cancellationToken)
        => (await Engine(session).GetCanvasAsync(session.Scope, new(session.Address.DefinitionKey), cancellationToken)) with { Observation = session.Observation };

    private ProcessDefinitionCanvasEditorProjectionService Engine(ProcessAuthoringSession session) {
        var engine = new ProcessDefinitionCanvasEditorProjectionService(templates, clock);
        engine.InitializeAuthored(session);
        return engine;
    }
}
