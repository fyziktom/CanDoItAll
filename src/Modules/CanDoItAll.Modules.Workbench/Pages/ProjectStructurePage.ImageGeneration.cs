using System.Security.Claims;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Content.UI.Generation;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage {
    [Inject] private IProjectStructureDeferredNodeCompletionQueue DeferredNodeCompletionQueue { get; set; } = default!;
    private sealed class ImageOpening(ProjectStructureAuthoringOpening ownership, ProjectStructureCreateLeafDefinition definition,
        CanvasWorkbenchCreateActionRequest request, ProjectStructureImageAuthority authority) {
        public ProjectStructureAuthoringOpening Ownership { get; } = ownership;
        public ProjectStructureCreateLeafDefinition Definition { get; } = definition;
        public CanvasWorkbenchCreateActionRequest Request { get; } = request;
        public ProjectStructureImageAuthority Authority { get; } = authority;
        public IReadOnlyList<ProviderProfile> Providers { get; set; } = [];
        public ContentImageSetup State { get; set; } = new(ownership.Id, [], true, false, false);
        public bool Queued { get; set; }
    }
    private ImageOpening? imageOpening;
    private readonly Dictionary<ProjectStructureImageAuthority, ProjectStructureActionContext> imageAuthorities = [];

    private async Task<bool> TryCreateGeneratedImageAssetAsync(ProjectStructureCreateLeafDefinition definition,
        CanvasWorkbenchCreateActionRequest request, ProjectStructureActionContext? capturedContext = null) {
        if (!IsGeneratedImageAssetCreateAction(request.ActionId)) {
            return false;
        }
        var context = capturedContext ?? CaptureActionContext();
        var source = context.Surface.Nodes.FirstOrDefault(node => node.Id == request.SourceNodeId);
        var parentId = ProjectStructurePlacementPolicy.ResolveParentNodeId(source, request) ?? $"project:{context.Surface.ProjectId:D}";
        var parent = context.Surface.Nodes.FirstOrDefault(node => node.Id == parentId);
        if (!IsCurrentAction(context) || parent is null || request.SourceNodeId is not null && source is null) {
            return true;
        }
        var actorId = context.Actor is { IsCompletedSuccessfully: true } actor
            ? actor.Result.User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        var authority = new ProjectStructureImageAuthority(context.Admission, context.RuntimeGeneration, actorId);
        var opening = new ImageOpening(new(context, parent), definition, request with {
            ParentNodeId = parent.Id,
            InputValues = request.InputValues?.Select(value => new CanvasWorkbenchInputValue { Key = value.Key, Value = value.Value }).ToArray()
        }, authority);
        CloseImageSetup(imageOpening);
        imageOpening = opening;
        imageAuthorities.Add(authority, context);
        try {
            var providers = (await ProviderRuntimeProfileSource.ListProvidersAsync(deferredCompletionCts.Token))
                .Where(provider => provider.IsEnabled && provider.Purpose == ProviderProfilePurpose.ImageGeneration).ToArray();
            if (IsCurrentImage(opening)) {
                opening.Providers = providers;
                opening.State = new(opening.Ownership.Id, providers.Select(ProjectImageProvider).ToArray(), false, false, true);
            }
        } catch (Exception failure) {
            LogContentFailure(opening.Ownership, ProjectStructureAuthoringOperation.GenerateImage, failure);
            if (IsCurrentImage(opening)) {
                opening.State = opening.State with { IsLoading = false, Message = "Image providers could not be loaded. Close and reopen the setup." };
            }
        } finally {
            if (!ReferenceEquals(opening, imageOpening)) {
                imageAuthorities.Remove(authority);
            }
            await RenderAuthoringOutcomeAsync();
        }
        return true;
    }

    private static ContentImageProvider ProjectImageProvider(ProviderProfile provider) {
        var models = provider.IsSourceManaged
            ? provider.ModelCatalog.Select(model => new ContentImageModel(model.Id, model.DisplayName)).ToArray()
            : provider.SuggestedModels.Append(provider.DefaultModel).Where(model => !string.IsNullOrWhiteSpace(model))
                .Distinct(StringComparer.Ordinal).Select(model => new ContentImageModel(model, provider.GetModelDisplayName(model))).ToArray();
        return new(provider.Id, provider.Name, provider.GetModelDisplayName(provider.DefaultModel), models, !provider.IsSourceManaged);
    }

    private bool IsCurrentImage(ImageOpening opening)
        => ReferenceEquals(imageOpening, opening) && IsCurrentAction(opening.Ownership.Context);

    private void CloseImageSetup(ImageOpening? opening) {
        if (opening is null || !ReferenceEquals(imageOpening, opening)) {
            return;
        }
        imageOpening = null;
        if (!opening.Ownership.IsBusy && !opening.Queued) {
            imageAuthorities.Remove(opening.Authority);
        }
    }

    private void RetireImageAuthorities() {
        foreach (var pair in imageAuthorities) {
            if (!HasOriginalContentAuthority(pair.Value)) {
                pair.Key.Revoke();
            }
        }
        if (imageOpening is { } opening && !IsCurrentImage(opening)) {
            CloseImageSetup(opening);
        }
    }

    private async Task GenerateImageAsync(ImageOpening opening, ContentImageDraft draft) {
        if (!IsCurrentImage(opening) || !opening.State.CanSubmit || opening.Ownership.IsBusy || opening.Ownership.RequiresObservation) {
            return;
        }
        var owner = opening.Ownership;
        var context = owner.Context;
        owner.IsBusy = true;
        opening.State = opening.State with { IsBusy = true, Message = string.Empty };
        var outcome = NewContentOutcome(owner, ProjectStructureAuthoringOperation.GenerateImage);
        var invoked = false;
        try {
            var provider = opening.Providers.FirstOrDefault(provider => provider.Id == draft.ProviderId)
                ?? throw new ArgumentException("Select an available image provider.");
            var model = string.IsNullOrWhiteSpace(draft.Model) ? provider.DefaultModel.Trim() : draft.Model.Trim();
            if (string.IsNullOrWhiteSpace(draft.Prompt) || string.IsNullOrWhiteSpace(model) ||
                provider.IsSourceManaged && !provider.ModelCatalog.Any(item => item.Id == model) ||
                !Enum.IsDefined(draft.Size) || !Enum.IsDefined(draft.Quality) || !Enum.IsDefined(draft.Format)) {
                throw new ArgumentException("Select an available model and supported image settings, and enter a prompt.");
            }
            var format = draft.Format switch {
                ContentImageFormat.Png => AgentGeneratedImageFormat.Png,
                ContentImageFormat.Jpeg => AgentGeneratedImageFormat.Jpeg,
                ContentImageFormat.Webp => AgentGeneratedImageFormat.Webp,
                _ => throw new ArgumentException("Unsupported image format.")
            };
            var request = new ProjectStructureGeneratedImageCompletionRequest(provider.Id, model, draft.Prompt.Trim(),
                draft.Size switch { ContentImageSize.Auto => "auto", ContentImageSize.Square => "1024x1024", ContentImageSize.Portrait => "1024x1536", _ => "1536x1024" },
                draft.Quality.ToString().ToLowerInvariant(), format, BuildGeneratedImageFileName(draft.Title, format)) {
                ProviderFingerprint = ProviderConfigurationFingerprintFactory.Create(provider)
            };
            if (!HasOriginalContentAuthority(context)) {
                throw new ProjectStructureEditConflictException();
            }
            invoked = true;
            var created = await CreateObjectAsync(opening.Definition, opening.Request with {
                UploadedFile = BuildGeneratedImageWaitingPlaceholderUpload(), Title = draft.Title, Subtitle = draft.Usage, Notes = request.Prompt
            }, native => native with {
                ExpectedProjectAdmission = context.Admission, ExpectedParticipants = [owner.Node],
                MetadataJson = ProjectStructureDeferredCompletionMetadataFactory.BuildGeneratedImageMetadataJson(outcome.SubmissionId,
                    ProjectStructureDeferredNodeCompletionState.Queued, request, provider), Status = "Image generation queued"
            }, deferredCompletionCts.Token, onNodeCommitted: node => {
                owner.RequiresObservation = true;
                outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.PartialCommit, Node = node, ProviderId = provider.Id,
                    Message = $"Image placeholder {node.Id} was saved. Generation has not yet been acknowledged by the queue." };
                RecordAuthoringOutcome(owner, outcome);
            }, capturedSurface: context.Surface, capturedNavigationRevision: context.NavigationRevision,
                canPublish: () => IsCurrentImage(opening)) ?? throw new ProjectStructureEditConflictException();
            opening.State = opening.State with { CanSubmit = false, Receipt = new(outcome.SubmissionId,
                ContentImagePhase.PlaceholderSaved, outcome.Message, created.Id, created.RecordId) };
            await RenderAuthoringOutcomeAsync();
            if (!HasOriginalContentAuthority(context)) {
                opening.Authority.Revoke();
                throw new ProjectStructureEditConflictException();
            }
            var handle = await DeferredNodeCompletionQueue.EnqueueAsync(new(outcome.SubmissionId, context.Admission.ProjectId,
                created.Id, ProjectStructureDeferredNodeCompletionKind.GeneratedImageAsset, request) {
                Origin = new(opening.Authority, created)
            }, deferredCompletionCts.Token);
            opening.Queued = true;
            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed,
                Message = $"Generation was queued for original placeholder {created.Id}. The queue is in memory; completion is not yet confirmed." };
            RecordAuthoringOutcome(owner, outcome);
            opening.State = opening.State with { Receipt = new(handle.OperationId, ContentImagePhase.Queued, outcome.Message, created.Id, created.RecordId) };
            _ = TrackAuthoringOperationAsync(ObserveImageCompletionAsync(opening, handle, created, outcome));
        } catch (Exception failure) {
            outcome = ContentFailure(owner, outcome, invoked, failure);
            RecordAuthoringOutcome(owner, outcome);
            opening.State = opening.State with { CanSubmit = !owner.RequiresObservation,
                Message = outcome.Message, Receipt = new(outcome.SubmissionId,
                    outcome.Node is not null ? ContentImagePhase.PlaceholderSaved : owner.RequiresObservation ? ContentImagePhase.Unconfirmed : ContentImagePhase.Rejected,
                    outcome.Message, outcome.Node?.Id, outcome.Node?.RecordId) };
        } finally {
            owner.IsBusy = false;
            opening.State = opening.State with { IsBusy = false };
            if (!opening.Queued && !ReferenceEquals(imageOpening, opening)) {
                imageAuthorities.Remove(opening.Authority);
            }
            await RenderAuthoringOutcomeAsync();
        }
    }

    private async Task ObserveImageCompletionAsync(ImageOpening opening, ProjectStructureDeferredNodeCompletionHandle handle,
        ProjectStructureNode placeholder, ProjectStructureAuthoringOutcome accepted) {
        try {
            var result = await handle.Completion.WaitAsync(deferredCompletionCts.Token);
            if (result.OperationId != handle.OperationId || result.ProjectId != accepted.Project.ProjectId || result.NodeId != placeholder.Id ||
                result.UpdatedNode is { } changed && changed.RecordId != placeholder.RecordId) {
                throw new InvalidOperationException("Image completion does not match its original placeholder.");
            }
            var receipt = accepted with {
                Node = result.UpdatedNode ?? placeholder,
                Kind = result.IsSuccess ? ProjectStructureAuthoringResultKind.Committed : ProjectStructureAuthoringResultKind.PartialCommit,
                ExternalEffect = result.ProviderCompleted ? ProjectStructureExternalEffectState.Completed
                    : result.ProviderInvoked ? ProjectStructureExternalEffectState.Dispatched : ProjectStructureExternalEffectState.NotStarted,
                Message = result.Message, StoredMedia = result.StoredMedia
            };
            RecordAuthoringOutcome(opening.Ownership, receipt);
            await InvokeAsync(async () => {
                opening.State = opening.State with { Receipt = new(result.OperationId,
                    result.IsSuccess ? ContentImagePhase.MediaSaved : result.ProviderCompleted ? ContentImagePhase.ProviderCompleted
                        : result.ProviderInvoked ? ContentImagePhase.Unconfirmed : ContentImagePhase.Rejected,
                    result.Message, placeholder.Id, placeholder.RecordId, result.ProviderInvoked, result.ProviderCompleted, result.ContentSha256) };
                if (IsCurrentAction(opening.Ownership.Context) && result.UpdatedNode is { } updated &&
                    surface?.Nodes.Any(node => node.Id == placeholder.Id && node.RecordId == placeholder.RecordId) is true) {
                    await ApplySurfaceNodeUpdatesAsync([updated]);
                }
                StateHasChanged();
            });
        } catch (OperationCanceledException) when (deferredCompletionCts.IsCancellationRequested) {
        } catch (Exception failure) {
            LogContentFailure(opening.Ownership, ProjectStructureAuthoringOperation.GenerateImage, failure);
            if (IsCurrentImage(opening)) {
                opening.State = opening.State with { Message = "The original queued operation could not be observed. Inspect its placeholder; do not generate it again." };
                await RenderAuthoringOutcomeAsync();
            }
        } finally {
            imageAuthorities.Remove(opening.Authority);
        }
    }
    private static CanvasWorkbenchUploadedFile BuildGeneratedImageWaitingPlaceholderUpload()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024">
              <rect width="1024" height="1024" rx="64" fill="#f8fafc"/>
              <rect x="96" y="96" width="832" height="832" rx="48" fill="#e0f2fe" stroke="#0f766e" stroke-width="8"/>
              <circle cx="512" cy="390" r="92" fill="#ffffff" opacity="0.92"/>
              <path d="M512 314v76l52 30" fill="none" stroke="#0f172a" stroke-width="22" stroke-linecap="round" stroke-linejoin="round"/>
              <text x="512" y="560" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="42" font-weight="700" fill="#0f172a">Waiting for Image creation by AI...</text>
              <text x="512" y="622" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="28" font-weight="500" fill="#475569">The generated image will replace this placeholder.</text>
            </svg>
            """;

        return new CanvasWorkbenchUploadedFile
        {
            FileName = "waiting-for-image-creation-by-ai.svg",
            ContentType = "image/svg+xml",
            Base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))
        };
    }

    private static string BuildGeneratedImageFileName(string? title, AgentGeneratedImageFormat format)
    {
        var extension = format switch
        {
            AgentGeneratedImageFormat.Jpeg => ".jpg",
            AgentGeneratedImageFormat.Webp => ".webp",
            _ => ".png"
        };
        var stem = new string((string.IsNullOrWhiteSpace(title) ? "generated-image" : title.Trim())
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray());
        stem = string.Join('-', stem.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(stem)
            ? $"generated-image{extension}"
            : $"{stem[..Math.Min(stem.Length, 64)]}{extension}";
    }

    private static bool IsGeneratedImageAssetCreateAction(string? actionId)
        => string.Equals(actionId, ProjectStructureCanvasCatalog.GenerateImageAssetActionId, StringComparison.Ordinal);

}
