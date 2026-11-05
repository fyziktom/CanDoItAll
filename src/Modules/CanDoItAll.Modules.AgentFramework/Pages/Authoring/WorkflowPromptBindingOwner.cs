using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Prompts;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Authoring;

public sealed class WorkflowPromptBindingOwner(IPromptGalleryService prompts,
    IWorkflowComponentLibraryService library, ILogger logger, WorkflowChanges changes,
    Func<bool, CancellationToken, ValueTask> authorize) {
    private readonly Guid editorId = Guid.NewGuid();
    private bool pending;
    private WorkflowComponentId? unknown;
    public IReadOnlyList<LlmCallComponent> Accepted => accepted;
    private readonly List<LlmCallComponent> accepted = [];

    public async Task<WorkflowPromptBindingOutcome> BindAsync(WorkflowPromptBindingRequest request, CancellationToken owner) {
        if (owner.IsCancellationRequested) {
            return new WorkflowPromptBindingOutcome.Rejected("This editor is no longer active.");
        }
        if (unknown is { } uncertainId) {
            return new WorkflowPromptBindingOutcome.Unknown(uncertainId);
        }
        if (pending) {
            return new WorkflowPromptBindingOutcome.Rejected("Another Prompt binding is already pending.");
        }
        pending = true;
        var dispatched = false;
        var componentId = WorkflowComponentId.New();
        try {
            await authorize(true, owner);
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
            var submission = new LlmCallComponentSaveRequest(componentId, selection.Title,
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
            await authorize(true, owner);
            using var admission = changes.EnterUiWrite(token => {
                owner.ThrowIfCancellationRequested();
                return authorize(true, token);
            });
            using var operation = changes.Enter(new(editorId, Guid.NewGuid()));
            dispatched = true;
            var component = await library.SaveComponentAsync(submission, CancellationToken.None);
            accepted.Add(component);
            return new WorkflowPromptBindingOutcome.Accepted(component);
        } catch (UnauthorizedAccessException) {
            return new WorkflowPromptBindingOutcome.Rejected("Current workflow editing access is required. No component was saved.");
        } catch (WorkflowComponentConcurrencyException) {
            return new WorkflowPromptBindingOutcome.Rejected("The component or its Prompt changed. Check the current library before binding again.");
        } catch (Exception error) {
            unknown = dispatched ? componentId : null;
            logger.LogWarning("Workflow Prompt binding failed at {Phase} for component {ComponentId}, Prompt {PromptId}/{PromptVersionId}: {FailureType}",
                dispatched ? "component acknowledgement" : "compatibility", componentId.Value, request.Selection.ArtifactId,
                request.Selection.VersionId, error.GetType().Name);
            return unknown is { } id ? new WorkflowPromptBindingOutcome.Unknown(id)
                : new WorkflowPromptBindingOutcome.Rejected("Prompt compatibility could not be confirmed. No component was submitted.");
        } finally {
            pending = false;
        }
    }
}
