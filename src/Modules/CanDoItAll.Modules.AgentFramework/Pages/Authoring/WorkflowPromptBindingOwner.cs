using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Prompts;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Authoring;

public sealed class WorkflowPromptBindingOwner(IPromptGalleryService prompts,
    IWorkflowComponentLibraryService library, ILogger logger) {
    private bool pending;
    private bool unknown;
    public IReadOnlyList<LlmCallComponent> Accepted => accepted;
    private readonly List<LlmCallComponent> accepted = [];

    public async Task<WorkflowPromptBindingOutcome> BindAsync(WorkflowPromptBindingRequest request, CancellationToken owner) {
        if (owner.IsCancellationRequested) {
            return new WorkflowPromptBindingOutcome.Rejected("This editor is no longer active.");
        }
        if (pending || unknown) {
            return new WorkflowPromptBindingOutcome.Unknown();
        }
        pending = true;
        var dispatched = false;
        try {
            var selection = request.Selection;
            var compatibility = await prompts.EvaluateCompatibilityAsync(selection.ArtifactId,
                new(PromptGalleryConsumer.Workflow, PromptGalleryCompatibilityPurpose.Selection,
                    Provider: request.Provider.Kind.ToString(), Model: request.Model), owner);
            if (owner.IsCancellationRequested) {
                return new WorkflowPromptBindingOutcome.Rejected("This editor is no longer active.");
            }
            if (compatibility.IsFailure || compatibility.Value is not { CanUse: true }) {
                return new WorkflowPromptBindingOutcome.Rejected("The Prompt owner did not approve this provider and model binding.");
            }
            var current = request.Current;
            var submission = new LlmCallComponentSaveRequest(null, selection.Title,
                request.Provider.ProviderProfileId, request.Model, current?.Modality ?? WorkflowModality.Text,
                new(selection.Recommendations.Temperature ?? current?.ModelSettings.Temperature ?? 0.2,
                    selection.Recommendations.MaxOutputTokens ?? current?.ModelSettings.MaxOutputTokens ?? 800,
                    current?.ModelSettings.RequireJsonOutput ?? false,
                    current?.ModelSettings.ResponseFormatJsonSchema ?? string.Empty),
                selection.Content, current?.InputShape ?? WorkflowValueShape.Text,
                current?.ResultShape ?? WorkflowValueShape.Text, current?.Permissions ?? AgentPermissionsPolicy.Default) {
                PromptArtifactId = selection.ArtifactId,
                PromptVersionId = selection.VersionId
            };
            dispatched = true;
            var component = await library.SaveComponentAsync(submission, CancellationToken.None);
            accepted.Add(component);
            return new WorkflowPromptBindingOutcome.Accepted(component);
        } catch (Exception error) {
            unknown = dispatched;
            logger.LogWarning("Workflow Prompt binding failed at {Phase} for {PromptId}/{PromptVersionId}: {FailureType}",
                dispatched ? "component acknowledgement" : "compatibility", request.Selection.ArtifactId,
                request.Selection.VersionId, error.GetType().Name);
            return unknown ? new WorkflowPromptBindingOutcome.Unknown()
                : new WorkflowPromptBindingOutcome.Rejected("Prompt compatibility could not be confirmed. No component was submitted.");
        } finally {
            pending = false;
        }
    }
}
