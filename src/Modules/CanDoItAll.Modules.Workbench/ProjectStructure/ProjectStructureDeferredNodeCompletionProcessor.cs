using System.Security.Cryptography;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureDeferredNodeCompletionProcessor(
    IProviderRuntimeProfileSource providerSource,
    IAgentImageGenerationService imageGenerationService,
    ProjectWorkbenchService workbench,
    ICanonicalRuntimeDatabase database,
    ILogger<ProjectStructureDeferredNodeCompletionProcessor> logger) {
    public async Task<ProjectStructureDeferredNodeCompletionResult> ProcessAsync(ProjectStructureDeferredNodeCompletionRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        var origin = ProjectStructureImageOrigin.Validate(request);
        var imageRequest = request.GeneratedImage!;
        var expected = origin.Placeholder;
        ProviderProfile? provider = null;
        var invoked = false;
        var completed = false;
        var persistenceAttempted = false;
        string? hash = null;
        ProjectStructureContentMediaReceipt? storedMedia = null;
        try {
            if (request.Kind != ProjectStructureDeferredNodeCompletionKind.GeneratedImageAsset) {
                throw new ArgumentException("Unsupported deferred content operation.");
            }
            RequireAuthority(origin.Authority);
            await workbench.RequireContentCurrentAsync(origin.Authority.Project, expected, cancellationToken);
            provider = await RequireProviderAsync(imageRequest, cancellationToken);
            RequireAuthority(origin.Authority);
            expected = await workbench.UpdateContentMetadataAsync(origin.Authority.Project, expected,
                Metadata(ProjectStructureDeferredNodeCompletionState.Running), "Image generation running", cancellationToken: cancellationToken)
                ?? throw new ProjectStructureEditConflictException();
            RequireAuthority(origin.Authority);
            await workbench.RequireContentCurrentAsync(origin.Authority.Project, expected, cancellationToken);
            invoked = true;
            var generated = await imageGenerationService.GenerateAsync(new(provider, imageRequest.Model, imageRequest.Prompt,
                imageRequest.Size, imageRequest.Quality, imageRequest.Format, []), cancellationToken);
            completed = true;
            var image = generated.Images.FirstOrDefault() ?? throw new InvalidDataException("The provider returned no image.");
            var contentType = ValidateImage(image, imageRequest.Format);
            hash = Convert.ToHexString(SHA256.HashData(image.Bytes));
            RequireAuthority(origin.Authority);
            await RequireProviderAsync(imageRequest, cancellationToken);
            RequireAuthority(origin.Authority);
            expected = await workbench.UpdateContentMetadataAsync(origin.Authority.Project, expected,
                Metadata(ProjectStructureDeferredNodeCompletionState.Running), "Image generated; attachment pending", cancellationToken: cancellationToken)
                ?? throw new ProjectStructureEditConflictException();
            RequireAuthority(origin.Authority);
            persistenceAttempted = true;
            var saved = await workbench.ReplaceContentMediaAsync(origin.Authority.Project, expected,
                new(imageRequest.FileName, contentType, Convert.ToBase64String(image.Bytes)),
                Metadata(ProjectStructureDeferredNodeCompletionState.Completed), "Generated image ready", cancellationToken)
                ?? throw new ProjectStructureEditConflictException();
            storedMedia = new(origin.Authority.Project, saved.Id, saved.RecordId!.Value, saved.StorageObjectReferenceJson,
                saved.MediaOriginalFileName, saved.MediaContentType);
            return Result(true, $"{imageRequest.FileName} was generated and saved for its original node.", saved);
        } catch (Exception failure) {
            storedMedia = (failure as ProjectStructureContentMediaWriteException)?.Receipt;
            logger.LogWarning("Generated image operation {OperationId} for project {ProjectId}, lifetime {LifetimeId}, node {NodeId} stopped. ProviderInvoked={Invoked} ProviderCompleted={Completed} PersistenceAttempted={PersistenceAttempted} FailureType={FailureType}",
                request.OperationId, request.ProjectId, origin.Authority.Project.LifetimeId, request.NodeId, invoked, completed, persistenceAttempted, failure.GetType().Name);
            var message = storedMedia is not null
                ? "Generated image bytes were stored. Their original node attachment could not be confirmed. Observe the stored media and operation; do not generate it again."
                : completed
                ? "The provider completed. Native image attachment was refused or could not be confirmed. Observe the original operation; do not generate it again."
                : invoked ? "The provider outcome is unconfirmed. Observe the original operation; do not generate it again."
                    : "The original image target or provider is unavailable. No generation was dispatched.";
            ProjectStructureNode? observed = null;
            if (!persistenceAttempted && !cancellationToken.IsCancellationRequested) {
                try {
                    RequireAuthority(origin.Authority);
                    observed = await workbench.UpdateContentMetadataAsync(origin.Authority.Project, expected,
                        Metadata(invoked ? ProjectStructureDeferredNodeCompletionState.RequiresObservation : ProjectStructureDeferredNodeCompletionState.Failed, message),
                        invoked ? "Image generation requires observation" : "Image generation refused", cancellationToken: cancellationToken);
                } catch (Exception observationFailure) {
                    logger.LogWarning("Image operation {OperationId} could not publish its failure to the original placeholder. FailureType={FailureType}",
                        request.OperationId, observationFailure.GetType().Name);
                }
            }
            return Result(false, message, observed);
        }

        string Metadata(ProjectStructureDeferredNodeCompletionState state, string message = "")
            => ProjectStructureDeferredCompletionMetadataFactory.BuildGeneratedImageMetadataJson(request.OperationId, state,
                imageRequest, provider, message, expected.MetadataJson, completed, hash);

        ProjectStructureDeferredNodeCompletionResult Result(bool success, string message, ProjectStructureNode? node)
            => new(request.OperationId, request.ProjectId, request.NodeId, request.Kind, success, message, node) {
                ProviderInvoked = invoked, ProviderCompleted = completed, PersistenceAttempted = persistenceAttempted, ContentSha256 = hash, StoredMedia = storedMedia
            };
    }

    private void RequireAuthority(ProjectStructureImageAuthority authority) {
        if (authority.IsRevoked || database.Profile.Profile.Id != authority.Project.DatabaseProfileId || database.Generation != authority.DatabaseGeneration) {
            throw new ProjectWriteAdmissionRejectedException(authority.Project);
        }
    }

    private async Task<ProviderProfile> RequireProviderAsync(ProjectStructureGeneratedImageCompletionRequest request, CancellationToken cancellationToken) {
        var provider = await providerSource.GetProviderAsync(request.ProviderProfileId, cancellationToken)
            ?? throw new InvalidOperationException("The original image provider is unavailable.");
        if (!provider.IsEnabled || provider.Purpose != ProviderProfilePurpose.ImageGeneration ||
            ProviderConfigurationFingerprintFactory.Create(provider) != request.ProviderFingerprint ||
            provider.IsSourceManaged && !provider.ModelCatalog.Any(model => model.Id == request.Model)) {
            throw new InvalidOperationException("The original image provider or selected model has changed.");
        }
        return provider;
    }

    private static string ValidateImage(AgentGeneratedImage image, AgentGeneratedImageFormat format) {
        ReadOnlySpan<byte> bytes = image.Bytes;
        if (bytes.Length is 0 or > 16 * 1024 * 1024) {
            throw new InvalidDataException("Generated image bytes exceed the supported bounds.");
        }
        var (contentType, valid) = format switch {
            AgentGeneratedImageFormat.Png => ("image/png", bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })),
            AgentGeneratedImageFormat.Jpeg => ("image/jpeg", bytes.Length > 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255),
            AgentGeneratedImageFormat.Webp => ("image/webp", bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)),
            _ => throw new InvalidDataException("Unsupported generated image format.")
        };
        if (!valid || !string.IsNullOrWhiteSpace(image.ContentType) && !string.Equals(image.ContentType.Trim(), contentType, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException("The generated image does not match its declared format.");
        }
        return contentType;
    }
}
