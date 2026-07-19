using CanDoItAll.Modules.Prompts;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class PromptGalleryApi
{
    public static RouteGroupBuilder MapPromptGalleryApi(this RouteGroupBuilder group)
    {
        var prompts = group.MapGroup("/prompt-gallery")
            .WithTags("Prompt Gallery")
            .DisableAntiforgery();

        prompts.MapGet("/items", async (
                [AsParameters] PromptGalleryApiQuery query,
                IPromptGalleryService gallery,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(async () => Results.Ok(await gallery.SearchAsync(query.ToDomain(), cancellationToken))))
            .WithName("SearchPromptGalleryItems");

        prompts.MapGet("/items/{promptId:guid}", async (
                Guid promptId,
                IPromptGalleryService gallery,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(
                await gallery.GetItemAsync(promptId, cancellationToken),
                "prompts.gallery.not-found"))
            .WithName("GetPromptGalleryItem");

        prompts.MapPost("/items", async (
                PromptGalleryDraft? request,
                IPromptGalleryService gallery,
                CancellationToken cancellationToken) =>
            request is null
                ? ApiEndpointResults.BadRequest("A prompt draft is required.", "prompt-gallery.request-required")
                : ApiEndpointResults.FromResult(
                    await gallery.SaveDraftAsync(request, cancellationToken),
                    "prompts.gallery.not-found"))
            .WithName("SavePromptGalleryDraft");

        prompts.MapPost("/items/{promptId:guid}/versions", async (
                Guid promptId,
                PromptVersionCreateRequest? request,
                IPromptGalleryService gallery,
                CancellationToken cancellationToken) =>
            request is null
                ? ApiEndpointResults.BadRequest("Version metadata is required.", "prompt-gallery.request-required")
                : ApiEndpointResults.FromResult(
                    await gallery.CreateVersionAsync(promptId, request, cancellationToken),
                    "prompts.gallery.not-found"))
            .WithName("CreatePromptGalleryVersion");

        prompts.MapGet("/items/{promptId:guid}/versions/{versionId:guid}", async (
                Guid promptId,
                Guid versionId,
                IPromptGalleryService gallery,
                CancellationToken cancellationToken) =>
        {
            var result = await gallery.GetVersionSnapshotAsync(versionId, cancellationToken);
            if (result.IsSuccess && result.Value?.PromptArtifactId != promptId)
            {
                return ApiEndpointResults.NotFound(
                    "Prompt Gallery version was not found for this item.",
                    "prompt-gallery.version-not-found");
            }

            return ApiEndpointResults.FromResult(result, "prompts.version.not-found");
        })
        .WithName("GetPromptGalleryVersion");

        prompts.MapPost("/items/{promptId:guid}/archive", async (
                Guid promptId,
                PromptGalleryArchiveRequest? request,
                IPromptGalleryService gallery,
                CancellationToken cancellationToken) =>
            request is null
                ? ApiEndpointResults.BadRequest("An archive request is required.", "prompt-gallery.request-required")
                : ApiEndpointResults.FromResult(
                    await gallery.ArchiveAsync(promptId, request.Archived, cancellationToken),
                    "prompts.gallery.not-found"))
            .WithName("ArchivePromptGalleryItem");

        prompts.MapPost("/items/{promptId:guid}/favorite", async (
                Guid promptId,
                PromptGalleryFavoriteRequest? request,
                IPromptGalleryService gallery,
                CancellationToken cancellationToken) =>
            request is null
                ? ApiEndpointResults.BadRequest("A favorite request is required.", "prompt-gallery.request-required")
                : ApiEndpointResults.FromResult(
                    await gallery.SetFavoriteAsync(promptId, request.Favorite, cancellationToken),
                    "prompts.gallery.not-found"))
            .WithName("SetPromptGalleryFavorite");

        prompts.MapPost("/compatibility/evaluate", async (
                PromptGalleryCompatibilityApiRequest? request,
                IPromptGalleryService gallery,
                CancellationToken cancellationToken) =>
            request?.Context is null
                ? ApiEndpointResults.BadRequest("A compatibility context is required.", "prompt-gallery.request-required")
                : ApiEndpointResults.FromResult(
                    await gallery.EvaluateCompatibilityAsync(
                        request.PromptArtifactId,
                        request.Context,
                        cancellationToken),
                    "prompts.gallery.not-found"))
            .WithName("EvaluatePromptGalleryCompatibility");

        prompts.MapPost("/warning-suppressions", async (
                PromptGalleryWarningSuppressionApiRequest? request,
                IPromptGalleryService gallery,
                CancellationToken cancellationToken) =>
            request is null
                ? ApiEndpointResults.BadRequest("A warning suppression request is required.", "prompt-gallery.request-required")
                : ApiEndpointResults.FromResult(
                    await gallery.SetWarningSuppressionAsync(
                        request.PromptArtifactId,
                        request.Consumer,
                        request.IssueCode,
                        request.Suppressed,
                        cancellationToken),
                    "prompts.gallery.not-found"))
            .WithName("SetPromptGalleryWarningSuppression");

        prompts.MapGet("/projection", async (
                IPromptGalleryProjectionCoordinator projection,
                CancellationToken cancellationToken) =>
            Results.Ok(await projection.GetStatusAsync(cancellationToken)))
            .WithName("GetPromptGalleryProjectionStatus");

        prompts.MapPost("/projection/rebuild", async (
                IPromptGalleryProjectionCoordinator projection,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(async () => Results.Ok(await projection.RebuildAsync(cancellationToken))))
            .WithName("RebuildPromptGalleryProjection");

        return prompts;
    }

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ArgumentException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "prompt-gallery.request-invalid");
        }
        catch (KeyNotFoundException exception)
        {
            return ApiEndpointResults.NotFound(exception.Message, "prompt-gallery.item-not-found");
        }
        catch (OverflowException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "prompt-gallery.request-invalid");
        }
    }
}

internal sealed record PromptGalleryApiQuery(
    string? Text = null,
    string[]? Tag = null,
    PromptGalleryItemKind? Kind = null,
    PromptArtifactStatus? Status = null,
    bool IncludeArchived = false,
    string? Provider = null,
    string? Model = null,
    PromptGalleryConsumer? Consumer = null,
    int PageIndex = 0,
    int PageSize = 25,
    bool FavoritesOnly = false)
{
    public PromptGalleryQuery ToDomain()
        => new(
            Text: Text,
            Tags: Tag,
            Kind: Kind,
            Status: Status,
            IncludeArchived: IncludeArchived,
            Provider: Provider,
            Model: Model,
            PageIndex: PageIndex,
            PageSize: PageSize,
            Consumer: Consumer,
            FavoritesOnly: FavoritesOnly);
}

internal sealed record PromptGalleryArchiveRequest(bool Archived = true);

internal sealed record PromptGalleryFavoriteRequest(bool Favorite = true);

internal sealed record PromptGalleryCompatibilityApiRequest(
    Guid PromptArtifactId,
    PromptGalleryConsumerContext? Context);

internal sealed record PromptGalleryWarningSuppressionApiRequest(
    Guid PromptArtifactId,
    PromptGalleryConsumer Consumer,
    PromptCompatibilityIssueCode IssueCode,
    bool Suppressed = true);
