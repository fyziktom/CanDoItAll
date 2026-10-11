using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessStepAuthoringAdapter(ProcessAuthoringWorkspace workspace, ProcessTemplatePackLoader templates, IProcessProjectionClock clock,
    ProcessExecutableDefinitionResolver resolver) {
    public async Task<ProcessDefinitionStepEditorProjection> ReadAsync(ProcessWorkspaceShellScope scope, ProcessDefinitionCatalogItemKey key, CancellationToken cancellationToken)
        => await ProjectAsync(await workspace.ReadAsync(scope, key, cancellationToken), null, cancellationToken);

    public async Task<ProcessDefinitionStepEditorCommandResult> ExecuteAsync(ProcessDefinitionStepEditorCommand submitted, CancellationToken cancellationToken) {
        var (command, fingerprint) = ProcessAuthoringRequests.Capture(submitted);
        if (await workspace.RecoverAsync(command.Scope, command.DefinitionKey, command.ExpectedVersionToken?.Value,
                command.OperationId, fingerprint, cancellationToken) is { } replay) {
            return await ResultAsync(command, workspace.FromReceipt(command.Scope, replay), replay.Outcome, replay.Selection?.StepKey, null, cancellationToken);
        }
        var baseline = await workspace.ReadAsync(command.Scope, command.DefinitionKey, cancellationToken);
        var key = command.Draft.Basic.StepKey.Value;
        if (!workspace.Matches(baseline, command.ExpectedVersionToken?.Value) || !baseline.Content.Definition.Steps.Any(step => step.Key == key)) {
            return await ResultAsync(command, baseline, ProcessAuthoringOutcome.Conflict, key, null, cancellationToken);
        }
        var roles = baseline.Content.Definition.RoleUsages.Select(role => role.Key).ToHashSet(StringComparer.Ordinal);
        if (command.Draft.Basic.DecisionRoleKey is { } decisionRole && !roles.Contains(decisionRole.Value) ||
                command.Draft.RoleBindings.Any(binding => binding.StepKey.Value != key || !roles.Contains(binding.RoleKey.Value))) {
            return await ResultAsync(command, baseline, ProcessAuthoringOutcome.Conflict, key, "Choose roles belonging to this definition and step.", cancellationToken);
        }
        var normalized = await Engine(baseline).ExecuteCommandAsync(command, cancellationToken);
        if (normalized.Receipt.Status != ProcessDefinitionStepCommandStatus.Accepted) {
            return normalized with { Projection = normalized.Projection with { Observation = baseline.Observation } };
        }
        var content = ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(baseline.Content));
        var step = content.Definition.Steps.Single(step => step.Key == key);
        var previousChild = step.SubprocessProcessKey;
        var previousMappings = ChildMappings(step);
        ProcessAuthoringStepPatch.Apply(step, normalized.Projection.SelectedStep!);
        if (command.CommandKind == ProcessDefinitionStepCommandKind.MapSubprocess || previousChild != step.SubprocessProcessKey ||
                !previousMappings.SequenceEqual(ChildMappings(step))) {
            try {
                if (string.IsNullOrWhiteSpace(step.SubprocessProcessKey)) {
                    step.SubprocessContract = null;
                } else {
                    var child = await resolver.ResolveAsync(command.Scope, step.SubprocessProcessKey, cancellationToken: cancellationToken);
                    ProcessAuthoringSubprocessPatch.Apply(step, ProcessExecutableDefinitionResolver.Decode(child, child.DefinitionKey).Definition);
                }
            } catch (InvalidOperationException exception) {
                return await ResultAsync(command, baseline, ProcessAuthoringOutcome.Conflict, key, "Subprocess mapping refused: " + exception.Message, cancellationToken);
            }
        }
        var saved = await workspace.CommitAsync(baseline, command.OperationId, fingerprint, content, ProcessAuthoringLifecycle.Draft,
            false, new(StepKey: key), cancellationToken);
        return await ResultAsync(command, workspace.FromReceipt(command.Scope, saved), saved.Outcome, saved.Selection?.StepKey, null, cancellationToken);
    }

    private static (string Step, string Artifact, Guid? Id)[] ChildMappings(ProcessTemplateDefinitionStepDocument step)
        => step.ArtifactExpectations.Where(item => !string.IsNullOrEmpty(item.SubprocessChildStepKey) ||
                !string.IsNullOrEmpty(item.SubprocessChildArtifactTitle) || item.SubprocessChildArtifactExpectationId is not null)
            .Select(item => (item.SubprocessChildStepKey, item.SubprocessChildArtifactTitle, item.SubprocessChildArtifactExpectationId)).ToArray();

    private async Task<ProcessDefinitionStepEditorCommandResult> ResultAsync(ProcessDefinitionStepEditorCommand command,
        ProcessAuthoringSession session, ProcessAuthoringOutcome outcome, string? selectedKey, string? reason, CancellationToken cancellationToken) {
        var projection = await ProjectAsync(session, selectedKey, cancellationToken);
        ProcessDefinitionStepCommandReceipt receipt = new(command.OperationId, command.CommandKind,
            outcome == ProcessAuthoringOutcome.Accepted ? ProcessDefinitionStepCommandStatus.Accepted : ProcessDefinitionStepCommandStatus.Rejected,
            projection.VersionToken, session.CommittedAtUtc ?? clock.GetUtcNow(), reason ?? (outcome == ProcessAuthoringOutcome.Accepted ? "The step change was committed."
                : "The definition changed. Review the current revision before saving this step again."), []);
        return new(receipt, projection with { LastCommandReceipt = receipt });
    }

    private async Task<ProcessDefinitionStepEditorProjection> ProjectAsync(ProcessAuthoringSession session, string? selectedKey, CancellationToken cancellationToken) {
        var projection = await Engine(session).GetEditorAsync(session.Scope, new(session.Address.DefinitionKey), cancellationToken);
        var selected = selectedKey is null ? projection.SelectedStep : projection.StepDrafts.FirstOrDefault(step => step.Basic.StepKey.Value == selectedKey);
        return projection with { Observation = session.Observation, SelectedStep = selected, SelectedStepKey = selected?.Basic.StepKey,
            Steps = projection.Steps.Select(step => step with { IsSelected = step.StepKey == selected?.Basic.StepKey }).ToArray() };
    }

    private ProcessDefinitionStepEditorProjectionService Engine(ProcessAuthoringSession session) {
        var engine = new ProcessDefinitionStepEditorProjectionService(templates, clock);
        engine.InitializeAuthored(session);
        return engine;
    }
}
