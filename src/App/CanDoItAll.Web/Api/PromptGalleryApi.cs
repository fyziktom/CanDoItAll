using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Prompts;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class PromptGalleryApi
{
    public static RouteGroupBuilder MapPromptGalleryApi(this RouteGroupBuilder group)
    {
        var prompts = group.MapGroup("/prompt-gallery").WithApiSection(ApiAccessScopeNames.ReadPrompts, ApiAccessScopeNames.WritePrompts)
            .WithTags("Prompt Gallery")
            .DisableAntiforgery();

        prompts.MapGet("/items", SearchItemsAsync)
            .WithName("SearchPromptGalleryItems")
            .Produces<PromptGalleryPage<PromptGallerySearchItem>>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        prompts.MapGet("/items/{promptId:guid}", GetItemAsync)
            .WithName("GetPromptGalleryItem")
            .Produces<PromptGalleryItemDetails>()
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        prompts.MapPost("/items", SaveDraftAsync)
            .WithName("SavePromptGalleryDraft")
            .Produces<PromptDraftSaveReceipt>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        prompts.MapPost("/items/{promptId:guid}/versions", CreateVersionAsync)
            .WithName("CreatePromptGalleryVersion")
            .Produces<PromptVersionSnapshot>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        prompts.MapGet("/items/{promptId:guid}/versions/{versionId:guid}", GetVersionAsync)
            .WithName("GetPromptGalleryVersion")
            .Produces<PromptVersionSnapshot>()
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        prompts.MapPost("/items/{promptId:guid}/archive", ArchiveItemAsync)
            .WithName("ArchivePromptGalleryItem")
            .Produces<ApiAck>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        prompts.MapPost("/items/{promptId:guid}/favorite", SetFavoriteAsync)
            .WithName("SetPromptGalleryFavorite")
            .Produces<ApiAck>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        prompts.MapPost("/compatibility/evaluate", EvaluateCompatibilityAsync)
            .WithName("EvaluatePromptGalleryCompatibility")
            .Produces<PromptCompatibilityResult>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        prompts.MapPost("/warning-suppressions", SetWarningSuppressionAsync)
            .WithName("SetPromptGalleryWarningSuppression")
            .Produces<ApiAck>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        prompts.MapGet("/projection", GetProjectionStatusAsync)
            .WithName("GetPromptGalleryProjectionStatus")
            .Produces<PromptGalleryProjectionStatus>();

        prompts.MapPost("/projection/rebuild", RebuildProjectionAsync)
            .WithName("RebuildPromptGalleryProjection")
            .Produces<PromptGalleryProjectionOperationResult>();

        return prompts;
    }

    /// <summary>
    /// Search Prompt Gallery items by text, tags, kind, status, provider, model and consumer, one page at a time.
    /// </summary>
    /// <remarks>
    /// Reads the canonical Prompt Gallery items directly, so a saved change is found at once; the search projection is
    /// not used. Favorites come first, then items by last update (newest first), title and identifier. Archived items
    /// are excluded unless <c>IncludeArchived</c> is true. Each result carries the first 280 characters of the current
    /// draft as a preview: read <c>GET /api/prompt-gallery/items/{promptId}</c> for the whole item and
    /// <c>GET /api/prompt-gallery/items/{promptId}/versions/{versionId}</c> for the content of an immutable version.
    ///
    /// Filters are combined, and every filter is optional:
    ///
    /// - <c>Text</c> matches, case-insensitively as a substring, the title, summary, phase, draft content and source
    /// information, or part of a tag name.
    /// - Each <c>Tag</c> value must equal one of the item's tags, ignoring case and surrounding whitespace; an item
    /// must have all of them.
    /// - <c>Provider</c> and <c>Model</c> keep items that declare a matching supported model (case-insensitive) and
    /// items that declare no supported models.
    /// - <c>Consumer</c> keeps items that declare that consumer and items that declare no consumers.
    ///
    /// Paging is zero-based: request the next page while <c>pageIndex</c> + 1 is less than <c>totalPages</c>.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <param name="query">Text, tag, classification, compatibility and paging filters.</param>
    /// <response code="200">The requested page. An empty <c>items</c> array means no item matched.</response>
    /// <response code="400">
    /// A filter is invalid (<c>prompt-gallery.request-invalid</c>): negative page index, page size outside 1 through
    /// 100, text longer than 500 characters, more than 20 tags, a blank tag or one longer than 120 characters, an
    /// undefined kind, status or consumer, a provider longer than 120 or a model longer than 200 characters, or a page
    /// offset too large to compute. A query value the framework cannot convert is rejected with HTTP 400 without the
    /// <c>errors</c> envelope.
    /// </response>
    internal static async Task<IResult> SearchItemsAsync(
        [AsParameters] PromptGalleryApiQuery query,
        IPromptGalleryService gallery,
        CancellationToken cancellationToken)
        => await ExecuteAsync(async () => Results.Ok(await gallery.SearchAsync(query.ToDomain(), cancellationToken)));

    /// <summary>
    /// Read one Prompt Gallery item with its current draft, metadata and list of versions.
    /// </summary>
    /// <remarks>
    /// Returns the item's editable draft (<c>draftContent</c>), classification, tags, supported models and consumers,
    /// saved warning suppressions, model recommendations, source information and its immutable versions (newest first,
    /// without their content). Archived items are returned too. Keep <c>updatedAtUtc</c>: draft saves and version
    /// creation require it as the item's current concurrency value.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <param name="promptId">
    /// Identifier of the Prompt Gallery item, as returned by <c>GET /api/prompt-gallery/items</c> or in
    /// <c>promptArtifactId</c> of a draft save.
    /// </param>
    /// <response code="200">The item.</response>
    /// <response code="404">No item has this identifier (<c>prompts.gallery.not-found</c>).</response>
    internal static async Task<IResult> GetItemAsync(
        Guid promptId,
        IPromptGalleryService gallery,
        CancellationToken cancellationToken)
        => ApiEndpointResults.FromResult(
            await gallery.GetItemAsync(promptId, cancellationToken),
            "prompts.gallery.not-found");

    /// <summary>
    /// Create a Prompt Gallery item or save a new draft of an existing item.
    /// </summary>
    /// <remarks>
    /// Without <c>id</c> a new item is created; every such call creates another item. With <c>id</c> the item's draft
    /// and metadata are overwritten, which requires <c>expectedUpdatedAtUtc</c> equal to the item's current
    /// <c>updatedAtUtc</c> from <c>GET /api/prompt-gallery/items/{promptId}</c> or from the previous save. Tags,
    /// supported models and supported consumers are replaced by the lists sent; an omitted or null list clears them.
    ///
    /// A draft is not a version. Saving sets the item's status to Draft and restores an archived item; existing
    /// versions and the current version number do not change. Create an immutable version with
    /// <c>POST /api/prompt-gallery/items/{promptId}/versions</c> to make the item Final again.
    ///
    /// All request problems are reported together in <c>errors</c>. The response carries the new
    /// <c>updatedAtUtc</c>; use it for the next save or version.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <param name="request">The item's draft content and metadata; omit <c>id</c> to create an item.</param>
    /// <response code="200">
    /// The draft was saved. The body carries the item identifier and its new <c>updatedAtUtc</c>.
    /// </response>
    /// <response code="400">
    /// Nothing was saved. Request problems: <c>prompt-gallery.request-required</c> (no body),
    /// <c>prompts.gallery.id-invalid</c>, <c>prompts.gallery.expected-updated-at-required</c>,
    /// <c>prompts.gallery.title-invalid</c>, <c>prompts.gallery.summary-too-long</c>,
    /// <c>prompts.gallery.phase-too-long</c>, <c>prompts.gallery.content-invalid</c>,
    /// <c>prompts.gallery.content-too-long</c>, <c>prompts.gallery.kind-invalid</c>,
    /// <c>prompts.gallery.tags-invalid</c>, <c>prompts.gallery.models-invalid</c>,
    /// <c>prompts.gallery.models-duplicate</c>, <c>prompts.gallery.models-preferred-duplicate</c>,
    /// <c>prompts.gallery.consumers-invalid</c>, <c>prompts.gallery.temperature-invalid</c>,
    /// <c>prompts.gallery.max-output-tokens-invalid</c> or <c>prompts.gallery.top-p-invalid</c>. Stale state:
    /// <c>prompts.gallery.concurrency-conflict</c> (the item changed after <c>expectedUpdatedAtUtc</c>; read it again
    /// and reapply your change). A body the framework cannot bind is rejected with HTTP 400 without the <c>errors</c>
    /// envelope.
    /// </response>
    /// <response code="404">No item has the sent <c>id</c> (<c>prompts.gallery.not-found</c>).</response>
    internal static async Task<IResult> SaveDraftAsync(
        PromptGalleryDraft? request,
        IPromptGalleryService gallery,
        CancellationToken cancellationToken)
        => request is null
            ? ApiEndpointResults.BadRequest("A prompt draft is required.", "prompt-gallery.request-required")
            : ApiEndpointResults.FromResult(
                await gallery.SaveDraftAsync(request, cancellationToken),
                "prompts.gallery.not-found");

    /// <summary>
    /// Create the next immutable version of a Prompt Gallery item from its current draft.
    /// </summary>
    /// <remarks>
    /// Copies the item's current draft content, title, summary, kind and model recommendations into a new version with
    /// the next version number, and sets the item's status to Final. Versions never change afterwards; later draft
    /// saves only change the draft. <c>expectedUpdatedAtUtc</c> must equal the item's current <c>updatedAtUtc</c>, so
    /// that the version is created from the draft you reviewed.
    ///
    /// Archived items and items with an empty draft cannot get a version. The item's <c>updatedAtUtc</c> changes; read
    /// the item again before the next draft save.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <param name="promptId">Identifier of the Prompt Gallery item.</param>
    /// <param name="request">
    /// The reason for the version, the reviewed <c>updatedAtUtc</c> and the output format.
    /// </param>
    /// <response code="200">
    /// The version was created. The body is its snapshot, including <c>promptVersionId</c> and <c>versionNumber</c>.
    /// </response>
    /// <response code="400">
    /// No version was created: <c>prompt-gallery.request-required</c> (no body), <c>prompts.version.reason-invalid</c>,
    /// <c>prompts.version.output-format-invalid</c>, <c>prompts.version.expected-updated-at-required</c>,
    /// <c>prompts.version.artifact-archived</c>, <c>prompts.gallery.concurrency-conflict</c> (the item changed after
    /// you read it), <c>prompts.version.content-required</c> or <c>prompts.version.content-too-long</c>. A body the
    /// framework cannot bind is rejected with HTTP 400 without the <c>errors</c> envelope.
    /// </response>
    /// <response code="404">No item has this identifier (<c>prompts.gallery.not-found</c>).</response>
    internal static async Task<IResult> CreateVersionAsync(
        Guid promptId,
        PromptVersionCreateRequest? request,
        IPromptGalleryService gallery,
        CancellationToken cancellationToken)
        => request is null
            ? ApiEndpointResults.BadRequest("Version metadata is required.", "prompt-gallery.request-required")
            : ApiEndpointResults.FromResult(
                await gallery.CreateVersionAsync(promptId, request, cancellationToken),
                "prompts.gallery.not-found");

    /// <summary>
    /// Read one immutable version of a Prompt Gallery item, including its content.
    /// </summary>
    /// <remarks>
    /// Returns the content and the title, summary, kind, output format and model recommendations captured when the
    /// version was created. Version identifiers are listed in <c>versions</c> of
    /// <c>GET /api/prompt-gallery/items/{promptId}</c>. Versions of archived items stay readable.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <param name="promptId">Identifier of the Prompt Gallery item that owns the version.</param>
    /// <param name="versionId">Identifier of the version (<c>versions[].id</c> of the item read).</param>
    /// <response code="200">The version snapshot.</response>
    /// <response code="404">
    /// The version does not exist (<c>prompts.version.not-found</c>) or belongs to another item
    /// (<c>prompt-gallery.version-not-found</c>).
    /// </response>
    internal static async Task<IResult> GetVersionAsync(
        Guid promptId,
        Guid versionId,
        IPromptGalleryService gallery,
        CancellationToken cancellationToken)
    {
        var result = await gallery.GetVersionSnapshotAsync(versionId, cancellationToken);
        if (result.IsSuccess && result.Value?.PromptArtifactId != promptId)
        {
            return ApiEndpointResults.NotFound(
                "Prompt Gallery version was not found for this item.",
                "prompt-gallery.version-not-found");
        }

        return ApiEndpointResults.FromResult(result, "prompts.version.not-found");
    }

    /// <summary>
    /// Archive or restore a Prompt Gallery item.
    /// </summary>
    /// <remarks>
    /// <c>archived</c> true archives the item and false restores it. Archived items are left out of search unless
    /// <c>IncludeArchived</c> is set, cannot get new versions and are reported as not usable by the compatibility
    /// evaluation; their versions stay readable. Requesting the state the item already has succeeds without a change; a
    /// change updates the item's <c>updatedAtUtc</c>. Saving a draft also restores an archived item.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <param name="promptId">Identifier of the Prompt Gallery item.</param>
    /// <param name="request">The requested archive state.</param>
    /// <response code="200">The item has the requested archive state.</response>
    /// <response code="400">
    /// Nothing was changed: <c>prompt-gallery.request-required</c> (no body) or
    /// <c>prompts.gallery.concurrency-conflict</c> (the item changed at the same time; read it and try again). A body
    /// the framework cannot bind is rejected with HTTP 400 without the <c>errors</c> envelope.
    /// </response>
    /// <response code="404">No item has this identifier (<c>prompts.gallery.not-found</c>).</response>
    internal static async Task<IResult> ArchiveItemAsync(
        Guid promptId,
        PromptGalleryArchiveRequest? request,
        IPromptGalleryService gallery,
        CancellationToken cancellationToken)
        => request is null
            ? ApiEndpointResults.BadRequest("An archive request is required.", "prompt-gallery.request-required")
            : ApiEndpointResults.FromResult(
                await gallery.ArchiveAsync(promptId, request.Archived, cancellationToken),
                "prompts.gallery.not-found");

    /// <summary>
    /// Mark a Prompt Gallery item as a favorite or remove the mark.
    /// </summary>
    /// <remarks>
    /// Favorites are listed first in search and can be selected with <c>FavoritesOnly</c>. The mark is stored on the
    /// item itself, so it is the same for every caller of this host. Requesting the current value succeeds without a
    /// change; a change updates the item's <c>updatedAtUtc</c>.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <param name="promptId">Identifier of the Prompt Gallery item.</param>
    /// <param name="request">The requested favorite state.</param>
    /// <response code="200">The item has the requested favorite state.</response>
    /// <response code="400">
    /// Nothing was changed: <c>prompt-gallery.request-required</c> (no body) or
    /// <c>prompts.gallery.concurrency-conflict</c> (the item changed at the same time; read it and try again). A body
    /// the framework cannot bind is rejected with HTTP 400 without the <c>errors</c> envelope.
    /// </response>
    /// <response code="404">No item has this identifier (<c>prompts.gallery.not-found</c>).</response>
    internal static async Task<IResult> SetFavoriteAsync(
        Guid promptId,
        PromptGalleryFavoriteRequest? request,
        IPromptGalleryService gallery,
        CancellationToken cancellationToken)
        => request is null
            ? ApiEndpointResults.BadRequest("A favorite request is required.", "prompt-gallery.request-required")
            : ApiEndpointResults.FromResult(
                await gallery.SetFavoriteAsync(promptId, request.Favorite, cancellationToken),
                "prompts.gallery.not-found");

    /// <summary>
    /// Check whether a Prompt Gallery item can be used by a consumer with a given provider and model.
    /// </summary>
    /// <remarks>
    /// Evaluates the item against the consumer context without changing anything. The result lists the issues found;
    /// <c>canUse</c> is false when any issue has severity Error.
    ///
    /// - Archived: the item is archived; always an error.
    /// - MissingFinalVersion: the item has no version while the context needs one (purpose Execution or
    /// <c>requiresFinalVersion</c> true); an error.
    /// - ConsumerNotSupported: the item declares supported consumers and the context consumer is not one of them; an
    /// error.
    /// - ItemKindMismatch: <c>requiredKind</c> differs from the item's kind.
    /// - ProviderModelNotSupported: a provider or model is given, the item declares supported models and none matches
    /// (case-insensitive).
    ///
    /// The last two are warnings for purpose Selection, where a saved suppression for the consumer marks them
    /// <c>isSuppressed</c>, and errors for purpose Execution, where they cannot be suppressed.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <param name="request">The item to check and the consumer context.</param>
    /// <response code="200">
    /// The evaluation result; an empty <c>issues</c> array means the item fits the context.
    /// </response>
    /// <response code="400">
    /// Nothing was evaluated: <c>prompt-gallery.request-required</c> (no body or no <c>context</c>) or
    /// <c>prompts.compatibility.context-invalid</c> (undefined consumer, purpose or required kind, a provider longer
    /// than 120 or a model longer than 200 characters). A body the framework cannot bind is rejected with HTTP 400
    /// without the <c>errors</c> envelope.
    /// </response>
    /// <response code="404">No item has <c>promptArtifactId</c> (<c>prompts.gallery.not-found</c>).</response>
    internal static async Task<IResult> EvaluateCompatibilityAsync(
        PromptGalleryCompatibilityApiRequest? request,
        IPromptGalleryService gallery,
        CancellationToken cancellationToken)
        => request?.Context is null
            ? ApiEndpointResults.BadRequest("A compatibility context is required.", "prompt-gallery.request-required")
            : ApiEndpointResults.FromResult(
                await gallery.EvaluateCompatibilityAsync(
                    request.PromptArtifactId,
                    request.Context,
                    cancellationToken),
                "prompts.gallery.not-found");

    /// <summary>
    /// Save or remove the suppression of one compatibility warning of a Prompt Gallery item for one consumer.
    /// </summary>
    /// <remarks>
    /// A suppression marks one warning (ItemKindMismatch or ProviderModelNotSupported) of the item as reviewed for one
    /// consumer: Selection evaluations for that consumer still return the issue, with <c>isSuppressed</c> true, and
    /// Execution evaluations are not affected. <c>suppressed</c> true saves the suppression and false removes it; both
    /// succeed when the state is already as requested. Suppressions are stored per item and consumer, not per caller,
    /// are listed in <c>warningSuppressions</c> of the item read and do not change the item's <c>updatedAtUtc</c>.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <param name="request">The item, the consumer, the issue code and the requested state.</param>
    /// <response code="200">The suppression has the requested state.</response>
    /// <response code="400">
    /// Nothing was changed: <c>prompt-gallery.request-required</c> (no body) or
    /// <c>prompts.compatibility.issue-not-suppressible</c> (an undefined consumer or issue code, or an issue that is
    /// not ItemKindMismatch or ProviderModelNotSupported). A body the framework cannot bind is rejected with HTTP 400
    /// without the <c>errors</c> envelope.
    /// </response>
    /// <response code="404">No item has <c>promptArtifactId</c> (<c>prompts.gallery.not-found</c>).</response>
    internal static async Task<IResult> SetWarningSuppressionAsync(
        PromptGalleryWarningSuppressionApiRequest? request,
        IPromptGalleryService gallery,
        CancellationToken cancellationToken)
        => request is null
            ? ApiEndpointResults.BadRequest("A warning suppression request is required.", "prompt-gallery.request-required")
            : ApiEndpointResults.FromResult(
                await gallery.SetWarningSuppressionAsync(
                    request.PromptArtifactId,
                    request.Consumer,
                    request.IssueCode,
                    request.Suppressed,
                    cancellationToken),
                "prompts.gallery.not-found");

    /// <summary>
    /// Read the state of the Prompt Gallery search projection.
    /// </summary>
    /// <remarks>
    /// The search projection is a derived copy of the Final, not archived items that have a version, kept in the
    /// application's search index for the global search. The Prompt Gallery operations, including item search, read
    /// the canonical items and do not depend on it. In the default host configuration the projection is disabled:
    /// <c>enabled</c> is false and <c>health</c> is Disabled.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <response code="200">The projection driver, whether it is enabled, and its health.</response>
    internal static async Task<IResult> GetProjectionStatusAsync(
        IPromptGalleryProjectionCoordinator projection,
        CancellationToken cancellationToken)
        => Results.Ok(await projection.GetStatusAsync(cancellationToken));

    /// <summary>
    /// Rebuild the Prompt Gallery search projection from the canonical items.
    /// </summary>
    /// <remarks>
    /// Replaces all Prompt Gallery entries of the search index, in one transaction, with an entry for each Final, not
    /// archived item that has a version, using the content of its current version; when the rebuild fails the previous
    /// entries are kept. No Prompt Gallery item is changed. Use it after a projection update failed: item changes are
    /// kept when their projection update fails, and the failure is only logged.
    ///
    /// When the projection is disabled the call changes nothing and returns <c>state</c> Disabled with
    /// <c>processedCount</c> 0. The rebuild runs within the request.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <response code="200">
    /// The rebuild finished (<c>state</c> Applied, with the number of projected items) or was skipped because the
    /// projection is disabled (<c>state</c> Disabled).
    /// </response>
    internal static async Task<IResult> RebuildProjectionAsync(
        IPromptGalleryProjectionCoordinator projection,
        CancellationToken cancellationToken)
        => await ExecuteAsync(async () => Results.Ok(await projection.RebuildAsync(cancellationToken)));

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

/// <summary>
/// Query-string filters of <c>GET /api/prompt-gallery/items</c>. Every filter is optional.
/// </summary>
/// <param name="Text">
/// Text matched case-insensitively as a substring of the title, summary, phase, draft content and source information,
/// or of a tag name. Surrounding whitespace is ignored; at most 500 characters. Omitted or blank applies no text
/// filter.
/// </param>
/// <param name="Tag">
/// Tags an item must all have, each compared with the item's tags ignoring case and surrounding whitespace. Repeat the
/// parameter for several tags, for example <c>?Tag=review&amp;Tag=release</c>; at most 20 values of 1 to 120
/// characters.
/// </param>
/// <param name="Kind">
/// Item kind to include, as the case-sensitive member name or its integer value: 0 FullPrompt, 1 Part. Omitted
/// includes both kinds.
/// </param>
/// <param name="Status">
/// Item status to include, as the case-sensitive member name or its integer value: 0 Draft, 1 Final. Omitted includes
/// both.
/// </param>
/// <param name="IncludeArchived">True to include archived items. Omitted or false excludes them.</param>
/// <param name="Provider">
/// Provider name, for example <c>OpenAi</c>, compared case-insensitively with the declared supported models; at most
/// 120 characters. Items that declare no supported models always pass this filter.
/// </param>
/// <param name="Model">
/// Model name compared case-insensitively with the declared supported models; at most 200 characters. With
/// <c>Provider</c>, both must match the same supported model. Items that declare no supported models always pass.
/// </param>
/// <param name="Consumer">
/// Consumer that must be supported, as the case-sensitive member name or its integer value: 0 Workflow,
/// 1 AgentRuntime, 2 Chat, 3 ProjectWorkbench. Items that declare no supported consumers always pass. Omitted applies
/// no consumer filter.
/// </param>
/// <param name="PageIndex">Zero-based page number. Omitted means 0; must not be negative.</param>
/// <param name="PageSize">Number of items per page, from 1 through 100. Omitted means 25.</param>
/// <param name="FavoritesOnly">True to return only items marked as favorites. Omitted or false returns all.</param>
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

/// <summary>
/// Request of <c>POST /api/prompt-gallery/items/{promptId}/archive</c>.
/// </summary>
/// <param name="Archived">True archives the item and false restores it. Omitted means true.</param>
internal sealed record PromptGalleryArchiveRequest(bool Archived = true);

/// <summary>
/// Request of <c>POST /api/prompt-gallery/items/{promptId}/favorite</c>.
/// </summary>
/// <param name="Favorite">True marks the item as a favorite and false removes the mark. Omitted means true.</param>
internal sealed record PromptGalleryFavoriteRequest(bool Favorite = true);

/// <summary>
/// Request of <c>POST /api/prompt-gallery/compatibility/evaluate</c>: the item to check and the context it would be
/// used in.
/// </summary>
/// <param name="PromptArtifactId">
/// Identifier of the Prompt Gallery item to evaluate, as returned by search or in <c>promptArtifactId</c> of a draft
/// save.
/// </param>
/// <param name="Context">
/// The consumer, purpose, required kind and provider and model to check against. Required.
/// </param>
internal sealed record PromptGalleryCompatibilityApiRequest(
    Guid PromptArtifactId,
    PromptGalleryConsumerContext? Context);

/// <summary>
/// Request of <c>POST /api/prompt-gallery/warning-suppressions</c>: suppress or restore one compatibility warning of an
/// item for one consumer.
/// </summary>
/// <param name="PromptArtifactId">
/// Identifier of the Prompt Gallery item whose warning is suppressed or restored.
/// </param>
/// <param name="Consumer">
/// Consumer for which the warning is suppressed or restored, as a JSON integer: 0 Workflow, 1 AgentRuntime, 2 Chat,
/// 3 ProjectWorkbench.
/// </param>
/// <param name="IssueCode">
/// Warning to suppress or restore, as a JSON integer: 3 ItemKindMismatch or 4 ProviderModelNotSupported. The other
/// issue codes (0 Archived, 1 MissingFinalVersion, 2 ConsumerNotSupported) cannot be suppressed.
/// </param>
/// <param name="Suppressed">True saves the suppression and false removes it. Omitted means true.</param>
internal sealed record PromptGalleryWarningSuppressionApiRequest(
    Guid PromptArtifactId,
    PromptGalleryConsumer Consumer,
    PromptCompatibilityIssueCode IssueCode,
    bool Suppressed = true);
