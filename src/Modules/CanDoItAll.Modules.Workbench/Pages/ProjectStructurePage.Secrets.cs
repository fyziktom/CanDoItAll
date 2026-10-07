using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Workbench.Operators.UI.Secrets;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage {
    [Inject] private IWorkspaceSecretsOwner SecretsOwner { get; set; } = default!;
    private ProjectSecretReferenceSession? secretSession;
    private readonly Queue<SecretReferenceReceipt> secretOutcomes = new();

    private async Task TrackSecretOperationAsync(Task operation) {
        try {
            await TrackAuthoringOperationAsync(operation);
        } finally {
            await RenderAuthoringOutcomeAsync();
        }
    }

    private Task OpenSecretReferenceDialogAsync(CanvasWorkbenchAction action, CanvasWorkbenchCreateActionRequest request,
        ProjectStructureActionContext? capturedContext = null) {
        if (!ProjectStructureCanvasCatalog.TryResolveCreateDefinition(action.ActionId, out var definition)) {
            throw new InvalidOperationException("The secret reference action is not registered.");
        }
        return OpenSecretSessionAsync(capturedContext ?? CaptureActionContext(), null, definition, request);
    }

    private Task OpenSecretReferenceEditDialogAsync(ProjectStructureNode node, ProjectStructureActionContext? capturedContext = null)
        => OpenSecretSessionAsync(capturedContext ?? CaptureActionContext(), node, null, null);

    private async Task OpenSecretSessionAsync(ProjectStructureActionContext context, ProjectStructureNode? edited,
        ProjectStructureCreateLeafDefinition? definition, CanvasWorkbenchCreateActionRequest? request) {
        if (!IsCurrentAction(context) || !HasOriginalContentAuthority(context)) {
            return;
        }
        var original = edited ?? context.Surface.Nodes.FirstOrDefault(node => node.Id == (request?.SourceNodeId ?? request?.ParentNodeId))
            ?? context.Surface.Nodes.First(node => node.ProjectRole == ProjectStructureProjectRole.ActiveProject);
        var parentId = request is null ? null : ProjectStructurePlacementPolicy.ResolveParentNodeId(original, request);
        var participants = context.Surface.Nodes.Where(node => node.Id == original.Id || node.Id == parentId).ToArray();
        CloseSecretReferenceDialog();
        ProjectSecretReferenceSession? opening = null;
        opening = new(context.Admission, original, edited is not null, SecretsOwner, ProjectWorkbenchService,
            () => ReferenceEquals(secretSession, opening) && IsCurrentAction(context), () => HasOriginalContentAuthority(context),
            async (secret, input, onCommitted) => {
                var purpose = string.IsNullOrWhiteSpace(input.Purpose) ? "Project structure reference" : input.Purpose.Trim();
                var note = input.Note.Trim();
                if (edited is not null) {
                    var metadata = ProjectObjectMetadataSerializer.Parse(edited.MetadataJson);
                    metadata.SecretReference = new() { SecretId = secret.Id, SecretNameSnapshot = secret.Name, Purpose = purpose, ExternalReference = note };
                    var updated = await ProjectWorkbenchService.UpdateObjectAsync(context.Admission.ProjectId, edited.Id,
                        new(secret.Name, note, purpose, edited.StartUtc, edited.EndUtc,
                            ProjectObjectMetadataSerializer.SerializePreservingUnknownProperties(edited.MetadataJson, metadata), edited.DurationSeconds, edited.NodeReferences) {
                            ExpectedProjectAdmission = context.Admission, ExpectedNode = edited
                        }) ?? throw new ProjectStructureEditConflictException();
                    onCommitted(updated);
                    return updated;
                }
                var submitted = request! with { Title = secret.Name, Subtitle = note, Notes = purpose,
                    InputValues = [new() { Key = "secretId", Value = secret.Id.ToString("D") }, new() { Key = "secretName", Value = secret.Name }] };
                return await CreateObjectAsync(definition!, submitted,
                    configureRequest: prepared => prepared with { ExpectedProjectAdmission = context.Admission, ExpectedParticipants = participants },
                    onNodeCommitted: onCommitted, capturedSurface: context.Surface, capturedNavigationRevision: context.NavigationRevision,
                    canPublish: () => opening!.IsCurrent) ?? throw new ProjectStructureEditConflictException();
            }, node => ReloadSurfaceAsync(node.Id, () => opening!.IsCurrent), receipt => {
                secretOutcomes.Enqueue(receipt);
                while (secretOutcomes.Count > 16) {
                    secretOutcomes.Dequeue();
                }
                Logger.LogInformation("Secret reference opening {OpeningId}, original project {ProjectId}, phase {Phase}: secret {SecretId}, node {NodeId}.",
                    opening!.State.OpeningId, context.Admission.ProjectId, receipt.Phase, receipt.SecretId, receipt.NodeId);
                if (HasOriginalContentAuthority(context)) {
                    var message = receipt.Phase == SecretReferencePhase.VaultCommitted
                        ? $"Secret {receipt.SecretId:D} was created; completion of its original project reference is pending. {receipt.Message}"
                        : receipt.Message;
                    ReportActionResult(context, message, receipt.Phase == SecretReferencePhase.Observed ? "mint" : "warn");
                }
            }, Logger);
        if (edited is not null) {
            var metadata = ProjectObjectMetadataSerializer.Parse(edited.MetadataJson).SecretReference;
            opening.State.Draft.SelectedId = metadata?.SecretId is { } id && id != Guid.Empty ? id : null;
            opening.State.Draft.Purpose = string.IsNullOrWhiteSpace(metadata?.Purpose) ? edited.Notes : metadata.Purpose;
            opening.State.Draft.Note = metadata?.ExternalReference ?? edited.Subtitle;
        }
        secretSession = opening;
        await InvokeAsync(StateHasChanged);
        await opening.LoadAsync();
    }

    private void CloseSecretReferenceDialog(ProjectSecretReferenceSession opening) {
        opening.Retire();
        if (ReferenceEquals(secretSession, opening)) {
            secretSession = null;
        }
    }

    private void CloseSecretReferenceDialog() {
        secretSession?.Retire();
        secretSession = null;
    }

    private static bool IsSecretReferenceCreateRequest(CanvasWorkbenchCreateActionRequest request)
        => IsSecretReferenceCreateAction(request.ActionId) || string.Equals(request.CreateMode, "secret-reference-picker", StringComparison.Ordinal);
}
